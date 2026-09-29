<#
.SYNOPSIS
    Proves a test suite actually exercises a line of source by mutating it, running the tests, and
    restoring the original file byte-for-byte.

.DESCRIPTION
    Two ways to target the mutation:
      - -Find/-Replace (default parameter set "FindReplace"): verifies that `-Find` occurs exactly once
        in `-File`, then performs a byte-faithful literal text replacement.
      - -Line/-Replace (parameter set "LineReplace"): replaces the whole content of 1-based line `-Line`
        with `-Replace`, keeping that line's own line ending untouched. Use this instead of -Find when the
        target text spans awkward escaping (e.g. embedded CRLF) that would be painful to spell out as a
        literal -Find string.

    Both sets preserve the file's existing line endings and BOM elsewhere in the file - the substitution
    is byte-faithful, not a re-encode - run agents/scripts/Run-Tests.ps1 (resolved next to this script via
    $PSScriptRoot) with the given filters and a "<Label>-mutation" label, and ALWAYS restore the original
    bytes in a finally block, then verify the restored file is byte-identical to the original.

    Exit codes:
      0  - the mutation turned at least one test red (tests ran, a test failed: the tests can see the
           mutation - good).
      2  - -Find does not occur exactly once, or -Line is out of range for the file.
      7  - everything stayed green (the tests cannot see the mutation - the coverage claim is false).
      10 - INVALID: the mutant did not build (Run-Tests.ps1 exit 1, or its output has "Build failed" /
           a compiler `error XX1234:` line and no test totals). A build failure is not a proof: the script
           prints `INVALID: the mutant did not build (first error: <line>)` instead of "Mutation caught".
           The file is still restored and RESTORED is printed.

    NOTE: the replacement text must keep every `using` the file still needs; if the mutation removes the
    last use of a type, the build fails (a compile error) and the result is INVALID (exit 10), not a catch.
    Only "tests ran and at least one failed" counts as a proof.

.PARAMETER File
    Path to the source file to mutate (and restore).

.PARAMETER Find
    The literal string to find. Must occur exactly once in the file, or the script exits 2 without
    changing anything. Part of the "FindReplace" parameter set - mutually exclusive with -Line.

.PARAMETER Line
    A 1-based line number in `-File` whose entire content is replaced with `-Replace`; that line's own
    line ending is preserved untouched. Part of the "LineReplace" parameter set - mutually exclusive with
    -Find. Exits 2 if the file has fewer than `-Line` lines.

.PARAMETER Replace
    The literal string to substitute for -Find, or (with -Line) the new content of that line.

.PARAMETER FilterClass
    Passed through to Run-Tests.ps1 -FilterClass.

.PARAMETER FilterMethod
    Passed through to Run-Tests.ps1 -FilterMethod.

.PARAMETER Label
    Passed through to Run-Tests.ps1 -Label as "<Label>-mutation".

.EXAMPLE
    pwsh agents/scripts/Prove-Mutation.ps1 -File E:\Repos\Huddle\src\Huddle.App\Tasks\TaskFileFormat.cs `
        -Find "[..16]" -Replace "[..15]" -FilterClass "*TaskFileFormatTests" -Label pm-check

.EXAMPLE
    pwsh agents/scripts/Prove-Mutation.ps1 -File E:\Repos\Huddle\src\Huddle.App\Tasks\TaskId.cs `
        -Line 12 -Replace "        return value.Length > 0;" -FilterClass "*TaskIdTests" -Label pm-line-check
#>
[CmdletBinding(DefaultParameterSetName = 'FindReplace')]
param(
    [Parameter(Mandatory)]
    [string]$File,
    [Parameter(Mandatory, ParameterSetName = 'FindReplace')]
    [string]$Find,
    [Parameter(Mandatory, ParameterSetName = 'LineReplace')]
    [int]$Line,
    [Parameter(Mandatory)]
    [string]$Replace,
    [string[]]$FilterClass,
    [string[]]$FilterMethod,
    [Parameter(Mandatory)]
    [string]$Label
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $File -PathType Leaf)) {
    throw "File not found: $File"
}

$resolvedFile = (Resolve-Path -LiteralPath $File).Path

# Read as raw bytes (preserves BOM) and decode with UTF8 (no BOM stripping issue for our purposes: .NET's
# UTF8Encoding with byte-order-mark detection round-trips correctly via Get/Set-Content -Encoding Byte
# equivalents below). To stay byte-faithful we work directly with bytes: decode to text using UTF-8,
# do the literal string substitution, then re-encode to UTF-8 preserving whether a BOM was present.
$originalBytes = [IO.File]::ReadAllBytes($resolvedFile)

