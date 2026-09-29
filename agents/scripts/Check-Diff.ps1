<#
.SYNOPSIS
    Scans a diff for a handful of house-style smells in changed/added .cs and .razor lines, and reports
    (but does not fix) what it finds.

.DESCRIPTION
    Runs a `git diff --unified=0 ... -- '*.cs' '*.razor'` in the CALLER's git toplevel (`git rev-parse
    --show-toplevel`) and walks only ADDED lines (a mutation/edit review cares about new code, not what
    was removed). -Scope picks which diff:

      - Branch  (the original behaviour): `git diff <Base>...HEAD` - everything the branch has committed
        since it forked from -Base. Misses a dispatch's uncommitted work entirely.
      - Working: `git diff <Base>` (two-dot: working tree, staged AND unstaged, against -Base) PLUS every
        untracked `.cs`/`.razor` file (`git ls-files --others --exclude-standard`), scanned in full as if
        every line were added. Sees everything on the branch AND everything not yet committed.
      - Mine:    `git diff HEAD` (working tree against HEAD) PLUS the same untracked-file scan, but
        restricted to files changed or newly added in the working tree relative to HEAD - i.e. only what
        THIS dispatch touched, not the rest of the branch. This is what an agent should run to check its
        own work before handing it back.

    With no -Scope given, it defaults to Mine when `git status --porcelain` shows uncommitted changes in
    the caller's worktree, else Branch - the common case ("check what I just did" while mid-edit; "check
    the whole branch" once it's all committed).

    For each added (or, for an untracked file, every) line, checks:

      - Null-forgiving `!` operators - heuristic: `\w!\.`, `\)!\.`, `\]!\.`, `!;`, `= null!`. Reported as a
        failure: agents/CSharpPrinciples.md treats `!` as a review finding, never a fix.
      - `var ` declarations in new .cs lines - house style (agents/CSharpPrinciples.md) prefers an
        explicit type on the left; reported as INFO only, not a failure, since `var` is fine where the
        type is already spelled on the right or naming it would be noise (foreach, LINQ).
      - Edits to files under docs/, or to -SpecPath - INFO only, listed by path (via
        `git diff --name-status` on the same ref as -Scope, not restricted to .cs/.razor).
      - `#pragma warning disable` with no reason comment on the same line - failure: the house style
        requires the pragma to carry its justification inline.
      - Weak asserts: `Assert.True(x.Contains(...))` / `Assert.Equal(0, x.Count)` style - INFO only (the
        specific-assertion rule is a style preference the review should flag, not a hard gate here). The
        `Assert.True(...Contains(...))` half is skipped when it looks like a bUnit DOM containment check
        rather than a weak string/collection assert - heuristic: the receiver name ends in `card`, `root`,
        `element`, `el`, `row`, or `cut`, or the argument is an `IElement` pulled from `Find(`.
      - Untested texts - a heuristic scan for refusal/problem/message text that shipped with no matching
        test. From added/changed lines in src/**/*.cs and src/**/*.razor, collects string literals and
        interpolated-string templates of >= 12 characters (quote-delimited content, `"..."` or `$"..."`)
        that sit on a line containing `return`, `refusal =`, `problems.Add`, `Problem(`, or `=> "` / `=>
        $"` - EXCEPT a line that is itself a `throw new UnreachableException(...)`, a `[LoggerMessage(...
        Message = ...)]` attribute, or a `LogWarning(`/`LogError(`/`LogInformation(`/`LogDebug(` call:
        an unreachable-code message and a structured log template are not user-facing text a test would
        assert on, so they are skipped rather than reported as untested. Each literal becomes a regex: its
        literal characters escaped, and each `{...}` interpolation
        hole widened to `.*` (this also covers a plain, non-interpolated literal - it just has no holes to
        widen). That regex is then matched against the full, on-disk content of every file under
        tests/**/*.cs (whole files, not just the diff, and not scoped to the changed test files - a text
        added in one file may legitimately be asserted from another). A `.*` hole swallows whatever the
        test side's own interpolation expression contains, so a test asserting with its own `$"..."`
        template still matches without a separate normalisation pass on the test side. A literal with no
        match anywhere in tests/**/*.cs is reported here and fails the check (see -AllowUntested).
      - Refusal/message asserts on Contains/StartsWith only (WARN) - for a test method touched by the
        diff (any added/changed line falls inside it, found by a brace-counting scan of the full file from
        its `[Fact]`/`[Theory]` attribute to the method's closing brace), warns when the method's body
        contains `Assert.Contains(` or `Assert.StartsWith(` but no `Assert.Equal(` - i.e. it never pins the
        exact text, only a fragment of it. Informational; does not affect the exit code.
      - Markup/string Contains asserts (FAIL, see -AllowContains) - a per-line check, independent of the
        method-level one above: `Assert.Contains(`/`Assert.StartsWith(`/`Assert.EndsWith(` on a line that
        also mentions `.Markup`, `.InnerHtml`, `.TextContent`, `.OuterHtml`, or `StringComparison` - i.e.
        the haystack looks like rendered markup or a string, not a collection - fails the check unless the
        same line or the line above it (read from the on-disk file, not the diff) carries a
        `// contains-ok: <reason>` comment. -AllowContains downgrades this from a failure to a warning.
      - Collection-membership Contains (INFO) - `Assert.Contains(` on a line that does NOT match the
        markup/string case above (no StringComparison, none of the markup members) is treated as the
        two-argument collection-membership overload and reported as info, with a hint to assert the whole
        list (`Assert.Equal([...], list)` / `Assert.Single`) instead of one membership check.
      - `// contains-ok:` annotations (own info section, plus FAIL - see -AllowContains) - every
        `// contains-ok: <reason>` comment found in the diff scope is listed, file:line and its reason, in
        its own "contains-ok annotations" section so a reviewer can see every exemption the diff is
        claiming, not just the ones that would otherwise have failed. contains-ok is meant for a haystack
        that only LOOKS like a false positive - source-file text, a URI, prompt text - never for rendered
        markup: an annotation whose asserted haystack (the same line if it also carries the
        `Assert.Contains/StartsWith/EndsWith` call, else the line directly below, read from disk) matches
        `.Markup`, `.InnerHtml`, `.OuterHtml` or `.TextContent` fails the check (-AllowContains downgrades
        this to a warning, same switch as the plain Markup/string Contains check above).

    Exit 1 if any null-forgiving `!` (including `= null!`), reason-less `#pragma warning disable`,
    (see -AllowUntested) untested text, or (see -AllowContains) markup/string Contains assert (including a
    `// contains-ok:` annotation on a markup haystack) is found. All other findings are informational and
    do not affect the exit code.

.PARAMETER Scope
    Branch, Working, or Mine - see DESCRIPTION. Defaults to Mine if the worktree has uncommitted changes,
    else Branch.

.PARAMETER Base
    The git ref to diff against (Branch and Working scopes only; Mine always diffs against HEAD).
    Defaults to "main".

.PARAMETER SpecPath
    A repo-relative path (as `git diff --name-status` prints it) whose changes are called out alongside
    docs/ changes, even though it is not itself under docs/. Defaults to the Tasks feature's spec,
    "docs/Huddle.Tasks-Specifications.md", since this script was written while that feature was in flight
    on feat/tasks; pass a different path for other branches/specs.

.PARAMETER AllowUntested
    Downgrades the "Untested texts" check from a failure to a warning: the section still prints and lists
    every untested literal, but a non-empty list no longer makes the script exit 1 by itself.

.PARAMETER AllowContains
    Downgrades the "Markup/string Contains asserts" check (and a `// contains-ok:` annotation found on a
    markup haystack) from a failure to a warning: the sections still print and list every offending line,
    but a non-empty list no longer makes the script exit 1 by itself. Mirrors -AllowUntested.

.PARAMETER GroupByFile
    -Scope Branch only: after the normal report, prints every finding (including untested texts and weak
    refusal asserts) grouped by file, with a count per kind, sorted by total finding count descending - so
    a hardening pass can dispatch work file by file instead of finding by finding.

.PARAMETER AcceptedUntestedPath
    Path to the manager-reviewed false-positive/accepted-untested-text allowlist (one `path|text|reason`
    line per entry, `#`-prefixed comments allowed - see accepted-untested.example.txt next to this script
    for the format and a worked example from the Tasks feature). Defaults to
    accepted-untested.example.txt next to this script; pass your own project's file via this parameter -
    each project keeps its own list, reviewed and grown by its own delivery manager, rather than sharing
    the Tasks feature's.

.EXAMPLE
    pwsh agents/scripts/Check-Diff.ps1

.EXAMPLE
    pwsh agents/scripts/Check-Diff.ps1 -Scope Mine

.EXAMPLE
    pwsh agents/scripts/Check-Diff.ps1 -Scope Branch -Base main

.EXAMPLE
    pwsh agents/scripts/Check-Diff.ps1 -Scope Mine -AllowUntested

.EXAMPLE
    pwsh agents/scripts/Check-Diff.ps1 -Scope Mine -AllowContains

.EXAMPLE
    pwsh agents/scripts/Check-Diff.ps1 -Scope Branch -Base main -GroupByFile

.EXAMPLE
    pwsh agents/scripts/Check-Diff.ps1 -AcceptedUntestedPath agents/scripts/my-project-accepted-untested.txt
#>
[CmdletBinding()]
param(
    [ValidateSet('Branch', 'Working', 'Mine')]
    [string]$Scope,

    [string]$Base = 'main',
    [string]$SpecPath = 'docs/Huddle.Tasks-Specifications.md',
    [switch]$AllowUntested,
    [switch]$AllowContains,
    [switch]$GroupByFile,
    [string]$AcceptedUntestedPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not $AcceptedUntestedPath) {
    $AcceptedUntestedPath = Join-Path $PSScriptRoot 'accepted-untested.example.txt'
}

$worktreeRoot = (git rev-parse --show-toplevel).Trim()

if (-not $PSBoundParameters.ContainsKey('Scope')) {
    $porcelain = @(& git -C $worktreeRoot status --porcelain 2>&1)
    $hasUncommitted = (@($porcelain | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })).Count -gt 0
    $Scope = if ($hasUncommitted) { 'Mine' } else { 'Branch' }
}

