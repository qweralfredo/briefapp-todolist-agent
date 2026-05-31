FROM python:3.12-slim

WORKDIR /app

COPY requirements.txt ./
RUN apt-get update && apt-get install -y git && rm -rf /var/lib/apt/lists/*
RUN pip install --no-cache-dir -r requirements.txt

COPY . .

ENV PANDORA_MCP_TRANSPORT=streamable-http
ENV PANDORA_MCP_HOST=0.0.0.0
ENV PANDORA_MCP_PORT=8000
ENV PANDORA_MCP_MOUNT_PATH=/

EXPOSE 8000

CMD ["python", "server.py"]
