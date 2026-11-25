from .models import *
from .schemes import *
from .language import Language
from .service_exceptions import *
from ipaddress import IPv4Address
from dataclasses import dataclass
from zoneinfo import ZoneInfo
from .service import Service
import asyncio
import bcrypt
import datetime
import os
import uuid


def now():
    return datetime.datetime.now(ZoneInfo(os.getenv("TIMEZONE", "Europe/Moscow")))


@dataclass
class Account:
    name: str
    permissions: list[str]
    operator: bool
    session_id: int


class GameService(Service):

    async def get_from_ident(self, ident: str) -> GameModel:
        game = await GameModel.get_or_none(ident=ident)
        if not game: raise GameNotFoundSException(f"Game '{ident}' not found")
        return game

    def _schema(self, game, default_mod=None, reference_client=None):
        return Game(ident=game.ident, name=game.name, short_name=game.short_name, files=bool(game.file_manifest), default_mod_id=default_mod.ident if default_mod else None, reference_client_id=reference_client.ident if reference_client else None)

    async def get(self, ident: str) -> Game:
        game = await self.get_from_ident(ident)
        default_mod = await GameModModel.get_or_none(game=game.id, default=True)
        ref_client = await GameClientModel.get_or_none(game=game.id, reference=True)
        return self._schema(game, default_mod, ref_client)

    async def get_all(cls) -> Games:
        result = []
        for game in await GameModel.all():
            default_mod = await GameModModel.get_or_none(game=game.id, default=True)
            ref_client = await GameClientModel.get_or_none(game=game.id, reference=True)
            result.append(cls._schema(game, default_mod, ref_client))
        return Games(games=result)


class GameClientService(Service):

    async def get_from_ident(self, ident: str) -> GameClientModel:
        client = await GameClientModel.get_or_none(ident=ident)
        if not client: raise GameClientNotFoundSException(f"Game client '{ident}' not found")
        return client

    def _schema(self, client):
        return GameClient(ident=client.ident, name=client.name, short_name=client.short_name, files=bool(client.file_manifest), reference=client.reference, address=client.master_server_address)

    async def get(self, ident: str) -> GameClient:
        return self._schema(await self.get_from_ident(ident))

    async def get_all(self, game: GameModel) -> GameClients:
        return GameClients(clients=[self._schema(c) for c in await GameClientModel.filter(game=game).all()], gameid=game.ident)

class GameModService(Service):

    async def get_from_ident(self, ident: str) -> GameModModel:
        mod = await GameModModel.get_or_none(ident=ident)
        if not mod: raise GameModNotFoundSException(f"Game mod '{ident}' not found")
        return mod

    def _schema(self, mod):
        return GameMod(modid=mod.ident, name=mod.name, version=mod.version, developer=mod.developer, files=bool(mod.file_manifest), default=mod.default)

    async def get(self, ident: str) -> GameMod:
        return self._schema(await self.get_from_ident(ident))

    async def get_all(self, game: GameModel) -> GameMods:
        return GameMods(mods=[self._schema(m) for m in await GameModModel.filter(game=game).all()])

class GameServerService(Service):

    def _schema(self, server, client):
        return GameServerData(address=server.address, query_port=server.query_port, name=server.name, clientid=client.ident)

    async def get_all(self, target: GameModel | GameClientModel) -> GameServers:
        clients = await GameClientModel.filter(game=target).all() if isinstance(target, GameModel) else [target]
        result = []
        for client in clients:
            result.extend(self._schema(s, client) for s in await GameServerModel.filter(client=client).all())
            result.extend(self._schema(s, client) for s in await GameServerDataModel.filter(client=client).all())
        return GameServers(servers=result)

class FileManifestService(Service):

    async def get_from_id(self, ident: int) -> FileManifestModel:
        manifest = await FileManifestModel.get_or_none(id=ident)
        if not manifest: raise ManifestNotFoundSException(f"Manifest '{ident}' not found")
        return manifest

    async def get_files(self, manifest: FileManifestModel) -> tuple[list[ResponseFile], int]:
        files, size = [], 0
        for file in await FileModel.filter(manifest=manifest).all():
            size += file.size
            files.append(ResponseFile(checksum_sha256=file.checksum_sha256, url="", path=file.local_path or "", size=file.size))
        return files, size

    async def get_for_game(self, game: GameModel) -> FileManifestModel | None:
        return await self.get_from_id(game.file_manifest_id) if game.file_manifest else None

    async def get_for_client(self, client: GameClientModel) -> FileManifestModel | None:
        return await self.get_from_id(client.file_manifest_id) if client.file_manifest else None

    async def get_for_mod(self, mod: GameModModel) -> FileManifestModel | None:
        return await self.get_from_id(mod.file_manifest_id) if mod.file_manifest else None

class ManifestDownloadSessionService(Service):

    async def create(self, manifest: FileManifestModel, client_version: str | None = None, client_id: str | None = None, user_os: str | None = None, possible_vpn: bool = False) -> str:
        session = await ManifestFileDownloadSession.create(manifest=manifest, client_version=client_version, client_id=client_id, os=user_os, completed=False, possible_vpn=possible_vpn)
        return session.ident.hex

    async def complete(self, ident: str) -> bool:
        try: session_uuid = uuid.UUID(hex=ident)
        except (ValueError, TypeError): return False
        session = await ManifestFileDownloadSession.get_or_none(ident=session_uuid)
        if not session or session.completed: return False
        session.completed = True
        session.completed_on = now()
        await session.save()
        return True

class StorageTaskService(Service):

    async def get(self, ident: str, storage) -> ManifestTask | None:
        task = storage.get_task(ident)
        model = await StorageTaskModel.get_or_none(ident=ident)
        return ManifestTask(id=model.ident, status=model.status.value, progress=task.progress, error=model.traceback) if task and model else None

class GameDisclaimerService(Service):

    async def get(self, game: GameModel, langcode: Language = Language.en) -> GameDisclaimer | None:
        text = await GameMLTextModel.filter(game=game, lang_code=langcode).get_or_none()
        return GameDisclaimer(text=text.text) if text else None
