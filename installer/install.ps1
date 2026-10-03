<#
.SYNOPSIS
    Installs Agency Huddle for the current user (Windows).
.DESCRIPTION
    One self-contained file: it must stay ASCII only, parse on Windows PowerShell 5.1 and
    pwsh 7, and be importable by tests (set HUDDLE_INSTALL_NO_MAIN=1 before dot-sourcing).
    Exit codes: 0 success / up to date / check passed, 1 a step failed, 2 prerequisites
    unmet, 3 bad usage.
#>
[CmdletBinding()]
param(
    [string]$Version = 'latest',
    [string]$BaseUrl = '',
    [string]$InstallRoot = '',
    [string]$DataRoot = '',
    [switch]$CheckOnly,
    [switch]$Uninstall,
    [switch]$PurgeData,
    [switch]$Force,
    [switch]$NoLaunch
)

# Strict mode and ErrorActionPreference 'Stop' are set inside Invoke-Main, not here: under
# irm | iex this top level runs in the user's own session and must not change it.

$script:HuddleRepo = if ($env:HUDDLE_REPO) { $env:HUDDLE_REPO } else { '__HUDDLE_REPO__' }
# Built from two parts so the release-time replacement of the literal above cannot rewrite it.
$script:RepoPlaceholder = '__HUDDLE_' + 'REPO__'

$script:ExitCodes = @{
    Success            = 0
    StepFailed         = 1
    PrerequisitesUnmet = 2
    BadUsage           = 3
}

$script:MinWindowsBuild = 17763
$script:MinFreeMb = 2048
$script:NetworkHosts = @('github.com', 'nodejs.org', 'registry.npmjs.org')

# ---------------------------------------------------------------------------
# Output
# ---------------------------------------------------------------------------

function Write-Step {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ValidateSet('ok', 'skip', '..', '!!')]
        [string]$Status,

        [Parameter(Mandatory)]
        [string]$Name,

        [string]$Detail = '',

        [string]$Fix = ''
    )

    $line = '[' + $Status + '] ' + $Name
    if ($Detail) {
        $line = $line + ' - ' + $Detail
    }

    $color = switch ($Status) {
        'ok' { 'Green' }
        'skip' { 'Gray' }
        '..' { 'Cyan' }
        default { 'Red' }
    }

    Write-Host $line -ForegroundColor $color
    if ($Fix) {
        Write-Host ('     fix: ' + $Fix) -ForegroundColor $color
    }
}

# ---------------------------------------------------------------------------
# Paths and asset sources
# ---------------------------------------------------------------------------

function Resolve-InstallPaths {
    [CmdletBinding()]
    param(
        [string]$InstallRoot = '',
        [string]$DataRoot = ''
    )

    $localAppData = $env:LOCALAPPDATA
    if (-not $localAppData) {
        $localAppData = [Environment]::GetFolderPath('LocalApplicationData')
    }

    if ([string]::IsNullOrWhiteSpace($InstallRoot)) {
        $InstallRoot = Join-Path $localAppData 'Programs\Agency.Huddle'
    }

    if ([string]::IsNullOrWhiteSpace($DataRoot)) {
        $DataRoot = Join-Path $localAppData 'Agency.Huddle'
    }

    return [pscustomobject]@{
        InstallRoot     = $InstallRoot
        DataRoot        = $DataRoot
        CacheDir        = Join-Path $DataRoot 'cache'
        ExePath         = Join-Path $InstallRoot 'app\Huddle.App.exe'
        NodeExe         = Join-Path $InstallRoot 'node\node.exe'
        AdapterEntry    = Join-Path $InstallRoot 'tools\acp\node_modules\@agentclientprotocol\claude-agent-acp\dist\index.js'
        StatePath       = Join-Path $InstallRoot 'install-state.json'
        ReleaseInfoPath = Join-Path $InstallRoot 'release-info.json'
        LauncherPath    = Join-Path $InstallRoot 'huddle.cmd'
        Rid             = 'win-x64'
    }
}

function Resolve-AssetSource {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Name,

        [string]$BaseUrl = '',

        [string]$Version = 'latest',

        [string]$Repo = $script:HuddleRepo
    )

    if ($BaseUrl) {
        if ($BaseUrl -match '^https://') {
            return [pscustomobject]@{
                Kind     = 'url'
                Location = $BaseUrl.TrimEnd('/') + '/' + $Name
            }
        }

        if (Test-Path -LiteralPath $BaseUrl -PathType Container) {
            return [pscustomobject]@{
                Kind     = 'file'
                Location = Join-Path $BaseUrl $Name
            }
        }

        throw "BaseUrl must be an https URL or an existing folder: $BaseUrl"
    }

    if ($Repo -eq $script:RepoPlaceholder) {
        throw 'This copy of install.ps1 is not tied to a release. Run the installer from a release (download install.ps1 from the release page), or pass -BaseUrl.'
    }

    $root = 'https://github.com/' + $Repo + '/releases'
    if ($Version -eq 'latest') {
        $location = $root + '/latest/download/' + $Name
    }
    else {
        $location = $root + '/download/' + $Version + '/' + $Name
    }

    return [pscustomobject]@{
        Kind     = 'url'
        Location = $location
    }
}

# ---------------------------------------------------------------------------
# Verified asset download (SHA256SUMS, cache, retry)
# ---------------------------------------------------------------------------

function Read-Sums {
    [CmdletBinding()]
    param(
        [AllowEmptyString()]
        [string]$Text = ''
    )

    $sums = @{}
    foreach ($line in ($Text -split "\r?\n")) {
        if ($line -match '^\s*([0-9a-fA-F]{64})\s+\*?(\S.*?)\s*$') {
            $sums[$Matches[2]] = $Matches[1].ToLowerInvariant()
        }
    }

    return $sums
}

