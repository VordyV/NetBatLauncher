from .storage_task_status import StorageTaskStatus
from tortoise.models import Model
from tortoise import fields

class FileModel(Model):
	id = fields.IntField(primary_key=True)
	ident = fields.CharField(max_length=64, unique=True, index=True)
	filename = fields.CharField(max_length=128)
	local_path = fields.CharField(max_length=255, null=True)
	manifest = fields.ForeignKeyField("models.FileManifestModel", null=True)
	checksum_sha256 = fields.CharField(max_length=255)
	storage = fields.CharField(max_length=64)
	path = fields.CharField(max_length=255)
	created_at = fields.DatetimeField(db_default=fields.Now())

	class Meta:
		table="nbl_file"

class FileManifestModel(Model):
	id = fields.IntField(primary_key=True)
	ident = fields.CharField(max_length=64, unique=True, index=True)
	created_at = fields.DatetimeField(db_default=fields.Now())

	class Meta:
		table="nbl_file_manifest"

class GameModel(Model):
	id = fields.IntField(primary_key=True)
	ident = fields.CharField(max_length=64, unique=True, index=True)
	name = fields.CharField(max_length=128)
	short_name = fields.CharField(max_length=64)
	created_at = fields.DatetimeField(db_default=fields.Now())
	modified = fields.DatetimeField(auto_now=True)
	file_manifest = fields.ForeignKeyField("models.FileManifestModel", null=True)

	class Meta:
		table="nbl_game"

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
	game = fields.ForeignKeyField("models.GameModel")
	ident = fields.CharField(max_length=64, unique=True, index=True)
	name = fields.CharField(max_length=128)
	short_name = fields.CharField(max_length=64)
	created_at = fields.DatetimeField(db_default=fields.Now())
	modified = fields.DatetimeField(auto_now=True)
	file_manifest = fields.ForeignKeyField("models.FileManifestModel", null=True)
	master_server_address = fields.CharField(max_length=64, null=True)
	master_server_enctypex_key = fields.CharField(max_length=64, null=True)

	class Meta:
		table="nbl_game_client"

class GameServerModel(Model):
	id = fields.IntField(primary_key=True)
	ident = fields.CharField(max_length=64, unique=True, index=True)
	client = fields.ForeignKeyField("models.GameClientModel")
	created_at = fields.DatetimeField(db_default=fields.Now())
	modified = fields.DatetimeField(auto_now=True)
	address = fields.CharField(max_length=15)
	query_port = fields.IntField()
	name = fields.CharField(max_length=128)

	class Meta:
		table="nbl_game_server"