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

    See docs/AgencyTeam.md ("Build, test, run") for the commands this wraps.

.PARAMETER Port
    Port to listen on (default: 5100, matching launchSettings.json).

.PARAMETER NoAcp
    Set Team__Acp__Enabled=false for this run, so no node process is spawned per
    Persona. The variable is restored when the script exits.

.PARAMETER NoBuild
    Skip the build and run whatever is already compiled.

.PARAMETER DataDir
    Overrides the data directory (default: App_Data under src/Huddle.App).
    $env:Team__DataDir is honoured when -DataDir is not passed; -DataDir wins
    over the environment variable. Relative values are resolved against
    src/Huddle.App, matching TeamOptions.DataDir's own default resolution.

.PARAMETER Clean
    Delete the previous run's SQLite database (Team:DataDir/team.db, plus its -wal/-shm
    sidecar files) and every Teammate definition file
    (Team:DataDir/Teammates/<Folder>/<Folder>.md, matched case-insensitively) before
    building and starting the app. Opt-in and irreversible - there is no prompt, so only
    pass it when you mean to start from an empty roster. Every work/ directory and all of
    Teams/ (Team folders and Library notes) are left alone, as is
    Teammates/_unsorted and the Teammates/.layout-migrated marker. Other App_Data
    content (prompts, appearance, avatars, rooms, logs) is left alone too.

.PARAMETER CleanOnly
    Run -Clean's deletion, then exit without building or starting anything. Implies -Clean.

.PARAMETER DryRun
    Print what would run, then exit without building or starting anything. With -Clean
    (or -CleanOnly), lists the actual files that would be deleted.

.EXAMPLE
    ./run.ps1
    ./run.ps1 -Port 5200
    ./run.ps1 -NoAcp
    ./run.ps1 -NoBuild
    ./run.ps1 -Clean
    ./run.ps1 -CleanOnly -DataDir C:\temp\scratch-data
    ./run.ps1 -DryRun