function Get-ExpectedHash {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [hashtable]$Sums,

        [Parameter(Mandatory)]
        [string]$Name
    )

    if (-not $Sums.ContainsKey($Name)) {
        throw "'$Name' is not listed in SHA256SUMS"
    }

    return [string]$Sums[$Name]
}

function Get-FileSha256 {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

# Seam: the only place a file is fetched over the network.
function Invoke-Download {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Url,

        [Parameter(Mandatory)]
        [string]$OutFile
    )

    # Progress rendering makes Windows PowerShell 5.1 downloads very slow. The assignment is local
    # to this function, so it does not touch the caller's session.
    $ProgressPreference = 'SilentlyContinue'

    if ($PSVersionTable.PSVersion.Major -lt 6) {
        [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
    }

    Invoke-WebRequest -Uri $Url -OutFile $OutFile -UseBasicParsing
}

function Get-VerifiedAsset {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Name,

        [Parameter(Mandatory)]
        [string]$ExpectedSha256,

        [Parameter(Mandatory)]
        $Source,

        [Parameter(Mandatory)]
        [string]$CacheDir
    )

    $expected = $ExpectedSha256.Trim().ToLowerInvariant()

    if (-not (Test-Path -LiteralPath $CacheDir -PathType Container)) {
        New-Item -ItemType Directory -Path $CacheDir -Force | Out-Null
    }

    $final = Join-Path $CacheDir $Name
    $partial = $final + '.partial'

    if (Test-Path -LiteralPath $final -PathType Leaf) {
        if ((Get-FileSha256 -Path $final) -eq $expected) {
            return $final
        }

        Remove-Item -LiteralPath $final -Force
    }

    # A local file is copied once; a network download gets 3 attempts with 1 s and 3 s backoff.
    $maxAttempts = 3
    if ($Source.Kind -eq 'file') {
        $maxAttempts = 1
    }

    $backoffSeconds = @(1, 3)
    $lastMessage = ''

    for ($attempt = 1; $attempt -le $maxAttempts; $attempt++) {
        try {
            if (Test-Path -LiteralPath $partial) {
                Remove-Item -LiteralPath $partial -Force
            }

            if ($Source.Kind -eq 'file') {
                Copy-Item -LiteralPath $Source.Location -Destination $partial -Force
            }
            else {
                Invoke-Download -Url $Source.Location -OutFile $partial
            }

            $actual = Get-FileSha256 -Path $partial
            if ($actual -ne $expected) {
                throw "Hash mismatch for '$Name': expected $expected but got $actual."
            }

            Move-Item -LiteralPath $partial -Destination $final -Force
            return $final
        }
        catch {
            $lastMessage = $_.Exception.Message
            if (Test-Path -LiteralPath $partial) {
                Remove-Item -LiteralPath $partial -Force -ErrorAction SilentlyContinue
            }

            if ($attempt -lt $maxAttempts) {
                Start-Sleep -Seconds $backoffSeconds[$attempt - 1]
            }
        }
    }

    throw "Could not get a verified '$Name' after $maxAttempts attempt(s): $lastMessage"
}

# ---------------------------------------------------------------------------
# Install state (idempotency, Spec section 6)
# ---------------------------------------------------------------------------

# The keys the state file may carry, in the order they are written.
$script:StateKeys = @('version', 'appSha256', 'nodeVersion', 'lockSha256')

function Read-InstallState {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return $null
    }

    try {
        $text = [System.IO.File]::ReadAllText($Path)
        if ([string]::IsNullOrWhiteSpace($text)) {
            return $null
        }

        $state = $text | ConvertFrom-Json -ErrorAction Stop
    }
    catch {
        # An unreadable or malformed state file means "nothing is known": every step runs again.
        return $null
    }

    if ($state -is [System.Management.Automation.PSCustomObject]) {
        return $state
    }

    return $null
}

function Write-InstallState {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        # A hashtable or the object Read-InstallState returned.
        [Parameter(Mandatory)]
        $State
    )

    $ordered = [ordered]@{ schema = '1' }
    foreach ($key in $script:StateKeys) {
        $property = $State.PSObject.Properties[$key]
        if ($null -ne $property) {
            $ordered[$key] = [string]$property.Value
        }
        elseif (($State -is [System.Collections.IDictionary]) -and $State.Contains($key)) {
            $ordered[$key] = [string]$State[$key]
        }
    }

    $parent = Split-Path -Parent $Path
    if ($parent -and -not (Test-Path -LiteralPath $parent -PathType Container)) {
        New-Item -ItemType Directory -Path $parent -Force | Out-Null
    }

    $json = $ordered | ConvertTo-Json
    $tmp = $Path + '.tmp'
    [System.IO.File]::WriteAllText($tmp, $json, (New-Object System.Text.UTF8Encoding($false)))
    Move-Item -LiteralPath $tmp -Destination $Path -Force
}

function Get-LauncherContent {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [pscustomobject]$Paths
    )

    $nodeDir = Split-Path -Parent $Paths.NodeExe
    $nodeExe = $Paths.NodeExe
    $dataDir = $Paths.DataRoot + '\data'
    $exePath = $Paths.ExePath

    # The outer quotes in set "VAR=value" protect spaces in the value
    $lines = @(
        '@echo off'
        'setlocal'
        'set "Team__Desktop__Enabled=true"'
        'set "Team__Acp__Enabled=true"'
        'set "Team__Acp__Command=' + $nodeExe + '"'
        'set "Team__DataDir=' + $dataDir + '"'
        'set "Team__PipeName=huddle-%USERNAME%"'
        'set "ASPNETCORE_URLS=http://127.0.0.1:0"'
        'set "ASPNETCORE_ENVIRONMENT=Production"'
        'set "AllowedHosts=localhost;127.0.0.1"'
        'set "PATH=' + $nodeDir + ';%PATH%"'
        '"' + $exePath + '" %*'
    )

    return $lines -join "`r`n"
}

