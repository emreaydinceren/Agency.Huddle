<#
.SYNOPSIS
    Scans configured source folders for public top-level type declarations not on an allowlist file,
    and prints any it finds.

.DESCRIPTION
    Scans, under the CALLER's git toplevel (`git rev-parse --show-toplevel`), the folders in -ScanDirs
    (repo-relative, forward or back slashes) for:
      - `**/*.cs` files (a plain C# file, or a `.razor.cs` code-behind file)
      - `**/*.razor` files (a Razor component)

    Defaults to today's Tasks feature folders (`src/Huddle.App/Tasks` and
    `src/Huddle.App/Components/Tasks`) and the allowlist to `public-types.example.txt` next to this
    script - override both with -ScanDirs and -AllowlistPath for a different feature or project; each
    project keeps its own allowlist.

    For a .cs (or .razor.cs code-behind) file, a "top-level" type declaration is one with no leading
    indentation - this codebase uses file-scoped namespaces (`namespace X;`), so every type that is not
    nested inside another type starts at column 0. For a .razor file, a Razor component is public by
    definition, so only types declared inside an `@code { ... }` block are considered; those are indented
    one level (4 spaces) under the block.

    In both cases, a `partial class` at that top level is treated as the component's own generated class
    (or its code-behind partial) and is never flagged - "a component is public by definition" per the
    task. Every OTHER public class/record/struct/enum/interface at that level is compared against the
    allowlist (one type name per line, in -AllowlistPath). Anything found that is not on the list is
    printed, one line per offending type as "<file>:<line>: <kind> <Name>", and the script exits 1. With
    nothing to report, it prints a one-line summary and exits 0.

    This is a scan, not a compiler: it does not resolve `#if` blocks, does not parse string/char literals
    for embedded braces, and (deliberately, per the task) does not try to determine whether a flagged type
    is used outside its assembly - it only reports what is marked `public`.

