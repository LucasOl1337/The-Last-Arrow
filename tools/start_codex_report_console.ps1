$ErrorActionPreference = 'Stop'

$python = (Get-Command python -ErrorAction Stop).Source
$scriptPath = Join-Path $PSScriptRoot 'codex_report_console.py'

if (-not (Test-Path $scriptPath)) {
    throw "Report console script not found at $scriptPath"
}

$host.UI.RawUI.WindowTitle = 'Codex Thought HUD'
& $python $scriptPath
