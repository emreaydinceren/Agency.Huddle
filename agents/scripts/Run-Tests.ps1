<#
.SYNOPSIS
    Runs `dotnet test` against the Huddle solution behind a machine-global mutex, tees output to a
    timestamped log under Conversation\logs, and prints a summary (per-assembly lines, totals, and a
    de-duplicated list of failing test names).

.DESCRIPTION
    Two or more `dotnet test` runs at once on this machine collide over a machine-global named pipe a
    test fixture registers, producing phantom failures. This script serialises every run - across every
    worktree and every agent on the box - behind a machine-global named mutex (Global\HuddleTestRun)
    before invoking dotnet test, and releases it in a finally block no matter how the run ends.

    A FULL run (neither -FilterClass nor -FilterMethod given) also passes Microsoft.Testing.Platform's
    `--diagnostic` and `--diagnostic-output-directory Conversation\logs\diag-<timestamp>`, so a failure
    that doesn't show up in the console output still leaves a trace log behind; the directory path is
    printed before the run starts. A filtered, ad-hoc run skips this - it's noise for a quick check.

    If a run exits non-zero with at least one "error" in the totals block but "failed: 0" (the runner
    itself broke - e.g. two runs colliding on the named pipe above even under the mutex - rather than a
    real test failing), the script prints "RUNNER ERROR (0 failed tests) - rerunning once" and reruns
    automatically, once. Both attempts' logs are kept and both summaries are printed; everything after
    (including -RedTask) uses the rerun's exit code, log and summary.

    Conversation/ is gitignored and always lives at the MAIN checkout's root, not necessarily the
    worktree this script is invoked from - logs and red files land there regardless of which worktree ran
    the tests. The main checkout is resolved via `git rev-parse --path-format=absolute --git-common-dir`
    (its parent), so this works unmodified whether invoked from the main checkout or any worktree, on any
    machine, in any session; override with -MainRepoRoot, -LogDir or -RedDir directly if you need
    somewhere else.

.PARAMETER FilterClass
    One or more `--filter-class` values, e.g. "*PromptDefaultsFileTests".

.PARAMETER FilterMethod
    One or more `--filter-method` values, e.g. "*.Foo_Bar".

.PARAMETER Label
    A short label used in the log file name. Defaults to 'run'.

.PARAMETER NoBuild
    Passes --no-build to dotnet test.

.PARAMETER Solution
    Path to the .slnx/.sln to test. Defaults to Huddle.slnx at the current git worktree's root (found
    via `git rev-parse --show-toplevel`), so this script works unmodified from any worktree.

.PARAMETER MainRepoRoot
    Override for the main checkout's root (where Conversation\logs and Conversation\red live). Defaults
    to the parent of `git rev-parse --path-format=absolute --git-common-dir`.

.PARAMETER LogDir
    Override for the log directory. Defaults to Conversation\logs under -MainRepoRoot.

.PARAMETER RedDir
    Override for the red-file directory (used with -RedTask). Defaults to Conversation\red under
    -MainRepoRoot.

.PARAMETER ParseOnly
    Instead of running tests, parses an existing log file at this path and prints the same summary. Used
    to verify the summary/failure-name parsing against a real (or fabricated) log without running tests.

.PARAMETER RedTask
    When set, before doing anything else, checks `git stash list` in the caller's toplevel; if it is
    non-empty, refuses with exit 9 and prints the stash entries (a red recorded on top of stashed work is
    misleading at review time). Otherwise, after the test run (or parse), collect red lines (compiler
    errors and failed test details) and write them to <RedDir>\<RedTask>.txt. If the run passed,
    exits 3 with a message instead of writing a file. If the log contains analyzer diagnostics
    (IDE/CA/S/xUnit errors), exits 4 without writing a file, and prints the offending diagnostic lines
    (de-duplicated, "file(line,col): error XX1234: message") under the "RED POLLUTED" header, indicating
    the test code itself has errors to fix first. See -NewNames for the exit-8 identifier/code gate. The
    red file's header also records `git diff --stat -- src/` and `git status --porcelain -- src/` (the
    latter so untracked new src files, invisible to `git diff --stat`, are visible at review too), so
    implementation-before-red is visible at review.

