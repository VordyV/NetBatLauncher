import flet as ft
import flet_easy as fs

simple_router = fs.AddPagesy()

@simple_router.page("/", title="Simple", index=0, cache=True, share_data=True)
class SimplePage:

    @property
    def get_data(self) -> fs.Datasy: return self.data

    @property
    def page(self) -> ft.Page: return self.get_data.page

    def build(self):

        return ft.View(
            horizontal_alignment=ft.CrossAxisAlignment.CENTER,
            vertical_alignment=ft.MainAxisAlignment.CENTER,
            controls=[
            ]
        )