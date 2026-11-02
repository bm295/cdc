FROM python:3.12-slim
ENV PYTHONDONTWRITEBYTECODE=1 PYTHONUNBUFFERED=1
WORKDIR /app
RUN groupadd --gid 10001 qa && useradd --uid 10001 --gid qa --no-create-home qa \
    && mkdir /data && chown qa:qa /data
COPY document_qa /app/document_qa
USER 10001:10001
EXPOSE 8090
HEALTHCHECK --interval=10s --timeout=3s --start-period=5s --retries=3 \
    CMD python -c "import urllib.request; urllib.request.urlopen('http://127.0.0.1:8090/health', timeout=2).close()"
CMD ["python", "-m", "document_qa.server", "--host", "0.0.0.0", "--port", "8090", "--db", "/data/knowledge.db"]