.PARAMETER Force
    With -RedTask, allows overwriting an existing <RedDir>\<RedTask>.txt. Without it, an existing
    file causes the script to exit 6 without writing anything.

.PARAMETER ExpectFail
    Comma-separated short test method names (e.g. "ComputeVersion_Is16LowerHex,Foo_Bar") to compare
    against the run's actual failing tests, used with -RedTask. The actual set is the last dotted
    segment of each "failed <FQN>" line found (for a compile-error red, with no failing test lines, the
    actual set is empty). If the expected and actual sets differ, prints "EXPECTED vs ACTUAL" (missing
    and unexpected names) and exits 5 without writing a file. If they match, proceeds normally.

.PARAMETER NewNames
    Comma-separated type/member names a TDD red is allowed to reference before they exist, used with
    -RedTask. A "not-yet-implemented" red is only ever one of these compiler codes, each of which quotes
    the missing identifier in its message: CS0246 (type/namespace not found), CS0103 (name does not
    exist), CS1061 (no such member), CS0117 (no such member on type), CS0234 (type/namespace does not
    exist in namespace), CS1739 (no argument given for parameter), CS0411 (type arguments cannot be
    inferred). When -NewNames is given, every line with one of those codes must quote an identifier in
    the list, or the run is rejected (exit 8, no file written) and the offending lines are printed. Any
    compiler error OUTSIDE that allowed code set (e.g. CS7036, CS1503, CS0029) is always rejected the
    same way, whether or not -NewNames is given - it means the red is not a clean "doesn't exist yet"
    failure. Without -NewNames, the code-set check still runs (disallowed codes still fail with exit 8),
    but identifiers are not checked against a list.

.EXAMPLE
    pwsh agents/scripts/Run-Tests.ps1 -FilterClass "*PromptDefaultsFileTests" -Label prompt-defaults

.EXAMPLE
    pwsh agents/scripts/Run-Tests.ps1 -FilterMethod "*.DefaultsFile_HasExactlyTheCatalogKeys" -Label smoke -NoBuild

.EXAMPLE
    pwsh agents/scripts/Run-Tests.ps1 -RedTask my-task

.EXAMPLE
    pwsh agents/scripts/Run-Tests.ps1 -RedTask my-task -ExpectFail "ComputeVersion_Is16LowerHex" -Force

.EXAMPLE
    pwsh agents/scripts/Run-Tests.ps1 -RedTask my-task -NewNames "TaskId,TaskId.Parse"
#>
[CmdletBinding()]
param(
    [string[]]$FilterClass,
    [string[]]$FilterMethod,
    [string]$Label = 'run',
    [switch]$NoBuild,
    [string]$Solution,
    [string]$MainRepoRoot,
    [string]$LogDir,
    [string]$RedDir,
    [string]$ParseOnly,
    [string]$RedTask,
    [switch]$Force,
    [string]$ExpectFail,
    [string]$NewNames
)

$ErrorActionPreference = 'Stop'

# Conversation/ is gitignored and lives at the MAIN checkout's root, not necessarily the worktree this
# script is invoked from - logs always land under the main checkout's Conversation\logs regardless of
# which worktree ran the tests. `git rev-parse --git-common-dir` resolves to the main checkout's .git
# directory even when run from a linked worktree, so its parent is the main checkout root in every case.
if (-not $MainRepoRoot) {
    $gitCommonDir = (git rev-parse --path-format=absolute --git-common-dir).Trim()
    $MainRepoRoot = (Resolve-Path -LiteralPath (Join-Path $gitCommonDir '..')).Path
}
if (-not $LogDir) {
    $LogDir = Join-Path $MainRepoRoot 'Conversation\logs'
}
if (-not $RedDir) {
    $RedDir = Join-Path $MainRepoRoot 'Conversation\red'
}

if ($RedTask) {
    # A red recorded on top of stashed work is misleading at review time - the diff the red file's
    # header shows would not be the whole story. Refuse before doing anything else.
    $callerToplevel = (git rev-parse --show-toplevel).Trim()
    $stashList = @(& git -C $callerToplevel stash list 2>&1)
    if (($stashList | Where-Object { $_ -ne '' }).Count -gt 0) {
        Write-Host "RED REFUSED: git stash list is non-empty in $callerToplevel - pop or drop the stash before recording a red baseline."
        foreach ($s in $stashList) {
            Write-Host "  $s"
        }
        exit 9
    }
}

