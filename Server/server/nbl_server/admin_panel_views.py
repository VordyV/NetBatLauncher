from tessellock.admin_panel_views.ap_view import APView
from starlette_admin.fields import *
from starlette_admin import CustomView, StatWidget, RowWidget, HtmlWidget
from .models import FileManifestModel
from starlette_admin.storage import LocalStorage
from starlette.exceptions import HTTPException
from starlette_admin.flash import flash
from .storage_task_status import StorageTaskStatus
import os


_MAX_ARCHIVE_SIZE = 6 * 1024 ** 3

_temp_storage = LocalStorage(base_dir=os.getenv("STORAGE_DIR"), name="temp")

async def _get_manifest_ident(request, obj):
	manifest = await FileManifestModel.get_or_none(id=obj.file_manifest_id)
	return manifest.ident if manifest else ""

async def _build_st_page(self, request: Request):
	manifest_id = request.query_params.get("manifestid")

	core = request.app.state.core
	task = await core.storage_task_service.get(manifest_id, core.storage)
	if not manifest_id or not task: raise HTTPException(status_code=404)

	async def _getter_st_stat_widget_status(request: Request) -> int:
		return task.status

	return RowWidget(
        children=[
            StatWidget(
                title="Status",
                value_callback=_getter_st_stat_widget_status,
            )
        ],
	)

async def _getter_sts_status(request: Request, obj):
	return obj.status.name

class GameAPView(APView):

	permission_read: str = "accounts.read"
	permission_create: str = "accounts.create"
	permission_edit: str = "accounts.update"
	permission_delete: str = "accounts.delete"
	action_permissions: dict[str, str] = {}

	menu_label = "Games"

	fields = [
		StringField("ident", label="ID", required=True),
		StringField("name", label="Name", required=True, copy_to_clipboard=True),
		StringField("short_name", label="Short name", required=True, copy_to_clipboard=True),
		DateTimeField("created_at", label="Creation time", read_only=True, exclude_from_edit=True, exclude_from_create=True),
		DateTimeField("modified", label="Update time", read_only=True, exclude_from_edit=True, exclude_from_create=True, exclude_from_list=True),
		BooleanField("has_file_manifest", label="Files added", getter=lambda request, obj: obj.file_manifest_id is not None, read_only=True, exclude_from_create=True, exclude_from_edit=True),
		StringField("file_manifest", label="Manifest ID", getter=_get_manifest_ident, copy_to_clipboard=True, exclude_from_create=True, exclude_from_edit=True, exclude_from_list=True),
		FileField("file", label="Game files", storage=_temp_storage, upload_folder="temp", accept=".zip", max_size=_MAX_ARCHIVE_SIZE, exclude_from_list=True, exclude_from_detail=True),
	]

	async def before_create(self, request: Request, data: dict[str, Any], obj: Any) -> None:
		print(1)

	async def before_edit(self, request: Request, data: dict[str, Any], obj: Any) -> None:
		file = data.get("file")
		if not file: return

		path = _temp_storage.base_dir / 'temp' / file.get("filename")
		ident = await self.core.storage.add_files(path)
		flash(
			request,
			f"A storage task '{ident}' was created",
			category="info",
		)


class StorageTaskAPView(CustomView):

	menu_label = "Storage task"

	widget = _build_st_page
	path = "/storage/tasks"


class StorageTasksAPView(APView):

	permission_read: str = "accounts.read"
	permission_create: str = "accounts.create"
	permission_edit: str = "accounts.update"
	permission_delete: str = "accounts.delete"
	action_permissions: dict[str, str] = {}

	menu_label = "Storage tasks"

	fields = [
		StringField("ident", label="ID"),
		StringField("status", label="Status", getter=_getter_sts_status),
		StringField("error", label="Error", exclude_from_list=True)
	]

	def can_create(self, request: Request) -> bool:
		return False

	def can_delete(self, request: Request) -> bool:
		return False

	def can_edit(self, request: Request) -> bool:
		return False