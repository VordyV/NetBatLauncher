from pydantic import BaseModel

class ResponseFile(BaseModel):
	checksum_sha256: str
	url: str
	path: str
	size: int

class ResponseGameFilesManifest(BaseModel):
	ident: str
	files: list[ResponseFile]
	size: int

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
	reference: bool
	address: str | None

class GameClients(BaseModel):
	gameid: str
	clients: list[GameClient] = []

class Game(BaseModel):
	ident: str
	name: str
	short_name: str
	files: bool
	default_mod_id: str | None
	reference_client_id: str | None

class Games(BaseModel):
	games: list[Game] = []

class GameMod(BaseModel):
	modid: str
	name: str
	version: str
	developer: str
	files: bool
	default: bool

class GameMods(BaseModel):
	mods: list[GameMod]