function Test-StepSatisfied {
    [CmdletBinding()]
    param(
        [AllowNull()]
        $State,

        [Parameter(Mandatory)]
        [string]$Key,

        [AllowEmptyString()]
        [AllowNull()]
        [string]$Expected
    )

    if ($null -eq $State -or [string]::IsNullOrEmpty($Expected)) {
        return $false
    }

    $property = $State.PSObject.Properties[$Key]
    if ($null -eq $property -or $null -eq $property.Value) {
        return $false
    }

    return [string]::Equals([string]$property.Value, $Expected, [System.StringComparison]::OrdinalIgnoreCase)
}

# ---------------------------------------------------------------------------
# Stop a running instance and install the app (Spec 6, rules 3 and 4)
# ---------------------------------------------------------------------------

# Seam: the only place an archive is extracted.
function Invoke-Tar {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Archive,

        [Parameter(Mandatory)]
        [string]$Destination
    )

    # Prefer the system bsdtar: a GNU tar earlier on PATH cannot read zip archives.
    $tar = 'tar.exe'
    if ($env:SystemRoot) {
        $systemTar = Join-Path $env:SystemRoot 'System32\tar.exe'
        if (Test-Path -LiteralPath $systemTar -PathType Leaf) {
            $tar = $systemTar
        }
    }

    $null = & $tar -xf $Archive -C $Destination
    if ($LASTEXITCODE -ne 0) {
        throw ('Could not extract ' + $Archive + ' (tar exit code ' + $LASTEXITCODE + ').')
    }
}

# Seam: runs "<launcher> stop" and returns its exit code, or -1 when it did not finish within
# the cap. The cap (60 s) is above the stop verb's own 45 s wait.
function Invoke-HuddleStop {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        $Paths,

        [int]$TimeoutSeconds = 60
    )

    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    if (Test-Path -LiteralPath $Paths.LauncherPath -PathType Leaf) {
        $startInfo.FileName = 'cmd.exe'
        $startInfo.Arguments = '/c ""' + $Paths.LauncherPath + '" stop"'
    }
    elseif (Test-Path -LiteralPath $Paths.ExePath -PathType Leaf) {
        # No launcher yet: the app host finds its instance record through Team__DataDir.
        $startInfo.FileName = $Paths.ExePath
        $startInfo.Arguments = 'stop'
        $startInfo.EnvironmentVariables['Team__DataDir'] = Join-Path $Paths.DataRoot 'data'
    }
    else {
        return 127
    }

    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardInput = $true

    $process = [System.Diagnostics.Process]::Start($startInfo)
    try {
        $process.StandardInput.Close()
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            try {
                & taskkill.exe /PID $process.Id /T /F | Out-Null
            }
            catch {
                Write-Verbose ('taskkill failed: ' + $_.Exception.Message)
            }

            return -1
        }

        return $process.ExitCode
    }
    finally {
        $process.Dispose()
    }
}

# Seam: true only for a live process that is the Huddle app host (guards against a recycled pid).
function Test-ProcessRunning {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [int]$ProcessId
    )

    $process = Get-Process -Id $ProcessId -ErrorAction SilentlyContinue
    if ($null -eq $process) {
        return $false
    }

    return [string]::Equals($process.ProcessName, 'Huddle.App', [System.StringComparison]::OrdinalIgnoreCase)
}

# Seam: ends a process that survived the stop request (-Force only).
function Stop-HuddleProcess {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [int]$ProcessId
    )

    Stop-Process -Id $ProcessId -Force -ErrorAction SilentlyContinue
    Wait-Process -Id $ProcessId -Timeout 10 -ErrorAction SilentlyContinue
}

# Stops a running instance (instance record present). Returns whether a stop was attempted.
function Stop-HuddleIfRunning {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        $Paths,

        [switch]$Force
    )

    $recordPath = Join-Path (Join-Path $Paths.DataRoot 'data') 'instance.json'
    if (-not (Test-Path -LiteralPath $recordPath -PathType Leaf)) {
        return $false
    }

    $instancePid = 0
    try {
        $record = Get-Content -LiteralPath $recordPath -Raw | ConvertFrom-Json
        $instancePid = [int]$record.PSObject.Properties['pid'].Value
    }
    catch {
        # An unreadable record means no usable pid: the exit code of the stop request decides below.
        $instancePid = 0
    }

    Write-Step -Status '..' -Name 'stop' -Detail 'stopping the running Agency Huddle'
    $exitCode = Invoke-HuddleStop -Paths $Paths

    if ($instancePid -gt 0) {
        if (Test-ProcessRunning -ProcessId $instancePid) {
            if (-not $Force) {
                throw ('Agency Huddle (pid ' + $instancePid + ') is still running after the stop request. Close it and run the installer again, or pass -Force to end the process.')
            }

            Write-Step -Status '..' -Name 'stop' -Detail ('ending process ' + $instancePid + ' (-Force)')
            Stop-HuddleProcess -ProcessId $instancePid
        }
    }
    elseif ($exitCode -ne 0) {
        if (-not $Force) {
            throw ('The stop request for Agency Huddle failed (exit code ' + $exitCode + '). Close it and run the installer again, or pass -Force.')
        }

        Write-Step -Status '..' -Name 'stop' -Detail ('stop request exited with code ' + $exitCode + '; continuing because of -Force')
    }

    Write-Step -Status 'ok' -Name 'stop' -Detail 'stopped'
    return $true
}

