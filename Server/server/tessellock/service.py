from __future__ import annotations
from typing import TYPE_CHECKING
if TYPE_CHECKING: from .core import Tessellock
from .service_exceptions import *
from tortoise.models import Model
from abc import ABC
import functools
import traceback

def service_exception_handler(exception: type[Exception], out_exception: type[Exception]):

	def decorator_repeat(func):

		@functools.wraps(func)
		async def wrapper(*args, **kwargs):
			try:
				return await func(*args, **kwargs)
			except exception as e:
				raise out_exception()
			except Exception as e:
				print(e)
				traceback.print_exc()
				raise ServiceException()

		return wrapper

	return decorator_repeat

class Service(ABC):

	model: Model

	def __init__(self, core: Tessellock):
		self.core = core

	async def _create(self, **fields) -> Model:
		return await self.model.create(**fields)

	async def _get(self, field: str, value: object, null: bool = False) -> Model | None:
		record = await self.model.get_or_none(**{field: value})
		if not record and not null: raise NotFoundSException(0, f"Record {field} = {value} not found")
		return record

	async def get_from_id(self, ident: int | str):
		if isinstance(ident, str):
			return await self._get("uuid", ident)
		elif isinstance(ident, int):
			return await self._get("id", ident)

	async def _remove(self, field: str, value: object):
		record = self.get(field, value)
		await record.delete()

	async def _update(self, field: str, value: object, **updatable_data):
		record = self.get(field, value)
		for k, v in updatable_data.items():
			if not hasattr(record, k): continue
			setattr(record, k, v)
		await record.save()

	async def _exists(self, field: str, value: object) -> bool: return await self.model.exists(**{field: value})