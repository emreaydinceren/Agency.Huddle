<#
.SYNOPSIS
    Runs Check-ManualTests.ps1 against the miniature fixture tree and diffs its output with the expected files.

.DESCRIPTION
    The fixture lives in agents/scripts/fixtures/manual-tests/. It is a miniature docs/engineering
    tree seeded with known defects. This runner invokes

        pwsh -NoProfile -File agents/scripts/Check-ManualTests.ps1 -Root <fixture>

    and compares its stdout, line by line and in order, with the concatenation of expected-a.txt and
    (when it exists) expected-b.txt, with the part A summary line `CHECK-MANUALTESTS Errors=<n>`
    replaced by one summary line carrying the total. When expected-b.txt exists, its lines are
    inserted before the summary and the summary count is the one written in the LAST expected file.

    Expected files:
      expected-a.txt   part A: ids, headers, registries; ends with the summary line
      expected-b.txt   part B (optional): links, anchors, procedure ids; ends with the summary line,
                       which then states the total for both parts

    Exits 1 on any difference or when the checker cannot run, otherwise 0. The last output line is
        TEST-CHECKMANUALTESTS Result=PASS|FAIL

.EXAMPLE
    pwsh agents/scripts/Test-CheckManualTests.ps1
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scriptsDir = $PSScriptRoot
$fixtureRoot = Join-Path $scriptsDir 'fixtures/manual-tests'
$checker = Join-Path $scriptsDir 'Check-ManualTests.ps1'

function Read-Lines {
    param([string]$Path)
    return @(Get-Content -LiteralPath $Path | Where-Object { $_.Trim().Length -gt 0 } | ForEach-Object { $_.TrimEnd() })
}

$summaryPattern = '^CHECK-MANUALTESTS Errors=\d+$'
$expected = [System.Collections.Generic.List[string]]::new()
$summary = $null
foreach ($name in @('expected-a.txt', 'expected-b.txt')) {
    $path = Join-Path $fixtureRoot $name
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        if ($name -eq 'expected-a.txt') {
            Write-Host "Missing expected file: $path"
            Write-Host 'TEST-CHECKMANUALTESTS Result=FAIL'
            exit 1
        }
        continue
    }
    foreach ($line in (Read-Lines -Path $path)) {
        if ($line -match $summaryPattern) {
            $summary = $line
        }
        else {
            $expected.Add($line)
        }
    }
}
if ($null -ne $summary) {
    $expected.Add($summary)
}

if (-not (Test-Path -LiteralPath $checker -PathType Leaf)) {
    Write-Host "Checker not found: $checker"
    Write-Host 'TEST-CHECKMANUALTESTS Result=FAIL'
    exit 1
}

$raw = & pwsh -NoProfile -File $checker -Root $fixtureRoot 2>&1
$actual = @($raw | ForEach-Object { "$_".TrimEnd() } | Where-Object { $_.Length -gt 0 })

$failed = $false
$count = [Math]::Max($expected.Count, $actual.Count)
for ($i = 0; $i -lt $count; $i++) {
    $want = if ($i -lt $expected.Count) { $expected[$i] } else { '<no line>' }
    $got = if ($i -lt $actual.Count) { $actual[$i] } else { '<no line>' }
    if (-not [string]::Equals($want, $got, [System.StringComparison]::Ordinal)) {
        $failed = $true
        Write-Host "DIFF line $($i + 1)"
        Write-Host "  expected: $want"
        Write-Host "  actual:   $got"
    }
}

if ($failed) {
    Write-Host 'TEST-CHECKMANUALTESTS Result=FAIL'
    exit 1
}

Write-Host 'TEST-CHECKMANUALTESTS Result=PASS'
exit 0
