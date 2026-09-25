<#
.SYNOPSIS
    Normalises one or more files to CRLF line endings, preserving a UTF-8 BOM if present, then marks any
    untracked ones intent-to-add so `git ls-files --eol` can report on them.

.DESCRIPTION
    Reads each file as text, collapses every line ending to `\n`, then rewrites every `\n` as `\r\n`.
    [IO.File]::WriteAllText with the default (no-encoding-specified) overload writes UTF-8 without a BOM,
    so a BOM the file had going in is detected first and re-applied after the rewrite.

    `git add -N` (intent-to-add) is run for the given paths - the one git index operation this script is
    allowed to perform - so a newly-created untracked file shows up in `git ls-files --eol`. It does NOT
    stage file contents; nothing here approaches `git add`, `git commit`, or `git stash`.

.PARAMETER Path
    One or more file paths to normalise.

.EXAMPLE
    pwsh agents/scripts/Fix-Crlf.ps1 -Path src/Huddle.App/Foo.cs

.EXAMPLE
    pwsh agents/scripts/Fix-Crlf.ps1 -Path (git status --porcelain --untracked-files=all | ForEach-Object { $_.Substring(3) })
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string[]]$Path
)

$ErrorActionPreference = 'Stop'

$Utf8Bom = [byte[]](0xEF, 0xBB, 0xBF)

foreach ($p in $Path) {
    $fullPath = Resolve-Path -LiteralPath $p | Select-Object -ExpandProperty Path

    $bytes = [IO.File]::ReadAllBytes($fullPath)
    $hasBom = ($bytes.Length -ge 3) -and ($bytes[0] -eq 0xEF) -and ($bytes[1] -eq 0xBB) -and ($bytes[2] -eq 0xBF)

    $text = [IO.File]::ReadAllText($fullPath)
    $normalized = (($text -replace "`r`n", "`n") -replace "`n", "`r`n")

    if ($hasBom) {
        $encoded = [Text.Encoding]::UTF8.GetBytes($normalized)
        $outBytes = New-Object byte[] ($Utf8Bom.Length + $encoded.Length)
        [Array]::Copy($Utf8Bom, 0, $outBytes, 0, $Utf8Bom.Length)
        [Array]::Copy($encoded, 0, $outBytes, $Utf8Bom.Length, $encoded.Length)
        [IO.File]::WriteAllBytes($fullPath, $outBytes)
    }
    else {
        [IO.File]::WriteAllText($fullPath, $normalized)
    }

    Write-Host "Fixed CRLF: $fullPath"
}

# Intent-to-add only, for untracked paths, so git can report their EOL state. This is the one git index
# operation this script performs.
& git add -N -- @Path 2>$null

& git ls-files --eol -- @Path
