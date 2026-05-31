<#
.SYNOPSIS
  Briefapp -> Azure DevOps: Dashboard & Widgets Provisioner
.DESCRIPTION
  Cria (ou reutiliza) um dashboard dedicado ao projeto Briefapp no Azure DevOps
  e provisiona os seguintes widgets via REST API v7.1:
    1. Markdown         - Descricao e link de retorno ao Briefapp
    2. Sprint Burndown  - Burndown do sprint ativo
    3. Velocity Chart   - Velocity por sprint
    4. Cumulative Flow  - Fluxo cumulativo de work items
    5. Pie Chart (Estado)  - Contagem por estado
    6. Bar Chart (Tipo)    - Contagem por tipo de work item
    7. Sprint Overview  - Overview do sprint
    8. Team Members     - Membros do time
    9. New Work Item    - Atalho de criacao rapida
   10. Other Links      - Links rapidos

  Ref API:
    Dashboards: https://learn.microsoft.com/rest/api/azure/devops/dashboard/dashboards
    Widgets:    https://learn.microsoft.com/rest/api/azure/devops/dashboard/widgets

.EXAMPLE
  .\scripts\create-ado-dashboard.ps1 `
      -Organization "minha-org" `
      -Project      "meu-projeto" `
      -Pat          "xxxxxxxxxxxx"
#>

[CmdletBinding()]
param (
    [Parameter(Mandatory = $true)]  [string]$Organization,
    [Parameter(Mandatory = $true)]  [string]$Project,
    [Parameter(Mandatory = $true)]  [string]$Pat,
    [Parameter(Mandatory = $false)] [string]$Team = "",
    [Parameter(Mandatory = $false)] [string]$DashboardName = "Briefapp Kanban Overview",
    [Parameter(Mandatory = $false)] [switch]$ForceRecreate
)

Set-StrictMode -Version Latest
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$ErrorActionPreference = "Stop"

# ──────────────────────────────────────────────────────────────
# Helpers
# ──────────────────────────────────────────────────────────────
function Get-AuthHeaders {
    $b64 = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes(":$Pat"))
    @{ Authorization = "Basic $b64"; "Content-Type" = "application/json"; Accept = "application/json" }
}

function Invoke-Ado {
    param([string]$Uri, [string]$Method = "GET", [object]$Body = $null)
    $h = Get-AuthHeaders
    $p = @{ Uri = $Uri; Method = $Method; Headers = $h; UseBasicParsing = $true }
    if ($Body) { $p["Body"] = [System.Text.Encoding]::UTF8.GetBytes(($Body | ConvertTo-Json -Depth 10 -Compress)) }
    (Invoke-WebRequest @p).Content | ConvertFrom-Json
}

function Log-Ok   { param([string]$m) Write-Host "  [OK]   $m" -ForegroundColor Green  }
function Log-Warn { param([string]$m) Write-Host "  [WARN] $m" -ForegroundColor Yellow }
function Log-Fail { param([string]$m) Write-Host "  [FAIL] $m" -ForegroundColor Red    }
function Log-Step { param([string]$m) Write-Host "`n=== $m ===" -ForegroundColor Cyan  }

# ──────────────────────────────────────────────────────────────
# Base URLs  (API versions: Dashboards=7.1-preview.3 / Widgets=7.1-preview.2)
# ──────────────────────────────────────────────────────────────
$enc     = [Uri]::EscapeDataString
$encProj = $enc.Invoke($Project)
$encTeam = if ($Team) { $enc.Invoke($Team) } else { $enc.Invoke("$Project Team") }
$BASE    = "https://dev.azure.com/$Organization/$encProj/$encTeam"
$DASH_API = "$BASE/_apis/dashboard/dashboards"
$DV = "api-version=7.1-preview.3"
$WV = "api-version=7.1-preview.2"

