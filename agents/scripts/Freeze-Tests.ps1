<#
.SYNOPSIS
    Freezes the tests an approved red wrote, so the implementation step cannot relax them.

.DESCRIPTION
    A failing test is the spec. After the delivery manager approves a .t red, -Freeze copies every
    changed or new file under tests/ into this worktree's private git directory
    (<git-dir>/frozen-tests/) and lists it in a manifest. The PreToolUse hook
    .claude/hooks/block-frozen-test-edits.sh then refuses Edit and Write on those paths, so an
    implementer who cannot get a test green must stop and report instead of weakening it.

    The hook cannot see a shell redirect or a script that rewrites a file, so -Check is the
    backstop: it diffs every frozen file against its copy and fails on any change, listing the
    patterns that usually mean a test was relaxed (fewer asserts, Equal weakened to Contains or
    NotEmpty, Skip, delays, broad catch, widened timeouts, a deleted file).

    State lives under the worktree's own git directory, so it is per worktree, never committed and
    never visible to another session's checkout.

    -Release is the manager's escape hatch for a real arrange defect: it removes one file from the
    freeze so the implementer may fix it, after the manager has read the test change request.

.PARAMETER Freeze
    Freeze the files named by -Path, or, with no -Path, every file under tests/ that git reports
    as modified or untracked. Re-freezing a file replaces its copy.

.PARAMETER Check
    Compare every frozen file with its copy. Exit 1 when any differs or is gone. A file that differs
    only in line endings (CRLF against LF) is reported as EOL-ONLY and does not fail the check.

.PARAMETER Release
    Remove the files named by -Path from the freeze (repo-relative or absolute).

.PARAMETER Clear
    Remove the whole freeze for this worktree. Use after the pair is committed.

.PARAMETER Status
    List the frozen files. This is the default when no other switch is given.

.PARAMETER Path
    Files for -Freeze or -Release.

.EXAMPLE
    pwsh -NoProfile -File agents/scripts/Freeze-Tests.ps1 -Freeze

.EXAMPLE
    pwsh -NoProfile -File agents/scripts/Freeze-Tests.ps1 -Check

.EXAMPLE
    pwsh -NoProfile -File agents/scripts/Freeze-Tests.ps1 -Release -Path tests/Huddle.Tests.Tasks/Tasks/FooTests.cs
#>
[CmdletBinding()]
param(
    [switch]$Freeze,
    [switch]$Check,
    [switch]$Release,
    [switch]$Clear,
    [switch]$Status,
    [string[]]$Path
)

$ErrorActionPreference = 'Stop'

$selected = @($Freeze, $Check, $Release, $Clear, $Status) | Where-Object { $_ }
if ($selected.Count -gt 1) {
    Write-Host 'Pass one of -Freeze, -Check, -Release, -Clear, -Status.' -ForegroundColor Yellow
    exit 2
}
if ($selected.Count -eq 0) {
    $Status = $true
}

$topLevel = (git rev-parse --show-toplevel).Trim()
$gitDir = (git rev-parse --absolute-git-dir).Trim()
$stateDir = Join-Path $gitDir 'frozen-tests'
$filesDir = Join-Path $stateDir 'files'
$manifestPath = Join-Path $stateDir 'manifest.txt'

# The key the hook compares against: absolute, forward slashes, lower case, "e:/..." drive form.
function ConvertTo-FreezeKey {
    param([string]$FullPath)

    return ([IO.Path]::GetFullPath($FullPath) -replace '\\', '/').ToLowerInvariant()
}

function Get-RelativePath {
    param([string]$FullPath)

    return [IO.Path]::GetRelativePath($topLevel, $FullPath) -replace '\\', '/'
}

function Resolve-InputPath {
    param([string]$Value)

    if ([IO.Path]::IsPathRooted($Value)) {
        return [IO.Path]::GetFullPath($Value)
    }

    return [IO.Path]::GetFullPath((Join-Path $topLevel $Value))
}

