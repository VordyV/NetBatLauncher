import os
import uuid

from .storage_task_status import StorageTaskStatus
from .mfds_status import MFDSStatus
from tortoise.models import Model
from tortoise import fields

class FileModel(Model):
	id = fields.IntField(primary_key=True)
	ident = fields.CharField(max_length=64, unique=True, index=True)
	filename = fields.CharField(max_length=128)
	local_path = fields.CharField(max_length=255, null=True)
	manifest = fields.ForeignKeyField("models.FileManifestModel", null=True, on_delete=fields.NO_ACTION, related_name="files")
	checksum_sha256 = fields.CharField(max_length=255)
	storage = fields.CharField(max_length=64)
	path = fields.CharField(max_length=255)
	created_at = fields.DatetimeField(db_default=fields.Now())
	size = fields.IntField()

	class Meta:
		table="nbl_file"

	def __admin_repr__(self, request) -> str: return self.ident

class FileManifestModel(Model):
	id = fields.IntField(primary_key=True)
	ident = fields.CharField(max_length=64, unique=True, index=True)
	created_at = fields.DatetimeField(db_default=fields.Now())
	files: fields.ReverseRelation["FileModel"]

	class Meta:
		table="nbl_file_manifest"

	def __admin_repr__(self, request) -> str: return self.ident

class GameModel(Model):
	id = fields.IntField(primary_key=True)
	ident = fields.CharField(max_length=64, unique=True, index=True)
	name = fields.CharField(max_length=128)
	short_name = fields.CharField(max_length=64)
	created_at = fields.DatetimeField(db_default=fields.Now())
	modified = fields.DatetimeField(auto_now=True)
	file_manifest = fields.ForeignKeyField("models.FileManifestModel", null=True, on_delete=fields.NO_ACTION)

	class Meta:
		table="nbl_game"

	def __admin_repr__(self, request) -> str: return self.name

class StorageTaskModel(Model):
	id = fields.IntField(primary_key=True)
	ident = fields.CharField(max_length=64, unique=True, index=True)
	status = fields.IntEnumField(StorageTaskStatus)
	error = fields.CharField(max_length=255, null=True)
	traceback = fields.TextField(null=True)

	class Meta:
		table="nbl_storage_task"

class GameClientModel(Model):
	id = fields.IntField(primary_key=True)
	game = fields.ForeignKeyField("models.GameModel", on_delete=fields.CASCADE)
	ident = fields.CharField(max_length=64, index=True)
	name = fields.CharField(max_length=128)
	short_name = fields.CharField(max_length=64)
	created_at = fields.DatetimeField(db_default=fields.Now())
	modified = fields.DatetimeField(auto_now=True)
	file_manifest = fields.ForeignKeyField("models.FileManifestModel", null=True, on_delete=fields.NO_ACTION)
	master_server_address = fields.CharField(max_length=64, null=True)
	master_server_enctypex_key = fields.CharField(max_length=64, null=True)
	reference = fields.BooleanField(default=False)

	class Meta:
		table="nbl_game_client"

	async def __admin_repr__(self, request) -> str:
		await self.fetch_related("game")
		return f"{self.game.name} - {self.name}"

class GameServerModel(Model):
	id = fields.IntField(primary_key=True)
	ident = fields.CharField(max_length=64, index=True)
	client = fields.ForeignKeyField("models.GameClientModel", on_delete=fields.CASCADE)
	created_at = fields.DatetimeField(db_default=fields.Now())
	modified = fields.DatetimeField(auto_now=True)
	address = fields.CharField(max_length=15)
	query_port = fields.IntField()
	name = fields.CharField(max_length=128)

	class Meta:
		table="nbl_game_server"

class GameServerDataModel(Model):
	id = fields.IntField(primary_key=True)
	client = fields.ForeignKeyField("models.GameClientModel", on_delete=fields.CASCADE)
	created_at = fields.DatetimeField(db_default=fields.Now())
	address = fields.CharField(max_length=15)
	query_port = fields.IntField()
	name = fields.CharField(max_length=128, null=True)
	fields = fields.JSONField(default={})

	class Meta:
		table="nbl_game_server_data"

class GameModModel(Model):
	id = fields.IntField(primary_key=True)
	ident = fields.CharField(max_length=64, index=True)
	game = fields.ForeignKeyField("models.GameModel", on_delete=fields.CASCADE)
	name = fields.CharField(max_length=128)
	version = fields.CharField(max_length=16)
	developer = fields.CharField(max_length=128)
	created_at = fields.DatetimeField(db_default=fields.Now())
	modified = fields.DatetimeField(auto_now=True)
	default = fields.BooleanField(default=False)
	file_manifest = fields.ForeignKeyField("models.FileManifestModel", null=True, on_delete=fields.NO_ACTION)

	class Meta:
		table="nbl_game_mod"

class AccountModel(Model):
	id = fields.IntField(primary_key=True)
	ident = fields.UUIDField(unique=True, default=uuid.uuid4, index=True)
	name = fields.CharField(max_length=128)
	photo_url = fields.CharField(max_length=255, null=True)
	steam_url = fields.CharField(max_length=255, null=True)
	steam_id = fields.CharField(max_length=128, unique=True, index=True, null=True)
	created_at = fields.DatetimeField(db_default=fields.Now())
	modified = fields.DatetimeField(auto_now=True)
	active = fields.BooleanField(default=True)
	op = fields.BooleanField(default=False)
	permissions = fields.JSONField(default=[])
	password = fields.CharField(max_length=255, null=True)

	class Meta:
		table="nbl_account"

	def __admin_repr__(self, request) -> str: return f"{self.name}"

def get_token(): return os.urandom(32).hex()

class SessionAccountModel(Model):
	id = fields.IntField(primary_key=True)
	account = fields.ForeignKeyField("models.AccountModel", on_delete=fields.CASCADE)
	token = fields.CharField(max_length=64, default=get_token, index=True)
	created_at = fields.DatetimeField(db_default=fields.Now())
	address = fields.CharField(max_length=15)
	agent = fields.CharField(max_length=128)

	class Meta:
		table="nbl_session_account"

	async def __admin_repr__(self, request) -> str:
		await self.fetch_related("account")
		return f"{self.account.name}"

class ManifestFileDownloadSession(Model):
	id = fields.IntField(primary_key=True)
	ident = fields.UUIDField(unique=True, default=uuid.uuid4, index=True)
	manifest = fields.ForeignKeyField("models.FileManifestModel", null=True, on_delete=fields.NO_ACTION)
	completed = fields.BooleanField()
	created_at = fields.DatetimeField(db_default=fields.Now())
	completed_on = fields.DatetimeField(null=True)
	country = fields.CharField(max_length=64, null=True)
	city = fields.CharField(max_length=64, null=True)
	possible_vpn = fields.BooleanField()
	client_version = fields.CharField(max_length=8, null=True)
	client_id = fields.CharField(max_length=32, null=True)
	os = fields.CharField(max_length=16, null=True)