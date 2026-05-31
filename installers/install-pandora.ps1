<#
.SYNOPSIS
    Briefapp Box — Windows Installer
.DESCRIPTION
    Installs the Briefapp ecosystem on Windows:
    1. Registers the briefapp:// protocol handler
    2. Creates C:\briefapp directory structure
    3. Registers Windows Explorer context menus
    4. Copies browser extension to C:\briefapp\extensions
    5. Configures MCP connection for VS Code
.NOTES
    Run as Administrator for full functionality.
    Usage: powershell -ExecutionPolicy Bypass -File .\installers\install-briefapp.ps1
#>

param(
    [string]$BriefappDir = "C:\briefapp",
    [string]$McpUrl = "http://127.0.0.1:8481/mcp",
    [switch]$SkipContextMenu,
    [switch]$SkipProtocol,
    [switch]$SkipExtensions,
    [switch]$SkipMcp
)

$ErrorActionPreference = 'Stop'
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-Path (Join-Path $scriptDir "..")

Write-Host ""
Write-Host "  ╔══════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "  ║   Briefapp Box — Windows Installer v3.0           ║" -ForegroundColor Cyan
Write-Host "  ╚══════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

# ─── Check Admin ──────────────────────────────────────────────────────────────
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "  [WARN] Not running as Administrator. Some features may require elevation." -ForegroundColor Yellow
    Write-Host "  Tip: Right-click PowerShell → Run as Administrator" -ForegroundColor DarkGray
    Write-Host ""
}

# ─── 1. Create C:\briefapp directory structure ─────────────────────────────────
Write-Host "  [1/5] Creating directory structure..." -ForegroundColor White

$dirs = @(
    $BriefappDir,
    "$BriefappDir\extensions",
    "$BriefappDir\config",
    "$BriefappDir\logs",
    "$BriefappDir\handler"
)

foreach ($d in $dirs) {
    if (-not (Test-Path $d)) {
        New-Item -ItemType Directory -Path $d -Force | Out-Null
        Write-Host "        Created: $d" -ForegroundColor Green
    }
    else {
        Write-Host "        Exists:  $d" -ForegroundColor DarkGray
    }
}

# ─── 2. Register briefapp:// protocol ─────────────────────────────────────────
if (-not $SkipProtocol) {
    Write-Host "  [2/5] Registering briefapp:// protocol handler..." -ForegroundColor White

    $protocolKey = "HKCU:\Software\Classes\briefapp"

    # Create protocol key
    if (-not (Test-Path $protocolKey)) {
        New-Item -Path $protocolKey -Force | Out-Null
    }
    Set-ItemProperty -Path $protocolKey -Name "(Default)" -Value "URL:Briefapp Protocol"
    Set-ItemProperty -Path $protocolKey -Name "URL Protocol" -Value ""

    # Icon
    $iconKey = "$protocolKey\DefaultIcon"
    if (-not (Test-Path $iconKey)) {
        New-Item -Path $iconKey -Force | Out-Null
    }
    
    # Check if icon exists in extensions
    $iconPath = Join-Path $repoRoot "extensions\windows-context-menu\icons\briefapp.ico"
    if (Test-Path $iconPath) {
        Set-ItemProperty -Path $iconKey -Name "(Default)" -Value "$iconPath,0"
        # Also copy icon to briefapp dir
        Copy-Item $iconPath "$BriefappDir\briefapp.ico" -Force -ErrorAction SilentlyContinue
    }

    # Command
    $cmdKey = "$protocolKey\shell\open\command"
    if (-not (Test-Path $cmdKey)) {
        New-Item -Path $cmdKey -Force | Out-Null
    }

    # Look for protocol handler — check common locations
    $handlerScript = Join-Path $repoRoot "extensions\windows-context-menu\egeria_handler.py"
    if (Test-Path $handlerScript) {
        Set-ItemProperty -Path $cmdKey -Name "(Default)" -Value "pythonw `"$handlerScript`" `"%1`""
    }
    else {
        Set-ItemProperty -Path $cmdKey -Name "(Default)" -Value "pythonw `"$BriefappDir\handler\egeria_handler.py`" `"%1`""
    }

    Write-Host "        Registered: briefapp:// → egeria_handler.py" -ForegroundColor Green
}
else {
    Write-Host "  [2/5] Skipping protocol registration (--SkipProtocol)" -ForegroundColor DarkGray
}

