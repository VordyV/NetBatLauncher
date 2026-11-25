import pydantic

class Settings(pydantic.BaseModel):
	DEFAULT_GROUP: str = ""