<#
.SYNOPSIS
    Reseeds one or more PromptGoldenTests golden files: deletes each, runs the golden tests once to
    regenerate them (expected to fail), runs them again to confirm they now pass, then prints the diff.

.DESCRIPTION
    Golden files live at tests/Huddle.Tests/Acp/Golden/<Name>, resolved relative to the CALLER's git
    toplevel (`git rev-parse --show-toplevel`), so this script works unmodified from any worktree.

    Sequence, for each name in -Names:
      1. Delete tests/Huddle.Tests/Acp/Golden/<Name> if it exists.
      2. Run agents/scripts/Run-Tests.ps1 (resolved next to this script via $PSScriptRoot) -NoBuild
         -FilterClass "*PromptGoldenTests" -Label reseed-1. PromptGoldenTests regenerates a missing golden
         from the actual output and then fails the comparison on that same run - this first run is
         EXPECTED to fail, and its exit code is not treated as an error.
      3. Run the same filter again as -Label reseed-2. This run must pass, now that the golden file
         exists and matches the just-regenerated content. If it does not, this script exits non-zero.
      4. Print `git diff --stat` and the full `git diff` for the golden files, so the reseed is visible
         at review before it's committed.

    IMPORTANT: this script passes -NoBuild through to Run-Tests.ps1, so the caller must build the
    solution first (e.g. `dotnet build Huddle.slnx`, or a prior non-NoBuild Run-Tests.ps1 call). Without
    a build, "reseed-1" fails for the wrong reason (stale or missing binaries) and the golden is never
    actually regenerated.

.PARAMETER Names
    One or more golden file names (not paths) under tests/Huddle.Tests/Acp/Golden/, e.g.
    "systemPrompt.txt", "turnPromptGreeting.txt".

.PARAMETER DryRun
    Prints what would be deleted and run, and the git commands that would report the diff, without
    deleting anything, invoking Run-Tests.ps1, or touching the git index.

.EXAMPLE
    dotnet build Huddle.slnx
    pwsh agents/scripts/Reseed-Goldens.ps1 -Names "systemPrompt.txt","turnPromptGreeting.txt"

.EXAMPLE
    pwsh agents/scripts/Reseed-Goldens.ps1 -Names "systemPrompt.txt" -DryRun
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string[]]$Names,
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'

$worktreeRoot = (git rev-parse --show-toplevel).Trim()
$goldenDir = Join-Path $worktreeRoot 'tests/Huddle.Tests/Acp/Golden'
$scriptRoot = $PSScriptRoot
$runTestsScript = Join-Path $scriptRoot 'Run-Tests.ps1'

$relativePaths = $Names | ForEach-Object { "tests/Huddle.Tests/Acp/Golden/$_" }
$fullPaths = $Names | ForEach-Object { Join-Path $goldenDir $_ }

if ($DryRun) {
    Write-Host '*** DRY RUN - nothing will be deleted, no tests will run, no git state will change ***'
    Write-Host ''
    Write-Host 'Would delete:'
    foreach ($p in $fullPaths) {
        $exists = Test-Path -LiteralPath $p
        Write-Host "  $p (exists: $exists)"
    }
    Write-Host ''
    Write-Host "Would run: pwsh $runTestsScript -NoBuild -FilterClass `"*PromptGoldenTests`" -Label reseed-1"
    Write-Host "Would run: pwsh $runTestsScript -NoBuild -FilterClass `"*PromptGoldenTests`" -Label reseed-2"
    Write-Host ''
    Write-Host "Would run: git -C $worktreeRoot diff --stat -- $($relativePaths -join ' ')"
    Write-Host "Would run: git -C $worktreeRoot diff -- $($relativePaths -join ' ')"
    exit 0
}

foreach ($p in $fullPaths) {
    if (Test-Path -LiteralPath $p) {
        Remove-Item -LiteralPath $p -Force
        Write-Host "Deleted: $p"
    }
    else {
        Write-Host "Not present (nothing to delete): $p"
    }
}

Write-Host ''
Write-Host '=== reseed-1: expected to fail (regenerates the golden(s)) ==='
& $runTestsScript -NoBuild -FilterClass '*PromptGoldenTests' -Label reseed-1
$reseed1ExitCode = $LASTEXITCODE
Write-Host "reseed-1 exit code: $reseed1ExitCode (expected failure - not treated as an error)"

Write-Host ''
Write-Host '=== reseed-2: must pass ==='
& $runTestsScript -NoBuild -FilterClass '*PromptGoldenTests' -Label reseed-2
$reseed2ExitCode = $LASTEXITCODE

Write-Host ''
Write-Host "git diff --stat -- $($relativePaths -join ' ')"
& git -C $worktreeRoot diff --stat -- @relativePaths

Write-Host ''
Write-Host "git diff -- $($relativePaths -join ' ')"
& git -C $worktreeRoot diff -- @relativePaths

if ($reseed2ExitCode -ne 0) {
    Write-Host ''
    Write-Host "RESEED FAILED: reseed-2 did not pass (exit $reseed2ExitCode). The golden(s) still do not match actual output."
    exit $reseed2ExitCode
}

Write-Host ''
Write-Host 'RESEED OK: reseed-2 passed.'
exit 0
