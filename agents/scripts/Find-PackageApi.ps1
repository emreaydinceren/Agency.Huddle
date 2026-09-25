<#
.SYNOPSIS
    Prints a type's XML-doc summary and every member's summary from a restored NuGet package's shipped
    IntelliSense XML, without a disk-wide search.

.DESCRIPTION
    Resolves the package's doc XML directly from the local NuGet cache:

        %USERPROFILE%\.nuget\packages\<id-lowercase>\<version>\lib\<highest tfm>\*.xml

    - `<id-lowercase>` is -Package, lower-cased (the NuGet global packages folder convention).
    - `<version>` is -Version if given, otherwise the version resolved from the single
      `<PackageVersion Include="<Package>" Version="..." />` entry in the CALLER's
      `Directory.Packages.props` (found at the git toplevel via `git rev-parse --show-toplevel`, so this
      works unmodified from any worktree; case-insensitive match on Include).
    - `<highest tfm>` is chosen among the lib/ subfolders: dotted modern TFMs (netX.Y, e.g. net10.0) rank
      above legacy undotted ones (netXXX, e.g. net48), and within each group the numeric version wins -
      this avoids the trap of "net9.0" sorting after "net10.0" as plain strings.

    -Type matches a `<member name="T:...">` entry by its short (non-namespace-qualified) name. If -Type
    does not itself carry a generic-arity backtick suffix (e.g. "MudList`1"), the match also accepts one,
    so -Type "MudList" finds "T:MudBlazor.MudList`1". Once the type entry resolves to its full doc-XML
    name (e.g. "MudBlazor.MudList`1"), every `<member name="P:|M:|E:|F:<fullName>.*">` entry is treated as
    one of its members and printed as one line: "<kind> <remainder> - <summary>" (kind is P/M/E/F;
    remainder is the member name with parameters, generic method arity etc. exactly as the doc XML has it;
    summary text has embedded whitespace/newlines collapsed to single spaces).

.PARAMETER Package
    The NuGet package id, e.g. "MudBlazor". Case-insensitive; lower-cased for the cache-folder lookup.

.PARAMETER Type
    The type's short name (no namespace), e.g. "MudList" or "MudList`1".

.PARAMETER Version
    Override for the package version. Defaults to the version pinned in the caller's git toplevel's
    Directory.Packages.props.

.EXAMPLE
    pwsh agents/scripts/Find-PackageApi.ps1 -Package MudBlazor -Type MudList`1

.EXAMPLE
    pwsh agents/scripts/Find-PackageApi.ps1 -Package MudBlazor -Type MudTextField -Version 9.10.0
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$Package,
    [Parameter(Mandatory)]
    [string]$Type,
    [string]$Version
)

$ErrorActionPreference = 'Stop'

$worktreeRoot = (git rev-parse --show-toplevel).Trim()
$PackagesPropsPath = Join-Path $worktreeRoot 'Directory.Packages.props'

if (-not $Version) {
    if (-not (Test-Path -LiteralPath $PackagesPropsPath -PathType Leaf)) {
        throw "Directory.Packages.props not found: $PackagesPropsPath"
    }

    [xml]$propsXml = Get-Content -LiteralPath $PackagesPropsPath -Raw
    $versionNode = $propsXml.Project.ItemGroup.PackageVersion |
        Where-Object { $_.Include -and $_.Include.ToLowerInvariant() -eq $Package.ToLowerInvariant() } |
        Select-Object -First 1

    if (-not $versionNode) {
        throw "No <PackageVersion Include=`"$Package`" .../> found in $PackagesPropsPath - pass -Version explicitly."
    }

    $Version = $versionNode.Version
}

$packagesRoot = Join-Path $env:USERPROFILE '.nuget\packages'
$packageDir = Join-Path $packagesRoot (Join-Path $Package.ToLowerInvariant() $Version)

if (-not (Test-Path -LiteralPath $packageDir -PathType Container)) {
    throw "Package cache folder not found: $packageDir (is it restored?)"
}

