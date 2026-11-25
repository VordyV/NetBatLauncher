from starlette_admin.fields import *
from starlette_admin import action, ActionSelection, row_action
from tessellock.services import AccountService
from datetime import datetime
from .ap_view import APView


async def _getter_session_expiration(request, obj):
	state = request.app.state.parent_state
	core = state.tessellock
	return obj.last_access + core.session_expiration

async def _getter_maximum_session_lifetime(request, obj):
	state = request.app.state.parent_state
	core = state.tessellock
	return obj.dt_created + core.maximum_session_lifetime


class AccountSessionAPView(APView):

	permission_read: str = "accounts.sessions.read"
	permission_delete: str = "accounts.sessions.delete"
	action_permissions: dict[str, str] = {}

	menu_label = "Sessions"
	fields_default_sort = [("dt_created", True)]

	fields = [
		UUIDField("uuid", label="ID"),
		HasOne("account", label="Account", key="account-model"),
		DateTimeField("dt_created", label="Creation time", read_only=True, exclude_from_edit=True, exclude_from_create=True),
		DateTimeField("last_access", label="Update time", read_only=True, exclude_from_edit=True, exclude_from_create=True, exclude_from_list=True),
		DateTimeField("session_expiration", getter=_getter_session_expiration, label="Session expiration", read_only=True, exclude_from_edit=True, exclude_from_create=True),
		DateTimeField("maximum_session_lifetime", getter=_getter_maximum_session_lifetime, label="Maximum session lifetime", read_only=True, exclude_from_edit=True, exclude_from_create=True),
		IPAddressField("ip_address", label="IP address", copy_to_clipboard=True, exclude_from_list=True),
		StringField("agent", label="Agent", read_only=True, exclude_from_edit=True, exclude_from_create=True),
	]

	def can_edit(self, request: Request) -> bool:
		return False

	def can_create(self, request: Request) -> bool:
		return False