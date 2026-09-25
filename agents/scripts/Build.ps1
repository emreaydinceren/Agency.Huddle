<#
.SYNOPSIS
    Runs `dotnet build` against the Huddle solution, saves full output to a timestamped log under
    Conversation\logs, and prints a de-duplicated summary of error/warning lines.

.DESCRIPTION
    Resolves the solution as Huddle.slnx at the current git worktree's root (via
    `git rev-parse --show-toplevel`), so it works unmodified from any worktree. The full `dotnet build`
    output is saved to <main checkout>\Conversation\logs\build-<timestamp>.log - Conversation/ is
    gitignored and always lives at the MAIN checkout's root, regardless of which worktree ran the build.
    The main checkout is resolved via `git rev-parse --path-format=absolute --git-common-dir` (its
    parent), so this works unmodified whether invoked from the main checkout or any worktree, on any
    machine, in any session; override with -MainRepoRoot or -LogDir directly if you need somewhere else.
    The console output is trimmed to the de-duplicated `error`/`warning` lines (with the trailing
    " [project]" segment stripped), the "N Warning(s)" / "N Error(s)" totals lines, and the log path.
    Exits with dotnet's own exit code.

.PARAMETER Solution
    Path to the .slnx/.sln to build. Defaults to Huddle.slnx at the current git worktree's root.

.PARAMETER MainRepoRoot
    Override for the main checkout's root (where Conversation\logs lives). Defaults to the parent of
    `git rev-parse --path-format=absolute --git-common-dir`.

.PARAMETER LogDir
    Override for the log directory. Defaults to Conversation\logs under -MainRepoRoot.

.EXAMPLE
    pwsh agents/scripts/Build.ps1

.EXAMPLE
    pwsh agents/scripts/Build.ps1 -Solution E:\Repos\Huddle\Huddle.slnx
#>
[CmdletBinding()]
param(
    [string]$Solution,
    [string]$MainRepoRoot,
    [string]$LogDir
)

$ErrorActionPreference = 'Stop'

# Conversation/ is gitignored and lives at the MAIN checkout's root, not necessarily the worktree this
# script is invoked from - logs always land under the main checkout's Conversation\logs regardless of
# which worktree ran the build. `git rev-parse --git-common-dir` resolves to the main checkout's .git
# directory even when run from a linked worktree, so its parent is the main checkout root in every case.
if (-not $MainRepoRoot) {
    $gitCommonDir = (git rev-parse --path-format=absolute --git-common-dir).Trim()
    $MainRepoRoot = (Resolve-Path -LiteralPath (Join-Path $gitCommonDir '..')).Path
}
if (-not $LogDir) {
    $LogDir = Join-Path $MainRepoRoot 'Conversation\logs'
}

if (-not $Solution) {
    $worktreeRoot = (git rev-parse --show-toplevel).Trim()
    $Solution = Join-Path $worktreeRoot 'Huddle.slnx'
}

if (-not (Test-Path -LiteralPath $Solution)) {
    throw "Solution not found: $Solution"
}

if (-not (Test-Path -LiteralPath $LogDir)) {
    New-Item -ItemType Directory -Force -Path $LogDir | Out-Null
}

$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$logPath = Join-Path $LogDir "build-$timestamp.log"

Write-Host "Running: dotnet build $Solution"
& dotnet build $Solution 2>&1 | Tee-Object -FilePath $logPath | Out-Null
$exitCode = $LASTEXITCODE

$lines = Get-Content -LiteralPath $logPath

$diagLines = New-Object System.Collections.Generic.List[string]
$totalLines = New-Object System.Collections.Generic.List[string]

foreach ($line in $lines) {
    # "<file>(<line>,<col>): error CS1234: message [E:\Repos\Huddle\src\Foo\Foo.csproj]"
    # "<file>(<line>,<col>): warning IDE0060: message [E:\Repos\Huddle\src\Foo\Foo.csproj]"
    if ($line -match '^(?<body>.*\s(error|warning)\s[A-Za-z]+\d*:.*?)\s*\[[^\[\]]*\]\s*$') {
        $diagLines.Add($Matches.body.Trim()) | Out-Null
        continue
    }

    # Also catch diagnostic lines with no trailing "[project]" segment.
    if ($line -match '\s(error|warning)\s[A-Za-z]+\d*:') {
        $diagLines.Add($line.Trim()) | Out-Null
        continue
    }

    # Totals lines, e.g. "    0 Warning(s)" / "    2 Error(s)"
    if ($line -match '^\s*\d+\s+(Warning|Error)\(s\)\s*$') {
        $totalLines.Add($line.Trim()) | Out-Null
    }
}

$uniqueDiagLines = @($diagLines | Select-Object -Unique)

Write-Host ''
if ($uniqueDiagLines.Count -gt 0) {
    Write-Host 'Diagnostics:'
    foreach ($d in $uniqueDiagLines) {
        Write-Host "  $d"
    }
}
else {
    Write-Host 'Diagnostics: (none)'
}

Write-Host ''
foreach ($t in $totalLines) {
    Write-Host $t
}

Write-Host ''
Write-Host "Log: $logPath"

exit $exitCode
