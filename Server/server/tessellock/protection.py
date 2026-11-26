from .schemes import *
import functools

class Protection:

	def __init__(self):
		self._permissions: dict[str, Permission] = {}

	def register_permission(self, code: str, name: str, description: str):
		code = self._get_code(code)

		if code in self._permissions: raise Exception(f"The permission '{code}' is already registered")
		self._permissions[code] = Permission(code=code, name=name, description=description)

	def _get_code(self, code: str) -> str:
		return code.strip().replace(" ","_").lower()