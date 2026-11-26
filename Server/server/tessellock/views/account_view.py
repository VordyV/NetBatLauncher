from fastapi import APIRouter, Request, Depends
from fastapi.responses import HTMLResponse, Response
from ipaddress import ip_address
from tessellock.view_context import UVC, VC, permission
from tessellock.schemes import *
from datetime import timedelta

acc_router = APIRouter(prefix="/accounts")

@acc_router.get("/me")
async def on_acc_accounts_get_me(ctx: VC) -> Account:
	return await ctx.core.account_service.get(account_id=ctx.account_id)

@acc_router.put("/me")
async def on_acc_accounts_update_me(ctx: VC, data: AccountUpdateRequest):
	await ctx.core.account_service.update(ctx.account_id, new_name=data.name, new_email=data.email, initiator=ctx.account_id)

@acc_router.delete("/me")
async def on_acc_accounts_delete_me(ctx: VC):
	await ctx.core.account_service.delete(account_id=ctx.account_id, initiator=ctx.account_id)

@acc_router.post("/changepassword/me")
async def on_acc_accounts_change_pwd_me(ctx: VC, data: ChangePasswordRequest):
	await ctx.core.account_service.change_password(account_id=ctx.account_id, password=data.password, new_password=data.new_password, initiator=ctx.account_id)

@acc_router.get("/", dependencies=[Depends(permission("accounts.read"))])
async def on_acc_accounts_get(ctx: VC, aid: AccountIdField = None, page: int = 1, size: int = 10) -> Account | list[Account]:
	if aid:
		return await ctx.core.account_service.get(account_id=aid)
	else:
		return await ctx.core.account_service.gel_all(page, size)

@acc_router.post("/", dependencies=[Depends(permission("accounts.create"))])
async def on_acc_accounts_create(ctx: VC, data: RegisterRequest):
	return await ctx.core.account_service.create(name=data.name, email=data.email, password=data.password, is_operator=False)

@acc_router.put("/", dependencies=[Depends(permission("accounts.update"))])
async def on_acc_accounts_update(ctx: VC, data: AccountUpdateRequest, aid: AccountIdField):
	await ctx.core.account_service.update(account_id=aid, new_name=data.name, new_email=data.email, initiator=ctx.account_id)

@acc_router.delete("/", dependencies=[Depends(permission("accounts.delete"))])
async def on_acc_accounts_delete(ctx: VC, aid: AccountIdField):
	await ctx.core.account_service.delete(account_id=aid, initiator=ctx.account_id)

@acc_router.post("/changepassword", dependencies=[Depends(permission("accounts.changepassword"))])
async def on_acc_accounts_change_pwd(ctx: VC, data: ChangePasswordRequest, aid: AccountIdField):
	await ctx.core.account_service.change_password(account_id=aid, password=data.password, new_password=data.new_password, initiator=ctx.account_id)

@acc_router.get("/groups/assign", dependencies=[Depends(permission("accounts.groups.assign"))])
async def on_acc_assign_to_group(ctx: VC, aid: AccountIdField, gid: GroupIdField):
	await ctx.core.account_service.add_group(account_id=aid, group_id=gid, initiator=ctx.account_id)

@acc_router.get("/groups/remove", dependencies=[Depends(permission("accounts.groups.remove"))])
async def on_acc_remove_group(ctx: VC, aid: AccountIdField, gid: GroupIdField):
	await ctx.core.account_service.remove_group(account_id=aid, group_id=gid, initiator=ctx.account_id)

@acc_router.get("/groups", dependencies=[Depends(permission("accounts.groups.read"))])
async def on_acc_get_groups(ctx: VC, aid: AccountIdField) -> list[GroupB]:
	return await ctx.core.account_service.get_groups(account_id=aid)

@acc_router.get("/groups/me")
async def on_acc_get_groups_me(ctx: VC) -> list[GroupB]:
	return await ctx.core.account_service.get_groups(account_id=ctx.account_id)

@acc_router.post("/permissions", dependencies=[Depends(permission("accounts.permissions.add"))])
async def on_acc_add_perm(ctx: VC, aid: AccountIdField, permcode: PermCodeField, expiration: timedelta | None = None):
	return await ctx.core.account_service.add_permission(account_id=aid, permission_code=permcode, expiration=expiration, initiator=ctx.account_id)

@acc_router.delete("/permissions", dependencies=[Depends(permission("accounts.permissions.delete"))])
async def on_acc_remove_perm(ctx: VC, aid: AccountIdField, permcode: PermCodeField):
	return await ctx.core.account_service.remove_permission(account_id=aid, permission_code=permcode, initiator=ctx.account_id)

@acc_router.get("/permissions", dependencies=[Depends(permission("accounts.permissions.read"))])
async def on_acc_get_perms(ctx: VC, aid: AccountIdField) -> AccountPermissions:
	return await ctx.core.account_service.get_permissions(account_id=aid)

@acc_router.get("/permissions/me")
async def on_acc_get_perms_me(ctx: VC) -> AccountPermissions:
	return await ctx.core.account_service.get_permissions(account_id=ctx.account_id)

@acc_router.post("/operator", dependencies=[Depends(permission("accounts.operator.make"))])
async def on_acc_make_operator(ctx: VC, aid: AccountIdField):
	await ctx.core.account_service.make_operator(account_id=aid, initiator=ctx.account_id)

@acc_router.delete("/operator", dependencies=[Depends(permission("accounts.operator.delete"))])
async def on_acc_remove_operator(ctx: VC, aid: AccountIdField):
	await ctx.core.account_service.remove_operator(account_id=aid, initiator=ctx.account_id)

@acc_router.post("/active", dependencies=[Depends(permission("accounts.active.enable"))])
async def on_acc_enable(ctx: VC, aid: AccountIdField):
	await ctx.core.account_service.enable(account_id=aid, initiator=ctx.account_id)

@acc_router.delete("/active", dependencies=[Depends(permission("accounts.active.disable"))])
async def on_acc_disable(ctx: VC, aid: AccountIdField):
	await ctx.core.account_service.disable(account_id=aid, initiator=ctx.account_id)