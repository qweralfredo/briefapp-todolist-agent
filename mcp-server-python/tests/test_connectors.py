import pytest
from unittest.mock import patch, MagicMock
import os

# Simula variáveis de ambiente para não falhar no import
os.environ["ONEDRIVE_CLIENT_ID"] = "mock_id"

from src.connectors.gdrive.auth import list_gdrive_files
from src.connectors.onedrive.auth import list_onedrive_files

@patch("src.connectors.gdrive.auth.build")
@patch("src.connectors.gdrive.auth.get_gdrive_service")
def test_list_gdrive_files_mock(mock_get_service, mock_build):
    # Mock do serviço do Google Drive
    mock_service = MagicMock()
    mock_get_service.return_value = mock_service
    mock_service.files().list().execute.return_value = {
        "files": [{"id": "123", "name": "test_file.pdf"}]
    }
    
    files = list_gdrive_files()
    assert len(files) == 1
    assert files[0]["name"] == "test_file.pdf"

@patch("src.connectors.onedrive.auth.httpx.get")
@patch("src.connectors.onedrive.auth.get_onedrive_token")
def test_list_onedrive_files_mock(mock_get_token, mock_httpx_get):
    # Mock do token e da resposta do MS Graph
    mock_get_token.return_value = "mock_token"
    mock_response = MagicMock()
    mock_response.status_code = 200
    mock_response.json.return_value = {
        "value": [{"id": "456", "name": "office_doc.docx"}]
    }
    mock_httpx_get.return_value = mock_response
    
    files = list_onedrive_files()
    assert len(files) == 1
    assert files[0]["name"] == "office_doc.docx"
