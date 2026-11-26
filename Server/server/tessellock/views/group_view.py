from fastapi import APIRouter, Request, Depends
from fastapi.responses import HTMLResponse, Response
from ipaddress import ip_address
from tessellock.view_context import UVC, VC, permission
from tessellock.schemes import *
from datetime import timedelta

gr_router = APIRouter(prefix="/groups")

@gr_router.post("/", dependencies=[Depends(permission("groups.create"))])
async def on_gr_create(ctx: VC, data: GroupCreateRequest) -> Group:
	return await ctx.core.group_service.create(name=data.name, description=data.description, is_active=data.is_active, initiator=ctx.account_id)

@gr_router.get("/", dependencies=[Depends(permission("groups.read"))])
async def on_gr_get(ctx: VC, gid: GroupIdField = None) -> Group | list[Group]:
	if gid:
		return await ctx.core.group_service.get(gid)
	return await ctx.core.group_service.get_all()

@gr_router.put("/", dependencies=[Depends(permission("groups.update"))])
async def on_gr_update(ctx: VC, gid: GroupIdField, data: GroupUpdateRequest):
	return await ctx.core.group_service.update(group_id=gid, new_name=data.name, new_description=data.description, initiator=ctx.account_id)

@gr_router.delete("/", dependencies=[Depends(permission("groups.delete"))])
async def on_gr_delete(ctx: VC, gid: GroupIdField):
	return await ctx.core.group_service.delete(group_id=gid, initiator=ctx.account_id)

@gr_router.get("/permissions", dependencies=[Depends(permission("groups.permissions.read"))])
async def on_gr_get_perms(ctx: VC, gid: GroupIdField) -> list[Permission]:
	return await ctx.core.group_service.get_permissions(group_id=gid)

@gr_router.post("/permissions", dependencies=[Depends(permission("groups.permissions.add"))])
async def on_gr_add_perm(ctx: VC, gid: GroupIdField, permcode: PermCodeField, expiration: timedelta | None = None):
	return await ctx.core.group_service.add_permission(group_id=gid, permission_code=permcode, expiration=expiration, initiator=ctx.account_id)

@gr_router.delete("/permissions", dependencies=[Depends(permission("groups.permissions.delete"))])
async def on_gr_remove_perm(ctx: VC, gid: GroupIdField, permcode: PermCodeField):
	return await ctx.core.group_service.remove_permission(group_id=gid, permission_code=permcode, initiator=ctx.account_id)

@gr_router.post("/active", dependencies=[Depends(permission("groups.active.enable"))])
async def on_gr_enable(ctx: VC, gid: GroupIdField):
	await ctx.core.group_service.enable(group_id=gid, initiator=ctx.account_id)

@gr_router.delete("/active", dependencies=[Depends(permission("groups.active.disable"))])
async def on_gr_disable(ctx: VC, gid: GroupIdField):
	await ctx.core.group_service.disable(group_id=gid, initiator=ctx.account_id)