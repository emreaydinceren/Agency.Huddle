<#
.SYNOPSIS
    Moves one target's test files (and its S9 content files) out of tests/Huddle.Tests with git mv.

.DESCRIPTION
    Regenerates the manifest, then runs git mv for every row of -Target plus the S9 content rows for that
    target into the sibling folder Huddle.Tests.<Target>. Never edits a file. With -WhatIf prints one
    line per file and moves nothing. Otherwise prints "moved <n> files to Huddle.Tests.<Target>".
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('Common', 'Hosted', 'Agents', 'Components', 'SkillPacks')]
    [string]$Target,
    [switch]$WhatIf,
    [string]$Root
)
$ErrorActionPreference = 'Stop'
if (-not $Root) {
    $Root = Join-Path (git rev-parse --show-toplevel) 'tests/Huddle.Tests'
}
$rootFull = (Resolve-Path $Root).Path
$destRoot = Join-Path (Split-Path $rootFull -Parent) "Huddle.Tests.$Target"

# S9: content source prefix (relative to Root, forward slashes) -> target project.
$contentTargets = [ordered]@{
    'Acp/Golden/'                                = 'Hosted'
    'SkillPacks/Fixtures/'                       = 'Common'
    'SkillPacks/Move/Golden/'                    = 'SkillPacks'
    'Teams/neutrality-allow.txt'                 = 'Domain'
    'Ui/Conventions/Baseline/'                   = 'Domain'
    'Ui/Composer/composer-attach-harness.mjs'    = 'Agents'
    'Ui/Library/markdown-commands.harness.mjs'   = 'Agents'
    'Ui/References/composer-picker.harness.js'   = 'Agents'
    'Ui/ThemeLabScript/theme-lab-harness.mjs'    = 'Agents'
}

$manifest = Join-Path $PSScriptRoot 'New-TestSplitManifest.ps1'
$rows = & $manifest -Root $rootFull -PassThru
$paths = foreach ($row in $rows) {
    $rel = $row.Path -replace '[\\]', '/'
    if ($row.Target -eq 'Content') {
        $hit = $contentTargets.Keys | Where-Object { $rel.StartsWith($_, [StringComparison]::Ordinal) } | Select-Object -First 1
        if ($hit -and $contentTargets[$hit] -eq $Target) { $rel }
    }
    elseif ($row.Target -eq $Target) {
        $rel
    }
}
$paths = @($paths | Sort-Object)

if ($WhatIf) {
    foreach ($p in $paths) { "would move $p to Huddle.Tests.$Target" }
    return
}
foreach ($p in $paths) {
    $dest = Join-Path $destRoot $p
    New-Item -ItemType Directory -Force -Path (Split-Path $dest -Parent) | Out-Null
    git -C $rootFull mv (Join-Path $rootFull $p) $dest
    if ($LASTEXITCODE -ne 0) { throw "git mv failed for $p" }
}
"moved $($paths.Count) files to Huddle.Tests.$Target"
