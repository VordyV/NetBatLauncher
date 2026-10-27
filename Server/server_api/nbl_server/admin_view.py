import os

from starlette_admin.contrib.tortoise import (InlineModelView as TortoiseInlineModelView, ModelView)
from starlette_admin import CustomView, route
from starlette_admin.fields import *
from starlette_admin.validators import slug, length, number_range
from starlette_admin.actions import row_action
from starlette.responses import RedirectResponse
from starlette.exceptions import HTTPException
from starlette_admin import TextWidget, HtmlWidget
from .models import GameClientModel, GameModModel, SessionAccountModel, AccountModel, FileModel, GameModel, ManifestFileDownloadSession, FileManifestModel, GameMLTextModel
from tortoise.models import Model
import httpx
from .services import AccountService, Account
from urllib.parse import quote
from starlette.datastructures import UploadFile
from starlette.requests import Request
from starlette.responses import JSONResponse, Response
from pathlib import Path
import anyio
from uuid import uuid4
from tortoise.expressions import Q
from urllib.parse import urlencode
import asyncio
import re
import time

class BaseCustomView(CustomView):

    perm_read: str
    perm_create: str
    perm_edit: str
    perm_delete: str

    def __init__(self, core, *args, **kwargs):
        self.core = core
        super().__init__(*args, **kwargs)

        if hasattr(self, "build_widget"): self.widget = self.build_widget

    def _is_permission(self, request: Request, permission) -> bool:
        permissions = request.state.admin_user.permissions
        operator = request.state.admin_user.operator
        if not operator and permission not in permissions: return False
        return True

    def is_accessible(self, request: Request) -> bool:
        return self._is_permission(request, self.perm_read)

    def can_create(self, request: Request) -> bool:
        return self._is_permission(request, self.perm_create)

    def can_edit(self, request: Request) -> bool:
        return self._is_permission(request, self.perm_edit)

    def can_delete(self, request: Request) -> bool:
        return self._is_permission(request, self.perm_delete)

class BaseModelView(ModelView):

    perm_read: str
    perm_create: str
    perm_edit: str
    perm_delete: str
    perm_actions: dict[str, str] = {}

    def __init__(self, core, *args, **kwargs):
        self.core = core
        super().__init__(*args, **kwargs)

    def _is_permission(self, request: Request, permission) -> bool:
        permissions = request.state.admin_user.permissions
        operator = request.state.admin_user.operator
        if not operator and permission not in permissions: return False
        return True

    def is_accessible(self, request: Request) -> bool:
        return self._is_permission(request, self.perm_read)

    def can_create(self, request: Request) -> bool:
        return self._is_permission(request, self.perm_create)

    def can_edit(self, request: Request) -> bool:
        return self._is_permission(request, self.perm_edit)

    def can_delete(self, request: Request) -> bool:
        return self._is_permission(request, self.perm_delete)

    async def is_action_allowed(self, request: Request, name: str) -> bool:
        if name in self.perm_actions:
            permissions = request.state.admin_user.permissions
            if self.perm_actions[name] not in permissions: return False
        return await super().is_action_allowed(request, name)

class GeneralView(BaseCustomView):

    perm_read: str = "admin.general.read"
    perm_create: str = "admin.general.create"
    perm_edit: str = "admin.general.edit"
    perm_delete: str = "admin.general.delete"

    menu_label = "General"

    '''async def build_widget(self, request: Request) -> ColumnWidget:
        return ColumnWidget(
            children=[
                CardRowWidget(
                    children=[
                        Col(
                            StatWidget(
                                title="1",
                                value_callback=self._count_downloads,
                            )
                        )
                    ]
                )
            ]
        )'''

    async def _count_downloads(self, request: Request) -> int:
        return 124

async def _get_NumDownloadsGame(request, obj):
    if obj.file_manifest:
        mfds = await ManifestFileDownloadSession.filter(Q(manifest_id=obj.file_manifest_id) & Q(completed_on__not_isnull=True)).count()
        return mfds
    return 0

