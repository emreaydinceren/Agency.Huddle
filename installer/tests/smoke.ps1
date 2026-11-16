<#
.SYNOPSIS
    End-to-end smoke test of the Windows installer against a real release (plan task 39).
.DESCRIPTION
    Builds <ReleaseDir> with installer/build-release.sh when huddle-win-x64.zip is not there, then installs it
    into a temp sandbox (-InstallRoot / -DataRoot under the sandbox, -NoShellIntegration so the real user PATH
    and Start menu are never edited), starts the app on 127.0.0.1 and walks Spec section 6 and 8:
    install, check, idempotent rerun, start, second start, rerun while running, upgrade over the running app,
    stop, uninstall twice. Prints "STEP n: ..." lines and exits 1 naming the failing step. Everything is
    restored and every process under the sandbox is ended in the finally block.
    ASCII only; runs under Windows PowerShell 5.1:
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File installer/tests/smoke.ps1 -ReleaseDir <dir>
.PARAMETER ReleaseDir
    Folder with huddle-win-x64.zip and SHA256SUMS. Built when the zip is missing.
.PARAMETER ExpectedRepo
    Optional owner/repo the release workflow stamped into install.ps1 (the workflow passes its own repository).
    When given, the stamped copy must contain it and must no longer contain the placeholder.
.PARAMETER KeepSandbox
    Keep the temp sandbox for inspection instead of deleting it.
.NOTES
    The installer under test is <ReleaseDir>\install.ps1 (the stamped copy users download) when it exists.
    Otherwise a stamped copy is built in the sandbox from installer\install.ps1 with the same replacement
    the release workflow makes.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ReleaseDir,

    [string]$ExpectedRepo = '',

    [switch]$KeepSandbox
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$script:Step = 0
$script:RepoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$script:InstallPs1 = Join-Path $script:RepoRoot 'installer\install.ps1'
$script:LocalStampRepo = 'emreaydinceren/Agency.Huddle'
$script:Sandbox = ''
$script:SandboxLeaf = ''
$script:InstallRoot = ''
$script:DataRoot = ''
$script:Launcher = ''
$script:InstanceFile = ''
$script:LogDir = ''
$script:Version1 = '0.0.1-smoke'
$script:Version2 = '0.0.2-smoke'
$script:ShortcutPath = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Agency Huddle.lnk'
$script:PowerShellExe = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
$script:StepNames = @('app', 'node', 'adapter', 'launcher', 'shortcut', 'path', 'stop')

function Write-Info {
    param([string]$Text)
    Write-Host ('    ' + $Text)
}

function Start-SmokeStep {
    param([int]$Number, [string]$Text)
    $script:Step = $Number
    Write-Host ('STEP ' + $Number + ': ' + $Text)
}

function Stop-Smoke {
    param([string]$Message)
    throw $Message
}

function ConvertTo-Quoted {
    param([string]$Text)
    return '"' + $Text + '"'
}

# Runs a program with redirected output and a timeout. Returns Code, Lines, TimedOut.
function Invoke-Child {
    param(
        [string]$File,
        [string]$Arguments,
        [string]$WorkingDirectory,
        [int]$TimeoutSec = 900
    )

    $stamp = [guid]::NewGuid().ToString('N')
    $outFile = Join-Path $script:LogDir ('out-' + $stamp + '.txt')
    $errFile = Join-Path $script:LogDir ('err-' + $stamp + '.txt')
    $splat = @{
        FilePath               = $File
        WorkingDirectory       = $WorkingDirectory
        NoNewWindow            = $true
        PassThru               = $true
        RedirectStandardOutput = $outFile
        RedirectStandardError  = $errFile
    }
    if ($Arguments) {
        $splat['ArgumentList'] = $Arguments
    }

    $process = Start-Process @splat
    $null = $process.Handle
    $timedOut = $false
    if (-not $process.WaitForExit($TimeoutSec * 1000)) {
        $timedOut = $true
        & taskkill.exe /T /F /PID $process.Id 2>&1 | Out-Null
        $process.WaitForExit(10000) | Out-Null
    }

    $process.WaitForExit()
    $code = -1
    if (-not $timedOut) {
        $code = [int]$process.ExitCode
    }

    $lines = @()
    foreach ($file in @($outFile, $errFile)) {
        if (Test-Path -LiteralPath $file) {
            $lines += @(Get-Content -LiteralPath $file)
        }
    }

    return [pscustomobject]@{ Code = $code; Lines = $lines; TimedOut = $timedOut }
}

