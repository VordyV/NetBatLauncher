import flet as ft
import flet_easy as fs
from .page import Page
from nbl_server.controls import AppBar, NavigationRail, View
import asyncio

general_router = fs.AddPagesy()

@general_router.page("/", title="Gen", page_clear=True, share_data=True)
class GeneralPage(Page):

    def build(self):
        return View(
            data=self.data,
            selected_index_page=0,
            content=ft.Text("11")
        )