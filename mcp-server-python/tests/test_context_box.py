import pytest
from unittest.mock import patch, MagicMock
import os
import urllib.parse
from server import (
    get_modification_impact,
    context_box_ingest,
    context_box_query,
    context_box_list,
    context_box_delete,
    context_box_ingest_batch,
    context_box_batch_status,
    context_box_batch_stats,
    mcp_wl_spawn,
    mcp_wl_stop,
    mcp_wl_status,
    mcp_wl_registry,
    mcp_wl_registry_stats,
    ApiError,
    resource_context_rag
)

@patch("server._request")
@patch("server.git_graph_service.analyze_impact")
def test_get_modification_impact(mock_analyze, mock_request):
    mock_request.return_value = [{"id": "fb", "localPath": "/tmp/project"}]
    mock_analyze.return_value = {
        "co_modified_files": [{"file": "a.py", "frequency": 2}],
        "historically_related_workitems": ["wi-1"]
    }
    result = get_modification_impact("fb", "main.py")
    assert "a.py" in result
    assert "wi-1" in result

@patch("server._request")
@patch("server.git_graph_service.analyze_impact")
def test_get_modification_impact_no_project(mock_analyze, mock_request):
    mock_request.return_value = []
    with pytest.raises(ApiError):
        get_modification_impact("unknown", "main.py")

@patch("server._request")
@patch("server.git_graph_service.analyze_impact")
def test_get_modification_impact_error(mock_analyze, mock_request):
    mock_request.return_value = [{"id": "fb", "localPath": "/tmp/project"}]
    mock_analyze.return_value = {"error": "Graph parsing error"}
    result = get_modification_impact("fb", "main.py")
    assert result == "Graph parsing error"

@patch("server._request")
@patch("server.git_graph_service.analyze_impact")
def test_get_modification_impact_no_coupling(mock_analyze, mock_request):
    mock_request.return_value = [{"id": "fb", "localPath": "/tmp/project"}]
    mock_analyze.return_value = {
        "co_modified_files": [],
        "historically_related_workitems": []
    }
    result = get_modification_impact("fb", "main.py")
    assert "No temporal coupling found." in result
    assert "No correlated WorkItems found" in result

@patch("server.httpx.Client.request")
def test_resource_context_rag(mock_request):
    mock_response = MagicMock()
    mock_response.status_code = 200
    mock_response.content = b'true'
    mock_response.json.return_value = [{"file": "test.txt"}]
    mock_request.return_value = mock_response
    assert "test.txt" in resource_context_rag("box-id")

@patch("server.httpx.Client.post")
@patch("os.path.exists")
def test_context_box_ingest(mock_exists, mock_post):
    mock_exists.return_value = True
    mock_response = MagicMock()
    mock_response.status_code = 200
    mock_response.json.return_value = {"success": True}
    mock_post.return_value = mock_response
    
    with patch("builtins.open", MagicMock()):
        result = context_box_ingest("file.txt")
    assert result["success"]

@patch("server.httpx.Client.post")
@patch("os.path.exists")
def test_context_box_ingest_error(mock_exists, mock_post):
    mock_exists.return_value = True
    mock_response = MagicMock()
    mock_response.status_code = 500
    mock_response.text = "Server Error"
    mock_post.return_value = mock_response
    
    with patch("builtins.open", MagicMock()):
        with pytest.raises(ApiError):
            context_box_ingest("file.txt")

@patch("os.path.exists")
def test_context_box_ingest_not_found(mock_exists):
    mock_exists.return_value = False
    with pytest.raises(ApiError):
        context_box_ingest("unknown.txt")

@patch("server._context_request")
def test_context_box_query(mock_req):
    mock_req.return_value = {"matches": []}
    result = context_box_query("test", limit=5, file_type="pdf")
    assert result == {"matches": []}
    mock_req.assert_called_with("POST", "/query", payload={"query": "test", "limit": 5, "file_type": "pdf"})

@patch("server._context_request")
def test_context_box_list(mock_req):
    mock_req.return_value = []
    assert context_box_list() == []
    mock_req.assert_called_with("GET", "/files")

@patch("server._context_request")
def test_context_box_delete(mock_req):
    mock_req.return_value = {"deleted": True}
    assert context_box_delete("file.txt") == {"deleted": True}
    mock_req.assert_called_with("DELETE", "/files/file.txt")

