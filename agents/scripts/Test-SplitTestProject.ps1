<#
.SYNOPSIS
    Pins Split-TestProject.ps1 (Design 5.1 and 5.2) against a fixture repo built under a temp root.

.DESCRIPTION
    Builds a real git repo with tests/Huddle.Tests.Domain (A/X1Tests.cs, A/B/X2Tests.cs, Ui/Y1Tests.cs,
    Ui/Z/ZTests.cs, Ui/Override1Tests.cs, Q/data.txt, GlobalUsings.cs), an empty tests/Huddle.Tests.Components
    (csproj only) and a small map (.psd1) inside the fixture. Then runs

        pwsh -NoProfile -File <ScriptPath> -Root <fixture> -MapPath <map> ...

    and checks the resolution order (Files, longest Folders prefix, Fallback, unmapped), the report,
    the refusals and the git renames. Exits 1 on any failure, otherwise 0. The last output line is
        TEST-SPLITTESTPROJECT Result=PASS|FAIL

.PARAMETER ScriptPath
    The script under test. Defaults to Split-TestProject.ps1 next to this file.

.EXAMPLE
    pwsh agents/scripts/Test-SplitTestProject.ps1
#>
[CmdletBinding()]
param(
    [string]$ScriptPath = (Join-Path $PSScriptRoot 'Split-TestProject.ps1')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ("split-test-project-" + [guid]::NewGuid().ToString('N'))
$mapPath = Join-Path $fixtureRoot 'test-split-map.psd1'
$failures = 0

function Write-FixtureFile {
    param([string]$RelativePath, [string]$Content)
    $path = Join-Path $fixtureRoot $RelativePath
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $path) | Out-Null
    Set-Content -LiteralPath $path -Value $Content
}

function Invoke-Git {
    param([string[]]$Arguments)
    $output = & git -C $fixtureRoot @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "git $($Arguments -join ' ') failed: $output"
    }
    return @($output | ForEach-Object { "$_" })
}