<#
.SYNOPSIS
    Parses `dotnet test` (Microsoft.Testing.Platform + xunit v3) console output into a summary object.

.DESCRIPTION
    Recognises three families of line, learned from a real run's output (see Conversation/logs and the
    delivery report for a sample):
      - Per-assembly result lines:      "<dll path> (net10.0|x64) <passed|failed|Zero tests ran> (<dur>)"
      - The totals block:               "  total: N", "  failed: N", "  succeeded: N", "  skipped: N",
                                         "  duration: <dur>", plus "Passed!"/"Failed!" on the summary line.
      - Individual failing tests:       xunit v3's in-proc console reporter prints a failing test as a
                                         line starting with "failed " followed by the fully-qualified
                                         test name and a "(<duration>)" suffix, e.g.
                                         "failed Agency.Huddle.Tests.Foo.Bar_Baz (12ms)". This is distinct
                                         from the per-assembly "<dll> ... failed (<dur>)" line because it
                                         has no "(net10.0|x64)" segment and the name is dotted, not a path.
#>
function Get-TestRunSummary {
    # Deliberately not [Parameter(Mandatory)]: PowerShell's mandatory-parameter binder rejects any
    # string[] element that is an empty string (e.g. "Cannot bind argument to parameter 'Lines' because
    # it is an empty string"), and dotnet test output is full of blank lines. Validate null manually
    # instead.
    param([string[]]$Lines)
    if ($null -eq $Lines) {
        throw 'Get-TestRunSummary: -Lines is required.'
    }

    $assemblyLines = @()
    $totals = [ordered]@{
        total     = $null
        failed    = $null
        succeeded = $null
        skipped   = $null
        duration  = $null
    }
    $failingTests = New-Object System.Collections.Generic.List[string]
    $zeroTestsRan = $false
    $overallResult = $null

    foreach ($line in $Lines) {
        if ($line -match 'Zero tests ran') {
            $zeroTestsRan = $true
        }

        # Per-assembly summary: "<path>.dll (net10.0|x64) <passed|failed|Zero tests ran> (<dur>)"
        if ($line -match '^\s*(?<path>\S+\.dll)\s+\((?<tfm>[^)]+)\)\s+(?<status>passed|failed|Zero tests ran)\s+\((?<dur>[^)]+)\)\s*$') {
            $assemblyLines += $line.Trim()
            continue
        }

        # Overall summary line: "Test run summary: Passed!" / "Test run summary: Failed!"
        if ($line -match '^Test run summary:\s*(?<result>Passed|Failed)!') {
            $overallResult = $Matches.result
            continue
        }

        # Totals block lines: "  total: 3", "  failed: 0", etc.
        if ($line -match '^\s*(?<key>total|failed|succeeded|skipped|duration|error):\s*(?<value>.+?)\s*$') {
            $key = $Matches.key
            if ($totals.Contains($key)) {
                $totals[$key] = $Matches.value
            }
            continue
        }

        # Individual failing test: "failed <Fully.Qualified.Name> (<dur>)" - no "(tfm|arch)" segment, so
        # this cannot be confused with the per-assembly line above.
        if ($line -match '^\s*failed\s+(?<name>\S+)\s+\(') {
            $name = $Matches.name
            if (-not $failingTests.Contains($name)) {
                $failingTests.Add($name) | Out-Null
            }
            continue
        }
    }

    [PSCustomObject]@{
        AssemblyLines = $assemblyLines
        OverallResult = $overallResult
        Totals        = $totals
        FailingTests  = $failingTests
        ZeroTestsRan  = $zeroTestsRan
    }
}

