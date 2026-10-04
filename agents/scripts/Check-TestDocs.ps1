<#
.SYNOPSIS
    Fails when a test method ADDED on this branch has no `///` summary comment above it.

.DESCRIPTION
    agents/CSharpPrinciples.md requires every test method to carry a `///` summary. No analyzer can
    check that (CS1591 is suppressed in Directory.Build.props, and only covers public members anyway),
    and about one in ten of the existing tests predates the rule, so this scans only lines the branch
    added rather than the whole tree.

    Added lines are found with `git diff -U0` against the merge-base with -Base (committed AND
    uncommitted changes to tracked files), plus every line of an untracked file under tests/. A test is
    a `[Fact]` or `[Theory]` attribute line; the method is documented when the first line above its
    attribute stack (other `[...]` attribute lines) starts with `///`. The attribute line is what has to
    be an added line, so editing the body of an old undocumented test is not flagged.

    Prints one "<file>:<line>: test has no /// summary" per offender and exits 1; with none it prints a
    one-line summary and exits 0.

.PARAMETER Base
    The branch to diff against. Defaults to main.

.EXAMPLE
    pwsh agents/scripts/Check-TestDocs.ps1
#>
[CmdletBinding()]
param(
    [string]$Base = 'main'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (git rev-parse --show-toplevel).Trim()
Push-Location -LiteralPath $repoRoot
try {
    $mergeBase = (git merge-base HEAD $Base).Trim()
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($mergeBase)) {
        throw "No merge-base between HEAD and '$Base'."
    }

    # file -> set of added line numbers (null means "every line", for an untracked file)
    $added = @{}

    $currentFile = $null
    foreach ($line in (git diff -U0 --no-color $mergeBase -- 'tests/*.cs')) {
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

    foreach ($untracked in (git ls-files --others --exclude-standard -- 'tests/*.cs')) {
        $added[$untracked] = $null
    }

    $offenders = New-Object System.Collections.Generic.List[string]
    foreach ($file in $added.Keys) {
        if (-not (Test-Path -LiteralPath $file -PathType Leaf)) {
            continue
        }

        $lines = @(Get-Content -LiteralPath $file)
        for ($i = 0; $i -lt $lines.Count; $i++) {
            if ($lines[$i] -notmatch '^\s*\[(Fact|Theory)\b') {
                continue
            }

            $lineNumber = $i + 1
            if ($null -ne $added[$file] -and -not $added[$file].Contains($lineNumber)) {
                continue
            }

            $j = $i - 1
            while ($j -ge 0 -and $lines[$j].Trim().StartsWith('[')) {
                $j--
            }

            if ($j -lt 0 -or -not $lines[$j].Trim().StartsWith('///')) {
                $offenders.Add("${file}:${lineNumber}: test has no /// summary")
            }
        }
    }

    if ($offenders.Count -gt 0) {
        $offenders | Sort-Object | ForEach-Object { Write-Host $_ }
        Write-Host "Check-TestDocs: $($offenders.Count) added test(s) without a summary."
        exit 1
    }

    Write-Host 'Check-TestDocs: every added test has a /// summary.'
    exit 0
}
finally {
    Pop-Location
}
