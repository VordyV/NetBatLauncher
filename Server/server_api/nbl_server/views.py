from fastapi import APIRouter, Request, Depends, WebSocket
from fastapi.responses import RedirectResponse, Response, FileResponse
from fastapi.exceptions import HTTPException
from typing import Annotated
from pydantic import Field
from .models import GameModel, FileModel, FileManifestModel, GameClientModel, GameServerModel, GameServerDataModel, GameModModel, AccountModel, StorageTaskModel, ManifestFileDownloadSession, GameMLTextModel
from .schemes import ResponseFile, ResponseGameFilesManifest
from .storage import StorageObjectType
from .schemes import GameServerData, GameServers, GameClient, GameClients, Games, Game, GameMod, GameMods, Exchange, AuthCallback, ManifestTask, GameDisclaimer
from .language import Language
from .server_status import ServerStatus
import os
import re
import urllib.parse
import datetime
import uuid

router = APIRouter(prefix="/api")

class Context:

	def __init__(self, request: Request):
		self.request = request
		self.core = request.app.state.core

async def ctx(request: Request):
	return Context(request)

async def game(ctx: CtxField, gameid: GameIdField) -> GameModel:
	game = await GameModel.get_or_none(ident=gameid)
	if not game: raise HTTPException(status_code=404, detail=f"Game '{gameid}' does not exist")
	return game

async def game_client(ctx: CtxField, game: GameField, clientid: GameClientIdField) -> GameClientModel:
	client = await GameClientModel.get_or_none(ident=clientid)
	if not client: raise HTTPException(status_code=404, detail=f"Game client '{clientid}' does not exist")
	return client

async def game_client_opt(ctx: CtxField, game: GameField, clientid: GameClientIdField = None) -> GameClientModel | GameModel:
	if not clientid: return game
	client = await GameClientModel.get_or_none(ident=clientid)
	if not client: raise HTTPException(status_code=404, detail=f"Game client '{clientid}' does not exist")
	return client

async def game_mod(ctx: CtxField, game: GameField, modid: GameModIdField) -> GameModModel:
	mod = await GameModModel.get_or_none(ident=modid)
	if not mod: raise HTTPException(status_code=404, detail=f"Game mod '{modid}' does not exist")
	return mod

def is_perm(permission: str):
	op_sid64 = os.getenv("OP_STEAMID64")

	async def func(ctx: Annotated[Context, Depends(ctx)]):
		pass
		#print(ctx.account.steam_id == op_sid64, ctx.account.steam_id, op_sid64)
		#if ctx.account.is_op or op_sid64 and ctx.account.steam_id == op_sid64: return
		#raise HTTPException(status_code=403, detail="You do not have permission")

	return func

def extract_steamid64(identity: str) -> str:
	if not identity:
		return None
	match = re.search(r"/openid/id/(\d{17,25})$", identity)
	return match.group(1) if match else None

CtxField = Annotated[Context, Depends(ctx)]
GameIdField = Annotated[str, Field(max_length=64)]
GameClientIdField = Annotated[str, Field(max_length=64)]
GameField = Annotated[GameModel, Depends(game)]
GameClientField = Annotated[GameClientModel, Depends(game_client)]
GameClientOptField = Annotated[GameClientModel | GameModel, Depends(game_client_opt)]
GameModIdField = Annotated[str, Field(max_length=64)]
GameModField = Annotated[GameModModel, Depends(game_mod)]
AgentField = Annotated[str, Field(max_length=128)]
OTCField = Annotated[str, Field(max_length=16, min_length=16)]
ManifestTaskIdField = Annotated[str, Field(max_length=32, min_length=32)]
ClientVersionField = Annotated[str, Field(max_length=8)]
ClientIdField = Annotated[str, Field(max_length=32, min_length=32)]
UserOsField = Annotated[str, Field(max_length=16)]
ManifestSessionIdField = Annotated[str, Field(max_length=32, min_length=32)]

