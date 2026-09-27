<#
.SYNOPSIS
    Builds the solution and runs the Agency.Huddle web app.

.DESCRIPTION
    Builds Huddle.slnx, then starts src/Huddle.App (Blazor Server) and leaves it
    running until Ctrl+C. The build happens once: the app is started with
    --no-build so `dotnet run` does not repeat it.

    Development configuration sets Team:Acp:Enabled to true, so the app starts
    one node process per Persona before you type anything. Pass -NoAcp to open
    the app without them.

    On a fresh clone, that requires the ACP adapter installed under
    tools/acp/node_modules. This script checks for it (the same check
    DotAcpAgentHostFactory makes at Persona startup) and, when it is missing,
    runs tools/acp/install.ps1 automatically before building - so a blank
    checkout needs nothing but this script and node on PATH. Pass -NoAcp to
    skip that check entirely.

    See docs/AgencyTeam.md ("Build, test, run") for the commands this wraps.

.PARAMETER Port
    Port to listen on (default: 5100, matching launchSettings.json).

.PARAMETER NoAcp
    Set Team__Acp__Enabled=false for this run, so no node process is spawned per
    Persona. The variable is restored when the script exits.

.PARAMETER NoBuild
    Skip the build and run whatever is already compiled.

.PARAMETER Clean
    Delete the previous run's SQLite database (Team:DataDir/team.db, plus its -wal/-shm
    sidecar files) and every Persona file (Team:DataDir/Team:Acp:TeamsDir, i.e. App_Data/Teams)
    before building and starting the app. Opt-in and irreversible - there is no prompt, so only
    pass it when you mean to start from an empty team. Other App_Data content (prompts,
    appearance, avatars, rooms, logs, per-Persona work directories) is left alone.

.PARAMETER DryRun
    Print what would run, then exit without building or starting anything.

.EXAMPLE
    ./run.ps1
    ./run.ps1 -Port 5200
    ./run.ps1 -NoAcp
    ./run.ps1 -NoBuild
    ./run.ps1 -Clean
    ./run.ps1 -DryRun