# Installs the app from a verified archive. Returns $true when the install changed, $false when
# the step was skipped. Does not write the install state (Invoke-Main does, last).
function Install-App {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        $Paths,

        [Parameter(Mandatory)]
        [string]$Archive,

        [AllowNull()]
        $State,

        [switch]$Force
    )

    $ErrorActionPreference = 'Stop'

    $archiveSha = Get-FileSha256 -Path $Archive
    if ((Test-StepSatisfied -State $State -Key 'appSha256' -Expected $archiveSha) -and (Test-Path -LiteralPath $Paths.ExePath -PathType Leaf)) {
        Write-Step -Status 'skip' -Name 'app' -Detail 'already installed'
        return $false
    }

    $installRoot = $Paths.InstallRoot.TrimEnd('\', '/')
    $staging = $installRoot + '.staging'
    $old = $installRoot + '.old'

    # Leftovers of an interrupted run: a swap that died between its two renames, then stale folders.
    if ((-not (Test-Path -LiteralPath $installRoot)) -and (Test-Path -LiteralPath $old)) {
        Move-Item -LiteralPath $old -Destination $installRoot
    }

    foreach ($leftover in @($staging, $old)) {
        if (Test-Path -LiteralPath $leftover) {
            Remove-Item -LiteralPath $leftover -Recurse -Force
        }
    }

    $parent = Split-Path -Parent $installRoot
    if (-not (Test-Path -LiteralPath $parent)) {
        New-Item -ItemType Directory -Path $parent -Force | Out-Null
    }

    Write-Step -Status '..' -Name 'app' -Detail 'extracting the release archive'

    $carried = New-Object System.Collections.ArrayList
    $oldMoved = $false
    try {
        New-Item -ItemType Directory -Path $staging | Out-Null
        Invoke-Tar -Archive $Archive -Destination $staging

        foreach ($required in @('app\Huddle.App.exe', 'release-info.json')) {
            if (-not (Test-Path -LiteralPath (Join-Path $staging $required) -PathType Leaf)) {
                throw ('The release archive does not contain ' + $required + '; the previous install was left untouched.')
            }
        }

        if (Test-Path -LiteralPath $installRoot) {
            # Windows cannot move a folder whose files a running process holds open.
            $null = Stop-HuddleIfRunning -Paths $Paths -Force:$Force

            foreach ($relative in @('node', 'tools\acp\node_modules')) {
                $from = Join-Path $installRoot $relative
                if (Test-Path -LiteralPath $from) {
                    $to = Join-Path $staging $relative
                    $toParent = Split-Path -Parent $to
                    if (-not (Test-Path -LiteralPath $toParent)) {
                        New-Item -ItemType Directory -Path $toParent -Force | Out-Null
                    }

                    if (Test-Path -LiteralPath $to) {
                        Remove-Item -LiteralPath $to -Recurse -Force
                    }

                    Move-Item -LiteralPath $from -Destination $to
                    $null = $carried.Add([pscustomobject]@{ From = $from; To = $to })
                }
            }

            # Small files the other steps read next: copied, so the old tree stays whole for a rollback.
            foreach ($file in @($Paths.StatePath, $Paths.LauncherPath)) {
                $target = Join-Path $staging (Split-Path -Leaf $file)
                if ((Test-Path -LiteralPath $file -PathType Leaf) -and (-not (Test-Path -LiteralPath $target))) {
                    Copy-Item -LiteralPath $file -Destination $target
                }
            }

            Move-Item -LiteralPath $installRoot -Destination $old
            $oldMoved = $true
        }

        Move-Item -LiteralPath $staging -Destination $installRoot
    }
    catch {
        $failure = $_
        try {
            if ($oldMoved -and (-not (Test-Path -LiteralPath $installRoot))) {
                Move-Item -LiteralPath $old -Destination $installRoot
            }

            for ($i = $carried.Count - 1; $i -ge 0; $i--) {
                $item = $carried[$i]
                $fromParent = Split-Path -Parent $item.From
                if (-not (Test-Path -LiteralPath $fromParent)) {
                    New-Item -ItemType Directory -Path $fromParent -Force | Out-Null
                }

                Move-Item -LiteralPath $item.To -Destination $item.From
            }

            if (Test-Path -LiteralPath $staging) {
                Remove-Item -LiteralPath $staging -Recurse -Force
            }
        }
        catch {
            Write-Step -Status '!!' -Name 'app' -Detail ('rollback failed: ' + $_.Exception.Message) -Fix ('Restore ' + $installRoot + ' from ' + $old + ' by hand.')
        }

        throw $failure
    }

    if (Test-Path -LiteralPath $old) {
        try {
            Remove-Item -LiteralPath $old -Recurse -Force
        }
        catch {
            Write-Step -Status '..' -Name 'app' -Detail ('could not delete ' + $old + '; the next run removes it')
        }
    }

    Write-Step -Status 'ok' -Name 'app' -Detail 'installed'
    return $true
}

# ---------------------------------------------------------------------------
# Install Node and the adapter (Spec 6, Findings F2)
# ---------------------------------------------------------------------------

# Seam: the only place the bundled node is asked for its version. Returns e.g. 'v24.21.0', or
# $null when the file is missing, does not run or exits non-zero.
function Get-InstalledNodeVersion {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$NodeExe
    )

    if (-not (Test-Path -LiteralPath $NodeExe -PathType Leaf)) {
        return $null
    }

    try {
        $output = & $NodeExe --version 2>$null
        if ($LASTEXITCODE -ne 0) {
            return $null
        }

        $first = @($output) | Select-Object -First 1
        if ($null -eq $first) {
            return $null
        }

        return ([string]$first).Trim()
    }
    catch {
        return $null
    }
}

