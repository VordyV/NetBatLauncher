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
	name: str | None
	clientid: str

class GameServers(BaseModel):
	servers: list[GameServerData] = []

class GameClient(BaseModel):
	ident: str
	name: str
	short_name: str
	files: bool

class GameClients(BaseModel):
	gameid: str
	clients: list[GameClient] = []

class Game(BaseModel):
	ident: str
	name: str
	short_name: str
	files: bool

class Games(BaseModel):
	games: list[Game] = []