# The git ref/range passed to `git diff` and `git diff --name-status`, and whether an untracked-file
# scan runs alongside it. Mine's untracked scan is further restricted below to files git already
# considers changed/added relative to HEAD (via --diff-filter=AM on `git diff --name-only HEAD`) so a
# stray untracked file elsewhere in the tree, not touched by this dispatch, isn't swept in.
$diffRef = switch ($Scope) {
    'Branch' { "$Base...HEAD" }
    'Working' { $Base }
    'Mine' { 'HEAD' }
}
$includeUntracked = $Scope -ne 'Branch'

$diffOutput = @(& git -C $worktreeRoot diff --unified=0 $diffRef -- '*.cs' '*.razor' 2>&1)
if ($LASTEXITCODE -ne 0) {
    throw "git diff failed: $($diffOutput -join "`n")"
}

$nameStatus = @(& git -C $worktreeRoot diff --name-status $diffRef 2>&1)
if ($LASTEXITCODE -ne 0) {
    throw "git diff --name-status failed: $($nameStatus -join "`n")"
}

$findings = New-Object System.Collections.Generic.List[object]

# File (repo-relative, forward-slash) -> System.Collections.Generic.HashSet[int] of added/changed line
# numbers in that file's NEW content, for files under tests/**/*.cs. Populated by the diff walk and the
# untracked-file scan below, then used after both to find test methods the diff actually touched (for the
# Contains/StartsWith-only assert warning).
$testAddedLines = @{}

