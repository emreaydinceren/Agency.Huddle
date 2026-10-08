<#
.SYNOPSIS
    Generates src/Huddle.App/Skills/SkillsCatalog.json from SKILLS-CATALOG.md and the local skill clones.

.DESCRIPTION
    Reads the "All skills" table of the Markdown catalog (columns Category, Sub Category, Publisher, Repo,
    Path, Skill Front Matter) and emits one metadata entry per row except category
    "Fixtures & Non-Skills". The commit comes from `git -C <clone> rev-parse HEAD`, the licence from the
    skill folder's own LICENSE* file or else the clone's LICENSE ("none" when neither exists). The script
    never edits the Markdown and never copies skill text (only the front-matter name and description).
    Output is a JSON array sorted by id (ordinal), indented, UTF-8 without BOM, LF endings.

    Exits 1 listing every row whose clone or skill folder is missing, and also when the entry count does
    not equal the Markdown header's SKILL.md count minus the fixture rows. Supports -WhatIf.

.PARAMETER CatalogMd
    Path to SKILLS-CATALOG.md.

.PARAMETER ClonesRoot
    Folder holding one clone per publisher (<ClonesRoot>/<Publisher>). Defaults to the catalog's folder.

.PARAMETER OutFile
    Where to write the JSON. Defaults to src/Huddle.App/Skills/SkillsCatalog.json in this repo.

.EXAMPLE
    pwsh -NoProfile -File agents/scripts/Build-SkillsCatalog.ps1
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$CatalogMd = 'E:\Projects\Software Factories\SKILLS-CATALOG.md',
    [string]$ClonesRoot,
    [string]$OutFile
)

$ErrorActionPreference = 'Stop'

if (-not $ClonesRoot) { $ClonesRoot = Split-Path -Parent $CatalogMd }
if (-not $OutFile) { $OutFile = Join-Path $PSScriptRoot '..\..\src\Huddle.App\Skills\SkillsCatalog.json' }
if (-not (Test-Path -LiteralPath $CatalogMd)) { throw "Catalog not found: $CatalogMd" }

$fixtureCategory = 'Fixtures & Non-Skills'
$lines = Get-Content -LiteralPath $CatalogMd -Encoding utf8

$expectedTotal = $null
foreach ($line in $lines) {
    if ($line -match '(\d+) SKILL\.md files') { $expectedTotal = [int]$Matches[1]; break }
}

$start = [array]::IndexOf($lines, ($lines | Where-Object { $_ -match '^## All skills' } | Select-Object -First 1))
if ($start -lt 0) { throw "No '## All skills' heading in $CatalogMd" }

function Get-LicenseId([string]$file) {
    $text = Get-Content -LiteralPath $file -Raw -Encoding utf8
    if ($text -match '(?i)\bMIT License\b') { return 'MIT' }
    if ($text -match '(?i)Apache License') { return 'Apache-2.0' }
    if ($text -match '(?i)\bBSD\b|Redistribution and use in source and binary forms') {
        if ($text -match 'Neither the name') { return 'BSD-3-Clause' }
        return 'BSD-2-Clause'
    }
    $first = ($text -split "\r?\n" | Where-Object { $_.Trim() } | Select-Object -First 1)
    if ($first) { return $first.Trim() }
    return 'none'
}

function Find-LicenseFile([string]$folder) {
    if (-not (Test-Path -LiteralPath $folder -PathType Container)) { return $null }
    Get-ChildItem -LiteralPath $folder -File -Filter 'LICENSE*' -ErrorAction SilentlyContinue |
        Sort-Object Name | Select-Object -First 1
}

$commits = @{}
$missing = [System.Collections.Generic.List[string]]::new()
$entries = [System.Collections.Generic.SortedDictionary[string, object]]::new([System.StringComparer]::Ordinal)
$fixtureRows = 0
$rows = 0

