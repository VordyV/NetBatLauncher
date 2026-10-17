from starlette.requests import Request
from starlette.responses import RedirectResponse, Response
from starlette_admin.auth import AdminUser, AuthProvider, LoginFailed
from .models import AccountModel, SessionAccountModel
from dataclasses import dataclass, field
import urllib.parse
import bcrypt
import asyncio

@dataclass
class StanAdminUser(AdminUser):
    permissions: frozenset[str] = field(default_factory=frozenset)
    operator: bool = field(default=False)

class StanAuthProvider(AuthProvider):

    async def login(self, name: str, password: str, remember_me: bool, request: Request) -> None:
        account = await AccountModel.get_or_none(name=name)
        if not account or not account.password or not account.op or not await asyncio.to_thread(bcrypt.checkpw, password.encode(), account.password.encode()): raise LoginFailed("Invalid username or password")

        session = await SessionAccountModel.create(account=account, address=request.client.host, agent=request.headers.get("user-agent"))

        request.session["token"] = session.token

    async def authenticate(self, request: Request) -> StanAdminUser | None:
        token = request.session.get("token")
        if not token: return None

        session = await SessionAccountModel.get_or_none(token=token)
        if not session: return None

        account = await AccountModel.get_or_none(id=session.account_id)

        return StanAdminUser(username=account.name, permissions=account.permissions, operator=account.op)

    async def logout(self, request: Request) -> None:
        token = request.session.get("token")
        if token:
            session = await SessionAccountModel.get_or_none(token=token)
            if session: await session.delete()
        request.session.clear()
