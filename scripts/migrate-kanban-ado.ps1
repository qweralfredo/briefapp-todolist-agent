param (
    [Parameter(Mandatory=$true)]
    [string]$ProjectId,

    [Parameter(Mandatory=$false)]
    [string]$BriefappUrl = "http://localhost:8480"
)

Write-Host "Iniciando migração do Kanban para o Azure DevOps..." -ForegroundColor Cyan

# 1. Obter informações do Projeto
$projectUrl = "$BriefappUrl/api/projects"
$projects = Invoke-RestMethod -Uri $projectUrl -Method Get
$project = $projects | Where-Object { $_.id -eq $ProjectId }

if (-not $project) {
    Write-Host "Projeto não encontrado na Briefapp." -ForegroundColor Red
    exit 1
}

$adoOrg = $project.adoOrganization
$adoProj = $project.adoProject
$pat = $project.adoPat

if (-not $adoOrg -or -not $adoProj -or -not $pat) {
    Write-Host "Configurações do Azure DevOps incompletas no projeto." -ForegroundColor Red
    exit 1
}

$base64AuthInfo = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes(":$($pat)"))
$headers = @{
    Authorization = "Basic $base64AuthInfo"
    "Content-Type" = "application/json-patch+json"
}

$adoBaseUrl = "https://dev.azure.com/$adoOrg/$adoProj/_apis/wit/workitems"
$apiVersion = "api-version=7.1"

# Dicionários para mapeamento
$backlogMap = @{}

# Função auxiliar para criar WorkItem no ADO
function Create-AdoWorkItem {
    param ($Type, $Title, $Description, $Tags, $ParentId)

    $patch = @(
        @{ op = "add"; path = "/fields/System.Title"; value = $Title }
    )

    if ($Description) {
        $patch += @{ op = "add"; path = "/fields/System.Description"; value = $Description }
    }

    if ($Tags) {
        $patch += @{ op = "add"; path = "/fields/System.Tags"; value = $Tags }
    }

    if ($ParentId) {
        $patch += @{
            op = "add"
            path = "/relations/-"
            value = @{
                rel = "System.LinkTypes.Hierarchy-Reverse"
                url = "https://dev.azure.com/$adoOrg/$adoProj/_apis/wit/workItems/$ParentId"
            }
        }
    }

    $body = $patch | ConvertTo-Json -Depth 5
    $url = "$adoBaseUrl/`$$Type`?$apiVersion"

    try {
        $response = Invoke-RestMethod -Uri $url -Method Patch -Headers $headers -Body $body
        return $response.id
    }
    catch {
        Write-Host "Erro ao criar $Type ($Title): $_" -ForegroundColor Red
        return $null
    }
}

# 2. Migrar Backlog Items (Issues/User Stories no Kanban)
Write-Host "`nMigrando Backlog Items..." -ForegroundColor Yellow
$backlogUrl = "$BriefappUrl/api/projects/$ProjectId/backlog"
$backlogs = Invoke-RestMethod -Uri $backlogUrl -Method Get

foreach ($bl in $backlogs) {
    # No Basic é Issue, no Agile é User Story. Vamos usar 'Issue' como padrão genérico que aparece no Kanban.
    $adoId = Create-AdoWorkItem -Type "Issue" -Title $bl.title -Description $bl.description -Tags "BacklogItem;$($bl.tags)"
    if ($adoId) {
        $backlogMap[$bl.id] = $adoId
        Write-Host "  -> Criado Issue #$adoId para Backlog '$($bl.title)'" -ForegroundColor Green
    }
}

# 3. Migrar Sprints e Tasks
Write-Host "`nMigrando Tasks (WorkItems)..." -ForegroundColor Yellow
$sprintsUrl = "$BriefappUrl/api/projects/$ProjectId/sprints"
$sprints = Invoke-RestMethod -Uri $sprintsUrl -Method Get

foreach ($sp in $sprints) {
    # A API da Briefapp tem os workitems no objeto de sprint? Se não, precisamos da rota.
    # Vamos tentar pegar os workItems da rota /api/projects/$ProjectId/sprints ou /api/projects/$ProjectId/knowledge
    # Se a API não expõe, precisamos de uma query direta. Mas o worker de sync já fez isso.
    # Como o usuário quer vincular as tasks, vamos tentar acessar a rota de workitems do sprint.
    try {
        $wiUrl = "$BriefappUrl/api/sprints/$($sp.id)/workitems"
        $workItems = Invoke-RestMethod -Uri $wiUrl -Method Get

        foreach ($wi in $workItems) {
            $parentId = $null
            if ($wi.backlogItemId -and $backlogMap.ContainsKey($wi.backlogItemId)) {
                $parentId = $backlogMap[$wi.backlogItemId]
            }

            $adoId = Create-AdoWorkItem -Type "Task" -Title $wi.title -Description $wi.description -Tags "Task" -ParentId $parentId
            if ($adoId) {
                Write-Host "  -> Criado Task #$adoId para WorkItem '$($wi.title)' (Parent: $parentId)" -ForegroundColor Green
            }
        }
    } catch {
        Write-Host "  Sem workitems ou erro no Sprint $($sp.name)" -ForegroundColor DarkGray
    }
}

# 4. Migrar Knowledge (Wikis, Docs, Checkpoints)
Write-Host "`nMigrando Conhecimento (Wiki, Documentação, Checkpoints)..." -ForegroundColor Yellow
$knowledgeUrl = "$BriefappUrl/api/projects/$ProjectId/knowledge"
$knowledge = Invoke-RestMethod -Uri $knowledgeUrl -Method Get

foreach ($wiki in $knowledge.wikis) {
    $adoId = Create-AdoWorkItem -Type "Issue" -Title "[Wiki] $($wiki.title)" -Description $wiki.contentMarkdown -Tags "Wiki;$($wiki.category);$($wiki.tags)"
    if ($adoId) { Write-Host "  -> Criado Wiki Issue #$adoId" -ForegroundColor Green }
}

foreach ($doc in $knowledge.documentation) {
    $adoId = Create-AdoWorkItem -Type "Issue" -Title "[Doc] $($doc.title)" -Description $doc.contentMarkdown -Tags "Documentation;$($doc.category);$($doc.tags)"
    if ($adoId) { Write-Host "  -> Criado Doc Issue #$adoId" -ForegroundColor Green }
}

foreach ($chk in $knowledge.checkpoints) {
    $desc = "Decisions: $($chk.decisions)`n`nRisks: $($chk.risks)`n`nNext Actions: $($chk.nextActions)"
    $adoId = Create-AdoWorkItem -Type "Issue" -Title "[Checkpoint] $($chk.name)" -Description $desc -Tags "Checkpoint;$($chk.category)"
    if ($adoId) { Write-Host "  -> Criado Checkpoint Issue #$adoId" -ForegroundColor Green }
}

Write-Host "`nMigração concluída!" -ForegroundColor Cyan
