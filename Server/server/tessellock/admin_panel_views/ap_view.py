from starlette_admin.contrib.tortoise import ModelView, InlineModelView
from starlette.requests import Request


class APView(ModelView):

	permission_read: str
	permission_create: str
	permission_edit: str
	permission_delete: str
	action_permissions: dict[str, str]

	def __init__(self, core, *args, **kwargs):
		self.__core = core
		super().__init__(*args, **kwargs)

	@property
	def core(self): return self.__core

	def is_accessible(self, request: Request) -> bool:
		return request.state.admin_user.is_op or self.permission_read in request.state.admin_user.permissions

	def can_create(self, request: Request) -> bool:
		return request.state.admin_user.is_op or self.permission_create in request.state.admin_user.permissions

	def can_edit(self, request: Request) -> bool:
		return request.state.admin_user.is_op or self.permission_edit in request.state.admin_user.permissions

	def can_delete(self, request: Request) -> bool:
		return request.state.admin_user.is_op or self.permission_delete in request.state.admin_user.permissions

	async def is_action_allowed(self, request: Request, name: str) -> bool:
		action = self.action_permissions.get(name)
		if action:
			return request.state.admin_user.is_op or action in request.state.admin_user.permissions

		return await super().is_action_allowed(request, name)

	async def is_row_action_allowed_for_obj(self, request: Request, name: str, obj) -> bool:
		action = self.action_permissions.get(name)
		if action:
			return request.state.admin_user.is_op or action in request.state.admin_user.permissions

		return await super().is_action_allowed(request, name)