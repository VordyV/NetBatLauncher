from .core import NetBatLauncherServer
import os

__version__ = "0.4"
__debug_mode__ = int(os.getenv("DEBUG", 0))