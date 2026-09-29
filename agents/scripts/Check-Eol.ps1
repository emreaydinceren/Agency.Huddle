<#
.SYNOPSIS
    Checks that every added/modified/untracked file in the working tree (excluding Conversation/ and
    binary extensions) has CRLF line endings, as the repo's .editorconfig requires for text files.

.DESCRIPTION
    Lists candidate paths from `git status --porcelain --untracked-files=all`, skips anything under
    Conversation/ (gitignored, not part of the repo's tracked convention) and common binary extensions,
    then inspects each remaining file's bytes directly: a bare `\n` not preceded by `\r` means the file
    is not pure CRLF.

.PARAMETER Fix
    When set, runs Fix-Crlf.ps1 (resolved next to this script via $PSScriptRoot) on every offending file
    after reporting them.

.EXAMPLE
    pwsh agents/scripts/Check-Eol.ps1

.EXAMPLE
    pwsh agents/scripts/Check-Eol.ps1 -Fix
#>
[CmdletBinding()]
param(
    [switch]$Fix
)

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

    # Shell scripts must stay LF: bash rejects CRLF (the crlf-after-write hook skips them too).
    if ($ext -eq '.sh') {
        continue
    }

    $candidates.Add($relPath) | Out-Null
}

$offending = New-Object System.Collections.Generic.List[string]
foreach ($relPath in $candidates) {
    if (-not (Test-Path -LiteralPath $relPath -PathType Leaf)) {
        continue
    }

    $bytes = [IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $relPath))
    $bareLf = $false
    for ($i = 0; $i -lt $bytes.Length; $i++) {
        if ($bytes[$i] -eq 0x0A) {
            if ($i -eq 0 -or $bytes[$i - 1] -ne 0x0D) {
                $bareLf = $true
                break
            }
        }
    }

    if ($bareLf) {
        $offending.Add($relPath) | Out-Null
    }
}

if ($offending.Count -eq 0) {
    Write-Host 'All checked files are CRLF.'
    exit 0
}

Write-Host 'Files with non-CRLF line endings:'
foreach ($f in $offending) {
    Write-Host "  $f"
}

if ($Fix) {
    $fixScript = Join-Path $PSScriptRoot 'Fix-Crlf.ps1'
    & $fixScript -Path $offending

    # Re-check the fixed files to verify they are now CRLF
    $stillOffending = New-Object System.Collections.Generic.List[string]
    foreach ($relPath in $offending) {
        if (-not (Test-Path -LiteralPath $relPath -PathType Leaf)) {
            continue
        }

        $bytes = [IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $relPath))
        $bareLf = $false
        for ($i = 0; $i -lt $bytes.Length; $i++) {
            if ($bytes[$i] -eq 0x0A) {
                if ($i -eq 0 -or $bytes[$i - 1] -ne 0x0D) {
                    $bareLf = $true
                    break
                }
            }
        }

        if ($bareLf) {
            $stillOffending.Add($relPath) | Out-Null
        }
    }

    if ($stillOffending.Count -eq 0) {
        Write-Host 'All files are now CRLF after fix.'
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