# Installs the pinned Node into <InstallRoot>\node. Returns $true when it installed or replaced
# node, $false when the step was skipped. It does NOT write state: the caller writes nodeVersion.
# NodeBaseUrl (an https URL or an existing folder) defaults to https://nodejs.org/dist/<version>.
function Install-Node {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        $Paths,

        [Parameter(Mandatory)]
        [string]$NodeVersion,

        [AllowNull()]
        $State,

        [string]$NodeBaseUrl = '',

        [switch]$Force
    )

    $ErrorActionPreference = 'Stop'

    $version = 'v' + $NodeVersion.Trim().TrimStart('v', 'V')
    if ($version -notmatch '^v\d+\.\d+\.\d+$') {
        throw ('The Node version "' + $NodeVersion + '" is not of the form v24.21.0.')
    }

    $bare = $version.Substring(1)
    $stateMatches = (Test-StepSatisfied -State $State -Key 'nodeVersion' -Expected $version) -or (Test-StepSatisfied -State $State -Key 'nodeVersion' -Expected $bare)
    if ($stateMatches -and (Test-Path -LiteralPath $Paths.NodeExe -PathType Leaf)) {
        $installed = Get-InstalledNodeVersion -NodeExe $Paths.NodeExe
        if ($installed -and ([string]$installed).Trim().TrimStart('v', 'V') -eq $bare) {
            Write-Step -Status 'skip' -Name 'node' -Detail ('already installed (' + $version + ')')
            return $false
        }
    }

    $nodeDir = Split-Path -Parent $Paths.NodeExe
    $stage = $nodeDir + '.staging'
    $old = $nodeDir + '.old'

    # Leftovers of an interrupted run: a swap that died between its two renames, then stale folders.
    if ((-not (Test-Path -LiteralPath $nodeDir)) -and (Test-Path -LiteralPath $old)) {
        Move-Item -LiteralPath $old -Destination $nodeDir
    }

    foreach ($leftover in @($stage, $old)) {
        if (Test-Path -LiteralPath $leftover) {
            Remove-Item -LiteralPath $leftover -Recurse -Force
        }
    }

    if (-not $NodeBaseUrl) {
        $NodeBaseUrl = 'https://nodejs.org/dist/' + $version
    }

    $wrapper = 'node-' + $version + '-win-x64'
    $zipName = $wrapper + '.zip'

    Write-Step -Status '..' -Name 'node' -Detail ('downloading ' + $zipName)

    # SHASUMS256.txt has no hash of its own to check against; it is fetched from the same
    # TLS-protected origin as the zip and then decides the zip's expected hash.
    $sumsSource = Resolve-AssetSource -Name 'SHASUMS256.txt' -BaseUrl $NodeBaseUrl
    if ($sumsSource.Kind -eq 'file') {
        $sumsPath = $sumsSource.Location
    }
    else {
        if (-not (Test-Path -LiteralPath $Paths.CacheDir -PathType Container)) {
            New-Item -ItemType Directory -Path $Paths.CacheDir -Force | Out-Null
        }

        $sumsPath = Join-Path $Paths.CacheDir ('node-' + $version + '-SHASUMS256.txt')
        if (Test-Path -LiteralPath $sumsPath) {
            Remove-Item -LiteralPath $sumsPath -Force
        }

        Invoke-Download -Url $sumsSource.Location -OutFile $sumsPath
    }

    $sums = Read-Sums -Text ([System.IO.File]::ReadAllText($sumsPath))
    $expected = Get-ExpectedHash -Sums $sums -Name $zipName
    $zipSource = Resolve-AssetSource -Name $zipName -BaseUrl $NodeBaseUrl
    $archive = Get-VerifiedAsset -Name $zipName -ExpectedSha256 $expected -Source $zipSource -CacheDir $Paths.CacheDir

    $oldMoved = $false
    try {
        New-Item -ItemType Directory -Path $stage -Force | Out-Null
        Invoke-Tar -Archive $archive -Destination $stage

        $inner = Join-Path $stage $wrapper
        foreach ($required in @('node.exe', 'node_modules\npm\bin\npm-cli.js')) {
            if (-not (Test-Path -LiteralPath (Join-Path $inner $required) -PathType Leaf)) {
                throw ('The Node archive does not contain ' + $wrapper + '\' + $required + '; the previous node was left untouched.')
            }
        }

        if (Test-Path -LiteralPath $nodeDir) {
            # A running adapter holds node.exe open; Windows cannot rename the folder around it.
            $null = Stop-HuddleIfRunning -Paths $Paths -Force:$Force
            Move-Item -LiteralPath $nodeDir -Destination $old
            $oldMoved = $true
        }

        Move-Item -LiteralPath $inner -Destination $nodeDir
    }
    catch {
        $failure = $_
        try {
            if ($oldMoved -and (-not (Test-Path -LiteralPath $nodeDir))) {
                Move-Item -LiteralPath $old -Destination $nodeDir
            }

            if (Test-Path -LiteralPath $stage) {
                Remove-Item -LiteralPath $stage -Recurse -Force
            }
        }
        catch {
            Write-Step -Status '!!' -Name 'node' -Detail ('rollback failed: ' + $_.Exception.Message) -Fix ('Restore ' + $nodeDir + ' from ' + $old + ' by hand.')
        }

        throw $failure
    }

    foreach ($leftover in @($stage, $old)) {
        if (Test-Path -LiteralPath $leftover) {
            try {
                Remove-Item -LiteralPath $leftover -Recurse -Force
            }
            catch {
                Write-Step -Status '..' -Name 'node' -Detail ('could not delete ' + $leftover + '; the next run removes it')
            }
        }
    }

    Write-Step -Status 'ok' -Name 'node' -Detail ('installed (' + $version + ')')
    return $true
}

