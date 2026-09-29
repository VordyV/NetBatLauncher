FROM python:3.14.7-alpine

WORKDIR /app

RUN pip install --upgrade pip && pip install --no-cache-dir poetry

COPY pyproject.toml poetry.lock* ./

RUN poetry install --no-root

COPY . .

ENV PYTHONUNBUFFERED=1

CMD ["poetry", "run", "python", "main.py"]