<#
.SYNOPSIS
    Restores a lane to a checkpoint made by Save-LaneCheckpoint.ps1, discarding everything done since.

.DESCRIPTION
    Refuses unless -Force is given; unless the lane is idle (port closed, team.db/-wal/-shm not held, no process
    with the lane root in its command line); when -Port differs from the saved port; when the checkpoint is
    incomplete or does not match the files on disk; or when LaneRoot is not the absolute path it was saved from
    (the Adapter's project folders are named after that path). The checkpoint is validated and every folder is
    staged beside its target before anything is removed. Then each target is swapped by rename; a folder saved
    as absent is removed the same way. A failure rolls every step back, each step on its own so one failure does
    not stop the rest, and reports all errors. restore-in-progress.json is written in the lane root before the
    swap and deleted after success; while it exists, Save and Restore refuse unless Restore gets -ResumeInterrupted.

.PARAMETER LaneRoot    Must equal the checkpoint's LaneRoot.
.PARAMETER Port        The lane's port; must equal the saved port and must not answer.
.PARAMETER Name        Checkpoint name.
.PARAMETER HomePath    Home whose .claude\projects holds the Adapter logs. Defaults to $HOME.
.PARAMETER CheckpointRoot  Defaults to $HOME\HuddleLaneCheckpoints.
.PARAMETER Force       Required: acknowledges that everything since the checkpoint is discarded.
.PARAMETER ResumeInterrupted  Restore again after an interrupted restore left restore-in-progress.json.

.EXAMPLE
    pwsh -NoProfile -File agents/scripts/Restore-LaneCheckpoint.ps1 -LaneRoot C:\HuddleZero -Port 5100 -Name after-ZERO-16 -Force
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$LaneRoot,
    [Parameter(Mandatory)][int]$Port,
    [Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9][A-Za-z0-9._-]*$')][string]$Name,
    [string]$HomePath = $HOME,
    [string]$CheckpointRoot = (Join-Path $HOME 'HuddleLaneCheckpoints'),
    [switch]$Force,
    [switch]$ResumeInterrupted
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'LaneCheckpointCommon.ps1')

$LaneRoot = Resolve-LanePath $LaneRoot
$CheckpointRoot = Resolve-LanePath $CheckpointRoot
$HomePath = Resolve-LanePath $HomePath

if (-not $Force) {
    throw "Restoring checkpoint '$Name' DISCARDS everything in $LaneRoot (data, repositories, Adapter session logs) since it was saved. Re-run with -Force to do that."
}
Assert-NoConfigDir

