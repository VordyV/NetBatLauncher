from contextlib import asynccontextmanager
from fastapi import FastAPI
from tortoise.contrib.fastapi import RegisterTortoise
from tortoise import Tortoise
from .views import router
from authx import AuthX, AuthXConfig
from .pages import routers
from apscheduler.schedulers.asyncio import AsyncIOScheduler
from zoneinfo import ZoneInfo
from .client import APIClient, APIError
from .session_state import SessionState
from .schemes import (
	ResponseExchange,
	ResponseRefresh,
	ResponseAccountData
)
from .middlewares import AuthMiddleware
from .navigation import NavigationItem
from .timer import TimerAsync
from .storage import LocalStorage, Storage
import flet.fastapi as flet_fastapi
import flet_easy as fs
import flet as ft
import re
import datetime
import httpx
import os
import asyncio
import jwt
import pathlib

class NetBatLauncherServer:

	Auth: AuthX | None = None

	def __init__(self, steam_api_key):
		self.__steam_api_key = steam_api_key

		self.__fp_app = FastAPI(lifespan=self._fa_lifespan)
		self.__fs_app = fs.FletEasy(route_init="/")
		# TODO: Replace the string with a full fledged key
		self.__authx_config = AuthXConfig(
			JWT_SECRET_KEY="123456789",
			JWT_TOKEN_LOCATION=["headers"],
			JWT_ACCESS_TOKEN_EXPIRES=datetime.timedelta(seconds=int(os.getenv("TOKEN_LIFETIME"))),
		)
		self.__auth = AuthX(config=self.__authx_config)
		self.__auth.handle_errors(self.__fp_app)
		self.__used_nonces_auth = set()
		self.__token_blacklist = set()
		self.__one_time_auth_codes: dict[str, str] = {}
		self.__scheduler = AsyncIOScheduler()
		self.__time_zone = ZoneInfo(os.getenv("TIMEZONE"))
		self.__storages: list[type[Storage]] = [
			LocalStorage
		]

		self.__fp_app.include_router(router)

		path_assets = pathlib.Path(__file__).parent / "assets"
		self.__fp_app.mount("/", flet_fastapi.app(self.__fs_app.run(fastapi=True), assets_dir=path_assets, upload_dir=os.getenv("TEMP_STORAGE_PATH")))

		self.__fs_app.add_pages(routers)
		self.__fs_app.config(self._fs_config)
		self.__fs_app.view(self._fs_config_event_handler)
		self.__fs_app.add_middleware(AuthMiddleware)

		NetBatLauncherServer.Auth = self.auth

		APIClient.configure(base_url="http://127.0.0.1:8080")

		self.__pages = [
			NavigationItem(label="General", icon=ft.Icons.SMART_BUTTON, url_page="/"),
			NavigationItem(label="Games", icon=ft.Icons.SMART_BUTTON, url_page="/games"),
			NavigationItem(label="Clients", icon=ft.Icons.SMART_BUTTON, url_page="/"),
			NavigationItem(label="Game servers", icon=ft.Icons.SMART_BUTTON, url_page="/")
		]

	@property
	def app(self) -> FastAPI: return self.__fp_app

	@property
	def auth(self) -> AuthX: return self.__auth

	@property
	def time_zone(self) -> ZoneInfo: return self.__time_zone

	@property
	def pages(self) -> list[NavigationItem]: return self.__pages

	@asynccontextmanager
	async def _fa_lifespan(self, app: FastAPI):
		app.state.core = self
		await RegisterTortoise(db_url='sqlite://db.sqlite3', modules={'models': ['nbl_server.models']})
		await Tortoise.generate_schemas(safe=True)
		self.__scheduler.start()
		yield
		self.__scheduler.shutdown(wait=False)
		await Tortoise.close_connections()

	def get_storage(self, name: str = None) -> type[Storage] | None:
		if not name:
			name = os.getenv("STORAGE")

		for s in self.__storages:
			if s.name == name:
				return s

		return None

	async def _remove_one_time_auth_code(self, code: str):
		await asyncio.sleep(15)
		if code in self.__one_time_auth_codes:
			del self.__one_time_auth_codes[code]

	async def _remove_from_backlist(self, token: str):
		if token in self.__token_blacklist:
			self.__token_blacklist.remove(token)

	def block_token(self, token: str, deletion_date: datetime.datetime):
		self.__token_blacklist.add(token)
		self.__scheduler.add_job(self._remove_from_backlist, 'date', args=(token,), run_date=deletion_date)

	def has_block_token(self, token: str) -> bool:
		return token in self.__token_blacklist

	def get_token_one_time_code(self, otac: str) -> str | None: # otac = one time auth code
		token = self.__one_time_auth_codes.get(otac)
		if token:
			if self.__scheduler.get_job(f"clear_otac_{otac}"):
				self.__scheduler.remove_job(f"clear_otac_{otac}")
			del self.__one_time_auth_codes[otac]
			return token
		return None

	def create_one_time_auth_code(self, token: str) -> str:
		code = os.urandom(8).hex()
		self.__one_time_auth_codes[code] = token
		self.__scheduler.add_job(self._remove_one_time_auth_code, 'date', args=(code,), run_date=datetime.datetime.now() + datetime.timedelta(seconds=15), id=f"clear_otac_{code}")
		return code

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

	def _on_event_session(self, data: fs.Datasy):

		async def func(topic: str, msg: dict):
			pass

		return func

	def _set_session(self, data: fs.Datasy):

		async def func(account_data: ResponseAccountData, exp_token: datetime.datetime):
			page = data.page
			page.session.store.set("session.state", SessionState.Authorized)
			page.session.store.set("account.id", account_data.id)
			page.session.store.set("account.name", account_data.name)
			page.session.store.set("account.photo", account_data.photo)
			page.session.store.set("account.steam_url", account_data.steam_url)

			data.timer.cancel()
			data.timer.run(exp_token-datetime.datetime.now(self.time_zone), self._refresh_session)

		return func

	def _undo_session(self, data: fs.Datasy):

		async def func():
			page = data.page
			page.session.store.set("session.state", SessionState.NotAuthorized)
			page.session.store.remove("account.id")
			page.session.store.remove("account.name")
			page.session.store.remove("account.photo")
			page.session.store.remove("account.steam_url")

			data.timer.cancel()

		return func

	def _get_session_state(self, data: fs.Datasy):

		def func() -> SessionState:
			page = data.page
			return SessionState(page.session.store.get("session.state"))

		return func

	async def _refresh_session(self, data: fs.Datasy):
		print("ref upd")

		prefs = ft.SharedPreferences()

		has_token = await prefs.contains_key("nbl.token")
		if not has_token or data.get_session_state() != SessionState.Authorized:
			await data.undo_session()
			data.go_route("/login")
			return

		token = await prefs.get("nbl.token")

		try:
			token = await APIClient.refresh(token)
			await prefs.set("nbl.token", token.access_token)

			account_data = await APIClient.me(access_token=token.access_token)

			payload = jwt.decode(token.access_token, options={"verify_signature": False})
			exp = datetime.datetime.fromtimestamp(payload.get("exp"), tz=self.time_zone) - datetime.timedelta(seconds=int(os.getenv("MIN_REM_LIFETIME_REFRESH")))

			await data.set_session(account_data, exp)
		except APIError as e:
			print(f"ref upd error {e.detail}")
			await data.undo_session()
			data.go_route("/login")

	def _on_disconnect(self, data: fs.Datasy):

		async def func(event):
			data.page.pubsub.unsubscribe_all()
			if data.get_session_state() == SessionState.Authorized:
				data.timer.cancel()

		return func

	def _logout(self, data: fs.Datasy):

		async def func():
			if data.get_session_state() != SessionState.Authorized: raise Exception("Not authorized")

			await data.undo_session()
			prefs = ft.SharedPreferences()
			await prefs.remove("nbl.token")
			data.go_route("/login")

		return func

	def _get_access_token(self, data: fs.Datasy):

		async def func():
			prefs = ft.SharedPreferences()
			return await prefs.get("nbl.token")

		return func

	def _get_agent_id(self, data: fs.Datasy):

		async def func():
			prefs = ft.SharedPreferences()
			agent = await prefs.get("nbl.agent")
			if not agent:
				code = os.urandom(16).hex()
				await prefs.set("nbl.agent", code)
				return code

			return agent

		return func

	def _send_event_session(self, data: fs.Datasy):

		async def func(msg: str):
			agent_id = await data.get_agent_id()
			data.page.pubsub.send_others_on_topic(f"session.{agent_id}", msg)

		return func

	async def _fs_config_event_handler(self, data: fs.Datasy):
		data.core = self
		data.set_session = self._set_session(data)
		data.undo_session = self._undo_session(data)
		data.get_session_state = self._get_session_state(data)
		data.logout = self._logout(data)
		data.timer = TimerAsync(data)
		data.get_token = self._get_access_token(data)
		data.get_agent_id = self._get_agent_id(data)
		data.send_event_session = self._send_event_session(data)

		data.page.on_disconnect = self._on_disconnect(data)

		agent_id = await data.get_agent_id()
		data.page.pubsub.subscribe_topic(f"session.{agent_id}", self._on_event_session(data))

	def _fs_config(self, page: ft.Page):
		page.session.store.set("session.state", SessionState.NotAuthorized)
		page.theme = ft.Theme(
			page_transitions=ft.PageTransitionsTheme(
				windows=ft.PageTransitionTheme.NONE,
				android=ft.PageTransitionTheme.NONE,
				ios=ft.PageTransitionTheme.NONE,
				macos=ft.PageTransitionTheme.NONE,
				linux=ft.PageTransitionTheme.NONE,
			),
			color_scheme=ft.ColorScheme(
				surface="#faf8ff",
				surface_dim="#d2d9f4",
				surface_bright="#faf8ff",
				surface_container_lowest="#ffffff",
				surface_container_low="#f2f3ff",
				surface_container="#eaedff",
				surface_container_high="#e2e7ff",
				surface_container_highest="#dae2fd",
				on_surface="#131b2e",
				on_surface_variant="#464555",
				inverse_surface="#283044",
				on_inverse_surface="#eef0ff",
				outline="#777587",
				outline_variant="#c7c4d8",
				surface_tint="#4d44e3",

				primary="#3525cd",
				on_primary="#ffffff",
				primary_container="#4f46e5",
				on_primary_container="#dad7ff",
				inverse_primary="#c3c0ff",

				secondary="#006591",
				on_secondary="#ffffff",
				secondary_container="#39b8fd",
				on_secondary_container="#004666",

				tertiary="#005338",
				on_tertiary="#ffffff",
				tertiary_container="#006e4b",
				on_tertiary_container="#67f4b7",

				error="#ba1a1a",
				on_error="#ffffff",
				error_container="#ffdad6",
				on_error_container="#93000a",

				primary_fixed="#e2dfff",
				primary_fixed_dim="#c3c0ff",
				on_primary_fixed="#0f0069",
				on_primary_fixed_variant="#3323cc",
				secondary_fixed="#c9e6ff",
				secondary_fixed_dim="#89ceff",
				on_secondary_fixed="#001e2f",
				on_secondary_fixed_variant="#004c6e",
				tertiary_fixed="#6ffbbe",
				tertiary_fixed_dim="#4edea3",
				on_tertiary_fixed="#002113",
				on_tertiary_fixed_variant="#005236",
			),
			button_theme=ft.ButtonTheme(style=ft.ButtonStyle(shape=ft.RoundedRectangleBorder(radius=4))),
			filled_button_theme=ft.FilledButtonTheme(style=ft.ButtonStyle(shape=ft.RoundedRectangleBorder(radius=4))),
		)