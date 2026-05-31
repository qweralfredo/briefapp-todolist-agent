<#
.SYNOPSIS
    Briefapp Box — Windows Uninstaller
.DESCRIPTION
    Removes the Briefapp ecosystem from Windows:
    1. Removes briefapp:// protocol handler
    2. Removes context menu entries
    3. Optionally removes C:\briefapp directory
.NOTES
    Run as Administrator for full cleanup.
#>

param(
    [string]$BriefappDir = "C:\briefapp",
    [switch]$RemoveDir,
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

Write-Host ""
Write-Host "  ╔══════════════════════════════════════════════════╗" -ForegroundColor Red
Write-Host "  ║   Briefapp Box — Windows Uninstaller              ║" -ForegroundColor Red
Write-Host "  ╚══════════════════════════════════════════════════╝" -ForegroundColor Red
Write-Host ""

$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

# ─── 1. Remove protocol handler ──────────────────────────────────────────────
Write-Host "  [1/3] Removing briefapp:// protocol handler..." -ForegroundColor White

$protocolKey = "HKCU:\Software\Classes\briefapp"
if (Test-Path $protocolKey) {
    Remove-Item -Path $protocolKey -Recurse -Force
    Write-Host "        Removed: briefapp:// protocol" -ForegroundColor Green
}
else {
    Write-Host "        Not found (already clean)" -ForegroundColor DarkGray
}

# ─── 2. Remove context menus ─────────────────────────────────────────────────
Write-Host "  [2/3] Removing context menu entries..." -ForegroundColor White

if ($isAdmin) {
    $regContent = @"
Windows Registry Editor Version 5.00

[-HKEY_CLASSES_ROOT\*\shell\BriefappSend]
[-HKEY_CLASSES_ROOT\Directory\shell\BriefappSend]
[-HKEY_CLASSES_ROOT\Directory\Background\shell\BriefappSend]
"@

    $tmpReg = Join-Path $env:TEMP "briefapp_uninstall.reg"
    $regContent | Set-Content $tmpReg -Encoding Unicode
    reg import $tmpReg 2>$null
    Remove-Item $tmpReg -Force -ErrorAction SilentlyContinue

    Write-Host "        Removed: context menu entries" -ForegroundColor Green
}
else {
    Write-Host "        [SKIP] Requires Administrator" -ForegroundColor Yellow
}

# ─── 3. Remove directory ─────────────────────────────────────────────────────
if ($RemoveDir) {
    Write-Host "  [3/3] Removing $BriefappDir..." -ForegroundColor White

    if (Test-Path $BriefappDir) {
        if (-not $Force) {
            $confirm = Read-Host "        Are you sure you want to delete $BriefappDir? (y/N)"
            if ($confirm -ne 'y' -and $confirm -ne 'Y') {
                Write-Host "        Cancelled." -ForegroundColor DarkGray
                return
            }
        }
        Remove-Item -Path $BriefappDir -Recurse -Force
        Write-Host "        Removed: $BriefappDir" -ForegroundColor Green
    }
    else {
        Write-Host "        Not found" -ForegroundColor DarkGray
    }
}
else {
    Write-Host "  [3/3] Keeping $BriefappDir (use -RemoveDir to delete)" -ForegroundColor DarkGray
}

Write-Host ""
Write-Host "  Uninstallation complete." -ForegroundColor Green
Write-Host ""
