<#
.SYNOPSIS
    Runs a batch of source mutations from a .psd1 spec against a single filter-class, reporting which
    mutations the test suite catches and which survive.

.DESCRIPTION
    Unlike Prove-Mutation.ps1 (one mutation, delegates to Run-Tests.ps1), this script drives several
    mutations from one spec file under a SINGLE acquisition of the machine-global test mutex
    (Global\HuddleTestRun, the same one Run-Tests.ps1 uses) - it must not shell out to Run-Tests.ps1 for
    the per-mutation test runs, because that would try to re-acquire the same mutex from a child process
    and deadlock against the mutex this script is already holding.

    The spec is a .psd1 with this shape:

        @{
            FilterClass = '*FooTests'
            Mutations   = @(
                @{ Name = 'off-by-one';   File = 'src/Huddle.App/Foo.cs'; Find = '<= 16'; Replace = '< 16' }
                @{ Name = 'harmless-tweak'; File = 'src/Huddle.App/Foo.cs'; Line = 12; Replace = '        // no-op' }
            )
        }

    `File` is relative to the CALLER's git toplevel (`git rev-parse --show-toplevel`). Each mutation entry
    is either `Find`/`Replace` (Find must occur exactly once in File) or `Line`/`Replace` (1-based line
    number, whole-line replacement, that line's own line ending preserved) - the same two shapes
    Prove-Mutation.ps1 accepts, and the same byte-faithful (line endings, BOM) substitution/restore logic.

    Conversation/ is gitignored and always lives at the MAIN checkout's root, not necessarily the
    worktree this script is invoked from - the batch log lands there regardless of which worktree ran the
    mutations. The main checkout is resolved via `git rev-parse --path-format=absolute --git-common-dir`
    (its parent), so this works unmodified whether invoked from the main checkout or any worktree, on any
    machine, in any session; override with -MainRepoRoot or -LogDir directly if you need somewhere else.

    Sequence:
      1. Build once, up front, via Build.ps1 (the whole solution, resolved next to this script via
         $PSScriptRoot), before taking the mutex.
      2. Acquire Global\HuddleTestRun once for the whole batch.
      3. For each mutation, in spec order:
         - Apply the mutation (byte-faithful).
         - Build only the test project incrementally: `dotnet build <Huddle.Tests.csproj> --no-restore -v q`
           (mutations change code, so a fresh per-mutation build is required - `--no-build` on the test
           step below is only valid because this build just ran).
         - On build failure: record BUILD-FAILED, no test run.
         - On build success: `dotnet test <Huddle.Tests.csproj> --no-build -- --filter-class <FilterClass>`.
           Non-zero exit -> CAUGHT (a test went red because of the mutation). Zero exit -> SURVIVED (the
           suite cannot see the mutation - the coverage claim for that spot is false).
         - Restore the original bytes in a finally block and verify the restore is byte-identical,
           regardless of the mutation's outcome, before moving to the next mutation.
      4. Release the mutex.
      5. Print a table: Name | Result | failing tests (only for CAUGHT).
      6. Print `git status --porcelain -- src/` so a mutation that (via a bug in this script, or a crash)
         left the tree dirty is visible.

    Exit 0 if every mutation was CAUGHT. Exit 7 if any mutation SURVIVED. Exit 2 if any mutation's build
    failed (BUILD-FAILED short-circuits that mutation's test step, but the batch continues to the rest).
    A malformed spec, missing file, or a Find/Line target that doesn't resolve throws (uncaught,
    non-zero exit) rather than silently skipping a mutation.

.PARAMETER Spec
    Path to the .psd1 spec file described above.

.PARAMETER MainRepoRoot
    Override for the main checkout's root (where Conversation\logs lives). Defaults to the parent of
    `git rev-parse --path-format=absolute --git-common-dir`.

.PARAMETER LogDir
    Override for the log directory. Defaults to Conversation\logs under -MainRepoRoot.

.EXAMPLE
    pwsh agents/scripts/Prove-Mutations.ps1 -Spec agents/scripts/specs/taskid-mutations.psd1
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$Spec,
    [string]$MainRepoRoot,
    [string]$LogDir
)

$ErrorActionPreference = 'Stop'

# Conversation/ is gitignored and lives at the MAIN checkout's root, not necessarily the worktree this
# script is invoked from - logs always land under the main checkout's Conversation\logs regardless of
# which worktree ran the mutations. `git rev-parse --git-common-dir` resolves to the main checkout's .git
# directory even when run from a linked worktree, so its parent is the main checkout root in every case.
if (-not $MainRepoRoot) {
    $gitCommonDir = (git rev-parse --path-format=absolute --git-common-dir).Trim()
    $MainRepoRoot = (Resolve-Path -LiteralPath (Join-Path $gitCommonDir '..')).Path
}
if (-not $LogDir) {
    $LogDir = Join-Path $MainRepoRoot 'Conversation\logs'
}

