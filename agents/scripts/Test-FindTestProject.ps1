<#
.SYNOPSIS
    Pins Find-TestProject.ps1 against a fixture tree built under a temp root.

.DESCRIPTION
    Builds tests/Huddle.Tests.A (FooTests), tests/Huddle.Tests.B (BazTests) and tests/Huddle.Tests.Common
    (IsTestProject false, FooTestsHelper), plus bin/ copies that must be ignored. Then runs

        pwsh -NoProfile -File agents/scripts/Find-TestProject.ps1 -Root <fixture> ...

    and compares the csproj paths it prints with the expected projects. Exits 1 on any failure,
    otherwise 0. The last output line is
        TEST-FINDTESTPROJECT Result=PASS|FAIL

.EXAMPLE
    pwsh agents/scripts/Test-FindTestProject.ps1
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$finder = Join-Path $PSScriptRoot 'Find-TestProject.ps1'
$fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ("find-test-project-" + [guid]::NewGuid().ToString('N'))

function New-Project {
    param([string]$Name, [string]$IsTestProject, [string]$FileName, [string]$Source)
    $dir = Join-Path $fixtureRoot "tests/$Name"
    New-Item -ItemType Directory -Force -Path (Join-Path $dir 'bin/Debug') | Out-Null
    Set-Content -LiteralPath (Join-Path $dir "$Name.csproj") -Value "<Project><PropertyGroup><IsTestProject>$IsTestProject</IsTestProject></PropertyGroup></Project>"
    Set-Content -LiteralPath (Join-Path $dir $FileName) -Value $Source
    # A copy under bin/ that must be ignored.
    Set-Content -LiteralPath (Join-Path $dir "bin/Debug/$FileName") -Value $Source
}

function Invoke-Finder {
    param([string[]]$Arguments)
    $output = & pwsh -NoProfile -File $finder -Root $fixtureRoot @Arguments 2>&1
    return @($output | ForEach-Object { "$_".Trim() } | Where-Object { $_.Length -gt 0 })
}

$failures = 0

function Assert-Case {
    param([string]$Name, [string[]]$Arguments, [string[]]$Expected)
    $actual = @(Invoke-Finder -Arguments $Arguments | ForEach-Object { $_.Replace('\', '/') })
    $want = @($Expected | ForEach-Object { (Join-Path $fixtureRoot "tests/$_/$_.csproj").Replace('\', '/') })
    if (($actual -join '|') -ceq ($want -join '|')) {
        Write-Output "ok   $Name"
    }
    else {
        $script:failures++
        Write-Output "FAIL $Name expected=[$($want -join ', ')] actual=[$($actual -join ', ')]"
    }
}

try {
    if (-not (Test-Path -LiteralPath $finder)) {
        throw "Find-TestProject.ps1 is missing: $finder"
    }

    New-Project 'Huddle.Tests.A' 'true' 'FooTests.cs' @'
public sealed class FooTests
{
    [Fact] public void Bar_Works() { }
}
'@
    New-Project 'Huddle.Tests.B' 'true' 'BazTests.cs' @'
public sealed class BazTests
{
    [Fact] public void Qux_Works() { }
}
'@
    New-Project 'Huddle.Tests.Common' 'false' 'FooTestsHelper.cs' @'
public sealed class FooTestsHelper
{
    [Fact] public void Bar_Works() { }
}
'@

    # A project that only imports the shared props (no literal IsTestProject) is still a test project.
    $propsDir = Join-Path $fixtureRoot 'tests/Huddle.Tests.C'
    New-Item -ItemType Directory -Force -Path $propsDir | Out-Null
    Set-Content -LiteralPath (Join-Path $propsDir 'Huddle.Tests.C.csproj') -Value '<Project><Import Project="$(MSBuildThisFileDirectory)..\Huddle.Tests.Feature.props" /></Project>'
    Set-Content -LiteralPath (Join-Path $propsDir 'GammaCheck.cs') -Value 'public sealed class GammaCheck { [Fact] public void G() { } }'

    Assert-Case 'class in props-importing project' @('-Class', 'GammaCheck') @('Huddle.Tests.C')
    Assert-Case 'class exact'@('-Class', 'FooTests') @('Huddle.Tests.A')
    Assert-Case 'class wildcard prefix' @('-Class', '*FooTests') @('Huddle.Tests.A')
    Assert-Case 'class namespace-qualified' @('-Class', 'Agency.Huddle.Tests.FooTests') @('Huddle.Tests.A')
    Assert-Case 'class wildcard both projects' @('-Class', '*Tests') @('Huddle.Tests.A', 'Huddle.Tests.B')
    Assert-Case 'method wildcard' @('-Method', '*.Bar_Works') @('Huddle.Tests.A')
    Assert-Case 'class no match' @('-Class', 'Nope') @()
}
finally {
    if (Test-Path -LiteralPath $fixtureRoot) {
        Remove-Item -LiteralPath $fixtureRoot -Recurse -Force
    }
}

$result = if ($failures -eq 0) { 'PASS' } else { 'FAIL' }
Write-Output "TEST-FINDTESTPROJECT Result=$result"
if ($failures -ne 0) { exit 1 }