function Get-RedLines {
    param([string[]]$Lines, [int]$ExitCode)

    $redLines = New-Object System.Collections.Generic.List[string]
    $analyzerDiagLines = New-Object System.Collections.Generic.List[string]
    $hasAnalyzerDiags = $false

    for ($i = 0; $i -lt $Lines.Length; $i++) {
        $line = $Lines[$i]

        # Detect compiler errors: "error CSxxxx:" format
        if ($line -match 'error (CS\d{4}):') {
            $redLines.Add($line) | Out-Null
        }

        # Detect analyzer diagnostics: "error (IDE|CA|S|xUnit)xxxx", e.g.
        # "file(line,col): error IDE0060: message [project]". Strip the trailing " [project]" segment so
        # the printed diagnostic reads "file(line,col): error XX1234: message".
        if ($line -match 'error (IDE|CA|S|xUnit)\d+') {
            $hasAnalyzerDiags = $true
            $diagLine = $line.Trim()
            $diagLine = $diagLine -replace '\s*\[[^\[\]]*\]\s*$', ''
            $analyzerDiagLines.Add($diagLine) | Out-Null
        }

        # Extract failed test lines and their following message lines (up to 3)
        if ($line -match '^\s*failed\s+\S+\s+\(') {
            $redLines.Add($line) | Out-Null
            # Capture up to 3 following lines for the failure message
            for ($j = 1; $j -le 3 -and ($i + $j) -lt $Lines.Length; $j++) {
                $redLines.Add($Lines[$i + $j]) | Out-Null
            }
        }
    }

    [PSCustomObject]@{
        RedLines          = $redLines
        HasAnalyzerDiags  = $hasAnalyzerDiags
        AnalyzerDiagLines = ($analyzerDiagLines | Select-Object -Unique)
    }
}

<#
.SYNOPSIS
    Compares the expected set of failing-test short names (from -ExpectFail) against the actual set
    derived from a test-run summary's FailingTests (fully-qualified names).
#>
function Compare-ExpectFail {
    param([string]$ExpectFail, $Summary)

    $expected = @($ExpectFail -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne '' })
    $actual = @($Summary.FailingTests | ForEach-Object { ($_ -split '\.')[-1] })

    $expectedSet = [System.Collections.Generic.HashSet[string]]::new([string[]]$expected)
    $actualSet = [System.Collections.Generic.HashSet[string]]::new([string[]]$actual)

    $missing = @($expectedSet | Where-Object { -not $actualSet.Contains($_) })
    $unexpected = @($actualSet | Where-Object { -not $expectedSet.Contains($_) })

    [PSCustomObject]@{
        Matches    = ($missing.Count -eq 0 -and $unexpected.Count -eq 0)
        Missing    = $missing
        Unexpected = $unexpected
    }
}

# Compiler codes that mean "this identifier doesn't exist yet" - the shape every clean TDD red takes.
# Any other CS error code (CS7036 wrong-argument-count, CS1503 wrong-argument-type, CS0029 cannot
# implicitly convert, etc.) means the red is broken in some other way, not just "not implemented yet".
$script:AllowedRedCodes = @('CS0246', 'CS0103', 'CS1061', 'CS0117', 'CS0234', 'CS1739', 'CS0411')

<#
.SYNOPSIS
    Gates a red run for -NewNames: every compiler error must be one of $script:AllowedRedCodes, and (when
    -NewNames is given) every such error's quoted identifier must be in the allowed name list.
#>
function Test-RedGate {
    param([string[]]$Lines, [string]$NewNames)

    $allowedNames = $null
    if ($NewNames) {
        $allowedNames = [System.Collections.Generic.HashSet[string]]::new(
            [string[]]@($NewNames -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne '' }))
    }

    $disallowedLines = New-Object System.Collections.Generic.List[string]
    foreach ($line in $Lines) {
        $codeMatch = [regex]::Match($line, 'error (?<code>CS\d{4}):')
        if (-not $codeMatch.Success) {
            continue
        }

        $code = $codeMatch.Groups['code'].Value
        if ($script:AllowedRedCodes -notcontains $code) {
            $disallowedLines.Add($line.Trim()) | Out-Null
            continue
        }

        if ($null -ne $allowedNames) {
            # CS1061/CS0117 read "'Receiver' does not contain a definition for 'Member'": the new
            # identifier is the member, the second quoted name. Every other code names it first.
            $names = [regex]::Matches($line, "'(?<name>[^']+)'")
            # CS1739 reads "The best overload for 'Type' does not have a parameter named 'param'".
            $nameIndex = if ($code -in 'CS1061', 'CS0117', 'CS1739') { 1 } else { 0 }
            if ($names.Count -gt $nameIndex -and -not $allowedNames.Contains($names[$nameIndex].Groups['name'].Value)) {
                $disallowedLines.Add($line.Trim()) | Out-Null
            }
        }
    }

    [PSCustomObject]@{
        Passed          = ($disallowedLines.Count -eq 0)
        DisallowedLines = ($disallowedLines | Select-Object -Unique)
    }
}

