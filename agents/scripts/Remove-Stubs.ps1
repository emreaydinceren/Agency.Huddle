<#
.SYNOPSIS
    Removes throwaway stub blocks from *.cs files: every line from one containing "// <stub" through the
    next line containing "// </stub>", inclusive.

.DESCRIPTION
    Test tasks build once against stubs of the missing names, placed inside an existing source file and
    wrapped in the markers above. This removes them again and prints each block removed. Line endings of
    the surviving text are preserved byte for byte (the file is split on LF and rejoined). A "// <stub"
    with no closing marker is reported and the file is left unchanged. bin/ and obj/ are skipped.

.PARAMETER Root
    Folder to scan. Defaults to src.

.PARAMETER WhatIf
    Lists what would be removed and changes nothing.

.EXAMPLE
    pwsh agents/scripts/Remove-Stubs.ps1 -WhatIf
#>
[CmdletBinding()]
param(
    [string]$Root = 'src',
    [switch]$WhatIf
)

$ErrorActionPreference = 'Stop'

$files = Get-ChildItem -LiteralPath $Root -Recurse -Filter '*.cs' -File |
    Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' }

$total = 0
foreach ($file in $files) {
    $text = [System.IO.File]::ReadAllText($file.FullName)
    if (-not $text.Contains('// <stub')) {
        continue
    }

    $lines = $text -split "`n", 0, 'SimpleMatch'
    $kept = New-Object System.Collections.Generic.List[string]
    $inStub = $false
    $start = 0
    $broken = $false
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        if (-not $inStub -and $line.Contains('// <stub') -and -not $line.Contains('// </stub>')) {
            $inStub = $true
            $start = $i + 1
        }

        if ($inStub) {
            Write-Host ('{0}:{1}: {2}' -f $file.FullName, ($i + 1), $line.TrimEnd("`r"))
            if ($line.Contains('// </stub>')) {
                $inStub = $false
                $total++
            }
            continue
        }

        $kept.Add($line)
    }

    if ($inStub) {
        Write-Host "UNCLOSED stub starting at $($file.FullName):$start - file left unchanged" -ForegroundColor Yellow
        continue
    }

    if (-not $WhatIf) {
        [System.IO.File]::WriteAllText($file.FullName, ($kept -join "`n"), (New-Object System.Text.UTF8Encoding($false)))
    }
}

$verb = if ($WhatIf) { 'would remove' } else { 'removed' }
Write-Host "$verb $total stub block(s)"
