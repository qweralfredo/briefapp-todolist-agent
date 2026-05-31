<#
.SYNOPSIS
Briefapp V3 Release Bootstrapper
.DESCRIPTION
This script prepares the release environment, copies the exact .env variables template if missing, and boots the docker-compose.release.yml.
#>

$ErrorActionPreference = 'Stop'
$envFile = ".env"
$envTemplate = ".env.release.example"

Write-Host "🛡️ Briefapp V3 - Agentic Orchestration MVP" -ForegroundColor Cyan
Write-Host "============================================="

if (-Not (Test-Path -Path $envFile)) {
    Write-Host "⚠️ Warning: .env file not found." -ForegroundColor Yellow
    Write-Host "📄 Copying template from $envTemplate to $envFile..."
    Copy-Item -Path $envTemplate -Destination $envFile
    Write-Host "✅ Created $envFile. You should edit its variables before deploying to production!" -ForegroundColor Green
} else {
    Write-Host "✅ .env file found. Proceeding with Docker Compose..." -ForegroundColor Green
}

Write-Host "🚀 Building and starting containers (Background Mode)..." -ForegroundColor Cyan
# Uses the new release stack:
docker compose -f docker-compose.release.yml up -d --build

Write-Host "============================================="
Write-Host "✅ Briefapp Stack Initialized Successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "Service URLs:"
Write-Host "  - UI Dashboard:      http://localhost/"
Write-Host "  - Box 1..3 API:      http://localhost:8080/"
Write-Host "  - Box 4 Gateway:     http://localhost:9700/"
Write-Host "  - Tansu Queue:       http://localhost:9600/"
Write-Host "  - MCP Server Proxy:  http://localhost:8000/"
Write-Host ""
Write-Host "To monitor logs, run: docker compose -f docker-compose.release.yml logs -f"
