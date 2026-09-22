import os.path

import flet as ft
from flet import control
from nbl_server.client import APIClient, APIError, ValidationError, ForbiddenError, NotFoundError
import flet_easy as fs
from .page import Page
from nbl_server.schemes import RequestGameCreate, RequestGameUpdate
from nbl_server.controls import AppBar, NavigationRail, View, TextField, TextButton
import flet_datatable2 as fdt

games_router = fs.AddPagesy()

@games_router.page("/games", title="Games", page_clear=True, share_data=True)
class GamesPage(Page):

    def _on_click_context_delete(self, game_id: str):
        async def func(e):
            try:
                await APIClient.games_delete(access_token=await self.data.get_token(), gameid=game_id)
                self.data.go_route("/games")
            except NotFoundError as e:
                self.page.show_dialog(ft.SnackBar(ft.Text(e.detail)))
            except ForbiddenError as e:
                self.page.show_dialog(ft.SnackBar(ft.Text(e.detail)))
            except APIError as e:
                self.page.show_dialog(ft.SnackBar(ft.Text(e.detail)))
            except Exception as e:
                print(f"page {self.data.route}: {e}")
        return func

    async def _load_games(self):
        await ft.BrowserContextMenu().disable()

        token = await self.data.get_token()
        if not token: return

        try:
            games = await APIClient.games_read_all(access_token=token)

            index = 0
            for game in games.games:
                self._ui_datatable.rows.append(fdt.DataRow2(
                    cells=[
                        ft.DataCell(content=ft.Text(index:=index+1)),
                        ft.DataCell(content=ft.Text(game.ident)),
                        ft.DataCell(content=ft.Text(game.name)),
                        ft.DataCell(content=ft.Text(game.short_name)),
                        ft.DataCell(content=ft.Text(game.created_at.astimezone(self.core.time_zone).strftime('%d.%m.%Y %H:%M'))),
                        ft.DataCell(content=ft.Text(game.modified.astimezone(self.core.time_zone).strftime('%d.%m.%Y %H:%M'))),
                        ft.DataCell(content=ft.ContextMenu(
                            content=ft.Icon(ft.Icons.MENU),
                            primary_items=[
                                ft.PopupMenuItem(content="Edit", on_click=self.data.go(f"/games/{game.ident}")),
                                ft.PopupMenuItem(content="Delete", on_click=self._on_click_context_delete(game_id=game.ident)),
                            ],
                            primary_trigger=ft.ContextMenuTrigger.DOWN,
                        )),
                    ]
                ))

            if index < 1: self._ui_datatable.empty = ft.Text("This table is empty.")
        except ForbiddenError as e:
            self.data.go_route("/")
        except APIError as e:
            self.page.show_dialog(ft.SnackBar(ft.Text(e.detail)))
        except Exception as e:
            print(f"page {self.data.route}: {e}")

    async def build(self):

        self._ui_datatable = fdt.DataTable2(
            expand=True,
            empty=ft.ProgressRing(),
            columns=[
                fdt.DataColumn2(label=ft.Text("#")),
                fdt.DataColumn2(label=ft.Text("ident")),
                fdt.DataColumn2(label=ft.Text("Name")),
                fdt.DataColumn2(label=ft.Text("Short name")),
                fdt.DataColumn2(label=ft.Text("Creation date")),
                fdt.DataColumn2(label=ft.Text("Date modified")),
                fdt.DataColumn2(label=ft.Text("Actions")),
            ],
        )

        await self._load_games()

        return View(
            data=self.data,
            selected_index_page=1,
            margin=ft.Padding.symmetric(vertical=24, horizontal=24),
            content=ft.Column(
                controls=[
                    ft.Row(
                        controls=[
                            ft.Text("Game management", theme_style=ft.TextThemeStyle.DISPLAY_SMALL),
                            ft.FilledButton("Add a new game", on_click=self.data.go("/games/new"))
                        ],
                    ),
                    self._ui_datatable
                ]
            )
        )