# (File, Line, LiteralText) candidates for the "Untested texts" check: src/**/*.cs and src/**/*.razor
# lines that look like they carry refusal/problem/message text. Populated the same way as $testAddedLines.
$untestedCandidates = New-Object System.Collections.Generic.List[object]

# (File, Line, Reason, IsMarkup) for every `// contains-ok: <reason>` annotation found in the diff scope -
# printed in its own info section regardless of IsMarkup, and also the source of the ContainsOkOnMarkup
# failure in $findings when IsMarkup is true. Populated the same way as $testAddedLines.
$containsOkAnnotations = New-Object System.Collections.Generic.List[object]

# File (repo-relative) -> string[] of that file's on-disk lines, lazily populated by Get-CachedFileLines.
# Used to look for a `// contains-ok:` comment on the line above a flagged Contains/StartsWith/EndsWith -
# the diff itself (--unified=0) carries no context lines, so "the line above" has to come from disk.
$script:FileLinesCache = @{}

<#
.SYNOPSIS
    Returns $File's on-disk lines (repo-relative path, resolved under $worktreeRoot), reading once and
    caching the result. Returns an empty array for a file that no longer exists on disk.
#>
function Get-CachedFileLines {
    param([string]$File)

    if (-not $script:FileLinesCache.ContainsKey($File)) {
        $fullPath = Join-Path $worktreeRoot $File
        $script:FileLinesCache[$File] = if (Test-Path -LiteralPath $fullPath) {
            @(Get-Content -LiteralPath $fullPath)
        } else {
            @()
        }
    }

    return $script:FileLinesCache[$File]
}

<#
.SYNOPSIS
    True when $File's on-disk line $LineNo, or the line above it, carries a `// contains-ok: <reason>`
    comment - the escape hatch for a markup/string Contains/StartsWith/EndsWith assert.
#>
function Test-HasContainsOkComment {
    param([string]$File, [int]$LineNo)

    $lines = Get-CachedFileLines -File $File
    if ($LineNo -lt 1 -or $LineNo -gt $lines.Count) {
        return $false
    }

    if ($lines[$LineNo - 1] -match '//\s*contains-ok:\s*\S') {
        return $true
    }

    # Walk upward over a run of sibling asserts (a block of Assert.Contains/StartsWith/EndsWith lines
    # covered by one annotation), then over the contiguous // comment block directly above them; the
    # annotation counts if any line of that comment block carries `contains-ok:` (so a two-line
    # comment whose first line is the annotation still covers the assert below it).
    $i = $LineNo - 2
    while ($i -ge 0 -and $lines[$i] -match '^\s*Assert\.(Contains|StartsWith|EndsWith)\(') {
        $i--
    }

    while ($i -ge 0 -and $lines[$i] -match '^\s*//') {
        if ($lines[$i] -match '//\s*contains-ok:\s*\S') {
            return $true
        }

        $i--
    }

    return $false
}

