import datetime

from starlette.requests import Request
from starlette.responses import RedirectResponse, Response
from starlette_admin.auth import AdminUser, AuthProvider, LoginFailed
from .models import AccountModel, SessionAccountModel
from .services import AccountService, Account
from dataclasses import dataclass, field
import urllib.parse
import bcrypt
import asyncio
import ipaddress

@dataclass
class StanAdminUser(AdminUser):
    permissions: frozenset[str] = field(default_factory=frozenset)
    operator: bool = field(default=False)

class StanAuthProvider(AuthProvider):

    async def login(self, name: str, password: str, remember_me: bool, request: Request) -> None:
        try:
            aid = await AccountService.authenticate(name=name, password=password)
            token = await AccountService.authorize(aid=aid, ip_address=ipaddress.ip_address(request.client.host), agent=request.headers.get("user-agent"))

            request.session["token"] = token
        except Exception as e:
            raise LoginFailed("Invalid username or password")

    async def authenticate(self, request: Request) -> StanAdminUser | None:
        token = request.session.get("token")
        if not token: return None

        account = await AccountService.re_authorize(token=token, ip_address=ipaddress.ip_address(request.client.host), agent=request.headers.get("user-agent"))
        if account:
            return StanAdminUser(username=account.name, permissions=account.permissions, operator=account.operator)

    async def logout(self, request: Request) -> None:
        token = request.session.get("token")
        if token:
            await AccountService.logout(token=token)
        request.session.clear()
