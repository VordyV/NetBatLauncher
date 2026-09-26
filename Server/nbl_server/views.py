from fastapi import APIRouter, Request, Depends
from fastapi.responses import RedirectResponse, Response, FileResponse
from fastapi.exceptions import HTTPException
from typing import Annotated
from pydantic import Field
from .models import GameModel, FileModel, FileManifestModel, GameClientModel, GameServerModel
from .schemes import ResponseFile, ResponseGameFilesManifest
from .storage import StorageObjectType
from .schemes import GameServerData, GameServers

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

CtxField = Annotated[Context, Depends(ctx)]
GameIdField = Annotated[str, Field(max_length=64)]
GameClientIdField = Annotated[str, Field(max_length=64)]
GameField = Annotated[GameModel, Depends(game)]
GameClientField = Annotated[GameClientModel, Depends(game_client)]

@router.get("/games/files/manifest")
async def games_files_manifest(ctx: CtxField, game: GameField) -> ResponseGameFilesManifest:

	if not game.file_manifest: return Response(status_code=204)

	storage = ctx.core.storage

	manifest = await FileManifestModel.get(id=game.file_manifest_id)

	files = []
	for file in await FileModel.filter(manifest=manifest).all():
		files.append(ResponseFile(checksum_sha256=file.checksum_sha256, url=str(ctx.request.url_for("storage_download", file_id=file.ident)), path=file.local_path))

	return ResponseGameFilesManifest(
		files=files,
		ident=manifest.ident,
	)

@router.get("/games/clients/files/manifest")
async def games_files_manifest(ctx: CtxField, game: GameField, client: GameClientField) -> ResponseGameFilesManifest:

	if not client.file_manifest: return Response(status_code=204)

	storage = ctx.core.storage

	manifest = await FileManifestModel.get(id=client.file_manifest_id)

	files = []
	for file in await FileModel.filter(manifest=manifest).all():
		files.append(ResponseFile(checksum_sha256=file.checksum_sha256, url=str(ctx.request.url_for("storage_download", file_id=file.ident)), path=file.local_path))

	return ResponseGameFilesManifest(
		files=files,
		ident=manifest.ident,
	)

@router.get("/storage/download/{file_id}", name="storage_download")
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

@router.get("/games/servers")
async def games_servers(request: Request, game: GameField) -> GameServers:

	result = []
	for client in await GameClientModel.filter(game=game).all():
		for server in await GameServerModel.filter(client=client).all():
			result.append(GameServerData(address=server.address, query_port=server.query_port, name=server.name))
	return GameServers(servers=result)