async def _get_manifest_ident(request, obj):
    manifest = await FileManifestModel.get_or_none(id=obj.file_manifest_id)
    return manifest.ident if manifest else ""

class GameDisclaimerTextInline(TortoiseInlineModelView):

    model = GameMLTextModel
    fk_attr = "game"
    fields = ["lang_code", "text"]
    extra = 1

class GameView(BaseModelView):

    perm_read: str = "admin.game.read"
    perm_create: str = "admin.game.create"
    perm_edit: str = "admin.game.edit"
    perm_delete: str = "admin.game.delete"

    fields = [
        StringField("ident", validators=[length(min=1, max=64), slug(allow_underscores=True)], label="ID", help_text="", required=True, copy_to_clipboard=True),
        StringField("name", validators=[length(min=1, max=128)], label="Name", help_text="", required=True),
        StringField("short_name", validators=[length(min=1, max=64)], label="Short name", help_text="", required=True),
        DateTimeField("created_at", label="Creation date and time", exclude_from_create=True, exclude_from_edit=True),
        DateTimeField("modified", label="Date and time of modification", exclude_from_create=True, exclude_from_edit=True),
        BooleanField("has_file_manifest", label="Files added", getter=lambda request, obj: obj.file_manifest_id is not None, read_only=True, exclude_from_create=True, exclude_from_edit=True),
        StringField("file_manifest", label="Manifest ID", getter=_get_manifest_ident, copy_to_clipboard=True, exclude_from_create=True, exclude_from_edit=True, exclude_from_list=True),
        ComputedField("step_count", label="Number of downloads", getter=_get_NumDownloadsGame),
    ]
    menu_label = "Viewing games"
    inlines = [GameDisclaimerTextInline]

class GameClientsView(BaseModelView):
    perm_read: str = "admin.gameclient.read"
    perm_create: str = "admin.gameclient.create"
    perm_edit: str = "admin.gameclient.edit"
    perm_delete: str = "admin.gameclient.delete"

    fields = [
        "game",
        StringField("ident", validators=[length(min=1, max=64), slug(allow_underscores=True)], label="ID", help_text="", required=True, copy_to_clipboard=True),
        StringField("name", validators=[length(min=1, max=128)], label="Name", help_text="", required=True),
        StringField("short_name", validators=[length(min=1, max=64)], label="Short name", help_text="", required=True),
        DateTimeField("created_at", label="Creation date and time", exclude_from_create=True, exclude_from_edit=True),
        DateTimeField("modified", label="Date and time of modification", exclude_from_create=True, exclude_from_edit=True),
        BooleanField("reference", label="Reference", exclude_from_create=True, exclude_from_edit=True),
        BooleanField("has_file_manifest", label="Files added", getter=lambda request, obj: obj.file_manifest_id is not None, read_only=True, exclude_from_create=True, exclude_from_edit=True),
        StringField("file_manifest", label="Manifest ID", getter=_get_manifest_ident, copy_to_clipboard=True, exclude_from_create=True, exclude_from_edit=True, exclude_from_list=True),
        IPAddressField("master_server_address", validators=[length(min=0, max=15)], label="Master server address", help_text=""),
        StringField("master_server_enctypex_key", validators=[length(min=0, max=64)], label="Master server key", help_text=""),
    ]
    menu_label = "Viewing game clients"
    row_actions = [
        "view",
        "edit",
        "set_reference",
        "delete",
    ]

    @row_action(
        name="set_reference",
        text="Set as reference",
        icon_class="fas fa-star",
        confirmation="Set this client as the reference client for its game?",
        action_btn_class="btn btn-primary",
        submit_btn_text="Set as reference",
    )
    async def set_reference_row_action(self, request: Request, pk: Any) -> None:
        client = await GameClientModel.get_or_none(id=pk)
        if not client: return

        for c in await GameClientModel.filter(game__id=client.game_id).all():
            if c.reference:
                c.reference = False
                await c.save()
                break

        client.reference = True
        await client.save()