@games_router.page("/games/new", title="Games new", page_clear=True, share_data=True)
class GamesDialogPage(Page):

    def __init__(self, data: fs.Datasy, game_id: str = None):
        self._game_id = game_id
        super().__init__(data)

    async def _on_click_save(self, e):
        try:
            token = await self.data.get_token()
            if not self._game_id: await APIClient.games_create(access_token=token, ident=self._ui_textfield_id.value, name=self._ui_textfield_name.value, short_name=self._ui_textfield_shortname.value)
            else: await APIClient.games_update(access_token=token, gameid=self._game_id, ident=self._ui_textfield_id.value, name=self._ui_textfield_name.value, short_name=self._ui_textfield_shortname.value)
            self.data.go_route("/games")
        except ValidationError as e:
            for k, v in e.field_errors().items():
                if k == "ident": self._ui_textfield_id.error = v
                elif k == "name": self._ui_textfield_name.error = v
                elif k == "short_name": self._ui_textfield_shortname.error = v
        except APIError as e:
            self.page.show_dialog(ft.SnackBar(ft.Text(e.detail)))
        except Exception as e:
            print(f"page {type(e)} {self.data.route}: {e}")

    async def _on_upload_file_picker(self, e: ft.FilePickerUploadEvent):
        self._ui_text_progress_bar_upload_file.value = e.progress
        print(self._ui_text_progress_bar_upload_file)
        if e.error:
            self._ui_text_upload_filename.value = f"error: {e.error}"
            self._ui_text_progress_bar_upload_file.value = 0.0

        if e.progress == 1.0:
            self._ui_text_upload_filename.value += " (uploaded)"

        if e.error or e.progress == 1.0:
            self._ui_button_save.disabled = False

    async def _on_click_select_file(self, e):
        self._file_picker = ft.FilePicker(on_upload=self._on_upload_file_picker)
        self._files = await self._file_picker.pick_files()
        if len(self._files) < 1:
            return

        self._ui_text_upload_filename.visible = True
        self._ui_textbutton_upload_file.visible = True
        self._ui_text_upload_filename.value = f"file name: {self._files[0].name}"

    async def _on_click_upload_file(self, e):
        self._ui_text_progress_bar_upload_file.visible = True
        self._ui_button_save.disabled = True
        self._ui_textbutton_upload_file.visible = False
        try:
            otac = await APIClient.upload_game_files(await self.data.get_token(), self._ui_textfield_id.value.strip(), self._files[0].name)
            await self._file_picker.upload([ft.FilePickerUploadFile(upload_url=f"/api/games/files/upload?otac={otac.otac}", name=file.name) for file in self._files])
        except APIError as e:
            self.page.show_dialog(ft.SnackBar(ft.Text(e.detail)))
        except Exception as e:
            print(f"page {type(e)} {self.data.route}: {e}")

    async def _load_game(self):
        try:
            game = await APIClient.games_read(access_token=await self.data.get_token(), game_id=self._game_id)
            self._ui_textfield_id.value = game.ident
            self._ui_textfield_name.value = game.name
            self._ui_textfield_shortname.value = game.short_name

            self._ui_text_title.value += " " + game.name
        except ForbiddenError as e:
            self.page.show_dialog(ft.SnackBar(ft.Text(e.detail)))
        except NotFoundError as e:
            self.page.show_dialog(ft.SnackBar(ft.Text(e.detail)))
        except APIError as e:
            self.page.show_dialog(ft.SnackBar(ft.Text(e.detail)))
        except Exception as e:
            print(f"page {self.data.route}: {e}")

    async def build(self):
        self._ui_textfield_id = TextField("id", required=True)
        self._ui_textfield_name = TextField("name", required=True)
        self._ui_textfield_shortname = TextField("short name", required=True)
        self._ui_textbutton_select_file = TextButton("Upload game files for download", "Select file", on_click=self._on_click_select_file)
        self._ui_text_upload_filename = ft.Text(visible=False)
        self._ui_text_progress_bar_upload_file = ft.ProgressBar(value=0.0, visible=False)
        self._ui_textbutton_upload_file = ft.TextButton("Upload files", visible=False, on_click=self._on_click_upload_file)
        self._ui_button_save = ft.Button("Save", on_click=self._on_click_save)
        self._ui_text_title = ft.Text("Add a new game" if not self._game_id else "Game editing", theme_style=ft.TextThemeStyle.DISPLAY_SMALL)

        self._file_picker = None
        self._files = None

        self._ui_textbutton_select_file.disabled = not bool(self._game_id)

        if self._game_id: await self._load_game()

        return View(
            data=self.data,
            selected_index_page=1,
            margin=ft.Padding.symmetric(vertical=24, horizontal=24),
            content=ft.Column(
                controls=[
                    ft.Row(
                        controls=[
                            self._ui_text_title
                        ],
                    ),
                    self._ui_textfield_id,
                    self._ui_textfield_name,
                    self._ui_textfield_shortname,
                    ft.Divider(),
                    self._ui_textbutton_select_file,
                    self._ui_text_progress_bar_upload_file,
                    self._ui_text_upload_filename,
                    self._ui_textbutton_upload_file,
                    ft.Text("The file must be a zip archive and contain all files in the root directory of the archive"),
                    ft.Divider(),
                    self._ui_button_save
                ]
            )
        )

