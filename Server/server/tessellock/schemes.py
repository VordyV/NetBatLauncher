from typing import Annotated
from pydantic import BaseModel, Field, EmailStr
from datetime import datetime

NameField = Annotated[str, Field(min_length=2, max_length=32)]
PasswordField = Annotated[str, Field(min_length=12, max_length=64, pattern=r"^[\x20-\x7E]*$")]
NameOrEmailField = Annotated[NameField | EmailStr, Field(max_length=255, pattern=r"^[\x20-\x7E]*$")]
AccountIdField = Annotated[str, Field(max_length=32, min_length=32, pattern=r"^[\x20-\x7E]*$")]
DescriptionField = Annotated[str, Field(max_length=128)]
GroupIdField = Annotated[str, Field(max_length=16, min_length=16, pattern=r"^[\x20-\x7E]*$")]
PermCodeField = Annotated[str, Field(max_length=128, pattern=r"^[\x20-\x7E]*$")]
PermNameField = Annotated[str, Field(min_length=2, max_length=128)]

class RegisterRequest(BaseModel):
	name: NameField
	email: EmailStr
	password: PasswordField

class LoginRequest(BaseModel):
	name_or_email: NameOrEmailField
	password: PasswordField

class AccountSession(BaseModel):
	access_token: str

class Account(BaseModel):
	id: str
	name: str
	email: str | None
	dt_created: datetime
	dt_modified: datetime
	dt_modified_pwdhash: datetime | None
	is_op: bool
	is_active: bool

class AccountUpdateRequest(BaseModel):
	name: NameField | None = None
	email: EmailStr | None = None

class ChangePasswordRequest(BaseModel):
	password: PasswordField
	new_password: PasswordField

class Group(BaseModel):
	id: str
	name: str
	description: str
	dt_created: datetime
	dt_modified: datetime
	is_active: bool

class GroupCreateRequest(BaseModel):
	name: NameField
	is_active: bool
	description: DescriptionField

class GroupUpdateRequest(BaseModel):
	name: NameField | None = None
	description: DescriptionField | None = None

class Permission(BaseModel):
	code: PermCodeField
	name: PermNameField
	description: DescriptionField
	dt_created: datetime | None = None
	dt_modified: datetime | None = None

class GroupB(BaseModel):
	id: str
	name: str
	description: str

class AccountPermissions(BaseModel):
	account_id: str
	permissions: list[Permission]