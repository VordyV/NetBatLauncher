from tortoise.models import Model
from tortoise import fields

class AccountModel(Model):
	id = fields.IntField(primary_key=True)
	uuid = fields.UUIDField(unique=True, index=True)
	name = fields.CharField(max_length=128)
	photo = fields.CharField(max_length=255, null=True)
	steam_url = fields.CharField(max_length=255, null=True)
	steam_id = fields.CharField(max_length=128, unique=True, index=True)
	created_at = fields.DatetimeField(db_default=fields.Now())
	modified = fields.DatetimeField(auto_now=True)
	is_active = fields.BooleanField()
	is_op = fields.BooleanField(default=False)

	class Meta:
		table="nbl_account"

#class GroupModel(Model):

#class AccountGroupModel(Model):

class FileModel(Model):
	id = fields.IntField(primary_key=True)
	ident = fields.CharField(max_length=64, unique=True, index=True)
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

class ManifestFilesModel(Model):
	id = fields.IntField(primary_key=True)
	file = fields.ForeignKeyField("models.FileModel")
	manifest = fields.ForeignKeyField("models.FileManifestModel")
	created_at = fields.DatetimeField(db_default=fields.Now())

	class Meta:
		table="nbl__manifest_files"

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