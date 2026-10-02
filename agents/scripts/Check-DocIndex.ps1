<#
.SYNOPSIS
    Checks that docs/Index.md lists every Markdown page under docs/ and links only to files that exist.

.DESCRIPTION
    Reads docs/Index.md under the CALLER's git toplevel (`git rev-parse --show-toplevel`) and collects
    every inline link target `[text](target)`, ignoring links inside fenced code blocks, absolute URLs
    (http, https, mailto) and pure anchors. Each target is resolved relative to docs/, has its #anchor
    removed, and is tested for existence.

    Two failures are reported, one line each:
      MISSING  <target>   a link in the index whose file does not exist
      UNLISTED <path>     a tracked .md file under docs/ (other than Index.md itself) that no link in
                          the index points at

    The very last output line is always
        CHECK-DOCINDEX Missing=<n> Unlisted=<n>
    Exits 0 only when both counts are 0, otherwise 1.

.EXAMPLE
    pwsh agents/scripts/Check-DocIndex.ps1
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (git rev-parse --show-toplevel).Trim()
$docsDir = Join-Path $repoRoot 'docs'
$indexPath = Join-Path $docsDir 'Index.md'
if (-not (Test-Path -LiteralPath $indexPath -PathType Leaf)) {
    Write-Host "docs/Index.md not found under $repoRoot"
    Write-Host 'CHECK-DOCINDEX Missing=1 Unlisted=0'
    exit 1
}

$linked = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
$missing = 0
$inFence = $false
foreach ($line in (Get-Content -LiteralPath $indexPath)) {
    if ($line -match '^\s*```') {
        $inFence = -not $inFence
        continue
    }
    if ($inFence) {
        continue
    }
    foreach ($match in [regex]::Matches($line, '\]\(([^)\s]+)\)')) {
        $target = $match.Groups[1].Value
        if ($target -match '^(https?:|mailto:|#)') {
            continue
        }
        $target = ($target -split '#', 2)[0]
        if ([string]::IsNullOrEmpty($target)) {
            continue
        }
        $full = [System.IO.Path]::GetFullPath((Join-Path $docsDir $target))
        if (-not (Test-Path -LiteralPath $full)) {
            Write-Host "MISSING  $target"
            $missing++
            continue
        }
        [void]$linked.Add($full)
    }
}

$unlisted = 0
foreach ($file in (git -C $repoRoot ls-files 'docs/*.md')) {
    $full = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $file))
    if ($full -ieq $indexPath) {
        continue
    }
    if (-not $linked.Contains($full)) {
        Write-Host "UNLISTED $file"
        $unlisted++
    }
}

Write-Host "CHECK-DOCINDEX Missing=$missing Unlisted=$unlisted"
if (($missing + $unlisted) -gt 0) {
    exit 1
}
exit 0