function Write-TestRunSummary {
    param($Summary, [string]$LogPath)

    Write-Host ''
    Write-Host "Log: $LogPath"

    Write-Host ''
    Write-Host 'Per-assembly results:'
    foreach ($line in $Summary.AssemblyLines) {
        Write-Host "  $line"
    }

    Write-Host ''
    Write-Host "Totals (overall: $($Summary.OverallResult)):"
    foreach ($key in $Summary.Totals.Keys) {
        if ($null -ne $Summary.Totals[$key]) {
            Write-Host "  $($key): $($Summary.Totals[$key])"
        }
    }

    Write-Host ''
    if ($Summary.FailingTests.Count -gt 0) {
        Write-Host 'Failing tests:'
        foreach ($name in $Summary.FailingTests) {
            Write-Host "  $name"
        }
    }
    else {
        Write-Host 'Failing tests: (none)'
    }

    if ($Summary.ZeroTestsRan) {
        Write-Host ''
        Write-Host '*** WARNING: "Zero tests ran" appeared in the output - a filter matched nothing in at least one assembly. ***' -ForegroundColor Yellow
    }
}

if ($ParseOnly) {
    $lines = Get-Content -LiteralPath $ParseOnly

    if ($RedTask) {
        # RedTask mode: extract red lines and write to file
        if (-not (Test-Path -LiteralPath $RedDir)) {
            New-Item -ItemType Directory -Force -Path $RedDir | Out-Null
        }

        $redFilePath = Join-Path $RedDir "$RedTask.txt"

        # Note: we don't have the actual exit code from a parse-only invocation, so assume failure (1)
        $redData = Get-RedLines -Lines $lines -ExitCode 1

        if ($redData.HasAnalyzerDiags) {
            Write-Host 'RED POLLUTED: fix these analyzer errors in your test code first'
            foreach ($diagLine in $redData.AnalyzerDiagLines) {
                Write-Host "  $diagLine"
            }
            exit 4
        }

        $redGate = Test-RedGate -Lines $lines -NewNames $NewNames
        if (-not $redGate.Passed) {
            Write-Host 'RED REJECTED: compile errors outside the allowed "not implemented yet" set, or naming an identifier not in -NewNames'
            foreach ($badLine in $redGate.DisallowedLines) {
                Write-Host "  $badLine"
            }
            exit 8
        }

        if ($ExpectFail) {
            $summary = Get-TestRunSummary -Lines $lines
            $cmp = Compare-ExpectFail -ExpectFail $ExpectFail -Summary $summary
            if (-not $cmp.Matches) {
                Write-Host 'EXPECTED vs ACTUAL'
                Write-Host "  missing:    $($cmp.Missing -join ', ')"
                Write-Host "  unexpected: $($cmp.Unexpected -join ', ')"
                exit 5
            }
        }

        if ((Test-Path -LiteralPath $redFilePath) -and (-not $Force)) {
            Write-Host "Red file already exists: $redFilePath (pass -Force to overwrite)"
            exit 6
        }

        # De-duplicate red lines
        $uniqueRedLines = $redData.RedLines | Select-Object -Unique

        if ($uniqueRedLines.Count -eq 0) {
            Write-Host 'No red lines found in log'
            exit 1
        }

        # Write to file with header
        $timestamp = Get-Date -Format 'o'
        $diffStat = @(& git diff --stat -- src/ 2>&1)
        $statusPorcelain = @(& git status --porcelain -- src/ 2>&1)
        $fileContent = @(
            "# red for $RedTask, $timestamp, command: (parsed log)"
            '# git diff --stat -- src/'
            $diffStat
            '# git status --porcelain -- src/'
            $statusPorcelain
            $uniqueRedLines
        )

        Set-Content -LiteralPath $redFilePath -Value $fileContent
        Write-Host "Red file: $redFilePath"
        Write-Host "Lines: $($uniqueRedLines.Count)"
        exit 0
    } else {
        # Normal parse mode: print summary
        $summary = Get-TestRunSummary -Lines $lines
        Write-TestRunSummary -Summary $summary -LogPath $ParseOnly
        return
    }
}

