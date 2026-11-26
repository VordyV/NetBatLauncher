import os
import uuid
from ipaddress import IPv4Address
from hashlib import sha256
from .service import Service, service_exception_handler
from .models import AccountModel, AccountSessionModel, PermissionGroupModel, PermissionModel, GroupPermissionsModel, AccountPermissionsModel, AccountGroupsModel, AccountPermissionsModel, ChronicleModel
from .schemes import *
from .service_exceptions import *
from tortoise.expressions import Q
from tortoise.exceptions import IntegrityError
import bcrypt
import asyncio
import tortoise.timezone
from datetime import timedelta, datetime
from .chronicle_types import *
from tortoise import fields


class AccountSessionService(Service):

	model = AccountSessionModel
	encodings: str = "ascii"

	async def _gen_hash_token(self, token: str | None = None):
		return await asyncio.to_thread(sha256(token.encode(self.encodings)).hexdigest)

	async def create(self, account_id: int | str, ip_address: IPv4Address, agent: str) -> str:
		acc = await self.core.account_service.get_from_id(account_id)
		token = os.urandom(32).hex()
		session = await self._create(account_id=None if account_id == 0 else acc.id, token=await self._gen_hash_token(token), ip_address=str(ip_address), agent=agent, is_op=not bool(account_id))
		self.core.chronicle_service.add(CreateSessionChronicleType(account_id=acc.uuid.hex, ip_address=str(ip_address), agent=agent, session_id=session.uuid.hex, initiator=acc.uuid.hex))
		return token

	async def authenticate(self, token: str, login_admin_panel: bool = False) -> (int, int): # aid, session id (uuid)
		#session = await self._get("token", await self._gen_hash_token(token), null=True)
		session = await self.model.get_or_none(token=await self._gen_hash_token(token)).prefetch_related("account")

		if not session or not getattr(session.account, "is_active", True): raise AuthFailedSException()
		if not session.account and (not self.core.OP_NAME or not self.core.OP_PASSWORD): raise AuthFailedSException()

		now = self.core.now()
		if now >= self.core.datetime(session.dt_created) + self.core.maximum_session_lifetime or now >= self.core.datetime(session.last_access) + self.core.session_expiration:
			await session.delete()
			raise AuthFailedSException()

		if login_admin_panel and getattr(session.account, "id", False) and not await self.core.permission_service.has_permission(account_id=session.account.id, permission_code="admin.access"): raise AccNotFoundSException()
		await self.model.filter(id=session.id).update(last_access=tortoise.timezone.now())
		return (session.account_id if session.account else 0, session.id)

	async def delete(self, session_id: int, initiator: str | int):
		session = await self._get("id", session_id, null=True)
		if not session: raise AuthFailedSException()
		await session.delete()
		self.core.chronicle_service.add(DeleteSessionChronicleType(session_id=session.uuid.hex, initiator=initiator))

	async def delete_invalid_sessions(self):
		now = self.core.now()
		await AccountSessionModel.filter(Q(dt_created__lte = now - self.core.maximum_session_lifetime) | Q(last_access__lte = now - self.core.session_expiration)).delete()