function Invoke-Split {
    param([string[]]$Arguments)
    $output = & pwsh -NoProfile -File $ScriptPath -Root $fixtureRoot -MapPath $mapPath @Arguments 2>&1
    $code = $LASTEXITCODE
    $lines = @($output | ForEach-Object { "$_".Trim().Replace('\', '/') } | Where-Object { $_.Length -gt 0 })
    return [pscustomobject]@{ Code = $code; Lines = $lines; Text = ($lines -join "`n") }
}

function Assert-Case {
    param([string]$Name, [bool]$Condition, [string]$Detail)
    if ($Condition) {
        Write-Output "PASS $Name"
    }
    else {
        $script:failures++
        Write-Output "FAIL $Name $Detail"
    }
}

function Test-Lists {
    param($Result, [string]$RelativePath)
    return @($Result.Lines | Where-Object { $_.Contains($RelativePath) }).Count -gt 0
}

if (-not (Test-Path -LiteralPath $ScriptPath)) {
    Write-Output "FAIL script under test is missing: $ScriptPath"
    Write-Output 'TEST-SPLITTESTPROJECT Result=FAIL'
    exit 1
}

function Get-FeatureCounts {
    # Expected -Report line shape (numbers are matched, not the exact formatting):
    #   F1  old: <files> files, <n> tests  new: <n> tests
    param($Result, [string]$Feature)
    $line = @($Result.Lines | Where-Object { $_ -match "^$Feature\b" }) | Select-Object -First 1
    if ($null -eq $line -or $line -notmatch 'old:\s*(\d+)\s*files,\s*(\d+)\s*tests\s+new:\s*(\d+)\s*tests') {
        return $null
    }
    return [pscustomobject]@{ OldFiles = [int]$Matches[1]; OldTests = [int]$Matches[2]; NewTests = [int]$Matches[3] }
}

try {
    $domain = 'tests/Huddle.Tests.Domain'
    Write-FixtureFile "$domain/Huddle.Tests.Domain.csproj" '<Project />'
    # Known attribute counts: X1Tests 2 facts, Y1Tests 1 theory (F1 total 3); data.txt and GlobalUsings.cs none.
    Write-FixtureFile "$domain/A/X1Tests.cs" "public sealed class X1Tests`n{`n    [Fact]`n    public void M1() { }`n`n    [Fact]`n    public void M2() { }`n}"
    Write-FixtureFile "$domain/A/B/X2Tests.cs" 'public sealed class X2Tests { [Fact] public void M() { } }'
    Write-FixtureFile "$domain/Ui/Y1Tests.cs" "public sealed class Y1Tests`n{`n    [Theory]`n    public void M(int x) { }`n}"
    Write-FixtureFile "$domain/Ui/Z/ZTests.cs" 'public sealed class ZTests { [Fact] public void M() { } }'
    Write-FixtureFile "$domain/Ui/Override1Tests.cs" 'public sealed class Override1Tests { [Fact] public void M() { } }'
    Write-FixtureFile "$domain/Q/data.txt" 'data'
    Write-FixtureFile "$domain/GlobalUsings.cs" 'global using Xunit;'
    Write-FixtureFile 'tests/Huddle.Tests.Components/Huddle.Tests.Components.csproj' '<Project />'

    Write-FixtureFile 'test-split-map.psd1' @'
@{
    Folders   = @{
        Domain     = @{ 'A' = 'F1'; 'A/B' = 'F2'; 'Q' = 'F1' }
        Components = @{ }
    }
    Files     = @{
        Domain     = @{ 'Ui/Override1Tests.cs' = 'F2' }
        Components = @{ }
    }
    RootFiles = @{
        Domain     = @{ }
        Components = @{ }
    }
    Keep      = @('GlobalUsings.cs')
    Fallback  = @{
        Domain     = @{ 'Ui' = 'F1' }
        Components = @{ }
    }
}
'@

    Invoke-Git @('init', '-q') | Out-Null
    Invoke-Git @('config', 'user.email', 'fixture@example.invalid') | Out-Null
    Invoke-Git @('config', 'user.name', 'fixture') | Out-Null
    Invoke-Git @('config', 'core.autocrlf', 'false') | Out-Null
    Invoke-Git @('add', '-A') | Out-Null
    Invoke-Git @('commit', '-q', '-m', 'fixture') | Out-Null

    # -Report: Ui/Z/ZTests.cs has no Files entry, no Folders prefix and is not directly in Ui, so it is unmapped.
    $report = Invoke-Split @('-Report')
    Assert-Case 'report lists unmapped Ui/Z/ZTests.cs' (Test-Lists $report 'Ui/Z/ZTests.cs') $report.Text
    Assert-Case 'report exits 1 with an unmapped file' ($report.Code -eq 1) "exit=$($report.Code)"
    $before = Get-FeatureCounts $report 'F1'
    Assert-Case 'report F1 line before apply: 3 files, 3 tests still in old project' (
        ($null -ne $before) -and ($before.OldFiles -eq 3) -and ($before.OldTests -eq 3)) $report.Text

    # -Feature F2: longest prefix (A/B beats A) and file entry (Files beats Fallback).
    $f2 = Invoke-Split @('-Feature', 'F2')
    Assert-Case 'F2 lists A/B/X2Tests.cs (longest prefix wins)' (Test-Lists $f2 'A/B/X2Tests.cs') $f2.Text
    Assert-Case 'F2 lists Ui/Override1Tests.cs (file wins)' (Test-Lists $f2 'Ui/Override1Tests.cs') $f2.Text
    Assert-Case 'F2 does not list A/X1Tests.cs' (-not (Test-Lists $f2 'A/X1Tests.cs')) $f2.Text
    Assert-Case 'F2 does not list Ui/Y1Tests.cs' (-not (Test-Lists $f2 'Ui/Y1Tests.cs')) $f2.Text
    Assert-Case 'F2 exits 0' ($f2.Code -eq 0) "exit=$($f2.Code)"

    # -Feature F1: folder, fallback and non-.cs data; never a Keep file, never F2's files.
    $f1 = Invoke-Split @('-Feature', 'F1')
    Assert-Case 'F1 lists A/X1Tests.cs' (Test-Lists $f1 'A/X1Tests.cs') $f1.Text
    Assert-Case 'F1 lists Ui/Y1Tests.cs (fallback)' (Test-Lists $f1 'Ui/Y1Tests.cs') $f1.Text
    Assert-Case 'F1 lists Q/data.txt (data moves with its folder)' (Test-Lists $f1 'Q/data.txt') $f1.Text
    Assert-Case 'F1 never lists GlobalUsings.cs' (-not (Test-Lists $f1 'GlobalUsings.cs')) $f1.Text
    Assert-Case 'F1 does not list A/B/X2Tests.cs' (-not (Test-Lists $f1 'A/B/X2Tests.cs')) $f1.Text
    Assert-Case 'F1 does not list Ui/Override1Tests.cs' (-not (Test-Lists $f1 'Ui/Override1Tests.cs')) $f1.Text
    Assert-Case 'F1 does not list Ui/Z/ZTests.cs' (-not (Test-Lists $f1 'Ui/Z/ZTests.cs')) $f1.Text

    # -Apply without the target csproj: refuses and moves nothing.
    $refused = Invoke-Split @('-Feature', 'F1', '-Apply')
    $statusAfterRefusal = @(Invoke-Git @('status', '--porcelain'))
    Assert-Case 'apply without csproj exits 1' ($refused.Code -eq 1) "exit=$($refused.Code) $($refused.Text)"
    Assert-Case 'apply without csproj moves nothing' (
        ($statusAfterRefusal.Count -eq 0) -and
        (Test-Path -LiteralPath (Join-Path $fixtureRoot "$domain/A/X1Tests.cs")) -and
        (-not (Test-Path -LiteralPath (Join-Path $fixtureRoot 'tests/Huddle.Tests.F1/A/X1Tests.cs')))) ($statusAfterRefusal -join '|')

    # -Apply with the csproj: files move as git renames.
    Write-FixtureFile 'tests/Huddle.Tests.F1/Huddle.Tests.F1.csproj' '<Project />'
    $applied = Invoke-Split @('-Feature', 'F1', '-Apply')
    $status = @(Invoke-Git @('status', '--porcelain') | ForEach-Object { $_.Replace('\', '/') })
    $renames = @($status | Where-Object { $_ -match '^R' })
    Assert-Case 'apply exits 0' ($applied.Code -eq 0) "exit=$($applied.Code) $($applied.Text)"
    Assert-Case 'apply prints one line: moved 3 files' (
        ($applied.Lines.Count -eq 1) -and ($applied.Lines[0] -eq 'moved 3 files to tests/Huddle.Tests.F1/')) $applied.Text
    Assert-Case 'apply moves A/X1Tests.cs' (Test-Path -LiteralPath (Join-Path $fixtureRoot 'tests/Huddle.Tests.F1/A/X1Tests.cs')) ($status -join '|')
    Assert-Case 'apply moves Ui/Y1Tests.cs' (Test-Path -LiteralPath (Join-Path $fixtureRoot 'tests/Huddle.Tests.F1/Ui/Y1Tests.cs')) ($status -join '|')
    Assert-Case 'apply moves Q/data.txt' (Test-Path -LiteralPath (Join-Path $fixtureRoot 'tests/Huddle.Tests.F1/Q/data.txt')) ($status -join '|')
    Assert-Case 'apply leaves GlobalUsings.cs' (Test-Path -LiteralPath (Join-Path $fixtureRoot "$domain/GlobalUsings.cs")) ($status -join '|')
    Assert-Case 'apply leaves other features files' (
        (Test-Path -LiteralPath (Join-Path $fixtureRoot "$domain/A/B/X2Tests.cs")) -and
        (Test-Path -LiteralPath (Join-Path $fixtureRoot "$domain/Ui/Override1Tests.cs"))) ($status -join '|')
    Assert-Case 'git status shows three R renames' ($renames.Count -eq 3) ($status -join '|')
    Assert-Case 'rename for A/X1Tests.cs' (@($renames | Where-Object { $_.Contains('A/X1Tests.cs') -and $_.Contains('Huddle.Tests.F1/A/X1Tests.cs') }).Count -eq 1) ($status -join '|')
    Assert-Case 'rename for Q/data.txt' (@($renames | Where-Object { $_.Contains('Huddle.Tests.F1/Q/data.txt') }).Count -eq 1) ($status -join '|')

    # -Report after the move: F1 has 0 files and 0 tests left in the old project, 3 tests in the new one.
    $afterReport = Invoke-Split @('-Report')
    $after = Get-FeatureCounts $afterReport 'F1'
    Assert-Case 'report F1 line after apply: old 0 files, 0 tests' (
        ($null -ne $after) -and ($after.OldFiles -eq 0) -and ($after.OldTests -eq 0)) $afterReport.Text
    Assert-Case 'report F1 line after apply: new 3 tests' (($null -ne $after) -and ($after.NewTests -eq 3)) $afterReport.Text

    # A second -Apply for the same feature has nothing left to move.
    $again = Invoke-Split @('-Feature', 'F1', '-Apply')
    Assert-Case 'second apply exits 0' ($again.Code -eq 0) "exit=$($again.Code) $($again.Text)"
    Assert-Case 'second apply lists nothing' (
        (-not (Test-Lists $again 'X1Tests.cs')) -and (-not (Test-Lists $again 'Y1Tests.cs')) -and (-not (Test-Lists $again 'data.txt'))) $again.Text

    # -Create: step 1 of the feature task, from templates embedded in the script (no Domain template files).
    $runnerText = "{`r`n  `"`$schema`": `"https://xunit.net/schema/v3/xunit.runner.schema.json`",`r`n  `"maxParallelThreads`": `"1x`"`r`n}`r`n"
    $slnxText = "<Solution>`r`n  <Folder Name=`"/tests/`">`r`n    <Project Path=`"tests/Huddle.Tests.Domain/Huddle.Tests.Domain.csproj`" />`r`n  </Folder>`r`n</Solution>`r`n"
    [IO.File]::WriteAllText((Join-Path $fixtureRoot 'Huddle.slnx'), $slnxText)
    $propsText = "<Project>`r`n  <ItemGroup>`r`n    <InternalsVisibleTo Include=`"Huddle.Tests.Domain`" />`r`n  </ItemGroup>`r`n</Project>`r`n"
    [IO.File]::WriteAllText((Join-Path $fixtureRoot 'tests/Huddle.TestAssemblies.props'), $propsText)

    $created = Invoke-Split @('-Feature', 'F3', '-Create')
    $f3Dir = Join-Path $fixtureRoot 'tests/Huddle.Tests.F3'
    $f3Csproj = Join-Path $f3Dir 'Huddle.Tests.F3.csproj'
    $f3Setup = Join-Path $f3Dir 'F3AssemblySetupTests.cs'
    Assert-Case 'create exits 0' ($created.Code -eq 0) "exit=$($created.Code) $($created.Text)"
    Assert-Case 'create writes the two-line csproj' (
        (Test-Path -LiteralPath $f3Csproj) -and
        ([IO.File]::ReadAllText($f3Csproj) -eq "<Project Sdk=`"Microsoft.NET.Sdk`">`r`n  <Import Project=`"..\Huddle.Tests.Feature.props`" />`r`n</Project>`r`n")) $created.Text
    Assert-Case 'create writes xunit.runner.json' (
        (Test-Path -LiteralPath (Join-Path $f3Dir 'xunit.runner.json')) -and
        ([IO.File]::ReadAllText((Join-Path $f3Dir 'xunit.runner.json')) -eq $runnerText)) $created.Text
    $setupCopy = if (Test-Path -LiteralPath $f3Setup) { [IO.File]::ReadAllText($f3Setup) } else { '' }
    Assert-Case 'create writes F3AssemblySetupTests.cs with class and summary renamed' (
        $setupCopy.Contains('public sealed class F3AssemblySetupTests') -and $setupCopy.Contains('<c>Huddle.Tests.F3</c>') -and
        (-not $setupCopy.Contains('__NAME__')) -and (-not [regex]::IsMatch($setupCopy, '(?<!\r)\n'))) $setupCopy
    $slnxAfter = [IO.File]::ReadAllText((Join-Path $fixtureRoot 'Huddle.slnx'))
    Assert-Case 'create adds the slnx Project line after the last tests/ line' (
        $slnxAfter.Contains("    <Project Path=`"tests/Huddle.Tests.Domain/Huddle.Tests.Domain.csproj`" />`r`n    <Project Path=`"tests/Huddle.Tests.F3/Huddle.Tests.F3.csproj`" />`r`n  </Folder>")) $slnxAfter
    $propsAfter = [IO.File]::ReadAllText((Join-Path $fixtureRoot 'tests/Huddle.TestAssemblies.props'))
    Assert-Case 'create adds the InternalsVisibleTo line before </ItemGroup>' (
        $propsAfter.Contains("    <InternalsVisibleTo Include=`"Huddle.Tests.F3`" />`r`n  </ItemGroup>")) $propsAfter
    Assert-Case 'create keeps CRLF in slnx and props' (
        (-not [regex]::IsMatch($slnxAfter, '(?<!\r)\n')) -and (-not [regex]::IsMatch($propsAfter, '(?<!\r)\n'))) 'bare LF found'

    $createdAgain = Invoke-Split @('-Feature', 'F3', '-Create')
    Assert-Case 'second create exits 0' ($createdAgain.Code -eq 0) "exit=$($createdAgain.Code) $($createdAgain.Text)"
    Assert-Case 'second create changes nothing' (
        ([IO.File]::ReadAllText((Join-Path $fixtureRoot 'Huddle.slnx')) -eq $slnxAfter) -and
        ([IO.File]::ReadAllText((Join-Path $fixtureRoot 'tests/Huddle.TestAssemblies.props')) -eq $propsAfter)) $createdAgain.Text

    Remove-Item -LiteralPath $f3Setup -Force
    $partial = Invoke-Split @('-Feature', 'F3', '-Create')
    Assert-Case 'create refuses when only some pieces exist' ($partial.Code -eq 1) "exit=$($partial.Code) $($partial.Text)"
}
catch {
    $failures++
    Write-Output "FAIL setup/run: $($_.Exception.Message)"
}
finally {
    if (Test-Path -LiteralPath $fixtureRoot) {
        Remove-Item -LiteralPath $fixtureRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}

$result = if ($failures -eq 0) { 'PASS' } else { 'FAIL' }
Write-Output "TEST-SPLITTESTPROJECT Result=$result"
if ($failures -ne 0) { exit 1 }