if (-not $Solution) {
    $worktreeRoot = (git rev-parse --show-toplevel).Trim()
    # A filtered run targets Huddle.Tests only: across the whole solution the ACP test assembly
    # matches nothing, reports "Zero tests ran" and turns the exit code non-zero (8).
    $Solution = if ($FilterClass -or $FilterMethod) {
        Join-Path $worktreeRoot 'tests/Huddle.Tests/Huddle.Tests.csproj'
    } else {
        Join-Path $worktreeRoot 'Huddle.slnx'
    }
}

if (-not (Test-Path -LiteralPath $Solution)) {
    throw "Solution not found: $Solution"
}

if (-not (Test-Path -LiteralPath $LogDir)) {
    New-Item -ItemType Directory -Force -Path $LogDir | Out-Null
}

$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'

# A filtered run (-FilterClass/-FilterMethod) is a targeted, ad-hoc check; a full run is the one worth
# capturing MTP's diagnostic trace for, so only a full run gets --diagnostic.
$isFullRun = -not ($FilterClass -or $FilterMethod)

$baseTestArgs = @($Solution)
if ($NoBuild) {
    $baseTestArgs += '--no-build'
}
$baseTestArgs += '--'
# A value containing commas (e.g. "*A,*B") means "run both classes/methods", not a single literal
# filter with a comma in it - split before passing to dotnet test, or "*A,*B" matches nothing.
foreach ($c in $FilterClass) {
    foreach ($part in ($c -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne '' })) {
        $baseTestArgs += @('--filter-class', $part)
    }
}
foreach ($m in $FilterMethod) {
    foreach ($part in ($m -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne '' })) {
        $baseTestArgs += @('--filter-method', $part)
    }
}

<#
.SYNOPSIS
    True when a run's failure looks like the Microsoft.Testing.Platform runner itself broke (a non-zero
    exit code and at least one "error" in the totals block) rather than a real test failure ("failed: 0").
    Two concurrent runs colliding over the fixture's machine-global named pipe (see the script banner)
    produce exactly this shape once in a while even under the mutex - e.g. a runner crash mid-discovery.
#>
function Test-IsRunnerError {
    param([int]$ExitCode, $Summary)

    if ($ExitCode -eq 0) {
        return $false
    }

    $errorCount = 0
    if ($Summary.Totals['error']) {
        $errorCount = [int]$Summary.Totals['error']
    }
    $failedCount = 0
    if ($Summary.Totals['failed']) {
        $failedCount = [int]$Summary.Totals['failed']
    }

    return ($errorCount -gt 0 -and $failedCount -eq 0)
}

