import os

from starlette_admin.contrib.tortoise import (InlineModelView as TortoiseInlineModelView, ModelView)
from starlette_admin import CustomView, route
from starlette_admin.fields import *
from starlette_admin.validators import slug, length, number_range
from starlette_admin.actions import row_action
from starlette.responses import RedirectResponse
from starlette.exceptions import HTTPException
from starlette_admin import TextWidget, HtmlWidget
from .models import GameClientModel, GameModModel, SessionAccountModel, AccountModel, FileModel
import httpx
from urllib.parse import quote
from starlette.datastructures import UploadFile
from starlette.requests import Request
from starlette.responses import JSONResponse, Response
from pathlib import Path
import anyio
from uuid import uuid4
from urllib.parse import urlencode

class BaseCustomView(CustomView):

    perm_read: str
    perm_create: str
    perm_edit: str
    perm_delete: str

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
        DateTimeField("modified", label="Date and time of modification", exclude_from_create=True, exclude_from_edit=True)
    ]
    menu_label = "Games"

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
        IPAddressField("master_server_address", validators=[length(min=0, max=15)], label="Master server address", help_text=""),
        StringField("master_server_enctypex_key", validators=[length(min=0, max=64)], label="Master server key", help_text=""),
    ]
    menu_label = "Game clients"
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
        DateTimeField("modified", label="Date and time of modification", exclude_from_create=True, exclude_from_edit=True)
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
        StringField("ident", validators=[length(min=1, max=32)], label="ID", help_text="", required=True, copy_to_clipboard=True),
        StringField("name", validators=[length(min=1, max=128)], label="Name", help_text="", required=True),
        StringField("steam_id", validators=[length(min=0, max=128)], label="Steam ID", help_text="", required=True),
        DateTimeField("created_at", label="Creation date and time", exclude_from_create=True, exclude_from_edit=True),
        DateTimeField("modified", label="Date and time of modification", exclude_from_create=True, exclude_from_edit=True),
        BooleanField("active", label="Active"),
        BooleanField("op", label="Operator"),
    ]
    menu_label = "Accounts"

class SessionView(BaseModelView):
    perm_read: str = "admin.session.read"
    perm_create: str = "admin.session.create"
    perm_edit: str = "admin.session.edit"
    perm_delete: str = "admin.session.delete"

    fields = [
        "account",
        DateTimeField("created_at", label="Creation date and time", exclude_from_create=True, exclude_from_edit=True),
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

class FileUploadView(BaseCustomView):
    menu_label = "Upload files"
    icon = "fa fa-upload"
    path = "/file"

    perm_read: str = "admin.fileupload.read"
    perm_create: str = "admin.fileupload.create"
    perm_edit: str = "admin.fileupload.edit"
    perm_delete: str = "admin.fileupload.delete"

    @route("")
    async def index(self, request: Request):
        base_path = request.url.path.rstrip("/")

        return self.templates.TemplateResponse(
            request=request,
            name="file_upload.html",
            context={
                "title": self.title(request),
                "upload_url": f"{base_path}/upload",
            },
        )

    @route("/upload", methods=["POST"])
    async def upload(self, request: Request):
        form = await request.form()
        file = form.get("file")

        if not isinstance(file, UploadFile): return JSONResponse({"error": "Файл не передан"},status_code=400)

        CHUNK_SIZE = 1024 * 1024 # 1 MB
        UPLOAD_DIR = Path(os.getenv("TEMP_STORAGE_PATH"))
        UPLOAD_DIR.mkdir(parents=True, exist_ok=True)

        destination = UPLOAD_DIR / uuid4().hex
        size = 0

        try:
            async with await anyio.open_file(destination, "wb") as output:
                while True:
                    chunk = await file.read(CHUNK_SIZE)

                    if not chunk:
                        break

                    size += len(chunk)

                    await output.write(chunk)

        except Exception as e:
            destination.unlink(missing_ok=True)
            return JSONResponse(
                {"error": str(e)},
                status_code=413,
            )

        finally:
            await file.close()

        manifest_id = await self.core.storage.add_files(destination.name)

        return JSONResponse({
            "ok": True,
            "message": f"Файл успешно загружен: MF:{manifest_id}",
            "filename": file.filename,
            "size": size,
            "redirect_url": f"/admin/file/task?manifestid={manifest_id}"
        })

    @route("/task")
    async def task(self, request: Request):
        manifest_id = request.query_params.get("manifestid")

        if not manifest_id or len(manifest_id) != 32:
            raise HTTPException(
                status_code=400,
                detail="Missing or invalid manifestid",
            )

        return self.templates.TemplateResponse(
            request=request,
            name="manifest_task_status.html",
            context={
                "title": self.title(request),
                "manifest_id": manifest_id,
                "status_api_url": "/api/storage/tasks",
            },
        )