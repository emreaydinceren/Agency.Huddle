<#
.SYNOPSIS
    Audits a dispatched subagent's transcript for shell commands that are risky, redundant with the
    house scripts, or that a PreToolUse hook already blocked.

.DESCRIPTION
    Reads a subagent transcript - a JSONL file: one JSON object per line - user/assistant messages,
    tool_use content blocks, and tool_result content blocks keyed back to their tool_use by
    `tool_use_id` - and pulls out every Bash/PowerShell `tool_use` block's `input.command`.

    The transcript file is `agent-<AgentId>.jsonl` under a session's `subagents` folder:
    `%USERPROFILE%\.claude\projects\<repo-slug>\<session-id>\subagents\agent-<AgentId>.jsonl`, where
    `<repo-slug>` is the repo's working-copy path with each path separator turned into a dash (e.g.
    `E--Repos-Huddle` for `E:\Repos\Huddle`) and `<session-id>` is the top-level Claude Code session's
    own id (visible in its own transcript path, or via a session-listing tool). Pass either:
      - -TranscriptPath: the full path to that .jsonl file directly, or
      - -SessionDir plus -AgentId: the `...\subagents` folder plus the agent id; the transcript path is
        computed as `<SessionDir>\agent-<AgentId>.jsonl`.
    Exactly one of -TranscriptPath or -SessionDir must be given (with -AgentId required alongside
    -SessionDir).

    Flags a command when it matches any of:

      - Find      - a `find` invocation whose search root is outside the repo AND outside `.nuget`
                    (e.g. `find /`, `find /c`, `find /c/Users`, `find ~`, `find $HOME`, `find C:\`).
                    A root under `.nuget` (e.g. `find ~/.nuget/packages`) is not flagged.
      - Python    - a bare `python3` or `python` invocation.
      - SedInPlace  - `sed -i` / `sed --in-place`, whether or not a hook actually blocked it (this repo
                    is CRLF; sed rewrites it to LF - see agents/CSharpPrinciples.md and the project memory
                    on Fix-Crlf.ps1/CRLF).
      - EditBypass  - a command that reaches around the Edit tool to write a file directly, requiring
                    action the same as SedInPlace does. Two shapes trigger it: (1) a `python`/`python3`/
                    `perl` invocation whose command text also contains file-writing code - `open(` with a
                    'w'/'wb'/'a'/'ab' mode, `.write(`, `write_text(`, `write_bytes(`, `perl ... -pi`, or
                    `-i.bak`; (2) a shell redirect (`>` or `>>`) into a path under `src/` or `tests/`,
                    unless that path is also under the scratchpad, `/tmp`, `Conversation/logs` or
                    `Conversation/red` (those are legitimate scratch/log destinations, not a bypass). A
                    command can be both Python and EditBypass at once - e.g. a bare `python3` that also
                    writes a file - and both categories are listed, comma-joined, same as any other
                    multi-category command.
      - Git:<sub> - `git stash|reset|checkout|switch|commit|add|restore|clean`, except `git add -N` (a
                    no-content intent-to-add is harmless).
      - BareDotnet  - a `dotnet build` or `dotnet test` invocation NOT going through
                    agents/scripts/Run-Tests.ps1 or Build.ps1 (those scripts hold the
                    machine-global test mutex and summary parsing; a bare invocation bypasses both).
      - MainCheckout - a Read/Edit/Write/NotebookEdit tool_use whose `input.file_path` is under the
                    MAIN checkout's src\ or tests\ (case-insensitive, either slash direction, "Huddle\"
                    exactly - a "Huddle-wt\..." worktree never matches), OR a Bash/PowerShell command that
                    `cd`s or writes (via a `>`/`>>` redirect) into that same src/tests tree. Paths under
                    the main checkout's Conversation\ and under any `...-wt\` worktree are fine and never
                    match (a worktree is a separate checkout; Conversation is gitignored tooling). A Read
                    hitting this pattern is reported separately as MainCheckoutRead (see below) rather than
                    MainCheckout - it still causes an edit-review miss (a subagent that read the wrong
                    tree's file is liable to edit it too) but is not itself a write, so it does not fail
                    the exit code on its own.
      - MainCheckoutRead - the read-only counterpart of MainCheckout: a `Read` tool_use whose
                    `input.file_path` is under the main checkout's src/ or tests/. Reported for visibility
                    but never causes the exit code to fail on its own - see EXIT CODE below.
      - Heredoc   - a Bash command containing a heredoc marker (`<<'EOF'`, `<<EOF`, `<< EOF`, or the `-`
                    (strip-tabs) variant) or a `cat >`/`tee ` invocation, whose write target (from a
                    `>`/`>>` redirect, or the path after `tee`) resolves inside the repo (any subfolder of
                    the main checkout, including agents/scripts) or a worktree (a sibling `...-wt\...`
                    directory), UNLESS that target is under the scratchpad, `/tmp`, `Conversation/logs` or
                    `Conversation/red` (legitimate scratch/log destinations). Unlike MainCheckout, a
                    worktree target is NOT exempt here - a heredoc bypasses the Edit tool wherever it
                    writes. A heredoc with no detectable write target (e.g. piped to a command's stdin,
                    such as a `git commit -m "$(cat <<'EOF' ... EOF)"`) is not flagged - there is no file to
                    have bypassed the Edit tool for. Requires action.
      - HookBlocked - the tool_use's matching tool_result (by `tool_use_id`) contains the literal text
                    "hook error: Blocked" - i.e. a PreToolUse hook actually rejected the command, whatever
                    category it falls in above.
      - PathError - a Bash command containing a Windows drive path after `cd` (e.g. `cd E:\Repos\Huddle`) -
                    Bash wants `cd /e/Repos/Huddle`, and a drive-rooted path is orientation waste, not a
                    real risk - OR the tool_use's matching tool_result contains the literal text
                    "unexpected EOF while looking for matching" (checked the same way HookBlocked reads
                    the tool_result text) - a heredoc/quoting mismatch from the same orientation confusion.

    A command can carry more than one category (e.g. a blocked `sed -i` is both SedInPlace and
    HookBlocked); all are listed, comma-joined, in one table row.

    PathError and MainCheckoutRead are reported for visibility but never cause the exit code to fail on
    their own - see EXIT CODE below.

.PARAMETER AgentId
    The subagent id, e.g. "abe0ce951591bb76a" (matches the `agent-<AgentId>.jsonl` file name). Required
    unless -TranscriptPath is given directly.

.PARAMETER TranscriptPath
    The full path to the transcript .jsonl file. Mutually exclusive with -SessionDir; one of the two is
    required.

.PARAMETER SessionDir
    The session's `...\subagents` folder (see DESCRIPTION for how to find it). Used with -AgentId to
    compute `<SessionDir>\agent-<AgentId>.jsonl`. Mutually exclusive with -TranscriptPath.

.EXAMPLE
    pwsh agents/scripts/Audit-Transcript.ps1 -TranscriptPath C:\Users\me\.claude\projects\E--Repos-Huddle\8d4694b0-b113-4e1f-908b-f3bbd08b30dc\subagents\agent-abe0ce951591bb76a.jsonl

.EXAMPLE
    pwsh agents/scripts/Audit-Transcript.ps1 -SessionDir C:\Users\me\.claude\projects\E--Repos-Huddle\8d4694b0-b113-4e1f-908b-f3bbd08b30dc\subagents -AgentId abe0ce951591bb76a
#>
[CmdletBinding()]
param(
    [string]$AgentId,

    [string]$TranscriptPath,

    [string]$SessionDir
)

$ErrorActionPreference = 'Stop'

if ($TranscriptPath -and $SessionDir) {
    throw 'Pass either -TranscriptPath or -SessionDir (with -AgentId), not both.'
}

if (-not $TranscriptPath) {
    if (-not $SessionDir) {
        throw 'Either -TranscriptPath, or -SessionDir together with -AgentId, is required. See the help (-?) for how to find the session''s subagents folder.'
    }
    if (-not $AgentId) {
        throw '-AgentId is required alongside -SessionDir.'
    }
    $TranscriptPath = Join-Path $SessionDir "agent-$AgentId.jsonl"
}

if (-not (Test-Path -LiteralPath $TranscriptPath)) {
    throw "Transcript not found: $TranscriptPath"
}

# Search roots that count as "outside the repo and outside .nuget" for the Find category. Compared
# case-insensitively against the whitespace-trimmed first argument to `find`.
$script:BadFindRoots = @('/', '/c', '/c/users', '~', '$home', 'c:\')

# The MAIN checkout's root (not a worktree's), resolved from wherever THIS script is invoked, via the
# same `git rev-parse --path-format=absolute --git-common-dir` trick Build.ps1/Run-Tests.ps1 use (its
# parent is the main checkout root, whether invoked from the main checkout itself or a linked worktree).
# If this script isn't run from inside a git repo at all, MainCheckout/MainCheckoutRead simply never
# match (there is no path to compare against) rather than throwing.
$script:MainCheckoutRoot = $null
try {
    $gitCommonDir = (git rev-parse --path-format=absolute --git-common-dir 2>$null).Trim()
    if ($gitCommonDir) {
        $script:MainCheckoutRoot = (Resolve-Path -LiteralPath (Join-Path $gitCommonDir '..')).Path.TrimEnd('\')
    }
} catch {
    $script:MainCheckoutRoot = $null
}

<#
.SYNOPSIS
    True when $Path (a Windows drive-letter path or a Git-Bash `/e/...` style path, either slash
    direction) resolves under the MAIN checkout's src/ or tests/ folder, and not under its Conversation\
    or a worktree (a sibling `...-wt\...` directory). Used by the MainCheckout/MainCheckoutRead
    categories.

    "Main checkout" means $script:MainCheckoutRoot (resolved once, above, via `git rev-parse
    --git-common-dir` from wherever this script itself is invoked) - never a worktree, whatever that
    worktree's own folder is named. If $script:MainCheckoutRoot could not be resolved (this script run
    outside any git repo), this always returns $false rather than guessing from the path text.
#>
function Test-IsMainCheckoutSrcOrTestsPath {
    param([string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path) -or -not $script:MainCheckoutRoot) {
        return $false
    }

    $normalized = $Path.Trim('"''') -replace '/', '\'
    # Git-Bash spells the drive as a leading `\e\...` (post the / -> \ swap above); turn it back into `E:\`.
    $normalized = $normalized -replace '^\\([A-Za-z])\\', '$1:\'

    if ($normalized -notmatch '(?i)^[A-Za-z]:\\.*\\(src|tests)(\\|$)') {
        return $false
    }
    if ($normalized -match '(?i)\\Conversation(\\|$)') {
        return $false
    }

    return $normalized.StartsWith("$script:MainCheckoutRoot\", [StringComparison]::OrdinalIgnoreCase)
}

<#
.SYNOPSIS
    True when $Path resolves inside a repo checkout or a worktree directory, UNLESS it is under the
    scratchpad, /tmp, Conversation/logs or Conversation/red. Used by the Heredoc category, which - unlike
    MainCheckout - does NOT exempt worktrees: a heredoc bypasses the Edit tool wherever it writes.
#>
function Test-IsRepoOrWorktreeWriteTarget {
    param([string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return $false
    }

    $trimmed = $Path.Trim('"''')

    $isExempt = $trimmed -match '(?i)scratchpad' -or
                $trimmed -match '(?i)(^|[/\\])tmp([/\\]|$)' -or
                $trimmed -match '(?i)Conversation[/\\](logs|red)([/\\]|$)'
    if ($isExempt) {
        return $false
    }

    # A rooted path - `/dev/null`, `/tmp/...` (already exempted above), a Windows drive - needs to actually
    # resolve under a repo/worktree; only a bare RELATIVE path (no leading `/` or drive letter) gets the
    # "assumed inside the repo, since the command's cwd is there" benefit of the doubt below. Without this,
    # a completely unrelated absolute path like the `/dev/null` in a routine `2>/dev/null` would wrongly
    # read as "not a drive letter, so must be relative" and get flagged.
    $isRooted = $trimmed -match '^[/\\]' -or $trimmed -match '^[A-Za-z]:'
    if (-not $isRooted) {
        return $true
    }

    $normalized = $trimmed -replace '/', '\'
    $normalized = $normalized -replace '^\\([A-Za-z])\\', '$1:\'

    # A repo checkout or worktree, recognised heuristically: a Windows drive path with at least one
    # folder segment (the repo/worktree's own top folder name is not otherwise constrained).
    return $normalized -match '(?i)^[A-Za-z]:\\[^\\]+(\\|$)'
}

<#
.SYNOPSIS
    Returns the zero-or-more risk categories a single shell command line falls into (Find, Python,
    SedInPlace, EditBypass, Git:<sub>, BareDotnet, MainCheckout, Heredoc, PathError). HookBlocked is added
    separately by the caller, since it depends on the matching tool_result rather than the command text
    alone.
#>
function Get-CommandCategories {
    param([string]$Command)

    $categories = New-Object System.Collections.Generic.List[string]

    if ($Command -match '(?m)(^|[\s;&|`])find\s+(?<root>"[^"]+"|''[^'']+''|\S+)') {
        $root = $Matches['root'].Trim('"''').ToLowerInvariant()
        $isOutsideRoot = $false
        foreach ($bad in $script:BadFindRoots) {
            if ($root -eq $bad -or $root.StartsWith("$bad/") -or $root.StartsWith("$bad\")) {
                $isOutsideRoot = $true
                break
            }
        }
        if ($isOutsideRoot -and $Command -notmatch '\.nuget') {
            $categories.Add('Find') | Out-Null
        }
    }

    if ($Command -match '(^|[\s;&|`])python3?(\s|$)') {
        $categories.Add('Python') | Out-Null
    }

    # The -i must belong to the sed segment itself (not a later `| grep -i`), so match within one pipeline segment.
    if ($Command -match '\bsed\b[^|;&\n]*?\s(-[a-zA-Z]*i\b|--in-place\b)') {
        $categories.Add('SedInPlace') | Out-Null
    }

    # EditBypass shape 1: python/python3/perl carrying file-writing code in the same command text.
    $hasPythonOrPerl = $Command -match '(^|[\s;&|`])(python3?|perl)(\s|$)'
    if ($hasPythonOrPerl) {
        $writesFile = $Command -match 'open\s*\(\s*[''"][^''"]*[''"]\s*,\s*[''"](w|wb|a|ab)[''"]' -or
                      $Command -match '\.write\s*\(' -or
                      $Command -match 'write_text\s*\(' -or
                      $Command -match 'write_bytes\s*\(' -or
                      $Command -match '\bperl\b[^|;&\n]*-pi\b' -or
                      $Command -match '-i\.bak\b'
        if ($writesFile) {
            $categories.Add('EditBypass') | Out-Null
        }
    }

    # EditBypass shape 2: a shell redirect into src/ or tests/, unless it targets the scratchpad, /tmp,
    # Conversation/logs or Conversation/red (legitimate scratch/log destinations, not a bypass).
    $redirectMatches = [regex]::Matches($Command, '(?<op>>{1,2})\s*(?<path>"[^"]+"|''[^'']+''|\S+)')
    foreach ($redirectMatch in $redirectMatches) {
        $redirectPath = $redirectMatch.Groups['path'].Value.Trim('"''')
        $targetsSrcOrTests = $redirectPath -match '(^|[/\\])(src|tests)[/\\]'
        $isExempt = $redirectPath -match '(?i)scratchpad' -or
                    $redirectPath -match '(?i)(^|[/\\])tmp([/\\]|$)' -or
                    $redirectPath -match '(?i)Conversation[/\\](logs|red)([/\\]|$)'
        if ($targetsSrcOrTests -and -not $isExempt) {
            $categories.Add('EditBypass') | Out-Null
            break
        }
    }

    $gitMatches = [regex]::Matches($Command, '\bgit\s+(?<sub>stash|reset|checkout|switch|commit|add|restore|clean)\b(?<rest>[^\n;&|]*)')
    foreach ($m in $gitMatches) {
        $sub = $m.Groups['sub'].Value
        $rest = $m.Groups['rest'].Value
        if ($sub -eq 'add' -and $rest -match '(^|\s)-N(\s|$)') {
            # `git add -N` (intent-to-add, no content staged) is exempt.
            continue
        }
        $categories.Add("Git:$sub") | Out-Null
    }

    if ($Command -match '\bdotnet\s+(build|test)\b' -and
        $Command -notmatch 'Run-Tests\.ps1' -and $Command -notmatch 'Build\.ps1') {
        $categories.Add('BareDotnet') | Out-Null
    }

    if ($Command -match '(^|[\s;&|`])cd\s+[A-Za-z]:\\') {
        $categories.Add('PathError') | Out-Null
    }

    # MainCheckout (Bash/PowerShell shape): a `cd` into, or a `>`/`>>` redirect targeting, the main
    # checkout's src/ or tests/ folder. The Read/Edit/Write/NotebookEdit shape is handled by the caller,
    # since those tool_use blocks carry a file_path rather than a command line.
    $mainCheckoutHit = $false
    if ($Command -match '(^|[\s;&|`])cd\s+(?<cdPath>"[^"]+"|''[^'']+''|\S+)') {
        $cdPath = $Matches['cdPath']
        if (Test-IsMainCheckoutSrcOrTestsPath -Path $cdPath) {
            $mainCheckoutHit = $true
        }
    }
    if (-not $mainCheckoutHit) {
        foreach ($redirectMatch in $redirectMatches) {
            $redirectPath = $redirectMatch.Groups['path'].Value
            if (Test-IsMainCheckoutSrcOrTestsPath -Path $redirectPath) {
                $mainCheckoutHit = $true
                break
            }
        }
    }
    if ($mainCheckoutHit) {
        $categories.Add('MainCheckout') | Out-Null
    }

    # Heredoc: a heredoc marker or a `cat >`/`tee ` invocation whose write target lands inside the repo or
    # a worktree (not the scratchpad/tmp/Conversation-logs/Conversation-red). A heredoc with no detectable
    # write target (piped to a command's stdin, e.g. a commit-message `$(cat <<'EOF' ... EOF)`) is not
    # flagged - there is no file for it to have bypassed the Edit tool on.
    $hasHeredocMarker = $Command -match '<<-?\s*''?"?EOF''?"?\b'
    $hasCatOrTeeWrite = $Command -match '\bcat\b[^|;&\n]*>' -or $Command -match '\btee\s'
    if ($hasHeredocMarker -or $hasCatOrTeeWrite) {
        $heredocTargets = New-Object System.Collections.Generic.List[string]
        foreach ($redirectMatch in $redirectMatches) {
            $heredocTargets.Add($redirectMatch.Groups['path'].Value) | Out-Null
        }
        foreach ($teeMatch in [regex]::Matches($Command, '\btee\s+(?:-\S+\s+)*(?<path>"[^"]+"|''[^'']+''|\S+)')) {
            $heredocTargets.Add($teeMatch.Groups['path'].Value) | Out-Null
        }

        foreach ($target in $heredocTargets) {
            if (Test-IsRepoOrWorktreeWriteTarget -Path $target) {
                $categories.Add('Heredoc') | Out-Null
                break
            }
        }
    }

    # The unary comma is required: PowerShell unrolls a returned collection into the output stream, so a
    # single-element List<string> would otherwise come back to the caller as a bare [string], which has
    # no .Add method.
    return ,$categories
}

$rawLines = Get-Content -LiteralPath $TranscriptPath

# tool_use_id -> tool_result info (concatenated text content across the (rare) multi-part case, plus
# is_error), so a flagged command can be cross-checked against the hook's verdict.
$resultsById = @{}
# tool_use_id -> ordered arrival index, purely so the table prints in transcript order.
$toolUses = New-Object System.Collections.Generic.List[object]

$lineNo = 0
foreach ($rawLine in $rawLines) {
    $lineNo++
    if ([string]::IsNullOrWhiteSpace($rawLine)) {
        continue
    }

    $entry = $null
    try {
        $entry = $rawLine | ConvertFrom-Json -ErrorAction Stop
    } catch {
        # Not every line need be well-formed JSON we care about (and a corrupt tail line should not
        # abort the whole audit); skip and keep going.
        continue
    }

    $message = $entry.message
    if ($null -eq $message -or $null -eq $message.content) {
        continue
    }
    # A plain string `content` (a text-only user/assistant turn) has no tool_use/tool_result blocks.
    if ($message.content -isnot [System.Collections.IEnumerable] -or $message.content -is [string]) {
        continue
    }

    foreach ($item in $message.content) {
        if ($null -eq $item -or $null -eq $item.type) {
            continue
        }

        if ($item.type -eq 'tool_use' -and $item.name -in @('Bash', 'PowerShell')) {
            $command = $item.input.command
            if ([string]::IsNullOrEmpty($command)) {
                continue
            }
            $toolUses.Add([PSCustomObject]@{
                Line     = $lineNo
                ToolUseId = $item.id
                Tool     = $item.name
                Command  = $command
            }) | Out-Null
        }

        if ($item.type -eq 'tool_use' -and $item.name -in @('Read', 'Edit', 'Write', 'NotebookEdit')) {
            $filePath = $item.input.file_path
            if ([string]::IsNullOrEmpty($filePath)) {
                continue
            }
            $toolUses.Add([PSCustomObject]@{
                Line     = $lineNo
                ToolUseId = $item.id
                Tool     = $item.name
                Command  = $filePath
            }) | Out-Null
        }

        if ($item.type -eq 'tool_result' -and $item.tool_use_id) {
            $resultText = $item.content
            if ($resultText -isnot [string]) {
                $resultText = ($resultText | Out-String)
            }
            $resultsById[$item.tool_use_id] = $resultText
        }
    }
}

$findings = New-Object System.Collections.Generic.List[object]

$index = 0
foreach ($use in $toolUses) {
    if ($use.Tool -in @('Bash', 'PowerShell')) {
        $categories = Get-CommandCategories -Command $use.Command
    } else {
        # Read/Edit/Write/NotebookEdit: $use.Command carries the tool's file_path, not a shell command.
        $categories = New-Object System.Collections.Generic.List[string]
        if (Test-IsMainCheckoutSrcOrTestsPath -Path $use.Command) {
            if ($use.Tool -eq 'Read') {
                $categories.Add('MainCheckoutRead') | Out-Null
            } else {
                $categories.Add('MainCheckout') | Out-Null
            }
        }
    }

    $resultText = $resultsById[$use.ToolUseId]
    if ($resultText -and $resultText -match 'hook error: Blocked') {
        $categories.Add('HookBlocked') | Out-Null
    }
    if ($resultText -and $resultText -match 'unexpected EOF while looking for matching') {
        $categories.Add('PathError') | Out-Null
    }

    if ($categories.Count -eq 0) {
        continue
    }

    $index++
    $oneLineCommand = ($use.Command -replace '\r?\n', ' \ ')
    $truncated = if ($oneLineCommand.Length -gt 160) { $oneLineCommand.Substring(0, 160) } else { $oneLineCommand }

    $findings.Add([PSCustomObject]@{
        Index    = $index
        Line     = $use.Line
        Tool     = $use.Tool
        Command  = $truncated
        Category = ($categories | Select-Object -Unique) -join ','
    }) | Out-Null
}

Write-Host "Audit-Transcript: transcript=$TranscriptPath"
Write-Host "Bash/PowerShell/Read/Edit/Write/NotebookEdit tool_use blocks scanned: $($toolUses.Count)"
Write-Host ''

if ($findings.Count -eq 0) {
    Write-Host 'OK: no matching commands found.'
    exit 0
}

$findings | Format-Table -Property Index, Tool, Category, Line, Command -AutoSize -Wrap | Out-String -Width 4096 | Write-Host

# PathError-only findings are orientation waste (a drive path handed to Bash, or the EOF a bad heredoc
# throws off the back of it), and MainCheckoutRead-only findings are a read of the wrong tree, not a write
# to it - neither must fail the audit on their own. A finding fails the audit only if it carries at least
# one other category alongside (or instead of) those two.
$nonFailingCategories = @('PathError', 'MainCheckoutRead')
$failingFindings = @($findings | Where-Object {
    (($_.Category -split ',') | Where-Object { $_ -notin $nonFailingCategories }).Count -gt 0
})

if ($failingFindings.Count -gt 0) {
    Write-Host "FAIL: $($findings.Count) matching command(s) found ($($failingFindings.Count) requiring action)."
    exit 1
}

Write-Host "OK: $($findings.Count) matching command(s) found, all PathError/MainCheckoutRead-only (not blocking)."
exit 0
