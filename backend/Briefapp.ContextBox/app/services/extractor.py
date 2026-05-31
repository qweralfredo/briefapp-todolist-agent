import json
import logging
from typing import Dict, Any, Optional
from pathlib import Path
import csv

logger = logging.getLogger(__name__)

class FileExtractor:
    """
    Extracts text and metadata from 30+ file types.
    Supported types: PDF, DOCX, XLSX, CSV, TXT, MD, JSON, YAML, HTML, XML, 
    PY, JS, TS, TSX, JSX, CSS, SQL, GO, RS, CPP, C, H, JAVA, KT, RB, PHP, SH, BAT, PS1, LOG
    """

    SUPPORTED_EXTENSIONS = {
        ".pdf", ".docx", ".xlsx", ".csv", ".txt", ".md", ".json", ".yaml", ".yml",
        ".html", ".xml", ".py", ".js", ".ts", ".tsx", ".jsx", ".css", ".sql", ".go",
        ".rs", ".cpp", ".c", ".h", ".java", ".kt", ".rb", ".php", ".sh", ".bat", ".ps1", ".log"
    }

    def __init__(self):
        # In a full implementation, we'd use PDFPlumber for PDFs, openpyxl for XLSX, etc.
        # For MVP Context-Box, we fallback to UTF-8 text decoding for code/logs,
        # and simple structure mapping for JSON/CSV.
        pass

    def is_supported(self, file_path: str) -> bool:
        path = Path(file_path)
        return path.suffix.lower() in self.SUPPORTED_EXTENSIONS

    def extract(self, file_content: bytes, file_name: str) -> Dict[str, Any]:
        """
        Extracts textual content from bytes based on file format.
        Returns a dict with 'content' and 'metadata'.
        """
        path = Path(file_name)
        ext = path.suffix.lower()

        metadata = {
            "source": file_name,
            "extension": ext,
            "size_bytes": len(file_content)
        }

        content = ""

        try:
            if ext == ".json":
                data = json.loads(file_content.decode("utf-8"))
                content = json.dumps(data, indent=2)
            
            elif ext == ".csv":
                text = file_content.decode("utf-8")
                # Normalize line endings and get content
                content = text.strip()
            
            elif ext in {".pdf", ".docx", ".xlsx"}:
                # TODO: Implement binary parsers (pdfplumber, python-docx, openpyxl)
                # Currently returning placeholder text to be handled by TDD 
                content = "[Binary extraction not fully implemented yet in MVP]"
            
            else:
                # Fallback for TXT, MD, Code files, Logs, XML, HTML, etc (Text based files)
                # Will attempt utf-8 degradation safely
                content = file_content.decode("utf-8", errors="replace")

        except Exception as e:
            logger.error(f"Extraction failed for {file_name}: {e}")
            metadata["error"] = str(e)
            content = f"Error extracting {ext} file: {e}"

        return {
            "content": content,
            "metadata": metadata
        }

extractor = FileExtractor()
