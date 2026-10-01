import os
from nbl_server import NetBatLauncherServer
from prompt_toolkit.patch_stdout import StdoutProxy

from loguru import logger

if __name__ == '__main__':
	logger.remove()
	logger.add(StdoutProxy(raw=True), level="DEBUG", format="[{time:HH:mm:ss}] <level>{level}</level>: {message}", colorize=True, enqueue=True, filter=lambda record: "request" not in record["extra"])
	logger.add(StdoutProxy(raw=True), level="DEBUG", format="[{time:HH:mm:ss}] {extra[addr]}:{extra[port]} {extra[method]} {extra[path]} - {extra[status]}", colorize=True, enqueue=True, filter=lambda record: "request" in record["extra"])
	logger.add("logs/{time:YYYY-MM-DD}.log", level="DEBUG", format="[{time:HH:mm:ss}] {level}: {message}", colorize=True, enqueue=True, filter=lambda record: "request" not in record["extra"], rotation="03:00")
	logger.add("logs/{time:YYYY-MM-DD}.rlog", level="DEBUG", format="[{time:HH:mm:ss}] {extra[addr]}:{extra[port]}:{extra[agent]}: {extra[method]} {extra[path]} - {extra[status]} reqlen:{extra[request_size]} reslen:{extra[response_size]}", colorize=True, enqueue=True, filter=lambda record: "request" in record["extra"], rotation="03:00")
	nbl = NetBatLauncherServer(address=os.getenv("ADDRESS", "127.0.0.1"), port=int(os.getenv("PORT", 80)), log_level=os.getenv("LOG_LEVEL", "error"))
	nbl.start()