#>
[CmdletBinding()]
param(
    [int]$Port = 5100,
    [switch]$NoAcp,
    [switch]$NoBuild,
    [switch]$Clean,
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$solution = Join-Path $scriptDir 'Huddle.slnx'
$appProject = Join-Path $scriptDir 'src/Huddle.App'
$url = "http://localhost:$Port"

# Matches TeamOptions.DataDir's default ("App_Data", resolved relative to src/Huddle.App - see
# launchSettings.json, which sets no workingDirectory) and AcpOptions.TeamsDir's default
# ("Teams"), and SqliteTeamDirectory's hardcoded "team.db". Not read from configuration: this
# script has no config binder, and every appsettings*.json in the repo leaves both defaults
# unchanged, so hardcoding the same defaults here is exactly as safe as the rest of this script's
# assumptions about a stock dev setup.
$dataDir = Join-Path $appProject 'App_Data'
$teamsDir = Join-Path $dataDir 'Teams'
$dbPath = Join-Path $dataDir 'team.db'

# Mirrors AdapterLocator.RelativeAdapterPath (src/Huddle.App/Acp/AdapterLocator.cs): the file
# DotAcpAgentHostFactory itself checks for before it will start a Persona. Checking the same path
# here, rather than merely "does tools/acp/node_modules exist", is what lets this script and the
# app agree on whether setup is done.
$acpToolsDir = Join-Path $scriptDir 'tools/acp'
$acpInstallScript = Join-Path $acpToolsDir 'install.ps1'
$acpAdapterEntryPoint = Join-Path $acpToolsDir 'node_modules/@agentclientprotocol/claude-agent-acp/dist/index.js'

if (-not (Test-Path $solution)) {
    Write-Host "Huddle.slnx not found at $solution." -ForegroundColor Yellow
    Write-Host "   Run this script from the repository root." -ForegroundColor Gray
    exit 1
}

# Acp:Enabled is on in Development and spawns one node process per Persona, so a
# missing node turns into a startup failure rather than a missing feature.
if (-not $NoAcp -and -not (Get-Command node -ErrorAction SilentlyContinue)) {
    Write-Host "node was not found on PATH." -ForegroundColor Yellow
    Write-Host "   Development config sets Team:Acp:Enabled=true, which starts one node process per Persona." -ForegroundColor Gray
    Write-Host "   Install node, or run without them: ./run.ps1 -NoAcp" -ForegroundColor Gray
    exit 1
}

# A blank clone has no tools/acp/node_modules, so the first Persona to start throws the
# InvalidOperationException this check exists to pre-empt. -NoAcp needs none of this: no Persona
# starts, so there is nothing to install.
$needsAcpSetup = -not $NoAcp -and -not (Test-Path $acpAdapterEntryPoint)

# -NoBuild runs whatever is compiled, so it needs something to be compiled.
$appAssembly = Join-Path $appProject 'bin/Debug/net10.0/Huddle.App.dll'
if ($NoBuild -and -not (Test-Path $appAssembly)) {
    Write-Host "-NoBuild was passed but $appAssembly does not exist." -ForegroundColor Yellow
    Write-Host '   Run ./run.ps1 once without -NoBuild to compile it first.' -ForegroundColor Gray
    exit 1
}

$buildCommand = "dotnet build `"$solution`""
$runCommand = "dotnet run --project `"$appProject`" --no-build --urls $url"

if ($DryRun) {
    Write-Host ''
    Write-Host 'Dry run - here is what I would do' -ForegroundColor Cyan
    if ($NoBuild) {
        Write-Host '   Build : skipped (-NoBuild)' -ForegroundColor Gray
    } else {
        Write-Host "   Build : $buildCommand" -ForegroundColor Gray
    }
    if ($NoAcp) {
        Write-Host '   Env   : Team__Acp__Enabled=false (no node process per Persona)' -ForegroundColor Gray
    }
    if ($needsAcpSetup) {
        Write-Host "   Setup : $acpInstallScript (ACP adapter not installed)" -ForegroundColor Gray
    }
    if ($Clean) {
        Write-Host "   Clean : delete $dbPath (+ -wal/-shm) and $teamsDir" -ForegroundColor Gray
    }
    Write-Host "   Run   : $runCommand" -ForegroundColor Gray
    Write-Host "   URL   : $url" -ForegroundColor Gray
    Write-Host ''
    Write-Host 'Dry run complete. Nothing was built or started.' -ForegroundColor Green
    exit 0
}

# Kestrel binds the port last: the hosted services start first, so an occupied
# port spawns a node process per Persona and the App Tool servers, and only then
# throws AddressInUseException - a sixty-line stack trace and exit code
# -532462766 (0xE0434352, "the CLR threw"), after a full build has already run.
# Get-NetTCPConnection is the NetTCPIP module, so this is a Windows-only check;
# elsewhere the app's own bind failure remains the only signal.
if (Get-Command Get-NetTCPConnection -ErrorAction SilentlyContinue) {
    $listener = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue |
        Select-Object -First 1

    if ($listener) {
        $ownerId = $listener.OwningProcess
        $owner = Get-Process -Id $ownerId -ErrorAction SilentlyContinue
        $ownerText = if ($owner) { "$($owner.ProcessName) (PID $ownerId)" } else { "PID $ownerId" }

        Write-Host ''
        Write-Host "Port $Port is already in use by $ownerText." -ForegroundColor Yellow
        Write-Host '   An earlier run that did not shut down cleanly is the usual cause.' -ForegroundColor Gray
        Write-Host "   Stop it        : Stop-Process -Id $ownerId" -ForegroundColor Gray
        Write-Host "   Or use another : ./run.ps1 -Port $($Port + 1)" -ForegroundColor Gray
        exit 1
    }
}

# -- Setup ----------------------------------------------------------------------

if ($needsAcpSetup) {
    Write-Host ''
    Write-Host 'ACP adapter not installed - running tools/acp/install.ps1...' -ForegroundColor Cyan

    # install.ps1 throws (rather than just setting $LASTEXITCODE) when npm install fails, so
    # that is the failure this script needs to catch here, not a nonzero exit code.
    try {
        & $acpInstallScript
    } catch {
        Write-Host ''
        Write-Host "tools/acp/install.ps1 failed: $_" -ForegroundColor Yellow
        Write-Host '   See the npm output above for details.' -ForegroundColor Gray
        exit 1
    }

    if (-not (Test-Path $acpAdapterEntryPoint)) {
        Write-Host ''
        Write-Host "tools/acp/install.ps1 completed, but $acpAdapterEntryPoint still does not exist." -ForegroundColor Yellow
        Write-Host '   Check the npm output above for errors, or run without ACP: ./run.ps1 -NoAcp' -ForegroundColor Gray
        exit 1
    }

    Write-Host 'ACP adapter installed.' -ForegroundColor Green
}

# -- Clean --------------------------------------------------------------------

if ($Clean) {
    Write-Host ''
    Write-Host 'Cleaning previous database and Persona files...' -ForegroundColor Cyan

    $dbFiles = @($dbPath, "$dbPath-wal", "$dbPath-shm") | Where-Object { Test-Path $_ }
    foreach ($dbFile in $dbFiles) {
        Remove-Item -LiteralPath $dbFile -Force
        Write-Host "   Deleted $dbFile" -ForegroundColor Gray
    }

    $teamsDirExisted = Test-Path $teamsDir
    if ($teamsDirExisted) {
        Remove-Item -LiteralPath $teamsDir -Recurse -Force
        Write-Host "   Deleted $teamsDir" -ForegroundColor Gray
    }

    if (-not $dbFiles -and -not $teamsDirExisted) {
        Write-Host '   Nothing to delete.' -ForegroundColor Gray
    }
}

# -- Build --------------------------------------------------------------------

if (-not $NoBuild) {
    Write-Host ''
    Write-Host 'Building Huddle.slnx...' -ForegroundColor Cyan

    Push-Location $scriptDir
    try {
        dotnet build $solution
        $buildExitCode = $LASTEXITCODE
    } finally {
        Pop-Location
    }

    if ($buildExitCode -ne 0) {
        Write-Host ''
        Write-Host "Build failed (exit code $buildExitCode). See the output above." -ForegroundColor Yellow
        exit $buildExitCode
    }

    Write-Host 'Build succeeded.' -ForegroundColor Green
}

# -- Run ----------------------------------------------------------------------

# $env: assignments land in this process, and `./run.ps1` runs in the caller's
# process - so an unrestored value would silently change the next run started
# from the same shell.
#
# Restoring is not the mirror image of saving. GetEnvironmentVariable returns
# $null for a variable that was never set, and PowerShell converts $null to
# [string]::Empty when it binds an argument to a [string] parameter - so handing
# that $null straight back to SetEnvironmentVariable DEFINES the variable as an
# empty string instead of removing it. .NET's "null deletes the variable"
# contract never gets the chance to fire. Configuration then reads
# Team:Acp:Enabled as '', the environment provider outranks
# appsettings.Development.json, and '' does not convert to a bool - so the next
# run in the same shell dies in the options binder, inside the
# SqliteTeamDirectory constructor, long before Kestrel. Remove-Item Env: is the
# only form here that genuinely unsets it.
$acpVariableName = 'Team__Acp__Enabled'
$savedAcp = [Environment]::GetEnvironmentVariable($acpVariableName)

# A shell that ran the version of this script with that bug is still carrying the
# empty string, and the fix above cannot reach back into it. Clear it instead of
# letting the app crash on it.
if ($null -ne $savedAcp -and [string]::IsNullOrWhiteSpace($savedAcp)) {
    Write-Host "Clearing an empty $acpVariableName left in this shell by an earlier run." -ForegroundColor Gray
    Remove-Item "Env:$acpVariableName" -ErrorAction SilentlyContinue
    $savedAcp = $null
}

Push-Location $scriptDir
try {
    if ($NoAcp) {
        [Environment]::SetEnvironmentVariable($acpVariableName, 'false')
        Write-Host 'ACP disabled for this run - no node process per Persona.' -ForegroundColor Gray
    }

    Write-Host ''
    Write-Host "Starting the app on $url - press Ctrl+C to stop." -ForegroundColor Green
    Write-Host ''

    # Always --no-build: either this script just built, or -NoBuild asked for
    # whatever is already compiled. Either way `dotnet run` must not build again.
    $runArgs = @('run', '--project', $appProject, '--no-build', '--urls', $url)

    & dotnet @runArgs
    $runExitCode = $LASTEXITCODE
} finally {
    # Only -NoAcp changes the variable, so only -NoAcp has anything to put back.
    if ($NoAcp) {
        if ($null -eq $savedAcp) {
            Remove-Item "Env:$acpVariableName" -ErrorAction SilentlyContinue
        } else {
            [Environment]::SetEnvironmentVariable($acpVariableName, $savedAcp)
        }
    }

    Pop-Location
}

if ($runExitCode -ne 0) {
    Write-Host ''
    Write-Host "The app exited with code $runExitCode." -ForegroundColor Yellow
    exit $runExitCode
}
