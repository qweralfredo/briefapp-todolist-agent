# Briefapp Box — Windows Context Menu Extension

## Requisitos
- Python 3.8+ (sem dependências externas — só stdlib)
- Windows 10/11

## Instalação

1. **Configure o `.env`** (criado automaticamente no install):
   ```
   PANDORA_MCP_URL=http://76.13.238.113:8481/mcp
   PANDORA_BOX_ID=<uuid-do-seu-box>
   PANDORA_API_KEY=<sua-key-opcional>
   MAX_FILE_SIZE_MB=50
   ```

2. **Execute `install.bat`** como Administrador  
   (o script pede elevação automaticamente se necessário)

## Como usar

Após a instalação, clique com o botão direito em:
- **Qualquer arquivo** → `Briefapp Box: Enviar para Context-Box`
- **Qualquer pasta** → `Briefapp Box: Enviar pasta para Context-Box`  
  *(envia todos os arquivos do nível raiz da pasta)*

## Tipos de arquivo suportados

| Tipo | Extensões | Processamento |
|------|-----------|---------------|
| Texto / Código | `.txt, .md, .py, .js, .ts, .cs, .go...` | Leitura completa como texto |
| Dados | `.json, .yaml, .csv, .xml, .toml` | Leitura completa |
| Documentos | `.pdf, .doc, .docx, .odt` | Leitura de texto (best-effort) |
| Imagens | `.png, .jpg, .svg...` | Metadados + hash sha256 |
| Binários | outros | Hash sha256 + metadados |

**Limite padrão**: 50 MB por arquivo (configurável em `.env`)

## Log

Operações ficam em `%APPDATA%\Briefapp\dispatch.log`

## Desinstalar

Execute `uninstall.bat` — remove todas as entradas do Registry.