class GameModsView(BaseModelView):
    perm_read: str = "admin.gamemod.read"
    perm_create: str = "admin.gamemod.create"
    perm_edit: str = "admin.gamemod.edit"
    perm_delete: str = "admin.gamemod.delete"

    fields = [
        "game",
        StringField("ident", validators=[length(min=1, max=64), slug(allow_underscores=True)], label="ID", help_text="", required=True, copy_to_clipboard=True),
        StringField("name", validators=[length(min=1, max=128)], label="Name", help_text="", required=True),
        StringField("version", validators=[length(min=1, max=16)], label="Version", help_text="", required=True),
        StringField("developer", validators=[length(min=1, max=128)], label="Developer", help_text="", required=True),
        BooleanField("default", label="Default", exclude_from_create=True, exclude_from_edit=True),
        DateTimeField("created_at", label="Creation date and time", exclude_from_create=True, exclude_from_edit=True),
        DateTimeField("modified", label="Date and time of modification", exclude_from_create=True, exclude_from_edit=True),
        BooleanField("has_file_manifest", label="Files added", getter=lambda request, obj: obj.file_manifest_id is not None, read_only=True, exclude_from_create=True, exclude_from_edit=True),
        StringField("file_manifest", label="Manifest ID", getter=_get_manifest_ident, copy_to_clipboard=True, exclude_from_create=True, exclude_from_edit=True, exclude_from_list=True),
    ]
    menu_label = "Mods"
    row_actions = [
        "view",
        "edit",
        "set_default",
        "delete",
    ]

    @row_action(
        name="set_default",
        text="Set as default",
        icon_class="fas fa-star",
        confirmation="Set this mod as the default mod for its game?",
        action_btn_class="btn btn-primary",
        submit_btn_text="Set as default",
    )
    async def set_default_row_action(self, request: Request, pk: Any) -> None:
        mod = await GameModModel.get_or_none(id=pk)
        if not mod: return

        for c in await GameModModel.filter(game__id=mod.game_id).all():
            if c.default:
                c.default = False
                await c.save()
                break

        mod.default = True
        await mod.save()

class GameServerView(BaseModelView):
    perm_read: str = "admin.gameserver.read"
    perm_create: str = "admin.gameserver.create"
    perm_edit: str = "admin.gameserver.edit"
    perm_delete: str = "admin.gameserver.delete"

    fields = [
        "client",
        StringField("ident", validators=[length(min=1, max=64), slug(allow_underscores=True)], label="ID", help_text="", required=True, copy_to_clipboard=True),
        StringField("name", validators=[length(min=1, max=128)], label="Name", help_text="", required=True),
        StringField("short_name", validators=[length(min=1, max=64)], label="Short name", help_text="", required=True),
        DateTimeField("created_at", label="Creation date and time", exclude_from_create=True, exclude_from_edit=True),
        DateTimeField("modified", label="Date and time of modification", exclude_from_create=True, exclude_from_edit=True),
        IPAddressField("address", validators=[length(min=1, max=15)], label="IP address", help_text="", required=True),
        IntegerField("query_port", validators=[number_range(min=1, max=65535)], label="Query port", help_text="", required=True),
    ]
    menu_label = "Clients"

class AccountView(BaseModelView):
    perm_read: str = "admin.account.read"
    perm_create: str = "admin.account.create"
    perm_edit: str = "admin.account.edit"
    perm_delete: str = "admin.account.delete"

    fields = [
        StringField("ident", label="ID", help_text="", required=True, exclude_from_list=True, exclude_from_create=True, exclude_from_edit=True, copy_to_clipboard=True),
        StringField("name", validators=[length(min=1, max=128)], label="Name", help_text="", required=True),
        PasswordField("before_password", label="Password", required=False, exclude_from_list=True, exclude_from_detail=True),
        StringField("steam_id", validators=[length(min=0, max=128)], label="Steam ID", help_text="", required=False),
        DateTimeField("created_at", label="Creation date and time", exclude_from_create=True, exclude_from_edit=True),
        DateTimeField("modified", label="Date and time of modification", exclude_from_create=True, exclude_from_edit=True),
        BooleanField("active", label="Active"),
        BooleanField("op", label="Operator"),
    ]
    menu_label = "Accounts"

    async def before_create(self, request: Request, data: dict[str, Any], obj: AccountModel):
        password = data["before_password"]
        obj.password = await AccountService.hash_password(password)

    async def before_edit(self, request: Request, data: dict[str, Any], obj: AccountModel):
        password = data["before_password"]
        obj.password = await AccountService.hash_password(password)

