param(
    [string]$ContainerName = "briefapp-postgres",
    [string]$Database = "briefapp_todo_list",
    [string]$User = "Briefapp"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$backupDir = Join-Path $root "postgres\backups"
if (-not (Test-Path $backupDir)) {
    New-Item -Path $backupDir -ItemType Directory | Out-Null
}

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$backupFile = Join-Path $backupDir ("briefapp_todo_list-" + $timestamp + ".sql")

$dump = docker exec $ContainerName pg_dump -U $User -d $Database
if ($LASTEXITCODE -ne 0) {
    throw "Backup failed. Ensure container '$ContainerName' is running."
}

$dump | Set-Content -Path $backupFile -Encoding UTF8
Write-Host "Backup created at $backupFile"

