class Multilingual:

	def __init__(self):
		self.__dicts: dict[str, dict[int, str]] = {}

	def add_dict(self, lang_code: str, phrases: dict[int, str]):
		self.__dicts[lang_code] = phrases

	def get_phrase(self, phrase_code: int, lang_code: str = None, default_phrase: str = "") -> str:
		return self.__dicts.get(lang_code, {}).get(phrase_code, default_phrase)

	def p(self, phrase_code: int, default_phrase: str = ""):

		def func(lang_code: str = None):
			return self.get_phrase(phrase_code, lang_code, default_phrase)

		return func