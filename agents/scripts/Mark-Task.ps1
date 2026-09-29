<#
.SYNOPSIS
    Flips one or more Team Pages tracker rows to a state and optionally appends a line to the progress
    log.

.DESCRIPTION
    Calls Set-Tracker.ps1 (next to this script) with -TrackerPath docs/Huddle.TeamPages-Tracker.md and
    prints its one-line result. When -Note is given, also appends
        - <yyyy-MM-dd HH:mm> <ids> <state>: <note>
    to Conversation/teampages/progress-log.md (relative to the git worktree root), creating the file with
    a "# Progress log" header if it is missing. The append is a CRLF `[IO.File]::AppendAllText`.
    Throws (via Set-Tracker.ps1) on an unknown task id, before anything is logged.

.PARAMETER Task
    One or more task ids, e.g. "6.5.i" or "6.5.i,6.5.t". Comma-separated values are accepted.

.PARAMETER State
    The target state: Active, InProgress, or Done.

.PARAMETER Note
    Optional one-line note to append to the progress log.

.EXAMPLE
    pwsh agents/scripts/Mark-Task.ps1 -Task 6.5.i -State Done -Note "CSS landed, Check-All green"
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string[]]$Task,
    [Parameter(Mandatory)][ValidateSet('Active', 'InProgress', 'Done')][string]$State,
    [string]$Note
)

$ErrorActionPreference = 'Stop'

$repoRoot = (git rev-parse --show-toplevel).Trim()
$ids = (($Task -split ',') | ForEach-Object { $_.Trim() } | Where-Object { $_ }) -join ','
$trackerPath = Join-Path $repoRoot 'docs/Huddle.TeamPages-Tracker.md'

& (Join-Path $PSScriptRoot 'Set-Tracker.ps1') -TrackerPath $trackerPath -Task $ids -State $State

if ($Note) {
    $logPath = Join-Path $repoRoot 'Conversation/teampages/progress-log.md'
    if (-not (Test-Path -LiteralPath $logPath -PathType Leaf)) {
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $logPath) | Out-Null
        [IO.File]::WriteAllText($logPath, "# Progress log`r`n")
    }
    $stamp = Get-Date -Format 'yyyy-MM-dd HH:mm'
    [IO.File]::AppendAllText($logPath, "- $stamp $ids ${State}: $Note`r`n")
}