class AccountService(Service):

	model = AccountModel
	encodings: str = "ascii"
	_opacc: AccountModel | None = None

	@service_exception_handler(NotFoundSException, AccNotFoundSException)
	async def get_from_id(self, ident: int | str):
		if isinstance(ident, str):
			return await self._get("uuid", uuid.UUID(ident))
		elif isinstance(ident, int):
			if ident == 0:
				if self._opacc: return self._opacc
				self._opacc = AccountModel.construct(name="OPERATOR", dt_created=datetime.now(), dt_modified=datetime.now(), uuid=uuid.uuid4(), is_active=True, is_op=True)
				return self._opacc
			return await self._get("id", ident)

	@staticmethod
	async def get_hash_password(password: str) -> bytes: return await asyncio.to_thread(bcrypt.hashpw, password.encode(AccountService.encodings), bcrypt.gensalt())

	@service_exception_handler(IntegrityError, AccDuplSException)
	async def create(self, name: str, email: str | None, password: str, is_operator: bool) -> Account:
		pwdhash = await self.get_hash_password(password)
		acc = await self._create(name=name, email=email, pwdhash=pwdhash, is_op=is_operator)

		if await self.core.group_service.has_group(self.core.settings.DEFAULT_GROUP):
			await self.add_group(acc.id, self.core.settings.DEFAULT_GROUP, initiator=acc.id)

		self.core.chronicle_service.add(RegisterChronicleType(name=name, email=email, initiator=acc.id))
		return Account(id=acc.uuid.hex, name=acc.name, email=acc.email, dt_created=acc.dt_created, dt_modified=acc.dt_modified, dt_modified_pwdhash=acc.dt_modified_pwdhash, is_op=acc.is_op, is_active=acc.is_active)

	async def authenticate(self, name_or_email: str, password: str, login_admin_panel: bool = False) -> int: # return id of AccountModel
		if (self.core.OP_NAME and self.core.OP_PASSWORD) and name_or_email == self.core.OP_NAME and password == self.core.OP_PASSWORD: return 0

		account = await AccountModel.filter(Q(name=name_or_email) | Q(email=name_or_email)).get_or_none()
		if not account: raise AccNotFoundSException()

		if login_admin_panel and not await self.core.permission_service.has_permission(account_id=account.id, permission_code="admin.access"): raise AccNotFoundSException()
		if not await asyncio.to_thread(bcrypt.checkpw, password.encode(self.encodings), account.pwdhash): raise AccNotFoundSException()

		return account.id

	async def authorize(self, account_id: int | str, ip_address: IPv4Address, agent: str) -> str: # aid = account id (pk)
		return await self.core.account_session_service.create(account_id, ip_address=ip_address, agent=agent)

	async def gel_all(self, page: int, size: int) -> list[Account]:
		offset = (page - 1) * size
		result = []
		for acc in await self.model.all().offset(offset).limit(size):
			result.append(Account(id=acc.uuid.hex, name=acc.name, email=acc.email, dt_created=acc.dt_created, dt_modified=acc.dt_modified, dt_modified_pwdhash=acc.dt_modified_pwdhash, is_op=acc.is_op, is_active=acc.is_active))
		return result

	async def get(self, account_id: int | str) -> Account:
		acc = await self.get_from_id(account_id)
		return Account(id=acc.uuid.hex, name=acc.name, email=acc.email, dt_created=acc.dt_created, dt_modified=acc.dt_modified, dt_modified_pwdhash=acc.dt_modified_pwdhash, is_op=acc.is_op, is_active=acc.is_active)

	async def update(self, account_id: int | str, new_name: str | None, new_email: str | None, initiator: str | int):
		if account_id == 0: return # Terminate execution for the operator account

		acc = await self.get_from_id(account_id)
		fields = {"dt_modified": datetime.now()}

		if new_name: fields["name"] = new_name
		if new_email: fields["email"] = new_email

		result = await self.model.filter(id=acc.id).update(**fields)
		if not bool(result): raise AccDataNotChangedSException()
		self.core.chronicle_service.add(UpdateAccChronicleType(account_id=acc.uuid.hex, new_name=new_name, new_email=new_email, initiator=initiator))

	async def delete(self, account_id: int | str, initiator: str | int):
		if account_id == 0: return # Terminate execution for the operator account

		acc = await self.get_from_id(account_id)
		await acc.delete()
		self.core.chronicle_service.add(DeleteAccChronicleType(account_id=acc.uuid.hex, initiator=initiator))

	async def change_password(self, account_id: int | str, password: str, new_password: str, initiator: str | int):
		if account_id == 0: return # Terminate execution for the operator account

		acc = await self.get_from_id(account_id)
		if not await asyncio.to_thread(bcrypt.checkpw, password.encode(self.encodings), acc.pwdhash): raise PwdIncorrectSException()

		pwdhash = await asyncio.to_thread(bcrypt.hashpw, new_password.encode(self.encodings), bcrypt.gensalt())
		acc.pwdhash = pwdhash
		acc.dt_modified_pwdhash = datetime.now()
		await acc.save()
		self.core.chronicle_service.add(ChangePwdAccChronicleType(account_id=acc.uuid.hex, initiator=initiator))

	async def add_group(self, account_id: int | str, group_id: int | str, initiator: str | int, expiration: timedelta | None = None):
		if account_id == 0: return # Terminate execution for the operator account

		acc = await self.get_from_id(account_id)
		gr = await self.core.group_service.get_from_id(group_id)

		if await AccountGroupsModel.filter(account=acc, group=gr).exists(): raise GroupAddAlreadySException()

		dt_expiration = datetime.now() + expiration if expiration else None
		await AccountGroupsModel.create(account=acc, group=gr, dt_expiration=dt_expiration)
		self.core.chronicle_service.add(AddGroupAccChronicleType(account_id=acc.uuid.hex, group_id=gr.uuid, expiration=dt_expiration.timestamp() if dt_expiration else None, initiator=initiator))

	async def remove_group(self, account_id: int | str, group_id: int | str, initiator: str | int, expiration: timedelta | None = None):
		if account_id == 0: return # Terminate execution for the operator account

		acc = await self.get_from_id(account_id)
		gr = await self.core.group_service.get_from_id(group_id)

		if not await AccountGroupsModel.filter(account=acc, group=gr).exists(): raise GroupNotAddedAccSException()

		await AccountGroupsModel.filter(account=acc, group=gr).delete()
		self.core.chronicle_service.add(RemoveGroupAccChronicleType(account_id=acc.uuid.hex, group_id=gr.uuid, initiator=initiator))

	async def get_groups(self, account_id: int | str) -> list[GroupB]:
		if account_id == 0: return [] # Terminate execution for the operator account

		acc = await self.get_from_id(account_id)

		groups = []
		for group in await AccountGroupsModel.filter(account=acc).prefetch_related("group").all():
			groups.append(GroupB(id=group.group.uuid, name=group.group.name, description=group.group.description))
		return groups

	async def add_permission(self, account_id: int | str, permission_code: str, initiator: str | int, expiration: timedelta | None = None):
		if account_id == 0: return # Terminate execution for the operator account

		acc = await self.get_from_id(account_id)

		permission_code = self.core.permission_service._get_code(permission_code)
		try:
			perm = await self.core.permission_service._get("code", permission_code)
		except NotFoundSException as e:
			raise PermissionNotFoundSException()
		except Exception as e:
			raise ServiceException(e)

		if await AccountPermissionsModel.filter(account=acc, permission=perm).exists(): raise PermissionAddAlreadySException()

		dt_expiration = datetime.now() + expiration if expiration else None
		try:
			await AccountPermissionsModel.create(account=acc, permission=perm, dt_expiration=dt_expiration)
		except IntegrityError as e:
			raise PermissionAddAlreadySException()
		except Exception as e:
			raise ServiceException(e)

		self.core.chronicle_service.add(AddPermAccChronicleType(account_id=acc.uuid.hex, permission_code=permission_code, expiration=dt_expiration.timestamp() if dt_expiration else None, initiator=initiator))

	async def remove_permission(self, account_id: int | str, permission_code: str, initiator: str | int) -> bool:
		if account_id == 0: return False # Terminate execution for the operator account

		acc = await self.get_from_id(account_id)

		permission_code = self.core.permission_service._get_code(permission_code)
		try:
			perm = await self.core.permission_service._get("code", permission_code)
		except NotFoundSException as e:
			raise PermissionNotFoundSException()
		except Exception as e:
			raise ServiceException(e)

		result = await AccountPermissionsModel.filter(account=acc, permission=perm).delete()
		if not result: raise PermissionNotAddedAccSException()
		self.core.chronicle_service.add(RemovePermAccChronicleType(account_id=acc.uuid.hex, permission_code=permission_code, initiator=initiator))
		return True

	async def get_permissions(self, account_id: int | str) -> AccountPermissions:
		if account_id == 0: return AccountPermissions(account_id="0", permissions=[]) # Terminate execution for the operator account

		acc = await self.get_from_id(account_id)

		permissions = []
		for acc_perm in await AccountPermissionsModel.filter(account=acc).prefetch_related("permission").all():
			perm = acc_perm.permission
			permissions.append(Permission(code=perm.code, name=perm.name, description=perm.description))

		return AccountPermissions(account_id=acc.uuid.hex, permissions=permissions)

	async def get_permission_codes(self, account_id: int | str) -> list[str]:
		if account_id == 0: return [] # Terminate execution for the operator account

		now = datetime.now()

		not_expired = (Q(dt_expiration__isnull=True) | Q(dt_expiration__gt=now))
		direct_permissions = await AccountPermissionsModel.filter(account_id=account_id).filter(not_expired).values_list("permission_id", flat=True)
		group_ids = await AccountGroupsModel.filter(account_id=account_id,group__is_active=True).filter(not_expired).values_list("group_id",flat=True)
		group_permissions = await GroupPermissionsModel.filter(group_id__in=group_ids).filter(not_expired).values_list("permission_id",flat=True)

		return sorted(set(direct_permissions) | set(group_permissions))

	async def make_operator(self, account_id: int | str, initiator: str | int):
		if account_id == 0: return # Terminate execution for the operator account

		acc = await self.get_from_id(account_id)

		if acc.is_op: raise AccAlreadyOperSException()
		acc.is_op = True
		await acc.save()
		self.core.chronicle_service.add(MakeOpAccChronicleType(account_id=acc.uuid.hex, initiator=initiator))

	async def remove_operator(self, account_id: int | str, initiator: str | int):
		if account_id == 0: return # Terminate execution for the operator account

		acc = await self.get_from_id(account_id)

		if not acc.is_op: raise AccNotOperSException()
		acc.is_op = False
		await acc.save()
		self.core.chronicle_service.add(RemoveOpAccChronicleType(account_id=acc.uuid.hex, initiator=initiator))

	async def enable(self, account_id: int | str, initiator: str | int):
		if account_id == 0: return # Terminate execution for the operator account

		acc = await self.get_from_id(account_id)

		if acc.is_active: raise AccAlreadyEnableSException()
		acc.is_active = True
		await acc.save()
		self.core.chronicle_service.add(EnableAccChronicleType(account_id=acc.uuid.hex, initiator=initiator))

	async def disable(self, account_id: int | str, initiator: str | int):
		if account_id == 0: return # Terminate execution for the operator account

		acc = await self.get_from_id(account_id)

		if not acc.is_active: raise AccNotEnableSException()
		acc.is_active = False
		await acc.save()
		self.core.chronicle_service.add(DisableAccChronicleType(account_id=acc.uuid.hex, initiator=initiator))

	async def delete_invalid_account_permissions(self):
		now = self.core.now()
		await AccountPermissionsModel.filter(dt_expiration__lte=now).delete()

	async def delete_invalid_account_groups(self):
		now = self.core.now()
		await AccountGroupsModel.filter(dt_expiration__lte=now).delete()


