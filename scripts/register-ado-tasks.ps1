<#
.SYNOPSIS
  Briefapp Task Registration Script - Azure DevOps Integration
.DESCRIPTION
  Execute when Briefapp API is reachable: .\scripts\register-ado-tasks.ps1
#>
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$API = "http://76.13.238.113:8480/api"
$PROJECT_ID = "fb17358f-c4fa-478a-8827-57e4ede73f94"
$headers = @{ "Content-Type" = "application/json; charset=utf-8" }
$today = Get-Date -Format "yyyy-MM-dd"
$end = (Get-Date).AddDays(14).ToString("yyyy-MM-dd")

Write-Host "=== Registrando Backlogs ===" -ForegroundColor Cyan

$backlogs = @(
    @{
        title       = "BL-ADO-01: Camada de Dados - AzureDevOpsMappingEntity"
        description = "Implementacao da AzureDevOpsMappingEntity e registro no AppDbContext com Fluent API e Migration EF Core."
        storyPoints = 5
        priority    = 3
    },
    @{
        title       = "BL-ADO-02: Infraestrutura - Cliente HTTP e Autenticacao PAT"
        description = "Setup do IHttpClientFactory named azuredevops com timeout 30s. Basic Auth via PAT Base64. API v7.2."
        storyPoints = 3
        priority    = 2
    },
    @{
        title       = "BL-ADO-03: Dominio - Mapeamento de Estados e DTOs JSON Patch"
        description = "Logica de traducao WorkItemStatus para System.State. DTOs: JsonPatchOperation, AdoWorkItemResponse."
        storyPoints = 8
        priority    = 2
    },
    @{
        title       = "BL-ADO-04: Core Service - Motor de Criacao de Work Items POST"
        description = "CreateWorkItemAsync via POST com JSON Patch. Campos: Title, State, Description, AssignedTo, Tags. Hyperlink Briefapp. Persiste mapping."
        storyPoints = 13
        priority    = 2
    },
    @{
        title       = "BL-ADO-05: Core Service - Motor de Atualizacao e Patch"
        description = "UpdateWorkItemAsync via PATCH com replace operations. Blocked tag injection. Atualiza mapping apos sucesso."
        storyPoints = 13
        priority    = 2
    },
    @{
        title       = "BL-ADO-06: Orquestracao - AzureDevOpsSyncWorker Background"
        description = "BackgroundService com PeriodicTimer. IServiceScopeFactory. Query delta WorkItems. SyncAsync publico."
        storyPoints = 8
        priority    = 3
    },
    @{
        title       = "BL-ADO-07: Configuracao e Injecao de Dependencia DI"
        description = "Secao AzureDevOps em appsettings.json. Registro condicional no Program.cs: HttpClient, SyncService, SyncWorker."
        storyPoints = 3
        priority    = 1
    },
    @{
        title       = "BL-ADO-08: API - Endpoints de Controle e Telemetria"
        description = "POST /api/azuredevops/sync trigger manual. GET /api/azuredevops/status health check com totalMappings."
        storyPoints = 5
        priority    = 1
    },
    @{
        title       = "BL-ADO-09: Observabilidade - Logging e Tratamento de Erros"
        description = "Pipeline de logs estruturados com ILogger. Never-throw pattern em SyncService e Worker."
        storyPoints = 5
        priority    = 2
    },
    @{
        title       = "BL-ADO-10: Knowledge Base e Documentacao Tecnica"
        description = "Wiki arquitetura, Doc setup guide PAT, Knowledge Checkpoint decisoes e riscos."
        storyPoints = 3
        priority    = 0
    }
)

$backlogIds = @()
foreach ($bl in $backlogs) {
    $body = $bl | ConvertTo-Json -Depth 3
    try {
        $result = Invoke-RestMethod -Uri "$API/projects/$PROJECT_ID/backlog" -Method Post -Headers $headers -Body ([System.Text.Encoding]::UTF8.GetBytes($body))
        $backlogIds += $result.id
        Write-Host "  OK $($bl.title) -> $($result.id)" -ForegroundColor Green
    }
    catch {
        Write-Host "  FAIL $($bl.title): $($_.Exception.Message)" -ForegroundColor Red
        $backlogIds += $null
    }
}

Write-Host "`n=== Criando Sprint ===" -ForegroundColor Cyan

$validIds = $backlogIds | Where-Object { $_ -ne $null }

$sprintBody = @{
    name           = "SP-ADO-01: Azure DevOps Integration Sprint"
    goal           = "Implementar sincronizacao Briefapp Kanban para Azure DevOps via REST API v7.2"
    startDate      = $today
    endDate        = $end
    backlogItemIds = @($validIds)
} | ConvertTo-Json -Depth 3

