FROM python:3.12-slim
WORKDIR /app
COPY server.py schema.sql ./
COPY static ./static
ENV BJA_DATA_DIR=/data BJA_TRUST_PROXY=1
VOLUME /data
EXPOSE 8080
# 처음 한 번:  docker compose run --rm bja-logs python server.py init-admin --username admin
CMD ["python", "server.py", "serve", "--host", "0.0.0.0", "--port", "8080"]
