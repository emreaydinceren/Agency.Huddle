<#
.SYNOPSIS
    Measures allocation rate and allocation sites of a running Huddle.App under the LoadDriver workload.

.DESCRIPTION
    Expects Huddle.App already running in Release on the real Claude adapter against a fresh copy of the
    run template (see README.md), with browser tabs open on the loadbot Room, the Tasks board and the
    Library so their live re-renders run too. LoadDriver asks Grace to work Tasks and Library notes and
    loads the Tasks and Library pages once a second. Then:
      1. warms up for 30 s,
      2. records System.Runtime counters for 90 s while the workload runs  -> counters-<Label>.csv
      3. records a gc-verbose trace for 90 s while the workload runs        -> alloc-<Label>.nettrace
      4. ranks the trace                                                     -> alloc-<Label>.md
      5. summarises the counters                                             -> printed

.PARAMETER Label
    before or after (any word; it names the output files).

.PARAMETER IntervalMs
    How often LoadDriver posts. Keep it the same for every run you compare.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Label,
    [int]$IntervalMs = 15000,
    [int]$Seconds = 90,
    [string]$Pipe = 'huddle-perf',
    [string]$BaseUrl = 'http://localhost:5199/',
    [string[]]$Pages = @('tasks', 'tasks/item/PLAT-0012', 'library', 'teams/Platform')
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$out = $PSScriptRoot
$driver = Join-Path $repo 'tools/LoadDriver/bin/Release/net10.0/LoadDriver.dll'
$report = Join-Path $repo 'tools/AllocReport/bin/Release/net10.0/AllocReport.dll'

$appPid = (Get-Process Huddle.App -ErrorAction Stop | Select-Object -First 1).Id
Write-Host "Huddle.App PID $appPid"

Write-Host 'Warm-up (30 s: opens the Room Session and JITs the page renders)'
dotnet $driver $Pipe 30 $IntervalMs $BaseUrl @Pages | Write-Host

$span = [TimeSpan]::FromSeconds($Seconds)
$duration = '{0:D2}:{1:D2}:{2:D2}:{3:D2}' -f $span.Days, $span.Hours, $span.Minutes, $span.Seconds
$csv = Join-Path $out "counters-$Label.csv"
Remove-Item $csv -ErrorAction SilentlyContinue
Write-Host "Counters ($Seconds s)"
$counters = Start-Process dotnet-counters -PassThru -NoNewWindow -ArgumentList @(
    'collect', '--process-id', $appPid, '--counters', 'System.Runtime', '--refresh-interval', '1',
    '--duration', $duration, '--format', 'csv', '--output', $csv)
Start-Sleep -Seconds 1
dotnet $driver $Pipe ($Seconds - 2) $IntervalMs $BaseUrl @Pages | Write-Host
$counters.WaitForExit()

$trace = Join-Path $out "alloc-$Label.nettrace"
Remove-Item $trace -ErrorAction SilentlyContinue
Write-Host "Trace ($Seconds s)"
$tracer = Start-Process dotnet-trace -PassThru -NoNewWindow -ArgumentList @(
    'collect', '--process-id', $appPid, '--profile', 'gc-verbose', '--duration', $duration, '--output', $trace)
Start-Sleep -Seconds 2
dotnet $driver $Pipe ($Seconds - 4) $IntervalMs $BaseUrl @Pages | Write-Host
$tracer.WaitForExit()

dotnet $report $trace 25 Agency.Huddle | Set-Content (Join-Path $out "alloc-$Label.md")

# dotnet-counters csv: Timestamp,Provider,Counter Name,Counter Type,Mean/Increment
$rows = Import-Csv $csv
$alloc = $rows | Where-Object { $_.'Counter Name' -like 'dotnet.gc.heap.total_allocated*' }
$gen0 = $rows | Where-Object { $_.'Counter Name' -like 'dotnet.gc.collections*' -and $_.'Counter Name' -like '*gen0*' }
$pause = $rows | Where-Object { $_.'Counter Name' -like 'dotnet.gc.pause.time*' }
$valueColumn = ($rows[0].PSObject.Properties.Name)[-1]
function Sum($set) { ($set | ForEach-Object { [double]$_.$valueColumn } | Measure-Object -Sum).Sum }
$samples = [Math]::Max(1, ($alloc | Measure-Object).Count)
'{0}: alloc {1:N1} MB/s, gen0 {2:N2} GC/s, GC pause {3:N1} ms/s over {4} samples' -f $Label,
    ((Sum $alloc) / 1MB / $samples), ((Sum $gen0) / $samples), ((Sum $pause) * 1000 / $samples), $samples |
    Tee-Object -FilePath (Join-Path $out "counters-$Label.txt")