'''@router.get("/auth/login", description="Log in to the account via Steam. This will redirect to the provider's login page. Performs authorization and returns the OTC code")
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
	print(url)
	return RedirectResponse(url=url)

@router.get("/auth/callback", name="auth_callback", description="Endpoint to which the provider redirects after successful authentication")
async def auth_callback(request: Request) -> AuthCallback:
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
		account = await AccountModel.create(ident=uuid.uuid4(), name=steam_data.get("personaname", "???"), photo=steam_data.get("avatar"), steam_id=steam_id, steam_url=steam_data.get("profileurl"))
	else:
		account.name = steam_data.get("personaname", "???")
		account.photo = steam_data.get("avatar")
		await account.save()

	#otc = core.create_one_time_code("1") AuthCallback
	#return RedirectResponse(url=f"{request.base_url}auth/callback/{otc}")

@router.get("/auth/exchange", description="Get an access token using the OTAC code after successful authorization")
async def auth_exchange(request: Request, otac: OTCField, agent: AgentField) -> Exchange:
	core = request.app.state.core
	token = core.get_token_one_time_code(otac)
	if token: return Exchange(access_token=token)
	else: raise HTTPException(status_code=400, detail="Invalid or expired code")'''


@router.get("/games/files/manifest", description="Get the list of game files and links to download them")
async def games_files_manifest(ctx: CtxField, game: GameField, install: bool = False, clientversion: ClientVersionField = None, clientid: ClientIdField = None, useros: UserOsField = None) -> ResponseGameFilesManifest:

	if not game.file_manifest: return Response(status_code=404)

	storage = ctx.core.storage

	manifest = await FileManifestModel.get(id=game.file_manifest_id)

	files = []
	size = 0
	for file in await FileModel.filter(manifest=manifest).all():
		size += file.size
		files.append(ResponseFile(checksum_sha256=file.checksum_sha256, url=str(ctx.request.url_for("storage_download", file_id=file.ident)), path=file.local_path, size=file.size))

	sid = None
	if install:
		# Not for Roskomnadzor, this is only for analytics
		possible_vpn = False

		country = ""
		city = ""

		session = await ManifestFileDownloadSession.create(manifest=manifest, client_version=clientversion, client_id=clientid, os=useros, completed=False, possible_vpn=possible_vpn)
		sid = session.ident.hex

	return ResponseGameFilesManifest(
		files=files,
		ident=manifest.ident,
		size=size,
		session_id=sid
	)

@router.get("/games/files/manifest/endsession")
async def games_files_manifest_end_session(ctx: CtxField, msid: ManifestSessionIdField):
	session = await ManifestFileDownloadSession.get_or_none(ident=uuid.UUID(hex=msid))
	if not session or session.completed: return Response(status_code=404)

	session.completed = True
	session.completed_on = datetime.datetime.now()
	await session.save()

@router.get("/games/clients/files/manifest", description="Get the list of game client files and links to download them")
async def games_files_manifest(ctx: CtxField, game: GameField, client: GameClientField) -> ResponseGameFilesManifest:

	if not client.file_manifest: return Response(status_code=404)

	storage = ctx.core.storage

	manifest = await FileManifestModel.get(id=client.file_manifest_id)

	files = []
	size = 0
	for file in await FileModel.filter(manifest=manifest).all():
		size += file.size
		files.append(ResponseFile(checksum_sha256=file.checksum_sha256, url=str(ctx.request.url_for("storage_download", file_id=file.ident)), path=file.local_path, size=file.size))

	return ResponseGameFilesManifest(
		files=files,
		ident=manifest.ident,
		size=size,
		session_id=""
	)

@router.get("/games/mods/files/manifest", description="")
async def games_files_manifest(ctx: CtxField, game_mod: GameModField) -> ResponseGameFilesManifest:

	if not game_mod.file_manifest: return Response(status_code=404)

	storage = ctx.core.storage

	manifest = await FileManifestModel.get(id=game_mod.file_manifest_id)

	files = []
	size = 0
	for file in await FileModel.filter(manifest=manifest).all():
		size += file.size
		files.append(ResponseFile(checksum_sha256=file.checksum_sha256, url=str(ctx.request.url_for("storage_download", file_id=file.ident)), path=file.local_path, size=file.size))

	return ResponseGameFilesManifest(
		files=files,
		ident=manifest.ident,
		size=size,
		session_id=""
	)