# ──────────────────────────────────────────────────────────────
# 1. Resolver time
# ──────────────────────────────────────────────────────────────
Log-Step "Resolvendo contexto do time"
try {
    $teams   = Invoke-Ado -Uri "https://dev.azure.com/$Organization/_apis/projects/$encProj/teams?api-version=7.1"
    $teamObj = if ($Team) { $teams.value | Where-Object { $_.name -eq $Team } | Select-Object -First 1 }
               else       { $teams.value | Select-Object -First 1 }
    if (-not $teamObj) { throw "Time '$Team' nao encontrado." }
    $teamId   = $teamObj.id
    $teamName = $teamObj.name
    Log-Ok "Time: '$teamName' (id=$teamId)"
} catch {
    Log-Fail "Falha ao resolver time: $_"; exit 1
}

# ──────────────────────────────────────────────────────────────
# 2. Verificar / criar dashboard
# ──────────────────────────────────────────────────────────────
Log-Step "Verificando dashboard '$DashboardName'"
$dashboardId   = $null
$dashboardEtag = "1"

try {
    $existingList = Invoke-Ado -Uri "$DASH_API?$DV"
    $found = $existingList.value | Where-Object { $_.name -eq $DashboardName } | Select-Object -First 1
    if ($found -and -not $ForceRecreate) {
        $dashboardId   = $found.id
        $dashboardEtag = $found.eTag
        Log-Ok "Dashboard existente reutilizado: id=$dashboardId"
    } elseif ($found -and $ForceRecreate) {
        Log-Warn "Deletando dashboard existente (ForceRecreate)..."
        Invoke-Ado -Uri "$DASH_API/$($found.id)?$DV" -Method DELETE | Out-Null
    }
} catch { Log-Warn "Nao foi possivel listar dashboards: $_" }

if (-not $dashboardId) {
    Log-Step "Criando dashboard '$DashboardName'"
    try {
        $d = Invoke-Ado -Uri "$DASH_API?$DV" -Method POST -Body @{
            name            = $DashboardName
            description     = "Provisionado automaticamente pelo Briefapp Sync."
            refreshInterval = 5
        }
        $dashboardId   = $d.id
        $dashboardEtag = $d.eTag
        Log-Ok "Dashboard criado: id=$dashboardId"
    } catch { Log-Fail "Falha ao criar dashboard: $_"; exit 1 }
}

$WGT_URL = "$DASH_API/$dashboardId/widgets?$WV"

# ──────────────────────────────────────────────────────────────
# 3. Definicao dos widgets
#    contributionId = IDs oficiais dos widgets nativos do Azure DevOps
# ──────────────────────────────────────────────────────────────
$markdownText = "## Briefapp Kanban Overview`n`n| Campo | Valor |`n|---|---|`n| Org | $Organization |`n| Projeto | $Project |`n| Time | $teamName |`n`n> Sincronizado via Briefapp Sync REST API v7.1"

