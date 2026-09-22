from __future__ import annotations

from typing import Any, Optional
from .schemes import (
	ResponseExchange,
	ResponseRefresh,
	ResponseAccountData,
	RequestGameCreate,
	RequestGameUpdate,
	ResponseGamesReadAll,
	ResponseGamesRead,
	ResponseGameFilesUpload
)
import httpx


class APIError(Exception):
	def __init__(
		self,
		status_code: int,
		detail: Any = None,
		response: Optional[httpx.Response] = None,
	):
		self.status_code = status_code
		self.detail = detail
		self.response = response
		super().__init__(f"HTTP {status_code}: {detail}")

	@property
	def is_not_found(self) -> bool:
		return self.status_code == 404

	@property
	def is_forbidden(self) -> bool:
		return self.status_code == 403

	@property
	def is_unauthorized(self) -> bool:
		return self.status_code in (401, 403)

	@property
	def is_client_error(self) -> bool:
		return 400 <= self.status_code < 500

	@property
	def is_validation_error(self) -> bool:
		return self.status_code == 422

class NotFoundError(APIError):

	def __init__(self, detail: Any = None, response: Optional[httpx.Response] = None):
		super().__init__(404, detail, response)


class ForbiddenError(APIError):

	def __init__(self, detail: Any = None, response: Optional[httpx.Response] = None):
		super().__init__(403, detail, response)

class ValidationError(APIError):

	def __init__(self, detail: Any = None, response: Optional[httpx.Response] = None):
		super().__init__(422, detail, response)

	@property
	def errors(self) -> list[dict[str, Any]]:
		if isinstance(self.detail, list):
			return [e for e in self.detail if isinstance(e, dict)]
		return []

	def field_errors(self) -> dict[str, str]:
		result: dict[str, str] = {}
		for err in self.errors:
			loc = err.get("loc") or []
			field = ".".join(str(x) for x in loc if x not in ("body", "query", "path", "header"))
			if not field:
				field = "non_field"
			msg = err.get("msg") or err.get("type") or "Invalid value"
			if field in result:
				result[field] = f"{result[field]}; {msg}"
			else:
				result[field] = msg
		return result

	def pretty_message(self) -> str:
		fields = self.field_errors()
		if not fields:
			return str(self.detail)
		return "; ".join(f"{field}: {msg}" for field, msg in fields.items())

	def __str__(self) -> str:
		return f"HTTP 422: {self.pretty_message()}"

