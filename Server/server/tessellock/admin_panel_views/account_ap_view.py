from starlette_admin.fields import *
from starlette_admin import action, ActionSelection, row_action
from starlette_admin.exceptions import FormValidationError
from tessellock.services import AccountService
from tessellock.models import AccountPermissionsModel, AccountModel, AccountGroupsModel, AccountSessionModel
from datetime import datetime
from .ap_view import APView, InlineModelView


class PermissionsInline(InlineModelView):
	model = AccountPermissionsModel
	fk_attr = "account_id"
	fields = ["permission", DateTimeField("dt_expiration", label="Expiration")]
	extra = 1
	allow_delete = True
	menu_label = "Permissions"

	async def check_duplicate(self, obj):
		query = AccountPermissionsModel.filter(account_id=obj.account_id, permission_id=obj.permission_id)
		if obj.pk is not None: query = query.exclude(id=obj.pk)
		if await query.exists(): raise FormValidationError({"permission": "This permission has already been added to the account"})

	async def before_create(self, request: Request, data: dict, obj):
		await self.check_duplicate(obj)

	async def before_edit(self, request: Request, data: dict, obj):
		await self.check_duplicate(obj)

class GroupsInline(InlineModelView):
	model = AccountGroupsModel
	fk_attr = "account_id"
	fields = ["group", DateTimeField("dt_expiration", label="Expiration")]
	extra = 1
	allow_delete = True
	menu_label = "Groups"

	async def check_duplicate(self, data, obj):
		group = data.get("group")
		if group is None: return
		query = self.model.filter(account_id=obj.account_id, group_id=group.id)
		if obj.id is not None: query = query.exclude(id=obj.id)
		if await query.exists(): raise FormValidationError({"group": "This group has already been added to the account"})

	async def before_create(self, request: Request, data: dict, obj):
		await self.check_duplicate(data, obj)

	async def before_edit(self, request: Request, data: dict, obj):
		await self.check_duplicate(data, obj)

async def _getter_count_sessions(request, obj):
	return await AccountSessionModel.filter(account_id=obj.id).count()

class AccountAPView(APView):

	permission_read: str = "accounts.read"
	permission_create: str = "accounts.create"
	permission_edit: str = "accounts.update"
	permission_delete: str = "accounts.delete"
	action_permissions: dict[str, str] = {
		"make_operator": "accounts.operator.make",
		"remove_operator": "accounts.operator.delete",
		"activate": "accounts.active.enable",
		"deactivate": "accounts.active.disable"
	}

	menu_label = "List accounts"
	fields_default_sort = [("dt_created", True)]

	fields = [
		UUIDField("uuid", label="ID", exclude_from_edit=True, exclude_from_create=True),
		StringField("name", label="Name", required=True, copy_to_clipboard=True),
		StringField("email", label="Email", copy_to_clipboard=True, exclude_from_list=True),
		DateTimeField("dt_created", label="Creation time", read_only=True, exclude_from_edit=True, exclude_from_create=True),
		DateTimeField("dt_modified", label="Update time", read_only=True, exclude_from_edit=True, exclude_from_create=True, exclude_from_list=True),
		DateTimeField("dt_modified_pwdhash", label="Time of last password change", read_only=True, exclude_from_edit=True, exclude_from_create=True),
		PasswordField("before_password", label="Password", required=False, exclude_from_list=True, exclude_from_detail=True),
		BooleanField("is_op", label="Operator", read_only=True, exclude_from_edit=True, exclude_from_create=True),
		BooleanField("is_active", label="Active", read_only=True, exclude_from_edit=True),
		BooleanField("is_pwd", label="Password set", getter=lambda request, obj: bool(obj.pwdhash), read_only=True, exclude_from_edit=True, exclude_from_create=True, exclude_from_list=True),
		IntegerField("count_sessions", label="Number of sessions", getter=_getter_count_sessions, read_only=True, exclude_from_edit=True, exclude_from_create=True, exclude_from_list=True)
	]

	inlines = [PermissionsInline, GroupsInline]

	async def before_create(self, request: Request, data: dict[str, Any], obj: AccountModel):
		password = data.get("before_password")
		if password:
			obj.pwdhash = await AccountService.get_hash_password(password)

	async def before_edit(self, request: Request, data: dict[str, Any], obj: AccountModel):
		password = data.get("before_password")
		if password:
			obj.pwdhash = await AccountService.get_hash_password(password)
			obj.dt_modified_pwdhash = datetime.now()

	async def is_row_action_allowed_for_obj(self, request: Request, name: str, obj: AccountModel) -> bool:
		is_allowed = await super().is_row_action_allowed_for_obj(request, name, obj)
		if "make_operator" == name and is_allowed:
			return not bool(obj.is_op)
		if "remove_operator" == name and is_allowed:
			return bool(obj.is_op)
		if "activate" == name and is_allowed:
			return not bool(obj.is_active)
		if "deactivate" == name and is_allowed:
			return bool(obj.is_active)
		return is_allowed

	@row_action(name="make_operator", text="Make an operator", confirmation="Grant operator rights to the entire system. This means the account will receive full access to everything. Are you absolutely sure about this action?")
	async def btn_set_operator(self, request: Request, pk: Any):
		await self.core.account_service.make_operator(account_id=int(pk), initiator=request.state.admin_user.account_uuid)

	@row_action(name="remove_operator", text="Remove operator", confirmation="The account will lose full access to all functions. Are you sure about this action?")
	async def btn_remove_operator(self, request: Request, pk: Any):
		await self.core.account_service.remove_operator(account_id=int(pk), initiator=request.state.admin_user.account_uuid)

	@row_action(name="activate", text="Activate")
	async def btn_activate(self, request: Request, pk: Any):
		await self.core.account_service.enable(account_id=int(pk), initiator=request.state.admin_user.account_uuid)

	@row_action(name="deactivate", text="Deactivate")
	async def btn_deactivate(self, request: Request, pk: Any):
		await self.core.account_service.disable(account_id=int(pk), initiator=request.state.admin_user.account_uuid)