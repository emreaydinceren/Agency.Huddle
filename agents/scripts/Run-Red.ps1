<#
.SYNOPSIS
    Wrapper for a .t red run with automatic red file generation and validation.

.DESCRIPTION
    Calls Run-Tests.ps1 with -RedTask, -Label, -FilterClass, and optional -NewNames and
    -AllowCodes parameters. Captures the exit code and reports the result: on success, prints
    the path and line count of the red file; on failure, prints the exit code. Never passes
    -Force to Run-Tests.ps1.

.PARAMETER Task
    The task identifier (e.g., "1.1.t" or "feature-red"). Used as -Label and -RedTask.

.PARAMETER FilterClass
    Test class filter pattern, passed to Run-Tests.ps1 -FilterClass. Use -FilterClass "*A,*B" for
    several classes.

.PARAMETER NewNames
    Passed to Run-Tests.ps1 -NewNames if supplied. A compile red requires -NewNames: without it every
    compiler error is rejected (exit 8, no red file). A runtime red (failing tests) needs none.

.PARAMETER AllowCodes
    Optional; passed to Run-Tests.ps1 -AllowCodes if supplied.

.EXAMPLE
    pwsh agents/scripts/Run-Red.ps1 -Task "1.1.t" -FilterClass "*MyTests"

.EXAMPLE
    pwsh agents/scripts/Run-Red.ps1 -Task "2.3.t" -FilterClass "*ServiceTests" -NewNames "NewType,NewMethod"
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$Task,

    [Parameter(Mandatory)]
    [string]$FilterClass,

    [string]$NewNames,

    [string]$AllowCodes
)

$ErrorActionPreference = 'Stop'

$runTestsArgs = @{
    FilterClass = $FilterClass
    Label = $Task
    RedTask = $Task
    RedDir = 'Conversation/teampages/red'
}

if ($NewNames) {
    $runTestsArgs['NewNames'] = $NewNames
}

if ($AllowCodes) {
    $runTestsArgs['AllowCodes'] = $AllowCodes
}

$scriptPath = Join-Path $PSScriptRoot 'Run-Tests.ps1'
& $scriptPath @runTestsArgs *>&1
$exitCode = $LASTEXITCODE
if ($null -eq $exitCode) { $exitCode = 0 }

if ($exitCode -eq 0) {
    $redPath = Join-Path (git rev-parse --show-toplevel) "Conversation\teampages\red\$Task.txt"
    if (Test-Path $redPath) {
        $lineCount = @(Get-Content -LiteralPath $redPath -ErrorAction SilentlyContinue | Measure-Object -Line).Lines
        Write-Host "red saved to Conversation/teampages/red/$Task.txt ($lineCount lines); do not re-read it"
    }
    else {
        Write-Host "red saved to Conversation/teampages/red/$Task.txt (0 lines); do not re-read it"
    }
}
else {
    Write-Host "RED REJECTED, exit $exitCode"
}

exit $exitCode
