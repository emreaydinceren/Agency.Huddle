<#
.SYNOPSIS
    Lists every place a type is constructed, so a new constructor parameter is applied to all of them.

.DESCRIPTION
    Prints file:line for each line holding `new <Type>(` and, in files that mention <Type>, each line
    holding a target-typed `new(`, marked "target-typed, check" because the script cannot tell which
    type it builds. bin/ and obj/ are skipped.

.PARAMETER Type
    The type name, without namespace.

.PARAMETER Root
    Folders to scan. Defaults to src and tests.

.EXAMPLE
    pwsh agents/scripts/Find-Constructions.ps1 -Type TeamOptions
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Type,
    [string[]]$Root = @('src', 'tests')
)

$ErrorActionPreference = 'Stop'

$explicit = '\bnew\s+' + [regex]::Escape($Type) + '\s*(<[^>]*>)?\s*\('
$mention = '(?<![\w])' + [regex]::Escape($Type) + '(?![\w])'
$targetTyped = '(?<![\w.])new\s*\('

$count = 0
foreach ($folder in $Root) {
    if (-not (Test-Path -LiteralPath $folder)) {
        continue
    }

    $files = Get-ChildItem -LiteralPath $folder -Recurse -Include '*.cs', '*.razor' -File |
        Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' }
    foreach ($file in $files) {
        $lines = [System.IO.File]::ReadAllLines($file.FullName)
        $mentions = $false
        foreach ($line in $lines) {
            if ($line -match $mention) { $mentions = $true; break }
        }

        if (-not $mentions) {
            continue
        }

        for ($i = 0; $i -lt $lines.Count; $i++) {
            if ($lines[$i] -match $explicit) {
                Write-Host ('{0}:{1}: {2}' -f $file.FullName, ($i + 1), $lines[$i].Trim())
                $count++
            }
            elseif ($lines[$i] -match $targetTyped) {
                Write-Host ('{0}:{1}: {2}   [target-typed, check]' -f $file.FullName, ($i + 1), $lines[$i].Trim())
                $count++
            }
        }
    }
}

Write-Host "$count line(s)"
