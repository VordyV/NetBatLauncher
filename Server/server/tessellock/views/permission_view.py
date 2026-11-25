from fastapi import APIRouter, Request, Depends
from fastapi.responses import HTMLResponse, Response
from ipaddress import ip_address
from tessellock.view_context import UVC, VC, permission
from tessellock.schemes import *

perm_router = APIRouter(prefix="/permissions")

@perm_router.get("/", dependencies=[Depends(permission("permissions.read"))])
async def on_perm_get(ctx: VC) -> list[Permission]:
	return await ctx.core.permission_service.get_all()