$mutex = New-Object System.Threading.Mutex($false, 'Global\HuddleTestRun')
$acquired = $false
try {
    # Wait up to 30 minutes total, in 5s slices, printing "waiting for another test run" once as soon
    # as the first slice shows the mutex is held elsewhere.
    $maxWait = [TimeSpan]::FromMinutes(30)
    $waitStart = Get-Date
    $printedWaiting = $false
    while (-not $acquired) {
        $acquired = $mutex.WaitOne([TimeSpan]::FromSeconds(5))
        if (-not $acquired) {
            if (-not $printedWaiting) {
                Write-Host 'waiting for another test run'
                $printedWaiting = $true
            }
            if ((Get-Date) - $waitStart -ge $maxWait) {
                throw "Timed out after 30 minutes waiting for Global\HuddleTestRun."
            }
        }
    }

    $mutexWaitSeconds = ((Get-Date) - $waitStart).TotalSeconds
    $mutexWaitLine = $null
    if ($mutexWaitSeconds -gt 1) {
        $mutexWaitLine = "waited $([Math]::Round($mutexWaitSeconds, 1)) s for another test run"
        Write-Host $mutexWaitLine
    }

    # Up to two attempts: a second only happens when the first looks like a runner error (see
    # Test-IsRunnerError) rather than a real red. Both logs are kept and both summaries are printed;
    # the loop's final $exitCode/$lines/$summary/$logPath (the rerun's, if there was one) drive
    # everything after it, including -RedTask.
    $maxAttempts = 2
    for ($attempt = 1; $attempt -le $maxAttempts; $attempt++) {
        $attemptTimestamp = if ($attempt -eq 1) { $timestamp } else { Get-Date -Format 'yyyyMMdd-HHmmss' }
        $logPath = Join-Path $LogDir "$Label-$attemptTimestamp.log"

        $testArgs = [string[]]$baseTestArgs
        if ($isFullRun) {
            $diagDir = Join-Path $LogDir "diag-$attemptTimestamp"
            $testArgs += @('--diagnostic', '--diagnostic-output-directory', $diagDir)
            Write-Host "Diagnostic output directory: $diagDir"
        }

        if ($attempt -eq 1 -and $mutexWaitLine) {
            Set-Content -LiteralPath $logPath -Value $mutexWaitLine
        }

        Write-Host "Running (attempt $attempt of $maxAttempts): dotnet test $($testArgs -join ' ')"
        & dotnet test @testArgs 2>&1 | Tee-Object -FilePath $logPath -Append | ForEach-Object { Write-Host $_ }
        $exitCode = $LASTEXITCODE

        $lines = Get-Content -LiteralPath $logPath
        $summary = Get-TestRunSummary -Lines $lines
        Write-TestRunSummary -Summary $summary -LogPath $logPath

        if ($attempt -lt $maxAttempts -and (Test-IsRunnerError -ExitCode $exitCode -Summary $summary)) {
            Write-Host 'RUNNER ERROR (0 failed tests) - rerunning once'
            continue
        }

        break
    }

    if ($RedTask) {
        # RedTask mode: handle red file extraction
        if (-not (Test-Path -LiteralPath $RedDir)) {
            New-Item -ItemType Directory -Force -Path $RedDir | Out-Null
        }

        $redFilePath = Join-Path $RedDir "$RedTask.txt"

        # If the run passed, exit with code 3 and no red file
        if ($exitCode -eq 0) {
            Write-Host 'RED REQUIRED: the run is green; no red file written'
            exit 3
        }

        # Extract red lines
        $redData = Get-RedLines -Lines $lines -ExitCode $exitCode

        # If analyzer diagnostics found, exit 4
        if ($redData.HasAnalyzerDiags) {
            Write-Host 'RED POLLUTED: fix these analyzer errors in your test code first'
            foreach ($diagLine in $redData.AnalyzerDiagLines) {
                Write-Host "  $diagLine"
            }
            exit 4
        }

        $redGate = Test-RedGate -Lines $lines -NewNames $NewNames
        if (-not $redGate.Passed) {
            Write-Host 'RED REJECTED: compile errors outside the allowed "not implemented yet" set, or naming an identifier not in -NewNames'
            foreach ($badLine in $redGate.DisallowedLines) {
                Write-Host "  $badLine"
            }
            exit 8
        }

        if ($ExpectFail) {
            $cmp = Compare-ExpectFail -ExpectFail $ExpectFail -Summary $summary
            if (-not $cmp.Matches) {
                Write-Host 'EXPECTED vs ACTUAL'
                Write-Host "  missing:    $($cmp.Missing -join ', ')"
                Write-Host "  unexpected: $($cmp.Unexpected -join ', ')"
                exit 5
            }
        }

        if ((Test-Path -LiteralPath $redFilePath) -and (-not $Force)) {
            Write-Host "Red file already exists: $redFilePath (pass -Force to overwrite)"
            exit 6
        }

        # De-duplicate red lines
        $uniqueRedLines = $redData.RedLines | Select-Object -Unique

        # Write to file with header
        $timestamp = Get-Date -Format 'o'
        $command = "dotnet test $($testArgs -join ' ')"
        $diffStat = @(& git diff --stat -- src/ 2>&1)
        $statusPorcelain = @(& git status --porcelain -- src/ 2>&1)
        $fileContent = @(
            "# red for $RedTask, $timestamp, command: $command"
            '# git diff --stat -- src/'
            $diffStat
            '# git status --porcelain -- src/'
            $statusPorcelain
            $uniqueRedLines
        )

        Set-Content -LiteralPath $redFilePath -Value $fileContent
        Write-Host "Red file: $redFilePath"
        Write-Host "Lines: $($uniqueRedLines.Count)"
        exit 0
    }

    exit $exitCode
}
finally {
    if ($acquired) {
        $mutex.ReleaseMutex() | Out-Null
    }
    $mutex.Dispose()
}
