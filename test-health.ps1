<#
.SYNOPSIS
    Proves the built Agency.Huddle app starts as a real process and answers GET /health.

.DESCRIPTION
    Starts src/Huddle.App's compiled output as a child process on a kernel-assigned loopback
    port, waits for Kestrel to report that port, GETs /health over a real socket, and prints
    PASS or FAIL. Exit code is 0 on pass and 1 on fail, so it is usable as a CI gate.

    This is the automated test for the health endpoint, and it is deliberately NOT an xUnit
    test. Every test in tests/Huddle.Tests hosts the app in-process through
    TeamWebApplicationFactory, so none of them can fail when the composed application does not
    boot as a process - which is the only thing asserted here.

    It never builds. Either CI already built, or you already ran ./run.ps1; a second build here
    would be a second place to get build flags wrong, and a red FAIL that actually meant
    "compile error" is exactly the ambiguity this script exists to avoid.

    The child is configured entirely through command-line arguments, never environment
    variables. Command-line configuration is the last provider ASP.NET Core registers, so it
    outranks appsettings*.json AND anything already in the caller's shell. It also means this
    script mutates nothing it would have to restore.

.PARAMETER Configuration
    Which build output to run: Debug (default, what ./run.ps1 produces) or Release (what CI
    builds). Not auto-detected on purpose - picking "whichever bin directory is newer" would
    silently smoke-test a stale output and report PASS.

.PARAMETER TimeoutSeconds
    Total budget for launch + bind + first healthy response. CI passes 120: that runner is
    arm64 and cold-starts slowly.

.EXAMPLE
    ./test-health.ps1
    ./test-health.ps1 -Configuration Release -TimeoutSeconds 120
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',

    [ValidateRange(5, 600)]
    [int]$TimeoutSeconds = 60
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

# -- Helpers ------------------------------------------------------------------

function Write-Field([string]$Label, [string]$Value) {
    Write-Host ('  {0,-14}: {1}' -f $Label, $Value) -ForegroundColor Gray
}

