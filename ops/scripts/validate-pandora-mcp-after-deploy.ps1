param(
    [string]$ApiBaseUrl = "http://127.0.0.1:8480",
    [string]$McpUrl = "http://127.0.0.1:8481/mcp",
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-Path (Join-Path $scriptDir "..\..")

Set-Location $repoRoot

if (-not $SkipBuild) {
    Write-Host "[1/2] Rebuild MCP container..."
    docker compose up -d --build mcp | Out-Host
}
else {
    Write-Host "[1/2] Build skipped (-SkipBuild)."
}

Write-Host "[2/2] Run MCP smoke test client..."
$pythonExe = Join-Path $repoRoot "mcp-server-python\.venv\Scripts\python.exe"
if (-not (Test-Path $pythonExe)) {
    $pythonExe = "python"
}

& $pythonExe "$repoRoot\mcp-server-python\mcp_client_smoke_test.py" --api-base-url $ApiBaseUrl --mcp-url $McpUrl
if ($LASTEXITCODE -ne 0) {
    throw "MCP post-deploy validation failed."
}

Write-Host "MCP post-deploy validation passed."
