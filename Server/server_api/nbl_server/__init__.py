from .core import NetBatLauncherServer
import os

__version__ = "0.6.1"
__debug_mode__ = int(os.getenv("DEBUG", 0))