import datetime
import os
from authx import TokenPayload
from authx.exceptions import TokenExpiredError
from typing import Annotated
from fastapi import APIRouter, Request, Depends
from fastapi.responses import RedirectResponse, Response
from fastapi.exceptions import HTTPException
from pydantic import Field
from .models import AccountModel, GameModel, FileManifestModel, FileModel, ManifestFilesModel
from .schemes import (
	ResponseExchange,
	ResponseRefresh,
	ResponseAccountData,
	RequestGameCreate,
	ResponseGamesReadAll,
	ResponseGamesRead,
	RequestGameUpdate,
	ResponseGameFilesUpload,
	ResponseFile,
	ResponseGameFilesManifest,
	ResponseGameFilesUploadProcess,
	ResponseStorageTask
)
import urllib.parse
import re
import uuid
import pathlib

router = APIRouter(prefix="/api")

class Context:

	def __init__(self, request: Request, payload: TokenPayload, account: AccountModel):
		self.core = request.app.state.core
		self.payload = payload
		self.account = account

def extract_steamid64(identity: str) -> str:
	if not identity:
		return None
	match = re.search(r"/openid/id/(\d{17,25})$", identity)
	return match.group(1) if match else None

async def ctx(request: Request):
	core = request.app.state.core
	token = await core.auth.get_access_token_from_request(request)
	payload = core.auth.verify_token(token)

	account = await AccountModel.get_or_none(uuid=uuid.UUID(payload.sub))
	if core.has_block_token(payload.jti) or not account: raise TokenExpiredError("Token has expired")

	return Context(request, payload, account)

async def game(ctx: CtxField, gameid: GameIdField) -> GameModel:
	game = await GameModel.get_or_none(ident=gameid)
	if not game: raise HTTPException(status_code=404, detail=f"Game {gameid} does not exist")
	return game

def is_perm(permission: str):
	op_sid64 = os.getenv("OP_STEAMID64")

	async def func(ctx: Annotated[Context, Depends(ctx)]):
		print(ctx.account.steam_id == op_sid64, ctx.account.steam_id, op_sid64)
		if ctx.account.is_op or op_sid64 and ctx.account.steam_id == op_sid64: return
		raise HTTPException(status_code=403, detail="You do not have permission")

	return func

CtxField = Annotated[Context, Depends(ctx)]
GameIdField = Annotated[str, Field(max_length=64)]
AgentField = Annotated[str, Field(max_length=128)]
OTACField = Annotated[str, Field(max_length=16, min_length=16)]
FileNameField = Annotated[str, Field(max_length=128, min_length=1)]
GameField = Annotated[GameModel, Depends(game)]

@router.get("/auth/login", description="Log in to the account via Steam. This will redirect to the provider's login page. Performs authorization and returns the OTAC code")
async def auth_login(request: Request):
	params = {
		"openid.ns": "http://specs.openid.net/auth/2.0",
		"openid.mode": "checkid_setup",
		"openid.return_to": str(request.url_for("auth_callback")),
		"openid.realm": str(request.base_url),
		"openid.identity": "http://specs.openid.net/auth/2.0/identifier_select",
		"openid.claimed_id": "http://specs.openid.net/auth/2.0/identifier_select",
	}
	url = f"https://steamcommunity.com/openid/login?{urllib.parse.urlencode(params)}"
	return RedirectResponse(url=url)

