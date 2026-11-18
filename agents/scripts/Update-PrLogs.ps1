<#
.SYNOPSIS
    Fetches every pull request from the Huddle Gitea repo and regenerates PRs.md and Prs-Feature.md.

.DESCRIPTION
    Step 1 of docs/FeaturesSummary/UPDATING.md. Everything it writes goes to private/feature-logs/, is
    CRLF, and has email addresses, the local Windows user path and the Gitea host redacted, because
    these files are tracked and the secret scan reads them. It:
      1. reads every PR (open, closed, merged) through the Gitea API;
      2. rewrites PRs.md, all PR descriptions, oldest first;
      3. appends every PR not yet in pr-classification.csv to it with kind 'unclassified';
      4. rewrites Prs-Feature.md from the PRs whose kind is 'feature';
      5. writes Conversation/New-PRs.md (gitignored, rebuilt each run), the digest of the unclassified
         PRs, which is all the agent needs to read: a routine PR by title, any other in full;
      6. prints the digest's size.

    It never edits feature-list.md; that is the agent's judgement work.

.PARAMETER BaseUrl
    The Gitea server. Defaults to the host of the `origin` remote.

.EXAMPLE
    pwsh -NoProfile -File agents/scripts/Update-PrLogs.ps1
#>
[CmdletBinding()]
param(
    [string] $BaseUrl = '',
    [string] $Repo = 'emre/Huddle'
)

$ErrorActionPreference = 'Stop'
$repoRoot = (git -C $PSScriptRoot rev-parse --show-toplevel).Trim()
$dir = Join-Path $repoRoot 'private/feature-logs'
$utf8 = [Text.UTF8Encoding]::new($false)

# The host is read from the remote so no tracked file names it (agents/GiteaOperations.md).
$origin = [Uri](git -C $repoRoot remote get-url origin).Trim()
if (-not $BaseUrl) { $BaseUrl = "$($origin.Scheme)://$($origin.Host):$($origin.Port)" }

function Write-Crlf([string] $path, [string] $text) {
    [IO.File]::WriteAllText($path, ($text -replace "`r?`n", "`r`n"), $utf8)
}

# Masks what must not reach a tracked file: PR text quotes email addresses, local paths and the host.
function Get-Redacted([string] $text) {
    if ([string]::IsNullOrEmpty($text)) { return $text }
    $emails = [regex]::Replace($text, '[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}', {
        param($m)
        if ($m.Value -match 'noreply@|@users\.noreply\.|@(.*\.)?example\.(com|org)$') { $m.Value } else { 'user@example.com' }
    })
    $paths = [regex]::Replace($emails, "Users[\\/]{1,2}$([regex]::Escape($env:USERNAME))\b", 'Users/user', 'IgnoreCase')
    return [regex]::Replace($paths, [regex]::Escape($origin.Host), 'gitea-host.example', 'IgnoreCase')
}

$token = [Environment]::GetEnvironmentVariable('GITEA_ACCESS_TOKEN', 'User')
if (-not $token) { $token = $env:GITEA_ACCESS_TOKEN }
if (-not $token) { throw 'GITEA_ACCESS_TOKEN is not set (a Windows User-scope environment variable).' }
$headers = @{ Authorization = "token $token" }

# 1. Fetch every PR, 50 a page, until a page comes back empty.
$all = [System.Collections.Generic.List[object]]::new()
$page = 1
do {
    # Invoke-RestMethod emits a JSON array as one object, so count it as returned; wrapping it
    # in @() would make an empty page count as 1 and loop forever.
    $batch = Invoke-RestMethod "$BaseUrl/api/v1/repos/$Repo/pulls?state=all&limit=50&page=$page" -Headers $headers
    foreach ($pr in $batch) { $all.Add($pr) }
    $page++
} while ($batch.Count -gt 0)

$prs = $all |
    Select-Object number, state, merged, created_at,
        @{ n = 'title'; e = { Get-Redacted $_.title } }, @{ n = 'body'; e = { Get-Redacted $_.body } },
        @{ n = 'head'; e = { $_.head.ref } }, @{ n = 'base'; e = { $_.base.ref } } |
    Sort-Object number

function Get-Body([object] $pr) {
    if ([string]::IsNullOrWhiteSpace($pr.body)) { return '_No description._' }
    # Push the description's own headings down two levels so they nest under the PR heading.
    return (($pr.body -replace "`r`n", "`n").Trim() -replace '(?m)^(#{1,4}) ', '$1## ')
}

function Get-Date10([object] $pr) { ([datetime]$pr.created_at).ToString('yyyy-MM-dd') }

$today = (Get-Date).ToString('yyyy-MM-dd')

# 2. PRs.md
$sb = [Text.StringBuilder]::new()
[void]$sb.AppendLine("# Huddle pull requests`r`n")
[void]$sb.AppendLine("All $($prs.Count) pull requests on ``$Repo`` (Gitea), oldest first, extracted $today.`r`n")
foreach ($p in $prs) {
    $status = if ($p.merged) { 'merged' } elseif ($p.state -eq 'open') { 'open' } else { 'closed' }
    [void]$sb.AppendLine("## #$($p.number) $($p.title)`r`n")
    [void]$sb.AppendLine("- **Status:** $status")
    [void]$sb.AppendLine("- **Opened:** $(Get-Date10 $p)")
    [void]$sb.AppendLine("- **Branch:** ``$($p.head)`` → ``$($p.base)```r`n")
    [void]$sb.AppendLine((Get-Body $p))
    [void]$sb.AppendLine("`r`n---`r`n")
}
Write-Crlf "$dir/PRs.md" $sb.ToString()

