from typing import Annotated
from fastapi import APIRouter, Depends, Request, HTTPException
from fastapi.responses import FileResponse, RedirectResponse, Response
from pydantic import Field
from .schemes import ResponseFile, ResponseGameFilesManifest, GameServers, GameClients, Games, Game, GameMods, GameMod, GameClient, ManifestTask, GameDisclaimer
from .language import Language
from .server_status import ServerStatus
from .storage import StorageObjectType
from .models import GameModel, GameClientModel, GameModModel

router = APIRouter(prefix="/api")

class Context:
	def __init__(self, request: Request):
		self.request = request
		self.core = request.app.state.core
		print(dir(self.core))


async def ctx(request: Request) -> Context:
	return Context(request)


async def game(ctx: "CtxField", gameid: "GameIdField") -> GameModel:
	return await ctx.core.game_service.get_from_ident(gameid)


async def game_client(ctx: "CtxField", game: "GameField", clientid: "GameClientIdField") -> GameClientModel:
	return await ctx.core.game_client_service.get_from_ident(game, clientid)


async def game_client_opt(ctx: "CtxField", game: "GameField", clientid: "GameClientIdField | None" = None) -> GameClientModel | GameModel:
	if clientid is None: return game
	return await ctx.core.game_client_service.get_from_ident(game, clientid)


async def game_mod(ctx: "CtxField", game: "GameField", modid: "GameModIdField") -> GameModModel:
	return await ctx.core.game_mod_service.get_from_ident(game, modid)


CtxField = Annotated[Context, Depends(ctx)]
GameIdField = Annotated[str, Field(max_length=64)]
GameClientIdField = Annotated[str, Field(max_length=64)]
GameModIdField = Annotated[str, Field(max_length=64)]
GameField = Annotated[GameModel, Depends(game)]
GameClientField = Annotated[GameClientModel, Depends(game_client)]
GameClientOptField = Annotated[GameClientModel | GameModel, Depends(game_client_opt)]
GameModField = Annotated[GameModModel, Depends(game_mod)]
ManifestTaskIdField = Annotated[str, Field(max_length=32, min_length=32)]
ClientVersionField = Annotated[str, Field(max_length=8)]
ClientIdField = Annotated[str, Field(max_length=32, min_length=32)]
UserOsField = Annotated[str, Field(max_length=16)]
ManifestSessionIdField = Annotated[str, Field(max_length=32, min_length=32)]


def build_manifest_response(request: Request, manifest, files, size: int, session_id: str | None) -> ResponseGameFilesManifest:
	return ResponseGameFilesManifest(
		ident=manifest.ident,
		files=[ResponseFile(checksum_sha256=file.checksum_sha256, url=str(request.url_for("storage_download", file_id=file.ident)), path=file.path, size=file.size) for file in files],
		size=size,
		session_id=session_id
	)


@router.get("/games/files/manifest", description="Get the list of game files and links to download them")
async def games_files_manifest(ctx: CtxField, game: GameField, install: bool = False, clientversion: ClientVersionField = None, clientid: ClientIdField = None, useros: UserOsField = None) -> ResponseGameFilesManifest:
	manifest = await ctx.core.file_manifest_service.get_for_game(game)
	if not manifest: raise HTTPException(status_code=404, detail="Game files manifest does not exist")
	files, size = await ctx.core.file_manifest_service.get_files(manifest)
	session_id = None
	if install:
		session_id = await ctx.core.manifest_download_session_service.create(manifest, client_version=clientversion, client_id=clientid, user_os=useros, possible_vpn=False)
	return build_manifest_response(ctx.request, manifest, files, size, session_id)


@router.get("/games/files/manifest/endsession")
async def games_files_manifest_end_session(ctx: CtxField, msid: ManifestSessionIdField):
	if not await ctx.core.manifest_download_session_service.complete(msid):
		raise HTTPException(status_code=404, detail="Manifest download session does not exist or is already completed")
	return Response(status_code=204)


