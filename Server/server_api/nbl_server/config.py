import os

if os.getenv("DB_HOST"):
    db_connection = {
        "engine": "tortoise.backends.mysql",
        "credentials": {
            "host": os.getenv("DB_HOST", "db"),
            "port": int(os.getenv("DB_PORT", "3306")),
            "user": os.getenv("DB_USER", ""),
            "password": os.getenv("DB_PASSWORD", ""),
            "database": os.getenv("DB_NAME", ""),
        },
    }
else:
    db_connection = os.getenv("DB_URL", "sqlite://db.sqlite3")

TORTOISE_ORM = {
    "connections": {
        "default": db_connection,
    },
    "apps": {
        "models": {
            "models": ["nbl_server.models"],
            "default_connection": "default",
            "migrations": "nbl_server.migrations",
        },
    },
}