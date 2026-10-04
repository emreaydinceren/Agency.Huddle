<#
.SYNOPSIS
    Checks that every added/modified/untracked file in the working tree (excluding Conversation/ and
    binary extensions) has CRLF line endings, as the repo's .editorconfig requires for text files.

.DESCRIPTION
    Lists candidate paths from `git status --porcelain --untracked-files=all`, skips anything under
    Conversation/ (gitignored, not part of the repo's tracked convention) and common binary extensions,
    then inspects each remaining file's bytes directly: a bare `\n` not preceded by `\r` means the file
    is not pure CRLF.

    Exception: shell scripts (paths ending .sh) must be LF, because bash rejects CRLF. For them the
    test is inverted: any `\r` byte is the violation, reported as "contains CRLF (shell scripts must
    be LF)".

.PARAMETER Fix
    When set, runs Fix-Crlf.ps1 (resolved next to this script via $PSScriptRoot) on every offending
    non-shell file after reporting them. Offending .sh files are instead rewritten by this script
    with \r\n -> \n (UTF-8, no BOM added); they are never routed through Fix-Crlf.ps1.

.EXAMPLE
    pwsh agents/scripts/Check-Eol.ps1

.EXAMPLE
    pwsh agents/scripts/Check-Eol.ps1 -Fix
#>
[CmdletBinding()]
param(
    [switch]$Fix
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$BinaryExtensions = @('.png', '.ico', '.woff', '.woff2', '.ttf', '.otf', '.jpg', '.jpeg', '.gif', '.pdf', '.zip', '.dll', '.exe', '.pfx', '.snk')

$porcelain = git status --porcelain --untracked-files=all
if (-not $porcelain) {
    Write-Host 'No added/modified/untracked files.'
    exit 0
}

$candidates = New-Object System.Collections.Generic.List[string]
foreach ($line in $porcelain) {
    if ($line.Length -lt 4) {
        continue
    }

    # Porcelain format: XY <path> (and "XY <old> -> <new>" for renames - take the new path).
    $rest = $line.Substring(3)
    if ($rest -match '^(?<old>.+) -> (?<new>.+)$') {
        $relPath = $Matches.new
    }
    else {
        $relPath = $rest
    }
    $relPath = $relPath.Trim('"')

    if ($relPath -like 'Conversation/*' -or $relPath -like 'Conversation\*') {
        continue
    }

    $ext = [IO.Path]::GetExtension($relPath).ToLowerInvariant()
    if ($BinaryExtensions -contains $ext) {
        continue
    }

    $candidates.Add($relPath) | Out-Null
}

# Shell scripts must be LF: bash rejects CRLF. For a path ending .sh the test is inverted.
function Test-IsShellScript([string]$Path) {
    return [IO.Path]::GetExtension($Path).ToLowerInvariant() -eq '.sh'
}

# True when the file violates its rule: a bare LF for CRLF files, any CR byte for shell scripts.
function Test-WrongEol([string]$Path) {
    $bytes = [IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $Path))
    if (Test-IsShellScript $Path) {
        return ($bytes -contains 0x0D)
    }

    for ($i = 0; $i -lt $bytes.Length; $i++) {
        if ($bytes[$i] -eq 0x0A) {
            if ($i -eq 0 -or $bytes[$i - 1] -ne 0x0D) {
                return $true
            }
        }
    }

    return $false
}

$offending = New-Object System.Collections.Generic.List[string]
foreach ($relPath in $candidates) {
    if (-not (Test-Path -LiteralPath $relPath -PathType Leaf)) {
        continue
    }

    if (Test-WrongEol $relPath) {
        $offending.Add($relPath) | Out-Null
    }
}

if ($offending.Count -eq 0) {
    Write-Host 'All checked files are CRLF.'
    exit 0
}

Write-Host 'Files with non-CRLF line endings:'
foreach ($f in $offending) {
    if (Test-IsShellScript $f) {
        Write-Host "  $f - contains CRLF (shell scripts must be LF)"
    }
    else {
        Write-Host "  $f"
    }
}

if ($Fix) {
    $crlfTargets = New-Object System.Collections.Generic.List[string]
    foreach ($relPath in $offending) {
        if (Test-IsShellScript $relPath) {
            # Rewritten here, not through Fix-Crlf.ps1: \r\n -> \n, UTF-8 without a BOM added.
            $fullPath = (Resolve-Path -LiteralPath $relPath).Path
            $text = [IO.File]::ReadAllText($fullPath)
            [IO.File]::WriteAllText($fullPath, $text.Replace("`r`n", "`n"), (New-Object System.Text.UTF8Encoding($false)))
        }
        else {
            $crlfTargets.Add($relPath) | Out-Null
        }
    }

    if ($crlfTargets.Count -gt 0) {
        $fixScript = Join-Path $PSScriptRoot 'Fix-Crlf.ps1'
        & $fixScript -Path $crlfTargets
    }

    # Re-check the fixed files to verify they now satisfy their rule
    $stillOffending = New-Object System.Collections.Generic.List[string]
    foreach ($relPath in $offending) {
        if (-not (Test-Path -LiteralPath $relPath -PathType Leaf)) {
            continue
        }

        if (Test-WrongEol $relPath) {
            $stillOffending.Add($relPath) | Out-Null
        }
    }

    if ($stillOffending.Count -eq 0) {
        Write-Host 'All files are now CRLF (shell scripts LF) after fix.'
        exit 0
    } else {
        Write-Host 'Files still with non-CRLF line endings after fix:'
        foreach ($f in $stillOffending) {
            Write-Host "  $f"
        }
        exit 1
    }
}

exit 1
