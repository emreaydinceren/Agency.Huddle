<#
.SYNOPSIS
    Checks that the analyzer rules the build says are on still fire, and that the files recording them agree.

.DESCRIPTION
    Four checks, each reported as a count of failures:

    Probes    Builds tests/Huddle.AnalyzerProbes (not part of Huddle.slnx) with warnings allowed and
              requires every "// probe: <RuleId>" tag in Probes/*.cs to be answered by that rule
              firing within the tag's line or the next 8 lines of the same file. A tag that goes
              unanswered means the rule stopped firing (an analyzer upgrade, a config change).
    Coverage  Works out which Sonar rules are enabled (default-on, or opted in by .editorconfig, minus
              the NoWarn denylist) and requires each to have a probe or a line in
              tests/Huddle.AnalyzerProbes/Unprobed.txt giving the reason. An Unprobed line for a rule
              that now has a probe, or is no longer enabled, also fails, so the list cannot rot.
    Config    Directory.Build.props and .editorconfig: every NoWarn token is a well-formed ID (a glued
              token such as S6575S6640 silently re-enables two rules), none is duplicated, and no
              rule is both denylisted and given a severity (the severity would do nothing).
    Doc       docs/engineering/analyzer-decisions.md: every denylisted Sonar rule is named in it, and
              its "It is now N" denylist count matches the real one.

    The very last output line is always
        CHECK-ANALYZERS Probes=<n> Coverage=<n> Config=<n> Doc=<n>
    where 0 means ok. Exits 0 only if all four are 0, otherwise 1.

    -SkipBuild reuses the last probe build output (tests/Huddle.AnalyzerProbes/probe-build.log) and is for
    iterating on the script itself.

.EXAMPLE
    pwsh agents/scripts/Check-Analyzers.ps1
#>
[CmdletBinding()]
param([switch]$SkipBuild)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (git rev-parse --show-toplevel).Trim()
$probeDir = Join-Path $repoRoot 'tests/Huddle.AnalyzerProbes'
# Sonar treats a project that references a test framework as test code and skips most rules there, so
# the xunit-specific probes live in a second project.
$probeProjects = @('tests/Huddle.AnalyzerProbes', 'tests/Huddle.AnalyzerProbes.Tests') | ForEach-Object { Join-Path $repoRoot $_ }
$counts = [ordered]@{ Probes = 0; Coverage = 0; Config = 0; Doc = 0 }
$allowedReasons = @('no-technology', 'compiler-rejects', 'needs-package', 'probe-did-not-fire', 'not-yet-probed')

function Fail([string]$area, [string]$message) {
    Write-Host "$($area.ToUpperInvariant()) $message"
    $script:counts[$area]++
}

# ---- Config: Directory.Build.props ------------------------------------------------------------
$propsText = [System.IO.File]::ReadAllText((Join-Path $repoRoot 'Directory.Build.props'))
$noWarn = [System.Collections.Generic.List[string]]::new()
foreach ($m in [regex]::Matches($propsText, '<NoWarn>\$\(NoWarn\);([^<]*)</NoWarn>')) {
    foreach ($token in $m.Groups[1].Value.Split(';')) { $noWarn.Add($token.Trim()) }
}
$wellFormed = '^(CS\d{4}|NU\d{4}|CA\d{4}|IDE\d{4}|S\d{1,4}|S9999-[A-Za-z-]+)$'
$seenTokens = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
foreach ($token in $noWarn) {
    if ($token -cnotmatch $wellFormed) { Fail 'Config' "malformed NoWarn token '$token' in Directory.Build.props" }
    elseif (-not $seenTokens.Add($token)) { Fail 'Config' "NoWarn token '$token' appears twice in Directory.Build.props" }
}
$deniedSonar = @($noWarn | Where-Object { $_ -cmatch '^S\d{1,4}$' })

# ---- Config: .editorconfig --------------------------------------------------------------------
# Only global sections count; a path-scoped section (the ACP exemption) is a local override.
$severity = @{}
$inGlobalSection = $true
foreach ($line in [System.IO.File]::ReadAllLines((Join-Path $repoRoot '.editorconfig'))) {
    if ($line -match '^\[(.+)\]\s*$') {
        $inGlobalSection = $Matches[1] -match '^\*\.(\{cs,vb\}|cs)$'
        continue
    }
    if ($inGlobalSection -and $line -match '^dotnet_diagnostic\.([A-Za-z]+\d+)\.severity\s*=\s*(\w+)') {
        $severity[$Matches[1]] = $Matches[2]
    }
}
foreach ($id in $deniedSonar) {
    if ($severity.ContainsKey($id) -and $severity[$id] -in 'warning', 'error') {
        Fail 'Config' "$id is in NoWarn and also has severity '$($severity[$id])' in .editorconfig; NoWarn wins, so the severity does nothing"
    }
}

# ---- The Sonar catalogue ----------------------------------------------------------------------
$catalogue = @{}
$listing = & dotnet run (Join-Path $PSScriptRoot 'List-SonarRules.cs') 2>&1
foreach ($row in $listing) {
    $f = "$row" -split "`t"
    if ($f.Count -ge 4 -and $f[0] -cmatch '^S\d{1,4}$') { $catalogue[$f[0]] = @{ DefaultOn = ($f[2] -eq 'True'); Title = $f[3] } }
}
if ($catalogue.Count -lt 400) {
    Fail 'Coverage' "List-SonarRules.cs returned only $($catalogue.Count) rules; cannot work out which are enabled"
}
$enabled = [System.Collections.Generic.SortedSet[string]]::new([System.StringComparer]::Ordinal)
foreach ($id in $catalogue.Keys) {
    if ($id -in $deniedSonar) { continue }
    $sev = if ($severity.ContainsKey($id)) { $severity[$id] } else { $null }
    if ($sev -in 'warning', 'error') { [void]$enabled.Add($id) }
    elseif ($null -eq $sev -and $catalogue[$id].DefaultOn) { [void]$enabled.Add($id) }
}
foreach ($id in $severity.Keys) {
    if ($id -cmatch '^S\d{1,4}$' -and -not $catalogue.ContainsKey($id)) { Fail 'Config' "$id has an .editorconfig severity but is not a rule in the pinned Sonar package" }
}

# ---- Probes -----------------------------------------------------------------------------------
$logLines = @()
foreach ($project in $probeProjects) {
    $logPath = Join-Path $project 'probe-build.log'
    $csproj = Get-ChildItem -LiteralPath $project -Filter '*.csproj' | Select-Object -First 1
    if (-not $SkipBuild) {
        & dotnet build $csproj.FullName -v:q --no-incremental -nologo *>&1 | Out-File -LiteralPath $logPath -Encoding utf8
    }
    if (-not (Test-Path -LiteralPath $logPath)) { Fail 'Probes' "no probe build log for $($csproj.Name); run without -SkipBuild" }
    else { $logLines += [System.IO.File]::ReadAllLines($logPath) }
}

$fired = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
foreach ($l in $logLines) {
    if ($l -match '^(?<file>.+?)\((?<line>\d+),\d+\): (?:warning|error) (?<id>[A-Za-z]+\d+):') {
        [void]$fired.Add(('{0}|{1}|{2}' -f ([System.IO.Path]::GetFileName($Matches['file'])), $Matches['line'], $Matches['id']))
    }
}
if (-not ($logLines | Where-Object { $_ -match 'warning S\d+' })) { Fail 'Probes' 'the probe build printed no Sonar warning at all; it did not build or the analyzer did not load' }

$probed = [System.Collections.Generic.SortedSet[string]]::new([System.StringComparer]::Ordinal)
foreach ($file in $probeProjects | ForEach-Object { Get-ChildItem -LiteralPath (Join-Path $_ 'Probes') -Filter '*.cs' -Recurse }) {
    $lines = [System.IO.File]::ReadAllLines($file.FullName)
    for ($i = 0; $i -lt $lines.Length; $i++) {
        $ids = if ($lines[$i] -match '/\*\s*probe:\s*(?<ids>[A-Za-z0-9, ]+?)\s*\*/') { $Matches['ids'] }
               elseif ($lines[$i] -match '//\s*probe:\s*(?<ids>[A-Za-z0-9, ]+?)\s*$') { $Matches['ids'] }
               else { continue }
        foreach ($id in ($ids -split '[ ,]+' | Where-Object { $_ })) {
            [void]$probed.Add($id)
            $tagLine = $i + 1
            $hit = $false
            foreach ($candidate in $tagLine..($tagLine + 8)) {
                if ($fired.Contains(('{0}|{1}|{2}' -f $file.Name, $candidate, $id))) { $hit = $true; break }
            }
            if (-not $hit) { Fail 'Probes' "$($file.Name):$tagLine expected $id to fire within 8 lines and it did not" }
        }
    }
}

