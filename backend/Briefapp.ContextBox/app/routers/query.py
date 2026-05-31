import logging
from typing import Optional
from pydantic import BaseModel
from fastapi import APIRouter, HTTPException
from fastapi.responses import JSONResponse

from app.services.database import database_service
from app.services.embeddings import embedding_service

logger = logging.getLogger(__name__)

router = APIRouter()

class QueryRequest(BaseModel):
    query: str
    limit: int = 10
    file_type: Optional[str] = None
    # Add other metadata filters as needed

@router.post("/query")
async def semantic_search(request: QueryRequest):
    """
    Executes a semantic search over the vectorized chunks.
    """
    try:
        # 1. Embed query
        vectors = embedding_service.embed_texts([request.query])
        if not vectors:
            raise HTTPException(status_code=500, detail="Failed to embed query.")
        query_vector = vectors[0]

        # 2. Build metadata filter
        # In LanceDB, you can filter using standard SQL WHERE strings
        metadata_filter = None
        if request.file_type:
            # We map extension checking. We stored the file name, 
            # so we can use SQL LIKE or direct string manipulation in lancedb
            # e.g., file_path LIKE '%.pdf'
            metadata_filter = f"file_path LIKE '%{request.file_type}'"

        # 3. Search
        search_results = database_service.search(query_vector, limit=request.limit, metadata_filter=metadata_filter)
        
        # 4. Format results
        # search_results includes `_distance` automatically mapped by LanceDB
        formatted_results = []
        for r in search_results:
            formatted_results.append({
                "chunk_id": r.get("chunk_id"),
                "file_path": r.get("file_path"),
                "content": r.get("content"),
                "score": r.get("_distance"), # Distance metric (0.0 is exact match, higher is further)
                "metadata": r.get("metadata_json")
            })

        return JSONResponse(content={
            "query": request.query,
            "results": formatted_results
        })

    except Exception as e:
        logger.error(f"Search failed: {e}")
        raise HTTPException(status_code=500, detail=str(e))

@router.get("/files")
async def list_files():
    """
    Lists unique files indexed.
    """
    try:
        files = database_service.list_files()
        return files
    except Exception as e:
        logger.error(f"List files failed: {e}")
        raise HTTPException(status_code=500, detail=str(e))

@router.delete("/files/{file_id:path}")
async def delete_file(file_id: str):
    """
    Deletes a file and all its chunks from the index.
    """
    try:
        database_service.delete_file(file_id)
        return JSONResponse(content={"status": "success", "deleted_file": file_id})
    except Exception as e:
        logger.error(f"Delete file failed: {e}")
        raise HTTPException(status_code=500, detail=str(e))
