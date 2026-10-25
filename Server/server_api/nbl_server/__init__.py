from .core import NetBatLauncherServer
import os

__version__ = "0.6.2"
__debug_mode__ = int(os.getenv("DEBUG", 0))