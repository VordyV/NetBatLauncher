import datetime
import ipaddress
import pathlib
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
from tortoise.context import TortoiseContext
from tortoise.migrations.autodetector import MigrationAutodetector
from tortoise.migrations.api import migrate
from callixir import AsyncSimpleShell
from .storage import LocalStorage, Storage
from .models import GameModel, FileManifestModel, FileModel, StorageTaskModel, StorageTaskStatus, GameClientModel, GameServerModel, GameServerDataModel, GameModModel, AccountModel, SessionAccountModel, ChronicleModel, MultiLangText
from .views import router
from .schemes import GameServerData
from .middlewares import LogMiddleware
from apscheduler.schedulers.asyncio import AsyncIOScheduler
from fastapi.middleware.trustedhost import TrustedHostMiddleware
from slugify import slugify
from .gs import get_server_list
from .config import TORTOISE_ORM
from starlette_admin.contrib.tortoise import Admin
from .admin_view import GameView, GameClientsView, GameModsView, GameServerView, AccountView, SessionView, FileManifestView, FileView, GameFileUploadView, GameClientFileUploadView, GeneralView
from .admin_auth_providers import StanAuthProvider
from starlette.middleware.sessions import SessionMiddleware
from starlette_admin import TextWidget, HtmlWidget, CustomView, DropDown
from .chronicle_action_type import ChronicleActionType
from .server_status import ServerStatus
from opengsq.protocols.battlefield2 import Battlefield2
import nbl_server
import os
import re
import httpx
import bcrypt

