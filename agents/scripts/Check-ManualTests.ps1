<#
.SYNOPSIS
    Checks the structure of the manual-test set under docs/engineering/manual-tests/.

.DESCRIPTION
    Area files are docs/engineering/manual-tests/*.md except common.md, tracker.md and planning.md.
    A test heading is `### <ID> - <title>` (em dash). Per-test minutes come from the first
    `about <n> min` within 4 lines below the heading. The area header is the first
    `**<n> tests**` and `about <h> hours` within the first 15 lines.

    Part A rules, one line each in the form `<CODE> <file> <detail>`:
      HEADER-COUNT      header count differs from the heading count (or the header has none)
      HEADER-HOURS      header hours differ from the sum of minutes / 60 by more than 0.25
      HUB-MISSING       no link to the area file from docs/engineering/manual-tests.md
      PLANNING-MISSING  no link to the area file from planning.md
      TRACKER-MISSING   a heading with no tracker row
      TRACKER-ORPHAN    a tracker row with no heading
      DUPLICATE-ID      the same id as a heading in two files (reported on the later file)

    -Area limits the run to one file's own rules (header and ids); registry rules are skipped.
    -Summary prints a table `Area | Tests | Paid | Hours` and totals instead of rule output.
    The last line is `CHECK-MANUALTESTS Errors=<n>`; exits 0 only at 0.

.PARAMETER Root
    Repository root (default: git toplevel). Holds docs/engineering/.

.PARAMETER Area
    One area file (absolute, or relative to the current directory or to Root).

.PARAMETER Summary
    Print the per-area table and totals.

.EXAMPLE
    pwsh agents/scripts/Check-ManualTests.ps1 -Area docs/engineering/manual-tests/work-mode.md
#>
[CmdletBinding()]
param(
    [string]$Root,
    [string]$Area,
    [switch]$Summary
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($Root)) {
    $Root = (git rev-parse --show-toplevel).Trim()
}
$Root = [System.IO.Path]::GetFullPath($Root)
$setDir = Join-Path $Root 'docs/engineering/manual-tests'
$hubPath = Join-Path $Root 'docs/engineering/manual-tests.md'
$planningPath = Join-Path $setDir 'planning.md'
$trackerPath = Join-Path $setDir 'tracker.md'
$nonArea = @('common.md', 'tracker.md', 'planning.md')

$headingPattern = '^### (?<id>[A-Z][A-Z0-9]*(?:-[A-Z0-9]+)*) — '
$errors = [System.Collections.Generic.List[string]]::new()
$moneyBag = [char]::ConvertFromUtf32(0x1F4B0)

function Get-Lines {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return @()
    }
    return @(Get-Content -LiteralPath $Path -Encoding UTF8)
}

function Get-LinkLeaves {
    param([string[]]$Lines)
    $leaves = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($line in $Lines) {
        foreach ($m in [regex]::Matches($line, '\]\((?<t>[^)\s#]+)(?:#[^)]*)?\)')) {
            [void]$leaves.Add([System.IO.Path]::GetFileName($m.Groups['t'].Value))
        }
    }
    return , $leaves
}

function Read-Area {
    param([string]$Path)
    $lines = Get-Lines -Path $Path
    $tests = [System.Collections.Generic.List[object]]::new()
    $inFence = $false
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match '^\s*```') {
            $inFence = -not $inFence
            continue
        }
        if ($inFence -or $lines[$i] -notmatch $headingPattern) {
            continue
        }
        $testId = $Matches['id']
        $minutes = 0
        $paid = $lines[$i].Contains($moneyBag)
        $stop = [Math]::Min($i + 4, $lines.Count - 1)
        $gotMinutes = $false
        for ($j = $i + 1; $j -le $stop; $j++) {
            if (-not $gotMinutes -and $lines[$j] -match 'about (?<m>\d+) min') {
                $minutes = [int]$Matches['m']
                $gotMinutes = $true
            }
            if ($lines[$j].Contains($moneyBag)) {
                $paid = $true
            }
        }
        $tests.Add([pscustomobject]@{ Id = $testId; Minutes = $minutes; Paid = $paid })
    }
    $head = ($lines | Select-Object -First 15) -join "`n"
    $headerCount = $null
    $headerHours = $null
    if ($head -match '\*\*(?<n>\d+) tests\*\*') {
        $headerCount = [int]$Matches['n']
    }
    if ($head -match 'about (?<h>[\d.]+) hours?') {
        $headerHours = [double]::Parse($Matches['h'], [System.Globalization.CultureInfo]::InvariantCulture)
    }
    return [pscustomobject]@{
        Name = [System.IO.Path]::GetFileName($Path)
        Tests = $tests
        HeaderCount = $headerCount
        HeaderHours = $headerHours
        Minutes = ($tests | Measure-Object -Property Minutes -Sum).Sum
    }
}

function Format-Hours {
    param([double]$Hours)
    return [Math]::Round($Hours, 1, [MidpointRounding]::AwayFromZero).ToString('0.0', [System.Globalization.CultureInfo]::InvariantCulture)
}

$areaFiles = @()
if (Test-Path -LiteralPath $setDir -PathType Container) {
    $areaFiles = @(Get-ChildItem -LiteralPath $setDir -Filter '*.md' -File |
            Where-Object { $nonArea -notcontains $_.Name } |
            Sort-Object -Property Name)
}
$areas = @($areaFiles | ForEach-Object { Read-Area -Path $_.FullName })

if ($Summary) {
    Write-Host 'Area | Tests | Paid | Hours'
    $totalTests = 0
    $totalPaid = 0
    $totalMinutes = 0
    foreach ($a in $areas) {
        $paid = @($a.Tests | Where-Object { $_.Paid }).Count
        Write-Host ("{0} | {1} | {2} | {3}" -f $a.Name.Replace('.md', ''), $a.Tests.Count, $paid, (Format-Hours ($a.Minutes / 60.0)))
        $totalTests += $a.Tests.Count
        $totalPaid += $paid
        $totalMinutes += $a.Minutes
    }
    Write-Host ("Total | {0} | {1} | {2}" -f $totalTests, $totalPaid, (Format-Hours ($totalMinutes / 60.0)))
    Write-Host 'CHECK-MANUALTESTS Errors=0'
    exit 0
}

$scope = $areas
if (-not [string]::IsNullOrWhiteSpace($Area)) {
    $candidates = @($Area, (Join-Path $Root $Area))
    $resolved = $null
    foreach ($c in $candidates) {
        if (Test-Path -LiteralPath $c -PathType Leaf) {
            $resolved = [System.IO.Path]::GetFileName((Resolve-Path -LiteralPath $c).Path)
            break
        }
    }
    if ($null -eq $resolved) {
        Write-Host "Area file not found: $Area"
        Write-Host 'CHECK-MANUALTESTS Errors=1'
        exit 1
    }
    $scope = @($areas | Where-Object { $_.Name -eq $resolved })
}

foreach ($a in $scope) {
    $count = $a.Tests.Count
    if ($null -eq $a.HeaderCount) {
        $errors.Add("HEADER-COUNT $($a.Name) header has no '**<n> tests**', found $count")
    }
    elseif ($a.HeaderCount -ne $count) {
        $errors.Add("HEADER-COUNT $($a.Name) header says $($a.HeaderCount) tests, found $count")
    }
}
foreach ($a in $scope) {
    if ($null -eq $a.HeaderHours) {
        $errors.Add("HEADER-HOURS $($a.Name) header has no 'about <h> hours'")
    }
    elseif ([Math]::Abs($a.HeaderHours - ($a.Minutes / 60.0)) -gt 0.25) {
        $errors.Add("HEADER-HOURS $($a.Name) header says $(Format-Hours $a.HeaderHours) hours, tests sum to $(Format-Hours ($a.Minutes / 60.0))")
    }
}

if ([string]::IsNullOrWhiteSpace($Area)) {
    $hubLeaves = Get-LinkLeaves -Lines (Get-Lines -Path $hubPath)
    foreach ($a in $areas) {
        if (-not $hubLeaves.Contains($a.Name)) {
            $errors.Add("HUB-MISSING $($a.Name) not linked from docs/engineering/manual-tests.md")
        }
    }
    $planLeaves = Get-LinkLeaves -Lines (Get-Lines -Path $planningPath)
    foreach ($a in $areas) {
        if (-not $planLeaves.Contains($a.Name)) {
            $errors.Add("PLANNING-MISSING $($a.Name) not linked from docs/engineering/manual-tests/planning.md")
        }
    }

    $rows = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    $rowOrder = [System.Collections.Generic.List[string]]::new()
    foreach ($line in (Get-Lines -Path $trackerPath)) {
        if ($line -match '^\|\s*\[(?<id>[A-Z][A-Z0-9]*(?:-[A-Z0-9]+)*)\]\((?:manual-tests/)?[^)#\s]+\.md#[^)]*\)') {
            if ($rows.Add($Matches['id'])) {
                $rowOrder.Add($Matches['id'])
            }
        }
    }
    $headingIds = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    foreach ($a in $areas) {
        foreach ($t in $a.Tests) {
            [void]$headingIds.Add($t.Id)
        }
    }
    foreach ($a in $areas) {
        foreach ($t in $a.Tests) {
            if (-not $rows.Contains($t.Id)) {
                $errors.Add("TRACKER-MISSING $($a.Name) $($t.Id) has no tracker row")
            }
        }
    }
    foreach ($id in $rowOrder) {
        if (-not $headingIds.Contains($id)) {
            $errors.Add("TRACKER-ORPHAN tracker.md $id has no heading")
        }
    }
}

$firstFile = @{}
foreach ($a in $areas) {
    foreach ($t in $a.Tests) {
        if (-not $firstFile.ContainsKey($t.Id)) {
            $firstFile[$t.Id] = $a.Name
        }
        elseif ($firstFile[$t.Id] -ne $a.Name -and ($scope | Where-Object { $_.Name -eq $a.Name })) {
            $errors.Add("DUPLICATE-ID $($a.Name) $($t.Id) is also a heading in $($firstFile[$t.Id])")
        }
    }
}

function Get-Slug {
    param([string]$Heading)
    $lower = $Heading.Trim().ToLowerInvariant()
    $kept = [regex]::Replace($lower, '[^\p{L}\p{Nd} _-]', '')
    return $kept.Replace(' ', '-')
}

function Get-Slugs {
    param([string]$Path)
    $set = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    $inFence = $false
    foreach ($line in (Get-Lines -Path $Path)) {
        if ($line -match '^\s*```') {
            $inFence = -not $inFence
            continue
        }
        if (-not $inFence -and $line -match '^#{1,6}\s+(?<h>.+)$') {
            [void]$set.Add((Get-Slug -Heading $Matches['h']))
        }
    }
    return , $set
}

$slugCache = @{}
$declared = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
foreach ($line in (Get-Lines -Path (Join-Path $setDir 'common.md'))) {
    if ($line -match '^#{2,4}\s+(?<id>[PEOT]-[A-Z0-9-]+)') {
        [void]$declared.Add($Matches['id'])
    }
    foreach ($m in [regex]::Matches($line, '\*\*(?<id>[PEOT]-[A-Z0-9-]+)\*\*')) {
        [void]$declared.Add($m.Groups['id'].Value)
    }
}

$linkFiles = [System.Collections.Generic.List[string]]::new()
if ([string]::IsNullOrWhiteSpace($Area)) {
    foreach ($p in @($hubPath, (Join-Path $setDir 'common.md'), $trackerPath, $planningPath)) {
        $linkFiles.Add($p)
    }
}
foreach ($a in $scope) {
    $linkFiles.Add((Join-Path $setDir $a.Name))
}
foreach ($file in $linkFiles) {
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) {
        continue
    }
    $name = [System.IO.Path]::GetFileName($file)
    $dir = [System.IO.Path]::GetDirectoryName($file)
    $isArea = ($nonArea -notcontains $name) -and ($file -ne $hubPath)
    $seen = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    $inFence = $false
    foreach ($line in (Get-Lines -Path $file)) {
        if ($line -match '^\s*```') {
            $inFence = -not $inFence
            continue
        }
        if ($inFence) {
            continue
        }
        foreach ($m in [regex]::Matches($line, '\[[^\]]*\]\((?<t>[^)\s]+)\)')) {
            $target = $m.Groups['t'].Value
            if ($target -match '^[A-Za-z][A-Za-z0-9+.-]*:') {
                continue
            }
            $pathPart = $target
            $anchor = $null
            $hash = $target.IndexOf('#')
            if ($hash -ge 0) {
                $pathPart = $target.Substring(0, $hash)
                $anchor = $target.Substring($hash + 1)
            }
            $dest = $file
            if ($pathPart.Length -gt 0) {
                $dest = [System.IO.Path]::GetFullPath((Join-Path $dir $pathPart))
                if (-not (Test-Path -LiteralPath $dest)) {
                    if ($seen.Add("L $pathPart")) {
                        $errors.Add("LINK-BROKEN $name $pathPart does not exist")
                    }
                    continue
                }
            }
            if ($anchor -and $dest.EndsWith('.md', [System.StringComparison]::OrdinalIgnoreCase) -and (Test-Path -LiteralPath $dest -PathType Leaf)) {
                if (-not $slugCache.ContainsKey($dest)) {
                    $slugCache[$dest] = Get-Slugs -Path $dest
                }
                if (-not $slugCache[$dest].Contains($anchor) -and $seen.Add("A $target")) {
                    $errors.Add("ANCHOR-BROKEN $name $target has no matching heading")
                }
            }
        }
        if ($isArea) {
            foreach ($m in [regex]::Matches($line, '`(?<id>[PEOT]-[A-Z0-9-]+)`')) {
                $id = $m.Groups['id'].Value
                if (-not $declared.Contains($id) -and $seen.Add("P $id")) {
                    $errors.Add("PROC-UNDEFINED $name $id is not declared in common.md")
                }
            }
        }
    }
}

$order = @('HEADER-COUNT', 'HEADER-HOURS', 'HUB-MISSING', 'PLANNING-MISSING', 'TRACKER-MISSING', 'TRACKER-ORPHAN', 'DUPLICATE-ID', 'LINK-BROKEN', 'ANCHOR-BROKEN', 'PROC-UNDEFINED')
$sorted = $errors | Sort-Object -Stable -Property @{ Expression = { $order.IndexOf(($_ -split ' ', 2)[0]) } }
foreach ($e in $sorted) {
    Write-Host $e
}
Write-Host "CHECK-MANUALTESTS Errors=$($errors.Count)"
if ($errors.Count -gt 0) {
    exit 1
}
exit 0