@patch("server.httpx.Client.post")
@patch("os.path.exists")
def test_context_box_ingest_batch(mock_exists, mock_post):
    mock_exists.return_value = True
    mock_response = MagicMock()
    mock_response.status_code = 200
    mock_response.json.return_value = {"job_id": "123"}
    mock_post.return_value = mock_response
    
    with patch("builtins.open", MagicMock()):
        result = context_box_ingest_batch(["file1.txt", "file2.txt"])
    assert result["job_id"] == "123"

@patch("server.httpx.Client.post")
@patch("os.path.exists")
def test_context_box_ingest_batch_error(mock_exists, mock_post):
    mock_exists.return_value = True
    mock_response = MagicMock()
    mock_response.status_code = 400
    mock_response.text = "Error"
    mock_post.return_value = mock_response
    
    with patch("builtins.open", MagicMock()):
        with pytest.raises(ApiError):
            context_box_ingest_batch(["file1.txt"])

@patch("os.path.exists")
def test_context_box_ingest_batch_not_found(mock_exists):
    mock_exists.side_effect = [True, False]
    with patch("builtins.open", MagicMock()):
        with pytest.raises(ApiError):
            context_box_ingest_batch(["file1.txt", "file2.txt"])

@patch("server._context_request")
def test_context_box_batch_status(mock_req):
    mock_req.return_value = {}
    context_box_batch_status()
    mock_req.assert_called_with("GET", "/ingest/jobs")
    context_box_batch_status("123")
    mock_req.assert_called_with("GET", "/ingest/jobs/123")

@patch("server._context_request")
def test_context_box_batch_stats(mock_req):
    mock_req.return_value = {"total_jobs": 0}
    assert context_box_batch_stats() == {"total_jobs": 0}
    mock_req.assert_called_with("GET", "/ingest/stats")

@patch("server._wl_request")
def test_mcp_wl_spawn(mock_req):
    mock_req.return_value = {"port": 1234}
    assert mcp_wl_spawn("box-1", "My Box", "key1") == {"port": 1234}
    mock_req.assert_called_with("POST", "/api/boxes/box-1/mcp/spawn", payload={"box_name": "My Box", "api_key": "key1"})

@patch("server._wl_request")
def test_mcp_wl_stop(mock_req):
    mock_req.return_value = {"stopped": True}
    assert mcp_wl_stop("box-1") == {"stopped": True}
    mock_req.assert_called_with("DELETE", "/api/boxes/box-1/mcp/stop")

@patch("server._wl_request")
def test_mcp_wl_status(mock_req):
    mock_req.return_value = {"health": "ok"}
    assert mcp_wl_status("box-1") == {"health": "ok"}
    mock_req.assert_called_with("GET", "/api/boxes/box-1/mcp/status")

@patch("server._wl_request")
def test_mcp_wl_registry(mock_req):
    mock_req.return_value = []
    assert mcp_wl_registry() == []
    mock_req.assert_called_with("GET", "/api/mcp-registry")

@patch("server._wl_request")
def test_mcp_wl_registry_stats(mock_req):
    mock_req.return_value = {"running": 1}
    assert mcp_wl_registry_stats() == {"running": 1}
    mock_req.assert_called_with("GET", "/api/mcp-registry/stats")

@patch("server.httpx.Client.request")
def test_wl_request_helper_success(mock_request):
    from server import _wl_request
    mock_response = MagicMock()
    mock_response.status_code = 200
    mock_response.content = b'true'
    mock_response.json.return_value = {"success": True}
    mock_request.return_value = mock_response
    assert _wl_request("GET", "/test") == {"success": True}

@patch("server.httpx.Client.request")
def test_wl_request_helper_error(mock_request):
    from server import _wl_request
    mock_response = MagicMock()
    mock_response.status_code = 404
    mock_response.json.side_effect = ValueError
    mock_response.text = "Not Found"
    mock_request.return_value = mock_response
    with pytest.raises(ApiError):
        _wl_request("GET", "/test")

@patch("server.httpx.Client.request")
def test_context_request_helper_error(mock_request):
    from server import _context_request
    mock_response = MagicMock()
    mock_response.status_code = 400
    mock_response.json.side_effect = ValueError
    mock_response.text = "Bad Request"
    mock_request.return_value = mock_response
    with pytest.raises(ApiError):
        _context_request("GET", "/test")
