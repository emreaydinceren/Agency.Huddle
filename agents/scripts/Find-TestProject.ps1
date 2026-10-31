<#
.SYNOPSIS
    Prints the full path of every test project that declares a class or test method matching a pattern.

.DESCRIPTION
    Looks in every tests/*/ folder whose csproj sets IsTestProject to true, at the .cs files outside
    bin and obj.
      -Class   takes the text after the last '.', turns '*' into \w*, and matches
               \b(class|record)\s+<that>\b.
      -Method  gets the same treatment and matches \b<that>\s*\( in files that contain [Fact or [Theory.
    Writes the matching csproj full paths to the pipeline, sorted and unique. Writes nothing on no match.

.PARAMETER Class
    Class or record name pattern; '*' is a wildcard; a namespace prefix is ignored.

.PARAMETER Method
    Method name pattern; '*' is a wildcard; a class prefix is ignored.

.PARAMETER Root
    Repository root. Defaults to the git toplevel.

.EXAMPLE
    pwsh agents/scripts/Find-TestProject.ps1 -Class '*PromptGoldenTests'
#>
[CmdletBinding()]
param(
    [string]$Class,
    [string]$Method,
    [string]$Root
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not $Root) {
    $Root = (& git rev-parse --show-toplevel).Trim()
}

function ConvertTo-NameRegex {
    param([string]$Pattern)
    $name = $Pattern.Substring($Pattern.LastIndexOf('.') + 1)
    return (($name -split '\*' | ForEach-Object { [regex]::Escape($_) }) -join '\w*')
}

$classRegex = if ($Class) { '\b(class|record)\s+' + (ConvertTo-NameRegex $Class) + '\b' }
$methodRegex = if ($Method) { '\b' + (ConvertTo-NameRegex $Method) + '\s*\(' }

$testsDir = Join-Path $Root 'tests'
$found = [System.Collections.Generic.SortedSet[string]]::new([StringComparer]::OrdinalIgnoreCase)

if ((Test-Path -LiteralPath $testsDir) -and ($classRegex -or $methodRegex)) {
    foreach ($dir in Get-ChildItem -LiteralPath $testsDir -Directory) {
        foreach ($proj in Get-ChildItem -LiteralPath $dir.FullName -Filter '*.csproj' -File) {
            $projText = Get-Content -LiteralPath $proj.FullName -Raw
            if (($projText -notmatch '<IsTestProject>\s*true\s*</IsTestProject>') -and ($projText -notmatch 'Huddle\.Tests\.Feature\.props')) {
                continue
            }
            $prefix = $dir.FullName.TrimEnd('\', '/')
            $files = Get-ChildItem -LiteralPath $dir.FullName -Filter '*.cs' -File -Recurse | Where-Object {
                $relative = $_.FullName.Substring($prefix.Length).Replace('\', '/')
                $relative -notmatch '/(bin|obj)/'
            }
            foreach ($file in $files) {
                $text = Get-Content -LiteralPath $file.FullName -Raw
                if ($classRegex -and ($text -cmatch $classRegex)) {
                    [void]$found.Add($proj.FullName)
                    break
                }
                if ($methodRegex -and ($text -match '\[(Fact|Theory)') -and ($text -cmatch $methodRegex)) {
                    [void]$found.Add($proj.FullName)
                    break
                }
            }
        }
    }
}

foreach ($path in $found) {
    Write-Output $path
}
