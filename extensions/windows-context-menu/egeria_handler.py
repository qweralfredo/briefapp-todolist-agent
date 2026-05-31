"""
egeria_handler.py — Briefapp File Dispatcher (T7: dispatcher + T8: toast UX)

Recebe arquivos pelo menu de contexto do Windows Explorer e os envia para o
Briefapp Box (Context-Box) via API MCP (context_box_ingest).

Funcionalidades:
- Suporte a múltiplos arquivos (drag & drop + multi-seleção no Explorer)
- Dispatcher: detecta tipo de arquivo e roteia para endpoint correto
- Toast notifications nativas do Windows (via ctypes, sem dependências extras)
- Barra de progresso no terminal para uploads grandes
- Log de operações em %APPDATA%\Briefapp\dispatch.log
"""

import sys
import os
import json
import urllib.request
import urllib.error
import time
import ctypes
import ctypes.wintypes
import logging
import hashlib
from pathlib import Path
from datetime import datetime

# ─── Configuração ──────────────────────────────────────────────────────────────

def load_config() -> dict:
    """Carrega .env manual (sem dependências externas)."""
    env_path = Path(__file__).parent / '.env'
    config = {
        'PANDORA_MCP_URL':   'http://127.0.0.1:8481/mcp',
        'PANDORA_BOX_ID':    '',
        'PANDORA_API_KEY':   '',
        'MAX_FILE_SIZE_MB':  '50',
    }
    if env_path.exists():
        for line in env_path.read_text(encoding='utf-8').splitlines():
            line = line.strip()
            if line and not line.startswith('#') and '=' in line:
                k, _, v = line.partition('=')
                config[k.strip()] = v.strip().strip('"').strip("'")
    return config


CFG = load_config()
MCP_URL       = CFG['PANDORA_MCP_URL']
BOX_ID        = CFG['PANDORA_BOX_ID']
API_KEY       = CFG['PANDORA_API_KEY']
MAX_BYTES     = int(CFG['MAX_FILE_SIZE_MB']) * 1024 * 1024

# ─── Logging ───────────────────────────────────────────────────────────────────

LOG_DIR = Path(os.environ.get('APPDATA', os.path.expanduser('~'))) / 'Briefapp'
LOG_DIR.mkdir(parents=True, exist_ok=True)
logging.basicConfig(
    filename=str(LOG_DIR / 'dispatch.log'),
    level=logging.INFO,
    format='%(asctime)s [%(levelname)s] %(message)s',
    datefmt='%Y-%m-%d %H:%M:%S',
)
log = logging.getLogger('briefapp')

# ─── Toast Notifications via ctypes (sem win11toast) ──────────────────────────

def show_toast(title: str, message: str, icon_type: int = 0x40) -> None:
    """
    Exibe toast nativo do Windows usando MessageBeep + Shell_NotifyIcon via ctypes.
    Fallback: usa ctypes.windll.user32.MessageBoxW se a API de notificação falhar.
    icon_type: 0x40 = info, 0x30 = warning, 0x10 = error
    """
    try:
        # MB_ICONINFORMATION = 0x40, MB_ICONWARNING = 0x30, MB_ICONERROR = 0x10
        # Usamos MessageBox em modo silencioso como fallback simples e confiável
        ctypes.windll.user32.MessageBeep(0xFFFFFFFF)  # beep simples

        # --- Shell_NotifyIcon (tray notification) ---
        NIF_MESSAGE  = 0x01
        NIF_ICON     = 0x02
        NIF_TIP      = 0x04
        NIF_INFO     = 0x10
        NIM_ADD      = 0x00
        NIM_MODIFY   = 0x01
        NIM_DELETE   = 0x02
        NIIF_INFO    = 0x01
        NIIF_WARNING = 0x02
        NIIF_ERROR   = 0x03

        class NOTIFYICONDATA(ctypes.Structure):
            _fields_ = [
                ('cbSize',           ctypes.wintypes.DWORD),
                ('hWnd',             ctypes.wintypes.HWND),
                ('uID',              ctypes.wintypes.UINT),
                ('uFlags',           ctypes.wintypes.UINT),
                ('uCallbackMessage', ctypes.wintypes.UINT),
                ('hIcon',            ctypes.wintypes.HANDLE),
                ('szTip',            ctypes.c_wchar * 128),
                ('dwState',          ctypes.wintypes.DWORD),
                ('dwStateMask',      ctypes.wintypes.DWORD),
                ('szInfo',           ctypes.c_wchar * 256),
                ('uTimeout',         ctypes.wintypes.UINT),
                ('szInfoTitle',      ctypes.c_wchar * 64),
                ('dwInfoFlags',      ctypes.wintypes.DWORD),
            ]

        shell32 = ctypes.windll.shell32
        user32  = ctypes.windll.user32

        hwnd = user32.GetForegroundWindow() or 0
        hicon = shell32.ExtractIconW(0, 'shell32.dll', 15)  # ícone info padrão

        nid = NOTIFYICONDATA()
        nid.cbSize       = ctypes.sizeof(NOTIFYICONDATA)
        nid.hWnd         = hwnd
        nid.uID          = 9999
        nid.uFlags       = NIF_ICON | NIF_TIP | NIF_INFO
        nid.hIcon        = hicon
        nid.szTip        = 'Briefapp Box'
        nid.szInfo       = message[:255]
        nid.szInfoTitle  = title[:63]
        nid.uTimeout     = 5000
        nid.dwInfoFlags  = NIIF_INFO if icon_type == 0x40 else (NIIF_ERROR if icon_type == 0x10 else NIIF_WARNING)

        shell32.Shell_NotifyIconW(NIM_ADD, ctypes.byref(nid))
        time.sleep(0.1)
        shell32.Shell_NotifyIconW(NIM_DELETE, ctypes.byref(nid))

    except Exception as e:
        log.warning(f'Toast falhou (ctypes): {e}')
        # Fallback absoluto: MessageBox visível
        try:
            ctypes.windll.user32.MessageBoxW(
                0, message, f'Briefapp Box — {title}', icon_type | 0x1000
            )
        except Exception:
            pass

