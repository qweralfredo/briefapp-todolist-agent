param(
    [Parameter(Mandatory=$true)]
    [string]$TaskId,

    [Parameter(Mandatory=$true)]
    [string]$TaskPrompt
)

$ErrorActionPreference = "Stop"

# Define worktree path
$WorktreePath = Join-Path $PSScriptRoot "..\..\.worktrees\task-$TaskId"
$BranchName = "agent/task-$TaskId"

Write-Host "Starting Subagent execution for Task: $TaskId" -ForegroundColor Cyan

# 1. Ensure worktrees base directory exists
$WorktreesBase = Join-Path $PSScriptRoot "..\..\.worktrees"
if (-not (Test-Path $WorktreesBase)) {
    New-Item -ItemType Directory -Path $WorktreesBase | Out-Null
}

# 2. Check if branch exists, if not create it
$branchExists = git branch --list $BranchName
if (-not $branchExists) {
    git branch $BranchName
}

# 3. Create worktree
if (Test-Path $WorktreePath) {
    Write-Host "Worktree already exists. Reusing..." -ForegroundColor Yellow
} else {
    git worktree add $WorktreePath $BranchName
}

# 4. Run Gemini CLI in Headless Mode inside the worktree
Push-Location $WorktreePath
try {
    Write-Host "Running Gemini CLI Headless Mode..." -ForegroundColor Cyan
    # Inject task into environment or prompt
    # Using generic command for demonstration
    gemini --prompt "Execute the following task: $TaskPrompt"
    
    # After completion, commit the changes
    git add .
    git commit -m "agent(task-$TaskId): automated completion of task"
    
    Write-Host "Task $TaskId completed and committed to branch $BranchName." -ForegroundColor Green
}
catch {
    Write-Host "Failed to execute agent for task $TaskId" -ForegroundColor Red
}
finally {
    Pop-Location
    # Optional: cleanup worktree after done
    # git worktree remove $WorktreePath --force
}
