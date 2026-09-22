import flet as ft

class NavigationItem:

	def __init__(self, label: str, icon: ft.IconData, url_page: str):
		self.__label = label
		self.__icon = icon
		self.__url_page = url_page

	@property
	def label(self) -> str: return self.__label

	@property
	def icon(self) -> ft.IconData: return self.__icon

	@property
	def url_page(self) -> str: return self.__url_page