<#
.SYNOPSIS
    Runs Check-Eol, Check-Diff, and Check-Visibility in sequence, plus a protected-file hash guard, and
    reports each script's exit code.

.DESCRIPTION
    Invokes Check-Eol.ps1 -Fix, Check-Diff.ps1 -Scope Mine, and Check-Visibility.ps1 in order via
    the $PSScriptRoot directory. Prints one "== <name> exit <code> ==" line after each script's output.
    Does not stop on first failure; continues running all three.

    Protected-file guard: if Conversation/teampages/protected.sha256 exists (relative to the git
    worktree root), each line "<sha256-hex> <repo-relative-path>" is checked - the file's SHA-256
    (lowercase hex) is recomputed and a difference prints "PROTECTED CHANGED: <path>" and sets
    Protected=1. A missing file, or a missing protected.sha256, gives Protected=0 for that entry (a
    protected path that no longer exists counts as changed). The manager re-records a hash ON PURPOSE
    when it stages that file (Get-FileHash -Algorithm SHA256, lowercase hex, forward slashes in the
    path); an agent must never edit protected.sha256 itself.

    The very last output line is always
        CHECK-ALL Eol=<n> Diff=<n> Visibility=<n> Protected=<n>
    where 0 means ok. Exits 0 only if all four are 0, otherwise with the first non-zero value, in the
    order Eol, Diff, Visibility, Protected.

.EXAMPLE
    pwsh agents/scripts/Check-All.ps1
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$exitCodes = [ordered]@{}

# Run Check-Eol -Fix
& (Join-Path $PSScriptRoot 'Check-Eol.ps1') -Fix *>&1
$exitCode = $LASTEXITCODE
if ($null -eq $exitCode) { $exitCode = 0 }
Write-Host "== Check-Eol exit $exitCode =="
$exitCodes['Eol'] = $exitCode

# Run Check-Diff -Scope Mine
& (Join-Path $PSScriptRoot 'Check-Diff.ps1') -Scope Mine *>&1
$exitCode = $LASTEXITCODE
if ($null -eq $exitCode) { $exitCode = 0 }
Write-Host "== Check-Diff exit $exitCode =="
$exitCodes['Diff'] = $exitCode

# Run Check-Visibility
& (Join-Path $PSScriptRoot 'Check-Visibility.ps1') *>&1
$exitCode = $LASTEXITCODE
if ($null -eq $exitCode) { $exitCode = 0 }
Write-Host "== Check-Visibility exit $exitCode =="
$exitCodes['Visibility'] = $exitCode

# Protected-file guard: protected.sha256 lines are "<sha256-hex> <repo-relative-path>".
$protected = 0
$repoRoot = (git rev-parse --show-toplevel).Trim()
$protectedList = Join-Path $repoRoot 'Conversation/teampages/protected.sha256'
if (Test-Path -LiteralPath $protectedList -PathType Leaf) {
    foreach ($entry in (Get-Content -LiteralPath $protectedList)) {
        if ([string]::IsNullOrWhiteSpace($entry)) {
            continue
        }
        $parts = $entry.Trim() -split '\s+', 2
        if ($parts.Count -lt 2) {
            Write-Host "PROTECTED CHANGED: (malformed line: $entry)"
            $protected = 1
            continue
        }
        $expectedHash = $parts[0].ToLowerInvariant()
        $relativePath = $parts[1].Trim()
        $fullPath = Join-Path $repoRoot $relativePath
        $actualHash = $null
        if (Test-Path -LiteralPath $fullPath -PathType Leaf) {
            $actualHash = (Get-FileHash -LiteralPath $fullPath -Algorithm SHA256).Hash.ToLowerInvariant()
        }
        if ($actualHash -ne $expectedHash) {
            Write-Host "PROTECTED CHANGED: $relativePath"
            $protected = 1
        }
    }
}
$exitCodes['Protected'] = $protected

$firstNonZero = $exitCodes.Values | Where-Object { $_ -ne 0 } | Select-Object -First 1

Write-Host "CHECK-ALL Eol=$($exitCodes['Eol']) Diff=$($exitCodes['Diff']) Visibility=$($exitCodes['Visibility']) Protected=$($exitCodes['Protected'])"

if ($null -ne $firstNonZero) {
    exit $firstNonZero
}
exit 0