class SessionView(BaseModelView):
    perm_read: str = "admin.session.read"
    perm_create: str = "admin.session.create"
    perm_edit: str = "admin.session.edit"
    perm_delete: str = "admin.session.delete"

    fields = [
        "account",
        DateTimeField("created_at", label="Creation date and time", exclude_from_create=True, exclude_from_edit=True),
        DateTimeField("last_access", label="Last activity", exclude_from_create=True, exclude_from_edit=True),
        IPAddressField("address", validators=[length(min=1, max=15)], label="IP address", help_text="", required=True),
        StringField("agent", validators=[length(min=1, max=128)], label="Agent", help_text="", required=True),
    ]
    menu_label = "Sessions"

class FileInline(TortoiseInlineModelView):
    model = FileModel
    fk_attr = "manifest"

    fields = [
        "ident",
        "filename",
        "size",
        "storage",
        "path",
        "checksum_sha256",
        "created_at",
    ]

class FileManifestView(BaseModelView):
    perm_read: str = "admin.filemanifest.read"
    perm_create: str = "admin.filemanifest.create"
    perm_edit: str = "admin.filemanifest.edit"
    perm_delete: str = "admin.filemanifest.delete"

    fields = [
        StringField("ident", validators=[length(min=1, max=32)], label="ID", help_text="", required=True, copy_to_clipboard=True),
        DateTimeField("created_at", label="Creation date and time", exclude_from_create=True, exclude_from_edit=True),
    ]
    inlines = [FileInline]
    menu_label = "File manifests"

    @row_action(
        name="remove_files",
        text="Remove files",
        icon_class="fas fa-star",
        confirmation="Delete ALL FILES of this manifest? Cancelling the action and recovery are impossible",
        action_btn_class="btn btn-primary",
        submit_btn_text="Remove files",
        custom_response=True
    )
    async def remove_files_row_action(self, request: Request, pk: Any):
        manifest = await FileManifestModel.get(id=pk)
        await self.core.storage.remove_files(manifest)
        await manifest.delete()
        return RedirectResponse("/admin/file-manifest-model/list", status_code=303)

class FileView(BaseModelView):
    perm_read: str = "admin.file.read"
    perm_create: str = "admin.file.create"
    perm_edit: str = "admin.file.edit"
    perm_delete: str = "admin.file.delete"

    fields = [
        StringField("ident", validators=[length(min=1, max=32)], label="ID", help_text="", required=True, copy_to_clipboard=True),
        StringField("filename", validators=[length(min=1, max=128)], label="Name", help_text="", required=True),
        StringField("local_path", validators=[length(min=1, max=255)], label="Local path", help_text="", required=True),
        StringField("checksum_sha256", validators=[length(min=1, max=255)], label="Checksum sha256", help_text="", required=True, read_only=True),
        "manifest",
        StringField("storage", validators=[length(min=1, max=64)], label="Storage", help_text="", required=True, read_only=True),
        StringField("path", validators=[length(min=1, max=64)], label="Path", help_text="", required=True, read_only=True),
        IntegerField("size", required=True, read_only=True),
        DateTimeField("created_at", label="Creation date and time", exclude_from_create=True, exclude_from_edit=True),
    ]
    menu_label = "Files"

class UploadTooLarge(Exception):
    pass

