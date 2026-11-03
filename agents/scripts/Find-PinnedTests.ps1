<#
.SYNOPSIS
    Lists every line in the test sources that matches a pattern, grouped per test project, and marks
    the files frozen in this worktree.

.DESCRIPTION
    Use it before changing behaviour that tests may pin (a prompt wording, a constant, a name): it
    shows every test that mentions the text, so none is found only when CI goes red.

    Searches the .cs files under -Root (default tests), skipping bin and obj. Output is grouped by
    the first folder under tests/ (the test project) as "file:line: text". A file listed in this
    worktree's freeze manifest, <git-dir>/frozen-tests/manifest.txt, is marked [FROZEN]: the
    block-frozen-test-edits.sh hook refuses edits to it, so a change that needs it is a test change
    request for the delivery manager. Manifest keys are lower-case, forward-slash "e:/..." paths.

    Writes nothing and exits 0 on no match.

.PARAMETER Pattern
    Regular expression, matched case-insensitively against each line.

.PARAMETER Root
    Folder to search, relative to the git toplevel or absolute. Defaults to tests.

.EXAMPLE
    pwsh agents/scripts/Find-PinnedTests.ps1 -Pattern 'mcp__team__'

.EXAMPLE
    pwsh agents/scripts/Find-PinnedTests.ps1 -Pattern 'Memory topic' -Root tests/Huddle.Tests.Agents
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$Pattern,
    [string]$Root = 'tests'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$top = (& git rev-parse --show-toplevel).Trim()
$gitDir = (& git rev-parse --absolute-git-dir).Trim()
$searchRoot = if ([System.IO.Path]::IsPathRooted($Root)) { $Root } else { Join-Path $top $Root }
if (-not (Test-Path -LiteralPath $searchRoot)) {
    throw "Root '$searchRoot' does not exist."
}

function ConvertTo-FreezeKey {
    param([string]$FullPath)
    return $FullPath.Replace('\', '/').ToLowerInvariant()
}

$frozen = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$manifest = Join-Path $gitDir 'frozen-tests/manifest.txt'
if (Test-Path -LiteralPath $manifest) {
    foreach ($entry in Get-Content -LiteralPath $manifest) {
        $trimmed = $entry.Trim([char]0xFEFF, ' ', "`t")
        if ($trimmed) {
            [void]$frozen.Add($trimmed)
        }
    }
}

$topPrefix = $top.TrimEnd('\', '/').Replace('\', '/')
$testsPrefix = "$topPrefix/tests/"
$regex = [regex]::new($Pattern, [System.Text.RegularExpressions.RegexOptions]::IgnoreCase -bor [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)

$hits = [System.Collections.Generic.List[object]]::new()
$files = Get-ChildItem -LiteralPath $searchRoot -Filter '*.cs' -File -Recurse | Where-Object {
    $_.FullName.Replace('\', '/') -notmatch '/(bin|obj)/'
}
foreach ($file in $files) {
    $full = $file.FullName.Replace('\', '/')
    $relative = if ($full.StartsWith($topPrefix + '/', [StringComparison]::OrdinalIgnoreCase)) { $full.Substring($topPrefix.Length + 1) } else { $full }
    $project = if ($full.StartsWith($testsPrefix, [StringComparison]::OrdinalIgnoreCase)) { $full.Substring($testsPrefix.Length).Split('/')[0] } else { '(outside tests)' }
    $isFrozen = $frozen.Contains((ConvertTo-FreezeKey $file.FullName))
    $lineNumber = 0
    foreach ($line in [System.IO.File]::ReadLines($file.FullName)) {
        $lineNumber++
        if ($regex.IsMatch($line)) {
            $hits.Add([pscustomobject]@{ Project = $project; File = $relative; Line = $lineNumber; Text = $line.Trim(); Frozen = $isFrozen })
        }
    }
}

foreach ($group in $hits | Group-Object Project | Sort-Object Name) {
    Write-Host "$($group.Name) ($($group.Count) lines in $(@($group.Group.File | Sort-Object -Unique).Count) files)"
    foreach ($hit in $group.Group | Sort-Object File, Line) {
        $mark = if ($hit.Frozen) { ' [FROZEN]' } else { '' }
        Write-Host "  $($hit.File):$($hit.Line):$mark $($hit.Text)"
    }
    Write-Host ''
}