for ($i = $start + 1; $i -lt $lines.Count; $i++) {
    $line = $lines[$i]
    if ($line -notmatch '^\| ') { continue }
    $cells = [regex]::Split($line, '(?<!\\)\|') | ForEach-Object { $_.Trim() }
    # Leading and trailing empty cells come from the outer pipes.
    if ($cells.Count -lt 8) { continue }
    $category = $cells[1]; $sub = $cells[2]; $publisher = $cells[3]; $repoSlug = $cells[4]
    if ($category -eq 'Category' -or $category -match '^-+$') { continue }
    $rows++
    if ($category -eq $fixtureCategory) { $fixtureRows++; continue }

    if ($cells[5] -notmatch '\[\[.*?\\\|(?<p>.+?)\]\]') { $missing.Add("row $($i + 1): unparseable path cell"); continue }
    $skillMd = $Matches['p']
    $folderPath = if ($skillMd -match '^(?<f>.+)/SKILL\.md$') { $Matches['f'] } else { '' }

    if ($cells[6] -notmatch '^\*\*(?<n>.+?)\*\* — (?<d>.*?)(?: _\(other keys: .*\)_)?$') { $missing.Add("row $($i + 1): unparseable front matter"); continue }
    $name = $Matches['n'].Replace('\|', '|')
    $description = $Matches['d'].Replace('\|', '|').Trim()

    $clone = Join-Path $ClonesRoot $publisher
    $skillFolder = if ($folderPath) { Join-Path $clone $folderPath } else { $clone }
    if (-not (Test-Path -LiteralPath $skillFolder -PathType Container)) {
        $missing.Add("row $($i + 1): $publisher/$folderPath (folder missing)")
        continue
    }

    if (-not $commits.ContainsKey($clone)) {
        $sha = (& git -C $clone rev-parse HEAD 2>$null)
        if ($LASTEXITCODE -ne 0 -or $sha -notmatch '^[0-9a-f]{40}$') { $missing.Add("row $($i + 1): $publisher is not a git clone"); continue }
        $commits[$clone] = $sha.Trim()
    }
    $commit = $commits[$clone]

    $licenseFile = Find-LicenseFile $skillFolder
    if (-not $licenseFile) { $licenseFile = Find-LicenseFile $clone }
    $license = if ($licenseFile) { Get-LicenseId $licenseFile.FullName } else { 'none' }

    $repoUrl = "https://github.com/$repoSlug"
    $skillLeaf = if ($folderPath) { ($folderPath -split '/')[-1] } else { ($repoSlug -split '/')[-1] }
    $id = "$repoSlug/$skillLeaf"
    $link = if ($folderPath) { "$repoUrl/tree/$commit/$folderPath" } else { "$repoUrl/tree/$commit" }

    if ($entries.ContainsKey($id)) { $missing.Add("row $($i + 1): duplicate id $id"); continue }
    $entries[$id] = [ordered]@{
        id          = $id
        name        = $name
        description = $description
        publisher   = $publisher
        repo        = $repoUrl
        path        = $folderPath
        link        = $link
        commit      = $commit
        license     = $license
        category    = $category
        subCategory = $sub
        requires    = @()
    }
}

if ($missing.Count -gt 0) {
    Write-Host "Skipped rows ($($missing.Count)):" -ForegroundColor Red
    $missing | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
    exit 1
}

$expectedEntries = if ($null -ne $expectedTotal) { $expectedTotal - $fixtureRows } else { $rows - $fixtureRows }
$json = ConvertTo-Json -InputObject @($entries.Values) -Depth 5
$json = ($json -replace "`r`n", "`n").TrimEnd() + "`n"

if ($PSCmdlet.ShouldProcess($OutFile, "Write $($entries.Count) catalog entries")) {
    $target = [System.IO.Path]::GetFullPath($OutFile, (Get-Location).Path)
    [System.IO.File]::WriteAllText($target, $json, [System.Text.UTF8Encoding]::new($false))
}

Write-Host "Entries: $($entries.Count); Markdown header count minus $fixtureRows fixtures: $expectedEntries; out: $OutFile"
if ($entries.Count -ne $expectedEntries) {
    Write-Host 'Entry count does not match the Markdown header.' -ForegroundColor Red
    exit 1
}
exit 0
