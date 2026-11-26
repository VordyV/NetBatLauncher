import datetime
import os.path
from typing import Callable
from fastapi import FastAPI, Request, APIRouter
from fastapi.exceptions import HTTPException
from .views.auth_view import auth_router
from .views.account_view import acc_router
from .views.group_view import gr_router
from .views.permission_view import perm_router
from .oauth2_providers import GoogleOAuth2Provider, BaseOAuth2Provider
from .services import AccountService, AccountSessionService, GroupService, PermissionService, ChronicleService
from .models import *
from .service_exceptions import *
from .multilingual import Multilingual
from .dictionaries.en import en_phrases
from .dictionaries.ru import ru_phrases
from .protection import Protection
from . import settings
from .admin_panel_views.account_ap_view import AccountAPView
from .admin_panel_views.account_session_ap_view import AccountSessionAPView
from .admin_panel_views.group_ap_view import GroupAPView
from .admin_panel_views.permission_ap_view import PermissionAPView
from .admin_panel_views.chronicle_ap_view import ChronicleAPView
from .admin_auth_provider import TessellockAuthProvider
from starlette_admin.contrib.tortoise import Admin
from starlette_admin.views import DropDown
from starlette.middleware.sessions import SessionMiddleware
from apscheduler.schedulers.asyncio import AsyncIOScheduler
from datetime import timedelta
from zoneinfo import ZoneInfo
import aiofiles
import asyncio
from pathlib import Path
import os


