import flet as ft
import flet_easy as fs
from net_bat_launcher_server.navigationRail import NavigationRail, NavigationRailItem

clients_router = fs.AddPagesy()

@clients_router.page("/clients", title="Clients", page_clear=True, share_data=True)
class ClientsPage:

    @property
    def get_data(self) -> fs.Datasy: return self.data

    @property
    def page(self) -> ft.Page: return self.get_data.page

    def build(self):
        self._ui_rail = NavigationRail(
            selected_index=2,
            data=self.get_data,
            items=self.get_data.core.nav_pages
        )

        return ft.View(
            appbar=self.get_data.core.app_bar(self.get_data),
            horizontal_alignment=ft.CrossAxisAlignment.CENTER,
            vertical_alignment=ft.MainAxisAlignment.CENTER,
            controls=[
                ft.Row(
                    expand=True,
                    controls=[
                        ft.SelectionArea(content=self._ui_rail),
                        ft.VerticalDivider(width=1),
                        ft.Column(
                            alignment=ft.MainAxisAlignment.START,
                            expand=True,
                            controls=[ft.Text("Clients!")],
                        ),
                    ],
                ),
            ]
        )