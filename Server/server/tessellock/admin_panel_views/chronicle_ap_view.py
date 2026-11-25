from starlette_admin.fields import *
from starlette_admin import action, ActionSelection, row_action
from tessellock.services import AccountService
from tessellock.models import PermissionGroupModel, GroupPermissionsModel
from starlette_admin.exceptions import FormValidationError
from datetime import datetime
from .ap_view import APView, InlineModelView


_types = {
	1: "Registered",
	2: "Created a new session",
	3: "Deleted session",
	4: "Updated account data",
	5: "Deleted account",
	6: "Changed account password",
	7: "Added account to a group",
	8: "Removed account from group",
	9: "Added a permission to account",
	10: "Removed a permission from account",
	11: "Made account an operator",
	12: "Revoked operator status from account",
	13: "Enabled account",
	14: "Disabled account",
	15: "Created a group",
	16: "Updated group data",
	17: "Deleted group",
	18: "Added a permission to group",
	19: "Removed a permission from group",
	20: "Enabled group",
	21: "Disabled group",
}

async def _getter_type(request, obj):
	return _types.get(obj.type, str(obj.type))

class ChronicleAPView(APView):

	permission_read: str = "chronicle.read"
	action_permissions: dict[str, str] = {}

	menu_label = "Chronicle"
	fields_default_sort = [("dt_created", True)]

	fields = [
		StringField("uuid", label="ID", copy_to_clipboard=True),
		StringField("type", getter=_getter_type, label="Type"),
		DateTimeField("dt_created", label="Creation time"),
		JSONField("args", label="Args", exclude_from_list=True),
	]

	def can_edit(self, request: Request) -> bool:
		return False

	def can_create(self, request: Request) -> bool:
		return False

	def can_delete(self, request: Request) -> bool:
		return False