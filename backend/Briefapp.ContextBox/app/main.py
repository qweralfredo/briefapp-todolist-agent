from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from app.core.config import settings

app = FastAPI(
    title=settings.PROJECT_NAME,
    version=settings.VERSION,
    openapi_url=f"{settings.API_V1_STR}/openapi.json"
)

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

@app.get("/health")
def health_check():
    return {"status": "ok", "service": "context-box"}

from app.routers import ingest, query, batch

app.include_router(ingest.router, prefix=settings.API_V1_STR, tags=["context-ingest"])
app.include_router(query.router, prefix=settings.API_V1_STR, tags=["context-query"])
app.include_router(batch.router, prefix=settings.API_V1_STR, tags=["context-batch"])

if __name__ == "__main__":
    import uvicorn
    uvicorn.run("app.main:app", host="0.0.0.0", port=8481, reload=True)
