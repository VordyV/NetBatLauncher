import datetime
from pydantic import BaseModel, Field

class ResponseExchange(BaseModel):
	access_token: str

class ResponseRefresh(ResponseExchange): pass

class ResponseAccountData(BaseModel):
	id: str
	name: str
	photo: str | None
	steam_url: str | None
	created_at: datetime.datetime

class RequestGameCreate(BaseModel):
	ident: str = Field(max_length=64, min_length=1)
	name: str = Field(max_length=128, min_length=1)
	short_name: str = Field(max_length=64, min_length=1)

class ResponseGamesRead(RequestGameCreate):
	created_at: datetime.datetime
	modified: datetime.datetime

class ResponseGamesReadAll(BaseModel):
	games: list[ResponseGamesRead]

class RequestGameUpdate(RequestGameCreate): pass

class ResponseGameFilesUpload(BaseModel):
	otac: str

class ResponseFile(BaseModel):
	ident: str
	checksum_sha256: str
	url: str

class ResponseGameFilesManifest(BaseModel):
	ident: str
	files: list[ResponseFile]

class ResponseGameFilesUploadProcess(BaseModel):
	task_ident: str

class ResponseStorageTask(ResponseGameFilesUploadProcess):
	status: str
	error: str | None
	exception: str | None