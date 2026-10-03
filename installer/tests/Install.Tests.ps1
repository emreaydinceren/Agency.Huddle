BeforeDiscovery {
    $script:Ps51Path = Join-Path ([string]$env:SystemRoot) 'System32\WindowsPowerShell\v1.0\powershell.exe'
    $script:NoPs51 = -not (Test-Path -LiteralPath $script:Ps51Path)
}

BeforeAll {
    $env:HUDDLE_INSTALL_NO_MAIN = '1'
    $ScriptPath = (Resolve-Path (Join-Path $PSScriptRoot '..\install.ps1')).Path
    . $ScriptPath
    Set-StrictMode -Version Latest
    $Ps51Path = Join-Path ([string]$env:SystemRoot) 'System32\WindowsPowerShell\v1.0\powershell.exe'

    # Runs a script block and returns everything it wrote to the host (information stream) as strings.
    function Get-HostLines {
        param([scriptblock]$Block)
        $lines = @(& $Block 6>&1 | ForEach-Object { [string]$_ })
        return , $lines
    }
}

Describe 'install.ps1 file shape' {
    It 'parses with Windows PowerShell 5.1' -Skip:$script:NoPs51 {
        $quoted = $ScriptPath.Replace("'", "''")
        $command = "`$t = `$null; `$e = `$null; [void][System.Management.Automation.Language.Parser]::ParseFile('$quoted', [ref]`$t, [ref]`$e); `$e | ForEach-Object { 'line ' + `$_.Extent.StartLineNumber + ': ' + `$_.Message }"
        $result = & $Ps51Path -NoProfile -Command $command
        $result | Should -BeNullOrEmpty
    }

    It 'is pure ASCII' {
        $bytes = [System.IO.File]::ReadAllBytes($ScriptPath)
        $bad = @($bytes | Where-Object { $_ -gt 127 })
        $bad.Count | Should -Be 0
    }

    It 'importing with HUDDLE_INSTALL_NO_MAIN set prints nothing and creates no files' {
        $savedLocal = $env:LOCALAPPDATA
        $probe = Join-Path ([System.IO.Path]::GetTempPath()) ('huddle-import-' + [guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $probe | Out-Null
        try {
            $env:LOCALAPPDATA = $probe
            $output = @(& { . $ScriptPath } *>&1)
            $output.Count | Should -Be 0
            @(Get-ChildItem -LiteralPath $probe -Force).Count | Should -Be 0
        }
        finally {
            $env:LOCALAPPDATA = $savedLocal
            Remove-Item -LiteralPath $probe -Recurse -Force
        }
    }
}

Describe 'Resolve-InstallPaths' {
    It 'uses the per-user LOCALAPPDATA defaults' {
        $paths = Resolve-InstallPaths
        $paths.InstallRoot | Should -Be (Join-Path $env:LOCALAPPDATA 'Programs\Agency.Huddle')
        $paths.DataRoot | Should -Be (Join-Path $env:LOCALAPPDATA 'Agency.Huddle')
        $paths.Rid | Should -Be 'win-x64'
    }

    It 'derives the files under InstallRoot' {
        $paths = Resolve-InstallPaths
        $paths.ExePath | Should -BeLike '*app\Huddle.App.exe'
        $paths.NodeExe | Should -BeLike '*node\node.exe'
        $paths.AdapterEntry | Should -BeLike '*tools\acp\node_modules\@agentclientprotocol\claude-agent-acp\dist\index.js'
        $paths.StatePath | Should -BeLike '*install-state.json'
        $paths.LauncherPath | Should -BeLike '*huddle.cmd'
        $paths.ExePath | Should -BeLike ($paths.InstallRoot + '*')
        $paths.StatePath | Should -BeLike ($paths.InstallRoot + '*')
    }

    It 'puts the cache under DataRoot' {
        $paths = Resolve-InstallPaths
        $paths.CacheDir | Should -Be (Join-Path $paths.DataRoot 'cache')
    }

    It 'honours the InstallRoot and DataRoot overrides' {
        $paths = Resolve-InstallPaths -InstallRoot 'C:\x\inst' -DataRoot 'C:\x\data'
        $paths.InstallRoot | Should -Be 'C:\x\inst'
        $paths.DataRoot | Should -Be 'C:\x\data'
        $paths.ExePath | Should -Be 'C:\x\inst\app\Huddle.App.exe'
        $paths.CacheDir | Should -Be 'C:\x\data\cache'
    }
}

Describe 'Resolve-AssetSource' {
    It 'resolves an existing folder BaseUrl to a file source' {
        $source = Resolve-AssetSource -Name 'x.zip' -BaseUrl $TestDrive -Version 'latest' -Repo 'o/r'
        $source.Kind | Should -Be 'file'
        $source.Location | Should -Be (Join-Path $TestDrive 'x.zip')
    }

    It 'resolves an https BaseUrl to a url source' {
        $source = Resolve-AssetSource -Name 'x' -BaseUrl 'https://example.test/rel/' -Version 'latest' -Repo 'o/r'
        $source.Kind | Should -Be 'url'
        $source.Location | Should -Be 'https://example.test/rel/x'
    }

    It 'resolves latest to the GitHub latest-download URL' {
        $source = Resolve-AssetSource -Name 'x' -Version 'latest' -Repo 'o/r'
        $source.Kind | Should -Be 'url'
        $source.Location | Should -Be 'https://github.com/o/r/releases/latest/download/x'
    }

    It 'resolves a tag to the GitHub tagged-download URL' {
        $source = Resolve-AssetSource -Name 'x' -Version 'v1.2.3' -Repo 'o/r'
        $source.Location | Should -Be 'https://github.com/o/r/releases/download/v1.2.3/x'
    }

    It 'throws a run-from-a-release message when the repo placeholder is unreplaced and no BaseUrl is given' {
        { Resolve-AssetSource -Name 'x' -Version 'latest' -Repo '__HUDDLE_REPO__' } | Should -Throw '*release*'
    }

    It 'still accepts a BaseUrl when the repo placeholder is unreplaced' {
        $source = Resolve-AssetSource -Name 'x' -BaseUrl $TestDrive -Version 'latest' -Repo '__HUDDLE_REPO__'
        $source.Kind | Should -Be 'file'
    }

    It 'rejects a BaseUrl that is neither https nor an existing folder' {
        { Resolve-AssetSource -Name 'x' -BaseUrl (Join-Path $TestDrive 'missing') -Version 'latest' -Repo 'o/r' } | Should -Throw
    }
}

Describe 'Write-Step' {
    It 'formats an ok step' {
        $lines = Get-HostLines { Write-Step -Status 'ok' -Name 'node' -Detail 'v24' }
        $lines | Should -Be @('[ok] node - v24')
    }

    It 'formats a skip step' {
        $lines = Get-HostLines { Write-Step -Status 'skip' -Name 'node' -Detail 'already satisfied' }
        $lines | Should -Be @('[skip] node - already satisfied')
    }

    It 'formats a working step' {
        $lines = Get-HostLines { Write-Step -Status '..' -Name 'node' -Detail 'working' }
        $lines | Should -Be @('[..] node - working')
    }

    It 'formats a failed step with an indented fix line' {
        $lines = Get-HostLines { Write-Step -Status '!!' -Name 'tar' -Detail 'missing' -Fix 'install tar' }
        $lines | Should -Be @('[!!] tar - missing', '     fix: install tar')
    }

    It 'omits the fix line when no fix is given' {
        $lines = Get-HostLines { Write-Step -Status '!!' -Name 'tar' -Detail 'missing' }
        $lines.Count | Should -Be 1
    }
}

Describe 'ExitCodes' {
    It 'pins the exit code contract' {
        $script:ExitCodes.Success | Should -Be 0
        $script:ExitCodes.StepFailed | Should -Be 1
        $script:ExitCodes.PrerequisitesUnmet | Should -Be 2
        $script:ExitCodes.BadUsage | Should -Be 3
    }
}

Describe 'Test-Prerequisites' {
    BeforeAll {
        function Get-PrereqItem {
            param($Items, [string]$Name)
            return @($Items | Where-Object { $_.Name -eq $Name })[0]
        }
    }

    BeforeEach {
        Mock Get-OsBuild { 22631 }
        Mock Get-HostArchitecture { 'AMD64' }
        Mock Test-CommandAvailable { $true }
        Mock Get-FreeDiskMb { 100000 }
        Mock Test-UrlReachable { $true }
        $paths = Resolve-InstallPaths -InstallRoot (Join-Path $TestDrive 'inst') -DataRoot (Join-Path $TestDrive 'data')
    }

    It 'passes every item when everything is good' {
        $items = @(Test-Prerequisites -Paths $paths -BaseUrl '')
        $items.Count | Should -BeGreaterThan 5
        @($items | Where-Object { -not $_.Ok }).Count | Should -Be 0
    }

    It 'returns Name, Ok, Detail and Fix on every item' {
        $items = @(Test-Prerequisites -Paths $paths -BaseUrl '')
        foreach ($item in $items) {
            $item.PSObject.Properties.Name | Should -Contain 'Name'
            $item.PSObject.Properties.Name | Should -Contain 'Ok'
            $item.PSObject.Properties.Name | Should -Contain 'Detail'
            $item.PSObject.Properties.Name | Should -Contain 'Fix'
        }
    }

    It 'fails the OS item below the minimum build and offers a fix' {
        Mock Get-OsBuild { 17000 }
        $item = Get-PrereqItem (Test-Prerequisites -Paths $paths -BaseUrl '') 'OS'
        $item.Ok | Should -BeFalse
        $item.Fix | Should -Not -BeNullOrEmpty
    }

    It 'accepts exactly the minimum build' {
        Mock Get-OsBuild { 17763 }
        (Get-PrereqItem (Test-Prerequisites -Paths $paths -BaseUrl '') 'OS').Ok | Should -BeTrue
    }

    It 'allows ARM64 with an emulation note' {
        Mock Get-HostArchitecture { 'ARM64' }
        $item = Get-PrereqItem (Test-Prerequisites -Paths $paths -BaseUrl '') 'CPU'
        $item.Ok | Should -BeTrue
        $item.Detail | Should -BeLike '*emulation*'
    }

    It 'fails an x86 CPU' {
        Mock Get-HostArchitecture { 'x86' }
        $item = Get-PrereqItem (Test-Prerequisites -Paths $paths -BaseUrl '') 'CPU'
        $item.Ok | Should -BeFalse
        $item.Fix | Should -Not -BeNullOrEmpty
    }

    It 'fails when tar is missing' {
        Mock Test-CommandAvailable { $false }
        $item = Get-PrereqItem (Test-Prerequisites -Paths $paths -BaseUrl '') 'tar'
        $item.Ok | Should -BeFalse
        $item.Fix | Should -Not -BeNullOrEmpty
    }

    It 'fails when free disk space is below the minimum' {
        Mock Get-FreeDiskMb { 500 }
        $item = Get-PrereqItem (Test-Prerequisites -Paths $paths -BaseUrl '') 'Disk space'
        $item.Ok | Should -BeFalse
        $item.Fix | Should -Not -BeNullOrEmpty
    }

    It 'reports an unreachable host and still runs the other checks' {
        Mock Test-UrlReachable { $false } -ParameterFilter { $Url -like '*nodejs.org*' }
        Mock Get-OsBuild { 17000 }
        $items = @(Test-Prerequisites -Paths $paths -BaseUrl '')
        (Get-PrereqItem $items 'Network: nodejs.org').Ok | Should -BeFalse
        (Get-PrereqItem $items 'Network: nodejs.org').Fix | Should -Not -BeNullOrEmpty
        (Get-PrereqItem $items 'Network: github.com').Ok | Should -BeTrue
        (Get-PrereqItem $items 'Network: registry.npmjs.org').Ok | Should -BeTrue
        (Get-PrereqItem $items 'OS').Ok | Should -BeFalse
        Should -Invoke Test-UrlReachable -Times 3 -Exactly
    }

    It 'makes no network probe when BaseUrl is a folder' {
        $items = @(Test-Prerequisites -Paths $paths -BaseUrl $TestDrive)
        Should -Invoke Test-UrlReachable -Times 0 -Exactly
        @($items | Where-Object { -not $_.Ok }).Count | Should -Be 0
    }

    It 'still probes the network when BaseUrl is an https URL' {
        $null = Test-Prerequisites -Paths $paths -BaseUrl 'https://example.test/rel'
        Should -Invoke Test-UrlReachable -Times 3 -Exactly
    }

    It 'creates no directory' {
        $null = Test-Prerequisites -Paths $paths -BaseUrl ''
        Test-Path -LiteralPath $paths.InstallRoot | Should -BeFalse
        Test-Path -LiteralPath $paths.DataRoot | Should -BeFalse
    }
}

Describe 'Show-Prerequisites' {
    It 'prints one line per passing item' {
        $items = @(
            [pscustomobject]@{ Name = 'OS'; Ok = $true; Detail = 'build 22631'; Fix = '' }
            [pscustomobject]@{ Name = 'CPU'; Ok = $true; Detail = 'AMD64'; Fix = '' }
        )
        $lines = Get-HostLines { Show-Prerequisites -Items $items }
        $lines.Count | Should -Be 2
        $lines[0] | Should -Be '[ok] OS - build 22631'
    }

    It 'prints a failing item as [!!] followed by its fix line' {
        $items = @([pscustomobject]@{ Name = 'tar'; Ok = $false; Detail = 'tar.exe not found'; Fix = 'install tar' })
        $lines = Get-HostLines { Show-Prerequisites -Items $items }
        $lines | Should -Be @('[!!] tar - tar.exe not found', '     fix: install tar')
    }
}

Describe 'Invoke-Main -CheckOnly' {
    BeforeEach {
        Mock Get-OsBuild { 22631 }
        Mock Get-HostArchitecture { 'AMD64' }
        Mock Test-CommandAvailable { $true }
        Mock Get-FreeDiskMb { 100000 }
        Mock Test-UrlReachable { $true }
        $installRoot = Join-Path $TestDrive 'inst'
        $dataRoot = Join-Path $TestDrive 'data'
    }

    It 'exits 0 when every prerequisite is met and creates no directory' {
        $code = Invoke-Main -CheckOnly -BaseUrl $TestDrive -InstallRoot $installRoot -DataRoot $dataRoot 6>$null
        $code | Should -Be 0
        Test-Path -LiteralPath $installRoot | Should -BeFalse
        Test-Path -LiteralPath $dataRoot | Should -BeFalse
    }

    It 'exits 2 when a prerequisite is unmet and creates no directory' {
        Mock Get-OsBuild { 17000 }
        $code = Invoke-Main -CheckOnly -BaseUrl $TestDrive -InstallRoot $installRoot -DataRoot $dataRoot 6>$null
        $code | Should -Be 2
        Test-Path -LiteralPath $installRoot | Should -BeFalse
        Test-Path -LiteralPath $dataRoot | Should -BeFalse
    }

    It 'prints a table through Write-Step' {
        $lines = Get-HostLines { $null = Invoke-Main -CheckOnly -BaseUrl $TestDrive -InstallRoot $installRoot -DataRoot $dataRoot }
        $lines.Count | Should -BeGreaterThan 5
        $lines[0] | Should -Match '^\[(ok|!!)\] '
    }

    It 'exits 3 for a BaseUrl that is neither https nor an existing folder' {
        $code = Invoke-Main -CheckOnly -BaseUrl (Join-Path $TestDrive 'missing') -InstallRoot $installRoot -DataRoot $dataRoot 6>$null
        $code | Should -Be 3
    }
}

Describe 'Prerequisite seams' {
    It 'Get-OsBuild returns the running build number' {
        Get-OsBuild | Should -BeGreaterThan 0
    }

    It 'Get-HostArchitecture returns a non-empty name' {
        Get-HostArchitecture | Should -Not -BeNullOrEmpty
    }

    It 'Test-CommandAvailable is false for an unknown command' {
        Test-CommandAvailable -Name 'huddle-no-such-command-xyz' | Should -BeFalse
    }

    It 'Test-CommandAvailable is true for a known command' {
        Test-CommandAvailable -Name 'Get-Command' | Should -BeTrue
    }

    It 'Get-FreeDiskMb measures the nearest existing ancestor of a missing path' {
        Get-FreeDiskMb -Path (Join-Path $TestDrive 'a\b\c') | Should -BeGreaterThan 0
    }
}

Describe 'Resolve-Options' {
    BeforeAll {
        $optionVars = @('HUDDLE_VERSION', 'HUDDLE_BASE_URL', 'HUDDLE_INSTALL_ROOT', 'HUDDLE_DATA_ROOT', 'HUDDLE_CHECK_ONLY', 'HUDDLE_UNINSTALL', 'HUDDLE_PURGE_DATA', 'HUDDLE_FORCE', 'HUDDLE_NO_LAUNCH')
    }

    BeforeEach {
        $saved = @{}
        foreach ($name in $optionVars) {
            $saved[$name] = [Environment]::GetEnvironmentVariable($name)
            [Environment]::SetEnvironmentVariable($name, $null)
        }
    }

    AfterEach {
        foreach ($name in $optionVars) {
            [Environment]::SetEnvironmentVariable($name, $saved[$name])
        }
    }

    It 'uses the defaults when nothing is set' {
        $o = Resolve-Options -Bound @{}
        $o.Version | Should -Be 'latest'
        $o.BaseUrl | Should -Be ''
        $o.InstallRoot | Should -Be ''
        $o.DataRoot | Should -Be ''
        $o.CheckOnly | Should -BeFalse
        $o.Uninstall | Should -BeFalse
        $o.PurgeData | Should -BeFalse
        $o.Force | Should -BeFalse
        $o.NoLaunch | Should -BeFalse
    }

    It 'reads the string options from the environment when the parameter is absent' {
        $env:HUDDLE_VERSION = 'v9.9.9'
        $env:HUDDLE_BASE_URL = 'https://example.test/x'
        $env:HUDDLE_INSTALL_ROOT = 'C:\env\inst'
        $env:HUDDLE_DATA_ROOT = 'C:\env\data'
        $o = Resolve-Options -Bound @{}
        $o.Version | Should -Be 'v9.9.9'
        $o.BaseUrl | Should -Be 'https://example.test/x'
        $o.InstallRoot | Should -Be 'C:\env\inst'
        $o.DataRoot | Should -Be 'C:\env\data'
    }

    It 'lets an explicit parameter win over the environment' {
        $env:HUDDLE_VERSION = 'v9.9.9'
        $env:HUDDLE_BASE_URL = 'https://example.test/x'
        $env:HUDDLE_INSTALL_ROOT = 'C:\env\inst'
        $env:HUDDLE_DATA_ROOT = 'C:\env\data'
        $o = Resolve-Options -Bound @{ Version = 'v1'; BaseUrl = 'C:\p'; InstallRoot = 'C:\pi'; DataRoot = 'C:\pd' }
        $o.Version | Should -Be 'v1'
        $o.BaseUrl | Should -Be 'C:\p'
        $o.InstallRoot | Should -Be 'C:\pi'
        $o.DataRoot | Should -Be 'C:\pd'
    }

    It 'switches a switch on for any non-empty value other than 0' {
        $env:HUDDLE_CHECK_ONLY = '1'
        $env:HUDDLE_UNINSTALL = 'true'
        $env:HUDDLE_PURGE_DATA = 'yes'
        $env:HUDDLE_FORCE = 'x'
        $env:HUDDLE_NO_LAUNCH = '1'
        $o = Resolve-Options -Bound @{}
        $o.CheckOnly | Should -BeTrue
        $o.Uninstall | Should -BeTrue
        $o.PurgeData | Should -BeTrue
        $o.Force | Should -BeTrue
        $o.NoLaunch | Should -BeTrue
    }

    It 'treats 0 and an empty value as off' {
        $env:HUDDLE_CHECK_ONLY = '0'
        $env:HUDDLE_UNINSTALL = ''
        $o = Resolve-Options -Bound @{}
        $o.CheckOnly | Should -BeFalse
        $o.Uninstall | Should -BeFalse
    }

    It 'lets an explicit switch win over the environment, including -Switch:$false' {
        $env:HUDDLE_CHECK_ONLY = '1'
        $env:HUDDLE_FORCE = '0'
        $o = Resolve-Options -Bound @{ CheckOnly = [switch]::new($false); Force = [switch]::new($true) }
        $o.CheckOnly | Should -BeFalse
        $o.Force | Should -BeTrue
    }

    It 'is used by Invoke-Main: HUDDLE_BASE_URL naming a missing folder gives exit 3' {
        $env:HUDDLE_BASE_URL = Join-Path $TestDrive 'missing'
        $code = Invoke-Main 6>$null
        $code | Should -Be 3
    }
}

Describe 'Read-Sums' {
    BeforeAll {
        $script:HashA = ('a' * 64)
        $script:HashB = ('0123456789abcdef' * 4)
    }

    It 'parses a two-line SHA256SUMS (two-space separator) into a name-to-hash table' {
        $text = $script:HashA + '  huddle-win-x64.zip' + "`n" + $script:HashB + '  install.ps1' + "`n"
        $sums = Read-Sums -Text $text
        $sums.Count | Should -Be 2
        $sums['huddle-win-x64.zip'] | Should -Be $script:HashA
        $sums['install.ps1'] | Should -Be $script:HashB
    }

    It 'tolerates CRLF line endings' {
        $text = $script:HashA + '  huddle-win-x64.zip' + "`r`n" + $script:HashB + '  install.ps1' + "`r`n"
        $sums = Read-Sums -Text $text
        $sums.Count | Should -Be 2
        $sums['huddle-win-x64.zip'] | Should -Be $script:HashA
        $sums['install.ps1'] | Should -Be $script:HashB
    }

    It 'ignores blank lines' {
        $text = "`r`n" + $script:HashA + '  huddle-win-x64.zip' + "`r`n`r`n   `r`n" + $script:HashB + '  install.ps1'
        $sums = Read-Sums -Text $text
        $sums.Count | Should -Be 2
        $sums['install.ps1'] | Should -Be $script:HashB
    }

    It 'stores hashes in lower case' {
        $sums = Read-Sums -Text (('ABCDEF0123456789' * 4) + '  x.zip')
        $sums['x.zip'] | Should -Be ('abcdef0123456789' * 4)
    }

    It 'returns an empty table for empty text' {
        $sums = Read-Sums -Text ''
        $sums.Count | Should -Be 0
    }
}

Describe 'Get-ExpectedHash' {
    It 'returns the hash listed for the name' {
        $sums = @{ 'huddle-win-x64.zip' = ('b' * 64) }
        Get-ExpectedHash -Sums $sums -Name 'huddle-win-x64.zip' | Should -Be ('b' * 64)
    }

    It 'throws when the name is not listed' {
        $sums = @{ 'huddle-win-x64.zip' = ('b' * 64) }
        { Get-ExpectedHash -Sums $sums -Name 'other.zip' } | Should -Throw "*'other.zip' is not listed in SHA256SUMS*"
    }
}

Describe 'Get-FileSha256' {
    It 'returns the lower-case hex SHA-256 of the file' {
        $path = Join-Path $TestDrive 'abc.txt'
        [System.IO.File]::WriteAllBytes($path, [System.Text.Encoding]::ASCII.GetBytes('abc'))
        Get-FileSha256 -Path $path | Should -BeExactly 'ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad'
    }
}

Describe 'Get-VerifiedAsset' {
    BeforeAll {
        # Independent of the code under test: hash the fixture bytes directly.
        function Get-TestHash {
            param([byte[]]$Bytes)
            $sha = [System.Security.Cryptography.SHA256]::Create()
            try {
                return (([System.BitConverter]::ToString($sha.ComputeHash($Bytes))).Replace('-', '')).ToLowerInvariant()
            }
            finally {
                $sha.Dispose()
            }
        }
    }

    BeforeEach {
        $script:Fixture = Join-Path $TestDrive 'fixture.zip'
        $script:GoodBytes = [System.Text.Encoding]::ASCII.GetBytes('the good archive')
        [System.IO.File]::WriteAllBytes($script:Fixture, $script:GoodBytes)
        $script:GoodHash = Get-TestHash -Bytes $script:GoodBytes
        $script:BadBytes = [System.Text.Encoding]::ASCII.GetBytes('a corrupted archive')
        $script:BadHash = Get-TestHash -Bytes $script:BadBytes
        $script:Cache = Join-Path $TestDrive ('cache-' + [guid]::NewGuid().ToString('N'))
        $script:UrlSource = [pscustomobject]@{ Kind = 'url'; Location = 'https://example.invalid/huddle-win-x64.zip' }
        $script:Attempts = 0
        Mock Start-Sleep { }
        Mock Invoke-Download { Copy-Item -LiteralPath $script:Fixture -Destination $OutFile }
    }

    It 'downloads into the cache and returns the cache path when the hash matches' {
        $path = Get-VerifiedAsset -Name 'huddle-win-x64.zip' -ExpectedSha256 $script:GoodHash -Source $script:UrlSource -CacheDir $script:Cache
        $path | Should -Be (Join-Path $script:Cache 'huddle-win-x64.zip')
        Test-Path -LiteralPath $path | Should -BeTrue
        Get-TestHash -Bytes ([System.IO.File]::ReadAllBytes($path)) | Should -Be $script:GoodHash
        Should -Invoke Invoke-Download -Times 1 -Exactly
        Should -Invoke Invoke-Download -Times 1 -Exactly -ParameterFilter { $Url -eq 'https://example.invalid/huddle-win-x64.zip' }
    }

    It 'leaves no .partial file behind after a successful download' {
        $null = Get-VerifiedAsset -Name 'huddle-win-x64.zip' -ExpectedSha256 $script:GoodHash -Source $script:UrlSource -CacheDir $script:Cache
        Test-Path -LiteralPath (Join-Path $script:Cache 'huddle-win-x64.zip.partial') | Should -BeFalse
    }

    It 'compares the expected hash case-insensitively' {
        $path = Get-VerifiedAsset -Name 'huddle-win-x64.zip' -ExpectedSha256 $script:GoodHash.ToUpperInvariant() -Source $script:UrlSource -CacheDir $script:Cache
        Test-Path -LiteralPath $path | Should -BeTrue
    }

    It 'skips the download when the cached file already has the right hash' {
        New-Item -ItemType Directory -Path $script:Cache | Out-Null
        Copy-Item -LiteralPath $script:Fixture -Destination (Join-Path $script:Cache 'huddle-win-x64.zip')
        $path = Get-VerifiedAsset -Name 'huddle-win-x64.zip' -ExpectedSha256 $script:GoodHash -Source $script:UrlSource -CacheDir $script:Cache
        $path | Should -Be (Join-Path $script:Cache 'huddle-win-x64.zip')
        Should -Invoke Invoke-Download -Times 0 -Exactly
    }

    It 're-downloads when the cached file has the wrong hash' {
        New-Item -ItemType Directory -Path $script:Cache | Out-Null
        $cached = Join-Path $script:Cache 'huddle-win-x64.zip'
        [System.IO.File]::WriteAllBytes($cached, $script:BadBytes)
        $path = Get-VerifiedAsset -Name 'huddle-win-x64.zip' -ExpectedSha256 $script:GoodHash -Source $script:UrlSource -CacheDir $script:Cache
        Should -Invoke Invoke-Download -Times 1 -Exactly
        Get-TestHash -Bytes ([System.IO.File]::ReadAllBytes($path)) | Should -Be $script:GoodHash
    }

    It 'throws on a hash mismatch, names both hashes and leaves no file under the final or .partial name' {
        Mock Invoke-Download { [System.IO.File]::WriteAllBytes($OutFile, $script:BadBytes) }
        $message = ''
        try {
            $null = Get-VerifiedAsset -Name 'huddle-win-x64.zip' -ExpectedSha256 $script:GoodHash -Source $script:UrlSource -CacheDir $script:Cache
        }
        catch {
            $message = $_.Exception.Message
        }

        $message | Should -Not -BeNullOrEmpty
        $message | Should -BeLike ('*' + $script:GoodHash + '*')
        $message | Should -BeLike ('*' + $script:BadHash + '*')
        Test-Path -LiteralPath (Join-Path $script:Cache 'huddle-win-x64.zip') | Should -BeFalse
        Test-Path -LiteralPath (Join-Path $script:Cache 'huddle-win-x64.zip.partial') | Should -BeFalse
    }

    It 'retries a transient failure: two failures then success makes 3 calls and succeeds' {
        Mock Invoke-Download {
            $script:Attempts = $script:Attempts + 1
            if ($script:Attempts -le 2) {
                throw 'transient network error'
            }

            Copy-Item -LiteralPath $script:Fixture -Destination $OutFile
        }

        $path = Get-VerifiedAsset -Name 'huddle-win-x64.zip' -ExpectedSha256 $script:GoodHash -Source $script:UrlSource -CacheDir $script:Cache
        Should -Invoke Invoke-Download -Times 3 -Exactly
        Test-Path -LiteralPath $path | Should -BeTrue
    }

    It 'backs off 1 s then 3 s between attempts' {
        Mock Invoke-Download { throw 'transient network error' }
        { Get-VerifiedAsset -Name 'huddle-win-x64.zip' -ExpectedSha256 $script:GoodHash -Source $script:UrlSource -CacheDir $script:Cache } | Should -Throw
        Should -Invoke Start-Sleep -Times 2 -Exactly
        Should -Invoke Start-Sleep -Times 1 -Exactly -ParameterFilter { $Seconds -eq 1 }
        Should -Invoke Start-Sleep -Times 1 -Exactly -ParameterFilter { $Seconds -eq 3 }
    }

    It 'throws after 3 failed attempts' {
        Mock Invoke-Download { throw 'transient network error' }
        { Get-VerifiedAsset -Name 'huddle-win-x64.zip' -ExpectedSha256 $script:GoodHash -Source $script:UrlSource -CacheDir $script:Cache } | Should -Throw '*transient network error*'
        Should -Invoke Invoke-Download -Times 3 -Exactly
        Test-Path -LiteralPath (Join-Path $script:Cache 'huddle-win-x64.zip') | Should -BeFalse
    }

    It 'copies a file-kind source instead of downloading it' {
        $source = [pscustomobject]@{ Kind = 'file'; Location = $script:Fixture }
        $path = Get-VerifiedAsset -Name 'huddle-win-x64.zip' -ExpectedSha256 $script:GoodHash -Source $source -CacheDir $script:Cache
        Should -Invoke Invoke-Download -Times 0 -Exactly
        $path | Should -Be (Join-Path $script:Cache 'huddle-win-x64.zip')
        Get-TestHash -Bytes ([System.IO.File]::ReadAllBytes($path)) | Should -Be $script:GoodHash
        Test-Path -LiteralPath $script:Fixture | Should -BeTrue
    }

    It 'rejects a file-kind source with the wrong hash and leaves nothing in the cache under the final name' {
        $source = [pscustomobject]@{ Kind = 'file'; Location = $script:Fixture }
        { Get-VerifiedAsset -Name 'huddle-win-x64.zip' -ExpectedSha256 $script:BadHash -Source $source -CacheDir $script:Cache } | Should -Throw ('*' + $script:BadHash + '*')
        Test-Path -LiteralPath (Join-Path $script:Cache 'huddle-win-x64.zip') | Should -BeFalse
    }
}

Describe 'Invoke-Download seam' {
    It 'is a function with Url and OutFile parameters' {
        $command = Get-Command Invoke-Download -CommandType Function
        $command.Parameters.ContainsKey('Url') | Should -BeTrue
        $command.Parameters.ContainsKey('OutFile') | Should -BeTrue
    }
}

Describe 'Read-InstallState' {
    It 'returns $null for a missing file' {
        Read-InstallState -Path (Join-Path $TestDrive 'no-such-state.json') | Should -BeNullOrEmpty
    }

    It 'returns $null for an empty file' {
        $path = Join-Path $TestDrive 'empty-state.json'
        [System.IO.File]::WriteAllBytes($path, [byte[]]@())
        Read-InstallState -Path $path | Should -BeNullOrEmpty
    }

    It 'returns $null for a whitespace-only file' {
        $path = Join-Path $TestDrive 'blank-state.json'
        [System.IO.File]::WriteAllText($path, "  `r`n  ")
        Read-InstallState -Path $path | Should -BeNullOrEmpty
    }

    It 'returns $null for malformed JSON' {
        $path = Join-Path $TestDrive 'bad-state.json'
        [System.IO.File]::WriteAllText($path, '{ "schema": "1", "version": ')
        Read-InstallState -Path $path | Should -BeNullOrEmpty
    }

    It 'returns $null when the JSON is not an object' {
        $path = Join-Path $TestDrive 'array-state.json'
        [System.IO.File]::WriteAllText($path, '[1, 2, 3]')
        Read-InstallState -Path $path | Should -BeNullOrEmpty
    }
}

Describe 'Write-InstallState' {
    BeforeEach {
        $script:StatePath = Join-Path $TestDrive ('state-' + [guid]::NewGuid().ToString('N') + '.json')
        $script:Full = @{
            version     = 'v1.2.3'
            appSha256   = ('a' * 64)
            nodeVersion = 'v24.21.0'
            lockSha256  = ('b' * 64)
        }
    }

    It 'round-trips version, appSha256, nodeVersion and lockSha256 with schema 1' {
        Write-InstallState -Path $script:StatePath -State $script:Full
        $state = Read-InstallState -Path $script:StatePath
        $state | Should -Not -BeNullOrEmpty
        $state.schema | Should -BeExactly '1'
        $state.version | Should -Be 'v1.2.3'
        $state.appSha256 | Should -Be ('a' * 64)
        $state.nodeVersion | Should -Be 'v24.21.0'
        $state.lockSha256 | Should -Be ('b' * 64)
    }

    It 'accepts the object that Read-InstallState returned, so a step can update one key' {
        Write-InstallState -Path $script:StatePath -State $script:Full
        $state = Read-InstallState -Path $script:StatePath
        $state.appSha256 = ('c' * 64)
        Write-InstallState -Path $script:StatePath -State $state
        $again = Read-InstallState -Path $script:StatePath
        $again.appSha256 | Should -Be ('c' * 64)
        $again.nodeVersion | Should -Be 'v24.21.0'
        $again.schema | Should -BeExactly '1'
    }

    It 'leaves no .tmp file behind' {
        Write-InstallState -Path $script:StatePath -State $script:Full
        Test-Path -LiteralPath ($script:StatePath + '.tmp') | Should -BeFalse
        Test-Path -LiteralPath $script:StatePath | Should -BeTrue
    }

    It 'overwrites an existing state file' {
        Write-InstallState -Path $script:StatePath -State $script:Full
        $second = @{ version = 'v2.0.0'; appSha256 = ('d' * 64); nodeVersion = 'v24.21.0'; lockSha256 = ('e' * 64) }
        Write-InstallState -Path $script:StatePath -State $second
        (Read-InstallState -Path $script:StatePath).version | Should -Be 'v2.0.0'
        Test-Path -LiteralPath ($script:StatePath + '.tmp') | Should -BeFalse
    }

    It 'writes ASCII with no byte order mark' {
        Write-InstallState -Path $script:StatePath -State $script:Full
        $bytes = [System.IO.File]::ReadAllBytes($script:StatePath)
        $bytes.Length | Should -BeGreaterThan 0
        @($bytes | Where-Object { $_ -gt 127 }).Count | Should -Be 0
    }

    It 'creates the parent folder when it does not exist' {
        $nested = Join-Path $TestDrive ('nested-' + [guid]::NewGuid().ToString('N') + '\install-state.json')
        Write-InstallState -Path $nested -State $script:Full
        (Read-InstallState -Path $nested).version | Should -Be 'v1.2.3'
    }
}

Describe 'Test-StepSatisfied' {
    BeforeAll {
        $script:SampleState = [pscustomobject]@{ schema = '1'; version = 'v1.2.3'; appSha256 = ('ab' * 32); nodeVersion = 'v24.21.0' }
    }

    It 'is true on an exact match' {
        Test-StepSatisfied -State $script:SampleState -Key 'appSha256' -Expected ('ab' * 32) | Should -BeTrue
    }

    It 'is true when only the case differs' {
        Test-StepSatisfied -State $script:SampleState -Key 'appSha256' -Expected ('AB' * 32) | Should -BeTrue
    }

    It 'is false on a different value' {
        Test-StepSatisfied -State $script:SampleState -Key 'appSha256' -Expected ('cd' * 32) | Should -BeFalse
    }

    It 'is false on a partial match' {
        Test-StepSatisfied -State $script:SampleState -Key 'nodeVersion' -Expected 'v24.21' | Should -BeFalse
    }

    It 'is false when the state is $null' {
        Test-StepSatisfied -State $null -Key 'appSha256' -Expected ('ab' * 32) | Should -BeFalse
    }

    It 'is false when the key is not in the state' {
        Test-StepSatisfied -State $script:SampleState -Key 'lockSha256' -Expected ('ab' * 32) | Should -BeFalse
    }

    It 'is false when the expected value is empty' {
        Test-StepSatisfied -State $script:SampleState -Key 'appSha256' -Expected '' | Should -BeFalse
    }
}

Describe 'Running as a file or through iex' {
    BeforeAll {
        $installerDir = Split-Path -Parent $ScriptPath
    }

    It 'under iex in Windows PowerShell 5.1 the session survives and LASTEXITCODE is set' -Skip:$script:NoPs51 {
        $quotedDir = $installerDir.Replace("'", "''")
        $quotedScript = $ScriptPath.Replace("'", "''")
        $command = "Remove-Item Env:HUDDLE_INSTALL_NO_MAIN -ErrorAction SilentlyContinue; `$env:HUDDLE_CHECK_ONLY='1'; `$env:HUDDLE_BASE_URL='$quotedDir'; iex ([IO.File]::ReadAllText('$quotedScript')); 'ALIVE:' + `$LASTEXITCODE"
        $output = (& $Ps51Path -NoProfile -Command $command) -join "`n"
        $output | Should -Match 'ALIVE:0'
    }

    It 'under iex a failed check sets LASTEXITCODE 2 or 3 and still survives' -Skip:$script:NoPs51 {
        $quotedScript = $ScriptPath.Replace("'", "''")
        $command = "Remove-Item Env:HUDDLE_INSTALL_NO_MAIN -ErrorAction SilentlyContinue; `$env:HUDDLE_CHECK_ONLY='1'; `$env:HUDDLE_BASE_URL='C:\no\such\folder'; iex ([IO.File]::ReadAllText('$quotedScript')); 'ALIVE:' + `$LASTEXITCODE"
        $output = (& $Ps51Path -NoProfile -Command $command) -join "`n"
        $output | Should -Match 'ALIVE:3'
    }

    It 'under iex leaves ErrorActionPreference and strict mode of the session untouched' -Skip:$script:NoPs51 {
        $quotedDir = $installerDir.Replace("'", "''")
        $quotedScript = $ScriptPath.Replace("'", "''")
        $command = "Remove-Item Env:HUDDLE_INSTALL_NO_MAIN -ErrorAction SilentlyContinue; `$env:HUDDLE_CHECK_ONLY='1'; `$env:HUDDLE_BASE_URL='$quotedDir'; iex ([IO.File]::ReadAllText('$quotedScript')) | Out-Null; 'EAP:' + `$ErrorActionPreference; try { `$null = `$notDefinedAnywhere; 'STRICT:OK' } catch { 'STRICT:FAIL' }"
        $output = (& $Ps51Path -NoProfile -Command $command) -join "`n"
        $output | Should -Match 'EAP:Continue'
        $output | Should -Match 'STRICT:OK'
    }

    It 'as a file in Windows PowerShell 5.1 -CheckOnly exits 0' -Skip:$script:NoPs51 {
        $saved = $env:HUDDLE_INSTALL_NO_MAIN
        $env:HUDDLE_INSTALL_NO_MAIN = $null
        try {
            $null = & $Ps51Path -NoProfile -File $ScriptPath -CheckOnly -BaseUrl $installerDir
            $LASTEXITCODE | Should -Be 0
        }
        finally {
            $env:HUDDLE_INSTALL_NO_MAIN = $saved
        }
    }

    It 'as a file in Windows PowerShell 5.1 a bad BaseUrl exits 3' -Skip:$script:NoPs51 {
        $saved = $env:HUDDLE_INSTALL_NO_MAIN
        $env:HUDDLE_INSTALL_NO_MAIN = $null
        try {
            $null = & $Ps51Path -NoProfile -File $ScriptPath -CheckOnly -BaseUrl 'C:\no\such\folder'
            $LASTEXITCODE | Should -Be 3
        }
        finally {
            $env:HUDDLE_INSTALL_NO_MAIN = $saved
        }
    }
}