# Runs a bash script through Git Bash from the repo root. Returns the Invoke-Child result.
function Invoke-Sh {
    param([string[]]$ShArguments)
    $sh = (Get-Command sh -ErrorAction Stop).Source
    $shDir = Split-Path -Parent $sh
    $savedPath = $env:PATH
    try {
        $env:PATH = $shDir + ';' + $env:PATH
        $parts = @()
        foreach ($argument in $ShArguments) {
            $parts += ConvertTo-Quoted ($argument -replace '\\', '/')
        }

        return Invoke-Child -File $sh -Arguments ($parts -join ' ') -WorkingDirectory $script:RepoRoot -TimeoutSec 1500
    }
    finally {
        $env:PATH = $savedPath
    }
}

function Invoke-Installer {
    param(
        [string]$BaseUrl = '',
        [string[]]$Extra = @()
    )

    $arguments = '-NoProfile -ExecutionPolicy Bypass -File ' + (ConvertTo-Quoted $script:InstallPs1)
    if ($BaseUrl) {
        $arguments += ' -BaseUrl ' + (ConvertTo-Quoted $BaseUrl)
    }

    $arguments += ' -InstallRoot ' + (ConvertTo-Quoted $script:InstallRoot)
    $arguments += ' -DataRoot ' + (ConvertTo-Quoted $script:DataRoot)
    $arguments += ' -NoShellIntegration'
    foreach ($item in $Extra) {
        $arguments += ' ' + $item
    }

    $result = Invoke-Child -File $script:PowerShellExe -Arguments $arguments -WorkingDirectory $script:Sandbox
    foreach ($row in @(Get-StepRows $result.Lines)) {
        Write-Info $row.Line
    }

    return $result
}

# Chooses the installer under test and proves it is a stamped copy: <ReleaseFolder>\install.ps1 when it
# exists (what users download), else a copy built in the sandbox with the workflow's replacement. Sets
# $script:InstallPs1. The stamped file must not hold the placeholder and must hold the repository; the
# release URL it builds for a bare version must name that repository and the v-prefixed tag.
function Select-StampedInstaller {
    param([string]$ReleaseFolder, [string]$Expected)

    $placeholder = '__HUDDLE_' + 'REPO__'
    $stamped = Join-Path $ReleaseFolder 'install.ps1'
    $repo = $Expected
    if (Test-Path -LiteralPath $stamped -PathType Leaf) {
        Write-Info ('installer under test: the stamped copy in the release folder, ' + $stamped)
    }
    else {
        $repo = $script:LocalStampRepo
        if ($Expected) {
            $repo = $Expected
        }

        $stampDir = Join-Path $script:Sandbox 'stamped'
        New-Item -ItemType Directory -Path $stampDir -Force | Out-Null
        $stamped = Join-Path $stampDir 'install.ps1'
        $text = [System.IO.File]::ReadAllText($script:InstallPs1)
        [System.IO.File]::WriteAllText($stamped, $text.Replace($placeholder, $repo), [System.Text.Encoding]::ASCII)
        Write-Info ('installer under test: no install.ps1 in the release folder, so a stamped copy of installer\install.ps1 (repository ' + $repo + ') at ' + $stamped)
    }

    $stampedText = [System.IO.File]::ReadAllText($stamped)
    if ($stampedText.Contains($placeholder)) {
        Stop-Smoke ('the stamped installer still contains the repository placeholder: ' + $stamped)
    }

    if ($repo -and (-not $stampedText.Contains($repo))) {
        Stop-Smoke ('the stamped installer does not contain the repository ' + $repo + ': ' + $stamped)
    }

    $probe = Join-Path $script:LogDir 'probe-asset.ps1'
    $probeText = "param([string]`$Installer)`r`n`$env:HUDDLE_INSTALL_NO_MAIN = '1'`r`nRemove-Item Env:HUDDLE_REPO -ErrorAction SilentlyContinue`r`n. `$Installer`r`nWrite-Output ('LOCATION:' + (Resolve-AssetSource -Name 'x.zip' -Version '1.0.0').Location)`r`n"
    [System.IO.File]::WriteAllText($probe, $probeText, [System.Text.Encoding]::ASCII)
    $result = Invoke-Child -File $script:PowerShellExe -Arguments ('-NoProfile -ExecutionPolicy Bypass -File ' + (ConvertTo-Quoted $probe) + ' -Installer ' + (ConvertTo-Quoted $stamped)) -WorkingDirectory $script:Sandbox -TimeoutSec 120
    $location = @($result.Lines | Where-Object { $_ -like 'LOCATION:*' } | Select-Object -First 1)
    if ($location.Count -eq 0) {
        Stop-Smoke ('the stamped installer could not build a release URL: ' + ((@($result.Lines) | Select-Object -Last 4) -join ' | '))
    }

    $url = ([string]$location[0]).Substring('LOCATION:'.Length)
    $pattern = '^https://github\.com/[^/]+/[^/]+/releases/download/v1\.0\.0/x\.zip$'
    if ($repo) {
        $pattern = '^' + [regex]::Escape('https://github.com/' + $repo + '/releases/download/v1.0.0/x.zip') + '$'
    }

    if ($url -notmatch $pattern) {
        Stop-Smoke ('the stamped installer built the unexpected release URL ' + $url)
    }

    Write-Info ('placeholder replaced; a bare version resolves to ' + $url)
    $script:InstallPs1 = $stamped
}

