from typing import Callable
import flet as ft
from flet import control

import flet_easy as fs
from .navigation import NavigationItem

class AppBar(ft.AppBar):
	
	def __init__(self, data: fs.Datasy, on_click_menu: Callable):
		self._data = data
		super().__init__(
			leading=ft.IconButton(ft.Icons.MENU, on_click=on_click_menu),
			title=ft.Text("Net bat launcher"),
			bgcolor=ft.Colors.SURFACE_CONTAINER,
			actions=[
				ft.Container(content=ft.Text(self._data.page.session.store.get("account.name")), padding=ft.Padding.only(right=12)),
				ft.Container(content=ft.CircleAvatar(foreground_image_src=self._data.page.session.store.get("account.photo")), padding=ft.Padding.only(right=12)),
				ft.Container(content=ft.FilledButton("Log out", on_click=self._on_logout), padding=ft.Padding.only(right=24)),
			],
		)

	async def _on_logout(self, e):
		await self._data.logout()

class NavigationRailButton(ft.Row):

	def __init__(self, text: str, icon: ft.IconData, on_click: Callable, url_page: str, selected: bool = False):
		super().__init__()
		self.on_click = on_click
		self.url_page = url_page
		self.selected = selected
		self._PART_button = ft.TextButton(text, icon=icon, on_click=self._on_click, expand=True, height=36, style=ft.ButtonStyle(alignment=ft.Alignment.CENTER_LEFT, shape=ft.RoundedRectangleBorder(radius=4), bgcolor=ft.Colors.PRIMARY if selected else None, color=ft.Colors.ON_PRIMARY if selected else ft.Colors.ON_SURFACE_VARIANT))

		self.controls = [
			self._PART_button
		]

	async def _on_click(self, e):
		if callable(self.on_click) and not self.selected: await self.on_click(self.url_page)


class NavigationRail(ft.Container):

	def __init__(self, data: fs.Datasy, items: list[NavigationItem], selected_index: int = 0):
		self._data = data
		super().__init__(
			width=255,
			bgcolor="#ffffff",
			padding=ft.Padding.symmetric(vertical=0, horizontal=8),
		)

		self._ui_text_nav = ft.Text("Navigation", weight=ft.FontWeight.BOLD, color=ft.Colors.ON_SURFACE_VARIANT)

		self.content = ft.Column(
			expand=True,
			spacing=4,
			controls=[
				ft.Container(content=self._ui_text_nav, padding=ft.Padding.only(top=8, bottom=4, left=12, right=12)),
				*[NavigationRailButton(text=items[i].label, icon=items[i].icon, on_click=self._on_click, url_page=items[i].url_page, selected=i == selected_index) for i in range(len(items))]
			]
		)

	async def _on_click(self, url_page):
		self._data.go_route(url_page)

	@property
	def extended(self) -> bool:
		if self.width == 255:
			self._ui_text_nav.visible = False
			return True
		else:
			self._ui_text_nav.visible = True
			return False

	@extended.setter
	def extended(self, value: bool): self.width = 255 if value else 55


class View(ft.View):
	
	def __init__(self, data: fs.Datasy, selected_index_page: int, content: ft.Control, margin: ft.Padding = None):
		self._data = data
		self.content = content
		super().__init__(
            appbar=AppBar(data=self._data, on_click_menu=self._on_click_appbar_menu),
            padding=ft.Padding.all(0),
            horizontal_alignment=ft.CrossAxisAlignment.CENTER,
            vertical_alignment=ft.MainAxisAlignment.CENTER,
		)

		self._PART_nav = NavigationRail(data=self._data, selected_index=selected_index_page, items=self._data.core.pages)

		self.controls=[
			ft.Row(
				expand=True,
				spacing=0,
				controls=[
					self._PART_nav,
					ft.Container(width=1, bgcolor=ft.Colors.with_opacity(0.3, "#c7c4d8")),
                    ft.Column(
						margin=margin,
                        alignment=ft.MainAxisAlignment.START,
                        expand=True,
                        controls=[content],
                    ),
 				]
			),
		]

	async def _on_click_appbar_menu(self, e):
		self._PART_nav.extended = not self._PART_nav.extended

class TextField(ft.Container):

	def __init__(self, label: str, required: bool = False):
		self._PART_textfield = ft.TextField(border=ft.NoInputBorder(), text_size=14, content_padding=ft.Padding.only(bottom=12, left=12), filled=True, height=36)

		super().__init__(
			expand=True,
			content=ft.Column(
				spacing=6,
				controls=[
					ft.Row([ft.Text(label, expand=True, weight=ft.FontWeight.BOLD), ft.Text("*", visible=required, color="red", weight=ft.FontWeight.BOLD)], spacing=4),
					self._PART_textfield
				]
			)
		)

	@property
	def value(self) -> str: return self._PART_textfield.value

	@value.setter
	def value(self, value: str): self._PART_textfield.value = value

	@property
	def error(self) -> str: return self._PART_textfield.error

	@error.setter
	def error(self, value: str): self._PART_textfield.error = value

class TextButton(ft.Container):
	def __init__(self, text: str, label: str, on_click: Callable = None, required: bool = False):
		self._PART_textbutton = ft.TextButton(text, on_click=on_click)

		super().__init__(
			content=ft.Column(
				spacing=6,
				controls=[
					ft.Row([ft.Text(label, weight=ft.FontWeight.BOLD), ft.Text("*", visible=required, color="red", weight=ft.FontWeight.BOLD)], spacing=4),
					self._PART_textbutton
				]
			)
		)