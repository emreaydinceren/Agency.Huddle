param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Describe '.gitattributes' {
    BeforeAll {
        $repoRoot = git rev-parse --show-toplevel
        Push-Location $repoRoot
    }

    AfterAll {
        Pop-Location
    }

    It 'installer/install.sh has eol:lf' {
        $output = git check-attr eol -- installer/install.sh
        $output | Should -Match 'eol: lf'
    }

    It 'installer/build-release.sh has eol:lf' {
        $output = git check-attr eol -- installer/build-release.sh
        $output | Should -Match 'eol: lf'
    }

    It 'installer/tests/lib.sh has eol:lf' {
        $output = git check-attr eol -- installer/tests/lib.sh
        $output | Should -Match 'eol: lf'
    }

    It 'installer/install.ps1 does not report eol:lf' {
        $output = git check-attr eol -- installer/install.ps1
        $output | Should -Not -Match 'eol: lf'
    }
}

Describe 'Check-Eol' {
    BeforeAll {
        $script:checkEol = Join-Path (git rev-parse --show-toplevel) 'agents/scripts/Check-Eol.ps1'
        $script:lfBytes = [byte[]](0x65, 0x63, 0x68, 0x6F, 0x20, 0x68, 0x69, 0x0A)
        $script:crlfBytes = [byte[]](0x65, 0x63, 0x68, 0x6F, 0x20, 0x68, 0x69, 0x0D, 0x0A)

        # Builds a throwaway git repo holding one untracked file with exact bytes.
        function New-EolRepo {
            param([string]$FileName, [byte[]]$Bytes)
            $dir = Join-Path ([IO.Path]::GetTempPath()) ('checkeol-' + [guid]::NewGuid().ToString('N'))
            New-Item -ItemType Directory -Path $dir | Out-Null
            Push-Location $dir
            try {
                git init -q | Out-Null
                git config user.name 'Test' | Out-Null
                git config user.email 'test@example.com' | Out-Null
                [IO.File]::WriteAllBytes((Join-Path $dir $FileName), $Bytes)
            }
            finally {
                Pop-Location
            }
            return $dir
        }

        # Runs Check-Eol.ps1 as a child pwsh with the repo as working directory.
        function Invoke-CheckEol {
            param([string]$Dir, [switch]$Fix)
            Push-Location $Dir
            try {
                if ($Fix) {
                    $out = & pwsh -NoProfile -File $script:checkEol -Fix 2>&1
                }
                else {
                    $out = & pwsh -NoProfile -File $script:checkEol 2>&1
                }
                return [pscustomobject]@{ ExitCode = $LASTEXITCODE; Output = ($out | Out-String) }
            }
            finally {
                Pop-Location
            }
        }

        $script:repos = New-Object System.Collections.Generic.List[string]
    }

    AfterAll {
        foreach ($d in $script:repos) {
            if (Test-Path -LiteralPath $d) {
                Remove-Item -LiteralPath $d -Recurse -Force
            }
        }
    }

    It 'a.sh with LF only passes' {
        $dir = New-EolRepo 'a.sh' $script:lfBytes
        $script:repos.Add($dir)
        $r = Invoke-CheckEol $dir
        $r.ExitCode | Should -Be 0
    }

    It 'a.sh with CRLF fails and names a.sh' {
        $dir = New-EolRepo 'a.sh' $script:crlfBytes
        $script:repos.Add($dir)
        $r = Invoke-CheckEol $dir
        $r.ExitCode | Should -Not -Be 0
        $r.Output | Should -Match 'a\.sh'
    }

    It 'a.cs with LF only still fails' {
        $dir = New-EolRepo 'a.cs' $script:lfBytes
        $script:repos.Add($dir)
        $r = Invoke-CheckEol $dir
        $r.ExitCode | Should -Not -Be 0
        $r.Output | Should -Match 'a\.cs'
    }

    It 'a.cs with CRLF still passes' {
        $dir = New-EolRepo 'a.cs' $script:crlfBytes
        $script:repos.Add($dir)
        $r = Invoke-CheckEol $dir
        $r.ExitCode | Should -Be 0
    }

    It '-Fix rewrites a CRLF a.sh to LF without adding CR' {
        $dir = New-EolRepo 'a.sh' $script:crlfBytes
        $script:repos.Add($dir)
        $null = Invoke-CheckEol $dir -Fix
        $actual = [IO.File]::ReadAllBytes((Join-Path $dir 'a.sh'))
        ($actual -contains 0x0D) | Should -BeFalse
        ($actual -join ',') | Should -Be ($script:lfBytes -join ',')
    }
}

Describe '.gitignore' {
    BeforeAll {
        $repoRoot = git rev-parse --show-toplevel
        Push-Location $repoRoot
    }

    AfterAll {
        Pop-Location
    }

    It 'dist/ entries are ignored' {
        git check-ignore dist/x | Should -Not -BeNullOrEmpty
    }

    It 'artifacts/ entries are ignored' {
        git check-ignore artifacts/x | Should -Not -BeNullOrEmpty
    }
}
