# Shared helpers for Save-LaneCheckpoint.ps1 and Restore-LaneCheckpoint.ps1 (dot-sourced; not run alone).
#
# Can CLAUDE_CONFIG_DIR isolate the Adapter's folder?  Not proven, so these scripts do not use it.
#   - docs/Huddle.RoomSessions-Specifications.md:535 says the Adapter reads it at start (acp-agent.js:40) and
#     "probably moves the login"; decision D-13 (line 758) chose per-session settingSources instead.
#   - docs/engineering/manual-tests/file-changes.md FC-V4 (line 376) is the test of exactly this; run-log.md has
#     no result row for it, so it was never run.
#   - It is set per Adapter profile, not per lane, so it would change every Agent on that profile.
# The scripts therefore copy the real $HOME\.claude\projects\<lane>-* folders, and Save refuses when the process
# has CLAUDE_CONFIG_DIR set, because the logs would then be somewhere else.

function Get-LaneKey {
    param([Parameter(Mandatory)][string]$FullPath)
    # The Adapter names a project folder after the working directory: every non-alphanumeric becomes '-'.
    return ($FullPath.TrimEnd('\') -replace '[^A-Za-z0-9]', '-')
}

function Resolve-LanePath {
    param([Parameter(Mandatory)][string]$Path)
    return [System.IO.Path]::GetFullPath($Path).TrimEnd('\')
}

function Test-PathInside {
    # True when $Path equals $Parent or lies below it (case-insensitive).
    param([Parameter(Mandatory)][string]$Path, [Parameter(Mandatory)][string]$Parent)
    return [string]::Equals($Path, $Parent, [StringComparison]::OrdinalIgnoreCase) -or
        $Path.StartsWith($Parent + '\', [StringComparison]::OrdinalIgnoreCase)
}

function Test-PortAnswers {
    param([Parameter(Mandatory)][int]$Port)
    foreach ($address in '127.0.0.1', '::1') {
        $family = if ($address -eq '::1') { [System.Net.Sockets.AddressFamily]::InterNetworkV6 } else { [System.Net.Sockets.AddressFamily]::InterNetwork }
        $client = [System.Net.Sockets.TcpClient]::new($family)
        try {
            $task = $client.ConnectAsync([System.Net.IPAddress]::Parse($address), $Port)
            if ($task.Wait(750) -and $client.Connected) { return $true }
        }
        catch { Write-Verbose "No answer on ${address}:$Port ($($_.Exception.Message))" }
        finally { $client.Dispose() }
    }
    return $false
}

function Assert-LaneIdle {
    # A closed port is not enough: the app may be starting or stopping, or something else may hold the database.
    param([Parameter(Mandatory)][string]$LaneRoot, [Parameter(Mandatory)][int]$Port, [Parameter(Mandatory)][string]$DataDir)
    if (Test-PortAnswers $Port) {
        throw "Something answers on port $Port. Stop the lane's app first (P-STOP, then wait five seconds); copying a live team.db is unsafe."
    }
    foreach ($file in 'team.db', 'team.db-wal', 'team.db-shm') {
        $path = Join-Path $DataDir $file
        if (-not (Test-Path -LiteralPath $path)) { continue }
        try {
            $stream = [System.IO.File]::Open($path, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::None)
            $stream.Dispose()
        }
        catch [System.IO.IOException] {
            throw "'$path' is open in another process, so the lane is not idle. Stop the app and anything reading the database."
        }
    }
    # Skip this script's own process and its ancestors: their command line names the lane root as an argument.
    $skip = [System.Collections.Generic.HashSet[int]]::new()
    $all = @(Get-CimInstance Win32_Process)
    $current = $PID
    while ($current -and $skip.Add($current)) {
        $parent = $all | Where-Object { $_.ProcessId -eq $current } | Select-Object -First 1
        $current = if ($parent) { [int]$parent.ParentProcessId } else { 0 }
    }
    foreach ($process in $all) {
        if ($skip.Contains([int]$process.ProcessId) -or -not $process.CommandLine) { continue }
        if ($process.CommandLine.IndexOf($LaneRoot, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            throw "Process $($process.ProcessId) ($($process.Name)) has the lane root in its command line, so the lane may be running. Stop it first."
        }
    }
}

function Assert-NoConfigDir {
    if ($env:CLAUDE_CONFIG_DIR) {
        throw "CLAUDE_CONFIG_DIR is set ('$env:CLAUDE_CONFIG_DIR'). The Adapter's logs would then be outside <HomePath>\.claude\projects, which this script copies. Clear it for this process."
    }
}

function Assert-NoInterruptedRestore {
    param([Parameter(Mandatory)][string]$LaneRoot)
    $marker = Get-RestoreMarkerPath $LaneRoot
    if (Test-Path -LiteralPath $marker) {
        throw "An earlier restore of this lane did not finish ('$marker'), so the lane may be a mix of old and restored folders. Restore again with -Force -ResumeInterrupted."
    }
}

function Get-RestoreMarkerPath {
    # In the lane root, outside every captured folder.
    param([Parameter(Mandatory)][string]$LaneRoot)
    return (Join-Path $LaneRoot 'restore-in-progress.json')
}

function Get-ReparsePoints {
    param([Parameter(Mandatory)][string[]]$Path)
    foreach ($root in $Path) {
        if (-not (Test-Path -LiteralPath $root)) { continue }
        Get-ChildItem -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue |
            Where-Object { $_.Attributes -band [System.IO.FileAttributes]::ReparsePoint } |
            ForEach-Object { $_.FullName }
    }
}

function Copy-Tree {
    param([Parameter(Mandatory)][string]$From, [Parameter(Mandatory)][string]$To)
    New-Item -ItemType Directory -Force -Path $To | Out-Null
    & robocopy $From $To /MIR /XJ /COPY:DAT /DCOPY:DAT /R:2 /W:1 /NFL /NDL /NJH /NJS /NP | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "robocopy failed copying '$From' to '$To' (exit $LASTEXITCODE)." }
}

function Get-TreeStats {
    param([Parameter(Mandatory)][string]$Path)
    $files = @(Get-ChildItem -LiteralPath $Path -Recurse -File -Force)
    $bytes = 0L
    foreach ($file in $files) { $bytes += $file.Length }
    return [pscustomobject]@{ Files = $files.Count; Bytes = $bytes }
}

function Get-AdapterFolders {
    # The Adapter's project folders that belong to this lane. A folder whose name starts with the lane's slug also
    # matches a sibling lane such as <lane>-old; a folder that starts with a longer sibling's slug goes to that sibling.
    param([Parameter(Mandatory)][string]$LaneRoot, [Parameter(Mandatory)][string]$HomePath)
    $projects = Join-Path $HomePath '.claude\projects'
    if (-not (Test-Path -LiteralPath $projects)) { return @() }
    $key = Get-LaneKey $LaneRoot
    $parent = Split-Path -Parent $LaneRoot
    $siblingKeys = @()
    if ($parent -and (Test-Path -LiteralPath $parent)) {
        $siblingKeys = @(Get-ChildItem -LiteralPath $parent -Directory -Force -ErrorAction SilentlyContinue |
                Where-Object { -not [string]::Equals($_.FullName, $LaneRoot, [StringComparison]::OrdinalIgnoreCase) } |
                ForEach-Object { Get-LaneKey $_.FullName } |
                Where-Object { $_.StartsWith("$key-", [StringComparison]::OrdinalIgnoreCase) })
    }
    $found = foreach ($dir in Get-ChildItem -LiteralPath $projects -Directory -Force) {
        $name = $dir.Name
        if ($name -match '\.(restoring|discard)-[0-9a-f]{8}$' -or $name -match '\.partial-') { continue }
        $mine = [string]::Equals($name, $key, [StringComparison]::OrdinalIgnoreCase) -or
            $name.StartsWith("$key-", [StringComparison]::OrdinalIgnoreCase)
        if (-not $mine) { continue }
        $sibling = $siblingKeys | Where-Object {
            [string]::Equals($name, $_, [StringComparison]::OrdinalIgnoreCase) -or $name.StartsWith("$_-", [StringComparison]::OrdinalIgnoreCase)
        }
        if ($sibling) { continue }
        $dir.FullName
    }
    return @($found)
}

function Remove-QuietlyIfPresent {
    param([string]$Path)
    if ($Path -and (Test-Path -LiteralPath $Path)) { Remove-Item -LiteralPath $Path -Recurse -Force -ErrorAction SilentlyContinue }
}

function Move-Dir {
    # A pure rename: Move-Item falls back to copy-then-delete on a directory, which can fail half way.
    param([Parameter(Mandatory)][string]$From, [Parameter(Mandatory)][string]$To)
    [System.IO.Directory]::Move($From, $To)
}