class APIClient:
	_base_url: str = ""
	_timeout: float = 7.0

	@classmethod
	def configure(cls, base_url: str, timeout: float = 7.0) -> None:
		cls._base_url = base_url.rstrip("/")
		cls._timeout = timeout

	@classmethod
	def _url(cls, path: str) -> str:
		if not cls._base_url:
			raise RuntimeError(
				"APIClient is not configured. Call APIClient.configure(base_url) first."
			)
		return f"{cls._base_url}{path}"

	@classmethod
	def _auth_headers(cls, access_token: Optional[str] = None) -> dict[str, str]:
		headers = {"Accept": "application/json"}
		if access_token:
			headers["Authorization"] = f"Bearer {access_token}"
		return headers

	@classmethod
	def _raise_for_status(cls, response: httpx.Response) -> None:
		if response.status_code < 400:
			return

		try:
			payload = response.json()
			detail = payload.get("detail", payload) if isinstance(payload, dict) else payload
		except Exception:
			detail = response.text or response.reason_phrase

		if response.status_code == 404:
			raise NotFoundError(detail, response)
		if response.status_code == 403:
			raise ForbiddenError(detail, response)
		if response.status_code == 422:
			raise ValidationError(detail, response)

		raise APIError(response.status_code, detail, response)

	@classmethod
	async def _request(
		cls,
		method: str,
		path: str,
		*,
		params: Optional[dict[str, Any]] = None,
		json: Optional[dict[str, Any]] = None,
		access_token: Optional[str] = None,
		follow_redirects: bool = False,
	) -> Any:
		url = cls._url(path)

		headers = cls._auth_headers(access_token)
		if json is not None:
			headers["Content-Type"] = "application/json"

		try:
			async with httpx.AsyncClient(
				timeout=cls._timeout,
				follow_redirects=follow_redirects,
			) as client:
				response = await client.request(
					method=method,
					url=url,
					params=params,
					json=json,
					headers=headers,
				)
		except httpx.TimeoutException as e:
			raise APIError(408, "Request timeout") from e
		except httpx.RequestError as e:
			raise APIError(0, f"Network error: {e}") from e

		cls._raise_for_status(response)

		if response.status_code == 204 or not response.content:
			return None

		content_type = response.headers.get("content-type", "")
		if "application/json" in content_type:
			return response.json()
		return response.text

	@classmethod
	def login_url(cls) -> str:
		return cls._url("/api/auth/login")

	@classmethod
	async def exchange(cls, otac: str, agent: str) -> ResponseExchange:
		if not otac:
			raise ValueError("otac is required")
		if not agent:
			raise ValueError("agent is required")

		data = await cls._request(
			"GET",
			"/api/auth/exchange",
			params={"otac": otac, "agent": agent},
		)
		return ResponseExchange.model_validate(data)

	@classmethod
	async def refresh(cls, access_token: str) -> ResponseRefresh:
		if not access_token:
			raise ValueError("access_token is required")

		data = await cls._request(
			"GET",
			"/api/auth/refresh",
			access_token=access_token,
		)
		return ResponseRefresh.model_validate(data)

	@classmethod
	async def logout(cls, access_token: str) -> None:
		if not access_token:
			raise ValueError("access_token is required")

		await cls._request(
			"GET",
			"/api/auth/logout",
			access_token=access_token,
		)

	@classmethod
	async def me(cls, access_token: str) -> ResponseAccountData:
		if not access_token:
			raise ValueError("access_token is required")

		data = await cls._request(
			"GET",
			"/api/accounts/me",
			access_token=access_token,
		)
		if not isinstance(data, dict):
			raise APIError(500, "Invalid response format")
		return ResponseAccountData.model_validate(data)

	@classmethod
	async def games_create(cls, access_token: str, ident: str, name: str, short_name: str) -> None:
		await cls._request(
			"POST",
			"/api/games",
			json={
				"ident": ident,
				"name": name,
				"short_name": short_name
			},
			access_token=access_token,
		)

	@classmethod
	async def games_read_all(cls, access_token: str) -> ResponseGamesReadAll:
		if not access_token:
			raise ValueError("access_token is required")

		data = await cls._request(
			"GET",
			"/api/games",
			access_token=access_token,
		)
		return ResponseGamesReadAll.model_validate(data)

	@classmethod
	async def games_read(cls, access_token: str, game_id: str) -> ResponseGamesRead:
		if not access_token:
			raise ValueError("access_token is required")

		data = await cls._request(
			"GET",
			f"/api/games?gameid={game_id}",
			access_token=access_token,
		)
		return ResponseGamesRead.model_validate(data)

	@classmethod
	async def games_update(cls, access_token: str, gameid: str, ident: str, name: str, short_name: str) -> None:
		await cls._request(
			"PUT",
			"/api/games",
			params={"gameid": gameid},
			json={
				"ident": ident,
				"name": name,
				"short_name": short_name
			},
			access_token=access_token,
		)

	@classmethod
	async def games_delete(cls, access_token: str, gameid: str) -> None:
		await cls._request(
			"DELETE",
			"/api/games",
			params={"gameid": gameid},
			access_token=access_token,
		)

	@classmethod
	async def upload_game_files(cls, access_token: str, gameid: str, filename: str) -> ResponseGameFilesUpload:
		data = await cls._request(
			"GET",
			"/api/games/files/upload",
			params={"gameid": gameid, "filename": filename},
			access_token=access_token,
		)

		return ResponseGameFilesUpload.model_validate(data)

	@classmethod
	async def upload_game_files_via_link(cls, otac: str, link: str):
		data = await cls._request(
			"PUT",
			"/api/games/files/upload",
			params={"mode": "link", "link": link, "otac": otac},
		)