# Seam: runs "npm ci" with the BUNDLED node (never a global npm) and returns the exit code. The
# node folder goes first on PATH for the child only, the working directory is the child's, and
# stdout plus stderr end up in LogPath.
function Invoke-NpmCi {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$NodeExe,

        [Parameter(Mandatory)]
        [string]$WorkingDirectory,

        [Parameter(Mandatory)]
        [string]$LogPath
    )

    $nodeDir = Split-Path -Parent $NodeExe
    $npmCli = Join-Path $nodeDir 'node_modules\npm\bin\npm-cli.js'

    $logDir = Split-Path -Parent $LogPath
    if (-not (Test-Path -LiteralPath $logDir -PathType Container)) {
        New-Item -ItemType Directory -Path $logDir -Force | Out-Null
    }

    $errPath = $LogPath + '.err'
    $argLine = '"' + $npmCli + '" ci --omit=dev --no-audit --no-fund'

    $oldPath = $env:PATH
    try {
        if ($oldPath) {
            $env:PATH = $nodeDir + ';' + $oldPath
        }
        else {
            $env:PATH = $nodeDir
        }

        $process = Start-Process -FilePath $NodeExe -ArgumentList $argLine -WorkingDirectory $WorkingDirectory -NoNewWindow -Wait -PassThru -RedirectStandardOutput $LogPath -RedirectStandardError $errPath
        $exitCode = [int]$process.ExitCode
    }
    finally {
        $env:PATH = $oldPath
    }

    $text = ''
    foreach ($file in @($LogPath, $errPath)) {
        if (Test-Path -LiteralPath $file -PathType Leaf) {
            $text = $text + [System.IO.File]::ReadAllText($file)
        }
    }

    [System.IO.File]::WriteAllText($LogPath, $text, (New-Object System.Text.UTF8Encoding($false)))
    if (Test-Path -LiteralPath $errPath) {
        Remove-Item -LiteralPath $errPath -Force
    }

    return $exitCode
}

# Installs the adapter's production dependencies into <InstallRoot>\tools\acp with the bundled
# node. Returns $true when npm ci ran, $false when the step was skipped. It does NOT write state:
# the caller writes lockSha256 (the SHA-256 of tools\acp\package-lock.json).
function Install-Adapter {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        $Paths,

        [AllowNull()]
        $State,

        [switch]$Force
    )

    $ErrorActionPreference = 'Stop'

    $acpDir = Join-Path $Paths.InstallRoot 'tools\acp'
    $lockPath = Join-Path $acpDir 'package-lock.json'
    if (-not (Test-Path -LiteralPath $lockPath -PathType Leaf)) {
        throw ('Cannot install the adapter: ' + $lockPath + ' is missing from the release archive.')
    }

    $lockSha = Get-FileSha256 -Path $lockPath
    if ((Test-StepSatisfied -State $State -Key 'lockSha256' -Expected $lockSha) -and (Test-Path -LiteralPath $Paths.AdapterEntry -PathType Leaf)) {
        Write-Step -Status 'skip' -Name 'adapter' -Detail 'already installed'
        return $false
    }

    if (-not (Test-Path -LiteralPath $Paths.NodeExe -PathType Leaf)) {
        throw ('Cannot install the adapter: the bundled node is missing at ' + $Paths.NodeExe + '.')
    }

    # npm ci replaces node_modules, which a running adapter holds open.
    $null = Stop-HuddleIfRunning -Paths $Paths -Force:$Force

    $logPath = Join-Path $Paths.CacheDir 'npm-ci.log'
    Write-Step -Status '..' -Name 'adapter' -Detail 'installing the adapter (npm ci)'

    $exitCode = Invoke-NpmCi -NodeExe $Paths.NodeExe -WorkingDirectory $acpDir -LogPath $logPath
    if ($exitCode -ne 0) {
        $tail = '(the npm log is empty or missing: ' + $logPath + ')'
        if (Test-Path -LiteralPath $logPath -PathType Leaf) {
            $lines = @(Get-Content -LiteralPath $logPath -Tail 15)
            if ($lines.Count -gt 0) {
                $tail = $lines -join "`r`n"
            }
        }

        throw ('npm ci failed (exit code ' + $exitCode + '). Last lines of ' + $logPath + ":`r`n" + $tail + "`r`n" + 'fix: if you are behind a proxy, set HTTPS_PROXY (and HTTP_PROXY) or run "npm config set proxy" and "npm config set https-proxy", then run the installer again.')
    }

    if (-not (Test-Path -LiteralPath $Paths.AdapterEntry -PathType Leaf)) {
        throw ('npm ci finished but the adapter entry not found at ' + $Paths.AdapterEntry + '. fix: run the installer again; if it persists, delete ' + $acpDir + '\node_modules and retry.')
    }

    Write-Step -Status 'ok' -Name 'adapter' -Detail 'installed'
    return $true
}

# ---------------------------------------------------------------------------
# Prerequisite seams (every external effect of the checks sits behind one)
# ---------------------------------------------------------------------------

function Get-OsBuild {
    [CmdletBinding()]
    param()

    return [int][Environment]::OSVersion.Version.Build
}

function Get-HostArchitecture {
    [CmdletBinding()]
    param()

    $arch = $env:PROCESSOR_ARCHITEW6432
    if (-not $arch) {
        $arch = $env:PROCESSOR_ARCHITECTURE
    }

    return ([string]$arch).ToUpperInvariant()
}

function Test-CommandAvailable {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Name
    )

    return [bool](Get-Command -Name $Name -ErrorAction SilentlyContinue)
}

function Get-ExistingAncestor {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    $current = [System.IO.Path]::GetFullPath($Path)
    while (-not (Test-Path -LiteralPath $current)) {
        $parent = Split-Path -Parent $current
        if ((-not $parent) -or ($parent -eq $current)) {
            return $null
        }

        $current = $parent
    }

    return $current
}

