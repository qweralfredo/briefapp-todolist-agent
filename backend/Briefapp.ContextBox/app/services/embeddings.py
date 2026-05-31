import os
import logging
import requests
from typing import List
from app.core.config import settings

logger = logging.getLogger(__name__)

class EmbeddingService:
    def __init__(self):
        self.api_key = settings.GEMINI_API_KEY or os.environ.get("GEMINI_API_KEY")
        self.model = "gemini-embedding-2-preview" # Default fallback, adjust as needed

        if not self.api_key:
            logger.warning("No GEMINI_API_KEY provided. Embeddings will fail.")

    def embed_texts(self, texts: List[str]) -> List[List[float]]:
        """
        Embeds a list of texts using Gemini REST API.
        Returns a list of vectors (List of floats).
        """
        if not self.api_key:
            raise ValueError("Gemini API key is missing.")
            
        if not texts:
            return []

        vectors = []
        # Gemini API does not natively batch well on free tier for embedContent, 
        # but there is batchEmbedContent. We will use batchEmbedContent.
        
        url = f"https://generativelanguage.googleapis.com/v1beta/models/{self.model}:batchEmbedContents?key={self.api_key}"
        
        requests_payload = [{"model": f"models/{self.model}", "content": {"parts": [{"text": text}]}} for text in texts]
        
        payload = {
            "requests": requests_payload
        }
        
        headers = {
            "Content-Type": "application/json"
        }

        try:
            response = requests.post(url, json=payload, headers=headers)
            response.raise_for_status()
            
            data = response.json()
            # Extract embeddings
            for emb in data.get("embeddings", []):
                vectors.append(emb["values"])
                
            return vectors

        except requests.exceptions.HTTPError as e:
            logger.error(f"HTTPError generating embeddings: {e.response.text}")
            raise RuntimeError(f"Error from Gemini API: {e.response.text}")
        except Exception as e:
            logger.error(f"Error generating embeddings via REST: {e}")
            raise e

embedding_service = EmbeddingService()
