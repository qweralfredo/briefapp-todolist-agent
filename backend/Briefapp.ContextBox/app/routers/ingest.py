from fastapi import APIRouter, File, UploadFile, HTTPException
from fastapi.responses import JSONResponse
from pydantic import BaseModel
import uuid
import json

from app.services.extractor import extractor
from app.services.splitter import router as splitter_router
from pathlib import Path
from app.services.embeddings import embedding_service
from app.services.database import database_service, ContextChunk

router = APIRouter()

@router.post("/ingest")
async def ingest_file(file: UploadFile = File(...)):
    """
    Ingests a file, extracts text, splits it, embeds chunks, and stores into LanceDB.
    """
    try:
        content_bytes = await file.read()
        file_name = file.filename
        
        if not extractor.is_supported(file_name):
            raise HTTPException(status_code=400, detail=f"File extension for '{file_name}' not supported.")

        # 1. Extract
        extraction_result = extractor.extract(content_bytes, file_name)
        text_content = extraction_result["content"]
        metadata = extraction_result["metadata"]
        
        if not text_content or text_content.startswith("Error"):
            raise HTTPException(status_code=422, detail=f"Could not extract text: {text_content}")

        # 2. Split — routed per file type
        file_extension = Path(file_name).suffix
        chunks = splitter_router.split(text_content, file_extension=file_extension, metadata=metadata)
        if not chunks:
            return JSONResponse(content={"status": "skipped", "message": "No valid text chunks found."})
        text_chunks = [c.text for c in chunks]

        # 3. Embed
        # Batch embedding execution
        try:
            vectors = embedding_service.embed_texts(text_chunks)
        except Exception as e:
            raise HTTPException(status_code=500, detail=f"Embedding service failed: {str(e)}")

        if len(vectors) != len(text_chunks):
            raise HTTPException(status_code=500, detail="Mismatch between chunks and generated vectors.")

        # 4. Store
        db_chunks = []
        metadata_json = json.dumps(metadata)
        
        for idx, (chunk_obj, chunk_text, vector) in enumerate(zip(chunks, text_chunks, vectors)):
            chunk_id = f"{file_name}-{uuid.uuid4()}-{idx}"
            # Merge split strategy metadata into the stored metadata
            chunk_meta = {**metadata, "split_strategy": chunk_obj.strategy, **chunk_obj.metadata}
            db_chunks.append(ContextChunk(
                chunk_id=chunk_id,
                file_path=file_name,
                file_name=file_name,
                content=chunk_text,
                metadata_json=json.dumps(chunk_meta),
                embedding=vector
            ))

        database_service.insert_chunks(db_chunks)

        return JSONResponse(status_code=201, content={
            "status": "success",
            "file_name": file_name,
            "chunks_processed": len(db_chunks)
        })
    except HTTPException as e:
        raise e
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Internal server error: {str(e)}")

class JsonIngestRequest(BaseModel):
    content: str
    source: str = "web"
    metadata: dict = {}

@router.post("/ingest/json")
async def ingest_json(request: JsonIngestRequest):
    """
    Direct JSON ingestion for browser extensions and quick notes.
    """
    try:
        text_content = request.content
        metadata = request.metadata
        source = request.source
        
        # Enriquecer metadata
        metadata["ingest_source"] = source
        file_name = metadata.get("title", f"web-ingest-{uuid.uuid4().hex[:8]}")

        # Split
        chunks = splitter_router.split(text_content, file_extension=".md", metadata=metadata)
        if not chunks:
            return JSONResponse(content={"status": "skipped", "message": "No valid text chunks found."})
        
        text_chunks = [c.text for c in chunks]
        vectors = embedding_service.embed_texts(text_chunks)

        db_chunks = []
        for idx, (chunk_obj, chunk_text, vector) in enumerate(zip(chunks, text_chunks, vectors)):
            chunk_id = f"json-{uuid.uuid4()}-{idx}"
            chunk_meta = {**metadata, "split_strategy": chunk_obj.strategy}
            db_chunks.append(ContextChunk(
                chunk_id=chunk_id,
                file_path=file_name,
                file_name=file_name,
                content=chunk_text,
                metadata_json=json.dumps(chunk_meta),
                embedding=vector
            ))

        database_service.insert_chunks(db_chunks)

        return JSONResponse(status_code=201, content={
            "status": "success",
            "chunks_processed": len(db_chunks)
        })

    except Exception as e:
        raise HTTPException(status_code=500, detail=f"JSON Ingest failed: {str(e)}")