.PARAMETER ScanDirs
    One or more repo-relative folders to scan recursively for *.cs and *.razor files. Defaults to
    "src/Huddle.App/Tasks" and "src/Huddle.App/Components/Tasks" (today's Tasks feature folders). A
    folder that doesn't exist under the caller's git toplevel is skipped silently (so the same default
    works before and after a feature's folders exist).

.PARAMETER AllowlistPath
    Override for the allowlist file (one type name per line). Defaults to public-types.example.txt next
    to this script.

.EXAMPLE
    pwsh agents/scripts/Check-Visibility.ps1

.EXAMPLE
    pwsh agents/scripts/Check-Visibility.ps1 -ScanDirs "src/Huddle.App/Library" -AllowlistPath agents/scripts/library-public-types.txt
#>
[CmdletBinding()]
param(
    [string[]]$ScanDirs = @('src/Huddle.App/Tasks', 'src/Huddle.App/Components/Tasks'),
    [string]$AllowlistPath
)

$ErrorActionPreference = 'Stop'

if (-not $AllowlistPath) {
    $AllowlistPath = Join-Path $PSScriptRoot 'public-types.example.txt'
}

if (-not (Test-Path -LiteralPath $AllowlistPath -PathType Leaf)) {
    throw "Allowlist not found: $AllowlistPath"
}

$allowlist = [System.Collections.Generic.HashSet[string]]::new(
    [string[]]@(Get-Content -LiteralPath $AllowlistPath | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne '' -and -not $_.StartsWith('#') }))

$worktreeRoot = (git rev-parse --show-toplevel).Trim()

# <indent>public<modifiers...> (record struct|record class|class|record|struct|enum|interface) Name
# Accessibility is always first per this repo's modifier order (agents/CSharpPrinciples.md), so `public`
# anchors the match; "record struct"/"record class" are tried before the bare "record" alternative so a
# `record struct Foo` declaration reports kind "record struct", not kind "record" with name "struct".
$typePattern = '^(?<indent>[ \t]*)public\s+(?<modifiers>(?:(?:sealed|abstract|static|partial|readonly|unsafe|new)\s+)*)(?<kind>record\s+struct|record\s+class|class|record|struct|enum|interface)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)'

<#
.SYNOPSIS
    Scans an array of lines for public type declarations at exactly the given indentation, returning
    those not treated as the file's own component/partial class.
#>
function Get-PublicTypesAtIndent {
    param([string[]]$Lines, [string]$RequiredIndent, [string]$FilePath, [int]$LineNumberOffset = 0)

    $found = New-Object System.Collections.Generic.List[object]
    for ($i = 0; $i -lt $Lines.Length; $i++) {
        $line = $Lines[$i]
        $match = [regex]::Match($line, $typePattern)
        if (-not $match.Success) {
            continue
        }
        if ($match.Groups['indent'].Value -ne $RequiredIndent) {
            continue
        }

        $isPartialClass = ($match.Groups['kind'].Value -eq 'class') -and
            ($line -match '\bpartial\s+class\b')
        if ($isPartialClass) {
            continue
        }

        $found.Add([PSCustomObject]@{
            File = $FilePath
            Line = $i + 1 + $LineNumberOffset
            Kind = $match.Groups['kind'].Value
            Name = $match.Groups['name'].Value
        }) | Out-Null
    }

    return $found
}

$violations = New-Object System.Collections.Generic.List[object]
$typesSeen = 0

foreach ($scanDir in $ScanDirs) {
    $fullScanDir = Join-Path $worktreeRoot ($scanDir -replace '/', '\')
    if (-not (Test-Path -LiteralPath $fullScanDir)) {
        continue
    }

    $csFiles = Get-ChildItem -LiteralPath $fullScanDir -Recurse -Include '*.cs' -File
    foreach ($file in $csFiles) {
        # An ordinary C# file, or a .razor.cs code-behind: both are column-0 top-level.
        $lines = Get-Content -LiteralPath $file.FullName
        $types = Get-PublicTypesAtIndent -Lines $lines -RequiredIndent '' -FilePath $file.FullName
        $typesSeen += $types.Count
        foreach ($t in $types) {
            if (-not $allowlist.Contains($t.Name)) {
                $violations.Add($t) | Out-Null
            }
        }
    }

    $razorFiles = Get-ChildItem -LiteralPath $fullScanDir -Recurse -Include '*.razor' -File
    foreach ($file in $razorFiles) {
        # The component itself is public by definition and out of scope. Only @code-declared types
        # count, and those sit one indent level (4 spaces) inside the @code block.
        $text = Get-Content -LiteralPath $file.FullName -Raw
        $codeMatch = [regex]::Match($text, '@code\s*\{')
        while ($codeMatch.Success) {
            $openBraceIndex = $codeMatch.Index + $codeMatch.Length - 1
            $depth = 0
            $endIndex = -1
            for ($i = $openBraceIndex; $i -lt $text.Length; $i++) {
                if ($text[$i] -eq '{') {
                    $depth++
                }
                elseif ($text[$i] -eq '}') {
                    $depth--
                    if ($depth -eq 0) {
                        $endIndex = $i
                        break
                    }
                }
            }

            if ($endIndex -ge 0) {
                $blockText = $text.Substring($openBraceIndex + 1, $endIndex - $openBraceIndex - 1)
                $lineNumberOffset = ($text.Substring(0, $openBraceIndex + 1) -split "`n").Length - 1
                $blockLines = $blockText -split "`r?`n"
                $types = Get-PublicTypesAtIndent -Lines $blockLines -RequiredIndent '    ' -FilePath $file.FullName -LineNumberOffset $lineNumberOffset
                $typesSeen += $types.Count
                foreach ($t in $types) {
                    if (-not $allowlist.Contains($t.Name)) {
                        $violations.Add($t) | Out-Null
                    }
                }
            }

            $codeMatch = $codeMatch.NextMatch()
        }
    }
}

if ($violations.Count -gt 0) {
    Write-Host "PUBLIC TYPES NOT ON THE ALLOWLIST ($AllowlistPath):"
    foreach ($v in $violations) {
        Write-Host "  $($v.File):$($v.Line): $($v.Kind) $($v.Name)"
    }
    exit 1
}

Write-Host "OK: $typesSeen public top-level type(s) scanned, all on the allowlist ($AllowlistPath)."
exit 0