class GroupService(Service):

	model = PermissionGroupModel

	@service_exception_handler(NotFoundSException, GroupNotFoundSException)
	async def get_from_id(self, ident: int | str) -> PermissionGroupModel:
		return await super().get_from_id(ident)

	async def create(self, name: str, description: str, initiator: str | int, is_active: bool = True) -> Group:
		gr = await self._create(name=name, description=description, is_active=is_active)
		self.core.chronicle_service.add(CreateGrChronicleType(group_id=gr.uuid, name=name, description=description, initiator=initiator))
		return Group(id=gr.uuid, name=gr.name, description=gr.description, dt_created=gr.dt_created, dt_modified=gr.dt_modified, is_active=gr.is_active)

	async def get(self, group_id: int | str) -> Group:
		gr = await self.get_from_id(group_id)
		return Group(id=gr.uuid, name=gr.name, description=gr.description, dt_created=gr.dt_created, dt_modified=gr.dt_modified, is_active=gr.is_active)

	async def get_all(self) -> list[Group]:
		groups = []
		for gr in await self.model.filter().all():
			groups.append(Group(id=gr.uuid, name=gr.name, description=gr.description, dt_created=gr.dt_created, dt_modified=gr.dt_modified, is_active=gr.is_active))
		return groups

	async def update(self, group_id: int | str, new_name: str | None, new_description: str | None, initiator: str | int):
		gr = await self.get_from_id(group_id)

		fields = {"dt_modified": datetime.now()}

		if new_name: fields["name"] = new_name
		if new_description: fields["description"] = new_description

		result = await self.model.filter(id=gr.id).update(**fields)
		if not bool(result): raise AccDataNotChangedSException()
		self.core.chronicle_service.add(UpdateGrChronicleType(group_id=gr.uuid, new_name=new_name, new_description=new_description, initiator=initiator))

	async def delete(self, group_id: int | str, initiator: str | int):
		gr = await self.get_from_id(group_id)
		await gr.delete()
		self.core.chronicle_service.add(DeleteGrChronicleType(group_id=gr.uuid, initiator=initiator))

	async def add_permission(self, group_id: int | str, permission_code: str, initiator: str | int, expiration: timedelta | None = None):
		gr = await self.get_from_id(group_id)

		permission_code = self.core.permission_service._get_code(permission_code)
		try:
			perm = await self.core.permission_service._get("code", permission_code)
		except NotFoundSException as e:
			raise PermissionNotFoundSException()
		except Exception as e:
			raise ServiceException(e)

		if await GroupPermissionsModel.filter(group=gr, permission=perm).exists(): raise GroupPermissionAddAlreadySException()

		dt_expiration = datetime.now() + expiration if expiration else None
		try:
			await GroupPermissionsModel.create(group=gr, permission=perm, dt_expiration=dt_expiration)
		except IntegrityError as e:
			raise GroupPermissionAddAlreadySException()
		except Exception as e:
			raise ServiceException(e)

		self.core.chronicle_service.add(AddPermGrChronicleType(group_id=gr.uuid, expiration=dt_expiration.timestamp() if dt_expiration else None, initiator=initiator))

	async def remove_permission(self, group_id: int | str, permission_code: str, initiator: str | int) -> bool:
		gr = await self.get_from_id(group_id)

		permission_code = self.core.permission_service._get_code(permission_code)
		try:
			perm = await self.core.permission_service._get("code", permission_code)
		except NotFoundSException as e:
			raise PermissionNotFoundSException()
		except Exception as e:
			raise ServiceException(e)

		result = await GroupPermissionsModel.filter(group=gr, permission=perm).delete()
		if not result: raise GroupPermissionNotAddedSException()

		self.core.chronicle_service.add(RemovePermGrChronicleType(group_id=gr.uuid, initiator=initiator))
		return True

	async def get_permissions(self, group_id: int | str) -> list[Permission]:
		gr = await self.get_from_id(group_id)

		perms = []
		now = datetime.now()
		for perm in await GroupPermissionsModel.filter(group=gr).prefetch_related("permission").filter(Q(dt_expiration__isnull=True) | Q(dt_expiration__gt=now)).all():
			perms.append(Permission(code=perm.permission.code, name=perm.permission.name, description=perm.permission.description))
		return perms

	async def enable(self, group_id: int | str, initiator: str | int):
		gr = await self.get_from_id(group_id)

		if gr.is_active: raise GroupAlreadyEnableSException()
		gr.is_active = True
		await gr.save()
		self.core.chronicle_service.add(EnableGrChronicleType(group_id=gr.uuid, initiator=initiator))

	async def disable(self, group_id: int | str, initiator: str | int):
		gr = await self.get_from_id(group_id)

		if not gr.is_active: raise GroupNotEnableSException()
		gr.is_active = False
		await gr.save()
		self.core.chronicle_service.add(DisableGrChronicleType(group_id=gr.uuid, initiator=initiator))

	async def has_group(self, group_id: int | str) -> bool:
		if isinstance(group_id, int):
			return await self.model.filter(id=group_id).exists()
		return await self.model.filter(uuid=group_id).exists()

	async def has_group_from_name(self, group_name: str) -> bool:
		return await self.model.filter(name=group_name).exists()

	async def delete_invalid_group_permissions(self):
		now = self.core.now()
		await GroupPermissionsModel.filter(dt_expiration__lte=now).delete()