# The "[status] name - detail" lines of the installer output, as objects.
function Get-StepRows {
    param([string[]]$Lines)
    $rows = @()
    foreach ($line in @($Lines)) {
        if ($line -match '^\[(ok|skip|\.\.|!!)\]\s+(\S+)') {
            $rows += [pscustomobject]@{ Status = $Matches[1]; Name = $Matches[2]; Line = $line }
        }
    }

    return $rows
}

function Get-StepRow {
    param([string[]]$Lines, [string]$Name)
    # The last line of a step is its outcome ("[..] app - extracting" comes before "[ok] app - installed").
    $found = $null
    foreach ($row in @(Get-StepRows $Lines)) {
        if ($row.Name -ceq $Name -and $script:StepNames -contains $row.Name) {
            $found = $row
        }
    }

    return $found
}

# Every installer step line is [skip], and app, node, adapter and launcher were all reported.
function Assert-AllSkipped {
    param([string[]]$Lines)
    foreach ($required in @('app', 'node', 'adapter', 'launcher')) {
        $row = Get-StepRow -Lines $Lines -Name $required
        if ($null -eq $row) {
            Stop-Smoke ('the installer printed no "' + $required + '" step line')
        }
    }

    foreach ($row in @(Get-StepRows $Lines)) {
        if (($script:StepNames -contains $row.Name) -and ($row.Status -ne 'skip')) {
            Stop-Smoke ('expected every step to be [skip], got: ' + $row.Line)
        }
    }
}

function Get-Snapshot {
    param([string]$Root)
    $rows = New-Object 'System.Collections.Generic.List[string]'
    foreach ($item in @(Get-ChildItem -LiteralPath $Root -Recurse -Force)) {
        # NTFS refreshes a folder's own timestamp lazily (npm ci's folders changed between two reads with
        # nothing running), so a folder counts by path only: any file added or removed still shows as a path.
        if ($item.PSIsContainer) {
            $rows.Add($item.FullName + '|dir')
        }
        else {
            $rows.Add($item.FullName + '|' + $item.Length + '|' + $item.LastWriteTimeUtc.Ticks)
        }
    }

    $rows.Sort([System.StringComparer]::Ordinal)
    return , $rows.ToArray()
}

function Assert-SameSnapshot {
    param([string[]]$Before, [string[]]$After)
    $difference = @(Compare-Object -ReferenceObject $Before -DifferenceObject $After)
    if ($difference.Count -gt 0) {
        $shown = @($difference | Select-Object -First 8 | ForEach-Object { $_.SideIndicator + ' ' + $_.InputObject })
        Stop-Smoke ('the install root changed on a rerun (' + $difference.Count + ' differences): ' + ($shown -join ' ; '))
    }
}

