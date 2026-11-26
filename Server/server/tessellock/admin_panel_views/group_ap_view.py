from starlette_admin.fields import *
from starlette_admin import action, ActionSelection, row_action
from tessellock.services import AccountService
from tessellock.models import PermissionGroupModel, GroupPermissionsModel
from starlette_admin.exceptions import FormValidationError
from datetime import datetime
from .ap_view import APView, InlineModelView


class PermissionsInline(InlineModelView):
	model = GroupPermissionsModel
	fk_attr = "group_id"
	fields = ["permission", DateTimeField("dt_expiration", label="Expiration")]
	extra = 1
	allow_delete = True
	menu_label = "Permissions"

	async def check_duplicate(self, obj, editing=False):
		query = GroupPermissionsModel.filter(group_id=obj.group_id, permission_id=obj.permission_id)
		if editing and obj.pk is not None: query = query.exclude(pk=obj.pk)
		if await query.exists(): raise FormValidationError({"permission": "This permission is already assigned to the group"})

	async def before_create(self, request: Request, data: dict, obj):
		await self.check_duplicate(obj)

	async def before_edit(self, request: Request, data: dict, obj):
		await self.check_duplicate(obj, editing=True)


class GroupAPView(APView):

	permission_read: str = "groups.read"
	permission_create: str = "groups.create"
	permission_edit: str = "groups.update"
	permission_delete: str = "groups.delete"
	action_permissions: dict[str, str] = {
		"activate": "groups.active.enable",
		"deactivate": "groups.active.disable"
	}

	menu_label = "Groups"
	fields_default_sort = [("dt_created", True)]

	fields = [
		StringField("uuid", label="ID", copy_to_clipboard=True, exclude_from_create=True, exclude_from_edit=True),
		StringField("name", label="Name", copy_to_clipboard=True),
		StringField("description", label="Description", exclude_from_list=True),
		DateTimeField("dt_created", label="Creation time", read_only=True, exclude_from_edit=True, exclude_from_create=True),
		DateTimeField("dt_modified", label="Update time", read_only=True, exclude_from_edit=True, exclude_from_create=True, exclude_from_list=True),
		BooleanField("is_active", label="Active", read_only=True, exclude_from_edit=True, default=True),
	]

	inlines = [PermissionsInline]

	async def is_row_action_allowed_for_obj(self, request: Request, name: str, obj: PermissionGroupModel) -> bool:
		is_allowed = await super().is_row_action_allowed_for_obj(request, name, obj)
		if "activate" == name and is_allowed:
			return not bool(obj.is_active)
		if "deactivate" == name and is_allowed:
			return bool(obj.is_active)
		return is_allowed

	@row_action(name="activate", text="Activate")
	async def btn_activate(self, request: Request, pk: Any):
		await self.core.group_service.enable(group_id=int(pk), initiator=request.state.admin_user.account_uuid)

	@row_action(name="deactivate", text="Deactivate")
	async def btn_deactivate(self, request: Request, pk: Any):
		await self.core.group_service.disable(group_id=int(pk), initiator=request.state.admin_user.account_uuid)