import flet as ft
import flet_easy as fs
import urllib.parse
import base64
import httpx
from nbl_server.client import APIClient, APIError
from .page import Page
from nbl_server.session_state import SessionState
import jwt
import datetime
import os

login_router = fs.AddPagesy()

@login_router.page("/auth/callback/{otac}", share_data=True, page_clear=True)
class AuthCallBackPage(Page):

    def __init__(self, *args, otac: str):
        super().__init__(*args)
        self._otac = otac

    async def task_verify_sig(self):
        try:
            prefs = ft.SharedPreferences()
            token = await APIClient.exchange(self._otac, self.page.client_user_agent)
            await prefs.set("nbl.token", token.access_token)

            self._ui_text.value = "Authorization completed successfully. Redirecting to the main page..."

            account_data = await APIClient.me(access_token=token.access_token)

            payload = jwt.decode(token.access_token, options={"verify_signature": False})
            exp = datetime.datetime.fromtimestamp(payload.get("exp"), tz=self.core.time_zone) - datetime.timedelta(seconds=int(os.getenv("MIN_REM_LIFETIME_REFRESH")))

            await self.data.set_session(account_data, exp)

            self.data.go_route("/")

        except APIError as e:
            self.data.go_route("/login/1")

    def build(self):
        self.data.page.run_task(self.task_verify_sig)
        self._ui_text = ft.Text("Signing in to the account")
        return ft.View(
            controls=[
                self._ui_text,
            ]
        )

@login_router.page("/login", title="Login", share_data=True, page_clear=True)
class LoginPage(Page):

    def __init__(self, data: fs.Datasy, err_code: int = 0):
        self._err_core = err_code
        super().__init__(data)

    async def _on_click(self, e):
        url_launcher = ft.UrlLauncher()
        url = APIClient.login_url()
        await url_launcher.launch_url(ft.Url(url=url, target=ft.UrlTarget.SELF))
        #self.data.go_route("/")

    def build(self):
        print(self.data.page.session.id)

        self._ui_text_warn_desc = ft.Text("", color="#92400e")

        if self._err_core:
            if self._err_core == 1: self._ui_text_warn_desc.value = "Your SteamID was not found in the list of trusted GameOps administrators"

        self._ui_warn = ft.Container(
            bgcolor=ft.Colors.with_opacity(0.8, "#fffbeb"),
            border=ft.Border.all(1, ft.Colors.with_opacity(0.8, "#fde68a")),
            content=ft.Column([
                ft.Row([ft.Icon(ft.Icons.WARNING_AMBER, color="#d97706", size=16), ft.Text("Access denied", color="#451a03", weight=ft.FontWeight.BOLD)]),
                self._ui_text_warn_desc
            ]),
            padding=ft.Padding.all(14),
            border_radius=4,
            visible=True if self._err_core else False
        )

        return ft.View(
            horizontal_alignment=ft.CrossAxisAlignment.CENTER,
            vertical_alignment=ft.MainAxisAlignment.CENTER,
            controls=[
                ft.Card(
                    shadow_color=ft.Colors.ON_SURFACE_VARIANT,
                    shape=ft.RoundedRectangleBorder(radius=4),
                    content=ft.Container(
                        padding=ft.Padding.all(36),
                        width=420,
                        bgcolor=ft.Colors.ON_PRIMARY,
                        content=ft.Column(
                            horizontal_alignment=ft.CrossAxisAlignment.CENTER,
                            controls=[
                                ft.Text("NBLauncher", theme_style=ft.TextThemeStyle.DISPLAY_SMALL, weight=ft.FontWeight.BOLD),
                                ft.Text("A unified control center for managing games, servers, and the launcher", text_align=ft.TextAlign.CENTER, color="#94a3b8"),
                                ft.Divider(),
                                ft.FilledButton(content=ft.Container(content=ft.Text("Sign in with Steam"), on_click=self._on_click, expand=True, padding=ft.Padding.symmetric(vertical=14, horizontal=20)), bgcolor="#171a21", icon=ft.Image(src="steam.svg", width=20, height=20, color=ft.Colors.ON_PRIMARY)),
                                ft.Text("Login is available only to authorized users", color="#94a3b8"),
                                self._ui_warn,
                            ]
                        )
                    )
                ),
                #ft.Button("", on_click=self.click)
            ]
        )

@login_router.page("/login/{err_code:int}", title="Login", share_data=True, page_clear=True)
class LoginErrorPage(Page):

    def __init__(self, data: fs.Datasy, err_code: int):
        super().__init__(data)
        self._err_code = err_code

    def build(self):
        return LoginPage(data=self.data, err_code=self._err_code).build()