$checkpoint = Join-Path (Join-Path $CheckpointRoot (Get-LaneKey $LaneRoot)) $Name
$manifestPath = Join-Path $checkpoint 'manifest.json'
if (-not (Test-Path -LiteralPath $manifestPath)) {
    throw "No complete checkpoint '$Name' at '$checkpoint' (manifest.json missing)."
}
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if (-not [string]::Equals($manifest.LaneRoot, $LaneRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Checkpoint was saved from '$($manifest.LaneRoot)', not '$LaneRoot'. Restore only to the same absolute path; elsewhere, room-sessions would have to be cleared and Agents start fresh."
}
if (-not [string]::Equals($manifest.HomePath, $HomePath, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Checkpoint was saved with home '$($manifest.HomePath)', not '$HomePath'."
}
if ([int]$manifest.Port -ne $Port) {
    throw "Checkpoint was saved for port $($manifest.Port), not $Port. Pass the lane's own port."
}
$markerPath = Get-RestoreMarkerPath $LaneRoot
if ((Test-Path -LiteralPath $markerPath) -and -not $ResumeInterrupted) { Assert-NoInterruptedRestore $LaneRoot }
Assert-LaneIdle -LaneRoot $LaneRoot -Port $Port -DataDir $manifest.DataDir

# 1. Validate every saved folder against the manifest before touching the lane.
foreach ($item in @($manifest.Items | Where-Object { $_.Present })) {
    $slotPath = Join-Path $checkpoint $item.Slot
    if (-not (Test-Path -LiteralPath $slotPath)) { throw "Checkpoint is damaged: '$($item.Slot)' is missing." }
    $stats = Get-TreeStats $slotPath
    if ($stats.Files -ne $item.Files -or $stats.Bytes -ne $item.Bytes) {
        throw "Checkpoint is damaged: '$($item.Slot)' has $($stats.Files) files / $($stats.Bytes) bytes, the manifest says $($item.Files) / $($item.Bytes)."
    }
}

# 2. Work out what changes, and stage the copies beside their targets. Nothing in the lane changes yet.
$token = [guid]::NewGuid().ToString('N').Substring(0, 8)
$entries = [System.Collections.Generic.List[object]]::new()
foreach ($item in @($manifest.Items)) {
    $entries.Add([pscustomobject]@{
            Target = $item.Path; Slot = $(if ($item.Present) { $item.Slot } else { $null })
            Stage = $(if ($item.Present) { "$($item.Path).restoring-$token" } else { $null })
            Discard = "$($item.Path).discard-$token"; HadOld = $false; StageMoved = $false
        })
}
$savedPaths = @($manifest.Items | ForEach-Object { $_.Path })
$currentAdapters = @(Get-AdapterFolders -LaneRoot $LaneRoot -HomePath $HomePath)
foreach ($extra in @($currentAdapters | Where-Object { $savedPaths -inotcontains $_ })) {
    $entries.Add([pscustomobject]@{ Target = $extra; Slot = $null; Stage = $null; Discard = "$extra.discard-$token"; HadOld = $false; StageMoved = $false })
}
Write-Host "Adapter folders being rewound:"
foreach ($entry in @($entries | Where-Object { $_.Target -like '*\.claude\projects\*' })) {
    $verb = if ($entry.Slot) { 'restored' } else { 'removed' }
    Write-Host "  $($entry.Target) ($verb)"
}

try {
    foreach ($entry in $entries) {
        if ($entry.Stage) { Copy-Tree -From (Join-Path $checkpoint $entry.Slot) -To $entry.Stage }
    }
}
catch {
    foreach ($entry in $entries) { Remove-QuietlyIfPresent $entry.Stage }
    throw
}

# 3. Record the intent, then swap by rename.
if (Test-Path -LiteralPath $markerPath) {
    # Leftovers of the interrupted restore hold only state that is being discarded anyway.
    $previous = Get-Content -LiteralPath $markerPath -Raw | ConvertFrom-Json
    foreach ($old in @($previous.Entries)) { Remove-QuietlyIfPresent $old.Stage; Remove-QuietlyIfPresent $old.Discard }
}
[pscustomobject]@{
    Checkpoint = $Name; Token = $token; StartedUtc = [DateTime]::UtcNow.ToString('o')
    Entries = @($entries | ForEach-Object { [pscustomobject]@{ Target = $_.Target; Stage = $_.Stage; Discard = $_.Discard } })
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $markerPath -Encoding UTF8

try {
    foreach ($entry in $entries) {
        if (Test-Path -LiteralPath $entry.Target) {
            Move-Dir -From $entry.Target -To $entry.Discard
            $entry.HadOld = $true
        }
        if ($entry.Stage) {
            Move-Dir -From $entry.Stage -To $entry.Target
            $entry.StageMoved = $true
        }
    }
}
catch {
    $failure = $_.Exception.Message
    $errors = [System.Collections.Generic.List[string]]::new()
    $reversed = @($entries)
    [array]::Reverse($reversed)
    foreach ($entry in $reversed) {
        if ($entry.StageMoved) {
            try { Remove-Item -LiteralPath $entry.Target -Recurse -Force; $entry.StageMoved = $false }
            catch { $errors.Add("remove restored '$($entry.Target)': $($_.Exception.Message)") }
        }
        if ($entry.HadOld -and -not $entry.StageMoved) {
            try { Move-Dir -From $entry.Discard -To $entry.Target; $entry.HadOld = $false }
            catch { $errors.Add("put back '$($entry.Target)' from '$($entry.Discard)': $($_.Exception.Message)") }
        }
        try { Remove-QuietlyIfPresent $entry.Stage }
        catch { $errors.Add("clean stage '$($entry.Stage)': $($_.Exception.Message)") }
    }
    if ($errors.Count -eq 0) {
        Remove-Item -LiteralPath $markerPath -Force
        throw "Restore failed and was rolled back; the lane is as it was. Cause: $failure"
    }
    throw "Restore failed ($failure) and the rollback had $($errors.Count) error(s); the lane may be mixed (restore-in-progress.json kept):`n  $($errors -join "`n  ")"
}
foreach ($entry in $entries) { Remove-QuietlyIfPresent $entry.Discard }
Remove-Item -LiteralPath $markerPath -Force
Write-Host "Restored $LaneRoot to checkpoint '$Name' (saved $($manifest.SavedUtc)). Everything since was discarded."
