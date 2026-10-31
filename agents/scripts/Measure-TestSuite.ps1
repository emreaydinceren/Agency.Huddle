<#
.SYNOPSIS
    Records the test-suite metrics of the Test Project Split (clean build, incremental test build, full
    run, per-assembly duration and test count, disk) under a label, into one markdown table.

.DESCRIPTION
    Measures the same way before and after the split (plan tasks 3, 13 and 36):

      - Clean build:            `dotnet clean Huddle.slnx -v q`, then Build.ps1, wall time.
                                -SkipClean skips the row and writes `skipped`.
      - Incremental test build: finds the project holding TaskStoreTests with Find-TestProject.ps1,
                                sets the LastWriteTime of TaskStoreTests.cs to now (never edits it),
                                then times Build.ps1.
      - Full run:               Run-Tests.ps1 -Label <label>, wall time and total/failed/skipped from the
                                run's log.
      - Per assembly:           one row per test assembly: tests and duration. The duration comes from the
                                `<dll> (net10.0|x64) passed (<dur>)` lines of the Run-Tests log. The log
                                carries no per-assembly count, so the count comes from running the built
                                test executable with `--list-tests` (n/a if that fails, and always n/a
                                under -ParseOnly).
      - Disk:                   summed size of every tests/*/bin folder, in MB.

    Writes <main checkout>\Conversation\test-split\metrics-<label>.md. The main checkout is resolved via
    `git rev-parse --path-format=absolute --git-common-dir` (its parent), like Run-Tests.ps1.

    It calls Build.ps1 and Run-Tests.ps1 rather than dotnet test/build directly. Run-Tests.ps1 takes the
    machine-global test mutex itself; do not run this beside another build or test run.

.PARAMETER Label
    Name of the measurement, used in the output file name and as the Run-Tests label. Required unless
    -ParseOnly is given.

.PARAMETER SkipClean
    Skips the clean build row (writes `skipped`).

.PARAMETER ParseOnly
    Path of an existing Run-Tests log. Only parses it into the per-assembly table and totals and prints
    them; builds, runs and writes nothing.

.PARAMETER MainRepoRoot
    Override for the main checkout's root. Defaults to the parent of the git common dir.

.EXAMPLE
    pwsh agents/scripts/Measure-TestSuite.ps1 -Label baseline

.EXAMPLE
    pwsh agents/scripts/Measure-TestSuite.ps1 -Label smoke -SkipClean

.EXAMPLE
    pwsh agents/scripts/Measure-TestSuite.ps1 -ParseOnly Conversation\logs\baseline-20261030-231141.log
#>
[CmdletBinding()]
param(
    [string]$Label,
    [switch]$SkipClean,
    [string]$ParseOnly,
    [string]$MainRepoRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

<#
.SYNOPSIS
    Converts a dotnet test duration such as "1m 17s 982ms" into seconds.
#>
function ConvertTo-Seconds {
    param([string]$Text)
    $seconds = 0.0
    foreach ($m in [regex]::Matches($Text, '(?<n>\d+(?:\.\d+)?)\s*(?<u>ms|h|m|s)')) {
        $n = [double]::Parse($m.Groups['n'].Value, [System.Globalization.CultureInfo]::InvariantCulture)
        switch ($m.Groups['u'].Value) {
            'h' { $seconds += $n * 3600 }
            'm' { $seconds += $n * 60 }
            's' { $seconds += $n }
            'ms' { $seconds += $n / 1000 }
        }
    }
    return $seconds
}

<#
.SYNOPSIS
    Parses a Run-Tests log into the per-assembly rows and the totals block.

.DESCRIPTION
    Per-assembly lines look like "<path>\<Name>.dll (net10.0|x64) passed (1m 17s 982ms)". The same lines
    are printed twice (once as each assembly finishes, once in the closing summary), so they are
    de-duplicated by assembly path, keeping the last. Totals come from the "  total: N" block.
#>
function Get-LogMetrics {
    param([string]$Path)
    $assemblies = [ordered]@{}
    $totals = @{ total = $null; failed = $null; succeeded = $null; skipped = $null; duration = $null }
    foreach ($line in (Get-Content -LiteralPath $Path)) {
        if ($line -match '^\s*(?<path>\S.*?\.dll)\s+\((?<tfm>[^)]+)\)\s+(?<status>passed|failed|Zero tests ran)\s+\((?<dur>[^)]+)\)\s*$') {
            $dll = $Matches.path
            $assemblies[$dll] = [PSCustomObject]@{
                Name    = [System.IO.Path]::GetFileNameWithoutExtension($dll)
                Dll     = $dll
                Status  = $Matches.status
                Seconds = ConvertTo-Seconds $Matches.dur
            }
            continue
        }
        if ($line -match '^\s*(?<key>total|failed|succeeded|skipped|duration):\s*(?<value>.+?)\s*$') {
            $totals[$Matches.key] = $Matches.value
        }
    }
    [PSCustomObject]@{
        Assemblies = @($assemblies.Values | Sort-Object Name)
        Totals     = $totals
    }
}

<#
.SYNOPSIS
    Counts the tests of a built test assembly by running its executable with --list-tests.
    Returns 'n/a' when the executable is missing or prints no test list.
#>
function Get-TestCount {
    param([string]$Dll)
    $exe = [System.IO.Path]::ChangeExtension($Dll, '.exe')
    if (-not (Test-Path -LiteralPath $exe)) {
        return 'n/a'
    }
    try {
        $out = @(& $exe --list-tests 2>&1 | ForEach-Object { "$_" })
    }
    catch {
        return 'n/a'
    }
    $start = [array]::FindIndex($out, [Predicate[string]] { param($l) $l -match 'tests are available' })
    if ($start -lt 0) {
        return 'n/a'
    }
    $count = 0
    for ($i = $start + 1; $i -lt $out.Count; $i++) {
        if ($out[$i] -match '^\s*\S') {
            $count++
        }
    }
    return [string]$count
}

function Format-Seconds {
    param([double]$Seconds)
    return $Seconds.ToString('0.0', [System.Globalization.CultureInfo]::InvariantCulture)
}

function Format-AssemblyTable {
    param($Assemblies, [hashtable]$Counts)
    $rows = New-Object System.Collections.Generic.List[string]
    $rows.Add('| Assembly | Tests | Duration (s) | Result |')
    $rows.Add('| --- | ---: | ---: | --- |')
    foreach ($a in $Assemblies) {
        $tests = if ($Counts.ContainsKey($a.Name)) { $Counts[$a.Name] } else { 'n/a' }
        $rows.Add("| $($a.Name) | $tests | $(Format-Seconds $a.Seconds) | $($a.Status) |")
    }
    return $rows
}

if ($ParseOnly) {
    $metrics = Get-LogMetrics -Path $ParseOnly
    $t = $metrics.Totals
    Write-Host "Totals: total=$($t.total) failed=$($t.failed) succeeded=$($t.succeeded) skipped=$($t.skipped) duration=$($t.duration)"
    Format-AssemblyTable -Assemblies $metrics.Assemblies -Counts @{} | ForEach-Object { Write-Host $_ }
    return
}

if (-not $Label) {
    throw 'Measure-TestSuite: -Label is required (or use -ParseOnly <log>).'
}

if (-not $MainRepoRoot) {
    $gitCommonDir = (git rev-parse --path-format=absolute --git-common-dir).Trim()
    $MainRepoRoot = (Resolve-Path -LiteralPath (Join-Path $gitCommonDir '..')).Path
}
$repoRoot = (git rev-parse --show-toplevel).Trim()
$scripts = Join-Path $repoRoot 'agents\scripts'
$outDir = Join-Path $MainRepoRoot 'Conversation\test-split'
$outFile = Join-Path $outDir "metrics-$Label.md"
$solution = Join-Path $repoRoot 'Huddle.slnx'

function Measure-Script {
    param([string]$Script, [string[]]$ScriptArgs = @())
    $watch = [System.Diagnostics.Stopwatch]::StartNew()
    $output = @(& pwsh -NoProfile -File (Join-Path $scripts $Script) @ScriptArgs 2>&1 | ForEach-Object { "$_" })
    $watch.Stop()
    [PSCustomObject]@{ Seconds = $watch.Elapsed.TotalSeconds; ExitCode = $LASTEXITCODE; Output = $output }
}

# Clean build.
$cleanCell = 'skipped'
if (-not $SkipClean) {
    Write-Host 'Clean build: dotnet clean, then Build.ps1'
    & dotnet clean $solution -v q | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet clean failed with exit code $LASTEXITCODE"
    }
    $clean = Measure-Script -Script 'Build.ps1'
    if ($clean.ExitCode -ne 0) {
        throw "Clean build failed (exit $($clean.ExitCode)):`n$($clean.Output -join "`n")"
    }
    $cleanCell = Format-Seconds $clean.Seconds
}

# Incremental test build: touch TaskStoreTests.cs, never edit it.
Write-Host 'Incremental test build: touching TaskStoreTests.cs'
$project = @(& pwsh -NoProfile -File (Join-Path $scripts 'Find-TestProject.ps1') -Class TaskStoreTests)
if ($project.Count -eq 0) {
    throw 'Find-TestProject.ps1 found no project holding TaskStoreTests.'
}
$projectDir = Split-Path -Parent ([string]$project[0])
$touched = @(Get-ChildItem -LiteralPath $projectDir -Filter 'TaskStoreTests.cs' -File -Recurse |
        Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' })
if ($touched.Count -ne 1) {
    throw "Expected exactly one TaskStoreTests.cs under $projectDir, found $($touched.Count)."
}
$touched[0].LastWriteTime = Get-Date
$incremental = Measure-Script -Script 'Build.ps1'
if ($incremental.ExitCode -ne 0) {
    throw "Incremental build failed (exit $($incremental.ExitCode)):`n$($incremental.Output -join "`n")"
}

# Full run. A red run is still measured; the counts say so.
Write-Host "Full run: Run-Tests.ps1 -Label $Label"
$run = Measure-Script -Script 'Run-Tests.ps1' -ScriptArgs @('-Label', $Label)
$logLine = @($run.Output | Where-Object { $_ -match '^Log:\s' } | ForEach-Object { ($_ -replace '^Log:\s*', '').Trim() })
if ($logLine.Count -eq 0) {
    throw "Run-Tests.ps1 printed no 'Log:' line (exit $($run.ExitCode)):`n$($run.Output | Select-Object -Last 20 | Out-String)"
}
$logPath = $logLine[-1]
$metrics = Get-LogMetrics -Path $logPath
$t = $metrics.Totals

$counts = @{}
foreach ($a in $metrics.Assemblies) {
    $counts[$a.Name] = Get-TestCount -Dll $a.Dll
}

# Disk: every tests/*/bin folder.
$bytes = 0L
foreach ($bin in Get-ChildItem -Path (Join-Path $repoRoot 'tests') -Directory -ErrorAction SilentlyContinue |
        ForEach-Object { Join-Path $_.FullName 'bin' } | Where-Object { Test-Path -LiteralPath $_ }) {
    $sum = (Get-ChildItem -LiteralPath $bin -Recurse -File -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum
    if ($sum) {
        $bytes += [long]$sum
    }
}
$diskMb = [Math]::Round($bytes / 1MB, 1).ToString('0.0', [System.Globalization.CultureInfo]::InvariantCulture)

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add("# Test suite metrics: $Label")
$lines.Add('')
$lines.Add("Measured $(Get-Date -Format 'yyyy-MM-dd HH:mm') on branch $((git rev-parse --abbrev-ref HEAD).Trim()). Run log: ``$logPath``.")
$lines.Add('')
$lines.Add('| Metric | Value |')
$lines.Add('| --- | ---: |')
$lines.Add("| Clean build (s) | $cleanCell |")
$lines.Add("| Incremental test build (s) | $(Format-Seconds $incremental.Seconds) |")
$lines.Add("| Full run (s) | $(Format-Seconds $run.Seconds) |")
$lines.Add("| Tests total | $($t.total) |")
$lines.Add("| Tests failed | $($t.failed) |")
$lines.Add("| Tests skipped | $($t.skipped) |")
$lines.Add("| tests/*/bin size (MB) | $diskMb |")
$lines.Add('')
foreach ($row in (Format-AssemblyTable -Assemblies $metrics.Assemblies -Counts $counts)) {
    $lines.Add($row)
}

New-Item -ItemType Directory -Force -Path $outDir | Out-Null
Set-Content -LiteralPath $outFile -Value $lines -Encoding utf8
Write-Host "Wrote $outFile"
$lines | ForEach-Object { Write-Host $_ }
if ($run.ExitCode -ne 0) {
    Write-Host "NOTE: Run-Tests.ps1 exited $($run.ExitCode); the figures are from a non-green run."
    exit $run.ExitCode
}
