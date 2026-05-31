"""
Batch Ingestion Router for Context-Box RAG.

Provides endpoints for submitting files for batch processing,
monitoring job status, and retrieving aggregate statistics.
"""
import json
import uuid
import logging
from typing import Optional
from fastapi import APIRouter, File, UploadFile, HTTPException, Query
from fastapi.responses import JSONResponse

from app.services.extractor import extractor
from app.services.splitter import router as splitter_router
from pathlib import Path
from app.services.embeddings import embedding_service
from app.services.database import database_service, ContextChunk
from app.services.batch_queue import batch_queue, IngestJob

logger = logging.getLogger(__name__)

router = APIRouter()


async def _process_file(
    file_name: str,
    file_content: bytes,
    job: IngestJob,
    batch_size: int,
) -> dict:
    """
    Core processing function called by batch workers.
    Implements: Extract → Split → Embed (batched) → Store.
    Updates job progress incrementally.
    """
    # 1. Extract
    if not extractor.is_supported(file_name):
        raise ValueError(f"File extension for '{file_name}' not supported.")

    extraction_result = extractor.extract(file_content, file_name)
    text_content = extraction_result["content"]
    metadata = extraction_result["metadata"]

    if not text_content or text_content.startswith("Error"):
        raise ValueError(f"Could not extract text: {text_content}")

    # 2. Split — type-aware routing
    file_extension = Path(file_name).suffix
    chunks = splitter_router.split(text_content, file_extension=file_extension, metadata=metadata)
    if not chunks:
        job.chunks_total = 0
        job.chunks_processed = 0
        return {"status": "skipped", "message": "No valid text chunks found."}

    text_chunks = [c.text for c in chunks]
    job.chunks_total = len(text_chunks)



    # 3. Embed in batches + 4. Store progressively
    for batch_start in range(0, len(text_chunks), batch_size):
        batch_end = min(batch_start + batch_size, len(text_chunks))
        batch_texts = text_chunks[batch_start:batch_end]

        # Batch embedding call to Gemini
        vectors = embedding_service.embed_texts(batch_texts)

        if len(vectors) != len(batch_texts):
            raise ValueError(
                f"Embedding mismatch: expected {len(batch_texts)}, got {len(vectors)}"
            )

        # Build chunks for this batch
        db_chunks = []
        for idx, (chunk_obj, chunk_text, vector) in enumerate(zip(
            chunks[batch_start:batch_end], batch_texts, vectors
        )):
            chunk_id = f"{file_name}-{uuid.uuid4()}-{batch_start + idx}"
            chunk_meta = {**metadata, "split_strategy": chunk_obj.strategy, **chunk_obj.metadata}
            db_chunks.append(
                ContextChunk(
                    chunk_id=chunk_id,
                    file_path=file_name,
                    file_name=file_name,
                    content=chunk_text,
                    metadata_json=json.dumps(chunk_meta),
                    embedding=vector,
                )
            )

        # Store this batch
        database_service.insert_chunks(db_chunks)

        # Update progress
        job.chunks_processed = batch_end

    return {"status": "success", "chunks": job.chunks_processed}


@router.on_event("startup")
async def _start_batch_workers():
    """Initialize the batch queue workers on app startup."""
    await batch_queue.start(_process_file)


@router.on_event("shutdown")
async def _stop_batch_workers():
    """Gracefully stop batch workers on app shutdown."""
    await batch_queue.stop()


@router.post("/ingest/batch")
async def ingest_batch(files: list[UploadFile] = File(...)):
    """
    Submit one or more files for batch RAG processing.
    Files are queued and processed asynchronously by the worker pool.
    Returns immediately with job IDs for tracking.
    """
    if not files:
        raise HTTPException(status_code=400, detail="No files provided.")

    file_pairs = []
    for f in files:
        content = await f.read()
        if not extractor.is_supported(f.filename):
            raise HTTPException(
                status_code=400,
                detail=f"Unsupported file type: '{f.filename}'",
            )
        file_pairs.append((f.filename, content))

    jobs = await batch_queue.enqueue_batch(file_pairs)

    return JSONResponse(
        status_code=202,
        content={
            "status": "accepted",
            "jobs_queued": len(jobs),
            "jobs": [j.to_dict() for j in jobs],
        },
    )


@router.get("/ingest/jobs")
async def list_jobs(
    limit: int = Query(50, ge=1, le=200),
    status: Optional[str] = Query(None, description="Filter by status: pending, processing, done, failed"),
):
    """List all ingestion jobs with optional status filter."""
    return JSONResponse(content={"jobs": batch_queue.list_jobs(limit=limit, status=status)})


@router.get("/ingest/jobs/{job_id}")
async def get_job(job_id: str):
    """Get status of a specific ingestion job."""
    job = batch_queue.get_job(job_id)
    if not job:
        raise HTTPException(status_code=404, detail=f"Job {job_id} not found.")
    return JSONResponse(content=job.to_dict())


@router.get("/ingest/stats")
async def get_stats():
    """Get aggregate statistics for the batch processing pipeline."""
    return JSONResponse(content=batch_queue.get_stats())