class PermissionService(Service):

	model = PermissionModel

	def _get_code(self, code: str) -> str:
		return code.strip().replace(" ","_").lower()

	async def synchronize(self, permissions: list[Permission]) -> bool:
		p = None
		success = False
		for perm in permissions:
			p = await self._get("code", self._get_code(perm.code), null=True)

			if not p:
				await self._create(code=self._get_code(perm.code), name=perm.name, description=perm.description)
				continue

			if p.name == perm.name and p.description == perm.description:
				continue

			p.name = perm.name
			p.description = perm.description
			success = True
			await p.save()
		return success

	async def get_all(self, exclude_datetime: bool = True) -> list[Permission]:
		perms = []
		if exclude_datetime:
			for perm in await self.model.filter().all():
				perms.append(Permission(code=perm.code, name=perm.name, description=perm.description, dt_created=perm.dt_created, dt_modified=perm.dt_modified))
		else:
			for perm in await self.model.filter().all():
				perms.append(Permission(code=perm.code, name=perm.name, description=perm.description))
		return perms

	async def has_permission(self, account_id: int | str, permission_code: str):
		try:
			acc = await self.core.account_service.get_from_id(account_id)
		except NotFoundSException as e:
			raise AccNotFoundSException()
		except Exception as e:
			raise ServiceException(e)

		if not acc.is_active: return False

		if acc.is_op: return True

		now = datetime.now()

		direct_permission = await AccountPermissionsModel.filter(account=acc, permission_id=permission_code).filter(Q(dt_expiration__isnull=True) | Q(dt_expiration__gt=now)).exists()

		if direct_permission:
			return True

		group_ids = await AccountGroupsModel.filter(account=acc).filter(Q(dt_expiration__isnull=True) | Q(dt_expiration__gt=now)).values_list("group_id", flat=True)

		if not group_ids:
			return False

		group_permission = await GroupPermissionsModel.filter(group_id__in=group_ids, permission_id=permission_code).filter(group__is_active=True).filter(Q(dt_expiration__isnull=True) | Q(dt_expiration__gt=now)).exists()

		return group_permission


class ChronicleService(Service):

	model = ChronicleModel

	def add(self, args: ChronicleTypeArg):
		task = asyncio.create_task(self._create(type=args.type, args=args.model_dump()))