$libDir = Join-Path $packageDir 'lib'
if (-not (Test-Path -LiteralPath $libDir -PathType Container)) {
    throw "No lib/ folder under $packageDir"
}

<#
.SYNOPSIS
    Ranks a TFM folder name so the highest-numbered, most-modern one sorts first: dotted "netX.Y" style
    TFMs outrank undotted legacy "netXXX" ones, and within a group the numeric version wins.
#>
function Get-TfmRank {
    param([string]$Tfm)

    if ($Tfm -match '^net(?<maj>\d+)\.(?<min>\d+)$') {
        return [PSCustomObject]@{ Modern = 1; Version = [version]"$($Matches.maj).$($Matches.min)" }
    }
    if ($Tfm -match '^net(?<digits>\d+)$') {
        $verStr = ($Matches.digits.ToCharArray() -join '.')
        $ver = $null
        if (-not [version]::TryParse($verStr, [ref]$ver)) {
            $ver = [version]'0.0'
        }
        return [PSCustomObject]@{ Modern = 0; Version = $ver }
    }

    return [PSCustomObject]@{ Modern = 0; Version = [version]'0.0' }
}

$tfmDirs = Get-ChildItem -LiteralPath $libDir -Directory
if ($tfmDirs.Count -eq 0) {
    throw "No target-framework folders under $libDir"
}

$highestTfmDir = $tfmDirs |
    ForEach-Object {
        $rank = Get-TfmRank -Tfm $_.Name
        [PSCustomObject]@{ Dir = $_; Modern = $rank.Modern; Version = $rank.Version }
    } |
    Sort-Object -Property Modern, Version -Descending |
    Select-Object -First 1 -ExpandProperty Dir

$xmlFiles = Get-ChildItem -LiteralPath $highestTfmDir.FullName -Filter '*.xml'
if ($xmlFiles.Count -eq 0) {
    throw "No .xml doc files under $($highestTfmDir.FullName)"
}

$typeEscaped = [regex]::Escape($Type)
$hasArity = $Type -match '`\d+$'
$arityTail = if ($hasArity) { '' } else { '(`\d+)?' }
$typePattern = "^T:(?:[\w.]+\.)?$typeEscaped$arityTail$"

$typeMember = $null
$sourceXmlFile = $null

foreach ($xmlFile in $xmlFiles) {
    [xml]$doc = Get-Content -LiteralPath $xmlFile.FullName -Raw
    $candidate = $doc.doc.members.member | Where-Object { $_.name -match $typePattern } | Select-Object -First 1
    if ($candidate) {
        $typeMember = $candidate
        $sourceXmlFile = $xmlFile.FullName
        $allMembers = $doc.doc.members.member
        break
    }
}

if (-not $typeMember) {
    throw "Type '$Type' not found in any of: $($xmlFiles.FullName -join ', ')"
}

function Get-SummaryText {
    param($MemberNode)

    $summaryNode = $MemberNode.SelectSingleNode('summary')
    if (-not $summaryNode) {
        return '(no summary)'
    }
    $text = $summaryNode.InnerText
    return (($text -replace '\s+', ' ').Trim())
}

$fullTypeName = $typeMember.name.Substring(2)

Write-Host "Type: $Type ($sourceXmlFile)"
Write-Host "Full name: $fullTypeName"
Write-Host "Summary: $(Get-SummaryText -MemberNode $typeMember)"
Write-Host ''
Write-Host 'Members:'

$memberPrefix = "$fullTypeName."
$memberPattern = '^(?<kind>[PMEF]):' + [regex]::Escape($memberPrefix) + '(?<remainder>.+)$'

$memberEntries = $allMembers | Where-Object { $_.name -match $memberPattern }

if ($memberEntries.Count -eq 0) {
    Write-Host '  (none found)'
    exit 0
}

foreach ($entry in $memberEntries) {
    $match = [regex]::Match($entry.name, $memberPattern)
    $kind = $match.Groups['kind'].Value
    $remainder = $match.Groups['remainder'].Value
    $summary = Get-SummaryText -MemberNode $entry
    Write-Host "  $kind $remainder - $summary"
}