@router.get("/games/clients/files/manifest", description="Get the list of game client files and links to download them")
async def games_clients_files_manifest(ctx: CtxField, game: GameField, client: GameClientField) -> ResponseGameFilesManifest:
	manifest = await ctx.core.file_manifest_service.get_for_client(client)
	if not manifest: raise HTTPException(status_code=404, detail="Game client files manifest does not exist")
	files, size = await ctx.core.file_manifest_service.get_files(manifest)
	return build_manifest_response(ctx.request, manifest, files, size, "")


@router.get("/games/mods/files/manifest", description="Get the list of game mod files and links to download them")
async def games_mods_files_manifest(ctx: CtxField, game: GameField, mod: GameModField) -> ResponseGameFilesManifest:
	manifest = await ctx.core.file_manifest_service.get_for_mod(mod)
	if not manifest: raise HTTPException(status_code=404, detail="Game mod files manifest does not exist")
	files, size = await ctx.core.file_manifest_service.get_files(manifest)
	return build_manifest_response(ctx.request, manifest, files, size, "")


@router.get("/storage/download/{file_id}", name="storage_download", description="Download a file by its id")
async def storage_download(ctx: CtxField, file_id: str):
	file = await ctx.core.file_service.get_download(file_id)
	if not file: return Response(status_code=204)
	if ctx.core.storage.object_type == StorageObjectType.path:
		return FileResponse(file.path, filename=file.filename)
	if ctx.core.storage.object_type == StorageObjectType.url:
		return RedirectResponse(url=file.path)
	raise HTTPException(status_code=501, detail="This storage type is not supported for downloads")


@router.get("/games/servers", description="Get the list of game servers for the game or game client")
async def games_servers(ctx: CtxField, game_client: GameClientOptField) -> GameServers:
	return await ctx.core.game_server_service.get_all(game_client)


@router.get("/games/mods")
async def games_mods(ctx: CtxField, game: GameField) -> GameMods:
	return await ctx.core.game_mod_service.get_all(game)


@router.get("/games/mod")
async def games_mod(ctx: CtxField, game: GameField, mod: GameModField) -> GameMod:
	return await ctx.core.game_mod_service.get(game, mod.ident)


@router.get("/games/clients", description="Get the list of clients for the game")
async def games_clients(ctx: CtxField, game: GameField) -> GameClients:
	return await ctx.core.game_client_service.get_all(game)


@router.get("/games/client")
async def games_client(ctx: CtxField, game: GameField, client: GameClientField) -> GameClient:
	return await ctx.core.game_client_service.get(game, client.ident)


@router.get("/games", description="Get the list of games")
async def games(ctx: CtxField) -> Games:
	return await ctx.core.game_service.get_all()


@router.get("/game")
async def game_read(ctx: CtxField, game: GameField) -> Game:
	return await ctx.core.game_service.get(game.ident)


@router.get("/storage/tasks")
async def storage_manifest(ctx: CtxField, manifestid: ManifestTaskIdField) -> ManifestTask:
	task = await ctx.core.storage_task_service.get(manifestid, ctx.core.storage)
	if not task: raise HTTPException(status_code=404, detail="Storage task does not exist")
	return task


@router.get("/heartbeat")
async def heartbeat(ctx: CtxField) -> ServerStatus:
	return ctx.core.status


@router.head("/status")
async def status(ctx: CtxField):
	return Response(status_code=200)


@router.get("/games/disclaimers")
async def games_disclaimers(ctx: CtxField, game: GameField, langcode: Language = Language.en) -> GameDisclaimer:
	disclaimer = await ctx.core.game_disclaimer_service.get(game, langcode)
	if not disclaimer: raise HTTPException(status_code=404, detail="Game disclaimer does not exist")
	return disclaimer


@router.get("/games/servers/numplayers")
async def games_servers_num_players(ctx: CtxField, client: GameClientField) -> int:
	return ctx.core.player_count.get(client.ident, 0)


@router.get("/games/servers/numplayers/all")
async def games_servers_num_players_all(ctx: CtxField) -> int:
	return sum(ctx.core.player_count.values())