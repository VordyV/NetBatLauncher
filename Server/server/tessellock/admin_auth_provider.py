from starlette_admin.auth import AdminUser, AuthProvider, LoginFailed
from starlette.requests import Request
from ipaddress import ip_address
from dataclasses import dataclass, field
import traceback


@dataclass(kw_only=True)
class TessellockAdminUser(AdminUser):
	session_id: int
	account_id: int
	account_uuid: str
	permissions: list[str]
	is_op: bool

class TessellockAuthProvider(AuthProvider):

	def __init__(self, core, *args, **kwargs):
		self.__core = core
		super().__init__(*args, **kwargs)

	@property
	def core(self): return self.__core

	async def login(self, username: str, password: str, remember_me: bool, request: Request) -> None:
		try:
			aid = await self.core.account_service.authenticate(name_or_email=username, password=password, login_admin_panel=True)
			user_agent = request.headers.get("user-agent", "")
			token = await self.core.account_service.authorize(account_id=aid, ip_address=ip_address(request.client.host), agent=user_agent)
			request.session["token"] = token
		except Exception as err:
			raise LoginFailed("Invalid username or password")

	async def authenticate(self, request: Request) -> TessellockAdminUser | None:
		token = request.session.get("token")
		if not token: return None
		try:
			aid, sessid = await self.core.account_session_service.authenticate(token, login_admin_panel=True)
			acc = await self.core.account_service.get(account_id=aid)
			perms = await self.core.account_service.get_permission_codes(account_id=aid)
			return TessellockAdminUser(username=acc.name, session_id=sessid, account_id=aid, permissions=perms, is_op=acc.is_op, account_uuid=acc.id)
		except Exception:
			traceback.print_exc()
			return None

	async def logout(self, request: Request) -> None:
		admin_user = request.state.admin_user
		token = request.session.get("token")
		if not token: return None

		try:
			await self.core.account_session_service.delete(admin_user.session_id, initiator=admin_user.account_uuid)
		except Exception:
			return None

		request.session.clear()