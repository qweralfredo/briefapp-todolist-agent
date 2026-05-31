import json
import logging
import lancedb
from lancedb.pydantic import LanceModel, Vector
from app.core.config import settings

logger = logging.getLogger(__name__)

# schema definition for LanceDB mapping
class ContextChunk(LanceModel):
    chunk_id: str
    file_path: str
    file_name: str
    content: str
    metadata_json: str
    # Vector dimensionality depends on the embedder. 
    # gemini-embedding-2-preview is 3072 dimensions.
    embedding: Vector(3072)

class DatabaseService:
    def __init__(self):
        # We need to set S3 endpoint override for MinIO
        self.uri = f"s3://{settings.MINIO_BUCKET}/"
        
        # S3 storage options specific for MinIO compatibility
        self.storage_options = {
            "endpoint_url": settings.MINIO_URL,
            "aws_access_key_id": settings.MINIO_ACCESS_KEY,
            "aws_secret_access_key": settings.MINIO_SECRET_KEY,
            "region_name": "us-east-1",  # dummy region for MinIO
            "allow_http": "true"         # required if MINIO_URL is http
        }

        self.table_name = "context_chunks"
        self._db = None
        self._table = None

    def _connect(self):
        if not self._db:
            logger.info(f"Connecting to LanceDB at {self.uri} with MinIO at {settings.MINIO_URL}")
            # connect using fsspec backed by s3fs
            self._db = lancedb.connect(self.uri, storage_options=self.storage_options)

    def get_table(self):
        self._connect()
        if self.table_name not in self._db.table_names():
            logger.info(f"Creating table '{self.table_name}' in LanceDB...")
            self._table = self._db.create_table(self.table_name, schema=ContextChunk)
        elif not self._table:
            self._table = self._db.open_table(self.table_name)
            
        return self._table

    def insert_chunks(self, chunks: list[ContextChunk]):
        table = self.get_table()
        table.add(chunks)
        
    def delete_file(self, file_path: str):
        table = self.get_table()
        # LanceDB delete syntax: double quotes or single quotes depending on SQL compliance
        table.delete(f"file_path = '{file_path}'")

    def search(self, vector: list[float], limit: int = 10, metadata_filter: str = None):
        """
        Executes semantic search against LanceDB vector index.
        """
        table = self.get_table()
        query = table.search(vector).limit(limit)
        
        if metadata_filter:
            query = query.where(metadata_filter)
            
        # Execute query and convert to list of dicts
        # .to_list() returns dictionaries natively mapped
        result_df = query.to_pandas()
        return result_df.to_dict(orient="records")

    def list_files(self):
        """
        Lists unique files currently in the index.
        For simplicity in LanceDB, we group by file_name using pandas.
        """
        table = self.get_table()
        df = table.to_pandas()
        
        if df.empty:
            return []
            
        # Group by file_name and count chunks
        stats = df.groupby(["file_path", "file_name"]).size().reset_index(name="chunks")
        return stats.to_dict(orient="records")

database_service = DatabaseService()