<#
.SYNOPSIS
    Runs the house-style smell checks (null-forgiving, var, reason-less pragma, weak assert, markup/string
    Contains) against one line of new/added content, appending any hits to $findings. Shared between the
    diff walk and the untracked-file scan below, so both paths use exactly the same rules.
#>
function Add-LineFindings {
    param([string]$Content, [string]$File, [int]$LineNo)

    if ($Content -match '\w!\.' -or $Content -match '\)!\.' -or $Content -match '\]!\.' -or
        $Content -match '!;' -or $Content -match '=\s*null!') {
        $findings.Add([PSCustomObject]@{
            Category = 'NullForgiving'
            File     = $File
            Line     = $LineNo
            Text     = $Content.Trim()
        }) | Out-Null
    }

    # bUnit idioms `var cut = ...` / `var x = Render...(...)` are excluded - they are the house pattern
    # for a rendered component under test, not a case of "explicit type on the left" being worth flagging.
    if ($File -like '*.cs' -and $Content -match '(?<![A-Za-z0-9_])var\s+[A-Za-z_]') {
        $isBunitIdiom = $Content -match '(?<![A-Za-z0-9_])var\s+cut\s*=' -or
                        $Content -match '(?<![A-Za-z0-9_])var\s+\w+\s*=\s*Render'
        if (-not $isBunitIdiom) {
            $findings.Add([PSCustomObject]@{
                Category = 'Var'
                File     = $File
                Line     = $LineNo
                Text     = $Content.Trim()
            }) | Out-Null
        }
    }

    if ($Content -match '#pragma\s+warning\s+disable') {
        if ($Content -notmatch '//\s*\S') {
            $findings.Add([PSCustomObject]@{
                Category = 'PragmaNoReason'
                File     = $File
                Line     = $LineNo
                Text     = $Content.Trim()
            }) | Out-Null
        }
    }

    # `Assert.True(x.Contains(...))` is skipped when it looks like a bUnit DOM containment check rather
    # than a weak string/collection assert - heuristic: the receiver name ends in card/root/element/el/
    # row/cut, or the argument comes from a `Find(...)` call (an IElement).
    if ($Content -match 'Assert\.True\(\s*(?<receiver>[A-Za-z_]\w*)\.Contains\(\s*(?<arg>[^)]*)\)\s*\)') {
        $receiver = $Matches['receiver']
        $arg = $Matches['arg']
        $isDomContainment = $receiver -match '(?i)(card|root|element|el|row|cut)$' -or $arg -match 'Find\('
        if (-not $isDomContainment) {
            $findings.Add([PSCustomObject]@{
                Category = 'WeakAssert'
                File     = $File
                Line     = $LineNo
                Text     = $Content.Trim()
            }) | Out-Null
        }
    }
    if ($Content -match 'Assert\.Equal\(\s*0\s*,\s*[^)]*\.Count\s*\)') {
        $findings.Add([PSCustomObject]@{
            Category = 'WeakAssert'
            File     = $File
            Line     = $LineNo
            Text     = $Content.Trim()
        }) | Out-Null
    }

    # Markup/string Contains/StartsWith/EndsWith (fail, unless `// contains-ok:` annotated) vs. plain
    # collection-membership Assert.Contains (info hint). See -AllowContains and the section header below.
    if ($Content -match 'Assert\.(Contains|StartsWith|EndsWith)\s*\(') {
        $looksLikeMarkupOrString = $Content -match '\.(Markup|InnerHtml|TextContent|OuterHtml)\b' -or
                                    $Content -match 'StringComparison'
        if ($looksLikeMarkupOrString) {
            if (-not (Test-HasContainsOkComment -File $File -LineNo $LineNo)) {
                $findings.Add([PSCustomObject]@{
                    Category = 'MarkupAssertFail'
                    File     = $File
                    Line     = $LineNo
                    Text     = $Content.Trim()
                }) | Out-Null
            }
        } elseif ($Content -match 'Assert\.Contains\s*\(') {
            $findings.Add([PSCustomObject]@{
                Category = 'CollectionMembershipInfo'
                File     = $File
                Line     = $LineNo
                Text     = $Content.Trim()
            }) | Out-Null
        }
    }

    # Every `// contains-ok: <reason>` annotation in the diff scope, listed for the reviewer regardless of
    # whether it is valid - contains-ok is only for a non-markup haystack (source text, a URI, prompt
    # text). The asserted haystack is this same line when it also carries the Assert call, else the line
    # directly below (the standalone-comment-above-the-assert shape Test-HasContainsOkComment supports),
    # read from disk since the diff itself carries no context lines.
    if ($Content -match '//\s*contains-ok:\s*(?<reason>\S.*)$') {
        $reason = $Matches['reason'].Trim()
        $assertContent = $Content
        if ($Content -notmatch 'Assert\.(Contains|StartsWith|EndsWith)\s*\(') {
            $onDiskLines = Get-CachedFileLines -File $File
            if ($LineNo -ge 1 -and $LineNo -lt $onDiskLines.Count) {
                # 0-based index $LineNo is the on-disk line directly below 1-based line $LineNo.
                $assertContent = $onDiskLines[$LineNo]
            }
        }
        $isMarkup = $assertContent -match '\.(Markup|InnerHtml|OuterHtml|TextContent)\b'

        $containsOkAnnotations.Add([PSCustomObject]@{
            File     = $File
            Line     = $LineNo
            Reason   = $reason
            IsMarkup = $isMarkup
        }) | Out-Null

        if ($isMarkup) {
            $findings.Add([PSCustomObject]@{
                Category = 'ContainsOkOnMarkup'
                File     = $File
                Line     = $LineNo
                Text     = $Content.Trim()
            }) | Out-Null
        }
    }

    if (($File -like 'tests/*.cs')) {
        if (-not $testAddedLines.ContainsKey($File)) {
            $testAddedLines[$File] = New-Object System.Collections.Generic.HashSet[int]
        }
        $testAddedLines[$File].Add($LineNo) | Out-Null
    }

    # An unreachable-code message and a structured log template are not user-facing text a test would
    # assert on - skip both from "Untested texts" rather than reporting them as untested.
    $isExemptFromUntested = $Content -match '\bUnreachableException\s*\(' -or
                            $Content -match '\[LoggerMessage\b' -or
                            $Content -match '\bLog(Warning|Error|Information|Debug)\s*\('

    if (($File -like 'src/*.cs' -or $File -like 'src/*.razor') -and -not $isExemptFromUntested -and
        $Content -match '(return|refusal\s*=|problems\.Add|Problem\(|=>\s*"|=>\s*\$")') {
        $untestedCandidates.Add([PSCustomObject]@{
            File    = $File
            Line    = $LineNo
            Content = $Content
        }) | Out-Null
    }
}

