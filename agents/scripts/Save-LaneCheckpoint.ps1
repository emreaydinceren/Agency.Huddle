<#
.SYNOPSIS
    Saves a whole test lane (data folder, Team repositories, Adapter session logs) as a named checkpoint.

.DESCRIPTION
    Copies the lane's data folder, its repositories folder and the lane's Adapter folders under
    `<HomePath>\.claude\projects` into `<CheckpointRoot>\<lane>\<Name>`. Refuses unless the lane is idle: the
    port is closed, team.db, -wal and -shm can be opened exclusively, and no process has the lane root in its
    command line. Refuses while an interrupted restore is recorded, when CLAUDE_CONFIG_DIR is set, and when a
    captured tree holds junctions or symbolic links (unless -AllowJunctions; they are not copied). The copy is
    built under a temporary name and renamed only when complete; with -Force the old checkpoint is kept until
    the new one has moved in. manifest.json is written last and records each folder as present or absent.

.PARAMETER LaneRoot    The lane's root folder, e.g. C:\HuddleZero. A checkpoint restores to this same absolute path only.
.PARAMETER Port        The port the lane's app listens on.
.PARAMETER Name        Checkpoint name, e.g. after-ZERO-16.
.PARAMETER DataDir     Defaults to <LaneRoot>\data.
.PARAMETER ReposDir    Defaults to <LaneRoot>\repos.
.PARAMETER HomePath    Home whose .claude\projects holds the Adapter logs. Defaults to $HOME.
.PARAMETER CheckpointRoot  Where checkpoints live; must be outside the lane and the data folder. Defaults to $HOME\HuddleLaneCheckpoints.
.PARAMETER Force       Replace an existing checkpoint of the same name.
.PARAMETER AllowJunctions  Save although junctions exist; they are left out of the copy.

.EXAMPLE
    pwsh -NoProfile -File agents/scripts/Save-LaneCheckpoint.ps1 -LaneRoot C:\HuddleZero -Port 5100 -Name after-ZERO-16
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$LaneRoot,
    [Parameter(Mandatory)][int]$Port,
    [Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9][A-Za-z0-9._-]*$')][string]$Name,
    [string]$DataDir,
    [string]$ReposDir,
    [string]$HomePath = $HOME,
    [string]$CheckpointRoot = (Join-Path $HOME 'HuddleLaneCheckpoints'),
    [switch]$Force,
    [switch]$AllowJunctions
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'LaneCheckpointCommon.ps1')

$LaneRoot = Resolve-LanePath $LaneRoot
if (-not $DataDir) { $DataDir = Join-Path $LaneRoot 'data' }
if (-not $ReposDir) { $ReposDir = Join-Path $LaneRoot 'repos' }
$DataDir = Resolve-LanePath $DataDir
$ReposDir = Resolve-LanePath $ReposDir
$CheckpointRoot = Resolve-LanePath $CheckpointRoot
$HomePath = Resolve-LanePath $HomePath

foreach ($guarded in $LaneRoot, $DataDir, $ReposDir) {
    if (Test-PathInside -Path $CheckpointRoot -Parent $guarded) {
        throw "The checkpoint root '$CheckpointRoot' is inside '$guarded'. Store checkpoints outside the lane and its folders."
    }
}
Assert-NoConfigDir
Assert-NoInterruptedRestore $LaneRoot
if (-not (Test-Path -LiteralPath $DataDir)) { throw "The data folder '$DataDir' does not exist." }
Assert-LaneIdle -LaneRoot $LaneRoot -Port $Port -DataDir $DataDir

$adapterFolders = @(Get-AdapterFolders -LaneRoot $LaneRoot -HomePath $HomePath)
Write-Host "Adapter folders being captured ($($adapterFolders.Count)):"
foreach ($folder in $adapterFolders) { Write-Host "  $folder" }

$items = @(
    [pscustomobject]@{ Kind = 'data'; Path = $DataDir }
    [pscustomobject]@{ Kind = 'repos'; Path = $ReposDir }
) + @($adapterFolders | ForEach-Object { [pscustomobject]@{ Kind = 'claude'; Path = $_ } })

$junctions = @(Get-ReparsePoints -Path @($items | ForEach-Object { $_.Path }))
if ($junctions.Count -gt 0 -and -not $AllowJunctions) {
    throw "These junctions or links would be left out of the copy (pass -AllowJunctions to save anyway):`n  $($junctions -join "`n  ")"
}

$laneStore = Join-Path $CheckpointRoot (Get-LaneKey $LaneRoot)
$final = Join-Path $laneStore $Name
if ((Test-Path -LiteralPath $final) -and -not $Force) {
    throw "Checkpoint '$Name' already exists at '$final'. Pass -Force to replace it."
}

$partial = Join-Path $laneStore ("$Name.partial-" + [guid]::NewGuid().ToString('N'))
$old = "$partial.old"
New-Item -ItemType Directory -Force -Path $partial | Out-Null
$recorded = @()
try {
    $index = 0
    foreach ($item in $items) {
        if (-not (Test-Path -LiteralPath $item.Path)) {
            if ($item.Kind -eq 'data') { throw "Required folder '$($item.Path)' is missing." }
            $recorded += [pscustomobject]@{ Kind = $item.Kind; Present = $false; Slot = $null; Path = $item.Path; Files = 0; Bytes = 0 }
            continue
        }
        $slot = '{0:D2}-{1}' -f $index++, $item.Kind
        Copy-Tree -From $item.Path -To (Join-Path $partial $slot)
        $source = Get-TreeStats $item.Path
        $copy = Get-TreeStats (Join-Path $partial $slot)
        if ($source.Files -ne $copy.Files -or $source.Bytes -ne $copy.Bytes) {
            throw "Copy of '$($item.Path)' differs from the source (files $($source.Files)/$($copy.Files), bytes $($source.Bytes)/$($copy.Bytes))."
        }
        $recorded += [pscustomobject]@{ Kind = $item.Kind; Present = $true; Slot = $slot; Path = $item.Path; Files = $copy.Files; Bytes = $copy.Bytes }
    }
    $manifest = [pscustomobject]@{
        Version = 2; Name = $Name; LaneRoot = $LaneRoot; DataDir = $DataDir; ReposDir = $ReposDir
        HomePath = $HomePath; Port = $Port; SavedUtc = [DateTime]::UtcNow.ToString('o'); Items = $recorded
    }
    $manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $partial 'manifest.json') -Encoding UTF8

    $hadOld = Test-Path -LiteralPath $final
    if ($hadOld) { Move-Dir -From $final -To $old }
    try {
        Move-Dir -From $partial -To $final
    }
    catch {
        if ($hadOld) { Move-Dir -From $old -To $final }
        throw
    }
    if ($hadOld) { Remove-QuietlyIfPresent $old }
}
catch {
    Remove-QuietlyIfPresent $partial
    throw
}
$present = @($recorded | Where-Object { $_.Present })
Write-Host "Saved checkpoint '$Name' for $LaneRoot at $final ($($present.Count) folders, $((($present | Measure-Object Files -Sum).Sum)) files)."
