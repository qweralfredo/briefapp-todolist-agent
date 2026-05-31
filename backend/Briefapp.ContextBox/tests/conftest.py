import sys
from unittest.mock import MagicMock

sys.modules['google'] = MagicMock()
sys.modules['google.genai'] = MagicMock()
sys.modules['lancedb'] = MagicMock()
sys.modules['lancedb.pydantic'] = MagicMock()
sys.modules['boto3'] = MagicMock()
sys.modules['fsspec'] = MagicMock()
sys.modules['s3fs'] = MagicMock()
sys.modules['s3fs'] = MagicMock()
