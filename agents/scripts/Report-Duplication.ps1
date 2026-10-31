<#
.SYNOPSIS
    Advisory: lists copy-pasted C# blocks (SonarQube-style duplication) under src/ as refactoring
    candidates. It never fails a build or a check.

.DESCRIPTION
    Runs Find-Duplicates.cs (a Roslyn token matcher run with `dotnet run`) over every tracked and
    untracked .cs file under src/, and with -IncludeTests under tests/ as well. A duplicate is a run of
    at least -MinTokens identical tokens spanning at least -MinLines lines; string, character and
    numeric literals are normalised and identifiers are kept, the same definition SonarQube's CPD uses
    for C#.

    Duplicates this branch introduced are listed first under "Added on this branch": a pair counts as
    added when at least half the lines of one of its sides are new against the merge-base with -Base
    (committed and uncommitted changes, plus untracked files) - which is what a copy-paste looks like,
    so editing a line inside an old duplicated block does not move it there. Every other duplicate
    follows under "Existing", longest first, then a summary with the duplicated-line density.

    Not every duplicate is worth extracting: two tools with the same shape, or a data table, can read
    better as copies. Treat the list as advice and judge each row.

    Exits 0, or 2 when the matcher itself fails.

.PARAMETER Base
    The branch to diff against when deciding what this branch added. Defaults to main.

.PARAMETER IncludeTests
    Also scan tests/. Off by default: test arrange code is repetitive by design (about 7% duplicated
    lines against about 1.5% in src/ when this script was written), and SonarQube leaves tests out of
    its duplication metric too.

.PARAMETER MinTokens
    Smallest duplicate, in tokens. Defaults to 100, SonarQube's value for C#.

.PARAMETER MinLines
    Smallest duplicate, in lines. Defaults to 10, SonarQube's value for C#.

.PARAMETER Top
    Show at most this many existing duplicates. Defaults to 0, meaning all of them.

.EXAMPLE
    pwsh agents/scripts/Report-Duplication.ps1

.EXAMPLE
    pwsh agents/scripts/Report-Duplication.ps1 -IncludeTests -Top 20
#>
[CmdletBinding()]
param(
    [string]$Base = 'main',
    [switch]$IncludeTests,
    [int]$MinTokens = 100,
    [int]$MinLines = 10,
    [int]$Top = 0
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (git rev-parse --show-toplevel).Trim()
Push-Location -LiteralPath $repoRoot
try {
    $pathSpecs = @('src/*.cs')
    if ($IncludeTests) {
        $pathSpecs += 'tests/*.cs'
    }

    $untracked = @(git ls-files --others --exclude-standard -- $pathSpecs)
    $files = @(git ls-files -- $pathSpecs) + $untracked

    $engine = Join-Path $PSScriptRoot 'Find-Duplicates.cs'
    $rows = @($files | dotnet run $engine -- --min-tokens $MinTokens --min-lines $MinLines)
    if ($LASTEXITCODE -ne 0) {
        $rows | ForEach-Object { Write-Host $_ }
        Write-Host "Report-Duplication: Find-Duplicates.cs failed with exit $LASTEXITCODE."
        exit 2
    }

    # file -> set of added line numbers (null means "every line", for an untracked file). With no
    # merge-base (a detached or unrelated history) nothing counts as added and every row is Existing.
    $added = @{}
    $mergeBase = (git merge-base HEAD $Base 2>$null)
    if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($mergeBase)) {
        $currentFile = $null
        foreach ($line in (git -c diff.renameLimit=5000 diff -M -U0 --no-color $mergeBase.Trim() -- $pathSpecs)) {
            if ($line -match '^\+\+\+ b/(?<path>.+)$') {
                $currentFile = $Matches['path']
                $added[$currentFile] = [System.Collections.Generic.HashSet[int]]::new()
            }
            elseif ($line -match '^@@ -\d+(?:,\d+)? \+(?<start>\d+)(?:,(?<count>\d+))? @@' -and $null -ne $currentFile) {
                $start = [int]$Matches['start']
                $count = if ($Matches['count']) { [int]$Matches['count'] } else { 1 }
                for ($n = $start; $n -lt ($start + $count); $n++) {
                    [void]$added[$currentFile].Add($n)
                }
            }
        }

        foreach ($file in $untracked) {
            $added[$file] = $null
        }
    }

    # A side is new when at least half its lines were added on this branch.
    function Test-NewSide([string]$Span) {
        if ($Span -notmatch '^(?<path>.+):(?<from>\d+)-(?<to>\d+)$') {
            return $false
        }

        $path = $Matches['path']
        if (-not $added.ContainsKey($path)) {
            return $false
        }

        if ($null -eq $added[$path]) {
            return $true
        }

        $from = [int]$Matches['from']
        $to = [int]$Matches['to']
        $hits = 0
        for ($n = $from; $n -le $to; $n++) {
            if ($added[$path].Contains($n)) {
                $hits++
            }
        }

        return ($hits * 2) -ge ($to - $from + 1)
    }

    $summary = $rows | Where-Object { $_ -like '#*' } | Select-Object -Last 1
    $new = New-Object System.Collections.Generic.List[string]
    $existing = New-Object System.Collections.Generic.List[string]
    foreach ($row in $rows) {
        if ($row -like '#*' -or [string]::IsNullOrWhiteSpace($row)) {
            continue
        }

        $tokens, $left, $right = $row -split "`t"
        $entry = "  {0,4} tokens  {1}  <->  {2}" -f $tokens, $left, $right
        if ((Test-NewSide $left) -or (Test-NewSide $right)) {
            $new.Add($entry)
        }
        else {
            $existing.Add($entry)
        }
    }

    Write-Host "Added on this branch ($($new.Count)):"
    if ($new.Count -eq 0) {
        Write-Host '  (none)'
    }
    $new | ForEach-Object { Write-Host $_ }

    # The outer @() keeps a one-row result an array; an if statement unrolls what its branch returns.
    $shown = @(if ($Top -gt 0) { $existing | Select-Object -First $Top } else { $existing })
    Write-Host ''
    Write-Host "Existing ($($existing.Count), showing $($shown.Count)):"
    $shown | ForEach-Object { Write-Host $_ }

    Write-Host ''
    Write-Host "Report-Duplication: $($summary.TrimStart('#', ' '))"
    if ($new.Count -gt 0) {
        Write-Host 'Advice: a block added on this branch repeats existing code. Consider extracting a shared helper; keep the copy if it reads better.'
    }
    exit 0
}
finally {
    Pop-Location
}