$widgetDefs = @(
    @{
        name           = "Briefapp Overview"
        contributionId = "ms.vss-dashboards-web.Microsoft.VisualStudioOnline.Dashboards.MarkdownWidget"
        position       = @{ row = 1; column = 1 }
        size           = @{ rowSpan = 2; columnSpan = 5 }
        settings       = (@{ content = $markdownText; imageUrl = "" } | ConvertTo-Json -Compress)
        settingsVersion = @{ major = 1; minor = 0; patch = 0 }
    },
    @{
        name           = "Team Members"
        contributionId = "ms.vss-dashboards-web.Microsoft.VisualStudioOnline.Dashboards.TeamMembersWidget"
        position       = @{ row = 1; column = 6 }
        size           = @{ rowSpan = 2; columnSpan = 2 }
        settings       = $null
        settingsVersion = @{ major = 1; minor = 0; patch = 0 }
    },
    @{
        name           = "Sprint Burndown"
        contributionId = "ms.vss-dashboards-web.Microsoft.VisualStudioOnline.Dashboards.SprintBurndownWidget"
        position       = @{ row = 3; column = 1 }
        size           = @{ rowSpan = 2; columnSpan = 3 }
        settings       = $null
        settingsVersion = @{ major = 1; minor = 0; patch = 0 }
    },
    @{
        name           = "Velocity"
        contributionId = "ms.vss-dashboards-web.Microsoft.VisualStudioOnline.Dashboards.VelocityWidget"
        position       = @{ row = 3; column = 4 }
        size           = @{ rowSpan = 2; columnSpan = 3 }
        settings       = $null
        settingsVersion = @{ major = 1; minor = 0; patch = 0 }
    },
    @{
        name           = "Cumulative Flow"
        contributionId = "ms.vss-dashboards-web.Microsoft.VisualStudioOnline.Dashboards.CFDWidget"
        position       = @{ row = 3; column = 7 }
        size           = @{ rowSpan = 2; columnSpan = 4 }
        settings       = $null
        settingsVersion = @{ major = 1; minor = 0; patch = 0 }
    },
    @{
        name           = "Work Items por Estado"
        contributionId = "ms.vss-dashboards-web.Microsoft.VisualStudioOnline.Dashboards.WitChartWidget"
        position       = @{ row = 5; column = 1 }
        size           = @{ rowSpan = 2; columnSpan = 3 }
        settings       = (@{
            lastArtifactName = "Work Items por Estado"
            transformOptions = @{
                filter       = "State <> 'Closed'"
                groupBy      = "System.State"
                orderBy      = @{ propertyName = "label"; direction = "descending" }
                measure      = @{ propertyName = "System.Id"; aggregation = "count" }
                historyRange = $null
            }
            userColors = @()
            chartType  = "pieChart"
            scope      = @{ project = @{ name = $Project }; team = @{ name = $teamName } }
        } | ConvertTo-Json -Depth 8 -Compress)
        settingsVersion = @{ major = 1; minor = 0; patch = 0 }
    },
    @{
        name           = "Work Items por Tipo"
        contributionId = "ms.vss-dashboards-web.Microsoft.VisualStudioOnline.Dashboards.WitChartWidget"
        position       = @{ row = 5; column = 4 }
        size           = @{ rowSpan = 2; columnSpan = 3 }
        settings       = (@{
            lastArtifactName = "Work Items por Tipo"
            transformOptions = @{
                filter       = "State <> 'Closed'"
                groupBy      = "System.WorkItemType"
                orderBy      = @{ propertyName = "label"; direction = "descending" }
                measure      = @{ propertyName = "System.Id"; aggregation = "count" }
                historyRange = $null
            }
            userColors = @()
            chartType  = "barChart"
            scope      = @{ project = @{ name = $Project }; team = @{ name = $teamName } }
        } | ConvertTo-Json -Depth 8 -Compress)
        settingsVersion = @{ major = 1; minor = 0; patch = 0 }
    },
    @{
        name           = "Sprint Overview"
        contributionId = "ms.vss-dashboards-web.Microsoft.VisualStudioOnline.Dashboards.SprintOverviewWidget"
        position       = @{ row = 5; column = 7 }
        size           = @{ rowSpan = 2; columnSpan = 4 }
        settings       = $null
        settingsVersion = @{ major = 1; minor = 0; patch = 0 }
    },
    @{
        name           = "Criar Work Item"
        contributionId = "ms.vss-dashboards-web.Microsoft.VisualStudioOnline.Dashboards.NewWorkItemWidget"
        position       = @{ row = 7; column = 1 }
        size           = @{ rowSpan = 1; columnSpan = 2 }
        settings       = (@{ workItemType = "Task"; teamId = $teamId } | ConvertTo-Json -Compress)
        settingsVersion = @{ major = 1; minor = 0; patch = 0 }
    },
    @{
        name           = "Links Rapidos"
        contributionId = "ms.vss-dashboards-web.Microsoft.VisualStudioOnline.Dashboards.OtherLinksWidget"
        position       = @{ row = 7; column = 3 }
        size           = @{ rowSpan = 1; columnSpan = 3 }
        settings       = $null
        settingsVersion = @{ major = 1; minor = 0; patch = 0 }
    }
)

# ──────────────────────────────────────────────────────────────
# 4. Criar widgets (idempotente)
# ──────────────────────────────────────────────────────────────
Log-Step "Provisionando $($widgetDefs.Count) widgets"

