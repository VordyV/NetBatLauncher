from fastapi import FastAPI
from fastapi.responses import Response
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
from .models import GameModel, FileManifestModel, FileModel, StorageTaskModel, StorageTaskStatus, GameClientModel, GameServerModel, GameServerDataModel
from .views import router
from .schemes import GameServerData
from .middlewares import LogMiddleware
from apscheduler.schedulers.asyncio import AsyncIOScheduler
from fastapi.middleware.trustedhost import TrustedHostMiddleware
from slugify import slugify
from .gs import get_server_list
import nbl_server
import os

class NetBatLauncherServer:

	def __init__(self, address: str, port: int, log_level: str ="info", storage: str = "local"):
		self.__address = address
		self.__port = port
		self.__fp_app = FastAPI(lifespan=self._fa_lifespan, openapi_url="/openapi.json" if nbl_server.__debug_mode__ else None, docs_url="/docs" if nbl_server.__debug_mode__ else None, redoc_url="/redoc" if nbl_server.__debug_mode__ else None, version=nbl_server.__version__, title="NBL Server", description="API server for the Net Bat Launcher")
		self.__http_server = uvicorn.Server(uvicorn.Config(self.__fp_app, port=self.__port, host=self.__address, log_level=log_level))
		self.__event_stop = asyncio.Event()
		self.__shell = AsyncSimpleShell()
		self.__storages = {
			"local": LocalStorage
		}
		self.__scheduler = AsyncIOScheduler()
		self.__game_servers: dict[str, dict[str, GameServerData]] = {}

		self.__fp_app.include_router(router)
		self.__fp_app.add_middleware(TrustedHostMiddleware)
		self.__fp_app.add_middleware(LogMiddleware)
		self.__fp_app.add_exception_handler(404, self._exc_404)

		self.__storage = self.__storages.get(storage)
		if not self.__storage: raise Exception(f"Storage '{storage}' does not exist")

		self.__shell.register("game.add", self._cmd_game_add, desc="Add a new game")
		self.__shell.register("game.rem", self._cmd_game_remove, desc="Delete a game")
		self.__shell.register("game.list", self._cmd_game_list, desc="Get the list of all games")
		self.__shell.register("game.files.upload", self._cmd_game_upload_files, desc="Upload game files")
		self.__shell.register("game.files.rem", self._cmd_game_remove_files, desc="Delete game files")
		self.__shell.register("client.add", self._cmd_game_client_add, desc="Add a new game client")
		self.__shell.register("client.rem", self._cmd_game_client_remove, desc="Delete a game client")
		self.__shell.register("client.list", self._cmd_game_clients_list, desc="Get the list of all game clients")
		self.__shell.register("client.files.upload", self._cmd_game_client_upload_files, desc="Upload game client files")
		self.__shell.register("client.files.rem", self._cmd_game_client_remove_files, desc="Delete game client files")
		self.__shell.register("gs.add", self._cmd_add_game_server, desc="Add a game server to a game client")
		self.__shell.register("gs.rem", self._cmd_rem_game_server, desc="Delete a game server of a game client")
		self.__shell.register("gs.list", self._cmd_game_server_list, desc="Get the list of game servers of a game client")
		self.__shell.register("help", self._on_cmd_help, desc="Get information about all commands")

		self.__scheduler.add_job(self._task_refresh_game_servers_data, "interval", None, hours=12, id="refresh_game_servers_data")

	async def _exc_404(self, request, exc):
		return Response(status_code=404)

	@property
	def storage(self) -> Storage: return self.__storage

	async def _task_refresh_game_servers_data(self):
		logger.debug("Refresh of the server list and their data...")

		async def func():
			servers = []
			for game in await GameModel.filter().all():
				for client in await GameClientModel.filter(game=game).all():
					if not client.master_server_address or not client.master_server_enctypex_key:
						continue
					try:
						data = await get_server_list(client.master_server_address, 28910, client.master_server_enctypex_key, 5.0)
						if not data: continue
						for serv in data["servers"]:
							servers.append(f"{serv["ip"]}:{serv["port"]}")
							await GameServerDataModel.filter(address=serv["ip"], query_port=serv["port"]).delete()
							await GameServerDataModel.create(client=client, address=serv["ip"], query_port=serv["port"], name=serv.get("data", {}).get("hostname"))
					except Exception as e:
						logger.debug(f"Failed to refresh the data for the servers of client '{client.ident}': {e}")
						continue
			for serv in await GameServerModel.filter().all():
				if f"{serv.address}:{serv.query_port}" not in servers:
					await serv.delete()
			logger.debug(f"Refresh completed. Servers {len(servers)}")

		task = asyncio.create_task(func())

	async def _get_game(self, gameid: str) -> GameModel:
		game = await GameModel.get_or_none(ident=gameid)
		if not game: raise Exception(f"Game '{gameid}' does not exist")
		return game

	async def _get_game_client(self, gameid: str, clientid: str) -> GameClientModel:
		try:
			return await GameClientModel.get(ident=clientid, game__ident=gameid)
		except Exception:
			raise Exception(f"Game client '{clientid}' does not exist")

	async def _get_game_server(self, gameid: str, clientid: str, serverid: str) -> GameServerModel:
		try:
			return await GameServerModel.get(ident=serverid, client__ident=clientid, client__game__ident=gameid)
		except Exception:
			raise Exception(f"Game server '{serverid}' does not exist")

	async def _on_cmd_help(self):
		return self.__shell.beautiful_help

	async def _cmd_game_server_list(self, gameid: str, clientid: str):
		rows = ["id\tname\tshort name\tcreated at\tmodified\taddress\tquery port"]

		client = await self._get_game_client(gameid, clientid)

		for server in await GameServerModel.filter(client=client).all():
			rows.append(f"{server.ident}\t{server.name}\t{server.created_at}\t{server.modified}\t{server.address}\t{server.query_port}")

		if len(rows) < 2:
			return "No servers"
		return "\n".join(rows)

	async def _cmd_add_game_server(self, gameid: str, clientid: str, serverid: str, address: str, query_port: int, name: str):
		serverid = slugify(serverid)
		client = await self._get_game_client(gameid, clientid)

		if await GameServerModel.filter(ident=serverid).exists(): raise Exception(f"A game server '{serverid}' already exists")

		await GameServerModel.create(ident=serverid, address=address, query_port=query_port, name=name, client=client)
		return f"Game server '{serverid}' added"

	async def _cmd_rem_game_server(self, gameid: str, clientid: str, serverid: str):
		gs = await self._get_game_server(gameid, clientid, serverid)
		if not gs: raise Exception(f"Game server '{serverid}' does not exist")
		await gs.delete()
		return f"Game server '{serverid}' deleted"

	async def _cmd_game_client_add(self, gameid: str, clientid: str, name: str, shortname: str, msaddress: str = None, msenckey: str = None):
		clientid = slugify(clientid)
		game = await self._get_game(gameid)

		if await GameClientModel.filter(ident=clientid, game=game).exists(): raise Exception(f"A game client with id '{clientid}' already exists")
		await GameClientModel.create(ident=clientid, game=game, name=name, short_name=shortname, master_server_address=msaddress, master_server_enctypex_key=msenckey)
		return f"Game client '{clientid}' added"

	async def _cmd_game_client_remove(self, gameid: str, clientid: str):
		client = await self._get_game_client(gameid, clientid)

		await client.delete()
		return f"Game client '{clientid}' deleted"

	async def _cmd_game_clients_list(self, gameid: str):
		rows = ["id\tname\tshort name\tcreated at\tmodified\tfiles\tms address"]

		game = await self._get_game(gameid)

		for client in await GameClientModel.filter(game=game).all():
			rows.append(f"{client.ident}\t{client.name}\t{client.short_name}\t{client.created_at}\t{client.modified}\t{"yes" if client.file_manifest else "no"}\t{client.master_server_address}")

		if len(rows) < 2:
			return "No clients"
		return "\n".join(rows)

	async def _cmd_game_client_upload_files(self, gameid: str, clientid: str, path: str):
		client = await self._get_game_client(gameid, clientid)

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
		client = await self._get_game_client(gameid, clientid)

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

	async def _cmd_game_add(self, gameid: str, name: str, shortname: str):
		gameid = slugify(gameid)
		if await GameModel.filter(ident=gameid).exists(): raise Exception(f"A game with id '{gameid}' already exists")
		await GameModel.create(ident=gameid, name=name, short_name=shortname)
		return f"Game '{gameid}' added"

	async def _cmd_game_remove(self, gameid: str):
		game = await self._get_game(gameid)
		await game.delete()
		return f"Game '{gameid}' deleted"

	async def _cmd_game_list(self):
		rows = ["id\tname\tshort name\tcreated at\tmodified\tfiles"]

		for game in await GameModel.filter().all():
			rows.append(f"{game.ident}\t{game.name}\t{game.short_name}\t{game.created_at}\t{game.modified}\t{"yes" if game.file_manifest else "no"}")

		if len(rows) < 2:
			return "No games"
		return "\n".join(rows)

	async def _cmd_game_upload_files(self, gameid: str, path: str):
		game = await self._get_game(gameid)

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

	async def _cmd_game_remove_files(self, gameid: str):
		game = await self._get_game(gameid)
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
		await RegisterTortoise(db_url=os.getenv("DB_URL", "sqlite://db.sqlite3"), modules={'models': ['nbl_server.models']})
		await Tortoise.generate_schemas(safe=True)
		self.__scheduler.start()
		await self._task_refresh_game_servers_data()
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