@router.get("/storage/download/{file_id}", name="storage_download", description="Download a file by its id")
async def storage_download(request: Request, file_id: str):
	file = await FileModel.get_or_none(ident=file_id)
	if not file: return Response(status_code=204)

	core = request.app.state.core
	storage = core.storage

	if storage.object_type == StorageObjectType.path:
		return FileResponse(
            file.path,
            filename=file.filename,
        )

@router.get("/games/servers", description="Get the list of game servers for the game client")
async def games_servers(request: Request, game_client: GameClientOptField) -> GameServers:
	result = []

	if isinstance(game_client, GameModel):
		for client in await GameClientModel.filter(game=game_client).all():
			for server in await GameServerModel.filter(client=client).all():
				result.append(GameServerData(address=server.address, query_port=server.query_port, name=server.name, clientid=client.ident))
			for server in await GameServerDataModel.filter(client=client).all():
				result.append(GameServerData(address=server.address, query_port=server.query_port, name=server.name, clientid=client.ident))
	else:
		for server in await GameServerModel.filter(client=game_client).all():
			result.append(GameServerData(address=server.address, query_port=server.query_port, name=server.name, clientid=game_client.ident))
		for server in await GameServerDataModel.filter(client=game_client).all():
			result.append(GameServerData(address=server.address, query_port=server.query_port, name=server.name, clientid=game_client.ident))
	return GameServers(servers=result)

@router.get("/games/mods")
async def games_mods(request: Request, game: GameField) -> GameMods:

	mods = []
	for mod in await GameModModel.filter(game=game).all():
		mods.append(GameMod(modid=mod.ident, name=mod.name, version=mod.version, developer=mod.developer, files=bool(mod.file_manifest), default=mod.default))

	return GameMods(mods=mods)

@router.get("/games/clients", description="Get the list of clients for the game")
async def games_clients(request: Request, game: GameField) -> GameClients:

	result = []
	for client in await GameClientModel.filter(game=game).all():
		result.append(GameClient(ident=client.ident, name=client.name, short_name=client.short_name, files=bool(client.file_manifest), reference=client.reference, address=client.master_server_address))

	return GameClients(clients=result, gameid=game.ident)

@router.get("/games", description="Get the list of games")
async def games(request: Request) -> Games:

	result = []
	for game in await GameModel.filter().all():
		default_mod = await GameModModel.get_or_none(game=game.id, default=True)
		ref_client = await GameClientModel.get_or_none(game=game.id, reference=True)
		result.append(Game(ident=game.ident, name=game.name, short_name=game.short_name, files=bool(game.file_manifest), default_mod_id=default_mod.ident if default_mod else None, reference_client_id=ref_client.ident if ref_client else None))

	return Games(games=result)

@router.get("/storage/tasks")
async def storage_manifest(request: Request, manifestid: ManifestTaskIdField) -> ManifestTask:
	core = request.app.state.core
	storage = core.storage

	task = storage.get_task(manifestid)
	manifest = await StorageTaskModel.get_or_none(ident=manifestid)
	if not task or not manifest: return Response(status_code=404)

	return ManifestTask(id=manifest.ident, status=manifest.status.value, progress=task.progress)

@router.get("/heartbeat")
async def heartbeat(ctx: CtxField) -> ServerStatus:
	return ctx.core.status

@router.get("/games/disclaimers")
async def games_disclaimers(ctx: CtxField, game: GameField, langcode: Language = Language.en) -> GameDisclaimer:
	text = await GameMLTextModel.filter(game=game, lang_code=langcode).get_or_none()
	if not text: return Response(status_code=404)
	return GameDisclaimer(text=text.text)