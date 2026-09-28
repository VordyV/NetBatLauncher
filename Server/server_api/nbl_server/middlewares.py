from starlette.middleware.base import BaseHTTPMiddleware
from starlette.requests import Request
from loguru import logger
import time

class LogMiddleware(BaseHTTPMiddleware):

	async def dispatch(self, request: Request, call_next):
		request_size = int(request.headers.get("content-length") or 0)
		start = time.perf_counter()
		response = await call_next(request)
		duration = (time.perf_counter() - start) * 1000
		response_size = int(response.headers.get("content-length") or 0)

		logger.bind(request=True, addr=request.client.host, port=request.client.port, agent=request.headers.get("user-agent", "none"), method=request.method, path=request.url.path, status=response.status_code, duration=duration, request_size=request_size, response_size=response_size).debug("")
		return response