@router.get("/auth/callback", name="auth_callback", description="Endpoint to which the provider redirects after successful authentication")
async def auth_callback(request: Request):
	core = request.app.state.core

	params = dict(request.query_params)

	if params.get("openid.mode") != "id_res":
		raise HTTPException(status_code=400, detail="Invalid OpenID mode")

	nonce = params.get("openid.response_nonce")
	if not core.is_nonce_auth_valid(nonce):
		raise HTTPException(status_code=400, detail="Invalid or reused nonce")

	is_valid = await core.verify_steam_openid(params)
	if not is_valid:
		raise HTTPException(status_code=401, detail="Steam verification failed")

	claimed_id = params.get("openid.claimed_id") or params.get("openid.identity")
	steam_id = extract_steamid64(claimed_id)
	if not steam_id:
		raise HTTPException(status_code=400, detail="Cannot extract SteamID64")

	account = await AccountModel.get_or_none(steam_id=steam_id)
	steam_data = await core.get_steam_data(steam_id=steam_id)

	if not account:
		account = await AccountModel.create(uuid=uuid.uuid4(), name=steam_data.get("personaname", "???"), photo=steam_data.get("avatar"), steam_id=steam_id, steam_url=steam_data.get("profileurl"), is_active=True)
	else:
		account.name = steam_data.get("personaname", "???")
		account.photo = steam_data.get("avatar")
		await account.save()

	token = core.auth.create_access_token(uid=account.uuid.hex)

	otac = core.create_one_time_auth_code(token)
	return RedirectResponse(url=f"{request.base_url}auth/callback/{otac}")

@router.get("/auth/exchange", description="Get an access token using the OTAC code after successful authorization")
async def auth_exchange(request: Request, otac: OTACField, agent: AgentField) -> ResponseExchange:
	core = request.app.state.core
	token = core.get_token_one_time_code(otac)
	if token: return ResponseExchange(access_token=token)
	else: raise HTTPException(status_code=400, detail="Invalid or expired code")

@router.get("/auth/refresh", description="Get a new access token")
async def auth_refresh(ctx: CtxField) -> ResponseRefresh:
	now = datetime.datetime.now(ctx.core.time_zone)

	if ctx.payload.exp.astimezone(ctx.core.time_zone) - now > datetime.timedelta(seconds=int(os.getenv("MIN_REM_LIFETIME_REFRESH"))): raise HTTPException(status_code=400, detail="The token is too young")

	account = await AccountModel.get_or_none(uuid=uuid.UUID(ctx.payload.sub))
	if not account: raise HTTPException(status_code=401, detail="Account not found")

	steam_data = await ctx.core.get_steam_data(steam_id=account.steam_id)
	account.name = steam_data.get("personaname", account.name)
	account.photo = steam_data.get("avatar", account.photo)
	await account.save()

	token = ctx.core.auth.create_access_token(uid=account.uuid.hex)
	return ResponseRefresh(access_token=token)

@router.get("/auth/logout", description="Log out of the account. The current access token will become invalid")
async def auth_logout(ctx: CtxField):
	date = ctx.payload.exp.astimezone(ctx.core.time_zone)
	ctx.core.block_token(ctx.payload.jti, date)

@router.get("/accounts/me", description="Get information about your account")
async def get_account_data(ctx: CtxField) -> ResponseAccountData:
	return ResponseAccountData(id=ctx.account.uuid.hex, name=ctx.account.name, steam_url=ctx.account.steam_url, photo=ctx.account.photo, created_at=ctx.account.created_at.astimezone(ctx.core.time_zone).date())

@router.post("/games", dependencies=[Depends(is_perm("games.create"))])
async def games_create(ctx: CtxField, data: RequestGameCreate):
	if await GameModel.filter(ident=data.ident).exists(): raise HTTPException(status_code=400, detail=f"A game with id '{data.ident}' already exists")

	await GameModel.create(ident=data.ident, name=data.name, short_name=data.short_name)

@router.get("/games", dependencies=[Depends(is_perm("games.read"))])
async def games_read(ctx: Annotated[Context, Depends(ctx)], gameid: str = None) -> ResponseGamesReadAll | ResponseGamesRead:
	if not gameid:
		games = [ResponseGamesRead(ident=game.ident, name=game.name, short_name=game.short_name, created_at=game.created_at, modified=game.modified) for game in await GameModel.filter().all()]
		return ResponseGamesReadAll(games=games)
	else:
		game = await GameModel.get_or_none(ident=gameid)
		if not game: raise HTTPException(status_code=404, detail=f"Game {gameid} does not exist")
		return ResponseGamesRead(ident=game.ident, name=game.name, short_name=game.short_name, created_at=game.created_at, modified=game.modified)

