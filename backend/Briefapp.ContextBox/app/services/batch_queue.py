"""
Batch Ingestion Queue for Context-Box RAG Pipeline.

Processes file ingestions asynchronously via a worker pool.
Each job goes through: PENDING → PROCESSING → DONE | FAILED.
"""
import asyncio
import json
import uuid
import logging
import time
from dataclasses import dataclass, field, asdict
from enum import Enum
from typing import Optional
from datetime import datetime, timezone

logger = logging.getLogger(__name__)


class JobStatus(str, Enum):
    PENDING = "pending"
    PROCESSING = "processing"
    DONE = "done"
    FAILED = "failed"


@dataclass
class IngestJob:
    id: str
    file_name: str
    file_size: int
    status: JobStatus = JobStatus.PENDING
    chunks_total: int = 0
    chunks_processed: int = 0
    error: Optional[str] = None
    created_at: str = field(default_factory=lambda: datetime.now(timezone.utc).isoformat())
    started_at: Optional[str] = None
    completed_at: Optional[str] = None
    processing_time_ms: Optional[int] = None

    @property
    def progress_pct(self) -> float:
        if self.chunks_total == 0:
            return 0.0
        return round((self.chunks_processed / self.chunks_total) * 100, 1)

    def to_dict(self) -> dict:
        d = asdict(self)
        d["progress_pct"] = self.progress_pct
        return d


class BatchQueue:
    """
    In-memory async batch queue with configurable worker pool.
    
    For production, replace with PostgreSQL-backed queue (e.g. pg_boss pattern)
    or Redis + Celery. The interface remains the same.
    """

    def __init__(self, max_workers: int = 4, batch_size: int = 50):
        self.max_workers = max_workers
        self.batch_size = batch_size  # Max chunks to embed in one API call
        self._jobs: dict[str, IngestJob] = {}
        self._queue: asyncio.Queue = None
        self._workers: list[asyncio.Task] = []
        self._started = False
        self._process_fn = None  # Will be set by the router

    async def start(self, process_fn):
        """Start the worker pool with the given processing function."""
        if self._started:
            return
        self._process_fn = process_fn
        self._queue = asyncio.Queue()
        self._workers = [
            asyncio.create_task(self._worker(i))
            for i in range(self.max_workers)
        ]
        self._started = True
        logger.info(f"Batch queue started with {self.max_workers} workers, batch_size={self.batch_size}")

    async def stop(self):
        """Gracefully stop all workers."""
        if not self._started:
            return
        for _ in range(self.max_workers):
            await self._queue.put(None)  # Sentinel to stop workers
        await asyncio.gather(*self._workers, return_exceptions=True)
        self._started = False
        logger.info("Batch queue stopped.")

    async def enqueue(self, file_name: str, file_content: bytes) -> IngestJob:
        """Enqueue a file for batch processing. Returns the job immediately."""
        job = IngestJob(
            id=str(uuid.uuid4()),
            file_name=file_name,
            file_size=len(file_content),
        )
        self._jobs[job.id] = job
        await self._queue.put((job.id, file_name, file_content))
        logger.info(f"Job {job.id} enqueued for '{file_name}' ({len(file_content)} bytes)")
        return job

    async def enqueue_batch(self, files: list[tuple[str, bytes]]) -> list[IngestJob]:
        """Enqueue multiple files at once. Returns list of jobs."""
        jobs = []
        for file_name, file_content in files:
            job = await self.enqueue(file_name, file_content)
            jobs.append(job)
        return jobs

    def get_job(self, job_id: str) -> Optional[IngestJob]:
        return self._jobs.get(job_id)

    def list_jobs(self, limit: int = 50, status: Optional[str] = None) -> list[dict]:
        jobs = sorted(self._jobs.values(), key=lambda j: j.created_at, reverse=True)
        if status:
            jobs = [j for j in jobs if j.status == status]
        return [j.to_dict() for j in jobs[:limit]]

    def get_stats(self) -> dict:
        """Return aggregate stats for the batch queue."""
        all_jobs = list(self._jobs.values())
        return {
            "total_jobs": len(all_jobs),
            "pending": sum(1 for j in all_jobs if j.status == JobStatus.PENDING),
            "processing": sum(1 for j in all_jobs if j.status == JobStatus.PROCESSING),
            "done": sum(1 for j in all_jobs if j.status == JobStatus.DONE),
            "failed": sum(1 for j in all_jobs if j.status == JobStatus.FAILED),
            "total_chunks_processed": sum(j.chunks_processed for j in all_jobs),
            "avg_processing_time_ms": (
                round(
                    sum(j.processing_time_ms or 0 for j in all_jobs if j.status == JobStatus.DONE)
                    / max(1, sum(1 for j in all_jobs if j.status == JobStatus.DONE))
                )
                if any(j.status == JobStatus.DONE for j in all_jobs) else 0
            ),
            "workers_active": self.max_workers if self._started else 0,
            "batch_size": self.batch_size,
            "queue_depth": self._queue.qsize() if self._queue else 0,
        }

    async def _worker(self, worker_id: int):
        """Worker coroutine that pulls jobs from the queue and processes them."""
        logger.info(f"Worker {worker_id} started.")
        while True:
            item = await self._queue.get()
            if item is None:
                break  # Stop sentinel

            job_id, file_name, file_content = item
            job = self._jobs.get(job_id)
            if not job:
                continue

            job.status = JobStatus.PROCESSING
            job.started_at = datetime.now(timezone.utc).isoformat()
            start_time = time.monotonic()

            try:
                result = await self._process_fn(file_name, file_content, job, self.batch_size)
                job.status = JobStatus.DONE
                elapsed = int((time.monotonic() - start_time) * 1000)
                job.processing_time_ms = elapsed
                job.completed_at = datetime.now(timezone.utc).isoformat()
                logger.info(
                    f"Worker {worker_id}: Job {job_id} done — "
                    f"{job.chunks_processed} chunks in {elapsed}ms"
                )
            except Exception as e:
                job.status = JobStatus.FAILED
                job.error = str(e)
                job.completed_at = datetime.now(timezone.utc).isoformat()
                elapsed = int((time.monotonic() - start_time) * 1000)
                job.processing_time_ms = elapsed
                logger.error(f"Worker {worker_id}: Job {job_id} failed — {e}")
            finally:
                self._queue.task_done()


# Singleton instance — configure via environment
import os

batch_queue = BatchQueue(
    max_workers=int(os.getenv("RAG_WORKERS", "4")),
    batch_size=int(os.getenv("RAG_BATCH_SIZE", "50")),
)