if (-not (Test-Path -LiteralPath $Spec -PathType Leaf)) {
    throw "Spec not found: $Spec"
}

$resolvedSpec = (Resolve-Path -LiteralPath $Spec).Path
$specData = Import-PowerShellDataFile -LiteralPath $resolvedSpec

if (-not $specData.ContainsKey('FilterClass') -or [string]::IsNullOrWhiteSpace($specData.FilterClass)) {
    throw "Spec must define a non-empty FilterClass: $resolvedSpec"
}
if (-not $specData.ContainsKey('Mutations') -or $specData.Mutations.Count -eq 0) {
    throw "Spec must define at least one entry in Mutations: $resolvedSpec"
}

$worktreeRoot = (git rev-parse --show-toplevel).Trim()
$csprojPath = Join-Path $worktreeRoot 'tests/Huddle.Tests/Huddle.Tests.csproj'
if (-not (Test-Path -LiteralPath $csprojPath -PathType Leaf)) {
    throw "Test project not found: $csprojPath"
}

if (-not (Test-Path -LiteralPath $LogDir)) {
    New-Item -ItemType Directory -Force -Path $LogDir | Out-Null
}

$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$logPath = Join-Path $LogDir "prove-mutations-$timestamp.log"

function Write-BatchLog {
    param([string]$Text)
    Add-Content -LiteralPath $logPath -Value $Text
    Write-Host $Text
}

function Add-RawLog {
    param([object[]]$Lines)
    foreach ($l in $Lines) {
        Add-Content -LiteralPath $logPath -Value $l
    }
}

Write-BatchLog "Prove-Mutations: spec=$resolvedSpec filterClass=$($specData.FilterClass) mutations=$($specData.Mutations.Count)"

# Step 1: build once, up front, before taking the mutex.
$buildScript = Join-Path $PSScriptRoot 'Build.ps1'
Write-BatchLog "Initial build via $buildScript"
& $buildScript
if ($LASTEXITCODE -ne 0) {
    throw "Initial build failed (exit $LASTEXITCODE) - see Build.ps1's own log."
}

$results = New-Object System.Collections.Generic.List[object]
$anySurvived = $false
$anyBuildFailed = $false