# 3. Append new PRs to the classification as 'unclassified'.
$csvPath = "$dir/pr-classification.csv"
$rows = [System.Collections.Generic.List[object]]::new()
if (Test-Path $csvPath) { $rows.AddRange(@(Import-Csv $csvPath)) }
$known = [System.Collections.Generic.HashSet[int]]::new()
$merged = [System.Collections.Generic.HashSet[int]]::new()
foreach ($p in $prs) { if ($p.merged) { [void]$merged.Add([int]$p.number) } }
foreach ($r in $rows) {
    [void]$known.Add([int]$r.number)
    # A PR recorded as unmerged that has since merged needs a fresh look.
    if ($r.kind -eq 'unmerged' -and $merged.Contains([int]$r.number)) { $r.kind = 'unclassified' }
}
foreach ($p in $prs) {
    if (-not $known.Contains([int]$p.number)) {
        $rows.Add([pscustomobject]@{ number = $p.number; kind = 'unclassified'; area = ''; title = $p.title })
    }
}
$rows | Sort-Object { [int]$_.number } | Export-Csv $csvPath -NoTypeInformation -Encoding utf8
Write-Crlf $csvPath ([IO.File]::ReadAllText($csvPath))

# 4. Prs-Feature.md from the 'feature' rows.
$featureNumbers = [System.Collections.Generic.HashSet[int]]::new()
foreach ($r in $rows) { if ($r.kind -eq 'feature') { [void]$featureNumbers.Add([int]$r.number) } }
$features = @($prs | Where-Object { $featureNumbers.Contains([int]$_.number) })

$sb = [Text.StringBuilder]::new()
[void]$sb.AppendLine("# Huddle feature log`r`n")
[void]$sb.AppendLine("The $($features.Count) pull requests on ``$Repo`` that added a feature, oldest first, taken from [PRs.md](PRs.md) on $today. The selection is the ``feature`` rows of [pr-classification.csv](pr-classification.csv). Bug fixes, docs, tests, CI and analyzer work, refactors, renames, visual polish passes, seed-data additions and small changes to an existing feature are left out.`r`n")
[void]$sb.AppendLine("## Index`r`n")
[void]$sb.AppendLine("| PR | Opened | Feature |`r`n| --- | --- | --- |")
foreach ($p in $features) { [void]$sb.AppendLine("| #$($p.number) | $(Get-Date10 $p) | $($p.title -replace '\|', '\|') |") }
[void]$sb.AppendLine('')
foreach ($p in $features) {
    [void]$sb.AppendLine("## #$($p.number) $($p.title)`r`n")
    [void]$sb.AppendLine("- **Opened:** $(Get-Date10 $p)")
    [void]$sb.AppendLine("- **Branch:** ``$($p.head)`` → ``$($p.base)```r`n")
    [void]$sb.AppendLine((Get-Body $p))
    [void]$sb.AppendLine("`r`n---`r`n")
}
Write-Crlf "$dir/Prs-Feature.md" $sb.ToString()

# 5. New-PRs.md, the digest. A title can mislead, so only these prefixes lose their description.
$open = @($prs | Where-Object { $_.state -eq 'open' })
$todo = @($rows | Where-Object { $_.kind -eq 'unclassified' })
$todoNumbers = [System.Collections.Generic.HashSet[int]]::new()
foreach ($t in $todo) { [void]$todoNumbers.Add([int]$t.number) }
$new = @($prs | Where-Object { $_.merged -and $todoNumbers.Contains([int]$_.number) })
$routinePattern = '^(test|docs|refactor|chore|build|ci)(\(|:)'
$routine = @($new | Where-Object { $_.title -match $routinePattern })
$toRead = @($new | Where-Object { $_.title -notmatch $routinePattern })
$newOpen = @($open | Where-Object { $todoNumbers.Contains([int]$_.number) })

$sb = [Text.StringBuilder]::new()
[void]$sb.AppendLine("# New pull requests`r`n")
[void]$sb.AppendLine("The $($new.Count) merged PRs not yet classified, rebuilt by ``agents/scripts/Update-PrLogs.ps1`` on $today. Read this instead of ``PRs.md``: $($toRead.Count) in full below, $($routine.Count) by title.`r`n")
if ($routine.Count -gt 0) {
    [void]$sb.AppendLine("## Probably ``other``, by title`r`n")
    [void]$sb.AppendLine("Open one with a Grep of ``PRs.md`` only if its title looks like more than it says.`r`n")
    foreach ($p in $routine) { [void]$sb.AppendLine("- #$($p.number) $($p.title)") }
    [void]$sb.AppendLine('')
}
if ($newOpen.Count -gt 0) {
    [void]$sb.AppendLine("## Open, leave as ``unclassified```r`n")
    foreach ($p in $newOpen) { [void]$sb.AppendLine("- #$($p.number) $($p.title)") }
    [void]$sb.AppendLine('')
}
foreach ($p in $toRead) {
    [void]$sb.AppendLine("## #$($p.number) $($p.title)`r`n")
    [void]$sb.AppendLine((Get-Body $p))
    [void]$sb.AppendLine("`r`n---`r`n")
}
$digestDir = Join-Path $repoRoot 'Conversation'
[void](New-Item -ItemType Directory -Force -Path $digestDir)
$digestPath = Join-Path $digestDir 'New-PRs.md'
Write-Crlf $digestPath $sb.ToString()

# 6. Report. The digest holds the list, so it is not printed here too.
Write-Host "PRs: $($prs.Count) (open: $($open.Count)); features: $($features.Count)"
if ($todo.Count -eq 0) {
    Write-Host 'Nothing to classify.'
}
else {
    Write-Host "To classify: $($toRead.Count) to read, $($routine.Count) by title, $($newOpen.Count) open."
    Write-Host "Digest: $digestPath, about $([int]($sb.Length / 4)) tokens."
}