# ─── File Dispatcher (T7) ──────────────────────────────────────────────────────

# Mapeamento de extensão → tipo semântico para o dispatcher
EXTENSION_MAP = {
    # Documentos
    '.pdf': 'document', '.doc': 'document', '.docx': 'document',
    '.odt': 'document', '.rtf': 'document',
    # Texto / Código
    '.txt': 'text', '.md': 'text', '.rst': 'text',
    '.py': 'code', '.js': 'code', '.ts': 'code', '.cs': 'code',
    '.go': 'code', '.rs': 'code', '.java': 'code', '.cpp': 'code',
    '.json': 'data', '.yaml': 'data', '.yml': 'data', '.toml': 'data',
    '.csv': 'data', '.xml': 'data',
    # Imagens (metadados apenas)
    '.png': 'image', '.jpg': 'image', '.jpeg': 'image',
    '.gif': 'image', '.svg': 'image', '.webp': 'image',
}

TEXT_TYPES = {'text', 'code', 'data', 'document'}

def dispatch_file(file_path: Path) -> dict:
    """
    Dispatcher: lê o arquivo, decide como processar e envia ao Briefapp Box.
    Retorna {'success': bool, 'message': str, 'chars': int}.
    """
    ext    = file_path.suffix.lower()
    ftype  = EXTENSION_MAP.get(ext, 'binary')
    size   = file_path.stat().st_size
    sha256 = hashlib.sha256(file_path.read_bytes()).hexdigest()[:12]

    if size > MAX_BYTES:
        return {
            'success': False,
            'message': f'Arquivo muito grande ({size / 1024 / 1024:.1f} MB > {MAX_BYTES // 1024 // 1024} MB limite)',
        }

    if ftype in TEXT_TYPES:
        # Lê como texto com detecção de encoding
        for enc in ('utf-8', 'latin-1', 'cp1252'):
            try:
                content = file_path.read_text(encoding=enc)
                break
            except UnicodeDecodeError:
                continue
        else:
            content = file_path.read_bytes().decode('utf-8', errors='replace')
    elif ftype == 'image':
        content = f'[Imagem: {file_path.name} | {size} bytes | sha256:{sha256}]'
    else:
        content = f'[Arquivo binário: {file_path.name} | {size} bytes | tipo:{ext} | sha256:{sha256}]'

    # Handshake MCP
    session_id = _mcp_initialize()

    # Chama context_box_ingest
    payload = {
        'jsonrpc': '2.0',
        'id': 2,
        'method': 'tools/call',
        'params': {
            'name': 'context_box_ingest_raw',
            'arguments': {
                'content':  content[:50000],  # cap 50k chars
                'metadata': {
                    'box_id': BOX_ID,
                    'source': 'windows_context_menu',
                    'file_name': file_path.name,
                    'file_type': ftype,
                    'file_size': size,
                    'sha256':    sha256,
                    'ingested_at': datetime.now().isoformat(),
                }
            }
        }
    }

    headers = {
        'Content-Type': 'application/json',
        'Accept': 'application/json, text/event-stream',
    }
    if session_id:
        headers['Mcp-Session-Id'] = session_id
    if API_KEY:
        headers['X-Briefapp-Api-Key'] = API_KEY

    body = json.dumps(payload).encode('utf-8')
    req  = urllib.request.Request(MCP_URL, data=body, headers=headers, method='POST')

    try:
        with urllib.request.urlopen(req, timeout=60) as resp:
            raw = resp.read().decode('utf-8').strip()
            # SSE parse
            if any(l.startswith('data:') for l in raw.splitlines()):
                raw = '\n'.join(l[5:].strip() for l in raw.splitlines() if l.startswith('data:'))
            result = json.loads(raw)
            if 'error' in result:
                return {'success': False, 'message': str(result['error'])}
            return {'success': True, 'message': 'OK', 'chars': len(content)}
    except urllib.error.HTTPError as e:
        return {'success': False, 'message': f'HTTP {e.code}: {e.read().decode()[:100]}'}
    except Exception as e:
        return {'success': False, 'message': str(e)}


