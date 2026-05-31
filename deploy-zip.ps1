<#
.SYNOPSIS
Script para compactar a pasta do projeto (ignorando arquivos ocultos/pesados base),
subir via SCP para o servidor destino e descompactar, substituindo os arquivos remotamente.
#>

param(
    [string]$RemHost = "root@76.13.238.113",
    [string]$RemDir  = "/var/www/html/todo-release-v1",
    [string]$LocalDir = "C:\projetos\todolist"
)

$ArchiveName = "todolist_deploy.tar.gz"
$ArchivePath = Join-Path $LocalDir $ArchiveName

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  Briefapp Deploy - ZIP/TAR Upload" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  Origem : $LocalDir"
Write-Host "  Destino: ${RemHost}:${RemDir}"
Write-Host ""

Write-Host "1. Compactando diretorio local..." -ForegroundColor Yellow
Set-Location $LocalDir

# Evitar subir as pastas pesadas de desenvolvimento e compilados
$Excludes = "--exclude=.git --exclude=*node_modules* --exclude=bin --exclude=obj --exclude=.venv --exclude=*data_v3* --exclude=.pytest_cache --exclude=__pycache__ --exclude=$ArchiveName"

# Executar o tar do Windows (bsdtar)
$TarCmd = "tar.exe -czvf $ArchiveName $Excludes ."
Invoke-Expression $TarCmd

if (-Not (Test-Path $ArchivePath)) {
    Write-Host "Falha na compactacao. Abortando." -ForegroundColor Red
    exit 1
}

Write-Host "`n2. Fazendo upload do pacote via SCP..." -ForegroundColor Yellow
scp $ArchivePath "${RemHost}:/tmp/${ArchiveName}"

if ($LASTEXITCODE -ne 0) {
    Write-Host "Falha ao enviar arquivo via SCP. Abortando." -ForegroundColor Red
    exit 1
}

Write-Host "`n3. Substituindo os arquivos no servidor remoto via SSH..." -ForegroundColor Yellow
# Isso criara o diretorio se nao existir, limpara o codigo antigo das pastas principais
# e descompactara zerado, removendo o arquivo tar.gz no final.
$CleanCmd = "rm -rf ${RemDir}/backend ${RemDir}/frontend ${RemDir}/extensions ${RemDir}/mcp-server-python ${RemDir}/code-agent"
$SshCmd = "mkdir -p ${RemDir} && $CleanCmd && tar -xzvf /tmp/${ArchiveName} -C ${RemDir}/ && rm /tmp/${ArchiveName}"
ssh $RemHost $SshCmd

Write-Host "`n4. Limpando lixo residual local..." -ForegroundColor Yellow
Remove-Item $ArchivePath -Force

Write-Host "`n✅  Deploy completo com sucesso! Os arquivos foram substituidos no servidor." -ForegroundColor Green
Write-Host ""
