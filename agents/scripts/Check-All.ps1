<#
.SYNOPSIS
    Runs Check-Eol, Check-Diff, and Check-Visibility in sequence, reporting each script's exit code.

.DESCRIPTION
    Invokes Check-Eol.ps1 -Fix, Check-Diff.ps1 -Scope Mine, and Check-Visibility.ps1 in order via
    the $PSScriptRoot directory. Prints a one-line header before each script's output showing the
    script name and exit code. Does not stop on first failure; continues running all three. Exits
    with 0 only if all three scripts exited 0, otherwise exits with the first non-zero exit code.

.EXAMPLE
    pwsh agents/scripts/Check-All.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$exitCodes = @()

# Run Check-Eol -Fix
Write-Host "== Check-Eol exit ? =="
& (Join-Path $PSScriptRoot 'Check-Eol.ps1') -Fix *>&1
$exitCode = $LASTEXITCODE
if ($null -eq $exitCode) { $exitCode = 0 }
Write-Host "== Check-Eol exit $exitCode =="
$exitCodes += $exitCode

# Run Check-Diff -Scope Mine
Write-Host "== Check-Diff exit ? =="
& (Join-Path $PSScriptRoot 'Check-Diff.ps1') -Scope Mine *>&1
$exitCode = $LASTEXITCODE
if ($null -eq $exitCode) { $exitCode = 0 }
Write-Host "== Check-Diff exit $exitCode =="
$exitCodes += $exitCode

# Run Check-Visibility
Write-Host "== Check-Visibility exit ? =="
& (Join-Path $PSScriptRoot 'Check-Visibility.ps1') *>&1
$exitCode = $LASTEXITCODE
if ($null -eq $exitCode) { $exitCode = 0 }
Write-Host "== Check-Visibility exit $exitCode =="
$exitCodes += $exitCode

$firstNonZero = $exitCodes | Where-Object { $_ -ne 0 } | Select-Object -First 1
if ($null -ne $firstNonZero) {
    exit $firstNonZero
}
exit 0
