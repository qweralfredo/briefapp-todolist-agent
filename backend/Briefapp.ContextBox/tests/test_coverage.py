import pytest
from unittest.mock import MagicMock, patch
from app.services.database import DatabaseService, ContextChunk
from app.services.batch_queue import BatchQueue, IngestJob

def test_database_insert_search():
    with patch("app.services.database.lancedb.connect") as mock_connect:
        mock_db = MagicMock()
        mock_table = MagicMock()
        mock_connect.return_value = mock_db
        mock_db.create_table.return_value = mock_table
        
        db_service = DatabaseService()
        db_service.db = mock_db
        db_service.table = mock_table
        
        chunk = ContextChunk(chunk_id="1", file_path="f", file_name="f", content="t", metadata_json="{}", embedding=[0.1])
        db_service.insert_chunks([chunk])
        mock_table.add.assert_called_once()
        
        mock_search = MagicMock()
        mock_table.search.return_value = mock_search
        mock_search.limit.return_value = mock_search
        mock_search.where.return_value = mock_search
        
        mock_pandas = MagicMock()
        mock_pandas.to_dict.return_value = [{"chunk_id": "1", "_distance": 0.5}]
        mock_search.to_pandas.return_value = mock_pandas
        
        res = db_service.search([0.1], limit=1, metadata_filter="test")
        assert len(res) == 1
        assert res[0]["_distance"] == 0.5

def test_database_list_delete():
    with patch("app.services.database.lancedb.connect") as mock_connect:
        mock_db = MagicMock()
        mock_table = MagicMock()
        mock_connect.return_value = mock_db
        mock_db.create_table.return_value = mock_table
        
        db_service = DatabaseService()
        db_service.db = mock_db
        db_service.table = mock_table
        
        mock_series = MagicMock()
        mock_series.unique.return_value.tolist.return_value = ["f"]
        
        mock_df = MagicMock()
        mock_df.__getitem__.return_value = mock_series
        mock_table.to_pandas.return_value = mock_df
        
        ans = db_service.list_files()
        assert len(ans) == 1
        
        db_service.delete_file("f")
        mock_table.delete.assert_called_once()

@pytest.mark.asyncio
async def test_batch_queue():
    queue = BatchQueue()
    mock_func = MagicMock()
    await queue.start(mock_func)
    
    job = IngestJob(id="uid", file_name="test.txt", file_size=10, status="pending")
    queue._jobs[job.id] = job
    
    job.status = "processing"
    assert queue.get_job(job.id).status == "processing"
    
    stats = queue.get_stats()
    assert stats["total_jobs"] == 1
    
    jobs = queue.list_jobs(status="processing")
    assert len(jobs) == 1
    
    jobs = await queue.enqueue_batch([("f.txt", b"123")])
    assert len(jobs) == 1
    assert jobs[0].file_name == "f.txt"
    await queue.stop()

