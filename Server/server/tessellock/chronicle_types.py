from pydantic import BaseModel


class ChronicleTypeArg(BaseModel):
	type: int
	initiator: str | int

class RegisterChronicleType(ChronicleTypeArg):
	type: int = 1
	name: str
	email: str

class CreateSessionChronicleType(ChronicleTypeArg):
	type: int = 2
	account_id: str
	session_id: str
	ip_address: str
	agent: str

class DeleteSessionChronicleType(ChronicleTypeArg):
	type: int = 3
	session_id: str

class UpdateAccChronicleType(ChronicleTypeArg):
	type: int = 4
	account_id: str
	new_name: str | None
	new_email: str | None

class DeleteAccChronicleType(ChronicleTypeArg):
	type: int = 5
	account_id: str

class ChangePwdAccChronicleType(ChronicleTypeArg):
	type: int = 6
	account_id: str

class AddGroupAccChronicleType(ChronicleTypeArg):
	type: int = 7
	account_id: str
	group_id: str
	expiration: float | None

class RemoveGroupAccChronicleType(ChronicleTypeArg):
	type: int = 8
	account_id: str
	group_id: str

class AddPermAccChronicleType(ChronicleTypeArg):
	type: int = 9
	account_id: str
	permission_code: str
	expiration: float | None

class RemovePermAccChronicleType(ChronicleTypeArg):
	type: int = 10
	account_id: str
	permission_code: str

class MakeOpAccChronicleType(ChronicleTypeArg):
	type: int = 11
	account_id: str

class RemoveOpAccChronicleType(ChronicleTypeArg):
	type: int = 12
	account_id: str

class EnableAccChronicleType(ChronicleTypeArg):
	type: int = 13
	account_id: str

class DisableAccChronicleType(ChronicleTypeArg):
	type: int = 14
	account_id: str

class CreateGrChronicleType(ChronicleTypeArg):
	type: int = 15
	group_id: str
	name: str
	description: str

class UpdateGrChronicleType(ChronicleTypeArg):
	type: int = 16
	group_id: str
	new_name: str | None
	new_description: str | None

class DeleteGrChronicleType(ChronicleTypeArg):
	type: int = 17
	group_id: str

class AddPermGrChronicleType(ChronicleTypeArg):
	type: int = 18
	group_id: str
	expiration: float | None

class RemovePermGrChronicleType(ChronicleTypeArg):
	type: int = 19
	group_id: str

class EnableGrChronicleType(ChronicleTypeArg):
	type: int = 20
	group_id: str

class DisableGrChronicleType(ChronicleTypeArg):
	type: int = 21
	group_id: str