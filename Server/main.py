from dotenv import load_dotenv
load_dotenv()
from nbl_server import NetBatLauncherServer
import os
import uvicorn
import asyncio

def app():
	nbls = NetBatLauncherServer(steam_api_key=os.getenv("STEAM_API_KEY"))
	return nbls.app