UPLOAD_CHUNK_SIZE_BYTES = 8 * 1024 * 1024
MAX_UPLOAD_SIZE_BYTES = int(os.getenv("MAX_UPLOAD_SIZE_BYTES", str(4 * 1024**3)))
UPLOAD_SESSION_MAX_AGE_SECONDS = 7 * 24 * 60 * 60
UPLOAD_ID_PATTERN = re.compile(r"^[0-9a-f]{32}$")
_UPLOAD_START_LOCK = asyncio.Lock()

class BaseFileUploadView(BaseCustomView):

    target: Model
    form_title: str
    select_desc: str

    def __init__(self, core, *args, **kwargs):
        super().__init__(core, *args, **kwargs)
        self._upload_locks: dict[str, asyncio.Lock] = {}

    def _upload_directory(self) -> Path:
        # Match Storage.add_files(), which resolves filenames relative to TEMP_STORAGE_PATH.
        directory = Path(os.getenv("TEMP_STORAGE_PATH") or ".")
        directory.mkdir(parents=True, exist_ok=True)
        return directory

    def _upload_paths(self, upload_id: str) -> tuple[Path, Path, Path]:
        if not UPLOAD_ID_PATTERN.fullmatch(upload_id):
            raise ValueError("Invalid upload ID")
        base = self._upload_directory() / f"admin-upload-{upload_id}"
        return base.with_suffix(".part"), base.with_suffix(".json"), base.with_suffix(".zip")

    def _upload_lock(self, upload_id: str) -> asyncio.Lock:
        lock = self._upload_locks.get(upload_id)
        if lock is None:
            lock = asyncio.Lock()
            self._upload_locks[upload_id] = lock
        return lock

    @staticmethod
    def _read_upload_metadata(path: Path) -> dict | None:
        try:
            with path.open("r", encoding="utf-8") as source:
                metadata = json.load(source)
            return metadata if isinstance(metadata, dict) else None
        except (OSError, ValueError, TypeError):
            return None

    @staticmethod
    def _write_upload_metadata(path: Path, metadata: dict) -> None:
        temporary = path.with_suffix(".json.tmp")
        with temporary.open("w", encoding="utf-8") as output:
            json.dump(metadata, output)
            output.flush()
            os.fsync(output.fileno())
        os.replace(temporary, path)

    def _cleanup_stale_uploads(self) -> None:
        cutoff = time.time() - UPLOAD_SESSION_MAX_AGE_SECONDS
        for metadata_path in self._upload_directory().glob("admin-upload-*.json"):
            try:
                if metadata_path.stat().st_mtime >= cutoff:
                    continue
                upload_id = metadata_path.stem.removeprefix("admin-upload-")
                if not UPLOAD_ID_PATTERN.fullmatch(upload_id):
                    continue
                part_path, _, archive_path = self._upload_paths(upload_id)
                part_path.unlink(missing_ok=True)
                archive_path.unlink(missing_ok=True)
                metadata_path.unlink(missing_ok=True)
            except OSError:
                continue

    @route("")
    async def index(self, request: Request):
        base_path = request.url.path.rstrip("/")
        targets = await self.target.all()
        model_view = next((view for view in self._admin._model_views if getattr(view, "model", None) is self.target), None)
        if model_view is None:
            raise RuntimeError(f"No ModelView is registered for the model {self.target.__name__}")

        choices = []
        for target in targets:
            pk = await model_view.get_pk_value(request, target)
            label = await model_view.repr(target, request)
            choices.append({"id": pk, "label": label})

        return self.templates.TemplateResponse(
            request=request,
            name="file_upload.html",
            context={
                "title": self.title(request),
                "upload_url": f"{base_path}/upload",
                "targets": choices,
                "select_desc": self.select_desc,
                "form_title": self.form_title,
            },
        )

    @route("/upload/start", methods=["POST"])
    async def upload_start(self, request: Request):
        if not self.can_create(request):
            return JSONResponse({"error": "Permission denied"}, status_code=403)

        form = await request.form(max_files=0, max_fields=10)
        target_id = str(form.get("target_id") or "")
        upload_id = str(form.get("upload_id") or "")
        filename = str(form.get("filename") or "").replace("\\", "/").rsplit("/", 1)[-1]
        try:
            total_size = int(form.get("size", 0))
        except (TypeError, ValueError):
            return JSONResponse({"error": "Invalid file size"}, status_code=400)

        if not UPLOAD_ID_PATTERN.fullmatch(upload_id):
            return JSONResponse({"error": "Invalid upload ID"}, status_code=400)
        if not filename.lower().endswith(".zip") or total_size <= 0:
            return JSONResponse({"error": "Select a non-empty ZIP archive"}, status_code=400)
        if total_size > MAX_UPLOAD_SIZE_BYTES:
            return JSONResponse({"error": f"File is too large; limit is {MAX_UPLOAD_SIZE_BYTES} bytes"}, status_code=413)
        target = await self.target.get_or_none(id=target_id)
        if not target:
            return JSONResponse({"error": "Record not found"}, status_code=400)

        async with _UPLOAD_START_LOCK:
            self._cleanup_stale_uploads()
            part_path, metadata_path, archive_path = self._upload_paths(upload_id)
            async with self._upload_lock(upload_id):
                metadata = self._read_upload_metadata(metadata_path)
                if metadata:
                    same_upload = (
                        str(metadata.get("target_id")) == target_id
                        and metadata.get("target_model") == self.target.__name__
                        and metadata.get("filename") == filename
                        and int(metadata.get("total_size", -1)) == total_size
                    )
                    if not same_upload:
                        return JSONResponse({"error": "Upload ID belongs to a different file"}, status_code=409)
                    if metadata.get("state") == "complete" and metadata.get("manifest_id"):
                        manifest_id = str(metadata["manifest_id"])
                        return JSONResponse({
                            "ok": True,
                            "completed": True,
                            "upload_id": upload_id,
                            "redirect_url": f"/admin{self.path}/task?manifestid={manifest_id}",
                        })
                    if metadata.get("state") == "processing":
                        return JSONResponse({"error": "File is being prepared by storage; retry shortly"}, status_code=503)
                    if metadata.get("state") != "uploading" or not part_path.is_file():
                        return JSONResponse({"error": "Upload session is invalid or expired"}, status_code=409)

                    # Reconcile the sidecar metadata with disk after a client/process disconnect.
                    received = min(int(metadata.get("received", 0)), part_path.stat().st_size)
                    if received != total_size:
                        received = (received // UPLOAD_CHUNK_SIZE_BYTES) * UPLOAD_CHUNK_SIZE_BYTES
                    with part_path.open("r+b") as partial:
                        partial.truncate(received)
                    metadata["received"] = received
                    metadata["next_chunk"] = (received + UPLOAD_CHUNK_SIZE_BYTES - 1) // UPLOAD_CHUNK_SIZE_BYTES
                    metadata["updated_at"] = time.time()
                    self._write_upload_metadata(metadata_path, metadata)
                    return JSONResponse({
                        "ok": True,
                        "upload_id": upload_id,
                        "chunk_size": UPLOAD_CHUNK_SIZE_BYTES,
                        "total_size": total_size,
                        "received": received,
                        "next_chunk": metadata["next_chunk"],
                        "resumed": True,
                    })

                part_path.unlink(missing_ok=True)
                archive_path.unlink(missing_ok=True)
                try:
                    with part_path.open("xb"):
                        pass
                    now = time.time()
                    metadata = {
                        "upload_id": upload_id,
                        "target_id": target_id,
                        "target_model": self.target.__name__,
                        "filename": filename,
                        "total_size": total_size,
                        "received": 0,
                        "next_chunk": 0,
                        "state": "uploading",
                        "created_at": now,
                        "updated_at": now,
                    }
                    self._write_upload_metadata(metadata_path, metadata)
                except OSError as exc:
                    part_path.unlink(missing_ok=True)
                    return JSONResponse({"error": f"Could not create temporary upload file: {exc}"}, status_code=507)

        return JSONResponse({
            "ok": True,
            "upload_id": upload_id,
            "chunk_size": UPLOAD_CHUNK_SIZE_BYTES,
            "total_size": total_size,
            "received": 0,
            "next_chunk": 0,
            "resumed": False,
        })

    @route("/upload/chunk", methods=["POST"])
    async def upload_chunk(self, request: Request):
        if not self.can_create(request):
            return JSONResponse({"error": "Permission denied"}, status_code=403)

        form = await request.form(max_files=1, max_fields=10)
        upload_id = str(form.get("upload_id") or "")
        file = form.get("file")
        try:
            chunk_index = int(form.get("chunk_index", -1))
            part_path, metadata_path, _ = self._upload_paths(upload_id)
        except (TypeError, ValueError):
            if isinstance(file, UploadFile):
                await file.close()
            return JSONResponse({"error": "Invalid upload ID or chunk index"}, status_code=400)

        if not isinstance(file, UploadFile) or chunk_index < 0:
            if isinstance(file, UploadFile):
                await file.close()
            return JSONResponse({"error": "Chunk data is missing or invalid"}, status_code=400)

        try:
            async with self._upload_lock(upload_id):
                metadata = self._read_upload_metadata(metadata_path)
                if not metadata or not part_path.is_file():
                    return JSONResponse({"error": "Upload session not found or expired"}, status_code=404)
                if metadata.get("state") != "uploading":
                    return JSONResponse({"error": "Upload session is no longer accepting chunks"}, status_code=409)

                expected_index = int(metadata["next_chunk"])
                current_received = int(metadata["received"])
                total_size = int(metadata["total_size"])
                if chunk_index < expected_index:
                    # The previous response may have been lost; acknowledge the saved chunk.
                    return JSONResponse({"ok": True, "received": current_received, "duplicate": True})
                if chunk_index != expected_index:
                    return JSONResponse({"error": "Chunks must be sent in order", "expected_chunk": expected_index}, status_code=409)

                expected_offset = chunk_index * UPLOAD_CHUNK_SIZE_BYTES
                expected_length = min(UPLOAD_CHUNK_SIZE_BYTES, total_size - expected_offset)
                if expected_offset != current_received or expected_length <= 0:
                    return JSONResponse({"error": "Unexpected chunk offset"}, status_code=409)
                if file.size is not None and file.size != expected_length:
                    return JSONResponse({"error": f"Invalid chunk size; expected {expected_length} bytes"}, status_code=400)

                written = 0
                try:
                    async with await anyio.open_file(part_path, "r+b") as output:
                        await output.seek(expected_offset)
                        while True:
                            chunk = await file.read(1024 * 1024)
                            if not chunk:
                                break
                            written += len(chunk)
                            if written > expected_length:
                                raise ValueError("Chunk is larger than expected")
                            await output.write(chunk)
                        if written != expected_length:
                            raise ValueError(f"Incomplete chunk: {written} of {expected_length} bytes")
                        await output.flush()
                except Exception as exc:
                    try:
                        async with await anyio.open_file(part_path, "r+b") as output:
                            await output.truncate(expected_offset)
                    except OSError:
                        pass
                    return JSONResponse({"error": str(exc)}, status_code=400 if isinstance(exc, ValueError) else 500)

                metadata["received"] = expected_offset + written
                metadata["next_chunk"] = expected_index + 1
                metadata["updated_at"] = time.time()
                self._write_upload_metadata(metadata_path, metadata)
                return JSONResponse({"ok": True, "received": metadata["received"], "total_size": total_size})
        finally:
            await file.close()

    @route("/upload/complete", methods=["POST"])
    async def upload_complete(self, request: Request):
        if not self.can_create(request):
            return JSONResponse({"error": "Permission denied"}, status_code=403)
        form = await request.form(max_files=0, max_fields=10)
        upload_id = str(form.get("upload_id") or "")
        try:
            part_path, metadata_path, archive_path = self._upload_paths(upload_id)
        except ValueError:
            return JSONResponse({"error": "Invalid upload ID"}, status_code=400)

        async with self._upload_lock(upload_id):
            metadata = self._read_upload_metadata(metadata_path)
            if not metadata:
                return JSONResponse({"error": "Upload session not found or expired"}, status_code=404)
            if metadata.get("state") == "complete" and metadata.get("manifest_id"):
                manifest_id = str(metadata["manifest_id"])
                return JSONResponse({
                    "ok": True,
                    "message": f"File successfully uploaded: MF:{manifest_id}",
                    "filename": metadata.get("filename", ""),
                    "size": int(metadata.get("total_size", 0)),
                    "redirect_url": f"/admin{self.path}/task?manifestid={manifest_id}",
                })
            if metadata.get("state") == "processing":
                return JSONResponse({"error": "File is being prepared by storage; retry shortly"}, status_code=503)
            if metadata.get("state") != "uploading":
                return JSONResponse({"error": "Upload is already being finalized"}, status_code=409)

            total_size = int(metadata.get("total_size", 0))
            received = int(metadata.get("received", 0))
            if received != total_size or not part_path.is_file() or part_path.stat().st_size != total_size:
                return JSONResponse({"error": "Upload is incomplete"}, status_code=409)
            target = await self.target.get_or_none(id=metadata.get("target_id"))
            if not target:
                return JSONResponse({"error": "Record not found"}, status_code=400)

            try:
                part_path.replace(archive_path)
                metadata["state"] = "processing"
                metadata["updated_at"] = time.time()
                self._write_upload_metadata(metadata_path, metadata)
                # Storage remains responsible for extracting the archive and persisting files.
                manifest_id = await self.core.storage.add_files(archive_path.name, target=target)
            except Exception as exc:
                if archive_path.exists() and not part_path.exists():
                    archive_path.replace(part_path)
                metadata["state"] = "uploading"
                metadata["updated_at"] = time.time()
                self._write_upload_metadata(metadata_path, metadata)
                return JSONResponse({"error": f"Could not pass the archive to storage: {exc}"}, status_code=500)

            metadata["state"] = "complete"
            metadata["manifest_id"] = str(manifest_id)
            metadata["updated_at"] = time.time()
            self._write_upload_metadata(metadata_path, metadata)

        return JSONResponse({
            "ok": True,
            "message": f"File successfully uploaded: MF:{manifest_id}",
            "filename": metadata.get("filename", ""),
            "size": total_size,
            "redirect_url": f"/admin{self.path}/task?manifestid={manifest_id}",
        })

    @route("/task")
    async def task(self, request: Request):
        manifest_id = request.query_params.get("manifestid")
        if not manifest_id or len(manifest_id) != 32:
            raise HTTPException(status_code=400, detail="Missing or invalid manifestid")
        return self.templates.TemplateResponse(
            request=request,
            name="manifest_task_status.html",
            context={
                "title": self.title(request),
                "manifest_id": manifest_id,
                "status_api_url": "/api/storage/tasks",
            },
        )

class GameFileUploadView(BaseFileUploadView):

    target = GameModel
    select_desc = "Select the desired game"
    form_title = "Uploading game files"
    menu_label = "Upload game files"
    icon = "fa fa-upload"
    path = "/games/file"

    perm_read: str = "admin.game.fileupload.read"
    perm_create: str = "admin.game.fileupload.create"
    perm_edit: str = "admin.game.fileupload.edit"
    perm_delete: str = "admin.game.fileupload.delete"

class GameClientFileUploadView(BaseFileUploadView):

    target = GameClientModel
    select_desc = "Select the desired game client"
    form_title = "Uploading game client files"
    menu_label = "Upload game client files"
    icon = "fa fa-upload"
    path = "/games/clients/file"

    perm_read: str = "admin.gameclient.fileupload.read"
    perm_create: str = "admin.gameclient.fileupload.create"
    perm_edit: str = "admin.gameclient.fileupload.edit"
    perm_delete: str = "admin.gameclient.fileupload.delete"