<#
.SYNOPSIS
    Tells a flaky test class from a really failing one by rerunning each failed class alone.

.DESCRIPTION
    Reads a Run-Tests.ps1 log and collects the "failed <Namespace.Class.Method> (<dur>)" lines (the
    ones Run-Tests.ps1 lists under "Failing tests:"). The class is the fully-qualified name without
    its last segment. Each distinct class is rerun -Times times on its own:

        Run-Tests.ps1 -FilterClass "*<Class>" -Label rerun-<Class>

    and the result per class is printed as
        FLAKY (n/N passed)            some runs passed and some failed: a timing, load or order issue
        REAL (failed every run)       no run passed: the class is broken
        PASSED ALONE (N/N passed)     never failed alone: the failure came from running beside others
    Exit 0 when no class is REAL, 1 otherwise. A class that is FLAKY still needs its root cause fixed;
    see agents/Testing.md, the flaky-test table.

.PARAMETER Log
    Path of a Run-Tests.ps1 log.

.PARAMETER Times
    How many times to rerun each class. Defaults to 3.

.PARAMETER ListOnly
    Parse the log and print the classes that would be rerun, without running any test.

.EXAMPLE
    pwsh agents/scripts/Rerun-Failed.ps1 -Log Conversation/logs/test-run-20261102-101500.log

.EXAMPLE
    pwsh agents/scripts/Rerun-Failed.ps1 -Log some.log -ListOnly
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$Log,
    [int]$Times = 3,
    [switch]$ListOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $Log)) {
    throw "Log '$Log' does not exist."
}

# Same shape Run-Tests.ps1 parses: "failed Agency.Huddle.Tests.Foo.Bar_Baz (12ms)", no "(tfm|arch)" segment.
$failedPattern = '^\s*failed\s+(?:\(\w+\)\s+)?(?<name>.+?)\s+\((?:\d+(?:\.\d+)?\s*(?:ms|s|m|h)\s*)+\)\s*$'
$classes = [System.Collections.Generic.SortedSet[string]]::new([StringComparer]::Ordinal)
foreach ($line in Get-Content -LiteralPath $Log) {
    if ($line -match $failedPattern) {
        # A theory row carries its arguments in parentheses, and they may hold dots: cut them first.
        $name = ($Matches['name'] -replace '\(.*$', '').Trim()
        $dot = $name.LastIndexOf('.')
        if ($dot -gt 0) {
            [void]$classes.Add($name.Substring(0, $dot))
        }
    }
}

if ($classes.Count -eq 0) {
    Write-Host 'No failed tests in the log.'
    return
}

if ($ListOnly) {
    $classes | ForEach-Object { Write-Host $_ }
    return
}

$runTests = Join-Path $PSScriptRoot 'Run-Tests.ps1'
$anyReal = $false
$verdicts = [System.Collections.Generic.List[string]]::new()
foreach ($class in $classes) {
    $short = $class.Substring($class.LastIndexOf('.') + 1)
    $passed = 0
    for ($i = 1; $i -le $Times; $i++) {
        Write-Host "Rerun $i/$Times of $class"
        & pwsh -NoProfile -File $runTests -FilterClass "*$short" -Label "rerun-$short" | Out-Host
        if ($LASTEXITCODE -eq 0) {
            $passed++
        }
    }

    if ($passed -eq 0) {
        $anyReal = $true
        $verdicts.Add("$class : REAL (failed every run)")
    }
    elseif ($passed -eq $Times) {
        $verdicts.Add("$class : PASSED ALONE ($passed/$Times passed)")
    }
    else {
        $verdicts.Add("$class : FLAKY ($passed/$Times passed)")
    }
}

Write-Host ''
$verdicts | ForEach-Object { Write-Host $_ }
if ($anyReal) {
    exit 1
}