# The child holds both log files open for writing for its whole life. Get-Content's share mode
# is not dependable against a live writer on Windows, so open the file explicitly with
# ReadWrite|Delete - that is what makes tailing it safe on both platforms.
function Read-SharedText([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { return '' }

    $stream = [System.IO.FileStream]::new(
        $Path,
        [System.IO.FileMode]::Open,
        [System.IO.FileAccess]::Read,
        [System.IO.FileShare]::ReadWrite -bor [System.IO.FileShare]::Delete)
    try {
        $reader = [System.IO.StreamReader]::new($stream)
        try { return $reader.ReadToEnd() } finally { $reader.Dispose() }
    } finally {
        $stream.Dispose()
    }
}

function Write-Tail([string]$Title, [string]$Path, [int]$Lines = 40) {
    Write-Host ''
    Write-Host "--- $Title (last $Lines lines) ---" -ForegroundColor Yellow

    $text = Read-SharedText $Path
    if ([string]::IsNullOrWhiteSpace($text)) {
        Write-Host '  (empty)' -ForegroundColor DarkGray
        return
    }

    $all = $text.TrimEnd() -split "`r?`n"
    $tail = if ($all.Count -gt $Lines) { $all[-$Lines .. -1] } else { $all }
    foreach ($line in $tail) { Write-Host "  $line" }
}

# Reading ExitCode on a live process throws, and neither case may take down the failure report.
function Get-ChildExitCode($Process) {
    try {
        [void]$Process.WaitForExit(5000)
        return $Process.ExitCode
    } catch {
        return '(unknown)'
    }
}

function Remove-TreeWithRetry([string]$Path) {
    for ($attempt = 1; $attempt -le 5; $attempt++) {
        try {
            if (-not (Test-Path -LiteralPath $Path)) { return $true }
            Remove-Item -LiteralPath $Path -Recurse -Force -ErrorAction Stop
            return $true
        } catch {
            # Windows can still hold a SQLite handle for a moment after the kill.
            Start-Sleep -Milliseconds (100 * $attempt)
        }
    }
    return $false
}

function Write-Banner([string]$Verdict, [string]$Text, [string]$Colour) {
    $rule = '=' * 70
    Write-Host ''
    Write-Host $rule -ForegroundColor $Colour
    Write-Host ("  {0}   {1}" -f $Verdict, $Text) -ForegroundColor $Colour
    Write-Host $rule -ForegroundColor $Colour
}

# -- Preconditions ------------------------------------------------------------

# -SkipHttpErrorCheck and -NoProxy are PowerShell 7 parameters. Windows PowerShell 5.1 would
# fail with a parameter-binding error, which is not a PASS/FAIL.
if ($PSVersionTable.PSVersion.Major -lt 7) {
    Write-Banner 'FAIL' "this script needs PowerShell 7 (pwsh); this is $($PSVersionTable.PSVersion)." 'Red'
    exit 1
}

$repoRoot = $PSScriptRoot
if (-not (Test-Path -LiteralPath (Join-Path $repoRoot 'Huddle.slnx'))) {
    Write-Banner 'FAIL' "Huddle.slnx not found next to this script ($repoRoot)." 'Red'
    exit 1
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Banner 'FAIL' 'dotnet was not found on PATH.' 'Red'
    exit 1
}

$appDir = Join-Path $repoRoot 'src/Huddle.App'
$appDll = Join-Path $appDir "bin/$Configuration/net10.0/Huddle.App.dll"

if (-not (Test-Path -LiteralPath $appDll)) {
    Write-Banner 'FAIL' "no $Configuration build to test." 'Red'
    Write-Host "  expected : $appDll" -ForegroundColor Gray
    Write-Host "  build it : dotnet build Huddle.slnx --configuration $Configuration" -ForegroundColor Gray
    exit 1
}

# -- Run layout ---------------------------------------------------------------

# Eight hex characters, not a GUID: on Unix a named pipe becomes $TMPDIR/CoreFxPipe_<name>, and
# sockaddr_un caps that path near 104 bytes.
$runId     = [guid]::NewGuid().ToString('n').Substring(0, 8)
$pipeName  = "huddle-health-$runId"
$runDir    = Join-Path ([System.IO.Path]::GetTempPath()) "huddle-health-$runId"
$dataDir   = Join-Path $runDir 'App_Data'
$stdoutLog = Join-Path $runDir 'stdout.log'
$stderrLog = Join-Path $runDir 'stderr.log'

New-Item -ItemType Directory -Path $runDir -Force | Out-Null

Write-Host ''
Write-Host 'Huddle health check' -ForegroundColor Cyan
Write-Field 'configuration' $Configuration
Write-Field 'app'           $appDll
Write-Field 'content root'  $appDir
Write-Field 'data dir'      $dataDir
Write-Field 'pipe name'     $pipeName
Write-Field 'timeout'       "${TimeoutSeconds}s"

# Start-Process joins -ArgumentList with spaces and quotes nothing, so anything that can
# contain a space carries its own quotes. None of these values ends in a directory separator,
# which matters: --key="C:\dir\" would escape the closing quote on Windows.
$childArgs = @(
    "`"$appDll`""
    '--environment', 'Development'          # the only environment that loads static web assets
                                            # from a BUILD output rather than a PUBLISH output
    "--contentRoot=`"$appDir`""             # so appsettings*.json and wwwroot resolve exactly
                                            # as they do under `dotnet run`
    '--urls', 'http://127.0.0.1:0'          # kernel-assigned port; the literal, never localhost
    "--Team:DataDir=`"$dataDir`""           # never the developer's App_Data
    "--Team:PipeName=$pipeName"             # never collides with a running ./run.ps1
    '--Team:Acp:Enabled=false'              # Development turns this ON; it spawns node per
                                            # Persona and spends real money
    '--Logging:LogLevel:Microsoft.Hosting.Lifetime=Information'
                                            # guarantees "Now listening on:" whatever
                                            # appsettings.json says now or later
)

$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
$proc = $null
$failure = $null
$boundUrl = $null
$status = $null
$body = $null

try {
    $proc = Start-Process -FilePath 'dotnet' `
        -ArgumentList $childArgs `
        -WorkingDirectory $appDir `
        -RedirectStandardOutput $stdoutLog `
        -RedirectStandardError  $stderrLog `
        -NoNewWindow `
        -PassThru

    Write-Field 'child pid' $proc.Id

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)

    # -- Phase 1: the port Kestrel actually bound ------------------------------
    #
    # \S+ and not (.+)$: the console formatter puts the message on its own indented line, and on
    # Windows this file is CRLF - .+ would capture the trailing carriage return into the URI.
    $listeningPattern = [regex]'Now listening on:\s*(?<url>\S+)'

    while ($true) {
        # Read the log BEFORE testing HasExited: an app that printed the line and then died must
        # still be reported as "died", but with the port known, by the HTTP loop below.
        $match = $listeningPattern.Match((Read-SharedText $stdoutLog))
        if ($match.Success) {
            $boundUrl = $match.Groups['url'].Value
            break
        }

        if ($proc.HasExited) {
            throw "the app exited with code $(Get-ChildExitCode $proc) before it printed 'Now listening on:'"
        }

        if ((Get-Date) -ge $deadline) {
            throw "the app never printed 'Now listening on:' within ${TimeoutSeconds}s"
        }

        [void]$proc.WaitForExit(200)   # the poll interval AND an instant wake-up when it dies
    }

    # Poll the loopback literal on the parsed port rather than the logged string, so a future
    # bind address that logs as http://[::]:NNNN still resolves to something reachable.
    $port = ([uri]$boundUrl).Port
    $healthUrl = "http://127.0.0.1:$port/health"
    Write-Field 'listening on' $boundUrl

    # -- Phase 2: /health ------------------------------------------------------
    #
    # -SkipHttpErrorCheck is what makes this readable: a 4xx/5xx comes back as a response object
    # instead of an exception, so everything reaching the catch is transport-level and therefore
    # retryable, and everything that returns is a verdict.
    # -NoProxy: a CI container with http_proxy set would otherwise route a loopback GET through
    # a proxy that cannot reach it.
    $lastTransportError = 'none'
    while ($true) {
        try {
            $response = Invoke-WebRequest -Uri $healthUrl -Method Get `
                -NoProxy -SkipHttpErrorCheck -TimeoutSec 5
            $status = [int]$response.StatusCode
            $body = "$($response.Content)".Trim()
            break
        } catch {
            $lastTransportError = $_.Exception.Message
        }

        if ($proc.HasExited) {
            throw "the app exited with code $(Get-ChildExitCode $proc) while /health was being polled (last error: $lastTransportError)"
        }

        if ((Get-Date) -ge $deadline) {
            throw "GET $healthUrl never completed within ${TimeoutSeconds}s (last error: $lastTransportError)"
        }

        [void]$proc.WaitForExit(200)
    }

    Write-Field 'GET /health' "$status $body"

    # The server answered, so the answer is the verdict - no retry. Kestrel starts listening only
    # after the endpoint pipeline is built, so a 404 here means /health is genuinely not mapped.
    if ($status -eq 404) {
        throw "GET /health answered 404 - the endpoint is not mapped. Check app.MapHealthChecks(`"/health`") in src/Huddle.App/Program.cs"
    }
    if ($status -ne 200) {
        throw "GET /health answered $status (body: '$body')"
    }
    if ($body -ne 'Healthy') {
        throw "GET /health answered 200 but the body was '$body', not 'Healthy'"
    }
} catch {
    $failure = $_.Exception.Message
} finally {
    # Cleanup may never change the verdict, so every step stands alone.
    if ($null -ne $proc) {
        try {
            if (-not $proc.HasExited) {
                # One process today (dotnet <dll> does not fork, and ACP is off), but tree-kill
                # costs nothing and stops this leaking the day a child is reintroduced.
                $proc.Kill($true)
            }
        } catch { }
        try { [void]$proc.WaitForExit(10000) } catch { }
    }
}

$elapsed = '{0:N1}s' -f $stopwatch.Elapsed.TotalSeconds

# Data directory always goes; logs survive a failure so there is something to read.
try {
    if (-not (Remove-TreeWithRetry $dataDir)) {
        Write-Host "  note: could not delete $dataDir - remove it by hand." -ForegroundColor DarkYellow
    }
} catch { }

try {
    if (-not $IsWindows) {
        # A named pipe is a unix domain socket, and SIGKILL leaves the file behind.
        $socketPath = Join-Path ([System.IO.Path]::GetTempPath()) "CoreFxPipe_$pipeName"
        Remove-Item -LiteralPath $socketPath -Force -ErrorAction SilentlyContinue
    }
} catch { }

if ($null -eq $failure) {
    try { [void](Remove-TreeWithRetry $runDir) } catch { }

    Write-Banner 'PASS' "/health answered 200 Healthy in $elapsed" 'Green'
    exit 0
}

Write-Banner 'FAIL' $failure 'Red'
Write-Host "  stdout : $stdoutLog" -ForegroundColor Gray
Write-Host "  stderr : $stderrLog" -ForegroundColor Gray

# stderr first: an unhandled exception lands there, and it is what you want at eye level.
try { Write-Tail 'child stderr' $stderrLog } catch { }
try { Write-Tail 'child stdout' $stdoutLog } catch { }

Write-Host ''
if ($env:GITHUB_ACTIONS -eq 'true') {
    # One line, no newlines: Gitea surfaces this as an annotation on the job.
    Write-Host "::error::test-health.ps1 FAIL: $failure"
}

# Printed twice on purpose - once above the diagnostics, once at the very bottom - so the
# verdict is visible whichever end of a CI log you land on.
Write-Host "FAIL (exit 1) after ${elapsed}: $failure" -ForegroundColor Red
exit 1