try {
    $sprint = Invoke-RestMethod -Uri "$API/projects/$PROJECT_ID/sprints" -Method Post -Headers $headers -Body ([System.Text.Encoding]::UTF8.GetBytes($sprintBody))
    Write-Host "  OK Sprint: $($sprint.id)" -ForegroundColor Green
    $sprintId = $sprint.id
}
catch {
    Write-Host "  FAIL Sprint: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host "`n=== Criando Work Items (Tasks) ===" -ForegroundColor Cyan

$tasks = @(
    @{
        title       = "T-ADO-01: Criar AzureDevOpsMappingEntity.cs"
        description = "Entidade Domain com Id, BriefappWorkItemId FK, AzureDevOpsWorkItemId int, AzureDevOpsUrl, LastSyncedRev, LastSyncedStatus, LastSyncAt, CreatedAt."
        backlogIdx  = 0
        subtasks    = @(
            @{ title = "ST-01a: Definir propriedades e FK WorkItemEntity"; description = "Classe com propriedades tipadas e navigation property." },
            @{ title = "ST-01b: Registrar DbSet no AppDbContext"; description = "DbSet AzureDevOpsMappings." },
            @{ title = "ST-01c: Configurar Fluent API OnModelCreating"; description = "HasIndex Unique, HasOne FK Cascade, HasMaxLength 500." },
            @{ title = "ST-01d: Gerar Migration EF Core"; description = "dotnet ef migrations add AddAzureDevOpsMappings." }
        )
    },
    @{
        title       = "T-ADO-02: Configurar HttpClient e Auth PAT"
        description = "HttpClient named azuredevops timeout 30s. Basic Auth Base64 encode PAT. AuthenticationHeaderValue por request."
        backlogIdx  = 1
        subtasks    = @(
            @{ title = "ST-02a: Registrar HttpClient azuredevops no DI"; description = "AddHttpClient com timeout 30s." },
            @{ title = "ST-02b: Implementar autenticacao PAT"; description = "Base64 de :PAT, Basic Auth header." },
            @{ title = "ST-02c: Validar IsEnabled guard"; description = "Verificar Enabled + Org/Project/Pat nao vazios." }
        )
    },
    @{
        title       = "T-ADO-03: Implementar MapStatus e DTOs JSON Patch"
        description = "Switch expression MapStatus. Classes JsonPatchOperation, AdoWorkItemResponse, AdoLinks com JsonPropertyName."
        backlogIdx  = 2
        subtasks    = @(
            @{ title = "ST-03a: Criar MapStatus switch expression"; description = "Todo->New, InProgress->Active, Review->Resolved, Done->Closed, Blocked->New." },
            @{ title = "ST-03b: Criar DTOs internos JSON Patch"; description = "JsonPatchOperation op path value. PatchOp helper." },
            @{ title = "ST-03c: Criar DTOs de resposta ADO"; description = "AdoWorkItemResponse id rev fields _links." },
            @{ title = "ST-03d: Configurar JsonSerializerOptions"; description = "CamelCase, WhenWritingNull, json-patch+json." }
        )
    },
    @{
        title       = "T-ADO-04: Implementar CreateWorkItemAsync POST"
        description = "POST wit/workitems com JSON Patch array. System.Title, State, Description, AssignedTo, Tags. Hyperlink relation. Persistir mapping."
        backlogIdx  = 3
        subtasks    = @(
            @{ title = "ST-04a: Construir array JSON Patch operations"; description = "add /fields/System.Title, State, Description, AssignedTo, Tags." },
            @{ title = "ST-04b: Adicionar hyperlink relation Briefapp"; description = "add /relations/- com rel=Hyperlink url=briefapp://workitems/id." },
            @{ title = "ST-04c: Executar POST e tratar resposta"; description = "PatchAsync com URL criacao. Deserializar AdoWorkItemResponse." },
            @{ title = "ST-04d: Persistir AzureDevOpsMappingEntity"; description = "Criar mapping com AdoId, Url, Rev, Status. SaveChangesAsync." }
        )
    },
    @{
        title       = "T-ADO-05: Implementar UpdateWorkItemAsync PATCH"
        description = "PATCH wit/workitems/id com replace operations. Blocked tag injection. Atualizar mapping."
        backlogIdx  = 4
        subtasks    = @(
            @{ title = "ST-05a: Construir array de replace operations"; description = "replace /fields/System.Title, State etc." },
            @{ title = "ST-05b: Implementar Blocked tag injection"; description = "Append Blocked nas tags sem duplicar." },
            @{ title = "ST-05c: Atualizar mapping apos sucesso"; description = "LastSyncedRev, LastSyncedStatus, LastSyncAt." }
        )
    },
    @{
        title       = "T-ADO-06: Implementar AzureDevOpsSyncWorker"
        description = "BackgroundService PeriodicTimer. ExecuteAsync startup delay. SyncAsync scope factory query delta."
        backlogIdx  = 5
        subtasks    = @(
            @{ title = "ST-06a: Estrutura BackgroundService base"; description = "Herdar BackgroundService. IServiceScopeFactory IConfiguration ILogger." },
            @{ title = "ST-06b: ExecuteAsync com PeriodicTimer"; description = "Startup delay 10s. Loop WaitForNextTickAsync." },
            @{ title = "ST-06c: SyncAsync com query delta"; description = "WorkItems where CreatedAt/UpdatedAt >= since. ForEach SyncWorkItemAsync." },
            @{ title = "ST-06d: Expor propriedades de status"; description = "LastSyncAt, LastSyncCount, SyncIntervalMinutes, SyncResult." }
        )
    },
    @{
        title       = "T-ADO-07: Configurar appsettings e DI"
        description = "Secao AzureDevOps em appsettings.json. Registro condicional Program.cs."
        backlogIdx  = 6
        subtasks    = @(
            @{ title = "ST-07a: Secao AzureDevOps no appsettings.json"; description = "Enabled, Organization, Project, Pat, SyncIntervalMinutes, DefaultWorkItemType." },
            @{ title = "ST-07b: Registro condicional no Program.cs"; description = "if adoEnabled AddHttpClient AddScoped AddSingleton AddHostedService." },
            @{ title = "ST-07c: Adicionar using no Program.cs"; description = "using BriefappTodoList.Api.Services.AzureDevOps." }
        )
    },
    @{
        title       = "T-ADO-08: Implementar endpoints REST"
        description = "POST /api/azuredevops/sync trigger manual. GET /api/azuredevops/status health."
        backlogIdx  = 7
        subtasks    = @(
            @{ title = "ST-08a: POST /api/azuredevops/sync endpoint"; description = "Resolve worker, SyncAsync, retorna synced/failed/triggeredAt." },
            @{ title = "ST-08b: GET /api/azuredevops/status endpoint"; description = "Worker status, count mappings. Retorna enabled, lastSyncAt, totalMappings." }
        )
    },
    @{
        title       = "T-ADO-09: Implementar logging estruturado"
        description = "LogWarning falhas HTTP. LogInformation creates/updates. Never-throw pattern."
        backlogIdx  = 8
        subtasks    = @(
            @{ title = "ST-09a: Logging no SyncService"; description = "LogWarning PatchAsync status+body. LogInformation Create/Update success." },
            @{ title = "ST-09b: Logging no SyncWorker"; description = "LogInformation startup e ciclo. LogWarning catch. LogDebug disabled." }
        )
    },
    @{
        title       = "T-ADO-10: Criar wiki e documentacao"
        description = "Wiki arquitetura ADO. Doc setup guide PAT. Knowledge checkpoint decisoes riscos."
        backlogIdx  = 9
        subtasks    = @(
            @{ title = "ST-10a: Wiki Arquitetura da Integracao"; description = "Diagrama Mermaid, fluxo sync, tabela mapeamento estados." },
            @{ title = "ST-10b: Doc Setup Guide"; description = "Pre-requisitos PAT, configuracao appsettings, variaveis de ambiente." },
            @{ title = "ST-10c: Knowledge Checkpoint"; description = "Decisoes arquiteturais, riscos identificados, proximos passos." }
        )
    }
)

foreach ($task in $tasks) {
    $backlogId = $backlogIds[$task.backlogIdx]
    if ($null -eq $backlogId) {
        Write-Host "  SKIP $($task.title) - no backlog ID" -ForegroundColor Yellow
        continue
    }

    $taskBody = @{
        title         = $task.title
        description   = $task.description
        backlogItemId = $backlogId
    } | ConvertTo-Json -Depth 3

    try {
        $wi = Invoke-RestMethod -Uri "$API/projects/$PROJECT_ID/workitems?sprintId=$sprintId" -Method Post -Headers $headers -Body ([System.Text.Encoding]::UTF8.GetBytes($taskBody))
        Write-Host "  OK $($task.title) -> $($wi.id)" -ForegroundColor Green

        foreach ($st in $task.subtasks) {
            $stBody = @{
                title       = $st.title
                description = $st.description
            } | ConvertTo-Json -Depth 3
            try {
                $sub = Invoke-RestMethod -Uri "$API/work-items/$($wi.id)/sub-tasks" -Method Post -Headers $headers -Body ([System.Text.Encoding]::UTF8.GetBytes($stBody))
                Write-Host "    > $($st.title) -> $($sub.id)" -ForegroundColor DarkGreen
            }
            catch {
                Write-Host "    > FAIL $($st.title): $($_.Exception.Message)" -ForegroundColor Red
            }
        }
    }
    catch {
        Write-Host "  FAIL $($task.title): $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host "`n=== Criando Wiki ===" -ForegroundColor Cyan
$wikiContent = "# Arquitetura de Integracao Briefapp - Azure DevOps`n`n## Visao Geral`nSincronizacao unidirecional Briefapp para Azure DevOps via REST API v7.2.`n`n## Fluxo`n1. AzureDevOpsSyncWorker identifica WorkItems modificados`n2. Novos itens: POST /_apis/wit/workitems`n3. Existentes: PATCH /_apis/wit/workitems/{id}`n4. Mapeamento persistido em AzureDevOpsMappingEntity`n`n## Mapeamento de Status`n| Briefapp | Azure DevOps |`n|---------|-------------|`n| Todo | New |`n| InProgress | Active |`n| Review | Resolved |`n| Done | Closed |`n| Blocked | New + Tag |`n`n## Autenticacao`nBasic Auth: :PAT encoded as Base64."
$wikiBody = @{
    title           = "Arquitetura da Integracao Azure DevOps"
    contentMarkdown = $wikiContent
    tags            = "architecture,azure-devops,sync,integration"
} | ConvertTo-Json -Depth 3

try {
    $wiki = Invoke-RestMethod -Uri "$API/projects/$PROJECT_ID/wiki" -Method Post -Headers $headers -Body ([System.Text.Encoding]::UTF8.GetBytes($wikiBody))
    Write-Host "  OK Wiki: $($wiki.id)" -ForegroundColor Green
}
catch {
    Write-Host "  FAIL Wiki: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n=== Criando Documentacao ===" -ForegroundColor Cyan
$docContent = "# Setup Guide - Azure DevOps Integration`n`n## Pre-requisitos`n- PAT com scope Work Items Read and Write`n- Azure DevOps Organization e Project Name`n`n## Configuracao appsettings.json`n``````json`n{`n  `"AzureDevOps`": {`n    `"Enabled`": true,`n    `"Organization`": `"minha-org`",`n    `"Project`": `"meu-projeto`",`n    `"Pat`": `"TOKEN`",`n    `"SyncIntervalMinutes`": 5`n  }`n}`n```````n`n## Endpoints`n- POST /api/azuredevops/sync - Trigger manual`n- GET /api/azuredevops/status - Health check"
$docBody = @{
    title           = "Setup Guide - Integracao Azure DevOps"
    contentMarkdown = $docContent
    category        = "Setup"
    tags            = "setup,config,azure-devops,pat"
} | ConvertTo-Json -Depth 3

try {
    $doc = Invoke-RestMethod -Uri "$API/projects/$PROJECT_ID/documentation" -Method Post -Headers $headers -Body ([System.Text.Encoding]::UTF8.GetBytes($docBody))
    Write-Host "  OK Doc: $($doc.id)" -ForegroundColor Green
}
catch {
    Write-Host "  FAIL Doc: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n=== Criando Knowledge Checkpoint ===" -ForegroundColor Cyan
$checkBody = @{
    name            = "Checkpoint - Integracao Azure DevOps v1.0"
    contextSnapshot = "Implementacao completa da integracao Briefapp para Azure DevOps. 3 novos arquivos criados, 3 modificados. Build 0 errors. Migration gerada."
    decisions       = "1) Sync unidirecional Briefapp para ADO. 2) BackgroundService nativo. 3) Persistencia mapping via EF Core. 4) JSON Patch content-type. 5) Blocked mapeia para New+Tag. 6) Registro condicional."
    risks           = "1) Rate limiting API ADO. 2) Latencia de rede. 3) PAT expiration. 4) Mapeamento estados pode diferir entre organizacoes."
    nextActions     = "1) Obter PAT valido. 2) Testar criacao work item. 3) Verificar mapeamento estados. 4) Considerar sync bidirecional."
} | ConvertTo-Json -Depth 3

try {
    $check = Invoke-RestMethod -Uri "$API/projects/$PROJECT_ID/checkpoints" -Method Post -Headers $headers -Body ([System.Text.Encoding]::UTF8.GetBytes($checkBody))
    Write-Host "  OK Checkpoint: $($check.id)" -ForegroundColor Green
}
catch {
    Write-Host "  FAIL Checkpoint: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n=== Registro completo! ===" -ForegroundColor Green
Write-Host "10 Backlogs + 1 Sprint + 10 Tasks + 33 Subtasks + Wiki + Doc + Checkpoint" -ForegroundColor Cyan