@games_router.page("/games/{gameid}", title="Game edit", page_clear=True, share_data=True)
class GamesDialogEditPage(Page):

    def __init__(self, data: fs.Datasy, gameid: str):
        super().__init__(data)
        self._game_id = gameid

    async def build(self):
        return await GamesDialogPage(data=self.data, game_id=self._game_id).build()

@games_router.page("/games/files/{gameid}", title="Game files", page_clear=True, share_data=True)
class GamesFilesPage(Page):

    def __init__(self, data: fs.Datasy, gameid: str):
        self._game_id = gameid
        super().__init__(data)

    async def _load_game(self):
        try:
            game = await APIClient.games_read(access_token=await self.data.get_token(), game_id=self._game_id)
            self._ui_text_title.value += " " + game.name

        except ForbiddenError as e:
            self.page.show_dialog(ft.SnackBar(ft.Text(e.detail)))
        except NotFoundError as e:
            self.page.show_dialog(ft.SnackBar(ft.Text(e.detail)))
        except APIError as e:
            self.page.show_dialog(ft.SnackBar(ft.Text(e.detail)))
        except Exception as e:
            print(f"page {self.data.route}: {e}")

    async def _on_click_upload_via_link(self, e):
        if not self._ui_textfield.value.strip():
            self._ui_textfield.error = "The field must not be empty"
            return

        try:
            token = await self.data.get_token()
            otac = await APIClient.upload_game_files(access_token=token, gameid=self._game_id, filename="link")
            game = await APIClient.upload_game_files_via_link(otac=otac.otac, link=self._ui_textfield.value.strip())
            print("ok")
        except ForbiddenError as e:
            self.page.show_dialog(ft.SnackBar(ft.Text(e.detail)))
        except NotFoundError as e:
            self.page.show_dialog(ft.SnackBar(ft.Text(e.detail)))
        except APIError as e:
            self.page.show_dialog(ft.SnackBar(ft.Text(e.detail)))
        except Exception as e:
            print(f"page {self.data.route}: {e}")

    async def build(self):
        self._ui_text_title = ft.Text("Game file management", theme_style=ft.TextThemeStyle.DISPLAY_SMALL)
        self._ui_textfield = ft.TextField(label="Direct link to the ZIP archive", expand=True)


        await self._load_game()
        return View(
            data=self.data,
            selected_index_page=1,
            margin=ft.Padding.symmetric(vertical=24, horizontal=24),
            content=ft.Column(
                controls=[
                    ft.Row(
                        controls=[
                            self._ui_text_title,
                        ],
                    ),
                    ft.Card(
                        shadow_color=ft.Colors.ON_SURFACE_VARIANT,
                        shape=ft.RoundedRectangleBorder(radius=4),
                        content=ft.Container(
                            padding=ft.Padding.all(96),
                            width=896,
                            height=624,
                            bgcolor=ft.Colors.ON_PRIMARY,
                            content=ft.Column(
                                horizontal_alignment=ft.CrossAxisAlignment.CENTER,
                                controls=[
                                    ft.Text("The client files have not been uploaded yet", theme_style=ft.TextThemeStyle.BODY_LARGE, weight=ft.FontWeight.BOLD),
                                    ft.Text("For the initial deployment of the game, provide a direct link to a ZIP archive containing the build. Selecting a regular local file from your device is not supported — the transfer is done directly via a URL to server storage or a CDN", text_align=ft.TextAlign.CENTER),
                                    ft.Container(
                                        bgcolor=ft.Colors.with_opacity(0.8, ft.Colors.SURFACE_CONTAINER_LOW),
                                        #border=ft.Border.all(1, ft.Colors.with_opacity(0.8, "#fde68a")),
                                        content=ft.Column([
                                            ft.Row([ft.Icon(ft.Icons.WARNING_AMBER, color=ft.Colors.PRIMARY, size=16), ft.Text("Requirements for the distribution source", color=ft.Colors.ON_SURFACE, weight=ft.FontWeight.BOLD)]),
                                            ft.Text("The direct link must end in .zip, be publicly accessible via the HTTP/HTTPS protocol without redirects or authorization pages", color=ft.Colors.ON_SURFACE_VARIANT)
                                        ]),
                                        padding=ft.Padding.all(14),
                                        border_radius=4,
                                    ),
                                    ft.Row([self._ui_textfield]),
                                    ft.FilledButton("Upload", on_click=self._on_click_upload_via_link)
                                ]
                            )
                        )
                    ),
                ]
            )
        )