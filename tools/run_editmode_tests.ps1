# Run the full ProjectPVP EditMode test suite in Unity batch mode.
#
# WHY THIS SCRIPT EXISTS:
#   The bare `Unity.exe -batchmode -runTests -quit ...` invocation does NOT
#   execute the test runner on this project: Unity quits immediately after the
#   asset import finishes, before the test runner attaches, and produces NO
#   -testResults XML. This previously led to either (a) a misleading code-0
#   exit with no results, or (b) reliance on cherry-picked -executeMethod
#   runners (e.g. ProjectPvpCodexFocusedTestRunner) that only cover a subset.
#
#   The correct invocation requires:
#     1. NO -quit flag (the batch quit races with test runner attachment).
#     2. -testFilter set to the test assembly so the runner binds the suite.
#
#   Verified 2026-06-19: full suite green (see Logs/redteam-editmode-full.xml).
#   Test count grows over time; the authoritative number is whatever the XML reports.

param(
    [string]$ProjectPath = "C:\Projetos\The-Last-Arrow",
    [string]$UnityExe = "C:\Program Files\Unity\Hub\Editor\6000.3.11f1\Editor\Unity.exe",
    [string]$ResultsXml = "C:\Projetos\The-Last-Arrow\Logs\redteam-editmode-full.xml",
    [string]$LogFile = "C:\Projetos\The-Last-Arrow\Logs\redteam-editmode-full.log"
)

$ErrorActionPreference = "Stop"

# Delete stale results so a missing XML after the run is an unambiguous failure signal.
Remove-Item -Path $ResultsXml -ErrorAction SilentlyContinue
Remove-Item -Path $LogFile -ErrorAction SilentlyContinue

Write-Host "[run_editmode_tests] launching Unity batch EditMode suite..."
Write-Host "[run_editmode_tests] unity:    $UnityExe"
Write-Host "[run_editmode_tests] project:  $ProjectPath"
Write-Host "[run_editmode_tests] results:  $ResultsXml"

# NOTE: deliberately NO -quit (it races the test runner and produces no XML).
& $UnityExe `
    -batchmode `
    -nographics `
    -projectPath $ProjectPath `
    -runTests `
    -testPlatform editmode `
    -testFilter "ProjectPVP.Runtime.EditorTests" `
    -testResults $ResultsXml `
    -logfile $LogFile
$unityExit = $LASTEXITCODE

# The call operator returns before Unity's async test runner flushes the results XML (the no-`-quit`
# launch is correct, but the file write lands on a later frame). Wait ONLY for batch-mode Unity
# processes (IsHumanControllingUs:0) to exit — never block the user's interactive Editor — then
# bounded-retry the XML check so a genuine green run is not reported as a false-RED exit 3.
$batchUnity = Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" -ErrorAction SilentlyContinue |
    Where-Object { $_.CommandLine -like "*-batchmode*" -and $_.CommandLine -like "*$ProjectPath*" }
foreach ($p in $batchUnity) {
    Wait-Process -Id $p.ProcessId -ErrorAction SilentlyContinue
}

$xmlReady = $false
for ($i = 0; $i -lt 60; $i++) {
    if (Test-Path $ResultsXml) { $xmlReady = $true; break }
    Start-Sleep -Seconds 1
}
Write-Host "[run_editmode_tests] unity exit code: $unityExit (xml-ready: $xmlReady)"

if (-not $xmlReady) {
    Write-Error "[run_editmode_tests] FAIL: no results XML produced at $ResultsXml after waiting for Unity exit + 60s. See $LogFile."
    exit 3
}

# Parse the XML summary line for the authoritative pass/fail counts.
[xml]$doc = Get-Content -Raw -Path $ResultsXml
$tr = $doc."test-run"
$total  = [int]$tr.total
$passed = [int]$tr.passed
$failed = [int]$tr.failed
$result = $tr.result
Write-Host "[run_editmode_tests] result=$result total=$total passed=$passed failed=$failed"

if ($failed -gt 0 -or $result -ne "Passed") {
    Write-Error "[run_editmode_tests] FAIL: $failed test(s) failed (result=$result)."
    exit 2
}

Write-Host "[run_editmode_tests] OK: $passed/$passed passed."
exit 0