def _mcp_initialize() -> str | None:
    """Executa handshake MCP initialize e retorna session_id."""
    payload = {
        'jsonrpc': '2.0', 'id': 1, 'method': 'initialize',
        'params': {
            'protocolVersion': '2024-11-05',
            'capabilities': {},
            'clientInfo': {'name': 'briefapp-windows-extension', 'version': '2.0.0'},
        }
    }
    headers = {'Content-Type': 'application/json', 'Accept': 'application/json, text/event-stream'}
    body = json.dumps(payload).encode('utf-8')
    req  = urllib.request.Request(MCP_URL, data=body, headers=headers, method='POST')
    try:
        with urllib.request.urlopen(req, timeout=15) as resp:
            sid = resp.headers.get('Mcp-Session-Id')
            # Envia initialized notification
            notif_payload = {'jsonrpc': '2.0', 'method': 'notifications/initialized', 'params': {}}
            notif_headers = dict(headers)
            if sid:
                notif_headers['Mcp-Session-Id'] = sid
            notif_req = urllib.request.Request(MCP_URL,
                data=json.dumps(notif_payload).encode(),
                headers=notif_headers, method='POST')
            try:
                urllib.request.urlopen(notif_req, timeout=5)
            except Exception:
                pass
            return sid
    except Exception:
        return None

# ─── Progresso no terminal (T8 UX) ────────────────────────────────────────────

def safe_print(*args, **kwargs):
    try:
        print(*args, **kwargs)
    except Exception:
        pass

def print_progress(current: int, total: int, width: int = 30) -> None:
    pct  = current / total if total else 1
    done = int(pct * width)
    bar  = 'M' * done + '.' * (width - done)
    safe_print(f'\r  [{bar}] {current}/{total}', end='', flush=True)

# ─── Main ──────────────────────────────────────────────────────────────────────

def main():
    files = [Path(p) for p in sys.argv[1:] if Path(p).exists() and Path(p).is_file()]
    dirs  = [Path(p) for p in sys.argv[1:] if Path(p).exists() and Path(p).is_dir()]

    # Expande diretórios (nível 1)
    for d in dirs:
        files += [f for f in d.iterdir() if f.is_file()]

    if not files:
        safe_print('Nenhum arquivo válido fornecido.')
        show_toast('Briefapp Box', 'Nenhum arquivo válido para enviar.', icon_type=0x30)
        return

    safe_print(f'Briefapp Box - File Dispatcher')
    safe_print(f'Endpoint: {MCP_URL}')
    safe_print(f'Box ID: {BOX_ID}')
    safe_print(f'Arquivos: {len(files)}\n')

    ok_count  = 0
    err_count = 0

    for i, fpath in enumerate(files, 1):
        size_kb = fpath.stat().st_size / 1024
        safe_print(f'[{i}/{len(files)}] {fpath.name} ({size_kb:.1f} KB)')
        print_progress(i - 1, len(files))

        log.info(f'Iniciando dispatch: {fpath} ({size_kb:.1f} KB)')
        result = dispatch_file(fpath)

        print_progress(i, len(files))
        safe_print()  # nova linha após barra

        if result['success']:
            chars = result.get('chars', 0)
            safe_print(f'  OK - {chars:,} chars enviados')
            log.info(f'  ✓ {fpath.name} — {chars} chars')
            ok_count += 1
        else:
            safe_print(f'  ERRO - {result["message"]}')
            log.error(f'  ✗ {fpath.name} — {result["message"]}')
            err_count += 1

    safe_print(f'\nResultado: {ok_count} enviados, {err_count} erros')

    # Toast de resumo (T8)
    if ok_count > 0 and err_count == 0:
        show_toast(
            'Briefapp Box ✓',
            f'{ok_count} arquivo(s) ingerido(s) com sucesso!',
            icon_type=0x40
        )
    elif err_count > 0:
        show_toast(
            'Briefapp Box ⚠',
            f'{ok_count} OK, {err_count} erro(s). Veja o log.',
            icon_type=0x30
        )

if __name__ == '__main__':
    main()
