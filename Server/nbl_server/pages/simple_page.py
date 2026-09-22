import flet as ft
import flet_easy as fs
from .page import Page

simple_router = fs.AddPagesy()

@simple_router.page("/", title="Simple", index=0, cache=True, share_data=True)
class SimplePage(Page):

    async def build(self):

        return ft.View(
            horizontal_alignment=ft.CrossAxisAlignment.CENTER,
            vertical_alignment=ft.MainAxisAlignment.CENTER,
            controls=[
            ]
        )