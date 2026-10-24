from .core import NetBatLauncherServer
import os

__version__ = "0.6"
__debug_mode__ = int(os.getenv("DEBUG", 0))