$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$pythonCandidates = @(Get-Command python.exe -All -ErrorAction Stop |
    Select-Object -ExpandProperty Source |
    Where-Object { $_ -notlike '*WindowsApps*' })
$python = @($pythonCandidates | Where-Object { $_ -notlike '*hermes*' } | Select-Object -First 1)[0]
if (-not $python) {
    $python = $pythonCandidates[0]
}

$brokerOut = Join-Path $PSScriptRoot 'codex_broker.out.log'
$brokerErr = Join-Path $PSScriptRoot 'codex_broker.err.log'
$agent1Out = Join-Path $PSScriptRoot 'codex_live_agent_slot1.out.log'
$agent1Err = Join-Path $PSScriptRoot 'codex_live_agent_slot1.err.log'
$agent2Out = Join-Path $PSScriptRoot 'codex_live_agent_slot2.out.log'
$agent2Err = Join-Path $PSScriptRoot 'codex_live_agent_slot2.err.log'

Get-CimInstance Win32_Process |
    Where-Object { $_.CommandLine -like '*tools/codex_broker.py*' -or $_.CommandLine -like '*tools/codex_live_agent.py*' } |
    ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }

Get-CimInstance Win32_Process |
    Where-Object {
        $_.CommandLine -like '*start_codex_report_console.ps1*' -or
        $_.CommandLine -like '*Codex Broker Live Report*'
    } |
    ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }

foreach ($path in @($brokerOut, $brokerErr, $agent1Out, $agent1Err, $agent2Out, $agent2Err)) {
    if (Test-Path $path) {
        Remove-Item $path -Force
    }
}

$broker = Start-Process -FilePath $python -ArgumentList 'tools/codex_broker.py' `
    -WorkingDirectory $root `
    -WindowStyle Hidden `
    -RedirectStandardOutput $brokerOut `
    -RedirectStandardError $brokerErr `
    -PassThru

Start-Sleep -Seconds 1

$env:CODEX_AGENT_SLOT_ID = '1'
$env:CODEX_BOT_ID = 'slot-1-smoke'
$agent1 = Start-Process -FilePath $python -ArgumentList 'tools/codex_live_agent.py' `
    -WorkingDirectory $root `
    -WindowStyle Hidden `
    -RedirectStandardOutput $agent1Out `
    -RedirectStandardError $agent1Err `
    -PassThru

Start-Sleep -Milliseconds 500

$env:CODEX_AGENT_SLOT_ID = '2'
$env:CODEX_BOT_ID = 'slot-2-smoke'
$agent2 = Start-Process -FilePath $python -ArgumentList 'tools/codex_live_agent.py' `
    -WorkingDirectory $root `
    -WindowStyle Hidden `
    -RedirectStandardOutput $agent2Out `
    -RedirectStandardError $agent2Err `
    -PassThru

Remove-Item Env:\CODEX_AGENT_SLOT_ID -ErrorAction SilentlyContinue
Remove-Item Env:\CODEX_BOT_ID -ErrorAction SilentlyContinue

Start-Process cmd.exe -ArgumentList @(
    '/k',
    "title Codex Broker Live Report && powershell -ExecutionPolicy Bypass -NoExit -File `"$($PSScriptRoot)\start_codex_report_console.ps1`""
) | Out-Null

Write-Host "python=$python"
Write-Host "broker pid=$($broker.Id)"
Write-Host "agent slot 1 pid=$($agent1.Id)"
Write-Host "agent slot 2 pid=$($agent2.Id)"
