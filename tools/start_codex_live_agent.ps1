$ErrorActionPreference = "Stop"

$python = (Get-Command python -ErrorAction Stop).Source
$scriptPath = Join-Path $PSScriptRoot "codex_live_agent.py"

if (-not (Test-Path $scriptPath)) {
    throw "Live agent script not found at $scriptPath"
}

& $python $scriptPath
