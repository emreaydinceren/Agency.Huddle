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
    [switch]$NoLaunch,
    [switch]$NoShellIntegration
)

# Strict mode and ErrorActionPreference 'Stop' are set inside Invoke-Main, not here: under
# irm | iex this top level runs in the user's own session and must not change it.

# Under iex every function below would stay in the user's session, so the names that exist before this
# script defines anything are remembered and the entry guard at the end removes the rest. The only
# top-level variable; the guard removes it. Not taken when running as a file or for the tests.
if ((-not $env:HUDDLE_INSTALL_NO_MAIN) -and (-not {}.File)) {
    $huddleFunctionSnapshot = @((Get-ChildItem -Path Function:).Name)
}

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

# Makes a folder path absolute against the session location (not the process directory). Throws a
# message that ends in 'fix:' for a non-FileSystem provider path or a drive that does not exist.
function ConvertTo-AbsolutePath {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter(Mandatory)]
        [string]$Label
    )

    $provider = $null
    $drive = $null
    try {
        $full = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Path, [ref]$provider, [ref]$drive)
    }
    catch {
        throw ($Label + ' ' + $Path + ' cannot be used: its drive or location does not exist. fix: pass a folder on a drive that exists, for example C:\Huddle')
    }

    if (($null -eq $provider) -or (-not [string]::Equals($provider.Name, 'FileSystem', [System.StringComparison]::OrdinalIgnoreCase))) {
        throw ($Label + ' ' + $Path + ' is not a file system folder. fix: pass a folder path such as C:\Huddle')
    }

    return $full
}

