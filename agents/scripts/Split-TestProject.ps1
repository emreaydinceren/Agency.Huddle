<#
.SYNOPSIS
    Reports on and performs the move of test files from the old Domain and Components test projects
    into the per-feature test projects (Design 5.2 of docs/Huddle.TestProjectSplit-ProjectPlan.md).

.DESCRIPTION
    The map (test-split-map.psd1) assigns each file of an old project to a feature. Resolution order:
    Files, then RootFiles, then the longest matching Folders prefix, then Fallback (only for files
    directly in that folder), else unmapped. Files in Keep, *.csproj files and anything under bin/ or obj/
    are never listed. Files are read from the working tree (tracked plus untracked, not HEAD), so the
    result is right after uncommitted moves.

    -Report                     One line per feature, then every unmapped file. Line shape:
                                    <Feature>  old: <files> files, <n> tests  new: <n> tests
                                where <n> counts [Fact / [Theory attribute lines. Exit 1 if any file is unmapped.
    -Feature <Name>             Lists "old -> new" for the files that go to tests/Huddle.Tests.<Name>/.
    -Feature <Name> -Apply      git mv's each listed file (creating folders). Refuses (exit 1, moves nothing)
                                if the target csproj is missing, a source is untracked or a target exists.
                                Prints one "moved N files to ..." line; -Verbose also lists old -> new.
    -Feature <Name> -Create     Step 1 of a feature split: writes tests/Huddle.Tests.<Name>/ (csproj, xunit.runner.json,
                                <Name>AssemblySetupTests.cs, all from templates embedded in this script) and adds the
                                Huddle.slnx and Huddle.TestAssemblies.props lines. Idempotent (exit 0 when all exist);
                                exit 1 if only some exist.
    -Root <path>                Repository root. Defaults to git rev-parse --show-toplevel.
    -MapPath <path>             The map. Defaults to test-split-map.psd1 next to this script.

.EXAMPLE
    pwsh agents/scripts/Split-TestProject.ps1 -Report
    pwsh agents/scripts/Split-TestProject.ps1 -Feature Tasks -Apply
#>
[CmdletBinding()]
param(
    [switch]$Report,
    [string]$Feature,
    [switch]$Apply,
    [switch]$Create,
    [string]$Root,
    [string]$MapPath = (Join-Path $PSScriptRoot 'test-split-map.psd1')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not $Report -and -not $Feature) {
    Write-Output 'Specify -Report or -Feature <Name> [-Apply].'
    exit 1
}
if (($Apply -or $Create) -and -not $Feature) {
    Write-Output '-Apply and -Create need -Feature <Name>.'
    exit 1
}

if (-not $Root) {
    $Root = (& git rev-parse --show-toplevel).Trim()
}
$Root = (Resolve-Path -LiteralPath $Root).Path
$map = Import-PowerShellDataFile -LiteralPath $MapPath

function Read-TextFile {
    param([string]$Path)
    $bytes = [IO.File]::ReadAllBytes($Path)
    $bom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
    return [pscustomobject]@{ Text = [IO.File]::ReadAllText($Path); Bom = $bom }
}

function Write-TextFile {
    param([string]$Path, [string]$Text, [bool]$Bom = $false)
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Path) | Out-Null
    [IO.File]::WriteAllText($Path, $Text, [System.Text.UTF8Encoding]::new($Bom))
}

function Add-LineAfterMatch {
    # Inserts $NewLine after the last line matching $Pattern (or before the first match when $Before), keeping the file's newline style.
    param([string]$Path, [string]$Pattern, [string]$NewLine, [switch]$Before)
    $file = Read-TextFile $Path
    $nl = if ($file.Text.Contains("`r`n")) { "`r`n" } else { "`n" }
    $lines = [System.Collections.Generic.List[string]]::new([string[]]($file.Text -split "`r?`n"))
    $index = -1
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match $Pattern) {
            $index = $i
            if ($Before) { break }
        }
    }
    if ($index -lt 0) { return $false }
    $lines.Insert($(if ($Before) { $index } else { $index + 1 }), $NewLine)
    Write-TextFile $Path ($lines -join $nl) $file.Bom
    return $true
}

