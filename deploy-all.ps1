<#
.SYNOPSIS
Script unificado para compactar, subir os arquivos (ZIP/SCP) e reconstruir/reiniciar os contêineres Docker remotamente usando --build.
#>

param(
    [string]$RemHost = "root@76.13.238.113",
    [string]$RemDir  = "/var/www/html/todo-release-v1",
    [string]$LocalDir = "C:\projetos\todolist",
    [string]$Services = "" # Deixe em branco para recriar TUDO (ou passe "frontend api")
)

$ArchiveName = "todolist_deploy.tar.gz"
$ArchivePath = Join-Path $LocalDir $ArchiveName

Write-Host "`n============================================" -ForegroundColor Cyan
Write-Host "  Briefapp One-Click Deploy" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  Origem   : $LocalDir"
Write-Host "  Destino  : ${RemHost}:${RemDir}"
Write-Host "  Servicos : $(if ($Services) { $Services } else { 'ALL' })"

Write-Host "`n[1/5] Compactando diretorio local..." -ForegroundColor Yellow
Set-Location $LocalDir

$Excludes = "--exclude=ops --exclude=.git --exclude=*node_modules* --exclude=bin --exclude=obj --exclude=.venv --exclude=*data_v3* --exclude=.pytest_cache --exclude=__pycache__ --exclude=$ArchiveName"
$TarCmd = "tar.exe -czvf $ArchiveName $Excludes ."
Invoke-Expression $TarCmd | Out-Null

if (-Not (Test-Path $ArchivePath)) {
    Write-Host "Falha na compactacao. Abortando." -ForegroundColor Red
    exit 1
}

Write-Host "[2/5] Fazendo upload via SCP..." -ForegroundColor Yellow
scp $ArchivePath "${RemHost}:/tmp/${ArchiveName}"

if ($LASTEXITCODE -ne 0) {
    Write-Host "Falha no SCP. Abortando." -ForegroundColor Red
    exit 1
}

Write-Host "[3/5] Descompactando e substituindo codigo no servidor remoto..." -ForegroundColor Yellow
$CleanCmd = "rm -rf ${RemDir}/backend ${RemDir}/frontend ${RemDir}/extensions ${RemDir}/mcp-server-python"
$SshCmdExtract = "mkdir -p ${RemDir} ; $CleanCmd ; tar -xzvf /tmp/${ArchiveName} -C ${RemDir}/ > /dev/null ; rm /tmp/${ArchiveName}"
ssh $RemHost $SshCmdExtract

Write-Host "[4/5] Limpando ZIP local..." -ForegroundColor Yellow
Remove-Item $ArchivePath -Force

Write-Host "[5/5] Reconstruindo dependencias e conteineres Docker remotamente (--build)..." -ForegroundColor Yellow
$SshCmdDocker = "cd ${RemDir} ; docker compose up -d --build $Services"
ssh $RemHost $SshCmdDocker

Write-Host "`nStatus final:" -ForegroundColor Cyan
ssh $RemHost "cd ${RemDir} ; docker compose ps"

Write-Host "`nDeploy Master concluido com sucesso!" -ForegroundColor Green
