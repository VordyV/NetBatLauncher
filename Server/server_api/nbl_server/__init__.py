from .core import NetBatLauncherServer
import os

__version__ = "0.8"
__debug_mode__ = int(os.getenv("DEBUG", 0))