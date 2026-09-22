import flet as ft
import flet_easy as fs
from .page import Page
from nbl_server.client import APIClient, APIError, ForbiddenError, NotFoundError

storage_router = fs.AddPagesy()

@storage_router.page("/storage/tasks/{taskid}", title="Simple", index=0, cache=False, share_data=True)
class StorageTaskPage(Page):

	def __init__(self, data: fs.Datasy, taskid: str):
		self._taskid = taskid
		super().__init__(data)

	async def _load(self):
		try:
			task = await APIClient.get_storage_task(access_token=await self.data.get_token(), task_id=self._taskid)
			self._ui_text_status.value = task.status
			self._ui_text_error.value = task.error
		except APIError as e:
			self.page.show_dialog(ft.SnackBar(ft.Text(e.detail)))
		except Exception as e:
			print(f"page {self.data.route}: {e}")

	async def _on_click_refresh(self, e):
		await self._load()

	async def build(self):
		self._ui_text_status = ft.Text()
		self._ui_text_error = ft.Text()
		self._ui_refresh = ft.Button("refresh", on_click=self._on_click_refresh)

		await self._load()

		return ft.View(
			horizontal_alignment=ft.CrossAxisAlignment.CENTER,
			vertical_alignment=ft.MainAxisAlignment.CENTER,
			controls=[
				self._ui_text_status,
				self._ui_text_error,
				self._ui_refresh
			]
		)