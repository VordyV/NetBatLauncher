class BaseOAuth2Provider:
	ident: str
	title: str

	@staticmethod
	def validate_token(token: str) -> bool: pass

class GoogleOAuth2Provider(BaseOAuth2Provider):
	ident: str = "google"
	title: str = "Google"

	@staticmethod
	def validate_token(token: str) -> bool:
		return True