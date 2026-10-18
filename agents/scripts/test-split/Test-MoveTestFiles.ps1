<#
.SYNOPSIS
    Pins Move-TestFiles.ps1 (test-split plan S11) and the S9 content table against a throwaway git repo.

.DESCRIPTION
    Builds a temp git repo holding tests/Huddle.Tests with CRLF files, then asserts that
    Move-TestFiles.ps1 -Target Hosted / Common moves the right files with git mv (identical SHA-256,
    R rows in git status --porcelain), never moves TestAssemblyInitializer.cs, and that -WhatIf moves
    nothing. Exits 0 when every check passes and 1 otherwise.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$mover = Join-Path $PSScriptRoot 'Move-TestFiles.ps1'
$script:failures = 0

function Assert-That([bool]$Condition, [string]$Message) {
    if ($Condition) {
        Write-Host "PASS: $Message"
    }
    else {
        Write-Host "FAIL: $Message"
        $script:failures++
    }
}

function Write-Crlf([string]$Repo, [string]$Rel, [string]$Text) {
    $path = Join-Path $Repo $Rel
    New-Item -ItemType Directory -Force -Path (Split-Path $path) | Out-Null
    [IO.File]::WriteAllText($path, (($Text -replace "`r`n", "`n") -replace "`n", "`r`n"))
}

function Get-Sha([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        return $null
    }

    return (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash
}

function New-Fixture {
    $repo = Join-Path ([IO.Path]::GetTempPath()) ("movetest-" + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $repo | Out-Null
    Write-Crlf $repo 'tests/Huddle.Tests/Ui/APageTests.cs' "public sealed class APageTests`n{`n    [Fact]`n    public void A() { _ = typeof(TeamWebApplicationFactory); }`n}`n"
    Write-Crlf $repo 'tests/Huddle.Tests/Acp/Golden/x.txt' "golden line one`nline two`n"
    Write-Crlf $repo 'tests/Huddle.Tests/Ui/Helper.cs' "public static class Helper`n{`n    public static int One => 1;`n}`n"
    Write-Crlf $repo 'tests/Huddle.Tests/TestAssemblyInitializer.cs' "public static class TestAssemblyInitializer`n{`n}`n"
    git -C $repo init -q
    git -C $repo config user.email 't@example.com'
    git -C $repo config user.name 'T'
    git -C $repo config core.autocrlf false
    git -C $repo add -A
    git -C $repo commit -q -m fixture
    return $repo
}

function Invoke-Mover([string]$Repo, [string[]]$MoverArgs) {
    Push-Location $Repo
    try {
        & pwsh -NoProfile -File $mover @MoverArgs -Root 'tests/Huddle.Tests' *> $null
        return $LASTEXITCODE
    }
    finally {
        Pop-Location
    }
}

Assert-That (Test-Path -LiteralPath $mover) "Move-TestFiles.ps1 exists at $mover"

$repos = @()
try {
    # Hosted: the test and the golden move, byte-identical, as renames.
    $repo = New-Fixture
    $repos += $repo
    $shaPage = Get-Sha (Join-Path $repo 'tests/Huddle.Tests/Ui/APageTests.cs')
    $shaGolden = Get-Sha (Join-Path $repo 'tests/Huddle.Tests/Acp/Golden/x.txt')
    $code = Invoke-Mover $repo @('-Target', 'Hosted')
    Assert-That ($code -eq 0) "-Target Hosted exits 0 (got $code)"
    $newPage = Join-Path $repo 'tests/Huddle.Tests.Hosted/Ui/APageTests.cs'
    $newGolden = Join-Path $repo 'tests/Huddle.Tests.Hosted/Acp/Golden/x.txt'
    Assert-That ((Get-Sha $newPage) -eq $shaPage -and $null -ne $shaPage) 'APageTests.cs moved to Huddle.Tests.Hosted with identical SHA-256'
    Assert-That ((Get-Sha $newGolden) -eq $shaGolden -and $null -ne $shaGolden) 'Acp/Golden/x.txt moved to Huddle.Tests.Hosted with identical SHA-256'
    Assert-That (-not (Test-Path (Join-Path $repo 'tests/Huddle.Tests/Ui/APageTests.cs'))) 'APageTests.cs is gone from Huddle.Tests'
    Assert-That (-not (Test-Path (Join-Path $repo 'tests/Huddle.Tests/Acp/Golden/x.txt'))) 'x.txt is gone from Huddle.Tests'
    Assert-That (Test-Path (Join-Path $repo 'tests/Huddle.Tests/Ui/Helper.cs')) 'Helper.cs stays when the target is Hosted'
    $status = @(git -C $repo status --porcelain)
    Assert-That (@($status | Where-Object { $_ -match '^R' }).Count -eq 2) "git status shows two R rows ($($status -join ' | '))"

    # Common: the helper moves, the initializer never does.
    $repo = New-Fixture
    $repos += $repo
    $shaHelper = Get-Sha (Join-Path $repo 'tests/Huddle.Tests/Ui/Helper.cs')
    $code = Invoke-Mover $repo @('-Target', 'Common')
    Assert-That ($code -eq 0) "-Target Common exits 0 (got $code)"
    $newHelper = Join-Path $repo 'tests/Huddle.Tests.Common/Ui/Helper.cs'
    Assert-That ((Get-Sha $newHelper) -eq $shaHelper -and $null -ne $shaHelper) 'Helper.cs moved to Huddle.Tests.Common with identical SHA-256'
    Assert-That (Test-Path (Join-Path $repo 'tests/Huddle.Tests/TestAssemblyInitializer.cs')) 'TestAssemblyInitializer.cs stays in Huddle.Tests'
    Assert-That (-not (Test-Path (Join-Path $repo 'tests/Huddle.Tests.Common/TestAssemblyInitializer.cs'))) 'TestAssemblyInitializer.cs is not in Huddle.Tests.Common'
    $status = @(git -C $repo status --porcelain)
    Assert-That (@($status | Where-Object { $_ -match '^R' }).Count -eq 1) "git status shows one R row ($($status -join ' | '))"

    # -WhatIf: nothing moves, nothing is staged.
    $repo = New-Fixture
    $repos += $repo
    $code = Invoke-Mover $repo @('-Target', 'Hosted', '-WhatIf')
    Assert-That ($code -eq 0) "-WhatIf exits 0 (got $code)"
    Assert-That (Test-Path (Join-Path $repo 'tests/Huddle.Tests/Ui/APageTests.cs')) '-WhatIf leaves APageTests.cs in place'
    Assert-That (Test-Path (Join-Path $repo 'tests/Huddle.Tests/Acp/Golden/x.txt')) '-WhatIf leaves x.txt in place'
    Assert-That (-not (Test-Path (Join-Path $repo 'tests/Huddle.Tests.Hosted'))) '-WhatIf creates no Huddle.Tests.Hosted folder'
    Assert-That (@(git -C $repo status --porcelain).Count -eq 0) '-WhatIf leaves git status clean'
}
finally {
    foreach ($r in $repos) {
        if (Test-Path $r) {
            Remove-Item -Recurse -Force $r -ErrorAction SilentlyContinue
        }
    }
}

if ($script:failures -gt 0) {
    Write-Host "FAILED: $script:failures check(s) failed"
    exit 1
}

Write-Host 'All checks passed'
exit 0