$currentFile = $null
$newLineNumber = 0

foreach ($line in $diffOutput) {
    if ($line -match '^diff --git ') {
        $currentFile = $null
        continue
    }
    if ($line -match '^\+\+\+ b/(?<path>.+)$') {
        $currentFile = $Matches.path
        continue
    }
    if ($line -match '^--- ') {
        continue
    }
    if ($line -match '^@@ -\d+(?:,\d+)? \+(?<newStart>\d+)(?:,\d+)? @@') {
        $newLineNumber = [int]$Matches.newStart
        continue
    }
    if ($null -eq $currentFile) {
        continue
    }
    if ($line.Length -eq 0) {
        continue
    }
    if ($line[0] -eq '-') {
        # Removed line: does not exist in the new file, does not advance newLineNumber.
        continue
    }
    if ($line[0] -ne '+') {
        continue
    }

    $content = $line.Substring(1)
    $lineNo = $newLineNumber
    $newLineNumber++

    Add-LineFindings -Content $content -File $currentFile -LineNo $lineNo
}

$untrackedScanned = New-Object System.Collections.Generic.List[string]
if ($includeUntracked) {
    $untrackedAll = @(& git -C $worktreeRoot ls-files --others --exclude-standard -- '*.cs' '*.razor' 2>&1)
    if ($LASTEXITCODE -ne 0) {
        throw "git ls-files failed: $($untrackedAll -join "`n")"
    }

    # `git ls-files --others` only ever lists files not in HEAD to begin with, so every untracked
    # .cs/.razor file is already "changed relative to HEAD" - Mine and Working scan the same untracked
    # set; they differ only in $diffRef (HEAD vs -Base) for TRACKED changes, handled above.
    foreach ($file in $untrackedAll) {
        if ([string]::IsNullOrWhiteSpace($file)) {
            continue
        }
        $fullPath = Join-Path $worktreeRoot $file
        if (-not (Test-Path -LiteralPath $fullPath)) {
            continue
        }

        $untrackedScanned.Add($file) | Out-Null
        $fileLines = @(Get-Content -LiteralPath $fullPath)
        for ($i = 0; $i -lt $fileLines.Count; $i++) {
            Add-LineFindings -Content $fileLines[$i] -File $file -LineNo ($i + 1)
        }
    }
}

<#
.SYNOPSIS
    Converts a string literal's raw content (the text between the quotes, as it appears in source - no
    unescaping) into a regex that also matches an equivalent interpolated literal: every `{...}`
    interpolation hole becomes `.*`, and everything else is escaped literally. A plain, non-interpolated
    literal has no holes, so it round-trips to a plain escaped-literal match.
#>
function Convert-LiteralToMatchPattern {
    param([string]$Body)

    $parts = [regex]::Split($Body, '\{[^{}]*\}')
    return '(?s)' + (($parts | ForEach-Object { [regex]::Escape($_) }) -join '.*')
}

# Every quoted string literal (plain, verbatim, interpolated, or both) inside one line of source, as
# (prefix, body) pairs. Deliberately simple - it does not special-case verbatim `""` escapes - this is a
# heuristic tripwire, not a C# parser.
$script:StringLiteralPattern = '(?<prefix>\$@|@\$|\$|@)?"(?<body>(?:[^"\\]|\\.)*)"'

$untestedTexts = New-Object System.Collections.Generic.List[object]
if ($untestedCandidates.Count -gt 0) {
    $testDir = Join-Path $worktreeRoot 'tests'
    $testContents = New-Object System.Collections.Generic.List[string]
    if (Test-Path -LiteralPath $testDir) {
        $testFilesOnDisk = @(Get-ChildItem -LiteralPath $testDir -Recurse -Filter '*.cs' -File -ErrorAction SilentlyContinue)
        foreach ($tf in $testFilesOnDisk) {
            $testContents.Add((Get-Content -LiteralPath $tf.FullName -Raw)) | Out-Null
        }
    }

    $seen = New-Object System.Collections.Generic.HashSet[string]
    foreach ($candidate in $untestedCandidates) {
        foreach ($m in [regex]::Matches($candidate.Content, $script:StringLiteralPattern)) {
            $body = $m.Groups['body'].Value
            if ($body.Length -lt 12) {
                continue
            }

            $key = "$($candidate.File)|$($candidate.Line)|$body"
            if (-not $seen.Add($key)) {
                continue
            }

            $pattern = Convert-LiteralToMatchPattern -Body $body
            $isTested = $false
            foreach ($tc in $testContents) {
                if ($tc -match $pattern) {
                    $isTested = $true
                    break
                }
            }

            if (-not $isTested) {
                $untestedTexts.Add([PSCustomObject]@{
                    File = $candidate.File
                    Line = $candidate.Line
                    Text = $body
                }) | Out-Null
            }
        }
    }
}

<#
.SYNOPSIS
    Brace-counting scan of a test file's full line array for every `[Fact]`/`[Theory]` method: from the
    attribute, forward to the method signature (the next line containing `(`), forward again to the
    opening `{`, then tracking brace depth to the matching close. Returns each method's name and its
    1-based (Start, End) line range (signature line through closing brace, inclusive). A heuristic, not a
    parser - a `{`/`}` inside a string or comment on the same line still counts, same as most greps do.
#>
function Get-TestMethodRanges {
    param([string[]]$Lines)

    $ranges = New-Object System.Collections.Generic.List[object]
    for ($i = 0; $i -lt $Lines.Count; $i++) {
        if ($Lines[$i] -notmatch '^\s*\[(Fact|Theory)\b') {
            continue
        }

        $sigIndex = $i + 1
        while ($sigIndex -lt $Lines.Count -and $Lines[$sigIndex] -notmatch '\(') {
            $sigIndex++
        }
        if ($sigIndex -ge $Lines.Count) {
            continue
        }

        $name = if ($Lines[$sigIndex] -match '(?<name>\w+)\s*\(') { $Matches.name } else { '<unknown>' }

        $braceIndex = $sigIndex
        while ($braceIndex -lt $Lines.Count -and $Lines[$braceIndex] -notmatch '\{') {
            $braceIndex++
        }
        if ($braceIndex -ge $Lines.Count) {
            continue
        }

        $depth = 0
        $endIndex = $braceIndex
        for ($m = $braceIndex; $m -lt $Lines.Count; $m++) {
            $depth += ([regex]::Matches($Lines[$m], '\{')).Count
            $depth -= ([regex]::Matches($Lines[$m], '\}')).Count
            $endIndex = $m
            if ($depth -le 0) {
                break
            }
        }

        $ranges.Add([PSCustomObject]@{
            Name  = $name
            Start = $sigIndex + 1
            End   = $endIndex + 1
        }) | Out-Null

        $i = $endIndex
    }

    return ,$ranges
}

$weakRefusalAsserts = New-Object System.Collections.Generic.List[object]
foreach ($file in $testAddedLines.Keys) {
    $fullPath = Join-Path $worktreeRoot $file
    if (-not (Test-Path -LiteralPath $fullPath)) {
        continue
    }

    $lines = @(Get-Content -LiteralPath $fullPath)
    $addedSet = $testAddedLines[$file]
    foreach ($range in (Get-TestMethodRanges -Lines $lines)) {
        $touched = $false
        foreach ($ln in $addedSet) {
            if ($ln -ge $range.Start -and $ln -le $range.End) {
                $touched = $true
                break
            }
        }
        if (-not $touched) {
            continue
        }

        $body = ($lines[($range.Start - 1)..($range.End - 1)] -join "`n")
        if (($body -match 'Assert\.(Contains|StartsWith)\s*\(') -and $body -notmatch 'Assert\.Equal\s*\(') {
            $weakRefusalAsserts.Add([PSCustomObject]@{
                File = $file
                Line = $range.Start
                Text = $range.Name
            }) | Out-Null
        }
    }
}

$docFindings = New-Object System.Collections.Generic.List[string]
foreach ($ns in $nameStatus) {
    if ([string]::IsNullOrWhiteSpace($ns)) {
        continue
    }
    $parts = $ns -split "`t"
    $status = $parts[0]
    $path = $parts[-1]
    if ($path -like 'docs/*' -or $path -eq $SpecPath) {
        $docFindings.Add("$status`t$path") | Out-Null
    }
}

function Write-FindingSection {
    param([string]$Title, [object[]]$Items, [string]$Hint)

    Write-Host ''
    Write-Host "${Title}:"
    if ($Items.Count -eq 0) {
        Write-Host '  (none)'
        return
    }
    foreach ($item in $Items) {
        Write-Host "  $($item.File):$($item.Line): $($item.Text)"
    }
    if ($Hint) {
        Write-Host "  hint: $Hint"
    }
}

Write-Host "Check-Diff: scope=$Scope base=$Base ref=$diffRef worktree=$worktreeRoot"
if ($includeUntracked) {
    Write-Host "Untracked .cs/.razor files scanned: $($untrackedScanned.Count)"
    foreach ($f in $untrackedScanned) {
        Write-Host "  $f"
    }
}

$nullForgiving = @($findings | Where-Object { $_.Category -eq 'NullForgiving' })
$pragmaNoReason = @($findings | Where-Object { $_.Category -eq 'PragmaNoReason' })
$varDecls = @($findings | Where-Object { $_.Category -eq 'Var' })
$weakAsserts = @($findings | Where-Object { $_.Category -eq 'WeakAssert' })
$markupAssertFail = @($findings | Where-Object { $_.Category -eq 'MarkupAssertFail' })
$containsOkOnMarkup = @($findings | Where-Object { $_.Category -eq 'ContainsOkOnMarkup' })
$collectionMembershipInfo = @($findings | Where-Object { $_.Category -eq 'CollectionMembershipInfo' })

Write-FindingSection -Title 'Null-forgiving operators (fail)' -Items $nullForgiving
Write-FindingSection -Title '#pragma warning disable without a reason comment (fail)' -Items $pragmaNoReason
Write-FindingSection -Title 'var declarations in new C# lines (info)' -Items $varDecls
Write-FindingSection -Title 'Weak asserts: Assert.True(...Contains(...)) / Assert.Equal(0, x.Count) (info)' -Items $weakAsserts
# Manager-reviewed false positives and accepted-untested texts live in -AcceptedUntestedPath (defaults to
# accepted-untested.example.txt beside this script), one `path|text|reason` per line (# comments allowed).
# A hit whose file ends with `path` and whose text equals `text` is moved to an info section with its
# reason instead of failing the run.
$acceptedUntested = New-Object System.Collections.Generic.List[object]
if (Test-Path -LiteralPath $AcceptedUntestedPath) {
    $acceptedEntries = Get-Content -LiteralPath $AcceptedUntestedPath |
        Where-Object { $_ -and -not $_.TrimStart().StartsWith('#') } |
        ForEach-Object { $parts = $_.Split('|', 3); if ($parts.Count -eq 3) { [PSCustomObject]@{ Path = $parts[0].Trim(); Text = $parts[1].Trim(); Reason = $parts[2].Trim() } } }
    $stillUntested = New-Object System.Collections.Generic.List[object]
    foreach ($untested in $untestedTexts) {
        $normalizedFile = $untested.File -replace '\\', '/'
        $match = $acceptedEntries | Where-Object { $normalizedFile.EndsWith($_.Path, [StringComparison]::Ordinal) -and ($untested.Text.Trim() -eq $_.Text) } | Select-Object -First 1
        if ($null -ne $match) {
            $acceptedUntested.Add([PSCustomObject]@{ File = $untested.File; Line = $untested.Line; Text = "$($untested.Text.Trim())  [accepted: $($match.Reason)]" }) | Out-Null
        }
        else {
            $stillUntested.Add($untested) | Out-Null
        }
    }

    $untestedTexts = $stillUntested
}

Write-FindingSection -Title 'Accepted untested texts (manager-reviewed, info)' -Items $acceptedUntested
Write-FindingSection -Title "Untested texts $(if ($AllowUntested) { '(warn - -AllowUntested)' } else { '(fail)' })" -Items $untestedTexts
Write-FindingSection -Title 'Refusal/message asserts using only Assert.Contains/StartsWith, no Assert.Equal (warn)' -Items $weakRefusalAsserts
Write-FindingSection -Title "Markup/string Contains, StartsWith or EndsWith with no // contains-ok: comment $(if ($AllowContains) { '(warn - -AllowContains)' } else { '(fail)' })" -Items $markupAssertFail
Write-FindingSection -Title 'Collection-membership Assert.Contains (info)' -Items $collectionMembershipInfo -Hint 'assert the whole list (Assert.Equal([...], list) / Assert.Single) instead of one membership check'

Write-Host ''
Write-Host 'contains-ok annotations (manager approves each):'
if ($containsOkAnnotations.Count -eq 0) {
    Write-Host '  (none)'
} else {
    foreach ($annotation in $containsOkAnnotations) {
        $flag = if ($annotation.IsMarkup) { " [MARKUP HAYSTACK - $(if ($AllowContains) { 'warn' } else { 'fail' })]" } else { '' }
        Write-Host "  $($annotation.File):$($annotation.Line): $($annotation.Reason)$flag"
    }
}

Write-Host ''
Write-Host "docs/ and $SpecPath changes (info):"
if ($docFindings.Count -eq 0) {
    Write-Host '  (none)'
} else {
    foreach ($d in $docFindings) {
        Write-Host "  $d"
    }
}

if ($GroupByFile -and $Scope -eq 'Branch') {
    # Everything the report can flag against a specific file, flattened into one (File, Category) list, so
    # a hardening pass can dispatch by file instead of finding by finding.
    $summarySource = New-Object System.Collections.Generic.List[object]
    foreach ($finding in $findings) {
        $summarySource.Add([PSCustomObject]@{ File = $finding.File; Category = $finding.Category }) | Out-Null
    }
    foreach ($untested in $untestedTexts) {
        $summarySource.Add([PSCustomObject]@{ File = $untested.File; Category = 'UntestedText' }) | Out-Null
    }
    foreach ($weakRefusal in $weakRefusalAsserts) {
        $summarySource.Add([PSCustomObject]@{ File = $weakRefusal.File; Category = 'WeakRefusalAssert' }) | Out-Null
    }

    $groupedByFile = $summarySource | Group-Object -Property File | ForEach-Object {
        $kindCounts = ($_.Group | Group-Object -Property Category | Sort-Object -Property Name |
            ForEach-Object { "$($_.Name)=$($_.Count)" }) -join ', '
        [PSCustomObject]@{
            File   = $_.Name
            Total  = $_.Count
            Counts = $kindCounts
        }
    } | Sort-Object -Property Total -Descending

    Write-Host ''
    Write-Host 'Findings grouped by file (-GroupByFile):'
    if ($groupedByFile.Count -eq 0) {
        Write-Host '  (none)'
    } else {
        foreach ($group in $groupedByFile) {
            Write-Host "  $($group.File): $($group.Total) total ($($group.Counts))"
        }
    }
}

Write-Host ''
$untestedFailing = $untestedTexts.Count -gt 0 -and -not $AllowUntested
$markupAssertFailing = $markupAssertFail.Count -gt 0 -and -not $AllowContains
$containsOkOnMarkupFailing = $containsOkOnMarkup.Count -gt 0 -and -not $AllowContains
if ($nullForgiving.Count -gt 0 -or $pragmaNoReason.Count -gt 0 -or $untestedFailing -or $markupAssertFailing -or
    $containsOkOnMarkupFailing) {
    $reasons = New-Object System.Collections.Generic.List[string]
    if ($nullForgiving.Count -gt 0) {
        $reasons.Add("$($nullForgiving.Count) null-forgiving usage(s)") | Out-Null
    }
    if ($pragmaNoReason.Count -gt 0) {
        $reasons.Add("$($pragmaNoReason.Count) reason-less pragma(s)") | Out-Null
    }
    if ($untestedFailing) {
        $reasons.Add("$($untestedTexts.Count) untested text(s)") | Out-Null
    }
    if ($markupAssertFailing) {
        $reasons.Add("$($markupAssertFail.Count) markup/string Contains assert(s)") | Out-Null
    }
    if ($containsOkOnMarkupFailing) {
        $reasons.Add("$($containsOkOnMarkup.Count) contains-ok annotation(s) on a markup/string haystack") | Out-Null
    }
    Write-Host "FAIL: $($reasons -join ', ')."
    exit 1
}

if ($untestedTexts.Count -gt 0) {
    Write-Host "WARN: $($untestedTexts.Count) untested text(s) allowed via -AllowUntested."
}
if ($markupAssertFail.Count -gt 0) {
    Write-Host "WARN: $($markupAssertFail.Count) markup/string Contains assert(s) allowed via -AllowContains."
}
if ($containsOkOnMarkup.Count -gt 0) {
    Write-Host "WARN: $($containsOkOnMarkup.Count) contains-ok annotation(s) on a markup/string haystack allowed via -AllowContains."
}

Write-Host 'OK: no null-forgiving operators, reason-less pragmas, or (blocking) untested texts / markup asserts in the diff.'
exit 0
