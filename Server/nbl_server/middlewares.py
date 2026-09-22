from nbl_server.client import APIClient, APIError
from nbl_server.session_state import SessionState
import flet_easy as fs
import flet as ft
import jwt
import datetime
import os

class AuthMiddleware(fs.MiddlewareRequest):

	PUBLIC_ROUTES = (
		"/login",
		"/auth/callback/{otac}",
	)

	def _is_public_route(self) -> bool: return self.data.route in AuthMiddleware.PUBLIC_ROUTES

	async def _restore_session(self) -> bool:
		prefs = ft.SharedPreferences()

		if not await prefs.contains_key("nbl.token"): return False
		token = await prefs.get("nbl.token")

		try:
			account_data = await APIClient.me(access_token=token)
		except APIError as e:
			await prefs.remove("nbl.token")
			return False

		payload = jwt.decode(token, options={"verify_signature": False})
		exp = datetime.datetime.fromtimestamp(payload.get("exp"), tz=self.data.core.time_zone) - datetime.timedelta(seconds=int(os.getenv("MIN_REM_LIFETIME_REFRESH")))

		await self.data.set_session(account_data, exp)

		return True

	async def before_request(self):
		authenticated = self.data.get_session_state() == SessionState.Authorized

		if not authenticated:
			authenticated = await self._restore_session()

		is_public = self._is_public_route()

		if authenticated and is_public:
			return fs.Redirect("/")

		if not authenticated and not is_public:
			return fs.Redirect("/login")

		return None

	async def after_request(self):
		pass