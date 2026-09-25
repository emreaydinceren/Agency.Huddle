<#
.SYNOPSIS
    Flips a tracker table's Active/InProgress/Done checkmarks for one or more task ids, and refreshes its
    **Status:** summary line.

.DESCRIPTION
    Updates -TrackerPath in place, keeping its existing line endings (CRLF, read/written as a single
    `[IO.File]::ReadAllText`/`WriteAllText` round-trip split/joined on "`r`n" - do not point this at an
    LF file).

    The tracker file's format is assumed to be:
      - Task rows start with "| <id> " where <id> looks like "0.1", "1.1.t", "2.3.i" (matched by
        `^\| \d+\.\d+` when counting Done/In-Progress totals).
      - Retrospective rows start with "| 🔁 R<n> " where <n> is a number (matched by `^\| 🔁 R\d`).
      - Every row (task or retro) ends in exactly three status cells: "| | | |" with an optional "✔ " in
        each of the three (Active, InProgress, Done) - e.g. "| ✔ | | |" (Active), "| | ✔ | |"
        (InProgress), "| | | ✔ |" (Done). The regex `\| (✔ )?\| (✔ )?\| (✔ )?\|$` at the end of the row
        is replaced wholesale with the target state's three-cell block.
      - A single line starting with "**Status:**" holds the running summary, rewritten to
        "**Status:** N of M tasks Done · N of M retrospectives done · last updated yyyy-MM-dd." each run.
    A -Task id matching neither the task-row nor a row in the file throws (no silent no-op).

.PARAMETER Task
    One or more task/retro ids to update, e.g. "0.1", "1.1.t", "2.3.i", "R1". Comma-separated values in a
    single string are also accepted and split.

.PARAMETER State
    The target state for every id in -Task: Active, InProgress, or Done.

.PARAMETER TrackerPath
    Path to the tracker Markdown file to update, e.g. E:\Repos\Huddle\docs\Huddle.Tasks-Tracker.md.
    Required - there is no default, since this script is shared across features/projects, each with its
    own tracker file.

.EXAMPLE
    pwsh agents/scripts/Set-Tracker.ps1 -Task "1.2" -State InProgress -TrackerPath E:\Repos\Huddle\docs\Huddle.Tasks-Tracker.md

.EXAMPLE
    pwsh agents/scripts/Set-Tracker.ps1 -Task "1.2,1.3,R1" -State Done -TrackerPath docs\Huddle.Tasks-Tracker.md
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string[]]$Task,
    [Parameter(Mandatory)][ValidateSet('Active', 'InProgress', 'Done')][string]$State,
    [Parameter(Mandatory)][string]$TrackerPath
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $TrackerPath -PathType Leaf)) {
    throw "Tracker not found: $TrackerPath"
}

$Task = $Task -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ }
$lines = [IO.File]::ReadAllText($TrackerPath) -split "`r`n"
$cells = @{ Active = '| ✔ | | |'; InProgress = '| | ✔ | |'; Done = '| | | ✔ |' }
foreach ($t in $Task) {
    $prefix = if ($t -match '^R\d+$') { "| 🔁 $t " } else { "| $t " }
    $hit = $false
    for ($i = 0; $i -lt $lines.Length; $i++) {
        if ($lines[$i].StartsWith($prefix)) {
            $lines[$i] = $lines[$i] -replace '\| (✔ )?\| (✔ )?\| (✔ )?\|$', $cells[$State]
            $hit = $true
        }
    }
    if (-not $hit) { throw "Task $t not found" }
}
$tasks = $lines | Where-Object { $_ -match '^\| \d+\.\d+' }
$retros = $lines | Where-Object { $_ -match '^\| 🔁 R\d' }
$done = ($tasks | Where-Object { $_.EndsWith('| | | ✔ |') }).Count
$rdone = ($retros | Where-Object { $_.EndsWith('| | | ✔ |') }).Count
$ip = ($tasks | Where-Object { $_.EndsWith('| | ✔ | |') }).Count
$today = Get-Date -Format 'yyyy-MM-dd'
for ($i = 0; $i -lt $lines.Length; $i++) {
    if ($lines[$i].StartsWith('**Status:**')) {
        $lines[$i] = "**Status:** $done of $($tasks.Count) tasks Done · $rdone of $($retros.Count) retrospectives done · last updated $today."
    }
}
[IO.File]::WriteAllText($TrackerPath, ($lines -join "`r`n"))
"$($tasks.Count) tasks: $done Done, $ip In Progress; retros $rdone/$($retros.Count)"