# Processes whose executable lives under the sandbox. Name is e.g. 'Huddle.App.exe', or '' for all.
function Get-SandboxProcesses {
    param([string]$Name = '')
    if (-not $script:SandboxLeaf) {
        return @()
    }

    $needle = '\' + $script:SandboxLeaf + '\'
    $found = @()
    foreach ($process in @(Get-CimInstance -ClassName Win32_Process)) {
        $path = [string]$process.ExecutablePath
        if ($path -and ($path.IndexOf($needle, [System.StringComparison]::OrdinalIgnoreCase) -ge 0)) {
            if ((-not $Name) -or ([string]$process.Name -ieq $Name)) {
                $found += $process
            }
        }
    }

    return $found
}

function Test-ProcessAlive {
    param([int]$ProcessId)
    return ($null -ne (Get-Process -Id $ProcessId -ErrorAction SilentlyContinue))
}

function Wait-ProcessGone {
    param([int]$ProcessId, [int]$TimeoutSec)
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    while ((Get-Date) -lt $deadline) {
        if (-not (Test-ProcessAlive $ProcessId)) {
            return $true
        }

        Start-Sleep -Milliseconds 300
    }

    return (-not (Test-ProcessAlive $ProcessId))
}

function Stop-SandboxProcesses {
    foreach ($process in @(Get-SandboxProcesses)) {
        try {
            Stop-Process -Id ([int]$process.ProcessId) -Force -ErrorAction Stop
        }
        catch {
            Write-Info ('could not end process ' + $process.ProcessId + ': ' + $_.Exception.Message)
        }
    }
}

# Starts huddle.cmd hidden (OpenBrowser=false is inherited from the environment).
function Start-Launcher {
    $process = Start-Process -FilePath $script:Launcher -WindowStyle Hidden -PassThru -WorkingDirectory $script:Sandbox
    $null = $process.Handle
    return $process
}

function Read-Instance {
    if (-not (Test-Path -LiteralPath $script:InstanceFile -PathType Leaf)) {
        return $null
    }

    try {
        $json = Get-Content -LiteralPath $script:InstanceFile -Raw | ConvertFrom-Json
        if ($json.url -and $json.pid) {
            return [pscustomobject]@{ Url = [string]$json.url; Pid = [int]$json.pid }
        }
    }
    catch {
        return $null
    }

    return $null
}

# Waits for instance.json, then GET /health = 200. Returns the instance.
function Wait-ForApp {
    param([System.Diagnostics.Process]$Launcher, [int]$TimeoutSec = 90)
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    $instance = $null
    while ((Get-Date) -lt $deadline) {
        $instance = Read-Instance
        if ($null -ne $instance) {
            break
        }

        if ($Launcher.HasExited) {
            Stop-Smoke ('the launcher exited with code ' + $Launcher.ExitCode + ' before instance.json appeared')
        }

        Start-Sleep -Milliseconds 500
    }

    if ($null -eq $instance) {
        Stop-Smoke ('instance.json did not appear within ' + $TimeoutSec + ' s')
    }

    Write-Info ('instance: ' + $instance.Url + ' pid ' + $instance.Pid)
    $status = 0
    for ($attempt = 0; $attempt -lt 10; $attempt++) {
        try {
            $response = Invoke-WebRequest -Uri ($instance.Url.TrimEnd('/') + '/health') -UseBasicParsing -TimeoutSec 15
            $status = [int]$response.StatusCode
        }
        catch {
            $status = 0
        }

        if ($status -eq 200) {
            break
        }

        Start-Sleep -Seconds 1
    }

    if ($status -ne 200) {
        Stop-Smoke ('GET /health returned ' + $status + ', expected 200')
    }

    Write-Info 'GET /health = 200'
    return $instance
}

