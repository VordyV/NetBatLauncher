from __future__ import annotations
from typing import TYPE_CHECKING
if TYPE_CHECKING: from .core import NetBatLauncherServer
from abc import ABC


class Service(ABC):

	def __init__(self, core: NetBatLauncherServer):
		self.core = core
