from tortoise.models import Model
from tortoise import fields
from html import escape
import uuid
import shortuuid

PREFIX_TABLE_NAME: str = "tsl"
LENGTH_SHORTUUID: int = 16

def get_shortuuid():
	return shortuuid.ShortUUID().random(length=LENGTH_SHORTUUID)

class AccountModel(Model):
	id = fields.IntField(primary_key=True)
	uuid = fields.UUIDField(default=uuid.uuid4, index=True)
	name = fields.CharField(max_length=32, index=True, unique=True)
	email = fields.CharField(max_length=255, unique=True, null=True)
	dt_created = fields.DatetimeField(db_default=fields.Now())
	dt_modified = fields.DatetimeField(auto_now=True)
	dt_modified_pwdhash = fields.DatetimeField(null=True)
	pwdhash = fields.BinaryField(null=True)
	is_op = fields.BooleanField(default=False)
	is_active = fields.BooleanField(default=True)

	class Meta:
		table=f"{PREFIX_TABLE_NAME}_account"

	def __admin_repr__(self, request) -> str: return self.name

class AccountSessionModel(Model):
	id = fields.IntField(primary_key=True)
	uuid = fields.UUIDField(default=uuid.uuid4, index=True)
	account = fields.ForeignKeyField("models.AccountModel", null=True, on_delete=fields.CASCADE)
	is_op = fields.BooleanField()
	token = fields.CharField(max_length=255)
	dt_created = fields.DatetimeField(db_default=fields.Now())
	ip_address = fields.CharField(max_length=15)
	agent = fields.CharField(max_length=128)
	last_access = fields.DatetimeField(db_default=fields.Now())

	class Meta:
		table=f"{PREFIX_TABLE_NAME}_account_session"

class PermissionGroupModel(Model):
	id = fields.IntField(primary_key=True)
	name = fields.CharField(max_length=128, unique=True)
	description = fields.CharField(max_length=128)
	uuid = fields.CharField(default=get_shortuuid, max_length=LENGTH_SHORTUUID, index=True)
	dt_created = fields.DatetimeField(db_default=fields.Now())
	dt_modified = fields.DatetimeField(auto_now=True)
	is_active = fields.BooleanField(default=True)

	class Meta:
		table=f"{PREFIX_TABLE_NAME}_permission_group"

	def __admin_repr__(self, request) -> str: return self.name
	def __admin_select2_repr__(self, request) -> str: return f"<strong>{escape(self.name)}</strong>"

class PermissionModel(Model):
	code = fields.CharField(primary_key=True, max_length=128, unique=True)
	name = fields.CharField(max_length=128)
	description = fields.CharField(max_length=128)
	dt_created = fields.DatetimeField(db_default=fields.Now())
	dt_modified = fields.DatetimeField(auto_now=True)

	class Meta:
		table=f"{PREFIX_TABLE_NAME}_permission"

	def __admin_repr__(self, request) -> str: return self.name
	def __admin_select2_repr__(self, request) -> str: return f"<strong>{self.name}</strong>"

# M:M
class GroupPermissionsModel(Model):
	group = fields.ForeignKeyField("models.PermissionGroupModel", on_delete=fields.CASCADE)
	permission = fields.ForeignKeyField("models.PermissionModel", on_delete=fields.CASCADE)
	dt_created = fields.DatetimeField(db_default=fields.Now())
	dt_expiration = fields.DatetimeField(null=True)

	class Meta:
		table=f"_{PREFIX_TABLE_NAME}_group_permissions"

# M:M
class AccountPermissionsModel(Model):
	account = fields.ForeignKeyField("models.AccountModel", on_delete=fields.CASCADE)
	permission = fields.ForeignKeyField("models.PermissionModel", on_delete=fields.CASCADE)
	dt_created = fields.DatetimeField(db_default=fields.Now())
	dt_expiration = fields.DatetimeField(null=True)

	class Meta:
		table=f"_{PREFIX_TABLE_NAME}_account_permissions"

	def __admin_repr__(self, request) -> str: return self.account_id

# M:M
class AccountGroupsModel(Model):
	account = fields.ForeignKeyField("models.AccountModel", on_delete=fields.CASCADE)
	group = fields.ForeignKeyField("models.PermissionGroupModel", on_delete=fields.CASCADE)
	dt_created = fields.DatetimeField(db_default=fields.Now())
	dt_expiration = fields.DatetimeField(null=True)

	class Meta:
		table=f"_{PREFIX_TABLE_NAME}_account_groups"

	def __admin_repr__(self, request) -> str: return self.account_id

class ChronicleModel(Model):
	id = fields.IntField(primary_key=True)
	uuid = fields.UUIDField(default=uuid.uuid4, index=True)
	dt_created = fields.DatetimeField(db_default=fields.Now())
	type = fields.IntField()
	args = fields.JSONField()

	class Meta:
		table=f"{PREFIX_TABLE_NAME}_chronicle"