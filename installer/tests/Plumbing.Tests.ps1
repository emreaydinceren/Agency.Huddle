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
