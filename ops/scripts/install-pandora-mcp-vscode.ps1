param(
    [switch]$OpenInstallLink,
    [switch]$Global
)

$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-Path (Join-Path $scriptDir "..\..")

if ($Global) {
    $targetDir = Join-Path $env:APPDATA "Code\User"
}
else {
    $targetDir = Join-Path $repoRoot ".vscode"
}

$mcpFile = Join-Path $targetDir "mcp.json"

if (-not (Test-Path $targetDir)) {
    New-Item -ItemType Directory -Path $targetDir | Out-Null
}

$config = $null
if (Test-Path $mcpFile) {
    $raw = Get-Content -Raw -Path $mcpFile
    if (-not [string]::IsNullOrWhiteSpace($raw)) {
        try {
            $config = $raw | ConvertFrom-Json
        }
        catch {
            throw "Arquivo mcp.json invalido em $mcpFile. Corrija o JSON e rode novamente."
        }
    }
}

if ($null -eq $config) {
    $config = [pscustomobject]@{}
}

if ($null -eq $config.servers) {
    $config | Add-Member -NotePropertyName "servers" -NotePropertyValue ([pscustomobject]@{})
}

$briefappServer = [pscustomobject]@{
    type = "http"
    url  = "http://127.0.0.1:8481/mcp"
}

$servers = $config.servers
if ($servers.PSObject.Properties.Match("briefapp-todo-list-mcp").Count -eq 0) {
    $servers | Add-Member -NotePropertyName "briefapp-todo-list-mcp" -NotePropertyValue $briefappServer
}
else {
    $servers."briefapp-todo-list-mcp" = $briefappServer
}

$json = $config | ConvertTo-Json -Depth 20
Set-Content -Path $mcpFile -Value $json -Encoding UTF8

Write-Host "MCP do Briefapp configurado com sucesso em: $mcpFile"
Write-Host "Reabra o VS Code ou execute o comando 'Developer: Reload Window'."

if ($Global) {
    Write-Host "Escopo aplicado: GLOBAL (todos os workspaces do VS Code)."
}
else {
    Write-Host "Escopo aplicado: WORKSPACE (apenas este repositorio)."
}

if ($OpenInstallLink) {
    Write-Host "OpenInstallLink ignorado para configuracao HTTP local."
}
