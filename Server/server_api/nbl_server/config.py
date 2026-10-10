import os

TORTOISE_ORM = {
    "connections": {
        "default": os.getenv("DB_URL", "sqlite://db.sqlite3"),
    },
    "apps": {
        "models": {
            "models": ["nbl_server.models"],
            "default_connection": "default",
            "migrations": "nbl_server.migrations",
        },
    },
}