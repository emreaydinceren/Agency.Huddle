<#
.SYNOPSIS
    Checks the test-split invariant: every baseline test exists in exactly one test assembly.

.DESCRIPTION
    Without -Lists, finds every test csproj under tests/Huddle.Tests* and runs its built executable
    (bin/<Configuration>/net10.0/<name>.exe -list tests -noLogo), which prints one fully qualified test
    method per line. Lines matching *AssemblySetupTests.* are dropped. Fails (exit 1) when a baseline
    name is missing, or when a name appears in two assemblies; warns on names not in the baseline.
    -Final also fails if tests/Huddle.Tests still exists, if a test project lacks
    TestAssemblyInitializer.cs or xunit.runner.json, or if any .cs under tests/ still contains
    "tests", "Huddle.Tests",.
    -Lists takes pre-made list files (one name per line) instead of running executables.

.PARAMETER Baseline
    File with one baseline test name per line.

.PARAMETER Lists
    Pre-made list files, one per assembly.

.PARAMETER Final
    Also run the end-state structural checks.

.PARAMETER Configuration
    Build configuration of the executables to run.

.EXAMPLE
    pwsh agents/scripts/test-split/Check-TestSplit.ps1 -Final
#>
[CmdletBinding()]
param(
    [string]$Baseline,
    [string[]]$Lists,
    [switch]$Final,
    [string]$Configuration = 'Debug'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
if ([string]::IsNullOrEmpty($Baseline)) {
    $Baseline = Join-Path $repoRoot 'Conversation/test-split/baseline-tests.txt'
}

function Select-TestNames {
    param([string[]]$Lines)
    return @($Lines |
        ForEach-Object { "$_".Trim() } |
        Where-Object { $_.Length -gt 0 -and $_ -notmatch '\s' -and $_ -notlike '*AssemblySetupTests.*' })
}

if (-not (Test-Path -LiteralPath $Baseline)) {
    Write-Host "Check-TestSplit: baseline file not found: $Baseline"
    exit 1
}

$baselineNames = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
foreach ($n in (Select-TestNames -Lines (Get-Content -LiteralPath $Baseline))) {
    [void]$baselineNames.Add($n)
}

$testsRoot = Join-Path $repoRoot 'tests'
$projectDirs = @()
if (Test-Path -LiteralPath $testsRoot) {
    $projectDirs = @(Get-ChildItem -LiteralPath $testsRoot -Directory -Filter 'Huddle.Tests*' |
        Where-Object { @(Get-ChildItem -LiteralPath $_.FullName -Filter '*.csproj' -File).Count -gt 0 })
}

# Each entry: one assembly's set of test names.
$assemblies = [System.Collections.Generic.List[System.Collections.Generic.HashSet[string]]]::new()
$failures = [System.Collections.Generic.List[string]]::new()

if ($PSBoundParameters.ContainsKey('Lists')) {
    # `pwsh -File` passes -Lists a,b as the single string "a,b", so split on commas here.
    foreach ($file in @($Lists | ForEach-Object { $_ -split ',' } | Where-Object { $_.Length -gt 0 })) {
        $set = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
        foreach ($n in (Select-TestNames -Lines (Get-Content -LiteralPath $file))) {
            [void]$set.Add($n)
        }
        $assemblies.Add($set)
    }
}
else {
    foreach ($dir in $projectDirs) {
        $name = (Get-ChildItem -LiteralPath $dir.FullName -Filter '*.csproj' -File | Select-Object -First 1).BaseName
        $exe = Join-Path $dir.FullName "bin/$Configuration/net10.0/$name.exe"
        if (-not (Test-Path -LiteralPath $exe)) {
            $failures.Add("executable not built: $exe")
            continue
        }
        $lines = & $exe -list tests -noLogo
        if ($LASTEXITCODE -ne 0) {
            $failures.Add("list failed ($LASTEXITCODE): $exe")
            continue
        }
        $set = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
        foreach ($n in (Select-TestNames -Lines $lines)) {
            [void]$set.Add($n)
        }
        $assemblies.Add($set)
    }
}

$seen = [System.Collections.Generic.Dictionary[string, int]]::new([System.StringComparer]::Ordinal)
foreach ($set in $assemblies) {
    foreach ($n in $set) {
        $seen[$n] = $(if ($seen.ContainsKey($n)) { $seen[$n] + 1 } else { 1 })
    }
}

$missing = @($baselineNames | Where-Object { -not $seen.ContainsKey($_) } | Sort-Object -CaseSensitive)
$duplicate = @($seen.Keys | Where-Object { $seen[$_] -gt 1 } | Sort-Object -CaseSensitive)
$new = @($seen.Keys | Where-Object { -not $baselineNames.Contains($_) } | Sort-Object -CaseSensitive)

foreach ($n in $missing) { $failures.Add("missing: $n") }
foreach ($n in $duplicate) { $failures.Add("duplicate: $n") }
foreach ($n in $new) { Write-Host "warning: not in baseline: $n" }

if ($Final) {
    if (Test-Path -LiteralPath (Join-Path $testsRoot 'Huddle.Tests')) {
        $failures.Add('final: tests/Huddle.Tests still exists')
    }
    foreach ($dir in $projectDirs) {
        foreach ($required in 'TestAssemblyInitializer.cs', 'xunit.runner.json') {
            if (-not (Test-Path -LiteralPath (Join-Path $dir.FullName $required))) {
                $failures.Add("final: $($dir.Name) lacks $required")
            }
        }
    }
    if (Test-Path -LiteralPath $testsRoot) {
        $stale = Get-ChildItem -LiteralPath $testsRoot -Filter '*.cs' -File -Recurse |
            Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } |
            Select-String -SimpleMatch -Pattern '"tests", "Huddle.Tests",' -List
        foreach ($hit in $stale) {
            $failures.Add("final: stale path literal in $($hit.Path)")
        }
    }
}

foreach ($f in $failures) { Write-Host $f }

$total = $seen.Count
$summary = "Check-TestSplit: $total tests in $($assemblies.Count) assemblies, missing $($missing.Count), duplicate $($duplicate.Count), new $($new.Count)"
Write-Host $summary

if ($failures.Count -gt 0) {
    exit 1
}
exit 0