# ---- Coverage ---------------------------------------------------------------------------------
$unprobed = @{}
$unprobedPath = Join-Path $probeDir 'Unprobed.txt'
if (Test-Path -LiteralPath $unprobedPath) {
    foreach ($line in [System.IO.File]::ReadAllLines($unprobedPath)) {
        if ($line -match '^\s*(#|$)') { continue }
        if ($line -notmatch '^(?<id>S\d{1,4})\s+(?<reason>[a-z-]+)\s*$' -or $Matches['reason'] -notin $allowedReasons) {
            Fail 'Coverage' "Unprobed.txt: cannot read '$line' (expected '<RuleId> <$($allowedReasons -join '|')>')"
            continue
        }
        $unprobed[$Matches['id']] = $Matches['reason']
    }
}
foreach ($id in $enabled) {
    if (-not $probed.Contains($id) -and -not $unprobed.ContainsKey($id)) { Fail 'Coverage' "$id is enabled but has no probe and no line in Unprobed.txt" }
}
foreach ($id in $unprobed.Keys) {
    if ($probed.Contains($id)) { Fail 'Coverage' "$id is in Unprobed.txt but now has a probe; delete the line" }
    elseif (-not $enabled.Contains($id)) { Fail 'Coverage' "$id is in Unprobed.txt but is not an enabled Sonar rule; delete the line" }
}
foreach ($id in $probed) {
    if ($id -cmatch '^S\d{1,4}$' -and -not $enabled.Contains($id)) { Fail 'Coverage' "$id has a probe but is not enabled" }
}