function Get-FreeDiskMb {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    $ancestor = Get-ExistingAncestor -Path $Path
    if (-not $ancestor) {
        return -1
    }

    try {
        $root = [System.IO.Path]::GetPathRoot($ancestor)
        $drive = New-Object System.IO.DriveInfo($root)
        return [long][math]::Floor($drive.AvailableFreeSpace / 1MB)
    }
    catch {
        # A drive that cannot be measured (for example a UNC share) is reported as unknown (-1).
        return -1
    }
}

function Test-PathWritable {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    $ancestor = Get-ExistingAncestor -Path $Path
    if ((-not $ancestor) -or (-not (Test-Path -LiteralPath $ancestor -PathType Container))) {
        return $false
    }

    $probe = Join-Path $ancestor ('.huddle-write-probe-' + [guid]::NewGuid().ToString('N'))
    try {
        [System.IO.File]::WriteAllText($probe, 'x')
        Remove-Item -LiteralPath $probe -Force
        return $true
    }
    catch {
        # Not writable (permissions, read-only media): the caller reports it.
        return $false
    }
}

function Test-UrlReachable {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Url,

        [int]$TimeoutSec = 10
    )

    $ProgressPreference = 'SilentlyContinue'
    try {
        [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
        $null = Invoke-WebRequest -Uri $Url -Method Head -UseBasicParsing -TimeoutSec $TimeoutSec
        return $true
    }
    catch {
        # Any HTTP answer (even 4xx or 5xx) proves the host is reachable; only a missing response means unreachable.
        $response = $null
        if ($_.Exception.PSObject.Properties['Response']) {
            $response = $_.Exception.Response
        }

        return ($null -ne $response)
    }
}

# ---------------------------------------------------------------------------
# Prerequisite checks (Spec section 5, read-only)
# ---------------------------------------------------------------------------

function ConvertTo-PrereqItem {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Name,

        [Parameter(Mandatory)]
        [bool]$Ok,

        [string]$Detail = '',

        [string]$Fix = ''
    )

    return [pscustomobject]@{
        Name   = $Name
        Ok     = $Ok
        Detail = $Detail
        Fix    = $Fix
    }
}

function Test-Prerequisites {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        $Paths,

        [string]$BaseUrl = ''
    )

    $items = New-Object System.Collections.Generic.List[object]

    $build = Get-OsBuild
    if ($build -ge $script:MinWindowsBuild) {
        $items.Add((ConvertTo-PrereqItem -Name 'OS' -Ok $true -Detail ('Windows build ' + $build)))
    }
    else {
        $items.Add((ConvertTo-PrereqItem -Name 'OS' -Ok $false -Detail ('Windows build ' + $build + ' is older than ' + $script:MinWindowsBuild) -Fix 'Update to Windows 10 version 1809 (build 17763) or later.'))
    }

    $arch = Get-HostArchitecture
    if ($arch -eq 'AMD64') {
        $items.Add((ConvertTo-PrereqItem -Name 'CPU' -Ok $true -Detail 'AMD64'))
    }
    elseif ($arch -eq 'ARM64') {
        $items.Add((ConvertTo-PrereqItem -Name 'CPU' -Ok $true -Detail 'ARM64 host, runs under x64 emulation'))
    }
    else {
        $items.Add((ConvertTo-PrereqItem -Name 'CPU' -Ok $false -Detail ('unsupported CPU ' + $arch) -Fix 'Use a 64-bit Windows PC (x64, or ARM64 with x64 emulation).'))
    }

    $psVersion = $PSVersionTable.PSVersion
    if ($psVersion -ge [version]'5.1') {
        $items.Add((ConvertTo-PrereqItem -Name 'PowerShell' -Ok $true -Detail $psVersion.ToString()))
    }
    else {
        $items.Add((ConvertTo-PrereqItem -Name 'PowerShell' -Ok $false -Detail ('PowerShell ' + $psVersion) -Fix 'Use Windows PowerShell 5.1 or PowerShell 7.'))
    }

    if (Test-CommandAvailable -Name 'tar.exe') {
        $items.Add((ConvertTo-PrereqItem -Name 'tar' -Ok $true -Detail 'tar.exe found'))
    }
    else {
        $items.Add((ConvertTo-PrereqItem -Name 'tar' -Ok $false -Detail 'tar.exe not found' -Fix 'Install Windows 10 version 1803 or later, which ships tar.exe in System32.'))
    }

    $freeInstall = Get-FreeDiskMb -Path $Paths.InstallRoot
    $freeCache = Get-FreeDiskMb -Path $Paths.CacheDir
    $freeMin = [math]::Min($freeInstall, $freeCache)
    if ($freeMin -ge $script:MinFreeMb) {
        $items.Add((ConvertTo-PrereqItem -Name 'Disk space' -Ok $true -Detail ([string]$freeMin + ' MB free')))
    }
    else {
        $items.Add((ConvertTo-PrereqItem -Name 'Disk space' -Ok $false -Detail ([string]$freeMin + ' MB free, ' + $script:MinFreeMb + ' MB needed') -Fix ('Free at least ' + $script:MinFreeMb + ' MB on the install and data drives.')))
    }

    $installWritable = Test-PathWritable -Path $Paths.InstallRoot
    $dataWritable = Test-PathWritable -Path $Paths.DataRoot
    if ($installWritable -and $dataWritable) {
        $items.Add((ConvertTo-PrereqItem -Name 'Write access' -Ok $true -Detail 'install and data folders can be created'))
    }
    else {
        $items.Add((ConvertTo-PrereqItem -Name 'Write access' -Ok $false -Detail 'cannot create the install or data folder' -Fix 'Choose writable folders with -InstallRoot and -DataRoot.'))
    }

    $isFolder = $BaseUrl -and ($BaseUrl -notmatch '^https://') -and (Test-Path -LiteralPath $BaseUrl -PathType Container)
    if ($isFolder) {
        $items.Add((ConvertTo-PrereqItem -Name 'Network' -Ok $true -Detail 'skipped, BaseUrl is a local folder'))
    }
    else {
        foreach ($hostName in $script:NetworkHosts) {
            if (Test-UrlReachable -Url ('https://' + $hostName) -TimeoutSec 10) {
                $items.Add((ConvertTo-PrereqItem -Name ('Network: ' + $hostName) -Ok $true -Detail 'reachable'))
            }
            else {
                $items.Add((ConvertTo-PrereqItem -Name ('Network: ' + $hostName) -Ok $false -Detail ('cannot reach https://' + $hostName) -Fix ('Check your connection, proxy or firewall allows https://' + $hostName + '.')))
            }
        }
    }

    return $items.ToArray()
}

