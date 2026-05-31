import pytest
from app.services.extractor import FileExtractor

def test_is_supported():
    extractor = FileExtractor()
    assert extractor.is_supported("test.py") is True
    assert extractor.is_supported("test.JSON") is True
    assert extractor.is_supported("test.csv") is True
    assert extractor.is_supported("test.unknown") is False

def test_extract_json():
    extractor = FileExtractor()
    result = extractor.extract(b'{"key": "value"}', "test.json")
    assert '"key": "value"' in result["content"]
    assert result["metadata"]["extension"] == ".json"

def test_extract_csv():
    extractor = FileExtractor()
    result = extractor.extract(b"col1,col2\nval1,val2", "test.csv")
    assert "col1,col2\nval1,val2" in result["content"]

def test_extract_binary_placeholder():
    extractor = FileExtractor()
    result = extractor.extract(b"dummy pdf bytes", "test.pdf")
    assert "[Binary extraction not fully implemented" in result["content"]

def test_extract_fallback_text():
    extractor = FileExtractor()
    result = extractor.extract(b"def main(): pass", "test.py")
    assert "def main(): pass" in result["content"]

def test_extract_json_error():
    extractor = FileExtractor()
    result = extractor.extract(b"{invalid json}", "test.json")
    assert "Error extracting" in result["content"]
    assert "error" in result["metadata"]
