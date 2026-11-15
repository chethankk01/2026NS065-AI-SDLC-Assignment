<#
.SYNOPSIS
  SpendWise deterministic harness. Runs gates G1-G6 and writes machine-generated evidence.
  Usage:  powershell -File .harness\run-harness.ps1 [-Baseline]
  -Baseline  (re)records the G6 integrity baseline (tests + gate configuration). Use only
             when a change to those files is intentional AND documented in harness-log.md.
  This script is the ONLY writer of evidence/harness-results/*. Never edit those files by hand.
#>
param([switch]$Baseline)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
$results = Join-Path $root 'evidence\harness-results'
New-Item -ItemType Directory -Force $results | Out-Null

$n = 1 + @(Get-ChildItem $results -Directory -Filter 'run-*' -ErrorAction SilentlyContinue).Count
$runId = 'run-{0:D3}' -f $n
$runDir = Join-Path $results $runId
New-Item -ItemType Directory -Force $runDir | Out-Null
$sln = 'SpendWise.sln'

function Invoke-Dotnet([string]$gate, [string[]]$dotnetArgs) {
    $out = Join-Path $runDir "$gate.stdout.txt"
    $err = Join-Path $runDir "$gate.stderr.txt"
    # NOTE: do not use "Start-Process -Wait": it also waits for dotnet's long-lived build-server
    # child processes and never returns. WaitForExit() waits for this process only.
    $p = Start-Process -FilePath 'dotnet' -ArgumentList $dotnetArgs -NoNewWindow -PassThru `
        -RedirectStandardOutput $out -RedirectStandardError $err
    $null = $p.Handle   # PowerShell 5.1: cache the handle, otherwise ExitCode is $null after WaitForExit()
    $p.WaitForExit()
    return [int]$p.ExitCode
}

function Get-Text([string]$gate) {
    $t = ''
    foreach ($s in 'stdout', 'stderr') {
        $f = Join-Path $runDir "$gate.$s.txt"
        if (Test-Path $f) { $t += (Get-Content $f -Raw) }
    }
    return $t
}

# ---- G6 integrity inputs -------------------------------------------------
$guarded = @(Get-ChildItem tests -Recurse -File -Include *.cs, *.csproj | Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' }) +
           @(Get-Item .editorconfig, Directory.Build.props, global.json, .harness\CodeMetricsConfig.txt, .harness\run-harness.ps1)
$current = [ordered]@{}
foreach ($f in ($guarded | Sort-Object FullName)) {
    $rel = $f.FullName.Substring($root.Length + 1).Replace('\', '/')
    $current[$rel] = (Get-FileHash $f.FullName -Algorithm SHA256).Hash.ToLower()
}
$baselineFile = Join-Path $PSScriptRoot 'baseline.json'
if ($Baseline) {
    $current | ConvertTo-Json | Set-Content $baselineFile -Encoding utf8
    Write-Host "Integrity baseline recorded: $baselineFile"
}

$gates = @()
function Add-Gate($id, $name, $command, [int]$exit, [bool]$pass, [string]$note) {
    $script:gates += [ordered]@{
        id = $id; name = $name; command = $command; exitCode = $exit
        status = $(if ($pass) { 'PASS' } else { 'FAIL' }); note = $note
        evidence = "evidence/harness-results/$runId/$id.stdout.txt"
    }
    Write-Host ("[{0}] {1,-22} {2}" -f $(if ($pass) { 'PASS' } else { 'FAIL' }), $name, $note)
}

# ---- G1 Build (syntax/compile, analyzers off so this gate isolates compile errors)
$code = Invoke-Dotnet 'G1-build' @('build', $sln, '--no-incremental', '-warnaserror', '-p:RunAnalyzers=false', '-p:EnforceCodeStyleInBuild=false', '--nologo')
Add-Gate 'G1-build' 'Build (syntax)' 'dotnet build -warnaserror (analyzers off)' $code ($code -eq 0) "exit $code"

# ---- G2 Lint / format
$code = Invoke-Dotnet 'G2-lint' @('format', $sln, '--verify-no-changes')
Add-Gate 'G2-lint' 'Lint (dotnet format)' 'dotnet format --verify-no-changes' $code ($code -eq 0) "exit $code"

# ---- G3 SAST: full analyzer build (security category + CA1502 complexity are errors) + vulnerable packages
$code = Invoke-Dotnet 'G3a-analyzers' @('build', $sln, '--no-incremental', '-warnaserror', '--nologo')
Add-Gate 'G3a-analyzers' 'SAST (analyzers)' 'dotnet build -warnaserror (security + complexity analyzers)' $code ($code -eq 0) "exit $code"
$code = Invoke-Dotnet 'G3b-packages' @('list', $sln, 'package', '--vulnerable', '--include-transitive')
$vuln = (Get-Text 'G3b-packages') -match 'has the following vulnerable packages'
$offline = (Get-Text 'G3b-packages') -match 'error|unable to load the service index'
$note = if ($vuln) { 'vulnerable package(s) found' } elseif ($offline) { 'vulnerability source unreachable (service failure, not an app failure)' } else { 'no vulnerable packages' }
Add-Gate 'G3b-packages' 'SAST (packages)' 'dotnet list package --vulnerable' $code ($code -eq 0 -and -not $vuln -and -not $offline) $note

# ---- G4 Unit & contract tests
$code = Invoke-Dotnet 'G4-tests' @('test', $sln, '--nologo', '--verbosity', 'normal')
$t = Get-Text 'G4-tests'
$skipped = if ($t -match 'Skipped:\s+(\d+)') { [int]$Matches[1] } else { 0 }
$failedN = if ($t -match 'Failed:\s+(\d+)') { [int]$Matches[1] } else { 0 }
$passedN = if ($t -match 'Passed:\s+(\d+)') { [int]$Matches[1] } else { 0 }
Add-Gate 'G4-tests' 'Unit & contract tests' 'dotnet test' $code ($code -eq 0 -and $skipped -eq 0) "passed=$passedN failed=$failedN skipped=$skipped"

# ---- G5 Coverage >= 85% line (Core + Infrastructure)
$covOut = Join-Path $runDir 'coverage'
New-Item -ItemType Directory -Force $covOut | Out-Null
$code = Invoke-Dotnet 'G5-coverage' @('test', 'tests/SpendWise.Tests', '--nologo', '-p:CollectCoverage=true',
    '-p:Include="[SpendWise.Core]*%2c[SpendWise.Infrastructure]*"', '-p:Threshold=85', '-p:ThresholdType=line',
    '-p:ThresholdStat=total', '-p:CoverletOutputFormat=cobertura', "-p:CoverletOutput=$covOut\")
$t = Get-Text 'G5-coverage'
$pct = if ($t -match 'Total\s*\|\s*([\d\.]+)%') { $Matches[1] } else { 'n/a' }
Add-Gate 'G5-coverage' 'Coverage >= 85%' 'dotnet test + coverlet threshold' $code ($code -eq 0) "line coverage=$pct% (exit $code)"

# ---- G6 Integrity (tests / gate config unchanged vs baseline)
$drift = @()
if (Test-Path $baselineFile) {
    $base = Get-Content $baselineFile -Raw | ConvertFrom-Json
    $baseMap = @{}; $base.PSObject.Properties | ForEach-Object { $baseMap[$_.Name] = $_.Value }
    foreach ($k in $current.Keys) { if (-not $baseMap.ContainsKey($k)) { $drift += "added: $k" } elseif ($baseMap[$k] -ne $current[$k]) { $drift += "modified: $k" } }
    foreach ($k in $baseMap.Keys) { if (-not $current.Contains($k)) { $drift += "deleted: $k" } }
} else { $drift += 'no baseline recorded' }
$drift | Set-Content (Join-Path $runDir 'G6-integrity.stdout.txt') -Encoding utf8
Add-Gate 'G6-integrity' 'Test/config integrity' 'SHA-256 vs .harness/baseline.json' $(if ($drift.Count) { 1 } else { 0 }) ($drift.Count -eq 0) $(if ($drift.Count) { $drift -join '; ' } else { 'unchanged' })

# ---- Structured failure evidence (feedback for the coding agent) -----------
$failed = @($gates | Where-Object { $_.status -eq 'FAIL' })
$overall = if ($failed.Count -eq 0) { 'PASS' } else { 'FAIL' }
$fb = New-Object System.Collections.Generic.List[string]
$fb.Add("# Harness failure evidence - $runId"); $fb.Add('')
$fb.Add('Spec: specs/001-expense-budget-tracker/spec.md. Do NOT weaken tests, thresholds, analyzers or the spec to obtain PASS.'); $fb.Add('')
foreach ($g in $failed) {
    $fb.Add("## Gate $($g.id) - $($g.name)"); $fb.Add("- command: ``$($g.command)``"); $fb.Add("- exit code: $($g.exitCode)"); $fb.Add("- summary: $($g.note)")
    $fb.Add('- evidence (verbatim, de-duplicated):'); $fb.Add('```')
    $text = Get-Text $g.id
    $lines = $text -split "`r?`n" | Where-Object { $_ -match 'error [A-Za-z0-9]+:|warning [A-Za-z0-9]+:|vulnerable|High|^\s+Failed |Expected:|Actual:|Assert\.|Threshold|Total\s*\|' } |
        ForEach-Object { $_.Trim() } | Select-Object -Unique | Select-Object -First 40
    if (-not $lines -and $g.id -eq 'G6-integrity') { $lines = $drift }
    foreach ($l in $lines) { $fb.Add($l) }
    $fb.Add('```'); $fb.Add('')
}
if ($failed.Count -gt 0) { $fb | Set-Content (Join-Path $runDir 'feedback.md') -Encoding utf8 }

$summary = [ordered]@{
    runId = $runId; timestamp = (Get-Date).ToString('s'); overall = $overall
    dotnetSdk = (& dotnet --version); gates = $gates
}
$summary | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $runDir 'result.json') -Encoding utf8
$line = "| $runId | $((Get-Date).ToString('yyyy-MM-dd HH:mm')) | $overall | " + (($gates | ForEach-Object { "$($_.id)=$($_.status)" }) -join ', ') + ' |'
$idx = Join-Path $results 'index.md'
if (-not (Test-Path $idx)) { "| run | time | overall | gates |`n|---|---|---|---|" | Set-Content $idx -Encoding utf8 }
Add-Content $idx $line -Encoding utf8

Write-Host "`nOVERALL: $overall  ($runId)  -> evidence/harness-results/$runId/result.json"
if ($overall -eq 'FAIL') { Write-Host "Failure evidence: evidence/harness-results/$runId/feedback.md" }
exit $(if ($overall -eq 'PASS') { 0 } else { 1 })
