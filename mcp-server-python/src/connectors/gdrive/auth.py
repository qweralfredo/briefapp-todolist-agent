import os.path
import json
from google.auth.transport.requests import Request
from google.oauth2.credentials import Credentials
from google_auth_oauthlib.flow import InstalledAppFlow
from googleapiclient.discovery import build
from googleapiclient.errors import HttpError

# Escopos para leitura do Google Drive
SCOPES = ["https://www.googleapis.com/auth/drive.readonly"]

def get_gdrive_service():
    """
    Autentica e retorna o serviço do Google Drive API v3.
    Persiste o token em .token_cache/token.json.
    """
    creds = None
    # O diretório .token_cache armazena os tokens de acesso e refresh do usuário.
    # Ele é criado automaticamente na primeira execução do fluxo de autorização.
    token_dir = ".token_cache"
    token_path = os.path.join(token_dir, "token.json")
    
    if not os.path.exists(token_dir):
        os.makedirs(token_dir)

    if os.path.exists(token_path):
        creds = Credentials.from_authorized_user_file(token_path, SCOPES)
    
    # Se não houver credenciais válidas, solicita o login do usuário.
    if not creds or not creds.valid:
        if creds and creds.expired and creds.refresh_token:
            creds.refresh(Request())
        else:
            # Procura por credentials.json no diretório atual ou raiz do projeto
            # O usuário deve fornecer este arquivo (obtido no Google Cloud Console)
            creds_file = "credentials.json"
            if not os.path.exists(creds_file):
                # Tenta localizar na raiz do mcp-server-python caso esteja rodando de dentro de src
                alt_creds_file = os.path.join(os.path.dirname(__file__), "..", "..", "..", "credentials.json")
                if os.path.exists(alt_creds_file):
                    creds_file = alt_creds_file
                else:
                    raise FileNotFoundError(
                        "Arquivo credentials.json não encontrado. "
                        "Obtenha-o no Google Cloud Console e salve-o na raiz do projeto."
                    )

            flow = InstalledAppFlow.from_client_secrets_file(creds_file, SCOPES)
            # Nota: run_local_server abre o browser. Para ambientes headless, 
            # seria necessário usar console flow, mas run_local_server é o padrão para testes.
            creds = flow.run_local_server(port=0)
            
        # Salva as credenciais para a próxima execução
        with open(token_path, "w") as token:
            token.write(creds.to_json())

    try:
        service = build("drive", "v3", credentials=creds)
        return service
    except HttpError as error:
        print(f"Erro ao conectar com Google Drive: {error}")
        return None

def list_gdrive_files(folder_id: str = None, page_size: int = 10):
    """
    Lista arquivos do Google Drive.
    """
    service = get_gdrive_service()
    if not service:
        return []

    query = "'root' in parents" if not folder_id else f"'{folder_id}' in parents"
    results = (
        service.files()
        .list(q=query, pageSize=page_size, fields="nextPageToken, files(id, name, mimeType)")
        .execute()
    )
    return results.get("files", [])

if __name__ == "__main__":
    # Teste básico de conexão
    print("Iniciando teste de conexão com Google Drive...")
    try:
        service = get_gdrive_service()
        if service:
            results = (
                service.files()
                .list(pageSize=10, fields="nextPageToken, files(id, name)")
                .execute()
            )
            items = results.get("files", [])

            if not items:
                print("Nenhum arquivo encontrado.")
            else:
                print("Arquivos encontrados:")
                for item in items:
                    print(f"- {item['name']} ({item['id']})")
    except Exception as e:
        print(f"Falha no teste: {e}")
