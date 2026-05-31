import os
import json
import msal
import httpx

# Configurações do Microsoft Graph
# Estes valores devem ser configurados no Azure Portal (App Registration)
CLIENT_ID = os.getenv("ONEDRIVE_CLIENT_ID")
TENANT_ID = os.getenv("ONEDRIVE_TENANT_ID", "common")  # 'common' para contas pessoais e corporativas
AUTHORITY = f"https://login.microsoftonline.com/{TENANT_ID}"
SCOPES = ["Files.Read", "User.Read"]

TOKEN_FILE = ".onedrive_token.json"

def get_onedrive_token():
    """
    Obtém um token de acesso para o OneDrive via MSAL.
    Usa cache local e Device Code Flow se necessário.
    """
    # Verifica se o diretório de destino existe (caso TOKEN_FILE tenha caminho)
    token_dir = os.path.dirname(TOKEN_FILE)
    if token_dir and not os.path.exists(token_dir):
        os.makedirs(token_dir)

    app = msal.PublicClientApplication(
        CLIENT_ID or "DUMMY_CLIENT_ID", # Fallback para evitar erro de inicialização se vazio
        authority=AUTHORITY,
        token_cache=msal.SerializableTokenCache()
    )

    # Tenta carregar o cache do arquivo
    if os.path.exists(TOKEN_FILE):
        try:
            with open(TOKEN_FILE, "r") as f:
                app.token_cache.deserialize(f.read())
        except Exception as e:
            print(f"Erro ao carregar cache de token: {e}")

    # Tenta obter token silenciosamente de contas já logadas no cache
    accounts = app.get_accounts()
    result = None
    if accounts:
        result = app.acquire_token_silent(SCOPES, account=accounts[0])

    if not result:
        # Se falhar ou não houver cache, inicia o Device Code Flow
        if not CLIENT_ID:
            raise ValueError(
                "ONEDRIVE_CLIENT_ID não configurado. "
                "Configure a variável de ambiente para realizar o login."
            )
            
        print("Iniciando Device Code Flow para OneDrive...")
        flow = app.initiate_device_flow(scopes=SCOPES)
        if "user_code" not in flow:
            raise ValueError(f"Não foi possível iniciar o device flow: {flow}")

        # Exibe a mensagem com o código para o usuário autenticar em outro dispositivo/aba
        print("-" * 50)
        print(flow["message"])
        print("-" * 50)
        
        result = app.acquire_token_by_device_flow(flow)

    if result and "access_token" in result:
        # Salva o estado atualizado do cache
        if app.token_cache.has_state_changed:
            with open(TOKEN_FILE, "w") as f:
                f.write(app.token_cache.serialize())
        return result["access_token"]
    else:
        error_msg = result.get('error_description') or result.get('error') if result else "Resultado vazio"
        raise ValueError(f"Falha na autenticação OneDrive: {error_msg}")

def list_onedrive_files(folder_id: str = None):
    """
    Lista arquivos do OneDrive usando a API Microsoft Graph.
    """
    token = get_onedrive_token()
    headers = {"Authorization": f"Bearer {token}"}
    
    # Se folder_id não for fornecido, usa a raiz do drive
    url = "https://graph.microsoft.com/v1.0/me/drive/root/children"
    if folder_id:
        url = f"https://graph.microsoft.com/v1.0/me/drive/items/{folder_id}/children"
        
    response = httpx.get(url, headers=headers)
    if response.status_code == 200:
        return response.json().get("value", [])
    else:
        print(f"Erro ao listar arquivos do OneDrive: {response.status_code}")
        return []

if __name__ == "__main__":
    print("Iniciando teste de autenticação OneDrive...")
    try:
        # Nota: O teste real requer ONEDRIVE_CLIENT_ID válido
        token = get_onedrive_token()
        print("Token obtido com sucesso!")
        
        # Teste básico chamando a API do Microsoft Graph
        headers = {"Authorization": f"Bearer {token}"}
        response = httpx.get("https://graph.microsoft.com/v1.0/me/drive", headers=headers)
        
        if response.status_code == 200:
            drive_data = response.json()
            user_name = drive_data.get('owner', {}).get('user', {}).get('displayName', 'Usuário Desconhecido')
            print(f"OneDrive conectado com sucesso! Bem-vindo, {user_name}.")
            print(f"Drive ID: {drive_data.get('id')}")
        else:
            print(f"Erro ao acessar API do OneDrive: {response.status_code}")
            print(response.text)
            
    except ValueError as ve:
        print(f"Erro de configuração/autenticação: {ve}")
    except Exception as e:
        print(f"Erro inesperado no teste: {e}")
