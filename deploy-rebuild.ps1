# deploy-rebuild.ps1
param(
    [ValidateSet("api", "mcp", "all")]
    [string]$Service = "api",
    [string]$RemHost = "root@76.13.238.113",
    [string]$RemDir  = "/var/www/html/todo-release-v1"
)

$services = if ($Service -eq "all") { "mcp api" } else { $Service }

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  Briefapp Deploy - Rebuild ($Service)" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  Host   : $RemHost" -ForegroundColor Gray
Write-Host "  Dir    : $RemDir" -ForegroundColor Gray
Write-Host "  Servico: $services" -ForegroundColor Gray
Write-Host ""


Write-Host ""
Write-Host "--- docker compose build $services ---" -ForegroundColor Yellow
ssh $RemHost "cd $RemDir && docker compose build $services"

Write-Host ""
Write-Host "--- docker compose up -d $services ---" -ForegroundColor Yellow
ssh $RemHost "cd $RemDir && docker compose up -d $services"

Write-Host ""
Write-Host "--- logs (tail 40) ---" -ForegroundColor Yellow
Start-Sleep -Seconds 4
ssh $RemHost "cd $RemDir && docker compose logs --tail 40 $services"

Write-Host ""
Write-Host "Deploy concluido!" -ForegroundColor Green
