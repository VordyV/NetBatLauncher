from fastapi import APIRouter, Request, Depends, WebSocket
from fastapi.responses import RedirectResponse, Response, FileResponse
from fastapi.exceptions import HTTPException
from typing import Annotated
from pydantic import Field

from .models import GameModel, FileModel, FileManifestModel, GameClientModel, GameServerModel, GameServerDataModel, GameModModel
from .schemes import ResponseFile, ResponseGameFilesManifest
from .storage import StorageObjectType
from .schemes import GameServerData, GameServers, GameClient, GameClients, Games, Game, GameMod, GameMods

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

CtxField = Annotated[Context, Depends(ctx)]
GameIdField = Annotated[str, Field(max_length=64)]
GameClientIdField = Annotated[str, Field(max_length=64)]
GameField = Annotated[GameModel, Depends(game)]
GameClientField = Annotated[GameClientModel, Depends(game_client)]
GameClientOptField = Annotated[GameClientModel | GameModel, Depends(game_client_opt)]
GameModIdField = Annotated[str, Field(max_length=64)]
GameModField = Annotated[GameModModel, Depends(game_mod)]

@router.get("/games/files/manifest", description="Get the list of game files and links to download them")
async def games_files_manifest(ctx: CtxField, game: GameField) -> ResponseGameFilesManifest:

	if not game.file_manifest: return Response(status_code=204)

	storage = ctx.core.storage

	manifest = await FileManifestModel.get(id=game.file_manifest_id)

	files = []
	size = 0
	for file in await FileModel.filter(manifest=manifest).all():
		size += file.size
		files.append(ResponseFile(checksum_sha256=file.checksum_sha256, url=str(ctx.request.url_for("storage_download", file_id=file.ident)), path=file.local_path, size=file.size))

	return ResponseGameFilesManifest(
		files=files,
		ident=manifest.ident,
		size=size
	)

@router.get("/games/clients/files/manifest", description="Get the list of game client files and links to download them")
async def games_files_manifest(ctx: CtxField, game: GameField, client: GameClientField) -> ResponseGameFilesManifest:

	if not client.file_manifest: return Response(status_code=204)

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
		size=size
	)

@router.get("/games/mods/files/manifest", description="")
async def games_files_manifest(ctx: CtxField, game_mod: GameModField) -> ResponseGameFilesManifest:

	if not game_mod.file_manifest: return Response(status_code=204)

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
		size=size
	)

@router.get("/storage/download/{file_id}", name="storage_download", description="Download a file by its id")
async def storage_download(request: Request, file_id: str):
	file = await FileModel.get_or_none(ident=file_id)
	if not file: raise Response(status_code=204)

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