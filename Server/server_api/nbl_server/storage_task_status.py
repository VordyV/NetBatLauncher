from enum import IntEnum

class StorageTaskStatus(IntEnum):
	Pending = 0
	Downloading = 1
	IntegrityCheck = 2
	Extraction = 3
	UploadStorage = 4
	Done = 5
	Cancelled = 6
	Error = 7