# ---- Doc --------------------------------------------------------------------------------------
$docText = [System.IO.File]::ReadAllText((Join-Path $repoRoot 'docs/engineering/analyzer-decisions.md'))
foreach ($id in $deniedSonar) {
    if ($docText -notmatch "``$id``") { Fail 'Doc' "$id is denylisted but analyzer-decisions.md does not name it" }
}
$realCount = @($noWarn | Where-Object { $_ -cmatch '^S\d{1,4}$' -or $_ -cmatch '^S9999-' }).Count
if ($docText -match 'It is now (?<n>\d+):') {
    if ([int]$Matches['n'] -ne $realCount) { Fail 'Doc' "analyzer-decisions.md says the denylist is $($Matches['n']) entries; Directory.Build.props has $realCount" }
}
else { Fail 'Doc' "analyzer-decisions.md has no 'It is now <n>:' line to compare with the denylist" }

Write-Host ("enabled Sonar rules: {0}; probed: {1}; listed as unprobed: {2}" -f $enabled.Count, @($probed | Where-Object { $_ -cmatch '^S' }).Count, $unprobed.Count)
Write-Host ("CHECK-ANALYZERS Probes={0} Coverage={1} Config={2} Doc={3}" -f $counts.Probes, $counts.Coverage, $counts.Config, $counts.Doc)
if (($counts.Values | Measure-Object -Sum).Sum -eq 0) { exit 0 } else { exit 1 }
