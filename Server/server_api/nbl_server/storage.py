import traceback
from .storage_task_status import StorageTaskStatus
from .models import FileModel, FileManifestModel, StorageTaskModel
from enum import IntEnum
import os
import aiofiles
from zipfile import ZipFile
import zipfile
import asyncio
import shutil
from pathlib import Path
import httpx
import hashlib

class StorageObjectType(IntEnum):
	path = 0
	url = 1

class StorageObject:

	def __init__(self, type: StorageObjectType, value: str):
		self.type = type
		self.value = value

class StorageTask:

	def __init__(self, ident: str, task: asyncio.Task):
		self.ident = ident
		self.task = task
		self.status: StorageTaskStatus = StorageTaskStatus.Pending
		self.progress: float = 0.0

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
		raise NotImplementedError()
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
	async def sha256_file(cls, filepath: str, chunk_size: int = 1024 * 1024) -> str:

		def func():

			h = hashlib.sha256()
			with open(filepath, "rb") as f:
				while chunk := f.read(chunk_size):
					h.update(chunk)
			return h.hexdigest()

		return await asyncio.to_thread(func)

	@classmethod
	async def add_files_via_path(cls, path: str) -> str:
		uuid = os.urandom(16).hex()

		async def func():
			try:
				if not Path(path).is_dir(): raise Exception(f"There is no folder '{path}' at this path")

				storage_task.status = StorageTaskStatus.UploadStorage
				await storage_task.save()

				added_files = []
				for fp in Path(path).rglob('*'):
					if not fp.is_file(): continue
					async with aiofiles.open(str(fp.resolve()), mode='rb') as f:
						added_files.append(await cls.add(await f.read(), fp.name, str(fp.relative_to(path).as_posix()), manifest))

				storage_task.status = StorageTaskStatus.Done
				await storage_task.save()
			except asyncio.CancelledError as e:
				storage_task.error = str(e)
				storage_task.status = StorageTaskStatus.Cancelled
				await storage_task.save()
			except Exception as e:
				storage_task.error = str(e)
				storage_task.status = StorageTaskStatus.Error
				storage_task.traceback = traceback.format_exc()
				await storage_task.save()

		manifest = await FileManifestModel.create(ident=uuid)
		storage_task = await StorageTaskModel.create(ident=uuid, status=StorageTaskStatus.Pending)
		task = asyncio.create_task(func())
		cls._tasks[uuid] = StorageTask(ident=uuid, task=task)

		return uuid

	@classmethod
	async def add_files_via_link(cls, url: str) -> str:
		uuid = os.urandom(16).hex()

		async def func():

			dir = os.path.join(os.getenv("TEMP_STORAGE_PATH"), uuid)
			path = os.path.join(dir, "temp.zip")
			zip_files = os.path.join(dir, "files")

			try:
				os.makedirs(dir, exist_ok=True)

				storage_task.status = StorageTaskStatus.Downloading
				await storage_task.save()

				async with httpx.AsyncClient(timeout=None) as client:
					async with client.stream("GET", url) as response:
						response.raise_for_status()
						total = int(response.headers.get("Content-Length", 0))
						downloaded = 0
						async with aiofiles.open(path, mode='wb') as file:
							async for chunk in response.aiter_bytes(chunk_size=8192):
								downloaded += len(chunk)
								await file.write(chunk)
								cls._tasks[uuid].progress = f"{(downloaded / total):.3f}"

				storage_task.status = StorageTaskStatus.IntegrityCheck
				await storage_task.save()

				if not zipfile.is_zipfile(path):
					raise Exception("This is not a ZIP archive or the file is corrupted")

				storage_task.status = StorageTaskStatus.Extraction
				await storage_task.save()

				await asyncio.to_thread(cls._extract, path, os.path.join(dir, zip_files))

				storage_task.status = StorageTaskStatus.UploadStorage
				await storage_task.save()

				added_files = []
				for fp in Path(zip_files).rglob('*'):
					if not fp.is_file(): continue
					async with aiofiles.open(str(fp.resolve()), mode='rb') as f:
						added_files.append(await cls.add(await f.read(), fp.name, str(fp.relative_to(zip_files).as_posix()), manifest))

				storage_task.status = StorageTaskStatus.Done
				await storage_task.save()
			except asyncio.CancelledError as e:
				storage_task.error = str(e)
				storage_task.status = StorageTaskStatus.Cancelled
				await storage_task.save()
			except Exception as e:
				storage_task.error = str(e)
				storage_task.status = StorageTaskStatus.Error
				storage_task.traceback = traceback.format_exc()
				await storage_task.save()
			finally:
				if os.path.isdir(dir):
					shutil.rmtree(dir)

		manifest = await FileManifestModel.create(ident=uuid)
		storage_task = await StorageTaskModel.create(ident=uuid, status=StorageTaskStatus.Pending)
		task = asyncio.create_task(func())
		cls._tasks[uuid] = StorageTask(ident=uuid, task=task)

		return uuid

	@classmethod
	async def add(cls, data: bytes | str, filename: str, local_path: str | None, manifest: FileManifestModel | None) -> FileModel:  # str -> uuid
		uuid = os.urandom(24).hex()
		try:
			url = await cls.on_add(data, uuid)
			return await FileModel.create(ident=uuid, storage=cls.name, path=url, checksum_sha256=await cls.sha256_file(url), manifest=manifest, filename=filename, local_path=local_path)
		except Exception as e:
			await cls.on_remove(uuid)
			raise Exception(f"File 1 could not be added to storage: {e}")

	@classmethod
	async def remove(cls, ident: str) -> bool:
		file = await FileModel.get_or_none(ident=ident)

		if file and await cls.on_remove(ident):
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
	async def on_remove(cls, ident: str) -> bool:
		path = os.path.join(cls.dir, ident)
		if not os.path.exists(path): return False
		os.remove(path)
		return True