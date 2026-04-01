$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$brokerOut = Join-Path $PSScriptRoot 'codex_broker.out.log'
$brokerErr = Join-Path $PSScriptRoot 'codex_broker.err.log'
$agentOut = Join-Path $PSScriptRoot 'codex_live_agent.out.log'
$agentErr = Join-Path $PSScriptRoot 'codex_live_agent.err.log'

Get-CimInstance Win32_Process |
    Where-Object { $_.CommandLine -like '*tools/codex_broker.py*' -or $_.CommandLine -like '*tools/codex_live_agent.py*' } |
    ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }

Get-CimInstance Win32_Process |
    Where-Object {
        $_.CommandLine -like '*start_codex_report_console.ps1*' -or
        $_.CommandLine -like '*Codex Broker Live Report*'
    } |
    ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }

foreach ($path in @($brokerOut, $brokerErr, $agentOut, $agentErr)) {
    if (Test-Path $path) {
        Remove-Item $path -Force
    }
}

$broker = Start-Process python -ArgumentList 'tools/codex_broker.py' `
    -WorkingDirectory $root `
    -WindowStyle Hidden `
    -RedirectStandardOutput $brokerOut `
    -RedirectStandardError $brokerErr `
    -PassThru

Start-Sleep -Seconds 1

$agent = Start-Process python -ArgumentList 'tools/codex_live_agent.py' `
    -WorkingDirectory $root `
    -WindowStyle Hidden `
    -RedirectStandardOutput $agentOut `
    -RedirectStandardError $agentErr `
    -PassThru

Start-Process cmd.exe -ArgumentList @(
    '/k',
    "title Codex Broker Live Report && powershell -ExecutionPolicy Bypass -NoExit -File `"$($PSScriptRoot)\start_codex_report_console.ps1`""
) | Out-Null

Write-Host "broker pid=$($broker.Id)"
Write-Host "agent pid=$($agent.Id)"
