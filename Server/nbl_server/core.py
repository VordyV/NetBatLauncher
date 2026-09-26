from fastapi import FastAPI
import uvicorn
import asyncio
from prompt_toolkit.patch_stdout import patch_stdout
from prompt_toolkit.shortcuts import PromptSession
from loguru import logger
from contextlib import asynccontextmanager
from tortoise.contrib.fastapi import RegisterTortoise
from tortoise import Tortoise
from callixir import AsyncSimpleShell
from .storage import LocalStorage, Storage
from .models import GameModel, FileManifestModel, FileModel, StorageTaskModel, StorageTaskStatus, GameClientModel, GameServerModel
from .views import router
from .schemes import GameServerData
from apscheduler.schedulers.asyncio import AsyncIOScheduler

class NetBatLauncherServer:

	def __init__(self, address: str, port: int, log_level: str ="info", storage: str = "local"):
		self.__address = address
		self.__port = port
		self.__fp_app = FastAPI(lifespan=self._fa_lifespan)
		self.__http_server = uvicorn.Server(uvicorn.Config(self.__fp_app, port=self.__port, host=self.__address, log_level=log_level))
		self.__event_stop = asyncio.Event()
		self.__shell = AsyncSimpleShell()
		self.__storages = {
			"local": LocalStorage
		}
		self.__scheduler = AsyncIOScheduler()
		self.__game_servers: dict[str, GameServerData] = {}

		self.__fp_app.include_router(router)

		self.__storage = self.__storages.get(storage)
		if not self.__storage: raise Exception(f"Storage '{storage}' does not exist")

		self.__shell.register("game.add", self._cmd_game_add)
		self.__shell.register("game.rem", self._cmd_game_remove)
		self.__shell.register("game.list", self._cmd_game_list)
		self.__shell.register("game.files.upload", self._cmd_game_upload_files)
		self.__shell.register("game.files.rem", self._cmd_game_remove_files)
		self.__shell.register("client.add", self._cmd_game_client_add)
		self.__shell.register("client.rem", self._cmd_game_client_remove)
		self.__shell.register("client.list", self._cmd_game_clients_list)
		self.__shell.register("client.files.upload", self._cmd_game_client_upload_files)
		self.__shell.register("client.files.rem", self._cmd_game_client_remove_files)
		self.__shell.register("gs.add", self._cmd_add_game_server)
		self.__shell.register("gs.rem", self._cmd_rem_game_server)
		self.__shell.register("gs.list", self._cmd_game_server_list)

	@property
	def storage(self) -> Storage: return self.__storage

	async def _cmd_game_server_list(self, gameid: str, clientid: str):
		rows = ["id\tname\tshort name\tcreated at\tmodified\taddress\tquery port"]

		game = await GameModel.get_or_none(ident=gameid)
		if not game: raise Exception(f"Game '{gameid}' does not exist")

		client = await GameClientModel.get_or_none(ident=clientid, game=game)
		if not client: raise Exception(f"Game client '{clientid}' does not exist")

		for server in await GameServerModel.filter(client=client).all():
			rows.append(f"{server.ident}\t{server.name}\t{server.created_at}\t{server.modified}\t{server.address}\t{server.query_port}")

		if len(rows) < 2:
			return "No servers"
		return "\n".join(rows)

	async def _cmd_add_game_server(self,  clientid: str, serverid: str, address: str, query_port: int, name: str):
		client = await GameClientModel.get_or_none(ident=clientid)
		if not client: raise Exception(f"Game client '{clientid}' does not exist")

		if await GameServerModel.filter(ident=serverid).exists(): raise Exception(f"A game server '{serverid}' already exists")

		await GameServerModel.create(ident=serverid, address=address, query_port=query_port, name=name, client=client)
		return f"Game server '{serverid}' added"

	async def _cmd_rem_game_server(self, serverid: str):

		gs = await GameServerModel.get_or_none(ident=serverid)
		if not gs: raise Exception(f"Game server '{serverid}' does not exist")

		await gs.delete()

	async def _cmd_game_client_add(self, gameid: str, clientid: str, name: str, shortname: str, msaddress: str = None, msenckey: str = None):
		game = await GameModel.get_or_none(ident=gameid)
		if not game: raise Exception(f"Game '{gameid}' does not exist")

		if await GameClientModel.filter(ident=clientid, game=game).exists(): raise Exception(f"A game client with id '{clientid}' already exists")
		await GameClientModel.create(ident=clientid, game=game, name=name, short_name=shortname, master_server_address=msaddress, master_server_enctypex_key=msenckey)
		return f"Game client '{clientid}' added"

	async def _cmd_game_client_remove(self, gameid: str, clientid: str):
		game = await GameModel.get_or_none(ident=gameid)
		if not game: raise Exception(f"Game '{gameid}' does not exist")

		client = await GameClientModel.get_or_none(ident=clientid)
		if not client: raise Exception(f"Game client '{clientid}' does not exist")

		await client.delete()
		return f"Game client '{clientid}' deleted"

	async def _cmd_game_clients_list(self, gameid: str):
		rows = ["id\tname\tshort name\tcreated at\tmodified\tfiles\tms address"]

		game = await GameModel.get_or_none(ident=gameid)
		if not game: raise Exception(f"Game '{gameid}' does not exist")

		for client in await GameClientModel.filter(game=game).all():
			rows.append(f"{client.ident}\t{client.name}\t{client.short_name}\t{client.created_at}\t{client.modified}\t{"yes" if client.file_manifest else "no"}\t{client.master_server_address}")

		if len(rows) < 2:
			return "No clients"
		return "\n".join(rows)

	async def _cmd_game_client_upload_files(self, gameid: str, clientid: str, path: str):
		game = await GameModel.get_or_none(ident=gameid)
		if not game: raise Exception(f"Game '{gameid}' does not exist")

		client = await GameClientModel.get_or_none(ident=clientid)
		if not client: raise Exception(f"Game client '{clientid}' does not exist")

		ident = await self.storage.add_files_via_path(path)

		print("Adding files to storage...")
		storage_task = self.storage.get_task(ident)
		await storage_task.task
		task = await StorageTaskModel.get(ident=ident)
		if task.status != StorageTaskStatus.Done: raise Exception(task.error)
		manifest = await FileManifestModel.get(ident=ident)
		client.file_manifest = manifest
		await client.save()
		return f"Manifest '{storage_task.ident}' created"

	async def _cmd_game_client_remove_files(self, gameid: str, clientid: str):
		game = await GameModel.get_or_none(ident=gameid)
		if not game: raise Exception(f"Game '{gameid}' does not exist")

		client = await GameClientModel.get_or_none(ident=clientid)
		if not client: raise Exception(f"Game client '{gameid}' does not exist")

		if not client.file_manifest: raise Exception(f"Game client '{clientid}' has no files")

		manifest = await FileManifestModel.get(id=client.file_manifest_id)
		m_id = manifest.ident

		print("Deleting files from storage...")
		for file in await FileModel.filter(manifest=manifest).all():
			await self.storage.remove(file.ident)
			await file.delete()

		client.file_manifest = None
		await client.save()
		await manifest.delete()
		return f"Manifest '{m_id}' deleted"

	async def _cmd_game_add(self, ident: str, name: str, shortname: str):
		if await GameModel.filter(ident=ident).exists(): raise Exception(f"A game with id '{ident}' already exists")
		await GameModel.create(ident=ident, name=name, short_name=shortname)
		return f"Game '{ident}' added"

	async def _cmd_game_remove(self, ident: str):
		game = await GameModel.get_or_none(ident=ident)
		if not game: raise Exception(f"Game '{ident}' does not exist")
		await game.delete()
		return f"Game '{ident}' deleted"

	async def _cmd_game_list(self):
		rows = ["id\tname\tshort name\tcreated at\tmodified\tfiles"]

		for game in await GameModel.filter().all():
			rows.append(f"{game.ident}\t{game.name}\t{game.short_name}\t{game.created_at}\t{game.modified}\t{"yes" if game.file_manifest else "no"}")

		if len(rows) < 2:
			return "No games"
		return "\n".join(rows)

	async def _cmd_game_upload_files(self, gameid: str, path: str):
		game = await GameModel.get_or_none(ident=gameid)
		if not game: raise Exception(f"Game '{gameid}' does not exist")

		ident = await self.storage.add_files_via_path(path)

		print("Adding files to storage...")
		storage_task = self.storage.get_task(ident)
		await storage_task.task
		task = await StorageTaskModel.get(ident=ident)
		if task.status != StorageTaskStatus.Done: raise Exception(task.error)
		manifest = await FileManifestModel.get(ident=ident)
		game.file_manifest = manifest
		await game.save()
		return f"Manifest '{storage_task.ident}' created"

	async def _cmd_game_remove_files(self, gameid):
		game = await GameModel.get_or_none(ident=gameid)
		if not game: raise Exception(f"Game '{gameid}' does not exist")
		if not game.file_manifest: raise Exception(f"Game '{gameid}' has no files")

		manifest = await FileManifestModel.get(id=game.file_manifest_id)
		m_id = manifest.ident

		print("Deleting files from storage...")
		for file in await FileModel.filter(manifest=manifest).all():
			await self.storage.remove(file.ident)
			await file.delete()

		game.file_manifest = None
		await game.save()
		await manifest.delete()
		return f"Manifest '{m_id}' deleted"

	async def _task_update_gs_list(self):
		try:
			for client in await GameClientModel.filter().all():
				if not client.master_server_address or not client.master_server_enctypex_key: continue
		except Exception as e:
			print(e)

	@asynccontextmanager
	async def _fa_lifespan(self, app: FastAPI):
		app.state.core = self
		await RegisterTortoise(db_url='sqlite://db.sqlite3', modules={'models': ['nbl_server.models']})
		await Tortoise.generate_schemas(safe=True)
		self.__scheduler.start()
		yield
		self.__scheduler.shutdown(wait=False)
		await Tortoise.close_connections()

	async def _inter_shell(self):
		session = PromptSession("")
		string = ""
		command = None
		while True:
			try:
				string = await session.prompt_async()
				if string.strip() == "": continue
				command = await self.__shell.execute(string)
				if command.error: print(command.error)
				else: print(command.result)
			except (EOFError, KeyboardInterrupt):
				self.__event_stop.set()
				return

	async def _loop(self):
		with patch_stdout(raw=True):
			logger.info("Start")
			task_shell = asyncio.create_task(self._inter_shell())
			task_http_server = asyncio.create_task(self.__http_server.serve())

			await self.__event_stop.wait()

			self.__http_server.should_exit = True
			await task_http_server
			logger.info("Stop")

	def start(self):
		asyncio.run(self._loop())