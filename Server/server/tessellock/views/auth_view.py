from fastapi import APIRouter, Request
from fastapi.responses import HTMLResponse, Response
from ipaddress import ip_address
from tessellock.view_context import UVC, VC
from tessellock.schemes import *

auth_router = APIRouter(prefix="/auth")

@auth_router.post("/register")
async def on_auth_register(ctx: UVC, data: RegisterRequest):
	await ctx.core.account_service.create(name=data.name, email=data.email, password=data.password, is_operator=False)

@auth_router.post("/login")
async def on_auth_login(ctx: UVC, data: LoginRequest) -> AccountSession:
	aid = await ctx.core.account_service.authenticate(data.name_or_email, data.password)
	user_agent = ctx.request.headers.get("user-agent", "")
	token = await ctx.core.account_service.authorize(aid, ip_address(ctx.request.client.host), user_agent)
	return AccountSession(access_token=token)

@auth_router.get("/logout")
async def on_auth_logout(ctx: VC):
	await ctx.core.account_session_service.delete(ctx.session_id, initiator=ctx.account_id)