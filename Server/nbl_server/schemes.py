from pydantic import BaseModel

class ResponseFile(BaseModel):
	checksum_sha256: str
	url: str
	path: str

class ResponseGameFilesManifest(BaseModel):
	ident: str
	files: list[ResponseFile]

class GameServerData(BaseModel):
	address: str
	query_port: int
	name: str

class GameServers(BaseModel):
	servers: list[GameServerData] = []