function Get-RawUserPath {
    $key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Environment')
    try {
        if ($null -eq $key) {
            return [pscustomobject]@{ Value = $null; Kind = $null }
        }

        $value = $key.GetValue('Path', $null, [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames)
        $kind = $null
        if ($null -ne $value) {
            $kind = $key.GetValueKind('Path')
        }

        return [pscustomobject]@{ Value = $value; Kind = $kind }
    }
    finally {
        if ($null -ne $key) {
            $key.Close()
        }
    }
}

function Get-InstalledVersion {
    $statePath = Join-Path $script:InstallRoot 'install-state.json'
    if (-not (Test-Path -LiteralPath $statePath -PathType Leaf)) {
        return ''
    }

    return [string]((Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json).version)
}

function Write-FailureContext {
    try {
        if ($script:DataRoot -and (Test-Path -LiteralPath $script:DataRoot)) {
            $log = Get-ChildItem -LiteralPath $script:DataRoot -Recurse -Force -Filter '*.log' -ErrorAction SilentlyContinue |
                Sort-Object LastWriteTime -Descending | Select-Object -First 1
            if ($null -ne $log) {
                Write-Host ('  last lines of ' + $log.FullName + ':')
                foreach ($line in @(Get-Content -LiteralPath $log.FullName -Tail 15)) {
                    Write-Host ('    ' + $line)
                }
            }
        }
    }
    catch {
        Write-Host ('  (could not read the app log: ' + $_.Exception.Message + ')')
    }
}

$failed = $false
$savedOpenBrowser = [Environment]::GetEnvironmentVariable('Team__Desktop__OpenBrowser', 'Process')
$pathBefore = Get-RawUserPath
$shortcutExistedBefore = Test-Path -LiteralPath $script:ShortcutPath
$shortcutStampBefore = $null
if ($shortcutExistedBefore) {
    $shortcutStampBefore = (Get-Item -LiteralPath $script:ShortcutPath).LastWriteTimeUtc.Ticks
}

try {
    $ReleaseDir = [System.IO.Path]::GetFullPath($ReleaseDir)
    $script:SandboxLeaf = 'hsmoke-' + [guid]::NewGuid().ToString('N').Substring(0, 8)
    $script:Sandbox = Join-Path ([System.IO.Path]::GetTempPath()) $script:SandboxLeaf
    $script:InstallRoot = Join-Path $script:Sandbox 'i'
    $script:DataRoot = Join-Path $script:Sandbox 'd'
    $script:LogDir = Join-Path $script:Sandbox 'log'
    $script:Launcher = Join-Path $script:InstallRoot 'huddle.cmd'
    $script:InstanceFile = Join-Path $script:DataRoot 'data\instance.json'
    New-Item -ItemType Directory -Path $script:LogDir -Force | Out-Null
    Write-Host ('Sandbox: ' + $script:Sandbox)
    $env:Team__Desktop__OpenBrowser = 'false'

    $others = @(Get-Process -Name 'Huddle.App' -ErrorAction SilentlyContinue)
    if ($others.Count -gt 0) {
        Write-Host ('Note: ' + $others.Count + ' other Huddle.App process(es) are running outside the sandbox; they are left alone.')
    }

    # STEP 0: the release folder.
    Start-SmokeStep 0 ('release folder ' + $ReleaseDir)
    $zip1 = Join-Path $ReleaseDir 'huddle-win-x64.zip'
    if (-not (Test-Path -LiteralPath $zip1 -PathType Leaf)) {
        Write-Info 'building it with installer/build-release.sh (dotnet publish; this takes a few minutes)'
        $build = Invoke-Sh @('installer/build-release.sh', '--version', $script:Version1, '--rid', 'win-x64', '--out', $ReleaseDir)
        if ($build.Code -ne 0) {
            Stop-Smoke ('build-release.sh failed with exit code ' + $build.Code + ': ' + ((@($build.Lines) | Select-Object -Last 8) -join ' | '))
        }
    }

    if (-not (Test-Path -LiteralPath (Join-Path $ReleaseDir 'SHA256SUMS') -PathType Leaf)) {
        $sums = Invoke-Sh @('installer/make-sums.sh', $ReleaseDir, 'huddle-win-x64.zip')
        if ($sums.Code -ne 0) {
            Stop-Smoke ('make-sums.sh failed with exit code ' + $sums.Code + ': ' + ((@($sums.Lines) | Select-Object -Last 5) -join ' | '))
        }
    }

    if (-not (Test-Path -LiteralPath $zip1 -PathType Leaf)) {
        Stop-Smoke ('huddle-win-x64.zip is not in ' + $ReleaseDir)
    }

    Write-Info ('release zip: ' + [math]::Round((Get-Item -LiteralPath $zip1).Length / 1MB, 1) + ' MB')
    Select-StampedInstaller -ReleaseFolder $ReleaseDir -Expected $ExpectedRepo

    # STEP 1: install.
    Start-SmokeStep 1 'install.ps1 into the sandbox (downloads Node and runs npm ci)'
    $result = Invoke-Installer -BaseUrl $ReleaseDir -Extra @('-NoLaunch')
    if ($result.Code -ne 0) {
        Stop-Smoke ('install.ps1 exited with ' + $result.Code + '; last lines: ' + ((@($result.Lines) | Select-Object -Last 6) -join ' | '))
    }

    if (-not (Test-Path -LiteralPath $script:Launcher -PathType Leaf)) {
        Stop-Smoke 'huddle.cmd was not written'
    }

    # STEP 2: check only.
    Start-SmokeStep 2 'install.ps1 -CheckOnly'
    $result = Invoke-Installer -BaseUrl $ReleaseDir -Extra @('-CheckOnly')
    if ($result.Code -ne 0) {
        Stop-Smoke ('-CheckOnly exited with ' + $result.Code)
    }

    # STEP 3: idempotent rerun.
    Start-SmokeStep 3 'rerun: every step is [skip] and the install root is byte-for-byte unchanged'
    $before = Get-Snapshot $script:InstallRoot
    $result = Invoke-Installer -BaseUrl $ReleaseDir -Extra @('-NoLaunch')
    if ($result.Code -ne 0) {
        Stop-Smoke ('the rerun exited with ' + $result.Code)
    }

    Assert-AllSkipped $result.Lines
    $after = Get-Snapshot $script:InstallRoot
    Assert-SameSnapshot -Before $before -After $after
    Write-Info ('snapshot identical (' + $after.Count + ' entries)')

    # STEP 4: start.
    Start-SmokeStep 4 'start huddle.cmd hidden; instance.json within 90 s; GET /health = 200'
    $launcher1 = Start-Launcher
    $instance1 = Wait-ForApp -Launcher $launcher1 -TimeoutSec 90
    if (-not (Test-ProcessAlive $instance1.Pid)) {
        Stop-Smoke ('instance.json names pid ' + $instance1.Pid + ' but no such process is alive')
    }

    # /health passes even when the app cannot find its wwwroot: every asset then answers 200 with an empty
    # body and the page renders unstyled. The launcher above runs from the sandbox, not the app folder.
    $cssUrl = $instance1.Url.TrimEnd('/') + '/_content/MudBlazor/MudBlazor.min.css'
    $cssBytes = 0
    try {
        $cssBytes = [int](Invoke-WebRequest -Uri $cssUrl -UseBasicParsing -TimeoutSec 15).RawContentLength
    }
    catch {
        $cssBytes = -1
    }

    if ($cssBytes -lt 1024) {
        Stop-Smoke ('GET ' + $cssUrl + ' returned ' + $cssBytes + ' bytes: the app is not serving its static files')
    }

    Write-Info ('static files served (MudBlazor.min.css ' + $cssBytes + ' bytes)')

    # STEP 5: second start hands off.
    Start-SmokeStep 5 'start huddle.cmd a second time: exits 0 within 20 s, exactly one Huddle.App'
    $launcher2 = Start-Launcher
    if (-not $launcher2.WaitForExit(20000)) {
        Stop-Smoke 'the second launcher did not exit within 20 s'
    }

    if ($launcher2.ExitCode -ne 0) {
        Stop-Smoke ('the second launcher exited with ' + $launcher2.ExitCode)
    }

    $apps = @(Get-SandboxProcesses 'Huddle.App.exe')
    if ($apps.Count -ne 1) {
        Stop-Smoke ('expected exactly one Huddle.App process under the sandbox, found ' + $apps.Count)
    }

    if ([int]$apps[0].ProcessId -ne $instance1.Pid) {
        Stop-Smoke ('the Huddle.App process is ' + $apps[0].ProcessId + ' but instance.json names ' + $instance1.Pid)
    }

    # STEP 6: rerun while running.
    Start-SmokeStep 6 'rerun the installer while the app runs: all [skip], same PID alive'
    $result = Invoke-Installer -BaseUrl $ReleaseDir -Extra @('-NoLaunch')
    if ($result.Code -ne 0) {
        Stop-Smoke ('the rerun exited with ' + $result.Code)
    }

    Assert-AllSkipped $result.Lines
    if (-not (Test-ProcessAlive $instance1.Pid)) {
        Stop-Smoke ('the app (pid ' + $instance1.Pid + ') was stopped by a no-change rerun')
    }

    $now = Read-Instance
    if (($null -eq $now) -or ($now.Pid -ne $instance1.Pid)) {
        Stop-Smoke 'instance.json changed during a no-change rerun'
    }

    # STEP 7: upgrade over the running app.
    Start-SmokeStep 7 ('install ' + $script:Version2 + ' over the running app: stop, replace, node and adapter [skip], data survives')
    $marker = Join-Path $script:DataRoot 'smoke-marker.txt'
    [System.IO.File]::WriteAllText($marker, 'keep me')
    $extract = Join-Path $script:Sandbox 'x'
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::ExtractToDirectory($zip1, $extract)
    $publish = Join-Path $extract 'app'
    if (-not (Test-Path -LiteralPath (Join-Path $publish 'Huddle.App.exe') -PathType Leaf)) {
        Stop-Smoke 'the first zip has no app\Huddle.App.exe to reuse as the second release'
    }

    $release2 = Join-Path $script:Sandbox 'rel2'
    $build = Invoke-Sh @('installer/build-release.sh', '--version', $script:Version2, '--rid', 'win-x64', '--out', $release2, '--publish-dir', $publish)
    if ($build.Code -ne 0) {
        Stop-Smoke ('building the second release failed with exit code ' + $build.Code + ': ' + ((@($build.Lines) | Select-Object -Last 8) -join ' | '))
    }

    $sums = Invoke-Sh @('installer/make-sums.sh', $release2, 'huddle-win-x64.zip')
    if ($sums.Code -ne 0) {
        Stop-Smoke ('make-sums.sh failed for the second release with exit code ' + $sums.Code)
    }

    $result = Invoke-Installer -BaseUrl $release2 -Extra @('-NoLaunch')
    if ($result.Code -ne 0) {
        Stop-Smoke ('the upgrade exited with ' + $result.Code + '; last lines: ' + ((@($result.Lines) | Select-Object -Last 6) -join ' | '))
    }

    $appRow = Get-StepRow -Lines $result.Lines -Name 'app'
    if (($null -eq $appRow) -or ($appRow.Status -ne 'ok')) {
        Stop-Smoke 'the upgrade did not report [ok] app'
    }

    foreach ($name in @('node', 'adapter')) {
        $row = Get-StepRow -Lines $result.Lines -Name $name
        if (($null -eq $row) -or ($row.Status -ne 'skip')) {
            Stop-Smoke ('the upgrade did not report [skip] ' + $name)
        }
    }

    if (Test-ProcessAlive $instance1.Pid) {
        Stop-Smoke ('the old app (pid ' + $instance1.Pid + ') is still running after the upgrade')
    }

    if (@(Get-SandboxProcesses 'Huddle.App.exe').Count -ne 0) {
        Stop-Smoke 'a Huddle.App process is still running after the upgrade'
    }

    if ((Get-InstalledVersion) -ne $script:Version2) {
        Stop-Smoke ('install-state.json says version "' + (Get-InstalledVersion) + '", expected ' + $script:Version2)
    }

    if (-not (Test-Path -LiteralPath $marker -PathType Leaf) -or ([System.IO.File]::ReadAllText($marker) -ne 'keep me')) {
        Stop-Smoke 'the marker file in the data root did not survive the upgrade'
    }

    # STEP 8: start again, then stop.
    Start-SmokeStep 8 'start again; huddle.cmd stop exits 0 and prints "Stopped."; no Huddle.App or node.exe is left'
    $launcher3 = Start-Launcher
    $instance3 = Wait-ForApp -Launcher $launcher3 -TimeoutSec 90
    $stop = Invoke-Child -File $script:Launcher -Arguments 'stop' -WorkingDirectory $script:Sandbox -TimeoutSec 120
    foreach ($line in @($stop.Lines)) {
        Write-Info ('stop> ' + $line)
    }

    if ($stop.Code -ne 0) {
        Stop-Smoke ('huddle.cmd stop exited with ' + $stop.Code)
    }

    if (-not (@($stop.Lines) -contains 'Stopped.')) {
        Stop-Smoke 'huddle.cmd stop did not print "Stopped."'
    }

    if (-not (Wait-ProcessGone -ProcessId $instance3.Pid -TimeoutSec 20)) {
        Stop-Smoke ('Huddle.App (pid ' + $instance3.Pid + ') is still running after stop')
    }

    if (Test-Path -LiteralPath $script:InstanceFile) {
        Stop-Smoke 'instance.json is still there after stop'
    }

    $nodes = @(Get-SandboxProcesses 'node.exe')
    if ($nodes.Count -gt 0) {
        Stop-Smoke ('KNOWN RISK A5 (adapter shutdown): ' + $nodes.Count + ' node.exe process(es) under the sandbox survive stop: ' + ((@($nodes | ForEach-Object { [string]$_.ProcessId })) -join ','))
    }

    # STEP 9: uninstall.
    Start-SmokeStep 9 '-Uninstall removes the install root and keeps the data'
    $result = Invoke-Installer -Extra @('-Uninstall')
    if ($result.Code -ne 0) {
        Stop-Smoke ('-Uninstall exited with ' + $result.Code)
    }

    if (Test-Path -LiteralPath $script:InstallRoot) {
        Stop-Smoke 'the install root still exists after -Uninstall'
    }

    if (-not (Test-Path -LiteralPath $marker -PathType Leaf)) {
        Stop-Smoke 'the data root was not kept by -Uninstall'
    }

    # STEP 10: uninstall again.
    Start-SmokeStep 10 '-Uninstall again exits 0'
    $result = Invoke-Installer -Extra @('-Uninstall')
    if ($result.Code -ne 0) {
        Stop-Smoke ('the second -Uninstall exited with ' + $result.Code)
    }
}
catch {
    $failed = $true
    Write-Host ('FAIL at STEP ' + $script:Step + ': ' + $_.Exception.Message) -ForegroundColor Red
    Write-FailureContext
}
finally {
    $leftovers = @(Get-SandboxProcesses)
    if ($leftovers.Count -gt 0) {
        Write-Host ('cleanup: ending ' + $leftovers.Count + ' process(es) under the sandbox')
        Stop-SandboxProcesses
        Start-Sleep -Seconds 2
    }

    [Environment]::SetEnvironmentVariable('Team__Desktop__OpenBrowser', $savedOpenBrowser, 'Process')

    # The opt-out must have kept the real user PATH and Start menu out of this. Restore them if it did not.
    $pathAfter = Get-RawUserPath
    if (($pathAfter.Value -cne $pathBefore.Value) -or ($pathAfter.Kind -ne $pathBefore.Kind)) {
        $failed = $true
        Write-Host 'FAIL: the user PATH changed during the smoke test; restoring it' -ForegroundColor Red
        $key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Environment', $true)
        try {
            if ($null -eq $pathBefore.Value) {
                $key.DeleteValue('Path', $false)
            }
            else {
                $key.SetValue('Path', $pathBefore.Value, $pathBefore.Kind)
            }
        }
        finally {
            $key.Close()
        }
    }

    $shortcutExistsNow = Test-Path -LiteralPath $script:ShortcutPath
    if ((-not $shortcutExistedBefore) -and $shortcutExistsNow) {
        $failed = $true
        Write-Host 'FAIL: a Start menu shortcut was created during the smoke test; deleting it' -ForegroundColor Red
        Remove-Item -LiteralPath $script:ShortcutPath -Force
    }
    elseif ($shortcutExistedBefore -and ((-not $shortcutExistsNow) -or ((Get-Item -LiteralPath $script:ShortcutPath).LastWriteTimeUtc.Ticks -ne $shortcutStampBefore))) {
        $failed = $true
        Write-Host 'FAIL: the existing Start menu shortcut was changed during the smoke test' -ForegroundColor Red
    }

    if ($script:Sandbox -and (Test-Path -LiteralPath $script:Sandbox)) {
        if ($KeepSandbox) {
            Write-Host ('Sandbox kept: ' + $script:Sandbox)
        }
        else {
            for ($attempt = 1; $attempt -le 5; $attempt++) {
                try {
                    Remove-Item -LiteralPath $script:Sandbox -Recurse -Force -ErrorAction Stop
                    break
                }
                catch {
                    if ($attempt -eq 5) {
                        Write-Host ('cleanup: could not delete ' + $script:Sandbox + ': ' + $_.Exception.Message)
                    }
                    else {
                        Start-Sleep -Seconds 2
                    }
                }
            }
        }
    }
}

if ($failed) {
    Write-Host 'SMOKE FAILED' -ForegroundColor Red
    exit 1
}

Write-Host 'SMOKE PASSED'
exit 0
