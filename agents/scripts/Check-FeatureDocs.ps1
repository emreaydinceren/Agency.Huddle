<#
.SYNOPSIS
    Checks the user-facing feature pages and the numbering of docs/FeaturesSummary/feature-list.md.

.DESCRIPTION
    Reads under the CALLER's git toplevel (`git rev-parse --show-toplevel`). Two groups of checks:

    User-facing pages (docs/FeaturesSummary/Features-&-Capabilities.md and Features/*.md):
      HISTORY  <page>            the page names a PR (#123, PR, PRs) or a commit hash; these pages tell no history
      MISSING  <page> -> <path>  a relative link whose file does not exist
      ANCHOR   <page> -> <link>  a link to a heading that does not exist in the target file

    feature-list.md:
      NUMBER   <message>         a section is not `## n. Title` in sequence from 1, or an entry is not
                                 `**n.m Title.**` (or `- **n.m ...` under "Designed but not built") in
                                 sequence from n.1 inside section n
      TOC      <message>         the Contents list does not match the headings, in order, with their anchors

    Links inside fenced code blocks are ignored. Heading anchors follow GitHub's rule: lowercase, backticks
    and punctuation removed, spaces to hyphens.

    The very last output line is always
        CHECK-FEATUREDOCS Problems=<n>
    Exits 0 only when n is 0, otherwise 1.

.EXAMPLE
    pwsh agents/scripts/Check-FeatureDocs.ps1
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (git rev-parse --show-toplevel).Trim()
$dir = Join-Path $repoRoot 'docs/FeaturesSummary'
$problems = 0

function Get-Slug([string] $heading) {
    (($heading -replace '`', '').ToLowerInvariant() -replace '[^\w\s-]', '') -replace '\s', '-'
}

function Get-OutsideFences([string] $text) {
    $keep = [System.Collections.Generic.List[string]]::new()
    $inFence = $false
    foreach ($line in ($text -split "`r?`n")) {
        if ($line -match '^\s*```') {
            $inFence = -not $inFence
            continue
        }
        if (-not $inFence) {
            $keep.Add($line)
        }
    }
    return $keep
}

$slugCache = @{}
function Get-Slugs([string] $file) {
    if (-not $slugCache.ContainsKey($file)) {
        $slugs = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
        foreach ($line in (Get-OutsideFences ([System.IO.File]::ReadAllText($file)))) {
            if ($line -match '^#{1,6} (.+)$') {
                [void]$slugs.Add((Get-Slug $Matches[1]))
            }
        }
        $slugCache[$file] = $slugs
    }
    return $slugCache[$file]
}

$pages = @(Get-Item -LiteralPath (Join-Path $dir 'Features-&-Capabilities.md')) +
    @(Get-ChildItem -LiteralPath (Join-Path $dir 'Features') -Filter '*.md')
foreach ($page in $pages) {
    $lines = Get-OutsideFences ([System.IO.File]::ReadAllText($page.FullName))
    $text = $lines -join "`n"
    if ($text -cmatch '(?<![\w/])#\d{2,}\b|\bPRs?\b|\b[0-9a-f]{8}\b') {
        Write-Host "HISTORY  $($page.Name)"
        $problems++
    }
    foreach ($match in [regex]::Matches($text, '\]\(([^)\s]+)\)')) {
        $link = $match.Groups[1].Value
        if ($link -match '^(https?:|mailto:)') {
            continue
        }
        $path, $anchor = $link -split '#', 2
        $target = if ($path) { [System.IO.Path]::GetFullPath((Join-Path $page.DirectoryName $path)) } else { $page.FullName }
        if (-not (Test-Path -LiteralPath $target -PathType Leaf)) {
            Write-Host "MISSING  $($page.Name) -> $path"
            $problems++
            continue
        }
        if ($anchor -and -not (Get-Slugs $target).Contains($anchor)) {
            Write-Host "ANCHOR   $($page.Name) -> $link"
            $problems++
        }
    }
}

$listPath = Join-Path $dir 'feature-list.md'
$expectedToc = [System.Collections.Generic.List[string]]::new()
$actualToc = [System.Collections.Generic.List[string]]::new()
$section = 0
$entry = 0
$inContents = $false
$designed = $false
foreach ($line in (Get-OutsideFences ([System.IO.File]::ReadAllText($listPath)))) {
    if ($line -match '^## (.+)$') {
        $title = $Matches[1]
        $inContents = ($title -eq 'Contents')
        if ($inContents) {
            continue
        }
        $section++
        $entry = 0
        if ($title -notmatch "^$section\. (.+)$") {
            Write-Host "NUMBER   section $section is '## $title', expected '## $section. ...'"
            $problems++
        }
        $designed = ($title -match '^\d+\. Designed but not built$')
        $expectedToc.Add("- [$title](#$(Get-Slug $title))")
        continue
    }
    if ($inContents) {
        if ($line -match '^- \[') {
            $actualToc.Add($line)
        }
        continue
    }
    if ($section -eq 0) {
        continue
    }
    if ($line -match '^\*\*(\d+)\.(\d+) ' -or ($designed -and $line -match '^- \*\*(\d+)\.(\d+) ')) {
        $entry++
        if ([int]$Matches[1] -ne $section -or [int]$Matches[2] -ne $entry) {
            Write-Host "NUMBER   section $section entry $entry is numbered $($Matches[1]).$($Matches[2])"
            $problems++
        }
    }
}
if (($expectedToc -join "`n") -ne ($actualToc -join "`n")) {
    Write-Host "TOC      Contents does not match the $section section headings"
    $problems++
}

Write-Host "CHECK-FEATUREDOCS Problems=$problems"
if ($problems -gt 0) {
    exit 1
}
exit 0