function Get-Manifest {
    if (-not (Test-Path -LiteralPath $manifestPath)) {
        return @()
    }

    return @(Get-Content -LiteralPath $manifestPath | Where-Object { $_ })
}

function Set-Manifest {
    param([string[]]$Keys)

    if ($Keys.Count -eq 0) {
        Remove-Item -LiteralPath $manifestPath -Force -ErrorAction SilentlyContinue
        return
    }

    New-Item -ItemType Directory -Force -Path $stateDir | Out-Null
    Set-Content -LiteralPath $manifestPath -Value ($Keys | Sort-Object -Unique) -Encoding utf8
}

# The copy of a frozen file is stored under its repo-relative path, so two files with the same
# name in different folders never collide.
function Get-CopyPath {
    param([string]$FullPath)

    return Join-Path $filesDir (Get-RelativePath $FullPath)
}

function Get-ChangedTestFiles {
    $lines = git -C $topLevel status --porcelain --untracked-files=all -- tests
    $result = [System.Collections.Generic.List[string]]::new()
    foreach ($line in $lines) {
        if ($line.Length -lt 4) {
            continue
        }

        $relative = $line.Substring(3).Trim('"')
        if ($relative -match '(^|/)(bin|obj)/') {
            continue
        }

        if ($line[0] -eq 'D' -or $line[1] -eq 'D') {
            continue
        }

        $result.Add((Join-Path $topLevel $relative))
    }

    return $result
}

# Heuristics for "this edit relaxed a test". They flag, they do not prove: the manager reads
# each hit against the diff.
function Get-RelaxationFlags {
    param([string]$FrozenCopy, [string]$Current)

    $diff = git diff --no-index --no-color -U0 -- $FrozenCopy $Current 2>$null
    $removed = @($diff | Where-Object { $_ -match '^-(?!--)' })
    $added = @($diff | Where-Object { $_ -match '^\+(?!\+\+)' })
    $flags = [System.Collections.Generic.List[string]]::new()

    $assertsRemoved = @($removed | Where-Object { $_ -match 'Assert\.' }).Count
    $assertsAdded = @($added | Where-Object { $_ -match 'Assert\.' }).Count
    if ($assertsAdded -lt $assertsRemoved) {
        $flags.Add("fewer asserts ($assertsRemoved removed, $assertsAdded added)")
    }

    $exactRemoved = @($removed | Where-Object { $_ -match 'Assert\.(Equal|Single|Empty|Null|Same|Throws)' }).Count
    $looseAdded = @($added | Where-Object { $_ -match 'Assert\.(Contains|NotEmpty|NotNull|NotEqual|True\(true|InRange)' }).Count
    if ($exactRemoved -gt 0 -and $looseAdded -gt 0) {
        $flags.Add('exact assert weakened to a looser one')
    }

    $patterns = [ordered]@{
        'Skip added'                  = 'Skip\s*='
        'delay or sleep added'        = 'Thread\.Sleep|Task\.Delay'
        'try/catch added'             = '\bcatch\b'
        'timeout or tolerance edited' = 'Timeout|Tolerance|TimeSpan\.From|precision'
        'pragma added'                = '#pragma'
        'test attribute removed'      = '^\s*\[(Fact|Theory)'
    }

    foreach ($name in $patterns.Keys) {
        $source = if ($name -eq 'test attribute removed') { $removed } else { $added }
        $hits = @($source | Where-Object { $_ -match $patterns[$name] }).Count
        if ($hits -gt 0) {
            $flags.Add("$name ($hits line$(if ($hits -ne 1) { 's' }))")
        }
    }

    return $flags
}

if ($Status) {
    $keys = Get-Manifest
    if ($keys.Count -eq 0) {
        Write-Host 'No tests are frozen in this worktree.' -ForegroundColor Gray
        exit 0
    }

    Write-Host "Frozen in this worktree ($($keys.Count)):" -ForegroundColor Cyan
    foreach ($key in $keys) {
        Write-Host "   $key" -ForegroundColor Gray
    }

    exit 0
}

