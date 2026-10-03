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
