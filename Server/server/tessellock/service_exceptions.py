class ServiceException(Exception): pass

# Account duplication. Attempt to create an account with a non-unique email or name
class AccDuplSException(ServiceException): pass
class NotFoundSException(ServiceException): pass

class LoginFailedSException(ServiceException): pass
class AccNotFoundSException(LoginFailedSException): pass

class GroupNotFoundSException(NotFoundSException): pass

class AuthFailedSException(ServiceException): pass
class AccDataNotChangedSException(ServiceException): pass
class PwdIncorrectSException(ServiceException): pass
class GroupDuplSException(ServiceException): pass
class GroupDataNotChangedSException(ServiceException): pass
class GroupAddAlreadySException(ServiceException): pass
class GroupNotAddedAccSException(ServiceException): pass
class PermissionNotFoundSException(NotFoundSException): pass
class PermissionAddAlreadySException(ServiceException): pass
class PermissionNotAddedAccSException(ServiceException): pass
class GroupPermissionAddAlreadySException(ServiceException): pass
class GroupPermissionNotAddedSException(ServiceException): pass
class AccAlreadyOperSException(ServiceException): pass
class AccNotOperSException(ServiceException): pass
class AccAlreadyEnableSException(ServiceException): pass
class AccNotEnableSException(ServiceException): pass
class GroupAlreadyEnableSException(ServiceException): pass
class GroupNotEnableSException(ServiceException): pass
class InvalidId32SException(ServiceException): pass