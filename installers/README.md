# Briefapp Box — Cross-Platform Installers

Instaladores unificados para configurar o ecosistema Briefapp em Windows e macOS/Linux.

## O que é instalado

| Feature | Windows | macOS |
|---------|---------|-------|
| **Diretório `briefapp/`** | `C:\briefapp\` | `/briefapp` ou `~/briefapp` |
| **Protocolo `briefapp://`** | Registry (HKCU) | App Bundle + LSRegister |
| **Context Menu** | Windows Explorer (arquivo, pasta, background) | Automator Quick Action (Finder) |
| **Extensões** | `.zip` em `C:\briefapp\extensions\` | `.zip` em `/briefapp/extensions/` |
| **Handler** | `egeria_handler.py` em `C:\briefapp\handler\` | `egeria_handler.py` em `/briefapp/handler/` |
| **MCP Config** | `.vscode/mcp.json` | `.vscode/mcp.json` |

## Estrutura criada

```
C:\briefapp\  (ou /briefapp)
├── extensions\
│   ├── browser-scrapper.zip
│   └── windows-context-menu.zip
├── handler\
│   ├── egeria_handler.py
│   └── .env
├── config\
└── logs\
```

## Instalação

### Windows

```powershell
# Instalação completa (recomendado: executar como Administrador)
powershell -ExecutionPolicy Bypass -File .\installers\install-briefapp.ps1

# Opções
#   -BriefappDir "D:\briefapp"      # diretório customizado
#   -McpUrl "http://host:8481/mcp" # URL MCP customizada
#   -SkipContextMenu              # pular registro de context menu
#   -SkipProtocol                 # pular registro de protocolo
#   -SkipExtensions               # pular empacotamento de extensões
#   -SkipMcp                      # pular configuração MCP VS Code
```

### macOS / Linux

```bash
# Instalação completa (requer root para /briefapp)
chmod +x ./installers/install-briefapp.sh
sudo ./installers/install-briefapp.sh

# Instalação em ~/briefapp (sem root)
./installers/install-briefapp.sh --user

# Opções
#   --mcp-url=http://host:8481/mcp  # URL MCP customizada
#   --skip-menu                     # pular Quick Action
#   --skip-proto                    # pular registro de protocolo
```

## Desinstalação

### Windows

```powershell
powershell -ExecutionPolicy Bypass -File .\installers\uninstall-briefapp.ps1

# Remover também o diretório C:\briefapp
powershell -ExecutionPolicy Bypass -File .\installers\uninstall-briefapp.ps1 -RemoveDir
```

### macOS / Linux

```bash
sudo ./installers/uninstall-briefapp.sh

# Remover também o diretório /briefapp
sudo ./installers/uninstall-briefapp.sh --remove-dir
```

## Requisitos

| Requisito | Windows | macOS |
|-----------|---------|-------|
| Python 3.8+ | ✅ (para egeria_handler) | ✅ |
| Admin/root | Recomendado (context menu + C:\briefapp) | Necessário para /briefapp |
| Docker | Para subir a stack Briefapp | Para subir a stack Briefapp |
| zip | Incluso (PowerShell Compress-Archive) | Necessário para empacotar extensões |

## Pós-instalação

1. **Subir a stack:** `docker compose up -d`
2. **Verificar MCP:** `curl http://127.0.0.1:8481/health`
3. **Recarregar IDE:** Ctrl+Shift+P → Developer: Reload Window
4. **Testar context menu:** Clique direito em um arquivo → "Briefapp Box: Send to Context-Box"
5. **Configurar `.env`:** Edite `C:\briefapp\handler\.env` com seu `PANDORA_BOX_ID`

## Skills por IDE

Após instalar o MCP, instale as skills correspondentes da sua IDE:

| IDE | Comando |
|-----|---------|
| Cursor | `Copy-Item docs\skills\cursor\*.mdc .cursor\rules\` |
| Windsurf | `Copy-Item docs\skills\windsurf\*.md .windsurf\rules\` |
| Antigravity | Copiar para `~/.gemini/antigravity/skills/` |

Veja [docs/skills/README.md](../docs/skills/README.md) para detalhes completos.