class NetBatLauncherServer:

	def __init__(self, address: str, port: int, steam_api_key: str, log_level: str ="info", storage: str = "local"):
		self.__address = address
		self.__port = port
		self.__fp_app = FastAPI(lifespan=self._fa_lifespan, openapi_url="/openapi.json" if nbl_server.__debug_mode__ else None, docs_url="/docs" if nbl_server.__debug_mode__ else None, redoc_url="/redoc" if nbl_server.__debug_mode__ else None, version=nbl_server.__version__, title="NBL Server", description="API server for the Net Bat Launcher")
		self.__http_server = uvicorn.Server(uvicorn.Config(self.__fp_app, proxy_headers=True, forwarded_allow_ips="*", port=self.__port, host=self.__address, log_level=log_level))
		self.__event_stop = asyncio.Event()
		self.__shell = AsyncSimpleShell()
		self.__storages = {
			"local": LocalStorage
		}
		self.__scheduler = AsyncIOScheduler()
		self.__game_servers: dict[str, dict[str, GameServerData]] = {}
		self.__one_time_codes: dict[str, str] = {}
		self.__used_nonces_auth = set()
		self.__steam_api_key = steam_api_key
		self.__admin_panel = Admin(title="Net Bat 2142 Admin", index_view=GeneralView(self, icon="fa fa-newspaper"), templates_dir="templates", auth_provider=StanAuthProvider(), secret_key=os.getenv("SECRET_KEY"))
		self.__status: ServerStatus = ServerStatus.ok
		self.__player_count: dict[str, int] = {}

		self.__fp_app.include_router(router)
		self.__fp_app.add_middleware(TrustedHostMiddleware)
		self.__fp_app.add_middleware(LogMiddleware)
		self.__fp_app.add_middleware(SessionMiddleware, secret_key=os.getenv("SECRET_KEY"))
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
		self.__shell.register("client.setref", self._on_cmd_client_set_ref, desc="Set the game client as the reference")
		self.__shell.register("gs.add", self._cmd_add_game_server, desc="Add a game server to a game client")
		self.__shell.register("gs.rem", self._cmd_rem_game_server, desc="Delete a game server of a game client")
		self.__shell.register("gs.list", self._cmd_game_server_list, desc="Get the list of game servers of a game client")
		self.__shell.register("help", self._on_cmd_help, desc="Get information about all commands")
		self.__shell.register("mod.list", self._on_cmd_mod_list, desc="Get the list of game mods")
		self.__shell.register("mod.add", self._cmd_add_game_mod, desc="Add a game mod")
		self.__shell.register("mod.rem", self._cmd_rem_game_mod, desc="Delete a game mod")
		self.__shell.register("mod.files.upload", self._cmd_game_mod_upload_files, desc="Upload game mod files")
		self.__shell.register("mod.files.rem", self._cmd_game_mod_remove_files, desc="Delete game mod files")
		self.__shell.register("mod.setdef", self._on_cmd_mod_set_def, desc="Set the game mod as the default")
		self.__shell.register("mig", self._on_cmd_migrate, desc="Create a database changes migration")
		self.__shell.register("appmig", self._on_cmd_apply_migrate, desc="Apply database migration changes")
		self.__shell.register("acc.reg", self._on_cmd_register_account, desc="")

		self.__scheduler.add_job(self._task_refresh_game_servers_data, "interval", None, hours=1, next_run_time=datetime.datetime.now() + datetime.timedelta(seconds=120), id="refresh_game_servers_data")
		self.__scheduler.add_job(self._task_refresh_player_count, "interval", None, seconds=120, id="refresh_game_player_count")

		Tortoise.init_models(["nbl_server.models"], "models")
		self.__admin_panel.add_view(GeneralView(self, icon="fa fa-newspaper"))
		self.__admin_panel.add_view(DropDown("Games", icon="fa fa-newspaper", views=[GameView(self, GameModel, icon="fa fa-newspaper"), GameFileUploadView(self)]))
		self.__admin_panel.add_view(DropDown("Game clients", icon="fa fa-newspaper", views=[GameClientsView(self, GameClientModel, icon="fa fa-newspaper"), GameClientFileUploadView(self)]))
		self.__admin_panel.add_view(GameModsView(self,GameModModel, icon="fa fa-newspaper"))
		self.__admin_panel.add_view(GameServerView(self,GameServerModel, icon="fa fa-newspaper"))
		self.__admin_panel.add_view(AccountView(self,AccountModel, icon="fa fa-newspaper"))
		self.__admin_panel.add_view(SessionView(self,SessionAccountModel, icon="fa fa-newspaper"))
		self.__admin_panel.add_view(FileManifestView(self,FileManifestModel, icon="fa fa-newspaper"))
		self.__admin_panel.add_view(FileView(self, FileModel, icon="fa fa-newspaper"))
		self.__admin_panel.mount_to(self.__fp_app)

	async def _exc_404(self, request, exc):
		return Response(status_code=404)

	@property
	def storage(self) -> Storage: return self.__storage

	@property
	def status(self) -> ServerStatus: return self.__status

	@property
	def player_count(self) -> dict[str, int]: return self.__player_count

	def write_chronicle(self, account_executor: AccountModel, action_type: ChronicleActionType, address: ipaddress.IPv4Address, agent: str, account_target: AccountModel | None = None, action_desc: str | None = None):
		task = asyncio.create_task(ChronicleModel.create(account_executor_uuid=account_executor.ident, account_target_uuid=account_target.ident, action_desc=action_desc, action_type=action_type, address=str(address), agent=agent))

	async def verify_steam_openid(self, params: dict) -> bool:
		if params.get("openid.mode") != "id_res":
			return False

		signed = params.get("openid.signed", "")
		if not signed:
			return False

		data = {
			"openid.ns": "http://specs.openid.net/auth/2.0",
			"openid.mode": "check_authentication",
			"openid.assoc_handle": params.get("openid.assoc_handle", ""),
			"openid.signed": signed,
			"openid.sig": params.get("openid.sig", ""),
		}

		for item in signed.split(","):
			key = f"openid.{item}"
			value = params.get(key) or params.get(key.replace(".", "_"))
			if value is not None:
				data[key] = value

		async with httpx.AsyncClient(timeout=10.0) as client:
			response = await client.post(
				"https://steamcommunity.com/openid/login",
				data=data,
				headers={"Content-Type": "application/x-www-form-urlencoded"},
			)

		if response.status_code != 200:
			return False

		return "is_valid:true" in response.text

	async def get_steam_data(self, steam_id: str) -> dict:
		async with httpx.AsyncClient(timeout=5.0) as client:
			response = await client.get(f"https://api.steampowered.com/ISteamUser/GetPlayerSummaries/v0002?key={self.__steam_api_key}&steamids={steam_id}")
			if response.status_code != 200:
				return dict()

			data = response.json()
			players = data.get("response", {}).get("players", [])

			if not players: return dict()
			return players[0]

	def is_nonce_auth_valid(self, nonce: str, max_age_minutes: int = 15) -> bool:
		if not nonce:
			return False

		match = re.match(r"^(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z)", nonce)
		if not match:
			return False

		try:
			nonce_time = datetime.datetime.strptime(match.group(1), "%Y-%m-%dT%H:%M:%SZ")
			nonce_time = nonce_time.replace(tzinfo=datetime.timezone.utc)
		except ValueError:
			return False

		now = datetime.datetime.now(datetime.timezone.utc)
		if now - nonce_time > datetime.timedelta(minutes=max_age_minutes):
			return False

		if nonce in self.__used_nonces_auth:
			return False

		self.__used_nonces_auth.add(nonce)
		return True

	async def _task_remove_one_time_code(self, code: str):
		if code in self.__one_time_codes:
			del self.__one_time_codes[code]

	def create_one_time_code(self, value: str) -> str:
		code = os.urandom(8).hex()
		self.__one_time_codes[code] = value
		self.__scheduler.add_job(self._task_remove_one_time_code, 'date', args=(code,), run_date=datetime.datetime.now() + datetime.timedelta(seconds=15), id=f"clear_otc_{code}")
		return code

	def get_value_one_time_code(self, otc: str) -> str | None: # otc = one time code
		token = self.__one_time_codes.get(otc)
		if token:
			if self.__scheduler.get_job(f"clear_otc_{otc}"):
				self.__scheduler.remove_job(f"clear_otc_{otc}")
			del self.__one_time_codes[otc]
			return token
		return None

	async def _task_refresh_player_count(self):

		async def func():
			for client in await GameClientModel.filter().all():

				num = 0
				for server in await GameServerDataModel.filter(client=client).all():
					try:
						gs3 = Battlefield2(host=server.address, port=server.query_port, timeout=3.0)
						data = await gs3.get_status()
						num += int(data.info.get("numplayers", 0))
					except TimeoutError:
						continue
					except Exception as e:
						logger.exception(e)

				self.__player_count[client.ident] = num

			print(self.__player_count)

		task = asyncio.create_task(func())

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

	async def _get_game_mod(self, gameid: str, modid: str) -> GameModModel:
		try:
			return await GameModModel.get(ident=modid, game__ident=gameid)
		except Exception:
			raise Exception(f"Game mod '{modid}' does not exist")

	async def _on_cmd_register_account(self, name: str, password: str, op: bool = False):
		if await AccountModel.filter(name=name).exists():
			return "Such an account has already been created"

		hashed = await asyncio.to_thread(bcrypt.hashpw, password.encode(), bcrypt.gensalt())

		account = await AccountModel.create(name=name, op=op, password=hashed.decode())
		return f"A new account '{account.ident}' has been created"

	async def _on_cmd_migrate(self):
		migrations_dir = pathlib.Path("app/migrations")
		migrations_dir.mkdir(parents=True, exist_ok=True)

		init_file = migrations_dir / "__init__.py"
		init_file.touch(exist_ok=True)

		async with TortoiseContext() as ctx:
			await ctx.init(config=TORTOISE_ORM)

			autodetector = MigrationAutodetector(ctx.apps, TORTOISE_ORM.get("apps"))

			writers = await autodetector.changes()

			if not writers:
				return "No model changes detected"

			for writer in writers:
				path = writer.write()
				print(f"Migration '{path}' created")

			return "Done."

	async def _on_cmd_apply_migrate(self):
		try:
			await migrate(config=TORTOISE_ORM)
			return f"Changes applied"
		except Exception as e:
			return f"Changes were not applied: {e}"

	async def _on_cmd_mod_list(self, gameid: str):
		rows = ["id\tname\tversion\tdeveloper\tcreated_at\tmodified\tis default\tis files"]

		game = await self._get_game(gameid)

		for mod in await GameModModel.filter(game=game).all():
			rows.append(f"{mod.ident}\t{mod.name}\t{mod.version}\t{mod.developer}\t{mod.created_at}\t{mod.modified}\t{"yes" if mod.default else "no"}\t{"yes" if bool(mod.file_manifest) else "no"}")

		if len(rows) < 2:
			return "No mods"
		return "\n".join(rows)

	async def _cmd_add_game_mod(self, gameid: str, modid: str, name: str, version: str, developer: str):
		modid = slugify(modid)
		game = await self._get_game(gameid)

		if await GameModModel.filter(ident=modid, game=game).exists(): raise Exception(f"A game mod '{modid}' already exists")

		await GameModModel.create(ident=modid, game=game, name=name, version=version, developer=developer)
		return f"Game mod '{modid}' added"

	async def _cmd_rem_game_mod(self, gameid: str, modid: str):
		mod = await self._get_game_mod(gameid, modid)
		if not mod: raise Exception(f"Game mod '{mod}' does not exist")
		await mod.delete()
		return f"Game mod '{modid}' deleted"

	async def _cmd_game_mod_upload_files(self, gameid: str, modid: str, path: str):
		mod = await self._get_game_mod(gameid, modid)

		ident = await self.storage.add_files_via_path(path)

		print("Adding files to storage...")
		storage_task = self.storage.get_task(ident)
		await storage_task.task
		task = await StorageTaskModel.get(ident=ident)
		if task.status != StorageTaskStatus.Done: raise Exception(task.error)
		manifest = await FileManifestModel.get(ident=ident)
		mod.file_manifest = manifest
		await mod.save()
		return f"Manifest '{storage_task.ident}' created"

	async def _cmd_game_mod_remove_files(self, gameid: str, modid: str):
		mod = await self._get_game_mod(gameid, modid)

		if not mod.file_manifest: raise Exception(f"Game mod '{modid}' has no files")

		manifest = await FileManifestModel.get(id=mod.file_manifest_id)
		m_id = manifest.ident

		print("Deleting files from storage...")
		for file in await FileModel.filter(manifest=manifest).all():
			await self.storage.remove(file.ident)
			await file.delete()

		mod.file_manifest = None
		await mod.save()
		await manifest.delete()
		return f"Manifest '{m_id}' deleted"

	async def _on_cmd_mod_set_def(self, gameid: str, modid: str):
		mod = await self._get_game_mod(gameid, modid)
		if mod.default: raise Exception(f"Mod '{modid}' is already set as default")

		for c in await GameModModel.filter(game__id=mod.game_id).all():
			if c.default:
				c.default = False
				await c.save()
				break

		mod.default = True
		await mod.save()
		return f"Mod '{modid}' has been set as default"

	async def _on_cmd_client_set_ref(self, gameid: str, clientid: str):
		client = await self._get_game_client(gameid, clientid)
		if client.reference: raise Exception(f"Client '{clientid}' is already set as the reference")

		for c in await GameClientModel.filter(game__id=client.game_id).all():
			if c.reference:
				c.reference = False
				await c.save()
				break

		client.reference = True
		await client.save()
		return f"Client '{clientid}' has been set as the reference"

	async def _on_cmd_help(self):
		return self.__shell.beautiful_help

	async def _cmd_game_server_list(self, gameid: str, clientid: str):
		rows = ["id\tname\tcreated at\tmodified\taddress\tquery port"]

		client = await self._get_game_client(gameid, clientid)

		for server in await GameServerModel.filter(client=client).all():
			rows.append(f"{server.ident}\t{server.name}\t\t{server.created_at}\t{server.modified}\t{server.address}\t{server.query_port}")

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
		rows = ["id\tname\tshort name\tcreated at\tmodified\tfiles\tms address\tms enckey"]

		game = await self._get_game(gameid)

		for client in await GameClientModel.filter(game=game).all():
			rows.append(f"{client.ident}\t{client.name}\t{client.short_name}\t{client.created_at}\t{client.modified}\t{"yes" if client.file_manifest else "no"}\t{client.master_server_address if client.master_server_address else "no"}\t{client.master_server_enctypex_key if client.master_server_enctypex_key else "no"}")

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
		await RegisterTortoise(config=TORTOISE_ORM)
		#await Tortoise.generate_schemas(safe=True)
		self.__scheduler.start()
		#await self._task_refresh_game_servers_data()
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
				if command.error:
					print(command.error)
					#print(command.err_traceback)
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