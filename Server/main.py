from dotenv import load_dotenv
load_dotenv()
from nbl_server import NetBatLauncherServer
from prompt_toolkit.patch_stdout import StdoutProxy
import os
from loguru import logger

if __name__ == '__main__':
	logger.remove()
	logger.add(StdoutProxy(raw=True), format="[{time:HH:mm:ss}] {level}: {message}", colorize=True, enqueue=True)
	logger.add("{time:YYYY-MM-DD}.log", enqueue=True, rotation="03:00")

	nbl = NetBatLauncherServer(address=os.getenv("ADDRESS"), port=int(os.getenv("PORT")), log_level=os.getenv("LOG_LEVEL"))
	nbl.start()