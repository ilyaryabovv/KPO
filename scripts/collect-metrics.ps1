param(
    [string]$Stage = "infrastructure",
    [string]$Since = "2026-10-15",
    [string]$Until = "2026-10-16",
    [string]$Output = ""
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $root

if ([string]::IsNullOrWhiteSpace($Output)) {
    $Output = Join-Path $root "docs/metrics/$Stage.json"
}

New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Output) | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $root "artifacts") | Out-Null

$started = Get-Date
$lintErrors = 0
$lintWarnings = 0

Write-Host "dotnet format"
& dotnet format "server/FitnessCenter.slnx" --verify-no-changes --verbosity diagnostic
if ($LASTEXITCODE -ne 0) {
    $lintErrors += 1
}

Write-Host "dotnet build"
$buildOutput = & dotnet build "server/FitnessCenter.slnx" -c Release 2>&1 | Out-String
$lintWarnings += ([regex]::Matches($buildOutput, ": warning ")).Count
$lintErrors += ([regex]::Matches($buildOutput, ": error ")).Count

$clientLine = $null
$clientBranch = $null
$clientPassed = 0
$clientFailed = 0
$clientDuration = 0.0

if (Test-Path (Join-Path $root "client/angular.json")) {
    Write-Host "eslint"
    Push-Location (Join-Path $root "client")
    $eslintPath = Join-Path $root "artifacts/eslint.json"
    & npx eslint "src/**/*.ts" "src/**/*.html" --format json --output-file $eslintPath
    $lintExit = $LASTEXITCODE
    Pop-Location
    if (Test-Path $eslintPath) {
        $eslint = Get-Content $eslintPath -Raw | ConvertFrom-Json
        foreach ($file in @($eslint)) {
            $lintErrors += @($file.messages | Where-Object { $_.severity -eq 2 }).Count
            $lintWarnings += @($file.messages | Where-Object { $_.severity -eq 1 }).Count
        }
    }
    elseif ($lintExit -ne 0) {
        $lintErrors += 1
    }
}

if (Test-Path (Join-Path $root "TestResults")) {
    Remove-Item (Join-Path $root "TestResults") -Recurse -Force
}

Write-Host "dotnet test"
& dotnet test "server/FitnessCenter.slnx" -c Release --collect:"XPlat Code Coverage" --results-directory "TestResults" --logger "trx;LogFileName=server.trx"
if ($LASTEXITCODE -ne 0 -and -not (Test-Path (Join-Path $root "TestResults/server.trx"))) {
    throw "dotnet test failed and did not write TestResults/server.trx"
}

[xml]$trx = Get-Content (Join-Path $root "TestResults/server.trx")
$counters = $trx.TestRun.ResultSummary.Counters
$times = $trx.TestRun.Times
$serverPassed = [int]$counters.passed
$serverFailed = [int]$counters.failed
$serverDuration = 0.0
if ($times.start -and $times.finish) {
    $serverDuration = ([datetime]$times.finish - [datetime]$times.start).TotalSeconds
}

$coverageFile = Get-ChildItem -Path (Join-Path $root "TestResults") -Recurse -Filter "coverage.cobertura.xml" | Select-Object -First 1
$serverLine = 0.0
$serverBranch = 0.0
if ($null -ne $coverageFile) {
    [xml]$coverageXml = Get-Content $coverageFile.FullName
    $serverLine = [math]::Round([double]$coverageXml.coverage.GetAttribute("line-rate") * 100, 2)
    $serverBranch = [math]::Round([double]$coverageXml.coverage.GetAttribute("branch-rate") * 100, 2)
}

if (Test-Path (Join-Path $root "client/angular.json")) {
    Write-Host "ng test"
    Push-Location (Join-Path $root "client")
    $clientOutput = & npx ng test --watch=false --coverage 2>&1 | Out-String
    $clientExit = $LASTEXITCODE
    Pop-Location
    Write-Host $clientOutput
    $testsLine = @($clientOutput -split "`r?`n" | Where-Object { $_ -match "^\s*Tests\s+" } | Select-Object -First 1)
    if ($testsLine) {
        $passedMatch = [regex]::Match($testsLine, "(\d+)\s+passed")
        $failedMatch = [regex]::Match($testsLine, "(\d+)\s+failed")
        if ($passedMatch.Success) { $clientPassed = [int]$passedMatch.Groups[1].Value }
        if ($failedMatch.Success) { $clientFailed = [int]$failedMatch.Groups[1].Value }
    }
    elseif ($clientExit -ne 0) {
        $clientFailed = 1
    }
    $durationMatch = [regex]::Match($clientOutput, "Duration\s+([0-9]+(?:[.,][0-9]+)?)s")
    if ($durationMatch.Success) {
        $clientDuration = [double]($durationMatch.Groups[1].Value.Replace(",", "."))
    }

    $summaryPath = Get-ChildItem -Path (Join-Path $root "client/coverage") -Recurse -Filter "coverage-summary.json" -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -ne $summaryPath) {
        $summary = Get-Content $summaryPath.FullName -Raw | ConvertFrom-Json
        $clientLine = [math]::Round([double]$summary.total.lines.pct, 2)
        $clientBranch = [math]::Round([double]$summary.total.branches.pct, 2)
    }
    if ($null -eq $clientLine) {
        $lineMatch = [regex]::Match($clientOutput, "Lines\s+:\s+([0-9]+(?:[.,][0-9]+)?)%")
        $branchMatch = [regex]::Match($clientOutput, "Branches\s+:\s+([0-9]+(?:[.,][0-9]+)?)%")
        if ($lineMatch.Success) {
            $clientLine = [double]($lineMatch.Groups[1].Value.Replace(",", "."))
        }
        if ($branchMatch.Success) {
            $clientBranch = [double]($branchMatch.Groups[1].Value.Replace(",", "."))
        }
    }
}

$commitLines = @(git -C $root log --since="$Since" --until="$Until" --pretty=format:"%h")
$commitCount = @($commitLines | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }).Count
$elapsed = [math]::Round(((Get-Date) - $started).TotalSeconds, 2)

$payload = [ordered]@{
    stage = $Stage
    measured_at = (Get-Date).ToString("yyyy-MM-dd")
    period_since = $Since
    period_until = $Until
    lint_errors = $lintErrors
    lint_warnings = $lintWarnings
    server_line_coverage_percent = $serverLine
    server_branch_coverage_percent = $serverBranch
    client_line_coverage_percent = $clientLine
    client_branch_coverage_percent = $clientBranch
    tests_passed = $serverPassed + $clientPassed
    tests_failed = $serverFailed + $clientFailed
    test_duration_sec = [math]::Round($serverDuration + $clientDuration, 2)
    commits_in_period = $commitCount
    pipeline_duration_sec = $elapsed
    note = "Local snapshot. Pipeline duration here is the collection script runtime. On GitHub Actions the metrics job overwrites it with the workflow runtime."
}

$json = $payload | ConvertTo-Json -Depth 5
Set-Content -Path $Output -Value $json -Encoding utf8
Write-Host $json
Write-Host "Wrote $Output"
