from .login_page import login_router
from .general_page import general_router
from .games_page import games_router
#from .clients_page import clients_router

routers = [
	login_router,
	general_router,
	games_router,
	#clients_router
]