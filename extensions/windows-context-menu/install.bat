@echo off
setlocal enabledelayedexpansion
title Briefapp Box - Context Menu Installer

:: ─── Verifica Admin ───────────────────────────────────────────────────────────
net session >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo Solicitando permissao de administrador...
    powershell -Command "Start-Process '%~f0' -Verb RunAs"
    exit /b
)

:: ─── Caminhos ─────────────────────────────────────────────────────────────────
set "EXT_DIR=%~dp0"
set "HANDLER=%EXT_DIR%egeria_handler.py"
set "ICON=%EXT_DIR%icons\briefapp.ico"
set "ENV_FILE=%EXT_DIR%.env"

:: Escapa barras invertidas para .reg
set "HANDLER_ESC=!HANDLER:\=\\!"
set "ICON_ESC=!ICON:\=\\!"

echo.
echo  ╔══════════════════════════════════════════════╗
echo  ║   Briefapp Box  --  Context Menu Installer   ║
echo  ╚══════════════════════════════════════════════╝
echo.
echo  Instalando entradas no Registro do Windows...
echo  Local: !EXT_DIR!
echo.

:: ─── Cria .env se não existir ────────────────────────────────────────────────
if not exist "!ENV_FILE!" (
    echo # Briefapp Box - Config > "!ENV_FILE!"
    echo PANDORA_MCP_URL=http://76.13.238.113:8481/mcp >> "!ENV_FILE!"
    echo PANDORA_BOX_ID= >> "!ENV_FILE!"
    echo PANDORA_API_KEY= >> "!ENV_FILE!"
    echo MAX_FILE_SIZE_MB=50 >> "!ENV_FILE!"
    echo  [INFO] .env criado em !ENV_FILE!
)

:: ─── Gera .reg com caminhos reais ────────────────────────────────────────────
set "TMP_REG=%TEMP%\briefapp_install.reg"

(
echo Windows Registry Editor Version 5.00
echo.
echo ; ── Menu para qualquer ARQUIVO ──────────────────────────────────────────
echo [HKEY_CLASSES_ROOT\*\shell\BriefappSend]
echo @="Briefapp Box: Enviar para Context-Box"
echo "Icon"="!ICON_ESC!"
echo "Position"="Top"
echo.
echo [HKEY_CLASSES_ROOT\*\shell\BriefappSend\command]
echo @="pythonw \"!HANDLER_ESC!\" \"%%1\""
echo.
echo ; ── Menu para DIRETÓRIOS ────────────────────────────────────────────────
echo [HKEY_CLASSES_ROOT\Directory\shell\BriefappSend]
echo @="Briefapp Box: Enviar pasta para Context-Box"
echo "Icon"="!ICON_ESC!"
echo "Position"="Top"
echo.
echo [HKEY_CLASSES_ROOT\Directory\shell\BriefappSend\command]
echo @="python \"!HANDLER_ESC!\" \"%%1\""
echo.
echo ; ── Menu para FUNDO DO EXPLORER ─────────────────────────────────────────
echo [HKEY_CLASSES_ROOT\Directory\Background\shell\BriefappSend]
echo @="Briefapp Box: Enviar esta pasta"
echo "Icon"="!ICON_ESC!"
echo.
echo [HKEY_CLASSES_ROOT\Directory\Background\shell\BriefappSend\command]
echo @="python \"!HANDLER_ESC!\" \"%%V\""
) > "!TMP_REG!"

:: ─── Aplica registro ──────────────────────────────────────────────────────────
reg import "!TMP_REG!"
set REG_RESULT=%ERRORLEVEL%
del "!TMP_REG!"

if %REG_RESULT% EQU 0 (
    echo  [OK] Menu de contexto instalado com sucesso!
    echo.
    echo  Entradas registradas:
    echo    - Arquivos: "Briefapp Box: Enviar para Context-Box"
    echo    - Pastas:   "Briefapp Box: Enviar pasta para Context-Box"
    echo    ^ ^(arquivos de nivel 1 sao ingeridos automaticamente^)
    echo.
    echo  Configuracoes em: !ENV_FILE!
) else (
    echo  [ERRO] Falha ao importar registro. Tente executar como Administrador.
)

echo.
pause
