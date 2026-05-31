# deploy-scp.ps1
# Sincroniza o código local com o servidor remoto via SCP
# Uso: .\deploy-scp.ps1
# Requer: OpenSSH instalado (disponível no Windows 10/11)

param(
    [string]$RemHost = "root@76.13.238.113",
    [string]$RemDir  = "/var/www/html/todo-release-v1",
    [string]$LocalDir = $PSScriptRoot
)

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  Briefapp Deploy - SCP Sync" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  Local : $LocalDir"
Write-Host "  Remote: ${RemHost}:${RemDir}"
Write-Host ""

# Arquivos/dirs a sincronizar (exclui node_modules, .git, etc.)
$excludes = @(
    "--exclude=.git",
    "--exclude=node_modules",
    "--exclude=frontend/node_modules",
    "--exclude=e2e/node_modules",
    "--exclude=code-agent/frontend/node_modules",
    "--exclude=backend/*/bin",
    "--exclude=backend/*/obj",
    "--exclude=backend/Briefapp.ContextBox/.venv",
    "--exclude=ops/minio/data_v3",
    "--exclude=*.user",
    "--exclude=.vs"
)

# Verifica se rsync existe (via WSL) ou usa scp
$rsync = Get-Command rsync -ErrorAction SilentlyContinue
if ($rsync) {
    Write-Host "Usando rsync..." -ForegroundColor Green
    $excludeArgs = $excludes -join " "
    $cmd = "rsync -avz --progress $excludeArgs `"$LocalDir/`" `"${RemHost}:${RemDir}/`""
    Write-Host "  $cmd" -ForegroundColor DarkGray
    Invoke-Expression $cmd
} else {
    Write-Host "rsync nao encontrado. Usando scp recursivo..." -ForegroundColor Yellow
    Write-Host ""

    # Lista de diretórios críticos para sincronizar
    $dirs = @(
        "backend/AgenticTodoList.Api",
        "mcp-server-python",
        "extensions"
    )

    foreach ($d in $dirs) {
        $src = Join-Path $LocalDir $d
        $dst = "${RemHost}:${RemDir}/$($d -replace '\\', '/')"
        Write-Host "  Enviando: $d ..." -ForegroundColor Yellow
        scp -r "$src" "$dst"
        if ($LASTEXITCODE -eq 0) {
            Write-Host "  OK: $d" -ForegroundColor Green
        } else {
            Write-Host "  ERRO: $d" -ForegroundColor Red
        }
    }
}

Write-Host ""
Write-Host "SCP concluido. Execute deploy-rebuild.ps1 para reconstruir a API." -ForegroundColor Cyan
Write-Host ""
