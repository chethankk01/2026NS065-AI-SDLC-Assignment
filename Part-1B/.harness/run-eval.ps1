<#
.SYNOPSIS
  TicketTriage evaluation harness wrapper.
  Usage:  powershell -ExecutionPolicy Bypass -File .harness\run-eval.ps1 [-Judge] [-Repeat 2] [-DelayMs 6000] [-Limit 0]
  Steps : 1) build (warnings as errors)  2) offline unit tests  3) live evaluation against evals/dataset.json
  Needs : environment variables GEMINI_API_KEY (required for the live step) and optionally GEMINI_MODEL (default gemini-pro-latest).
  Output: evidence/runs/run-NNN/{eval-results.json, feedback.md (if not PASS), build.log, tests.log, eval.log}
          and a copy of the latest result at evidence/eval-results.json.  Never edit these files by hand.
#>
param([switch]$Judge, [int]$Repeat = 1, [int]$DelayMs = 6000, [int]$Limit = 0)

$ErrorActionPreference = 'Continue'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
$tmp = Join-Path ([IO.Path]::GetTempPath()) ('tt-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $tmp | Out-Null

if (-not $env:GEMINI_API_KEY) { Write-Host 'WARNING: GEMINI_API_KEY is not set in this session; live calls will be SERVICE_ERROR (INCONCLUSIVE).' }

Write-Host '== 1/3 Build (warnings as errors) =='
dotnet build TicketTriage.sln --nologo -warnaserror 2>&1 | Out-String | Tee-Object -FilePath "$tmp\build.log" | Select-Object -Last 6 | Write-Host
$buildExit = $LASTEXITCODE
if ($buildExit -ne 0) { Write-Host "BUILD FAILED (exit $buildExit) - see log. Evaluation not run."; Copy-Item "$tmp\build.log" (Join-Path $root 'evidence\last-build-failure.log') -Force; exit 2 }

Write-Host '== 2/3 Offline unit tests =='
dotnet test tests/TicketTriage.Tests --no-build --nologo 2>&1 | Out-String | Tee-Object -FilePath "$tmp\tests.log" | Select-String -Pattern 'Passed!|Failed!|error' | ForEach-Object { Write-Host $_.Line }
$testExit = $LASTEXITCODE
if ($testExit -ne 0) { Write-Host "UNIT TESTS FAILED (exit $testExit). Evaluation not run."; Copy-Item "$tmp\tests.log" (Join-Path $root 'evidence\last-test-failure.log') -Force; exit 2 }

Write-Host '== 3/3 Live evaluation =='
$evalArgs = @('run', '--project', '.harness/TicketTriage.Harness', '--no-build', '--', 'eval', '--delay-ms', $DelayMs, '--repeat', $Repeat)
if ($Judge) { $evalArgs += '--judge' }
if ($Limit -gt 0) { $evalArgs += @('--limit', $Limit) }
dotnet @evalArgs | Tee-Object -FilePath "$tmp\eval.log"
$evalExit = $LASTEXITCODE

$runs = Join-Path $root 'evidence\runs'
$latest = Get-ChildItem $runs -Directory -Filter 'run-*' -ErrorAction SilentlyContinue | Sort-Object Name | Select-Object -Last 1
if ($latest) {
    foreach ($f in 'build.log', 'tests.log', 'eval.log') { Copy-Item "$tmp\$f" (Join-Path $latest.FullName $f) -Force }
    $result = Get-Content (Join-Path $latest.FullName 'eval-results.json') -Raw | ConvertFrom-Json
    $line = "| $($latest.Name) | $((Get-Date).ToString('yyyy-MM-dd HH:mm')) | $($result.config.model) | $($result.decision) | scored=$($result.aggregate.scoredCases)/$($result.aggregate.totalCases) serviceErrors=$($result.aggregate.serviceErrors) casePass=$($result.aggregate.casePassRate) guardrail=$($result.aggregate.guardrailPassRate) meanS=$($result.aggregate.meanSemanticScore) |"
    $idx = Join-Path $runs 'index.md'
    if (-not (Test-Path $idx)) { "| run | time | model | decision | metrics |`n|---|---|---|---|---|" | Set-Content $idx -Encoding utf8 }
    Add-Content $idx $line -Encoding utf8
}
Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
exit $evalExit
