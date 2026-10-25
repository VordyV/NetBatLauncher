from .models import AccountModel, SessionAccountModel
from ipaddress import IPv4Address
from dataclasses import dataclass
import asyncio
import bcrypt
import datetime

class ServiceException(Exception): pass

@dataclass
class Account:
	name: str
	permissions: list[str]
	operator: bool
	session_id: int

class AccountService:

	@staticmethod
	async def authenticate(name: str, password: str) -> int:
		account = await AccountModel.get_or_none(name=name)
		if not account or not account.active or not account.password or not account.op or not await asyncio.to_thread(bcrypt.checkpw, password.encode(), account.password.encode()): raise ServiceException("Invalid username or password")
		return account.id

	@staticmethod
	async def authorize(aid: int, ip_address: IPv4Address, agent: str) -> str:
		session = await SessionAccountModel.create(account_id=aid, address=str(ip_address), agent=agent)
		return session.token

	@staticmethod
	async def re_authorize(token: str, ip_address: IPv4Address, agent: str) -> Account | None:
		session = await SessionAccountModel.get_or_none(token=token)
		if not session: return None
		session.last_access = datetime.datetime.now()
		await session.save()

		account = await AccountModel.get_or_none(id=session.account_id)
		return Account(name=account.name, permissions=account.permissions, operator=account.op, session_id=session.id)

	@staticmethod
	async def logout(token: str) -> bool:
		session = await SessionAccountModel.get_or_none(token=token)
		if session:
			await session.delete()
			return True
		return False

	@staticmethod
	async def hash_password(password: str):
		return (await asyncio.to_thread(bcrypt.hashpw, password.encode(), bcrypt.gensalt())).decode()