function Invoke-Create {
    param([string]$Name)
    if ($Name -notmatch '^[A-Za-z][A-Za-z0-9]*$') {
        Write-Output "Refusing: '$Name' is not a valid feature name."
        return 1
    }
    $dir = Join-Path $Root "tests/Huddle.Tests.$Name"
    $csprojPath = Join-Path $dir "Huddle.Tests.$Name.csproj"
    $runnerPath = Join-Path $dir 'xunit.runner.json'
    $setupPath = Join-Path $dir "${Name}AssemblySetupTests.cs"
    $slnxPath = Join-Path $Root 'Huddle.slnx'
    $propsPath = Join-Path $Root 'tests/Huddle.TestAssemblies.props'
    $slnxLine = "    <Project Path=`"tests/Huddle.Tests.$Name/Huddle.Tests.$Name.csproj`" />"
    $propsLine = "    <InternalsVisibleTo Include=`"Huddle.Tests.$Name`" />"

    $slnxHas = (Read-TextFile $slnxPath).Text.Contains($slnxLine.Trim())
    $propsHas = (Read-TextFile $propsPath).Text.Contains($propsLine.Trim())
    $states = @(
        (Test-Path -LiteralPath $csprojPath), (Test-Path -LiteralPath $runnerPath),
        (Test-Path -LiteralPath $setupPath), $slnxHas, $propsHas)
    $present = @($states | Where-Object { $_ }).Count
    if ($present -eq $states.Count) {
        Write-Output "tests/Huddle.Tests.$Name/ already exists; nothing to do."
        return 0
    }
    if ($present -gt 0) {
        Write-Output "Refusing: tests/Huddle.Tests.$Name/ is partly created (csproj=$($states[0]) runner=$($states[1]) setup=$($states[2]) slnx=$($states[3]) props=$($states[4]))."
        return 1
    }

    $csproj = "<Project Sdk=`"Microsoft.NET.Sdk`">`r`n  <Import Project=`"..\Huddle.Tests.Feature.props`" />`r`n</Project>`r`n"
    Write-TextFile $csprojPath $csproj
    Write-TextFile $runnerPath "{`r`n  `"`$schema`": `"https://xunit.net/schema/v3/xunit.runner.schema.json`",`r`n  `"maxParallelThreads`": `"1x`"`r`n}`r`n"
    $setupTemplate = @'
namespace Agency.Huddle.Tests;

/// <summary>
/// Pins that the <c>Huddle.Tests.__NAME__</c> assembly wires its own shared test-process setup: the temp-folder redirect
/// is applied by this assembly's initializer, and its runner file is pinned.
/// </summary>
public sealed class __NAME__AssemblySetupTests
{
    /// <summary>The assembly initializer applies the temp-folder redirect before any test runs.</summary>
    [Fact]
    public void ModuleInitializer_AppliedTheTempRedirect()
    {
        Assert.True(TestTempRedirect.HasRun);
    }

    /// <summary>The runner file pins parallelism to one thread per core, matching the other test projects.</summary>
    [Fact]
    public void RunnerConfig_MaxParallelThreads_IsPinned()
    {
        Assert.Equal("1x", RunnerConfig.MaxParallelThreads());
    }
}
'@
    $setupText = ($setupTemplate -replace "`r?`n", "`r`n").Replace('__NAME__', $Name) + "`r`n"
    Write-TextFile $setupPath $setupText
    if (-not (Add-LineAfterMatch $slnxPath '^\s*<Project Path="tests/' $slnxLine)) {
        Write-Output 'Refusing: Huddle.slnx has no tests/ Project line to insert after.'
        return 1
    }
    if (-not (Add-LineAfterMatch $propsPath '^\s*</ItemGroup>' $propsLine -Before)) {
        Write-Output 'Refusing: tests/Huddle.TestAssemblies.props has no </ItemGroup>.'
        return 1
    }
    Write-Output "created tests/Huddle.Tests.$Name/ (csproj, xunit.runner.json, ${Name}AssemblySetupTests.cs), Huddle.slnx and Huddle.TestAssemblies.props updated."
    return 0
}

if ($Create) {
    $createOutput = @(Invoke-Create $Feature)
    $createOutput | Select-Object -SkipLast 1 | ForEach-Object { Write-Output $_ }
    exit ([int]$createOutput[-1])
}

function Get-Section {
    param($Table, [string]$Old)
    if ($Table.ContainsKey($Old)) { return $Table[$Old] }
    return @{}
}

function Resolve-Feature {
    param([string]$Old, [string]$Relative)
    $files = Get-Section $map.Files $Old
    if ($files.ContainsKey($Relative)) { return $files[$Relative] }

    $slash = $Relative.LastIndexOf('/')
    $dir = if ($slash -lt 0) { '' } else { $Relative.Substring(0, $slash) }

    if ($dir -eq '') {
        $rootFiles = Get-Section $map.RootFiles $Old
        if ($rootFiles.ContainsKey($Relative)) { return $rootFiles[$Relative] }
        return $null
    }

    $folders = Get-Section $map.Folders $Old
    $parts = $dir.Split('/')
    for ($n = $parts.Count; $n -ge 1; $n--) {
        $key = $parts[0..($n - 1)] -join '/'
        if ($folders.ContainsKey($key)) { return $folders[$key] }
    }

    $fallback = Get-Section $map.Fallback $Old
    if ($fallback.ContainsKey($dir)) { return $fallback[$dir] }
    return $null
}

function Get-AllFeatures {
    $names = [System.Collections.Generic.SortedSet[string]]::new([StringComparer]::Ordinal)
    foreach ($section in @($map.Folders, $map.Files, $map.RootFiles, $map.Fallback)) {
        foreach ($byOld in $section.Values) {
            foreach ($name in $byOld.Values) { [void]$names.Add([string]$name) }
        }
    }
    return $names
}

