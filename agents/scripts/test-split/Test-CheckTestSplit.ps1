<#
.SYNOPSIS
    Pins the behaviour of Check-TestSplit.ps1 against fixture baseline and list files.

.DESCRIPTION
    Writes fixture files to a temp folder and runs

        pwsh -NoProfile -File Check-TestSplit.ps1 -Baseline <b> -Lists <l1>,<l2>

    for each case, asserting the exit code and a phrase of the output. Prints PASS or FAIL per case and
    exits 1 when any case fails or the checker is missing.

.EXAMPLE
    pwsh agents/scripts/test-split/Test-CheckTestSplit.ps1
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$checker = Join-Path $PSScriptRoot 'Check-TestSplit.ps1'
$work = Join-Path ([IO.Path]::GetTempPath()) ("test-checktestsplit-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work | Out-Null

$script:failures = 0
$script:counter = 0

function Write-Fixture {
    param([string[]]$Names)
    $script:counter++
    $path = Join-Path $work ("list{0}.txt" -f $script:counter)
    Set-Content -LiteralPath $path -Value $Names
    return $path
}

function Invoke-Case {
    param(
        [string]$Name,
        [string[]]$Baseline,
        [string[][]]$Lists,
        [int]$ExpectedExit,
        [string]$ExpectedPhrase
    )

    $baselineFile = Write-Fixture -Names $Baseline
    $listFiles = @($Lists | ForEach-Object { Write-Fixture -Names $_ })

    $output = ''
    $exit = -1
    if (Test-Path -LiteralPath $checker) {
        $output = (& pwsh -NoProfile -File $checker -Baseline $baselineFile -Lists ($listFiles -join ',') 2>&1 | Out-String)
        $exit = $LASTEXITCODE
    }
    else {
        $output = "Check-TestSplit.ps1 is missing: $checker"
    }

    $exitOk = ($exit -eq $ExpectedExit)
    $phraseOk = ([string]::IsNullOrEmpty($ExpectedPhrase)) -or ($output.Contains($ExpectedPhrase))
    $forbidden = ($Name -like '*ignored*') -and ($output -match 'AssemblySetupTests')

    if ($exitOk -and $phraseOk -and -not $forbidden) {
        Write-Host "PASS $Name"
    }
    else {
        $script:failures++
        Write-Host "FAIL $Name (exit $exit, expected $ExpectedExit; phrase '$ExpectedPhrase' found: $phraseOk)"
        Write-Host ($output.TrimEnd())
    }
}

try {
    if (-not (Test-Path -LiteralPath $checker)) {
        Write-Host "Check-TestSplit.ps1 is missing: $checker"
    }

    Invoke-Case -Name 'complete split passes' -Baseline @('A', 'B', 'C') -Lists @(@('A', 'B'), @('C')) -ExpectedExit 0 -ExpectedPhrase 'missing 0'
    Invoke-Case -Name 'missing test fails' -Baseline @('A', 'B', 'C') -Lists @(, @('A', 'B')) -ExpectedExit 1 -ExpectedPhrase 'missing: C'
    Invoke-Case -Name 'duplicate test fails' -Baseline @('A', 'B', 'C') -Lists @(@('A', 'B'), @('B', 'C')) -ExpectedExit 1 -ExpectedPhrase 'duplicate: B'
    Invoke-Case -Name 'new test warns' -Baseline @('A', 'B', 'C') -Lists @(, @('A', 'B', 'C', 'D')) -ExpectedExit 0 -ExpectedPhrase 'warning: not in baseline: D'
    Invoke-Case -Name 'AssemblySetupTests line is ignored' -Baseline @('A') -Lists @(, @('A', 'X.HostedAssemblySetupTests.Y')) -ExpectedExit 0 -ExpectedPhrase 'missing 0'
}
finally {
    Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
}

if ($script:failures -gt 0) {
    Write-Host "TEST-CHECKTESTSPLIT Result=FAIL ($($script:failures) failed)"
    exit 1
}

Write-Host 'TEST-CHECKTESTSPLIT Result=PASS'
exit 0
