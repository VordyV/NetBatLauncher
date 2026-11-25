from starlette_admin.fields import *
from starlette_admin import action, ActionSelection, row_action
from tessellock.services import AccountService
from tessellock.models import PermissionModel
from datetime import datetime
from .ap_view import APView


class PermissionAPView(APView):

	permission_read: str = "permissions.read"
	action_permissions: dict[str, str] = {}

	menu_label = "Permissions"
	fields_default_sort = [("dt_modified", True)]

	fields = [
		StringField("code", label="Code", copy_to_clipboard=True),
		StringField("name", label="Name"),
		StringField("description", label="Description", exclude_from_list=True),
		DateTimeField("dt_created", label="Creation time", read_only=True),
		DateTimeField("dt_modified", label="Update time", read_only=True, exclude_from_list=True),
	]

	def can_edit(self, request: Request) -> bool:
		return False

	def can_create(self, request: Request) -> bool:
		return False

	def can_delete(self, request: Request) -> bool:
		return False