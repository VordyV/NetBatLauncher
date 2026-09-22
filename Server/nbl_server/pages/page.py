from __future__ import annotations
from typing import TYPE_CHECKING
if TYPE_CHECKING: from nbl_server.core import NetBatLauncherServer
import flet_easy as fs
import flet as ft


class Page:

	def __init__(self, data: fs.Datasy):
		self.data: fs.Datasy = data
		self.page: ft.Page = self.data.page
		self.core: NetBatLauncherServer = self.data.core