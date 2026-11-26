from fastapi import FastAPI, Request
from tessellock import Tessellock
from tortoise.contrib.fastapi import RegisterTortoise
from contextlib import asynccontextmanager
from fastapi.responses import JSONResponse
from tortoise import Tortoise
from .views import router
from .services import *
from .models import *
from .admin_panel_views import GameAPView, StorageTaskAPView, StorageTasksAPView
from .storage import LocalStorage, Storage
import os
import nbl_server

class NetBatLauncherServer:

	def __init__(self):
		key = os.getenv("KEY")
		self.__timezone = os.getenv("TIMEZONE", "Europe/Moscow")
		self.__app = FastAPI(lifespan=self._lifespan, version=nbl_server.__version__, title="NBL Server", description="API server for the Net Bat Launcher")
		self.__tsl = Tessellock(fa_app=self.__app, secret_key=key, timezone=self.__timezone)
		self.__db_url = os.getenv("DB_URL", "sqlite://:memory:")
		self.__game_service = GameService(self)
		self.__game_client_service = GameClientService(self)
		self.__game_mod_service = GameModService(self)
		self.__game_server_service = GameServerService(self)
		self.__file_manifest_service = FileManifestService(self)
		self.__manifest_download_session_service = ManifestDownloadSessionService(self)
		self.__storage_task_service = StorageTaskService(self)
		self.__game_disclaimer_service = GameDisclaimerService(self)
		self.__storages = {
			"local": LocalStorage
		}

		self.__tsl.OP_NAME = os.getenv("OP_NAME", None)
		self.__tsl.OP_PASSWORD = os.getenv("OP_PASSWORD", None)
		self.__tsl.app.include_router(router)

		self.__app.add_exception_handler(ServiceException, self._service_exception_handler)

		storage = os.getenv("STORAGE", "local")
		self.__storage: Storage | None = self.__storages.get(storage)
		if not self.__storage: raise Exception(f"Storage '{storage}' does not exist")

	@property
	def app(self) -> FastAPI: return self.__app

	@property
	def tsl(self) -> Tessellock: return self.__tsl

	@property
	def storage(self) -> Storage: return self.__storage

	@property
	def game_service(self) -> GameService: return self.__game_service

	@property
	def game_client_service(self) -> GameClientService: return self.__game_client_service

	@property
	def game_mod_service(self) -> GameModService: return self.__game_mod_service

	@property
	def game_server_service(self) -> GameServerService: return self.__game_server_service

	@property
	def file_manifest_service(self) -> FileManifestService: return self.__file_manifest_service

	@property
	def manifest_download_session_service(self) -> ManifestDownloadSessionService: return self.__manifest_download_session_service

	@property
	def storage_task_service(self) -> StorageTaskService: return self.__storage_task_service

	@property
	def game_disclaimer_service(self) -> GameDisclaimerService: return self.__game_disclaimer_service

	async def _service_exception_handler(self, request: Request, exc: ServiceException) -> JSONResponse:
		status_code = 404 if isinstance(exc, NotFoundSException) else 400
		return JSONResponse(status_code=status_code, content={"detail": str(exc) or "Service error"})

	@asynccontextmanager
	async def _lifespan(self, app: FastAPI):
		app.state.core = self

		await RegisterTortoise(db_url=self.__db_url, modules={'models': ['tessellock.models', 'tessellock.sadaps', 'nbl_server.models']}, timezone=self.__timezone)

		# TODO: Replace with migrations at release
		await Tortoise.generate_schemas(safe=True)

		self.__tsl.admin_panel.add_view(StorageTasksAPView(self, StorageTaskModel))
		self.__tsl.admin_panel.add_view(GameAPView(self, GameModel))
		await self.__tsl.start()
		self.__tsl.admin_panel.app.state.core = self
		yield
		await self.__tsl.stop()
		await Tortoise.close_connections()