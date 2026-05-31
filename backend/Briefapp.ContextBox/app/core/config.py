from pydantic_settings import BaseSettings

class Settings(BaseSettings):
    PROJECT_NAME: str = "Briefapp Context-Box API"
    VERSION: str = "1.0.0"
    API_V1_STR: str = "/api/context"
    
    # MinIO / S3 Configuration
    MINIO_URL: str = "http://localhost:9100"
    MINIO_ACCESS_KEY: str = "briefapp"
    MINIO_SECRET_KEY: str = "briefapp-secret"
    MINIO_BUCKET: str = "briefapp-context"

    # Gemini API
    GEMINI_API_KEY: str = ""

    # Batch Processing
    RAG_WORKERS: int = 4
    RAG_BATCH_SIZE: int = 50

    class Config:
        case_sensitive = True

settings = Settings()