# Step 2: acquire the machine-global test mutex ONCE for the whole batch (same mutex Run-Tests.ps1 uses -
# duplicated here rather than shelling out to Run-Tests.ps1, which would try to re-acquire it and deadlock).
$mutex = New-Object System.Threading.Mutex($false, 'Global\HuddleTestRun')
$acquired = $false
try {
    $maxWait = [TimeSpan]::FromMinutes(30)
    $waitStart = Get-Date
    $printedWaiting = $false
    while (-not $acquired) {
        $acquired = $mutex.WaitOne([TimeSpan]::FromSeconds(5))
        if (-not $acquired) {
            if (-not $printedWaiting) {
                Write-BatchLog 'waiting for another test run'
                $printedWaiting = $true
            }
            if ((Get-Date) - $waitStart -ge $maxWait) {
                throw "Timed out after 30 minutes waiting for Global\HuddleTestRun."
            }
        }
    }

    foreach ($mutation in $specData.Mutations) {
        $name = $mutation.Name
        if ([string]::IsNullOrWhiteSpace($name)) {
            throw "A mutation entry is missing Name: $resolvedSpec"
        }
        if (-not $mutation.ContainsKey('File') -or [string]::IsNullOrWhiteSpace($mutation.File)) {
            throw "Mutation '$name': missing File."
        }

        $filePath = Join-Path $worktreeRoot $mutation.File
        if (-not (Test-Path -LiteralPath $filePath -PathType Leaf)) {
            throw "Mutation '$name': file not found: $filePath"
        }
        $resolvedFile = (Resolve-Path -LiteralPath $filePath).Path

        $originalBytes = [IO.File]::ReadAllBytes($resolvedFile)
        $hasBom = $originalBytes.Length -ge 3 -and $originalBytes[0] -eq 0xEF -and $originalBytes[1] -eq 0xBB -and $originalBytes[2] -eq 0xBF
        $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
        $text = $utf8NoBom.GetString($originalBytes)

        $mutationDescription = $null
        $mutatedText = $null

        if ($mutation.ContainsKey('Line')) {
            $parts = [regex]::Split($text, '(\r\n|\r|\n)')
            $lineIndex = ([int]$mutation.Line - 1) * 2
            if ([int]$mutation.Line -lt 1 -or $lineIndex -ge $parts.Length) {
                throw "Mutation '$name': -Line $($mutation.Line) is out of range for $resolvedFile."
            }
            $originalLineContent = $parts[$lineIndex]
            $mutationDescription = "line $($mutation.Line) ('$originalLineContent' -> '$($mutation.Replace)')"
            $parts[$lineIndex] = $mutation.Replace
            $mutatedText = -join $parts
        }
        elseif ($mutation.ContainsKey('Find')) {
            $occurrences = ([regex]::Matches($text, [regex]::Escape($mutation.Find))).Count
            if ($occurrences -ne 1) {
                throw "Mutation '$name': -Find occurs $occurrences times in $resolvedFile (expected exactly 1)."
            }
            $mutationDescription = "'$($mutation.Find)' -> '$($mutation.Replace)'"
            $mutatedText = $text.Replace($mutation.Find, $mutation.Replace)
        }
        else {
            throw "Mutation '$name': must specify either Find or Line."
        }

        $result = 'BUILD-FAILED'
        $failingTests = New-Object System.Collections.Generic.List[string]

        try {
            $mutatedBytes = if ($hasBom) {
                (New-Object System.Text.UTF8Encoding($true)).GetBytes($mutatedText)
            } else {
                $utf8NoBom.GetBytes($mutatedText)
            }
            [IO.File]::WriteAllBytes($resolvedFile, $mutatedBytes)

            Write-BatchLog "Mutation '$name': $mutationDescription in $resolvedFile"

            # Mutations change code, so the test project must be rebuilt per mutation - --no-build below
            # on the test step is only valid because this build just ran against the mutated source.
            $buildOut = @(& dotnet build $csprojPath --no-restore -v q 2>&1)
            Add-RawLog $buildOut
            if ($LASTEXITCODE -ne 0) {
                $result = 'BUILD-FAILED'
                $anyBuildFailed = $true
                Write-BatchLog "Mutation '$name': BUILD-FAILED"
            }
            else {
                $testOut = @(& dotnet test $csprojPath --no-build -- --filter-class $specData.FilterClass 2>&1)
                Add-RawLog $testOut
                $testExit = $LASTEXITCODE

                foreach ($line in $testOut) {
                    # xunit v3's in-proc console reporter prints "failed <FQN>(<theory args>) (<dur>)" for
                    # a [Theory] case - the opening paren sits directly against the name, no space - and
                    # "failed <FQN> (<dur>)" for a plain [Fact]. Match up to the first space OR paren so
                    # both forms yield just the fully-qualified test name.
                    if ($line -match '^\s*failed\s+(?<name>[^\s(]+)') {
                        $failingTests.Add($Matches.name) | Out-Null
                    }
                }

                if ($testExit -ne 0) {
                    $result = 'CAUGHT'
                }
                else {
                    $result = 'SURVIVED'
                    $anySurvived = $true
                }
                Write-BatchLog "Mutation '$name': $result"
            }
        }
        finally {
            [IO.File]::WriteAllBytes($resolvedFile, $originalBytes)

            $restoredBytes = [IO.File]::ReadAllBytes($resolvedFile)
            $identical = $restoredBytes.Length -eq $originalBytes.Length
            if ($identical) {
                for ($i = 0; $i -lt $originalBytes.Length; $i++) {
                    if ($restoredBytes[$i] -ne $originalBytes[$i]) {
                        $identical = $false
                        break
                    }
                }
            }
            if (-not $identical) {
                Write-BatchLog "Mutation '$name': *** RESTORE FAILED - $resolvedFile is not byte-identical to the original - manual check required ***"
            }
        }

        $results.Add([PSCustomObject]@{
            Name         = $name
            Result       = $result
            FailingTests = ($failingTests | Select-Object -Unique) -join ', '
        }) | Out-Null
    }
}
finally {
    if ($acquired) {
        $mutex.ReleaseMutex() | Out-Null
    }
    $mutex.Dispose()
}

Write-Host ''
Write-Host 'Mutation results:'
$results | Format-Table -Property Name, Result, FailingTests -AutoSize | Out-String -Width 4096 | Write-Host

Write-Host "Log: $logPath"

Write-Host ''
Write-Host 'git status --porcelain -- src/'
$statusPorcelain = @(& git -C $worktreeRoot status --porcelain -- src/ 2>&1)
if (($statusPorcelain | Where-Object { $_ -ne '' }).Count -eq 0) {
    Write-Host '  (clean)'
} else {
    foreach ($s in $statusPorcelain) {
        Write-Host "  $s"
    }
}

if ($anyBuildFailed) {
    exit 2
}
if ($anySurvived) {
    exit 7
}
exit 0
