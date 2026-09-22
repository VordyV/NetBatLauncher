import traceback

from .models import FileModel
from enum import Enum
import os
import aiofiles
from zipfile import ZipFile
import zipfile
import asyncio
import shutil
from pathlib import Path
import httpx

class StorageObjectType(Enum):
	path = 0
	url = 1

class StorageObject:

	def __init__(self, type: StorageObjectType, value: str):
		self.type = type
		self.value = value

class StorageTaskStatus(Enum):
	Pending = 0
	Downloading = 1
	IntegrityCheck = 2
	Extraction = 3
	UploadStorage = 4
	Done = 5
	Cancelled = 6
	Error = 7

class StorageTask:

	def __init__(self, ident: str, task: asyncio.Task):
		self.ident = ident
		self.task = task
		self.status: StorageTaskStatus = StorageTaskStatus.Pending
		self.error: Exception | None = None

class Storage:

	name: str
	object_type: StorageObjectType = StorageObjectType.path
	_tasks: dict[str, StorageTask] = {}

	@classmethod
	def get_task(cls, ident: str) -> StorageTask | None:
		return cls._tasks.get(ident)

	@classmethod
	def _extract(cls, path_sender: str, path_target):
		with ZipFile(path_sender, "r") as myzip:
			myzip.extractall(path=path_target)

	@classmethod
	async def add_files(cls, archive_data: bytes): # archive_data accepts the bytes of the zip archive file
		uuid = os.urandom(16).hex()
		dir = os.path.join(os.getenv("TEMP_STORAGE_PATH"), uuid)
		path = os.path.join(dir, "temp.zip")
		zip_files = os.path.join(dir, "files")

		try:

			os.makedirs(dir, exist_ok=True)
			async with aiofiles.open(path, mode='wb') as file:
				await file.write(archive_data)

			if not zipfile.is_zipfile(path):
				print("This is not a ZIP archive or the file is corrupted")
				raise Exception("This is not a ZIP archive or the file is corrupted")


			#print(zipfile.is_zipfile(path))

			#await asyncio.to_thread(cls._extract, path, os.path.join(dir, zip_files))

			#for file in Path(zip_files).rglob('*'):
			#	print(file)
		finally:
			if os.path.isdir(dir): pass
				#shutil.rmtree(dir)

	@classmethod
	async def add_files_via_link(cls, url: str) -> str:
		uuid = os.urandom(16).hex()

		async def func():

			dir = os.path.join(os.getenv("TEMP_STORAGE_PATH"), uuid)
			path = os.path.join(dir, "temp.zip")
			zip_files = os.path.join(dir, "files")

			try:
				os.makedirs(dir, exist_ok=True)

				cls._tasks[uuid].status = StorageTaskStatus.Downloading

				async with httpx.AsyncClient(timeout=None) as client:
					async with client.stream("GET", url) as response:
						response.raise_for_status()
						total = int(response.headers.get("Content-Length", 0))
						downloaded = 0
						async with aiofiles.open(path, mode='wb') as file:
							async for chunk in response.aiter_bytes(chunk_size=8192):
								downloaded += len(chunk)
								await file.write(chunk)
								percent = downloaded / total * 100
								print(f"\r{percent:6.2f}%  ({downloaded}/{total} b)", end="", flush=True)

				cls._tasks[uuid].status = StorageTaskStatus.IntegrityCheck

				if not zipfile.is_zipfile(path):
					raise Exception("This is not a ZIP archive or the file is corrupted")

				cls._tasks[uuid].status = StorageTaskStatus.Extraction

				await asyncio.to_thread(cls._extract, path, os.path.join(dir, zip_files))

				cls._tasks[uuid].status = StorageTaskStatus.UploadStorage
				for file in Path(zip_files).rglob('*'):
					if not file.is_file(): continue
					async with aiofiles.open(str(file.resolve()), mode='rb') as file:
						await cls.add(await file.read())

				cls._tasks[uuid].status = StorageTaskStatus.Done
			except asyncio.CancelledError as e:
				cls._tasks[uuid].error = e
				cls._tasks[uuid].status = StorageTaskStatus.Cancelled
			except Exception as e:
				cls._tasks[uuid].error = e
				cls._tasks[uuid].status = StorageTaskStatus.Error
			finally:
				if os.path.isdir(dir):
					shutil.rmtree(dir)

		task = asyncio.create_task(func())
		cls._tasks[uuid] = StorageTask(ident=uuid, task=task)

		return uuid

	@classmethod
	async def add(cls, data: bytes | str) -> str:  # str -> uuid
		uuid = os.urandom(16).hex()
		try:
			url = await cls.on_add(data, uuid)
			await FileModel.create(ident=uuid, storage=cls.name, path=url, checksum_sha256="1")
		except Exception as e:
			await cls.on_remove(uuid)
			raise

		return uuid

	@classmethod
	async def get_url(cls, uuid: str) -> StorageObject | None: # str -> url
		file = await FileModel.get_or_none(uuid=uuid)
		if not file: return None
		return StorageObject(type=cls.object_type, value=file.path)

	@classmethod
	async def remove(cls, uuid: str) -> bool:
		file = await FileModel.get_or_none(uuid=uuid)

		if file and await cls.on_remove(uuid):
			await file.delete()
			return True

		return False

	@classmethod
	async def on_add(cls, data: bytes, uuid: str) -> str: pass # str -> url
	@classmethod
	async def on_remove(cls, uuid: str) -> bool: pass

class LocalStorage(Storage):

	name: str = "local"
	object_type: StorageObjectType = StorageObjectType.path
	dir: str = os.getenv("LOCAL_STORAGE")

	@classmethod
	async def on_add(cls, data: bytes, uuid: str) -> str:
		path = os.path.join(cls.dir, uuid)
		async with aiofiles.open(path, mode='wb') as file:
			await file.write(data)
		return path

	@classmethod
	async def on_remove(cls, uuid: str) -> bool:
		path = os.path.join(cls.dir, uuid)
		if not os.path.exists(path): return False
		os.remove(path)
		return True