$hasBom = $originalBytes.Length -ge 3 -and $originalBytes[0] -eq 0xEF -and $originalBytes[1] -eq 0xBB -and $originalBytes[2] -eq 0xBF

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$text = $utf8NoBom.GetString($originalBytes)

$mutationDescription = $null

if ($PSCmdlet.ParameterSetName -eq 'LineReplace') {
    # Split on line endings while keeping them, so the target line's own terminator (and every other
    # line's) is preserved untouched: Regex.Split with a capturing group interleaves the separators back
    # into the result, e.g. "a\r\nb\n" -> @('a', "`r`n", 'b', "`n", '').
    $parts = [regex]::Split($text, '(\r\n|\r|\n)')
    $lineIndex = ($Line - 1) * 2

    if ($Line -lt 1 -or $lineIndex -ge $parts.Length) {
        Write-Host "-Line $Line is out of range for $resolvedFile."
        exit 2
    }

    $originalLineContent = $parts[$lineIndex]
    $mutationDescription = "line $Line ('$originalLineContent' -> '$Replace')"
    $parts[$lineIndex] = $Replace
    $mutatedText = -join $parts
}
else {
    $occurrences = ([regex]::Matches($text, [regex]::Escape($Find))).Count
    if ($occurrences -ne 1) {
        Write-Host "-Find occurs $occurrences times in $resolvedFile (expected exactly 1)."
        exit 2
    }

    $mutationDescription = "'$Find' -> '$Replace'"
}

$scriptRoot = $PSScriptRoot
$runTestsScript = Join-Path $scriptRoot 'Run-Tests.ps1'

$exitCode = 7
try {
    if ($PSCmdlet.ParameterSetName -eq 'FindReplace') {
        $mutatedText = $text.Replace($Find, $Replace)
    }
    $mutatedBytes = if ($hasBom) {
        (New-Object System.Text.UTF8Encoding($true)).GetBytes($mutatedText)
    } else {
        $utf8NoBom.GetBytes($mutatedText)
    }
    [IO.File]::WriteAllBytes($resolvedFile, $mutatedBytes)

    Write-Host "Mutation: $mutationDescription in $resolvedFile"

    $runTestsArgs = @{
        Label = "$Label-mutation"
    }
    if ($FilterClass) {
        $runTestsArgs['FilterClass'] = $FilterClass
    }
    if ($FilterMethod) {
        $runTestsArgs['FilterMethod'] = $FilterMethod
    }

    # Echo Run-Tests.ps1's output live (it writes with Write-Host) while keeping a copy to classify.
    $runOutput = New-Object System.Collections.Generic.List[string]
    & $runTestsScript @runTestsArgs *>&1 | ForEach-Object {
        $outLine = "$_"
        $runOutput.Add($outLine)
        Write-Host $outLine
    }
    $testExitCode = $LASTEXITCODE

    $compilerErrorPattern = '\berror [A-Za-z]+\d+:'
    $hasTotals = @($runOutput | Where-Object { $_ -match '^\s*total:\s*\d+' }).Count -gt 0
    $firstCompilerError = $runOutput | Where-Object { $_ -match $compilerErrorPattern } | Select-Object -First 1
    $buildFailed = ($testExitCode -eq 1) -or
        (-not $hasTotals -and (($null -ne $firstCompilerError) -or (@($runOutput | Where-Object { $_ -match 'Build failed' }).Count -gt 0)))

    if ($buildFailed) {
        $exitCode = 10
        $firstErrorText = if ($null -ne $firstCompilerError) { $firstCompilerError.Trim() } else { 'none found in output' }
        Write-Host ''
        Write-Host "INVALID: the mutant did not build (first error: $firstErrorText)"
    }
    elseif ($testExitCode -ne 0) {
        $exitCode = 0
        Write-Host ''
        Write-Host "Mutation caught: tests went red (Run-Tests.ps1 exit $testExitCode)."
    }
    else {
        $exitCode = 7
        Write-Host ''
        Write-Host 'Mutation NOT caught: tests stayed green.'
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

    if ($identical) {
        Write-Host 'RESTORED: byte-identical'
    }
    else {
        Write-Host 'RESTORED: *** NOT byte-identical - manual check required ***'
    }

    Write-Host ''
    git diff --stat -- $resolvedFile
}

exit $exitCode