function Resolve-InstallPaths {
    [CmdletBinding()]
    param(
        [string]$InstallRoot = '',
        [string]$DataRoot = '',
        [string]$ShortcutPath = ''
    )

    $localAppData = $env:LOCALAPPDATA
    if (-not $localAppData) {
        $localAppData = [Environment]::GetFolderPath('LocalApplicationData')
    }

    if ([string]::IsNullOrWhiteSpace($ShortcutPath)) {
        $appData = $env:APPDATA
        if (-not $appData) {
            $appData = [Environment]::GetFolderPath('ApplicationData')
        }

        $ShortcutPath = Join-Path $appData 'Microsoft\Windows\Start Menu\Programs\Agency Huddle.lnk'
    }

    if ([string]::IsNullOrWhiteSpace($InstallRoot)) {
        $InstallRoot = Join-Path $localAppData 'Programs\Agency.Huddle'
    }

    if ([string]::IsNullOrWhiteSpace($DataRoot)) {
        $DataRoot = Join-Path $localAppData 'Agency.Huddle'
    }

    $InstallRoot = ConvertTo-AbsolutePath -Path $InstallRoot -Label 'InstallRoot'
    $DataRoot = ConvertTo-AbsolutePath -Path $DataRoot -Label 'DataRoot'

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
        ShortcutPath    = $ShortcutPath
        Rid           = 'win-x64'
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
        # The release workflow takes 1.0.0 and creates the tag v1.0.0; both spellings reach the same URL.
        $tag = $Version
        if (-not $tag.StartsWith('v', [System.StringComparison]::Ordinal)) {
            $tag = 'v' + $tag
        }

        $location = $root + '/download/' + $tag + '/' + $Name
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

    # The protocol list is process-wide, so it is put back afterwards (under iex that is the user's session).
    $originalProtocol = [Net.ServicePointManager]::SecurityProtocol
    try {
        if ($PSVersionTable.PSVersion.Major -lt 6) {
            [Net.ServicePointManager]::SecurityProtocol = $originalProtocol -bor [Net.SecurityProtocolType]::Tls12
        }

        Invoke-WebRequest -Uri $Url -OutFile $OutFile -UseBasicParsing
    }
    finally {
        [Net.ServicePointManager]::SecurityProtocol = $originalProtocol
    }
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

# Spells a path for huddle.cmd: under InstallRoot as %~dp0 plus the remainder (the launcher lives in
# InstallRoot), under LOCALAPPDATA as %LOCALAPPDATA%\..., anything else unchanged. Case-insensitive.
function ConvertTo-LauncherPath {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter(Mandatory)]
        [string]$InstallRoot
    )

    $text = $Path.TrimEnd('\', '/')
    $root = $InstallRoot.TrimEnd('\', '/')
    if ([string]::Equals($text, $root, [System.StringComparison]::OrdinalIgnoreCase)) {
        return '%~dp0'
    }

    if ($text.StartsWith($root + '\', [System.StringComparison]::OrdinalIgnoreCase)) {
        return '%~dp0' + $text.Substring($root.Length + 1)
    }

    $local = ([string]$env:LOCALAPPDATA).TrimEnd('\', '/')
    if ($local) {
        if ([string]::Equals($text, $local, [System.StringComparison]::OrdinalIgnoreCase)) {
            return '%LOCALAPPDATA%'
        }

        if ($text.StartsWith($local + '\', [System.StringComparison]::OrdinalIgnoreCase)) {
            return '%LOCALAPPDATA%\' + $text.Substring($local.Length + 1)
        }
    }

    return $Path
}

function Get-LauncherContent {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [pscustomobject]$Paths
    )

    # The file is written as ASCII, so a path with a non-ASCII character (a user name such as Jose with an
    # acute e) cannot be spelled out. Everything under InstallRoot is relative to the launcher (%~dp0, which
    # ends with a backslash) and a data folder under LOCALAPPDATA goes through that variable.
    $nodeDir = ConvertTo-LauncherPath -Path (Split-Path -Parent $Paths.NodeExe) -InstallRoot $Paths.InstallRoot
    $nodeExe = ConvertTo-LauncherPath -Path $Paths.NodeExe -InstallRoot $Paths.InstallRoot
    $dataDir = ConvertTo-LauncherPath -Path ($Paths.DataRoot.TrimEnd('\', '/') + '\data') -InstallRoot $Paths.InstallRoot
    $exePath = ConvertTo-LauncherPath -Path $Paths.ExePath -InstallRoot $Paths.InstallRoot
    $appDir = ConvertTo-LauncherPath -Path (Split-Path -Parent $Paths.ExePath) -InstallRoot $Paths.InstallRoot

    # The outer quotes in set "VAR=value" protect spaces in the value.
    # The app takes its content root, and so its wwwroot, from the current directory: started from anywhere
    # else (the Start menu shortcut starts it in InstallRoot) every stylesheet and script answers 200 with an
    # empty body. cd /d sets that folder; setlocal gives the caller's own folder back when the launcher ends.
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
        'cd /d "' + $appDir + '"'
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

# Seam: waits for the Huddle.App process to disappear. Returns $true when it is gone, $false at the timeout.
function Wait-HuddleProcessExit {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [int]$ProcessId,

        [int]$TimeoutSec = 10
    )

    $deadline = [datetime]::UtcNow.AddSeconds($TimeoutSec)
    while (Test-ProcessRunning -ProcessId $ProcessId) {
        if ([datetime]::UtcNow -ge $deadline) {
            return $false
        }

        Start-Sleep -Milliseconds 200
    }

    return $true
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
        # The stop request returns before the process has finished exiting: give it up to 10 seconds.
        if ((Test-ProcessRunning -ProcessId $instancePid) -and (-not (Wait-HuddleProcessExit -ProcessId $instancePid -TimeoutSec 10))) {
            if (-not $Force) {
                throw ('Agency Huddle (pid ' + $instancePid + ') is still running after the stop request. Close it and run the installer again, or pass -Force to end the process (with irm | iex, set $env:HUDDLE_FORCE=''1'' first).')
            }

            Write-Step -Status '..' -Name 'stop' -Detail ('ending process ' + $instancePid + ' (-Force)')
            Stop-HuddleProcess -ProcessId $instancePid
        }
    }
    elseif ($exitCode -ne 0) {
        if (-not $Force) {
            throw ('The stop request for Agency Huddle failed (exit code ' + $exitCode + '). Close it and run the installer again, or pass -Force (with irm | iex, set $env:HUDDLE_FORCE=''1'' first).')
        }

        Write-Step -Status '..' -Name 'stop' -Detail ('stop request exited with code ' + $exitCode + '; continuing because of -Force')
    }

    Write-Step -Status 'ok' -Name 'stop' -Detail 'stopped'
    return $true
}

# True when the folder holds a file only an Agency Huddle install or release leaves behind.
function Test-HuddleMarker {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    foreach ($name in @('release-info.json', 'install-state.json')) {
        if (Test-Path -LiteralPath (Join-Path $Path $name) -PathType Leaf) {
            return $true
        }
    }

    return $false
}

# True when the folder is an install: it holds install-state.json, or release-info.json together with
# the app executable. A release-info.json alone could belong to any other project.
function Test-HuddleInstallRoot {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    if (Test-Path -LiteralPath (Join-Path $Path 'install-state.json') -PathType Leaf) {
        return $true
    }

    return ((Test-Path -LiteralPath (Join-Path $Path 'release-info.json') -PathType Leaf) -and (Test-Path -LiteralPath (Join-Path $Path 'app\Huddle.App.exe') -PathType Leaf))
}

# Describes how the two roots overlap (one equals or contains the other), or returns $null when they
# are separate folders. Installing would otherwise replace the data, or an uninstall would delete it.
function Get-RootOverlap {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        $Paths
    )

    $installKey = [System.IO.Path]::GetFullPath($Paths.InstallRoot).TrimEnd('\', '/')
    $dataKey = [System.IO.Path]::GetFullPath($Paths.DataRoot).TrimEnd('\', '/')
    $ordinal = [System.StringComparison]::OrdinalIgnoreCase

    if ([string]::Equals($installKey, $dataKey, $ordinal)) {
        return 'InstallRoot and DataRoot are the same folder (' + $installKey + ')'
    }

    if ($dataKey.StartsWith($installKey + '\', $ordinal)) {
        return 'the data folder ' + $dataKey + ' is inside the install folder ' + $installKey + ', and an upgrade replaces that folder'
    }

    if ($installKey.StartsWith($dataKey + '\', $ordinal)) {
        return 'the install folder ' + $installKey + ' is inside the data folder ' + $dataKey
    }

    return $null
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
    if ((-not $Force) -and (Test-StepSatisfied -State $State -Key 'appSha256' -Expected $archiveSha) -and (Test-Path -LiteralPath $Paths.ExePath -PathType Leaf)) {
        Write-Step -Status 'skip' -Name 'app' -Detail 'already installed'
        return $false
    }

    $installRoot = $Paths.InstallRoot.TrimEnd('\', '/')
    $staging = $installRoot + '.staging'
    $old = $installRoot + '.old'

    # Nothing is moved or deleted until every folder this step touches is known to be ours: the root
    # is missing, empty or a Huddle install, and the leftovers carry a Huddle marker file.
    $refusal = 'fix: choose an empty folder or remove it'
    if (-not (Test-SafeToDelete -Path $installRoot -MinSegments 2)) {
        throw ($installRoot + ' is a drive root or a protected folder (such as your user profile), so the installer will not replace it. ' + $refusal)
    }

    if (Test-Path -LiteralPath $installRoot) {
        $isFolder = Test-Path -LiteralPath $installRoot -PathType Container
        if (-not $isFolder) {
            throw ($installRoot + ' is a file, not a folder. ' + $refusal)
        }

        $isEmpty = @(Get-ChildItem -LiteralPath $installRoot -Force).Count -eq 0
        if ((-not $isEmpty) -and (-not (Test-HuddleInstallRoot -Path $installRoot))) {
            throw ($installRoot + ' is not empty and is not an Agency Huddle install. ' + $refusal)
        }
    }

    foreach ($leftover in @($staging, $old)) {
        if ((Test-Path -LiteralPath $leftover) -and ((-not (Test-HuddleMarker -Path $leftover)) -or (-not (Test-SafeToDelete -Path $leftover)))) {
            throw ($leftover + ' is not a leftover of an Agency Huddle install and was not touched. ' + $refusal)
        }
    }

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

    # npm ci runs in a staged copy, so a proxy or offline failure cannot delete the live node_modules.
    $staging = $acpDir + '.staging'
    $liveModules = Join-Path $acpDir 'node_modules'
    $oldModules = Join-Path $acpDir 'node_modules.old'
    $stagedModules = Join-Path $staging 'node_modules'
    $entryRelative = $Paths.AdapterEntry.Substring($acpDir.Length).TrimStart('\', '/')
    $stagedEntry = Join-Path $staging $entryRelative

    if (Test-Path -LiteralPath $staging) {
        Remove-Item -LiteralPath $staging -Recurse -Force
    }

    try {
        New-Item -ItemType Directory -Path $staging -Force | Out-Null
        foreach ($name in @('package.json', 'package-lock.json')) {
            $source = Join-Path $acpDir $name
            if (Test-Path -LiteralPath $source -PathType Leaf) {
                Copy-Item -LiteralPath $source -Destination (Join-Path $staging $name) -Force
            }
        }

        $exitCode = Invoke-NpmCi -NodeExe $Paths.NodeExe -WorkingDirectory $staging -LogPath $logPath
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

        if (-not (Test-Path -LiteralPath $stagedEntry -PathType Leaf)) {
            throw ('npm ci finished but the adapter entry not found at ' + $Paths.AdapterEntry + '. fix: run the installer again; if it persists, delete ' + $liveModules + ' and retry.')
        }

        # Swap: the old tree moves aside, the staged one moves in, the old one is deleted. A failed move
        # puts the old tree back.
        if (Test-Path -LiteralPath $oldModules) {
            Remove-Item -LiteralPath $oldModules -Recurse -Force
        }

        $hadOld = Test-Path -LiteralPath $liveModules
        if ($hadOld) {
            Move-Item -LiteralPath $liveModules -Destination $oldModules
        }

        try {
            Move-Item -LiteralPath $stagedModules -Destination $liveModules
        }
        catch {
            if ($hadOld -and (-not (Test-Path -LiteralPath $liveModules))) {
                Move-Item -LiteralPath $oldModules -Destination $liveModules
            }

            throw
        }

        if (Test-Path -LiteralPath $oldModules) {
            try {
                Remove-Item -LiteralPath $oldModules -Recurse -Force
            }
            catch {
                # The new tree is already in place; a leftover node_modules.old is removed by the next run.
                Write-Verbose ('Could not delete ' + $oldModules + ': ' + $_.Exception.Message)
            }
        }
    }
    finally {
        if (Test-Path -LiteralPath $staging) {
            Remove-Item -LiteralPath $staging -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    Write-Step -Status 'ok' -Name 'adapter' -Detail 'installed'
    return $true
}

# ---------------------------------------------------------------------------
# Entry points: launcher, Start menu shortcut and user PATH
# ---------------------------------------------------------------------------

# Seam: creates (or overwrites) a .lnk through the WScript.Shell COM object.
function New-HuddleShortcut {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter(Mandatory)]
        [string]$Target,

        [Parameter(Mandatory)]
        [string]$WorkingDirectory,

        [int]$WindowStyle = 7,

        [string]$IconPath = ''
    )

    $parent = Split-Path -Parent $Path
    if ($parent -and -not (Test-Path -LiteralPath $parent -PathType Container)) {
        New-Item -ItemType Directory -Path $parent -Force | Out-Null
    }

    $shell = New-Object -ComObject WScript.Shell
    if ($null -eq $shell) {
        throw 'The Windows Script Host (WScript.Shell) is not available, so the Start menu shortcut cannot be created.'
    }

    try {
        $link = $shell.CreateShortcut($Path)
        $link.TargetPath = $Target
        $link.WorkingDirectory = $WorkingDirectory
        $link.WindowStyle = $WindowStyle
        $link.Description = 'Agency Huddle'
        if ($IconPath) {
            $link.IconLocation = $IconPath + ',0'
        }

        $link.Save()
    }
    finally {
        [void][System.Runtime.InteropServices.Marshal]::ReleaseComObject($shell)
    }
}

# Seam: the target of an existing .lnk, or $null when the file is missing or unreadable.
function Get-ShortcutTarget {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return $null
    }

    $shell = New-Object -ComObject WScript.Shell
    if ($null -eq $shell) {
        return $null
    }

    try {
        return [string]$shell.CreateShortcut($Path).TargetPath
    }
    catch {
        # A shortcut that cannot be read has no known target: callers treat that as "not ours".
        return $null
    }
    finally {
        [void][System.Runtime.InteropServices.Marshal]::ReleaseComObject($shell)
    }
}

# Seam: HKCU\Environment value Path exactly as stored, %VARS% not expanded ('' when unset).
function Get-UserPathRegistryValue {
    [CmdletBinding()]
    param()

    $key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Environment')
    if ($null -eq $key) {
        return ''
    }

    try {
        return [string]$key.GetValue('Path', '', [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames)
    }
    finally {
        $key.Close()
    }
}

# Seam: writes HKCU\Environment value Path with the given kind (the caller passes ExpandString).
function Set-UserPathRegistryValue {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [AllowEmptyString()]
        [string]$Value,

        [Parameter(Mandatory)]
        [Microsoft.Win32.RegistryValueKind]$Kind
    )

    $key = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey('Environment')
    try {
        $key.SetValue('Path', $Value, $Kind)
    }
    finally {
        $key.Close()
    }
}

# Seam: tells running programs (Explorer, new terminals) that the user environment changed, by broadcasting
# WM_SETTINGCHANGE for 'Environment'. It never throws: the PATH is already saved, only the live refresh is lost.
function Send-EnvironmentChange {
    [CmdletBinding()]
    param()

    try {
        if (-not ('HuddleInstaller.NativeMethods' -as [type])) {
            $signature = '[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)] public static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint Msg, UIntPtr wParam, string lParam, uint fuFlags, uint uTimeout, out UIntPtr lpdwResult);'
            Add-Type -Namespace HuddleInstaller -Name NativeMethods -MemberDefinition $signature
        }

        $result = [UIntPtr]::Zero
        # HWND_BROADCAST = 0xffff, WM_SETTINGCHANGE = 0x1A, SMTO_ABORTIFHUNG = 2, five seconds per window.
        $null = [HuddleInstaller.NativeMethods]::SendMessageTimeout([IntPtr]0xffff, 0x1A, [UIntPtr]::Zero, 'Environment', 2, 5000, [ref]$result)
    }
    catch {
        # The broadcast only refreshes programs that are already open; a new terminal reads the registry anyway.
        Write-Verbose ('Could not broadcast the environment change: ' + $_.Exception.Message)
    }
}

# Seam: the current user's persistent PATH with %VARS% unexpanded, or '' when unset.
function Get-UserPathValue {
    [CmdletBinding()]
    param()

    return Get-UserPathRegistryValue
}

# Seam: replaces the current user's persistent PATH. Written as REG_EXPAND_SZ so the entries that use
# %VARS% keep working, then the change is broadcast.
function Set-UserPathValue {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [AllowEmptyString()]
        [string]$Value
    )

    Set-UserPathRegistryValue -Value $Value -Kind ([Microsoft.Win32.RegistryValueKind]::ExpandString)
    Send-EnvironmentChange
}

# Splits a PATH value into its non-empty entries.
function Split-PathValue {
    [CmdletBinding()]
    param(
        [AllowNull()]
        [AllowEmptyString()]
        [string]$Value
    )

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return , @()
    }

    $entries = @($Value.Split(';') | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    return , $entries
}

# True when a PATH entry names the folder: %VARS% on both sides are expanded first, case-insensitive,
# trailing backslash tolerant.
function Test-PathEntryMatch {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Entry,

        [Parameter(Mandatory)]
        [string]$Folder
    )

    $left = [Environment]::ExpandEnvironmentVariables($Entry.Trim()).TrimEnd('\', '/')
    $right = [Environment]::ExpandEnvironmentVariables($Folder.Trim()).TrimEnd('\', '/')
    return [string]::Equals($left, $right, [System.StringComparison]::OrdinalIgnoreCase)
}

# Writes huddle.cmd, the Start menu shortcut and the user PATH entry. Each is rewritten only when its
# content differs. Returns $true when anything changed, $false when all three were already right.
# -NoShellIntegration (the smoke test uses it) writes huddle.cmd only: no shortcut, no user PATH edit.
function Install-Launcher {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [pscustomobject]$Paths,

        [switch]$NoShellIntegration
    )

    $changed = $false

    # Launcher: compare the bytes that would be written with the bytes on disk. The file is ASCII, so a path
    # that still has a non-ASCII character after the relative forms would be written as '?': refuse it
    # before anything (launcher, shortcut or PATH) is changed.
    $content = (Get-LauncherContent -Paths $Paths) + "`r`n"
    foreach ($character in $content.ToCharArray()) {
        if ([int]$character -gt 127) {
            throw 'The launcher cannot spell a path that has a non-ASCII character, so nothing was written. fix: choose an install folder with only ASCII characters (-InstallRoot)'
        }
    }

    $expected = [System.Text.Encoding]::ASCII.GetBytes($content)
    $current = $null
    if (Test-Path -LiteralPath $Paths.LauncherPath -PathType Leaf) {
        $current = [System.IO.File]::ReadAllBytes($Paths.LauncherPath)
    }

    $same = $false
    if ($null -ne $current -and $current.Length -eq $expected.Length) {
        $same = ([System.Convert]::ToBase64String($current) -ceq [System.Convert]::ToBase64String($expected))
    }

    if ($same) {
        Write-Step -Status 'skip' -Name 'launcher' -Detail 'huddle.cmd is up to date'
    }
    else {
        if (-not (Test-Path -LiteralPath $Paths.InstallRoot -PathType Container)) {
            New-Item -ItemType Directory -Path $Paths.InstallRoot -Force | Out-Null
        }

        [System.IO.File]::WriteAllBytes($Paths.LauncherPath, $expected)
        Write-Step -Status 'ok' -Name 'launcher' -Detail ('wrote ' + $Paths.LauncherPath)
        $changed = $true
    }

    if ($NoShellIntegration) {
        return $changed
    }

    # Shortcut: present and pointing at this launcher.
    $target = Get-ShortcutTarget -Path $Paths.ShortcutPath
    if ($target -and (Test-PathEntryMatch -Entry $target -Folder $Paths.LauncherPath)) {
        Write-Step -Status 'skip' -Name 'shortcut' -Detail 'Start menu shortcut is up to date'
    }
    else {
        New-HuddleShortcut -Path $Paths.ShortcutPath -Target $Paths.LauncherPath -WorkingDirectory $Paths.InstallRoot -WindowStyle 7 -IconPath $Paths.ExePath
        Write-Step -Status 'ok' -Name 'shortcut' -Detail ('created ' + $Paths.ShortcutPath)
        $changed = $true
    }

    # User PATH: append InstallRoot once.
    $entries = Split-PathValue -Value (Get-UserPathValue)
    $found = $false
    foreach ($entry in $entries) {
        if (Test-PathEntryMatch -Entry $entry -Folder $Paths.InstallRoot) {
            $found = $true
            break
        }
    }

    if ($found) {
        Write-Step -Status 'skip' -Name 'path' -Detail 'InstallRoot is already on the user PATH'
    }
    else {
        Set-UserPathValue -Value ((@($entries) + $Paths.InstallRoot) -join ';')
        Write-Step -Status 'ok' -Name 'path' -Detail 'added InstallRoot to the user PATH (open a new terminal to use huddle)'
        $changed = $true
    }

    return $changed
}

# ---------------------------------------------------------------------------
# Uninstall
# ---------------------------------------------------------------------------

# True when it is safe to delete the folder recursively. The path is resolved with GetFullPath first and
# a wildcard is never accepted. Refuses: empty text, a drive or share root, fewer than -MinSegments (default 3;
# the install step passes 2, so C:\Huddle works) path segments, the
# user profile (and any folder that contains it), LOCALAPPDATA and APPDATA themselves. With
# -RequireStateFile the folder must also hold install-state.json (used for InstallRoot only).
function Test-SafeToDelete {
    [CmdletBinding()]
    param(
        [AllowEmptyString()]
        [AllowNull()]
        [string]$Path,

        [switch]$RequireStateFile,

        [int]$MinSegments = 3
    )

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return $false
    }

    if ($Path.IndexOfAny([char[]]@('*', '?')) -ge 0) {
        return $false
    }

    try {
        $resolved = [System.IO.Path]::GetFullPath($Path)
    }
    catch {
        # A path .NET cannot resolve is not a path we will delete.
        return $false
    }

    $full = $resolved.TrimEnd('\', '/')
    $root = [System.IO.Path]::GetPathRoot($resolved)
    if ([string]::IsNullOrEmpty($root) -or [string]::Equals($full, $root.TrimEnd('\', '/'), [System.StringComparison]::OrdinalIgnoreCase)) {
        return $false
    }

    $segments = @($full.Split([char[]]@('\', '/'), [System.StringSplitOptions]::RemoveEmptyEntries))
    if ($segments.Count -lt $MinSegments) {
        return $false
    }

    $profilePaths = @($env:USERPROFILE, [Environment]::GetFolderPath('UserProfile'))
    foreach ($candidate in $profilePaths) {
        if ([string]::IsNullOrWhiteSpace($candidate)) {
            continue
        }

        try {
            $protected = [System.IO.Path]::GetFullPath($candidate).TrimEnd('\', '/')
        }
        catch {
            # An unresolvable profile path cannot be compared; the other candidates still apply.
            continue
        }

        if ([string]::Equals($full, $protected, [System.StringComparison]::OrdinalIgnoreCase)) {
            return $false
        }

        if ($protected.StartsWith($full + '\', [System.StringComparison]::OrdinalIgnoreCase)) {
            return $false
        }
    }

    foreach ($candidate in @($env:LOCALAPPDATA, $env:APPDATA)) {
        if ([string]::IsNullOrWhiteSpace($candidate)) {
            continue
        }

        try {
            $protected = [System.IO.Path]::GetFullPath($candidate).TrimEnd('\', '/')
        }
        catch {
            # An unresolvable well-known folder cannot be compared; skip it.
            continue
        }

        if ([string]::Equals($full, $protected, [System.StringComparison]::OrdinalIgnoreCase)) {
            return $false
        }
    }

    if ($RequireStateFile) {
        if (-not (Test-Path -LiteralPath (Join-Path $full 'install-state.json') -PathType Leaf)) {
            return $false
        }
    }

    return $true
}

# Removes the install: stops a running instance, then the shortcut (only if it points at this launcher),
# the PATH entry and InstallRoot (install-state.json last, so a failed run can be repeated). DataRoot is kept
# unless -PurgeData. Every safety check runs before anything is changed. Returns an exit code (0 or 1).
function Invoke-Uninstall {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [pscustomobject]$Paths,

        [switch]$PurgeData,

        [switch]$Force
    )

    try {
        $installRoot = [System.IO.Path]::GetFullPath($Paths.InstallRoot)
        $dataRoot = [System.IO.Path]::GetFullPath($Paths.DataRoot)
    }
    catch {
        Write-Step -Status '!!' -Name 'uninstall' -Detail ('cannot resolve the install folders: ' + $_.Exception.Message) -Fix 'pass valid -InstallRoot and -DataRoot paths'
        return 1
    }

    $installExists = Test-Path -LiteralPath $installRoot -PathType Container
    $dataExists = Test-Path -LiteralPath $dataRoot -PathType Container

    # Safety first: nothing is stopped, edited or deleted until every check has passed.
    if ($installExists -and -not (Test-SafeToDelete -Path $installRoot -RequireStateFile)) {
        Write-Step -Status '!!' -Name 'uninstall' -Detail ('refusing to delete ' + $installRoot + ': it has no install-state.json, or it is a drive root, a folder with fewer than 3 segments or your profile') -Fix 'check the -InstallRoot value; delete the folder by hand only if you are sure it is yours'
        return 1
    }

    if ($PurgeData -and $dataExists -and -not (Test-SafeToDelete -Path $dataRoot)) {
        Write-Step -Status '!!' -Name 'uninstall' -Detail ('refusing to delete the data folder ' + $dataRoot + ': it is a drive root, a folder with fewer than 3 segments or your profile') -Fix 'check the -DataRoot value'
        return 1
    }

    if ($installExists -and -not $PurgeData) {
        $installKey = $installRoot.TrimEnd('\', '/')
        $dataKey = $dataRoot.TrimEnd('\', '/')
        if ([string]::Equals($dataKey, $installKey, [System.StringComparison]::OrdinalIgnoreCase) -or $dataKey.StartsWith($installKey + '\', [System.StringComparison]::OrdinalIgnoreCase)) {
            Write-Step -Status '!!' -Name 'uninstall' -Detail ('the data folder ' + $dataRoot + ' is inside the install folder, so removing the install would delete your data') -Fix 'move the data folder out, or pass -PurgeData if you want it deleted too'
            return 1
        }
    }

    if ($installExists) {
        try {
            $null = Stop-HuddleIfRunning -Paths $Paths -Force:$Force
        }
        catch {
            Write-Step -Status '!!' -Name 'stop' -Detail $_.Exception.Message -Fix 'close Agency Huddle and run the uninstall again, or pass -Force to end the process'
            return 1
        }
    }

    $changed = $false
    try {
        if (Test-Path -LiteralPath $Paths.ShortcutPath -PathType Leaf) {
            $target = Get-ShortcutTarget -Path $Paths.ShortcutPath
            if ($target -and (Test-PathEntryMatch -Entry $target -Folder $Paths.LauncherPath)) {
                Remove-Item -LiteralPath $Paths.ShortcutPath -Force -ErrorAction Stop
                Write-Step -Status 'ok' -Name 'shortcut' -Detail 'removed the Start menu shortcut'
                $changed = $true
            }
            else {
                Write-Step -Status 'skip' -Name 'shortcut' -Detail 'kept: the shortcut does not point at this install'
            }
        }
        else {
            Write-Step -Status 'skip' -Name 'shortcut' -Detail 'no Start menu shortcut'
        }

        $entries = Split-PathValue -Value (Get-UserPathValue)
        $kept = @($entries | Where-Object { -not (Test-PathEntryMatch -Entry $_ -Folder $Paths.InstallRoot) })
        if ($kept.Count -ne @($entries).Count) {
            Set-UserPathValue -Value ($kept -join ';')
            Write-Step -Status 'ok' -Name 'path' -Detail 'removed InstallRoot from the user PATH'
            $changed = $true
        }
        else {
            Write-Step -Status 'skip' -Name 'path' -Detail 'InstallRoot is not on the user PATH'
        }

        if ($installExists) {
            # install-state.json goes last: while it exists the folder is still recognised as ours and a
            # second uninstall can finish the job.
            foreach ($child in @(Get-ChildItem -LiteralPath $installRoot -Force -ErrorAction Stop)) {
                if (-not [string]::Equals($child.Name, 'install-state.json', [System.StringComparison]::OrdinalIgnoreCase)) {
                    Remove-Item -LiteralPath $child.FullName -Recurse -Force -ErrorAction Stop
                }
            }

            Remove-Item -LiteralPath $installRoot -Recurse -Force -ErrorAction Stop
            Write-Step -Status 'ok' -Name 'install' -Detail ('removed ' + $installRoot)
            $changed = $true
        }
        else {
            Write-Step -Status 'skip' -Name 'install' -Detail 'not installed'
        }

        if ($PurgeData) {
            if ($dataExists) {
                Remove-Item -LiteralPath $dataRoot -Recurse -Force -ErrorAction Stop
                Write-Step -Status 'ok' -Name 'data' -Detail ('removed ' + $dataRoot)
                $changed = $true
            }
            else {
                Write-Step -Status 'skip' -Name 'data' -Detail 'no data folder'
            }
        }
        elseif ($dataExists) {
            Write-Step -Status 'ok' -Name 'data' -Detail ('kept ' + $dataRoot + ' (pass -PurgeData to delete it too)')
        }
    }
    catch {
        Write-Step -Status '!!' -Name 'uninstall' -Detail $_.Exception.Message -Fix 'close Agency Huddle and anything using files under the install folder, then run the uninstall again'
        return 1
    }

    if (-not $changed) {
        Write-Step -Status 'skip' -Name 'uninstall' -Detail 'nothing to remove'
    }

    return 0
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
    $savedTime = [System.IO.Directory]::GetLastWriteTimeUtc($ancestor)
    try {
        [System.IO.File]::WriteAllText($probe, 'x')
        Remove-Item -LiteralPath $probe -Force
        return $true
    }
    catch {
        # Not writable (permissions, read-only media): the caller reports it.
        return $false
    }
    finally {
        # The probe must leave no trace: a second install run changes no file or folder time.
        try {
            [System.IO.Directory]::SetLastWriteTimeUtc($ancestor, $savedTime)
        }
        catch {
            # Best effort only: a folder we could not write to cannot be restored either.
            $null = $_
        }
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
    $originalProtocol = [Net.ServicePointManager]::SecurityProtocol
    try {
        [Net.ServicePointManager]::SecurityProtocol = $originalProtocol -bor [Net.SecurityProtocolType]::Tls12
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
    finally {
        [Net.ServicePointManager]::SecurityProtocol = $originalProtocol
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
        @('NoLaunch', 'HUDDLE_NO_LAUNCH'),
        @('NoShellIntegration', 'HUDDLE_NO_SHELL_INTEGRATION')
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

# Seam: takes the per-user installer mutex without waiting. Returns a releaser (a script block to invoke
# in the finally block), or $null when another installer already holds it. The Local\ prefix scopes the
# name to the logon session, so two users on one machine do not block each other.
function Enter-InstallerMutex {
    [CmdletBinding()]
    param()

    $mutex = [System.Threading.Mutex]::new($false, 'Local\Agency.Huddle.Installer')
    $acquired = $false
    try {
        $acquired = $mutex.WaitOne(0)
    }
    catch [System.Threading.AbandonedMutexException] {
        # The previous installer died while holding it; the mutex is ours now.
        $acquired = $true
    }

    if (-not $acquired) {
        $mutex.Dispose()
        return $null
    }

    return {
        try {
            $mutex.ReleaseMutex()
        }
        catch {
            # Released twice or from another thread: nothing is left to release.
            $null = $_
        }

        $mutex.Dispose()
    }.GetNewClosure()
}

# Seam: true when a person is at the console and may be pointed at a browser sign-in.
function Test-InteractiveSession {
    [CmdletBinding()]
    param()

    foreach ($name in @('NO_BROWSER', 'SSH_CONNECTION', 'SSH_CLIENT', 'SSH_TTY', 'CLAUDE_CODE_REMOTE')) {
        if ([Environment]::GetEnvironmentVariable($name)) {
            return $false
        }
    }

    if (-not [Environment]::UserInteractive) {
        return $false
    }

    try {
        if ([Console]::IsInputRedirected) {
            return $false
        }
    }
    catch {
        # No console at all (a service or a hidden host): not interactive.
        return $false
    }

    return $true
}

# Seam: runs "<claude> auth status" and returns its exit code. The output (it names the account) is
# discarded, never read. A missing binary, a start failure or the time cap all return -1 (= not signed in).
function Invoke-ClaudeAuthStatus {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$ClaudePath,

        [int]$TimeoutSeconds = 10
    )

    if (-not (Test-Path -LiteralPath $ClaudePath -PathType Leaf)) {
        return -1
    }

    $info = New-Object System.Diagnostics.ProcessStartInfo
    $info.FileName = $ClaudePath
    $info.Arguments = 'auth status'
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.RedirectStandardInput = $true
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true

    $process = $null
    try {
        $process = [System.Diagnostics.Process]::Start($info)
        $process.StandardInput.Close()
        # No handler is attached, so every line that arrives is dropped.
        $process.BeginOutputReadLine()
        $process.BeginErrorReadLine()

        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            try {
                $process.Kill()
                $null = $process.WaitForExit(2000)
            }
            catch {
                # It exited between the wait and the kill: the answer is "not signed in" either way.
                $null = $_
            }

            return -1
        }

        return [int]$process.ExitCode
    }
    catch {
        # Could not start (blocked by policy, corrupt binary): not signed in.
        return -1
    }
    finally {
        if ($null -ne $process) {
            $process.Dispose()
        }
    }
}

# Seam: starts the launcher minimized and does not wait for it.
function Start-HuddleLauncher {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$LauncherPath
    )

    if (-not (Test-Path -LiteralPath $LauncherPath -PathType Leaf)) {
        throw ('The launcher was not found: ' + $LauncherPath)
    }

    Start-Process -FilePath $LauncherPath -WorkingDirectory (Split-Path -Parent $LauncherPath) -WindowStyle Minimized | Out-Null
}

# Reads SHA256SUMS of the release (a folder file or a download into the cache) as a name -> hash table.
function Get-ReleaseSums {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        $Paths,

        [string]$BaseUrl = '',

        [string]$Version = 'latest'
    )

    $source = Resolve-AssetSource -Name 'SHA256SUMS' -BaseUrl $BaseUrl -Version $Version
    if ($source.Kind -eq 'file') {
        $sumsPath = $source.Location
        if (-not (Test-Path -LiteralPath $sumsPath -PathType Leaf)) {
            throw ('SHA256SUMS was not found at ' + $sumsPath)
        }
    }
    else {
        if (-not (Test-Path -LiteralPath $Paths.CacheDir -PathType Container)) {
            New-Item -ItemType Directory -Path $Paths.CacheDir -Force | Out-Null
        }

        # SHA256SUMS has no hash of its own to check against; it comes from the same TLS-protected origin as the archive.
        $sumsPath = Join-Path $Paths.CacheDir 'SHA256SUMS'
        if (Test-Path -LiteralPath $sumsPath) {
            Remove-Item -LiteralPath $sumsPath -Force
        }

        Invoke-Download -Url $source.Location -OutFile $sumsPath
    }

    return (Read-Sums -Text ([System.IO.File]::ReadAllText($sumsPath)))
}

# Reads release-info.json from the installed app and insists on the two values the run needs.
function Read-ReleaseInfo {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw ('release-info.json is missing from the installed app: ' + $Path)
    }

    $info = [System.IO.File]::ReadAllText($Path) | ConvertFrom-Json
    foreach ($name in @('version', 'nodeVersion')) {
        $property = $info.PSObject.Properties[$name]
        if (($null -eq $property) -or [string]::IsNullOrWhiteSpace([string]$property.Value)) {
            throw ('release-info.json has no "' + $name + '" value: ' + $Path)
        }
    }

    return $info
}

# Applies the changes to the in-memory state values and writes the state file only when a value differs,
# so a run with nothing new never touches install-state.json.
function Update-InstallState {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter(Mandatory)]
        [System.Collections.Specialized.OrderedDictionary]$Values,

        [Parameter(Mandatory)]
        [System.Collections.IDictionary]$Changes
    )

    $dirty = $false
    foreach ($key in @($Changes.Keys)) {
        $new = [string]$Changes[$key]
        if ((-not $Values.Contains($key)) -or ([string]$Values[$key] -cne $new)) {
            $Values[$key] = $new
            $dirty = $true
        }
    }

    if ($dirty) {
        Write-InstallState -Path $Path -State $Values
    }
}

# The last lines of a successful run: where Claude lives and how to sign in. Never fails the install.
function Show-ClaudeSignIn {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        $Paths
    )

    $claude = Join-Path $Paths.InstallRoot 'tools\acp\node_modules\@anthropic-ai\claude-agent-sdk-win32-x64\claude.exe'
    try {
        if (Test-InteractiveSession) {
            $status = -1
            try {
                $status = Invoke-ClaudeAuthStatus -ClaudePath $claude
            }
            catch {
                # Any failure of the check means "not signed in"; the note below tells the user what to do.
                $status = -1
            }

            if ($status -eq 0) {
                Write-Host 'Claude: signed in.'
                return
            }

            Write-Host 'Claude is not signed in yet. Sign in once, then start Agency Huddle:'
        }
        else {
            # Nobody to ask and no way to check without a terminal: only say how.
            Write-Host 'To use Claude, sign in once before you start Agency Huddle:'
        }

        Write-Host ('  "' + $claude + '" auth login')
        Write-Host '(This opens your browser. Add --console to use API billing instead of a Claude subscription.)'
    }
    catch {
        # The note is advice only: a host that cannot print it must not fail a finished install.
        $null = $_
    }
}

# Steps 1 to 5 of Spec section 6 (release, app, node, adapter, launcher) plus summary, sign-in note and launch.
# Returns the exit code; it prints [!!] with a fix line instead of throwing.
function Invoke-InstallSteps {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        $Paths,

        [string]$BaseUrl = '',

        [string]$Version = 'latest',

        [bool]$Force = $false,

        [bool]$NoLaunch = $false,

        [bool]$NoShellIntegration = $false
    )

    $step = 'release'
    try {
        $archiveName = 'huddle-' + $Paths.Rid + '.zip'
        $sums = Get-ReleaseSums -Paths $Paths -BaseUrl $BaseUrl -Version $Version
        $expected = Get-ExpectedHash -Sums $sums -Name $archiveName
        $state = Read-InstallState -Path $Paths.StatePath
        $values = [ordered]@{}
        if ($null -ne $state) {
            foreach ($key in $script:StateKeys) {
                $property = $state.PSObject.Properties[$key]
                if ($null -ne $property) {
                    $values[$key] = [string]$property.Value
                }
            }
        }

        # Each Install-* function decides for itself whether it is satisfied, stops the app only when a
        # change is certain, and never writes state. The key of a step is written right after that step.
        $step = 'app'
        # A satisfied app step is decided from the SHA256SUMS line alone: the zip is not downloaded.
        if ((-not $Force) -and (Test-StepSatisfied -State $state -Key 'appSha256' -Expected $expected) -and (Test-Path -LiteralPath $Paths.ExePath -PathType Leaf)) {
            Write-Step -Status 'skip' -Name 'app' -Detail 'already installed'
            $appChanged = $false
        }
        else {
            $source = Resolve-AssetSource -Name $archiveName -BaseUrl $BaseUrl -Version $Version
            $archive = Get-VerifiedAsset -Name $archiveName -ExpectedSha256 $expected -Source $source -CacheDir $Paths.CacheDir
            $appChanged = [bool](Install-App -Paths $Paths -Archive $archive -State $state -Force:$Force)
        }

        $info = Read-ReleaseInfo -Path $Paths.ReleaseInfoPath
        Update-InstallState -Path $Paths.StatePath -Values $values -Changes ([ordered]@{ version = [string]$info.version; appSha256 = $expected })

        $step = 'node'
        $nodeChanged = [bool](Install-Node -Paths $Paths -NodeVersion ([string]$info.nodeVersion) -State $state -Force:$Force)
        Update-InstallState -Path $Paths.StatePath -Values $values -Changes ([ordered]@{ nodeVersion = [string]$info.nodeVersion })

        $step = 'adapter'
        $adapterChanged = [bool](Install-Adapter -Paths $Paths -State $state -Force:$Force)
        $lockSha = Get-FileSha256 -Path (Join-Path $Paths.InstallRoot 'tools\acp\package-lock.json')
        Update-InstallState -Path $Paths.StatePath -Values $values -Changes ([ordered]@{ lockSha256 = $lockSha })

        $step = 'launcher'
        $launcherChanged = [bool](Install-Launcher -Paths $Paths -NoShellIntegration:$NoShellIntegration)
    }
    catch {
        $message = ([string]$_.Exception.Message).Trim()
        $fix = 'Run the installer again: it resumes where it stopped. If it keeps failing, report the lines above.'
        $marker = $message.LastIndexOf('fix:', [System.StringComparison]::OrdinalIgnoreCase)
        if ($marker -ge 0) {
            $fix = $message.Substring($marker + 4).Trim()
            $message = $message.Substring(0, $marker).Trim()
        }

        Write-Step -Status '!!' -Name $step -Detail $message -Fix $fix
        return $script:ExitCodes.StepFailed
    }

    $installedVersion = [string]$info.version
    if ($appChanged -or $nodeChanged -or $adapterChanged -or $launcherChanged) {
        Write-Host ('Agency Huddle ' + $installedVersion + ' installed.')
    }
    else {
        Write-Host ('Already up to date (' + $installedVersion + ').')
    }

    Write-Host 'Start it from the Start menu (Agency Huddle), or open a new terminal and run: huddle'
    Show-ClaudeSignIn -Paths $Paths

    if (-not $NoLaunch) {
        try {
            Start-HuddleLauncher -LauncherPath $Paths.LauncherPath
            Write-Step -Status 'ok' -Name 'launch' -Detail 'started Agency Huddle'
        }
        catch {
            Write-Step -Status '!!' -Name 'launch' -Detail ([string]$_.Exception.Message) -Fix 'Start Agency Huddle from the Start menu, or open a new terminal and run: huddle'
        }
    }

    return $script:ExitCodes.Success
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
        [switch]$NoLaunch,
        [switch]$NoShellIntegration
    )

    # Function scope: these end with Invoke-Main, and the helpers it calls inherit them.
    Set-StrictMode -Version Latest
    $ErrorActionPreference = 'Stop'

    $releaser = $null
    $failureCode = $script:ExitCodes.BadUsage
    try {
        $options = Resolve-Options -Bound $PSBoundParameters
        $Version = ([string]$options.Version).Trim()
        $BaseUrl = $options.BaseUrl
        $InstallRoot = $options.InstallRoot
        $DataRoot = $options.DataRoot
        $CheckOnly = $options.CheckOnly
        $Uninstall = $options.Uninstall
        $PurgeData = $options.PurgeData
        $Force = $options.Force
        $NoLaunch = $options.NoLaunch
        $NoShellIntegration = $options.NoShellIntegration

        # The version is spliced into the download URL, so only a release tag shape gets through.
        if (-not (($Version -eq 'latest') -or ($Version -cmatch '^v?[0-9]+\.[0-9]+\.[0-9]+([-+.][0-9A-Za-z.]+)*\z'))) {
            Write-Step -Status '!!' -Name 'usage' -Detail ('Not a valid version: "' + $Version + '"') -Fix 'use latest or a version such as v1.2.3'
            return $script:ExitCodes.BadUsage
        }

        if ($BaseUrl -and ($BaseUrl -notmatch '^https://') -and (-not (Test-Path -LiteralPath $BaseUrl -PathType Container))) {
            Write-Step -Status '!!' -Name 'usage' -Detail ('BaseUrl is neither an https URL nor an existing folder: ' + $BaseUrl) -Fix 'Pass -BaseUrl https://... or the path of an existing folder.'
            return $script:ExitCodes.BadUsage
        }

        if ($PurgeData -and (-not $Uninstall)) {
            Write-Step -Status '!!' -Name 'usage' -Detail '-PurgeData only works together with -Uninstall.' -Fix 'Run again with -Uninstall -PurgeData to remove the program and your data, or drop -PurgeData.'
            return $script:ExitCodes.BadUsage
        }

        $paths = Resolve-InstallPaths -InstallRoot $InstallRoot -DataRoot $DataRoot

        # The defaults are siblings under different parents; any nesting of one root in the other is refused.
        if (-not $Uninstall) {
            $overlap = Get-RootOverlap -Paths $paths
            if ($null -ne $overlap) {
                Write-Step -Status '!!' -Name 'usage' -Detail $overlap -Fix 'pass -InstallRoot and -DataRoot as separate folders, neither inside the other'
                return $script:ExitCodes.BadUsage
            }
        }

        # From here an unexpected error is a failed step, not a usage problem.
        $failureCode = $script:ExitCodes.StepFailed
        $releaser = Enter-InstallerMutex
        if ($null -eq $releaser) {
            Write-Step -Status '!!' -Name 'installer' -Detail 'another Agency Huddle installer is already running.' -Fix 'Wait for it to finish, then run this command again.'
            return $script:ExitCodes.StepFailed
        }

        # An uninstall works offline and on a machine that no longer meets the install prerequisites.
        if ($Uninstall) {
            $uninstallCode = @(Invoke-Uninstall -Paths $paths -PurgeData:$PurgeData -Force:$Force)
            return [int]$uninstallCode[-1]
        }

        $items = @(Test-Prerequisites -Paths $paths -BaseUrl $BaseUrl)
        Show-Prerequisites -Items $items

        if (@($items | Where-Object { -not $_.Ok }).Count -gt 0) {
            return $script:ExitCodes.PrerequisitesUnmet
        }

        if ($CheckOnly) {
            return $script:ExitCodes.Success
        }

        $installCode = @(Invoke-InstallSteps -Paths $paths -BaseUrl $BaseUrl -Version $Version -Force ([bool]$Force) -NoLaunch ([bool]$NoLaunch) -NoShellIntegration ([bool]$NoShellIntegration))
        return [int]$installCode[-1]
    }
    catch {
        $message = ([string]$_.Exception.Message).Trim()
        $fix = 'Run the installer again. If it keeps failing, report the line above.'
        $marker = $message.LastIndexOf('fix:', [System.StringComparison]::OrdinalIgnoreCase)
        if ($marker -ge 0) {
            $fix = $message.Substring($marker + 4).Trim()
            $message = $message.Substring(0, $marker).Trim()
        }

        Write-Step -Status '!!' -Name 'installer' -Detail $message -Fix $fix
        return $failureCode
    }
    finally {
        if ($null -ne $releaser) {
            & $releaser
        }
    }
}

if (-not $env:HUDDLE_INSTALL_NO_MAIN) {
    # Under iex (irm | iex) or as a scriptblock, exit would close the user's PowerShell window.
    # No top-level variable is created here: under iex it would persist in the user's session.
    # {}.File is the script path when this file runs as a file and empty under iex, even when the
    # iex call sits inside another script (where $PSCommandPath would be that script's path).
    if ({}.File) {
        exit (Invoke-Main @PSBoundParameters)
    }

    try {
        $global:LASTEXITCODE = Invoke-Main @PSBoundParameters
    }
    finally {
        # Nothing this script defined may stay in the user's session: every function that was not there
        # before the first line ran, then the constants (at the prompt $script: is the global scope) and
        # the snapshot itself.
        # Functions that belong to a module (autoloaded while this ran) are not ours and stay.
        Get-ChildItem -Path Function: | Where-Object { (-not $_.ModuleName) -and ($huddleFunctionSnapshot -notcontains $_.Name) } | Remove-Item -Force -ErrorAction SilentlyContinue

        Remove-Variable -Name HuddleRepo, RepoPlaceholder, ExitCodes, MinWindowsBuild, MinFreeMb, NetworkHosts, StateKeys, huddleFunctionSnapshot -Scope Script -ErrorAction SilentlyContinue
    }
}