if ($Clear) {
    Remove-Item -LiteralPath $stateDir -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host 'Freeze cleared for this worktree.' -ForegroundColor Green
    exit 0
}

if ($Freeze) {
    $targets = if ($Path) { @($Path | ForEach-Object { Resolve-InputPath $_ }) } else { @(Get-ChangedTestFiles) }
    $targets = @($targets | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf })
    if ($targets.Count -eq 0) {
        Write-Host 'Nothing to freeze: no changed or untracked files under tests/.' -ForegroundColor Yellow
        exit 1
    }

    $keys = [System.Collections.Generic.List[string]]::new()
    $keys.AddRange([string[]]@(Get-Manifest))
    foreach ($target in $targets) {
        $copy = Get-CopyPath $target
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $copy) | Out-Null
        Copy-Item -LiteralPath $target -Destination $copy -Force
        $keys.Add((ConvertTo-FreezeKey $target))
        Write-Host "   Frozen $(Get-RelativePath $target)" -ForegroundColor Gray
    }

    Set-Manifest $keys.ToArray()
    Write-Host "Froze $($targets.Count) file(s). Edit and Write on them are now refused in this worktree." -ForegroundColor Green
    exit 0
}

if ($Release) {
    if (-not $Path) {
        Write-Host '-Release needs -Path.' -ForegroundColor Yellow
        exit 2
    }

    $releaseKeys = @($Path | ForEach-Object { ConvertTo-FreezeKey (Resolve-InputPath $_) })
    $remaining = @(Get-Manifest | Where-Object { $releaseKeys -notcontains $_ })
    Set-Manifest $remaining
    foreach ($releaseKey in $releaseKeys) {
        Write-Host "   Released $releaseKey" -ForegroundColor Gray
    }

    exit 0
}

# -Check
$keys = Get-Manifest
if ($keys.Count -eq 0) {
    Write-Host 'Nothing is frozen in this worktree, so there is nothing to check.' -ForegroundColor Gray
    exit 0
}

$failed = 0
foreach ($key in $keys) {
    # Keys are lower case; the Windows file system matches paths case-insensitively, so the key
    # opens the live file and its copy directly.
    $live = $key
    $relative = Get-RelativePath $live
    $copy = Join-Path $filesDir $relative
    if (-not (Test-Path -LiteralPath $copy)) {
        $copy = $null
    }

    if (-not (Test-Path -LiteralPath $live)) {
        Write-Host "DELETED  $relative" -ForegroundColor Red
        $failed++
        continue
    }

    if ($copy -and (Get-FileHash -LiteralPath $copy).Hash -eq (Get-FileHash -LiteralPath $live).Hash) {
        continue
    }

    if ($copy -and (([IO.File]::ReadAllText($copy) -replace "`r`n", "`n") -ceq ([IO.File]::ReadAllText($live) -replace "`r`n", "`n"))) {
        Write-Host "EOL-ONLY $relative" -ForegroundColor Gray
        continue
    }

    $failed++
    Write-Host "CHANGED  $relative" -ForegroundColor Red
    if ($copy) {
        foreach ($flag in (Get-RelaxationFlags -FrozenCopy $copy -Current $live)) {
            Write-Host "   - $flag" -ForegroundColor Yellow
        }
    }
}

if ($failed -gt 0) {
    Write-Host ''
    Write-Host "$failed frozen test file(s) changed since the red was approved." -ForegroundColor Red
    Write-Host '   Read each diff. A legitimate arrange fix needs -Release first; anything else goes back to the implementer.' -ForegroundColor Gray
    exit 1
}

Write-Host "All $($keys.Count) frozen test file(s) are unchanged." -ForegroundColor Green
exit 0
