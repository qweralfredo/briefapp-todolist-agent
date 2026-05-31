param(
    [string]$HookType
)

# ── Token Tracking Strategy ──────────────────────────────────────────────
# Priority order:
#   1. Real env vars from Gemini CLI (if exposed in future versions)
#   2. Jaeger/OTEL trace query (if collector is running)
#   3. Fallback: report 0 (honest about not knowing)
# ────────────────────────────────────────────────────────────────────────

$inputTokens = 0
$outputTokens = 0
$tokenSource = "none"

# Strategy 1: Try Gemini CLI env vars (not currently exported, but future-proof)
if ($env:GEMINI_PROMPT_TOKENS -and $env:GEMINI_PROMPT_TOKENS -ne "0") {
    $inputTokens = [int]$env:GEMINI_PROMPT_TOKENS
    $outputTokens = if ($env:GEMINI_COMPLETION_TOKENS) { [int]$env:GEMINI_COMPLETION_TOKENS } else { 0 }
    $tokenSource = "gemini_env"
}
elseif ($env:GEMINI_INPUT_TOKENS -and $env:GEMINI_INPUT_TOKENS -ne "0") {
    $inputTokens = [int]$env:GEMINI_INPUT_TOKENS
    $outputTokens = if ($env:GEMINI_OUTPUT_TOKENS) { [int]$env:GEMINI_OUTPUT_TOKENS } else { 0 }
    $tokenSource = "gemini_env"
}
elseif ($env:LLM_PROMPT_TOKENS -and $env:LLM_PROMPT_TOKENS -ne "0") {
    $inputTokens = [int]$env:LLM_PROMPT_TOKENS
    $outputTokens = if ($env:LLM_COMPLETION_TOKENS) { [int]$env:LLM_COMPLETION_TOKENS } else { 0 }
    $tokenSource = "llm_env"
}

# Strategy 2: Try to query Jaeger/OTEL for recent traces (best-effort, non-blocking)
if ($tokenSource -eq "none" -and $HookType -eq "post-run") {
    try {
        $jaegerUrl = "http://localhost:16686/api/traces?service=gemini-cli&limit=1&lookback=5m"
        $traceResp = Invoke-RestMethod -Uri $jaegerUrl -Method GET -TimeoutSec 2 -ErrorAction SilentlyContinue
        if ($traceResp -and $traceResp.data -and $traceResp.data.Count -gt 0) {
            $trace = $traceResp.data[0]
            foreach ($span in $trace.spans) {
                foreach ($tag in $span.tags) {
                    if ($tag.key -eq "gen_ai.usage.input_tokens" -and $tag.value -gt 0) {
                        $inputTokens += [int]$tag.value
                    }
                    if ($tag.key -eq "gen_ai.usage.output_tokens" -and $tag.value -gt 0) {
                        $outputTokens += [int]$tag.value
                    }
                }
            }
            if ($inputTokens -gt 0 -or $outputTokens -gt 0) {
                $tokenSource = "jaeger_otel"
            }
        }
    }
    catch {
        # Jaeger not available — silently continue
    }
}

$model = "gemini-cli"
if ($env:GEMINI_MODEL) { $model = $env:GEMINI_MODEL }
elseif ($env:LLM_MODEL) { $model = $env:LLM_MODEL }

$tool = "unknown"
if ($env:GEMINI_TOOL_NAME) { $tool = $env:GEMINI_TOOL_NAME }

$summary = if ($HookType -eq 'post-tool') {
    "Tool '$tool' executed by Gemini CLI"
} else {
    "Agent Run finished by Gemini CLI"
}

$payload = @{
    AgentName     = "Gemini-CLI"
    OutputSummary = $summary
    Status        = "Success"
    Success       = $true
    ModelName     = $model
    TokensInput   = $inputTokens
    TokensOutput  = $outputTokens
    ErrorMessage  = "token_source=$tokenSource"
}

$json = $payload | ConvertTo-Json

try {
    Invoke-RestMethod -Uri "http://localhost:8480/api/agent-runs/webhook" -Method Post -Headers @{ "Content-Type" = "application/json" } -Body $json
} catch {
    Write-Host "Failed to send hook to Briefapp: $_" -ForegroundColor Red
}
