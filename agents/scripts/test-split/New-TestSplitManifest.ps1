<#
.SYNOPSIS
    Classifies every file under tests/Huddle.Tests into a split target (test-split plan S3, Appendix A).

.DESCRIPTION
    Per file, first match wins. Never classified: TestAssemblyInitializer.cs, GlobalUsings.cs,
    *AssemblySetupTests.cs, xunit.runner.json and *.csproj. Prints a per-target summary, or with
    -PassThru returns the rows (Path, Target, Tests) instead.
#>
param([string]$Root = 'tests/Huddle.Tests', [string]$Out, [switch]$PassThru)
$ErrorActionPreference = 'Stop'
$rootFull = (Resolve-Path $Root).Path
$rows = foreach ($f in Get-ChildItem $rootFull -Recurse -File | Where-Object {
        $_.FullName -notmatch '[\\/](bin|obj|TestResults)[\\/]' -and
        $_.Name -notin 'TestAssemblyInitializer.cs', 'GlobalUsings.cs', 'xunit.runner.json' -and
        $_.Name -notlike '*AssemblySetupTests.cs' -and
        $_.Extension -ne '.csproj' }) {
    $rel = $f.FullName.Substring($rootFull.Length + 1)
    $isCode = $f.Extension -in '.cs', '.razor'
    $text = if ($isCode) { Get-Content -Raw $f.FullName } else { '' }
    $isTest = $isCode -and $text -match '\[(Fact|Theory)'
    $target =
        if (-not $isCode) { 'Content' }
        elseif (-not $isTest) { 'Common' }
        elseif ($text -match '\bTeamWebApplicationFactory\b') { 'Hosted' }
        elseif ($text -match '\b(PipeHostFixture|MockAdapterFixture|FakeAgentProcess\w*|ProcessStartInfo)\b|Process\.Start') { 'Agents' }
        elseif ($text -match '\b(MudBunitContext|BunitContext)\b|\.Render<|RenderComponent<') { 'Components' }
        elseif ($rel -like 'SkillPacks\*') { 'SkillPacks' }
        else { 'Domain' }
    $tests = if ($isTest) { ([regex]::Matches($text, '\[(Fact|Theory)')).Count } else { 0 }
    [pscustomobject]@{ Path = $rel; Target = $target; Tests = $tests }
}
if ($Out) { $rows | Sort-Object Target, Path | Export-Csv -NoTypeInformation -Path $Out }
if ($PassThru) { return $rows }
$rows | Group-Object Target | ForEach-Object { '{0,-11} files={1,4} tests={2,5}' -f $_.Name, $_.Count, ($_.Group | Measure-Object Tests -Sum).Sum }