class Tessellock:

	OP_NAME: str | None
	OP_PASSWORD: str | None

	def __init__(self,
		fa_app: FastAPI,
		secret_key: str,
		prefix: str = "/api",
		admin_panel_title: str = "Admin",
		timezone: str = "Europe/Moscow",
		dev_mode: bool = False,
		filename_settings: str = "settings.json",
		session_expiration: timedelta = timedelta(days=7),
		maximum_session_lifetime: timedelta = timedelta(days=30),
		gc_invalid_session: timedelta = timedelta(minutes=1),
		gc_invalid_account_permissions: timedelta = timedelta(minutes=1),
		gc_invalid_account_groups: timedelta = timedelta(minutes=1),
		gc_invalid_group_permissions: timedelta = timedelta(minutes=1),
	):
		self.__dev_mode = dev_mode
		self.__fa_app = fa_app
		current_dir = Path(__file__).resolve().parent
		print(os.path.join(current_dir, "resources", "static"))
		self.__admin_panel = Admin(title=admin_panel_title, auth_provider=TessellockAuthProvider(self), secret_key=secret_key, static_dir=os.path.join(current_dir, "resources", "static"), templates_dir=os.path.join(current_dir, "resources", "templates"))
		self.__prefix = prefix
		self.__timezone = timezone
		self.__oauth2_providers: dict[str, type[BaseOAuth2Provider]] = {}
		self.__account_service = AccountService(self)
		self.__account_session_service = AccountSessionService(self)
		self.__group_service = GroupService(self)
		self.__permission_service = PermissionService(self)
		self.__chronicle_service = ChronicleService(self)
		self.__multilingual = Multilingual()
		self.__protection = Protection()
		self.__filename_settings = filename_settings
		self.__settings: settings.Settings = None
		self.__session_expiration = session_expiration
		self.__maximum_session_lifetime = maximum_session_lifetime
		self.__gc_invalid_session = gc_invalid_session
		self.__gc_invalid_account_permissions = gc_invalid_account_permissions
		self.__gc_invalid_account_groups = gc_invalid_account_groups
		self.__gc_invalid_group_permissions = gc_invalid_group_permissions
		self.__zone_info = ZoneInfo(self.__timezone)
		self.__gc_scheduler = AsyncIOScheduler() # Garbage Collector

		self.__router_api = APIRouter(prefix=self.__prefix)

		self.__router_api.include_router(auth_router)
		self.__router_api.include_router(acc_router)
		self.__router_api.include_router(gr_router)
		self.__router_api.include_router(perm_router)

		self.__fa_app.include_router(self.__router_api)
		self.__fa_app.add_middleware(SessionMiddleware, secret_key=secret_key)

		self.add_exception_handler(AccDuplSException, status_code=400, detail=self.p(1, "An account with this name or email already exists. Please try again with different details"))
		self.add_exception_handler(LoginFailedSException, status_code=400, detail=self.p(2, "Invalid login or password"))
		self.add_exception_handler(AuthFailedSException, status_code=401, detail=self.p(3, "You do not have access to this action"))
		self.add_exception_handler(AccDataNotChangedSException, status_code=400, detail=self.p(4, "Account data was not changed"))
		self.add_exception_handler(PwdIncorrectSException, status_code=400, detail=self.p(5, "Password is incorrect"))
		self.add_exception_handler(GroupDuplSException, status_code=400, detail=self.p(6, "A group with the name already exists. Use a different name"))
		self.add_exception_handler(GroupNotFoundSException, status_code=404, detail=self.p(6, "Group not found"))
		self.add_exception_handler(GroupDataNotChangedSException, status_code=400, detail=self.p(4, "Group data was not changed"))
		self.add_exception_handler(AccNotFoundSException, status_code=404, detail=self.p(6, "Account not found"))
		self.add_exception_handler(GroupAddAlreadySException, status_code=400, detail=self.p(7, "Account already belongs to this group"))
		self.add_exception_handler(GroupNotAddedAccSException, status_code=400, detail=self.p(7, "Account does not belong to this group"))
		self.add_exception_handler(PermissionNotFoundSException, status_code=404, detail=self.p(10, "Permission not found"))
		self.add_exception_handler(PermissionAddAlreadySException, status_code=400, detail=self.p(11, "Account already has this permission"))
		self.add_exception_handler(PermissionNotAddedAccSException, status_code=400, detail=self.p(12, "Account does not have this permission"))
		self.add_exception_handler(GroupPermissionAddAlreadySException, status_code=400, detail=self.p(13, "Group already has this permission"))
		self.add_exception_handler(GroupPermissionNotAddedSException, status_code=400, detail=self.p(14, "Group does not have this permission"))
		self.add_exception_handler(AccAlreadyOperSException, status_code=400, detail=self.p(15, "Is already an operator"))
		self.add_exception_handler(AccNotOperSException, status_code=400, detail=self.p(16, "Is not an operator"))
		self.add_exception_handler(AccAlreadyEnableSException, status_code=400, detail=self.p(17, "Account is already enabled"))
		self.add_exception_handler(AccNotEnableSException, status_code=400, detail=self.p(18, "Account is not enabled"))
		self.add_exception_handler(GroupAlreadyEnableSException, status_code=400, detail=self.p(19, "Group is already enabled"))
		self.add_exception_handler(GroupNotEnableSException, status_code=400, detail=self.p(20, "Group is not enabled"))
		self.add_exception_handler(InvalidId32SException, status_code=400, detail=self.p(21, "Provided id has an incorrect format; a hexadecimal string of size 32 is allowed"))
		self.add_exception_handler(ServiceException, status_code=400, detail=self.p(22, "Unhandled error"))

		self.__multilingual.add_dict("en", en_phrases)
		self.__multilingual.add_dict("ru", ru_phrases)

		self.__oauth2_providers["google"] = GoogleOAuth2Provider

		self.__protection.register_permission("accounts.read", "Read Accounts", "Allows viewing account information")
		self.__protection.register_permission("accounts.create", "Create Accounts", "Allows creating new accounts")
		self.__protection.register_permission("accounts.update", "Update Accounts", "Allows editing account information")
		self.__protection.register_permission("accounts.delete", "Delete Accounts", "Allows deleting accounts")
		self.__protection.register_permission("accounts.sessions.read", "Read Account Sessions", "Allows viewing account sessions")
		self.__protection.register_permission("accounts.sessions.delete", "Delete Account Sessions", "Allows deleting account sessions")
		self.__protection.register_permission("accounts.changepassword", "Change Account Password", "Allows changing an account's password")
		self.__protection.register_permission("accounts.groups.assign", "Assign Groups to Accounts", "Allows assigning groups to accounts")
		self.__protection.register_permission("accounts.groups.remove", "Remove Groups from Accounts", "Allows removing groups from accounts")
		self.__protection.register_permission("accounts.groups.read", "Read Account Groups", "Allows viewing groups assigned to accounts")
		self.__protection.register_permission("accounts.permissions.add", "Assign Permissions to Accounts", "Allows assigning permissions to accounts")
		self.__protection.register_permission("accounts.permissions.delete", "Remove Account Permissions", "Allows revoking permissions from accounts")
		self.__protection.register_permission("accounts.permissions.read", "Read Account Permissions", "Allows viewing permissions assigned to accounts")
		self.__protection.register_permission("accounts.operator.make", "Grant Operator Status", "Allows granting operator status to accounts")
		self.__protection.register_permission("accounts.operator.delete", "Revoke Operator Status", "Allows revoking operator status from accounts")
		self.__protection.register_permission("accounts.active.enable", "Activate Accounts", "Allows activating accounts")
		self.__protection.register_permission("accounts.active.disable", "Deactivate Accounts", "Allows deactivating accounts")
		self.__protection.register_permission("groups.create", "Create Groups", "Allows creating new groups")
		self.__protection.register_permission("groups.read", "Read Groups", "Allows viewing group information")
		self.__protection.register_permission("groups.update", "Update Groups", "Allows editing group information")
		self.__protection.register_permission("groups.delete", "Delete Groups", "Allows deleting groups")
		self.__protection.register_permission("groups.permissions.read", "Read Group Permissions", "Allows viewing permissions assigned to groups")
		self.__protection.register_permission("groups.permissions.add", "Assign Permissions to Groups", "Allows assigning permissions to groups")
		self.__protection.register_permission("groups.permissions.delete", "Remove Group Permissions", "Allows revoking permissions from groups")
		self.__protection.register_permission("groups.active.enable", "Activate Groups", "Allows activating groups")
		self.__protection.register_permission("groups.active.disable", "Deactivate Groups", "Allows deactivating groups")
		self.__protection.register_permission("permissions.read", "Read permissions", "Allows viewing permissions")
		self.__protection.register_permission("admin.access", "Access to admin panel", "Allows login to admin panel")
		self.__protection.register_permission("chronicle.read", "Read chronicle", "Allows reading chronicle of entire system")

	@property
	def app(self) -> FastAPI: return self.__fa_app

	@property
	def account_service(self) -> AccountService: return self.__account_service

	@property
	def account_session_service(self) -> AccountSessionService: return self.__account_session_service

	@property
	def group_service(self) -> GroupService: return self.__group_service

	@property
	def permission_service(self) -> PermissionService: return self.__permission_service

	@property
	def protection(self) -> Protection: return self.__protection

	@property
	def ml(self) -> Multilingual: return self.__multilingual

	@property
	def p(self): return self.__multilingual.p

	@property
	def dev_mode(self) -> bool: return self.__dev_mode

	@property
	def settings(self) -> settings.Settings: return self.__settings

	@property
	def chronicle_service(self) -> ChronicleService: return self.__chronicle_service

	@property
	def admin_panel(self) -> Admin: return self.__admin_panel

	@property
	def maximum_session_lifetime(self) -> timedelta: return self.__maximum_session_lifetime

	@property
	def session_expiration(self) -> timedelta: return self.__session_expiration

	def now(self) -> datetime.datetime: return datetime.datetime.now(self.__zone_info)

	def datetime(self, value: datetime.datetime): return value.astimezone(tz=self.__zone_info)

	async def load_settings(self):
		if not os.path.exists(self.__filename_settings):
			async with aiofiles.open(self.__filename_settings, mode='w') as f:
				await f.write(settings.Settings().model_dump_json(indent=4))
			await self.load_settings()
			return

		async with aiofiles.open(self.__filename_settings, mode='r') as f:
			row_data = await f.read()
			self.__settings = settings.Settings.model_validate_json(row_data)

	async def save_settings(self):
		if not self.__settings: raise Exception("Settings were not loaded, so they cannot be saved")
		async with aiofiles.open(self.__filename_settings, mode='w') as f:
			await f.write(self.__settings.model_dump_json(indent=4))

	def ext_lang(self, request: Request):
		return request.headers.get("Accept-Language")

	def add_exception_handler(self, exception: type[Exception], status_code: int, detail: str | Callable):

		async def func(request: Request, exc: Exception):
			text = detail
			if callable(detail):
				text = detail(self.ext_lang(request))

			raise HTTPException(status_code=status_code, detail=text)

		self.__fa_app.add_exception_handler(exception, func)

	async def _on_task_gc_invalid_sessions(self):
		await self.account_session_service.delete_invalid_sessions()

	async def _on_task_gc_invalid_permissions(self):
		await self.account_service.delete_invalid_account_permissions()

	async def _on_task_gc_invalid_group_permissions(self):
		await self.group_service.delete_invalid_group_permissions()

	async def _on_task_gc_invalid_account_groups(self):
		await self.account_service.delete_invalid_account_groups()

	async def start(self):
		self.__fa_app.state.tessellock = self

		await self.load_settings()
		#db_url = "sqlite://:memory:"
		#if not self.dev_mode: db_url = "sqlite://db.sqlite3"

		self.__admin_panel.add_view(DropDown("Accounts", icon="fa-solid fa-user", always_open=False, views=[AccountAPView(self, AccountModel, icon = "fa fa-user"), AccountSessionAPView(self, AccountSessionModel, icon="fa-solid fa-address-book")]))
		self.__admin_panel.add_view(GroupAPView(self, PermissionGroupModel, icon="fa-solid fa-user-group"))
		self.__admin_panel.add_view(PermissionAPView(self, PermissionModel, icon="fa-solid fa-unlock"))
		self.__admin_panel.add_view(ChronicleAPView(self, ChronicleModel, icon="fa-solid fa-floppy-disk"))
		self.__admin_panel.mount_to(self.__fa_app)
		self.__admin_panel.app.state.parent_state = self.__fa_app.state

		await self.permission_service.synchronize(self.protection._permissions.values())

		self.__gc_scheduler.add_job(self._on_task_gc_invalid_sessions, 'interval', seconds=self.__gc_invalid_session.total_seconds(), id="invalid_sessions")
		self.__gc_scheduler.add_job(self._on_task_gc_invalid_permissions, 'interval', seconds=self.__gc_invalid_account_permissions.total_seconds(), id="invalid_permissions")
		self.__gc_scheduler.add_job(self._on_task_gc_invalid_group_permissions, 'interval', seconds=self.__gc_invalid_group_permissions.total_seconds(), id="invalid_group_permissions")
		self.__gc_scheduler.add_job(self._on_task_gc_invalid_account_groups, 'interval', seconds=self.__gc_invalid_account_groups.total_seconds(), id="invalid_account_groups")

		self.__gc_scheduler.start()

	async def stop(self):

		self.__gc_scheduler.shutdown()

