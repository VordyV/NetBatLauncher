class ServiceException(Exception): pass
class NotFoundSException(ServiceException): pass
class GameNotFoundSException(NotFoundSException): pass
class GameClientNotFoundSException(NotFoundSException): pass
class GameModNotFoundSException(NotFoundSException): pass
class ManifestNotFoundSException(NotFoundSException): pass