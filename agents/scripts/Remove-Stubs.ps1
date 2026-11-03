<#
.SYNOPSIS
    Removes throwaway stub blocks from *.cs files: every line from one containing "// <stub" through the
    next line containing "// </stub>", inclusive.

.DESCRIPTION
    Test tasks build once against stubs of the missing names, placed inside an existing source file and
    wrapped in the markers above. This removes them again and prints each block removed. Line endings of
    the surviving text are preserved byte for byte (the file is split on LF and rejoined), and a UTF-8 BOM
    is kept if the file had one. When the line after a block is blank, "}" or the end of the file, one blank
    line directly before the opener is dropped too, so the file ends as it was before the stub was added.

    Malformed markers exit 1 with a message and leave that file unchanged: a "// </stub>" with no opener,
    a "// <stub" with no closer, or one line holding both markers. bin/ and obj/ are skipped.

.PARAMETER Root
    Folder to scan. Defaults to src.

.PARAMETER WhatIf
    Lists what would be removed and changes nothing.

.PARAMETER ExpectClean
    After removal, exit 1 and print the output if `git diff --stat -- src/` is not empty (the red run must
    start from a tree that differs from HEAD in nothing but the intended test files). Skipped with -WhatIf.

.EXAMPLE
    pwsh agents/scripts/Remove-Stubs.ps1 -WhatIf
#>
[CmdletBinding()]
param(
    [string]$Root = 'src',
    [switch]$WhatIf,
    [switch]$ExpectClean
)

$ErrorActionPreference = 'Stop'

$files = Get-ChildItem -LiteralPath $Root -Recurse -Filter '*.cs' -File |
    Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' }

$total = 0
$failed = $false
foreach ($file in $files) {
    $bytes = [System.IO.File]::ReadAllBytes($file.FullName)
    $hasBom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
    $text = [System.IO.File]::ReadAllText($file.FullName)
    if (-not $text.Contains('// <stub') -and -not $text.Contains('// </stub>')) {
        continue
    }

    $lines = $text -split "`n", 0, 'SimpleMatch'
    $kept = New-Object System.Collections.Generic.List[string]
    $inStub = $false
    $start = 0
    $problem = $null
    $removed = 0
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        $opens = $line.Contains('// <stub')
        $closes = $line.Contains('// </stub>')
        if ($opens -and $closes) {
            $problem = "line holding both markers at $($file.FullName):$($i + 1)"
            break
        }

        if (-not $inStub -and $closes) {
            $problem = "closer without opener at $($file.FullName):$($i + 1)"
            break
        }

        if (-not $inStub -and $opens) {
            $inStub = $true
            $start = $i + 1
            if ($kept.Count -gt 0 -and $kept[$kept.Count - 1].Trim() -eq '') {
                $blockEnd = $i
                while ($blockEnd -lt $lines.Count -and -not $lines[$blockEnd].Contains('// </stub>')) {
                    $blockEnd++
                }

                $next = if ($blockEnd + 1 -lt $lines.Count) { $lines[$blockEnd + 1].Trim() } else { $null }
                if ($null -eq $next -or $next -eq '' -or $next -eq '}') {
                    $kept.RemoveAt($kept.Count - 1)
                }
            }
        }

        if ($inStub) {
            Write-Host ('{0}:{1}: {2}' -f $file.FullName, ($i + 1), $line.TrimEnd("`r"))
            if ($closes) {
                $inStub = $false
                $removed++
            }

            continue
        }

        $kept.Add($line)
    }

    if ($null -eq $problem -and $inStub) {
        $problem = "opener without closer at $($file.FullName):$start"
    }

    if ($null -ne $problem) {
        Write-Host "ERROR: $problem - file left unchanged" -ForegroundColor Red
        $failed = $true
        continue
    }

    $total += $removed
    if (-not $WhatIf) {
        [System.IO.File]::WriteAllText($file.FullName, ($kept -join "`n"), (New-Object System.Text.UTF8Encoding($hasBom)))
    }
}

$verb = if ($WhatIf) { 'would remove' } else { 'removed' }
Write-Host "$verb $total stub block(s)"

if ($failed) {
    exit 1
}

if ($ExpectClean -and -not $WhatIf) {
    $diff = git diff --stat -- src/
    if ($diff) {
        Write-Host 'ERROR: -ExpectClean: git diff --stat -- src/ is not empty after removal:' -ForegroundColor Red
        $diff | ForEach-Object { Write-Host $_ }
        exit 1
    }
}