# ─── 3. Register Context Menus ────────────────────────────────────────────────
if (-not $SkipContextMenu) {
    Write-Host "  [3/5] Registering context menus..." -ForegroundColor White

    if (-not $isAdmin) {
        Write-Host "        [SKIP] Context menus require Administrator. Re-run as Admin." -ForegroundColor Yellow
    }
    else {
        $handlerPath = Join-Path $repoRoot "extensions\windows-context-menu\egeria_handler.py"
        $iconFile = Join-Path $repoRoot "extensions\windows-context-menu\icons\briefapp.ico"

        # Copy handler to C:\briefapp\handler
        if (Test-Path $handlerPath) {
            Copy-Item $handlerPath "$BriefappDir\handler\egeria_handler.py" -Force
            $envExample = Join-Path $repoRoot "extensions\windows-context-menu\.env.example"
            if (Test-Path $envExample) {
                Copy-Item $envExample "$BriefappDir\handler\.env.example" -Force
            }
            # Create .env if it doesn't exist
            $envTarget = "$BriefappDir\handler\.env"
            if (-not (Test-Path $envTarget)) {
                @"
# Briefapp Box - Config
PANDORA_MCP_URL=$McpUrl
PANDORA_BOX_ID=
PANDORA_API_KEY=
MAX_FILE_SIZE_MB=50
"@ | Set-Content $envTarget -Encoding UTF8
            }
        }

        $handlerEsc = ("$BriefappDir\handler\egeria_handler.py") -replace '\\', '\\'
        $iconEsc = if (Test-Path $iconFile) { $iconFile -replace '\\', '\\' } else { "" }

        $regContent = @"
Windows Registry Editor Version 5.00

; ── File context menu ──
[HKEY_CLASSES_ROOT\*\shell\BriefappSend]
@="Briefapp Box: Send to Context-Box"
"Icon"="$iconEsc"
"Position"="Top"

[HKEY_CLASSES_ROOT\*\shell\BriefappSend\command]
@="pythonw \"$handlerEsc\" \"%1\""

; ── Directory context menu ──
[HKEY_CLASSES_ROOT\Directory\shell\BriefappSend]
@="Briefapp Box: Send folder to Context-Box"
"Icon"="$iconEsc"
"Position"="Top"

[HKEY_CLASSES_ROOT\Directory\shell\BriefappSend\command]
@="python \"$handlerEsc\" \"%1\""

; ── Directory background context menu ──
[HKEY_CLASSES_ROOT\Directory\Background\shell\BriefappSend]
@="Briefapp Box: Send this folder"
"Icon"="$iconEsc"

[HKEY_CLASSES_ROOT\Directory\Background\shell\BriefappSend\command]
@="python \"$handlerEsc\" \"%V\""
"@

        $tmpReg = Join-Path $env:TEMP "briefapp_install.reg"
        $regContent | Set-Content $tmpReg -Encoding Unicode
        reg import $tmpReg 2>$null
        Remove-Item $tmpReg -Force -ErrorAction SilentlyContinue

        Write-Host "        Registered: File → 'Briefapp Box: Send to Context-Box'" -ForegroundColor Green
        Write-Host "        Registered: Directory → 'Briefapp Box: Send folder'" -ForegroundColor Green
        Write-Host "        Registered: Background → 'Briefapp Box: Send this folder'" -ForegroundColor Green
    }
}
else {
    Write-Host "  [3/5] Skipping context menus (--SkipContextMenu)" -ForegroundColor DarkGray
}

# ─── 4. Copy Extensions ──────────────────────────────────────────────────────
if (-not $SkipExtensions) {
    Write-Host "  [4/5] Packaging extensions..." -ForegroundColor White

    $browserExtDir = Join-Path $repoRoot "extensions\browser-scrapper"
    $targetZip = "$BriefappDir\extensions\browser-scrapper.zip"

    if (Test-Path $browserExtDir) {
        if (Test-Path $targetZip) {
            Remove-Item $targetZip -Force
        }
        Compress-Archive -Path "$browserExtDir\*" -DestinationPath $targetZip -Force
        Write-Host "        Packaged: browser-scrapper → $targetZip" -ForegroundColor Green
    }
    else {
        Write-Host "        [SKIP] Browser extension not found at $browserExtDir" -ForegroundColor Yellow
    }

    # Copy context menu extension too
    $ctxMenuDir = Join-Path $repoRoot "extensions\windows-context-menu"
    $ctxTargetZip = "$BriefappDir\extensions\windows-context-menu.zip"

    if (Test-Path $ctxMenuDir) {
        if (Test-Path $ctxTargetZip) {
            Remove-Item $ctxTargetZip -Force
        }
        Compress-Archive -Path "$ctxMenuDir\*" -DestinationPath $ctxTargetZip -Force
        Write-Host "        Packaged: windows-context-menu → $ctxTargetZip" -ForegroundColor Green
    }
}
else {
    Write-Host "  [4/5] Skipping extensions (--SkipExtensions)" -ForegroundColor DarkGray
}

# ─── 5. Configure MCP for VS Code ────────────────────────────────────────────
if (-not $SkipMcp) {
    Write-Host "  [5/5] Configuring MCP for VS Code..." -ForegroundColor White

    $vscodeDir = Join-Path $repoRoot ".vscode"
    if (-not (Test-Path $vscodeDir)) {
        New-Item -ItemType Directory -Path $vscodeDir -Force | Out-Null
    }

    $mcpFile = Join-Path $vscodeDir "mcp.json"
    $mcpConfig = @{
        servers = @{
            "briefapp-todo-list-mcp" = @{
                type = "http"
                url  = $McpUrl
            }
        }
    }

    $mcpConfig | ConvertTo-Json -Depth 10 | Set-Content $mcpFile -Encoding UTF8
    Write-Host "        Configured: $mcpFile" -ForegroundColor Green
}
else {
    Write-Host "  [5/5] Skipping MCP config (--SkipMcp)" -ForegroundColor DarkGray
}

# ─── Summary ──────────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "  ════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  Installation complete!" -ForegroundColor Green
Write-Host ""
Write-Host "  Briefapp directory: $BriefappDir" -ForegroundColor White
Write-Host "  Extensions:        $BriefappDir\extensions\" -ForegroundColor White
Write-Host "  Handler:           $BriefappDir\handler\" -ForegroundColor White
Write-Host "  MCP URL:           $McpUrl" -ForegroundColor White
Write-Host ""
Write-Host "  Next steps:" -ForegroundColor Yellow
Write-Host "    1. Start the stack: docker compose up -d" -ForegroundColor DarkGray
Write-Host "    2. Reload VS Code: Ctrl+Shift+P → Developer: Reload Window" -ForegroundColor DarkGray
Write-Host "    3. Ask Copilot: @workspace list my Briefapp projects" -ForegroundColor DarkGray
Write-Host ""