$existingWidgets = @()
try {
    $wList = Invoke-Ado -Uri $WGT_URL
    $existingWidgets = $wList.value
    Log-Ok "Widgets existentes: $($existingWidgets.Count)"
} catch { Log-Warn "Dashboard novo - sem widgets anteriores" }

$results = @(); $created = 0; $skipped = 0; $failed = 0

foreach ($w in $widgetDefs) {
    $dup = $existingWidgets | Where-Object { $_.name -eq $w.name -and $_.contributionId -eq $w.contributionId } | Select-Object -First 1
    if ($dup) {
        Log-Warn "SKIP: '$($w.name)' (id=$($dup.id))"
        $skipped++
        $results += [pscustomobject]@{ Name=$w.name; Status="Skipped"; Id=$dup.id; Pos="[$($w.position.row),$($w.position.column)]" }
        continue
    }

    $body = @{
        name            = $w.name
        contributionId  = $w.contributionId
        position        = $w.position
        size            = $w.size
        settingsVersion = $w.settingsVersion
        dashboard       = @{ eTag = $dashboardEtag }
    }
    if ($null -ne $w.settings) { $body["settings"] = $w.settings }

    try {
        $r = Invoke-Ado -Uri $WGT_URL -Method POST -Body $body
        if ($r.dashboard -and $r.dashboard.eTag) { $dashboardEtag = $r.dashboard.eTag }
        Log-Ok "[$($w.position.row),$($w.position.column)] '$($w.name)' -> id=$($r.id)"
        $created++
        $results += [pscustomobject]@{ Name=$w.name; Status="Created"; Id=$r.id; Pos="[$($w.position.row),$($w.position.column)]" }
    } catch {
        Log-Fail "[$($w.position.row),$($w.position.column)] '$($w.name)': $_"
        $failed++
        $results += [pscustomobject]@{ Name=$w.name; Status="Failed"; Id=$null; Pos="[$($w.position.row),$($w.position.column)]" }
    }
}

# ──────────────────────────────────────────────────────────────
# 5. Relatorio final
# ──────────────────────────────────────────────────────────────
Log-Step "Relatorio" "Magenta"
$dashUrl = "https://dev.azure.com/$Organization/$encProj/$encTeam/_dashboards/dashboard/$dashboardId"
Write-Host "  Dashboard : $DashboardName"  -ForegroundColor White
Write-Host "  URL       : $dashUrl"         -ForegroundColor DarkCyan
Write-Host "  Criados   : $created"         -ForegroundColor Green
Write-Host "  Pulados   : $skipped"         -ForegroundColor Yellow
Write-Host "  Falhos    : $failed"          -ForegroundColor $(if ($failed -gt 0) {"Red"} else {"Gray"})
$results | Format-Table -AutoSize Pos, Status, Name, Id

# Salvar widgets.json na raiz do projeto
$reportPath = Join-Path $PSScriptRoot "..\widgets.json"
@{
    dashboardId   = $dashboardId
    dashboardName = $DashboardName
    dashboardUrl  = $dashUrl
    organization  = $Organization
    project       = $Project
    team          = $teamName
    provisionedAt = (Get-Date -Format "o")
    widgets       = $results
} | ConvertTo-Json -Depth 5 | Set-Content -Path $reportPath -Encoding UTF8

Log-Ok "Resultado salvo em: $reportPath"

if ($failed -gt 0) {
    Write-Host "`n[ATENCAO] $failed widget(s) falharam. Verifique:" -ForegroundColor Yellow
    Write-Host "  1. PAT com escopo 'vso.analytics' (para Velocity/Burndown/CFD)" -ForegroundColor Yellow
    Write-Host "  2. Extensao Analytics habilitada na organizacao ADO"              -ForegroundColor Yellow
    Write-Host "  3. Time '$teamName' com permissao de dashboard"                  -ForegroundColor Yellow
}
Write-Host "`nProvisionamento concluido!" -ForegroundColor Green