function Get-WorkingTreeFiles {
    param([string]$ProjectDir)
    $listed = & git -C $Root -c core.quotepath=false ls-files -c -o --exclude-standard -- $ProjectDir
    foreach ($path in $listed) {
        if ($path -match '/(bin|obj)/' -or $path -like '*.csproj') { continue }
        if (Test-Path -LiteralPath (Join-Path $Root $path) -PathType Leaf) { $path }
    }
}

function Get-TestCount {
    param([string]$FullPath)
    $text = Get-Content -LiteralPath $FullPath -Raw
    if ([string]::IsNullOrEmpty($text)) { return 0 }
    return [regex]::Matches($text, '(?m)^\s*\[(Fact|Theory)\b').Count
}

# One entry per mapped or unmapped file of every old project.
$entries = [System.Collections.Generic.List[object]]::new()
foreach ($old in $map.Folders.Keys) {
    $oldDir = "tests/Huddle.Tests.$old"
    if (-not (Test-Path -LiteralPath (Join-Path $Root $oldDir))) { continue }
    foreach ($path in Get-WorkingTreeFiles $oldDir) {
        $relative = $path.Substring($oldDir.Length + 1)
        if ($map.Keep -contains $relative) { continue }
        $entries.Add([pscustomobject]@{
                Old      = $old
                Relative = $relative
                Path     = $path
                Feature  = (Resolve-Feature $old $relative)
            })
    }
}

if ($Report) {
    $features = @(Get-AllFeatures)
    foreach ($name in $features) {
        $mine = @($entries | Where-Object { $_.Feature -eq $name })
        $oldTests = 0
        foreach ($entry in $mine) {
            if ($entry.Path -like '*.cs') { $oldTests += Get-TestCount (Join-Path $Root $entry.Path) }
        }
        $newTests = 0
        $newDir = Join-Path $Root "tests/Huddle.Tests.$name"
        if (Test-Path -LiteralPath $newDir) {
            foreach ($file in Get-ChildItem -LiteralPath $newDir -Filter '*.cs' -File -Recurse) {
                $relative = $file.FullName.Substring($newDir.Length).Replace('\', '/')
                if ($relative -notmatch '/(bin|obj)/') { $newTests += Get-TestCount $file.FullName }
            }
        }
        Write-Output "$name  old: $($mine.Count) files, $oldTests tests  new: $newTests tests"
    }
    $unmapped = @($entries | Where-Object { $null -eq $_.Feature })
    foreach ($entry in $unmapped) {
        Write-Output "UNMAPPED $($entry.Path)"
    }
    if ($unmapped.Count -gt 0) { exit 1 }
    exit 0
}

# -Feature [-Apply]
if ((@(Get-AllFeatures)) -notcontains $Feature) {
    Write-Output "Unknown feature '$Feature'."
    exit 1
}

$newProject = "tests/Huddle.Tests.$Feature"
$moves = @($entries | Where-Object { $_.Feature -eq $Feature } | ForEach-Object {
        [pscustomobject]@{ From = $_.Path; To = "$newProject/$($_.Relative)" }
    })
if (-not $Apply -or $VerbosePreference -eq 'Continue') {
    foreach ($move in $moves) {
        Write-Output "$($move.From) -> $($move.To)"
    }
}
if (-not $Apply -or $moves.Count -eq 0) { exit 0 }

$csproj = Join-Path $Root "$newProject/Huddle.Tests.$Feature.csproj"
if (-not (Test-Path -LiteralPath $csproj)) {
    Write-Output "Refusing: $newProject/Huddle.Tests.$Feature.csproj does not exist."
    exit 1
}

$tracked = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($path in (& git -C $Root -c core.quotepath=false ls-files -c)) { [void]$tracked.Add($path) }
$targets = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$problems = [System.Collections.Generic.List[string]]::new()
foreach ($move in $moves) {
    if (-not $tracked.Contains($move.From)) { $problems.Add("untracked source: $($move.From)") }
    if ((Test-Path -LiteralPath (Join-Path $Root $move.To)) -or -not $targets.Add($move.To)) {
        $problems.Add("target exists: $($move.To)")
    }
}
if ($problems.Count -gt 0) {
    foreach ($problem in $problems) { Write-Output "Refusing: $problem" }
    exit 1
}

foreach ($move in $moves) {
    $targetDir = Split-Path -Parent (Join-Path $Root $move.To)
    New-Item -ItemType Directory -Force -Path $targetDir | Out-Null
    & git -C $Root mv -- $move.From $move.To
    if ($LASTEXITCODE -ne 0) {
        Write-Output "git mv failed for $($move.From)"
        exit 1
    }
}
Write-Output "moved $($moves.Count) files to $newProject/"
exit 0
