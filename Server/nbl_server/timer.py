import asyncio
import datetime
from typing import Callable

class TimerAsync:

	def __init__(self, arg: object = None):
		self.__arg = arg
		self.__task = None

	def run(self, delay: datetime.timedelta, callback: Callable):
		self.cancel()

		async def _runner():
			try:
				await asyncio.sleep(delay.total_seconds())
				await callback(self.__arg)
			except asyncio.CancelledError:
				raise

		self.__task = asyncio.create_task(_runner())

	def cancel(self):
		if self.__task and not self.__task.done():
			self.__task.cancel()
		self.__task = None