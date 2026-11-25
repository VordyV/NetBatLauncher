from tortoise.models import Model
from tortoise import fields
from .models import PREFIX_TABLE_NAME
from ipaddress import IPv4Address, IPv6Address
from enum import IntEnum
from dataclasses import dataclass
from datetime import datetime

class SadapsBanMethod(IntEnum):
	IP_ADDRESS = 0
	ACCOUNT = 1

class SadapsBanlistModel(Model):
	id = fields.IntField(primary_key=True)
	ipv4_address = fields.CharField(max_length=15, null=True)
	ipv6_address = fields.CharField(max_length=39, null=True)
	account = fields.ForeignKeyField("models.AccountModel", null=True)
	method = fields.IntEnumField(SadapsBanMethod)
	reason = fields.CharField(max_length=255)
	dt_expiration = fields.DatetimeField(null=True)
	dt_created = fields.DatetimeField(db_default=fields.Now())
	dt_modified = fields.DatetimeField(auto_now=True)

	class Meta:
		table=f"{PREFIX_TABLE_NAME}_sadaps_banlist"

class Sadaps: # Suspicious Activity Detection And Prevention System

	def __init__(self, core):
		self.__core = core

	@property
	def core(self): return self.__core

	async def check_authentication(self, name_or_email: str, ip_address: IPv4Address | IPv6Address, agent: str):
		pass