#>
[CmdletBinding()]
param(
    [int]$Port = 5100,
    [switch]$NoAcp,
    [switch]$NoBuild,
    [string]$DataDir,
    [switch]$Clean,
    [switch]$CleanOnly,
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'

if ($CleanOnly) {
    $Clean = $true
}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$solution = Join-Path $scriptDir 'Huddle.slnx'
$appProject = Join-Path $scriptDir 'src/Huddle.App'
$url = "http://localhost:$Port"

# TeamOptions.DataDir defaults to "App_Data" (resolved relative to src/Huddle.App - see
# launchSettings.json, which sets no workingDirectory), honouring the Team__DataDir
# environment variable and, here, an explicit -DataDir override (which wins over the
# environment variable). AcpOptions.TeammatesDir's default ("Teammates") and
# SqliteTeamDirectory's hardcoded "team.db" are not read from configuration: this script
# has no config binder, and every appsettings*.json in the repo leaves both defaults
# unchanged, so hardcoding them here is exactly as safe as the rest of this script's
# assumptions about a stock dev setup.
$dataDirInput = if ($DataDir) { $DataDir } elseif ($env:Team__DataDir) { $env:Team__DataDir } else { 'App_Data' }
$dataDir = if ([System.IO.Path]::IsPathRooted($dataDirInput)) { $dataDirInput } else { Join-Path $appProject $dataDirInput }
$teammatesDir = Join-Path $dataDir 'Teammates'
$dbPath = Join-Path $dataDir 'team.db'

# The definition file inside a Teammate folder is the one whose base name matches the
# folder name, case-insensitively (FolderSnapshot.PathComparer's own rule). _unsorted is
# migration overflow, never a Teammate, and is skipped; work/ subdirectories and the
# .layout-migrated marker are never touched.
function Get-DefinitionFilesToClean {
    param([string]$TeammatesDir)

    $result = [System.Collections.Generic.List[string]]::new()
    if (-not (Test-Path -LiteralPath $TeammatesDir)) {
        return $result
    }

    Get-ChildItem -LiteralPath $TeammatesDir -Directory | Where-Object {
        -not $_.Name.Equals('_unsorted', [System.StringComparison]::OrdinalIgnoreCase)
    } | ForEach-Object {
        $folder = $_
        Get-ChildItem -LiteralPath $folder.FullName -File -Filter '*.md' -ErrorAction SilentlyContinue |
            Where-Object { $_.BaseName.Equals($folder.Name, [System.StringComparison]::OrdinalIgnoreCase) } |
            ForEach-Object { $result.Add($_.FullName) }
    }

    return $result
}

if (-not (Test-Path $solution)) {
    Write-Host "Huddle.slnx not found at $solution." -ForegroundColor Yellow
    Write-Host "   Run this script from the repository root." -ForegroundColor Gray
    exit 1
}

# -- Clean-only: skip every other check, delete, exit -------------------------

if ($CleanOnly) {
    $dbFiles = @($dbPath, "$dbPath-wal", "$dbPath-shm") | Where-Object { Test-Path -LiteralPath $_ }
    $definitionFiles = Get-DefinitionFilesToClean -TeammatesDir $teammatesDir
    $toDelete = @($dbFiles) + @($definitionFiles)

    if ($DryRun) {
        Write-Host ''
        Write-Host 'Dry run - files that would be deleted' -ForegroundColor Cyan
        if ($toDelete.Count -eq 0) {
            Write-Host '   Nothing to delete.' -ForegroundColor Gray
        } else {
            foreach ($file in $toDelete) {
                Write-Host "   $file" -ForegroundColor Gray
            }
        }
        Write-Host ''
        Write-Host 'Dry run complete. Nothing was deleted.' -ForegroundColor Green
        exit 0
    }

    Write-Host ''
    Write-Host 'Cleaning previous database and Teammate definitions...' -ForegroundColor Cyan
    if ($toDelete.Count -eq 0) {
        Write-Host '   Nothing to delete.' -ForegroundColor Gray
    } else {
        foreach ($file in $toDelete) {
            Remove-Item -LiteralPath $file -Force
            Write-Host "   Deleted $file" -ForegroundColor Gray
        }
    }
    exit 0
}

# Acp:Enabled is on in Development and spawns one node process per Persona, so a
# missing node turns into a startup failure rather than a missing feature.
if (-not $NoAcp -and -not (Get-Command node -ErrorAction SilentlyContinue)) {
    Write-Host "node was not found on PATH." -ForegroundColor Yellow
    Write-Host "   Development config sets Team:Acp:Enabled=true, which starts one node process per Persona." -ForegroundColor Gray
    Write-Host "   Install node, or run without them: ./run.ps1 -NoAcp" -ForegroundColor Gray
    exit 1
}

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
    if ($Clean) {
        $dbFiles = @($dbPath, "$dbPath-wal", "$dbPath-shm") | Where-Object { Test-Path -LiteralPath $_ }
        $definitionFiles = Get-DefinitionFilesToClean -TeammatesDir $teammatesDir
        $toDelete = @($dbFiles) + @($definitionFiles)

        Write-Host '   Clean : delete the following files' -ForegroundColor Gray
        if ($toDelete.Count -eq 0) {
            Write-Host '      Nothing to delete.' -ForegroundColor Gray
        } else {
            foreach ($file in $toDelete) {
                Write-Host "      $file" -ForegroundColor Gray
            }
        }
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

# -- Clean --------------------------------------------------------------------

if ($Clean) {
    Write-Host ''
    Write-Host 'Cleaning previous database and Teammate definitions...' -ForegroundColor Cyan

    $dbFiles = @($dbPath, "$dbPath-wal", "$dbPath-shm") | Where-Object { Test-Path -LiteralPath $_ }
    foreach ($dbFile in $dbFiles) {
        Remove-Item -LiteralPath $dbFile -Force
        Write-Host "   Deleted $dbFile" -ForegroundColor Gray
    }

    $definitionFiles = Get-DefinitionFilesToClean -TeammatesDir $teammatesDir
    foreach ($definitionFile in $definitionFiles) {
        Remove-Item -LiteralPath $definitionFile -Force
        Write-Host "   Deleted $definitionFile" -ForegroundColor Gray
    }

    if (-not $dbFiles -and $definitionFiles.Count -eq 0) {
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
