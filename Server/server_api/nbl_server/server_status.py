from enum import IntEnum

class ServerStatus(IntEnum):
	ok = 0
	maintenance = 1
	unavailable = 2
	degraded = 3