function Show-Prerequisites {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        $Items
    )

    foreach ($item in @($Items)) {
        if ($item.Ok) {
            Write-Step -Status 'ok' -Name $item.Name -Detail $item.Detail
        }
        else {
            Write-Step -Status '!!' -Name $item.Name -Detail $item.Detail -Fix $item.Fix
        }
    }
}

# ---------------------------------------------------------------------------
# Entry point
# ---------------------------------------------------------------------------

function Resolve-Options {
    [CmdletBinding()]
    param(
        # The parameters the caller bound explicitly (a PSBoundParameters dictionary or a hashtable).
        [Parameter(Mandatory)]
        $Bound
    )

    $strings = @(
        @('Version', 'HUDDLE_VERSION', 'latest'),
        @('BaseUrl', 'HUDDLE_BASE_URL', ''),
        @('InstallRoot', 'HUDDLE_INSTALL_ROOT', ''),
        @('DataRoot', 'HUDDLE_DATA_ROOT', '')
    )
    $switches = @(
        @('CheckOnly', 'HUDDLE_CHECK_ONLY'),
        @('Uninstall', 'HUDDLE_UNINSTALL'),
        @('PurgeData', 'HUDDLE_PURGE_DATA'),
        @('Force', 'HUDDLE_FORCE'),
        @('NoLaunch', 'HUDDLE_NO_LAUNCH')
    )

    $result = [ordered]@{}
    foreach ($entry in $strings) {
        $value = $entry[2]
        if ($Bound.ContainsKey($entry[0])) {
            $value = [string]$Bound[$entry[0]]
        }
        else {
            $fromEnv = [Environment]::GetEnvironmentVariable($entry[1])
            if ($fromEnv) {
                $value = $fromEnv
            }
        }

        $result[$entry[0]] = $value
    }

    foreach ($entry in $switches) {
        if ($Bound.ContainsKey($entry[0])) {
            $on = [bool]$Bound[$entry[0]]
        }
        else {
            $fromEnv = [Environment]::GetEnvironmentVariable($entry[1])
            $on = [bool]($fromEnv -and ($fromEnv -ne '0'))
        }

        $result[$entry[0]] = $on
    }

    return [pscustomobject]$result
}

function Invoke-Main {
    [CmdletBinding()]
    param(
        [string]$Version = 'latest',
        [string]$BaseUrl = '',
        [string]$InstallRoot = '',
        [string]$DataRoot = '',
        [switch]$CheckOnly,
        [switch]$Uninstall,
        [switch]$PurgeData,
        [switch]$Force,
        [switch]$NoLaunch
    )

    # Function scope: these end with Invoke-Main, and the helpers it calls inherit them.
    Set-StrictMode -Version Latest
    $ErrorActionPreference = 'Stop'

    $options = Resolve-Options -Bound $PSBoundParameters
    $Version = $options.Version
    $BaseUrl = $options.BaseUrl
    $InstallRoot = $options.InstallRoot
    $DataRoot = $options.DataRoot
    $CheckOnly = $options.CheckOnly
    $Uninstall = $options.Uninstall
    $PurgeData = $options.PurgeData
    $Force = $options.Force
    $NoLaunch = $options.NoLaunch

    if ($BaseUrl -and ($BaseUrl -notmatch '^https://') -and (-not (Test-Path -LiteralPath $BaseUrl -PathType Container))) {
        Write-Step -Status '!!' -Name 'usage' -Detail ('BaseUrl is neither an https URL nor an existing folder: ' + $BaseUrl) -Fix 'Pass -BaseUrl https://... or the path of an existing folder.'
        return $script:ExitCodes.BadUsage
    }

    $paths = Resolve-InstallPaths -InstallRoot $InstallRoot -DataRoot $DataRoot
    $items = @(Test-Prerequisites -Paths $paths -BaseUrl $BaseUrl)
    Show-Prerequisites -Items $items

    if (@($items | Where-Object { -not $_.Ok }).Count -gt 0) {
        return $script:ExitCodes.PrerequisitesUnmet
    }

    return $script:ExitCodes.Success
}

if (-not $env:HUDDLE_INSTALL_NO_MAIN) {
    # Under iex (irm | iex) or as a scriptblock, exit would close the user's PowerShell window.
    # No top-level variable is created here: under iex it would persist in the user's session.
    if ($PSCommandPath) {
        exit (Invoke-Main @PSBoundParameters)
    }

    $global:LASTEXITCODE = Invoke-Main @PSBoundParameters

    # At the prompt, $script: is the global scope, so the constants above would stay in the user's session.
    Remove-Variable -Name HuddleRepo, RepoPlaceholder, ExitCodes, MinWindowsBuild, MinFreeMb, NetworkHosts, StateKeys -Scope Script -ErrorAction SilentlyContinue
}