@router.put("/games", dependencies=[Depends(is_perm("games.update"))])
async def games_update(ctx: CtxField, gameid: GameIdField, data: RequestGameUpdate):
	game = await GameModel.get_or_none(ident=gameid)
	if not game: raise HTTPException(status_code=404, detail=f"Game {gameid} does not exist")

	if gameid != data.ident and await GameModel.filter(ident=data.ident).exists(): raise HTTPException(status_code=400, detail=f"A game with id '{data.ident}' already exists")

	game.ident = data.ident
	game.name = data.name
	game.short_name = data.short_name

	await game.save()

@router.delete("/games", dependencies=[Depends(is_perm("games.delete"))])
async def games_delete(ctx: CtxField, gameid: GameIdField):
	game = await GameModel.get_or_none(ident=gameid)
	if not game: raise HTTPException(status_code=404, detail=f"Game {gameid} does not exist")

	await game.delete()

@router.get("/games/files/upload", dependencies=[Depends(is_perm("games.upload"))])
async def games_files_upload(ctx: CtxField, gameid: GameIdField, filename: FileNameField) -> ResponseGameFilesUpload:
	game = await GameModel.get_or_none(ident=gameid)
	if not game: raise HTTPException(status_code=404, detail=f"Game {gameid} does not exist")
	if game.file_manifest: raise HTTPException(status_code=400, detail=f"Game already has a file manifest")

	if filename != "link" and pathlib.Path(filename).suffix.lower() != ".zip": raise HTTPException(status_code=400, detail="The file must have a .zip extension")

	otac = ctx.core.create_one_time_auth_code(f"{ctx.account.uuid}:{gameid}:{filename}")
	return ResponseGameFilesUpload(otac=otac)

@router.put("/games/files/upload", dependencies=[])
async def games_files_upload(request: Request, otac: OTACField, mode: str = "content", link: str = None) -> ResponseGameFilesUploadProcess:
	core = request.app.state.core
	acc_gid_fn = core.get_token_one_time_code(otac)
	if not acc_gid_fn: raise HTTPException(status_code=400, detail="Invalid or expired code")

	account_uuid, gameid, filename = acc_gid_fn.split(":")

	game = await GameModel.get_or_none(ident=gameid)
	if not game: raise HTTPException(status_code=404, detail=f"Game {gameid} does not exist")
	if game.file_manifest: raise HTTPException(status_code=400, detail=f"Game already has a file manifest")

	storage = core.get_storage()
	if mode == "content":
		content = await request.body()
		if not content: raise HTTPException(status_code=400, detail="Empty file")

		await storage.add_files(content)
	elif mode == "link":
		if not link: raise HTTPException(status_code=400, detail="The link parameter must not be empty when in link mode")

		task_id = await storage.add_files_via_link(link)
		return ResponseGameFilesUploadProcess(task_ident=task_id)
	else: raise HTTPException(status_code=400, detail="Mode is incorrect")

@router.get("/storage/tasks")
async def storage_tasks(ctx: CtxField, taskid: str):
	storage = ctx.core.get_storage()
	task = storage.get_task(taskid)
	if not task: raise HTTPException(status_code=404, detail=f"Task '{taskid}' not found")
	return ResponseStorageTask(
		task_ident=task.ident,
		status=task.status,
		error=str(task.error),
		exception=task.error.__traceback__ if task.error else None
	)

@router.get("/storage/download")
async def storage_download(): pass

@router.get("/games/files/manifest", dependencies=[Depends(is_perm("games.manifest.read"))])
async def games_files_manifest(ctx: CtxField, game: GameField):
	if not game.file_manifest: return Response(status_code=204)

	files = []

	storage = ctx.core.get_storage()

	for file in await ManifestFilesModel.filter(manifest=game.file_manifest).get():
		url = storage.get_url(uuid=file.ident)

	return files

