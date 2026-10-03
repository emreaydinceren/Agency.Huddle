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

Describe 'Stop-HuddleIfRunning' {
    BeforeEach {
        $script:StopRoot = Join-Path $TestDrive ('stop-' + [guid]::NewGuid().ToString('N'))
        $script:StopPaths = Resolve-InstallPaths -InstallRoot (Join-Path $script:StopRoot 'install') -DataRoot (Join-Path $script:StopRoot 'data-root')
        $script:RecordPath = Join-Path $script:StopPaths.DataRoot 'data\instance.json'
        Mock Invoke-HuddleStop { return 0 }
        Mock Test-ProcessRunning { return $false }
        Mock Stop-HuddleProcess { }
    }

    It 'never calls Invoke-HuddleStop when no instance record exists' {
        Stop-HuddleIfRunning -Paths $script:StopPaths | Should -BeFalse
        Should -Invoke Invoke-HuddleStop -Times 0 -Exactly
    }

    It 'calls Invoke-HuddleStop once when an instance record exists and returns true' {
        New-Item -ItemType Directory -Path (Split-Path -Parent $script:RecordPath) -Force | Out-Null
        [System.IO.File]::WriteAllText($script:RecordPath, '{"url":"http://127.0.0.1:5000","stopToken":"t","pid":4242}')
        Stop-HuddleIfRunning -Paths $script:StopPaths | Should -BeTrue
        Should -Invoke Invoke-HuddleStop -Times 1 -Exactly
        Should -Invoke Stop-HuddleProcess -Times 0 -Exactly
    }

    It 'throws a clear message when the process survives and -Force is not given' {
        New-Item -ItemType Directory -Path (Split-Path -Parent $script:RecordPath) -Force | Out-Null
        [System.IO.File]::WriteAllText($script:RecordPath, '{"url":"http://127.0.0.1:5000","stopToken":"t","pid":4242}')
        Mock Test-ProcessRunning { return $true }
        { Stop-HuddleIfRunning -Paths $script:StopPaths } | Should -Throw '*still running*-Force*'
        Should -Invoke Stop-HuddleProcess -Times 0 -Exactly
    }

    It 'ends the surviving process with -Force' {
        New-Item -ItemType Directory -Path (Split-Path -Parent $script:RecordPath) -Force | Out-Null
        [System.IO.File]::WriteAllText($script:RecordPath, '{"url":"http://127.0.0.1:5000","stopToken":"t","pid":4242}')
        Mock Test-ProcessRunning { return $true } -ParameterFilter { $ProcessId -eq 4242 }
        Stop-HuddleIfRunning -Paths $script:StopPaths -Force | Should -BeTrue
        Should -Invoke Stop-HuddleProcess -Times 1 -Exactly -ParameterFilter { $ProcessId -eq 4242 }
    }

    It 'throws when the stop request failed and the record has no usable pid, unless -Force' {
        New-Item -ItemType Directory -Path (Split-Path -Parent $script:RecordPath) -Force | Out-Null
        [System.IO.File]::WriteAllText($script:RecordPath, '{"url":"http://127.0.0.1:5000"}')
        Mock Invoke-HuddleStop { return 1 }
        { Stop-HuddleIfRunning -Paths $script:StopPaths } | Should -Throw '*exit code 1*'
        Stop-HuddleIfRunning -Paths $script:StopPaths -Force 3>$null | Should -BeTrue
    }
}

Describe 'Install seams' {
    BeforeEach {
        $script:SeamRoot = Join-Path $TestDrive ('seam-' + [guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $script:SeamRoot | Out-Null
        $script:SeamPaths = Resolve-InstallPaths -InstallRoot (Join-Path $script:SeamRoot 'install') -DataRoot (Join-Path $script:SeamRoot 'data-root')
        New-Item -ItemType Directory -Path $script:SeamPaths.InstallRoot | Out-Null
    }

    It 'Invoke-Tar extracts a zip archive into the destination' {
        $src = Join-Path $script:SeamRoot 'src'
        New-Item -ItemType Directory -Path (Join-Path $src 'app') -Force | Out-Null
        [System.IO.File]::WriteAllText((Join-Path $src 'app\hello.txt'), 'hi')
        $zip = Join-Path $script:SeamRoot 'a.zip'
        Compress-Archive -Path (Join-Path $src '*') -DestinationPath $zip
        $dest = Join-Path $script:SeamRoot 'out'
        New-Item -ItemType Directory -Path $dest | Out-Null
        Invoke-Tar -Archive $zip -Destination $dest
        Test-Path -LiteralPath (Join-Path $dest 'app\hello.txt') | Should -BeTrue
    }

    It 'Invoke-Tar throws when the archive is not an archive' {
        $bad = Join-Path $script:SeamRoot 'bad.zip'
        [System.IO.File]::WriteAllText($bad, 'this is not a zip')
        $dest = Join-Path $script:SeamRoot 'out2'
        New-Item -ItemType Directory -Path $dest | Out-Null
        { Invoke-Tar -Archive $bad -Destination $dest } | Should -Throw '*Could not extract*bad.zip*'
    }

    It 'Invoke-HuddleStop runs the launcher with the stop verb and returns its exit code' {
        [System.IO.File]::WriteAllText($script:SeamPaths.LauncherPath, "@echo off`r`necho %1> `"%~dp0verb.txt`"`r`nexit /b 3`r`n")
        Invoke-HuddleStop -Paths $script:SeamPaths | Should -Be 3
        (Get-Content -LiteralPath (Join-Path $script:SeamPaths.InstallRoot 'verb.txt') -Raw).Trim() | Should -Be 'stop'
    }

    It 'Invoke-HuddleStop gives up after the time cap and returns -1' {
        [System.IO.File]::WriteAllText($script:SeamPaths.LauncherPath, "@echo off`r`nping -n 20 127.0.0.1 >nul`r`n")
        Invoke-HuddleStop -Paths $script:SeamPaths -TimeoutSeconds 1 | Should -Be -1
    }

    It 'Invoke-HuddleStop defaults to a 60 second cap' {
        (Get-Command Invoke-HuddleStop).Parameters['TimeoutSeconds'].ParameterType | Should -Be ([int])
        [IO.File]::ReadAllText($ScriptPath) | Should -Match '\[int\]\$TimeoutSeconds = 60'
    }

    It 'Test-ProcessRunning is false for a process that is not the Huddle app host' {
        Test-ProcessRunning -ProcessId $PID | Should -BeFalse
        Test-ProcessRunning -ProcessId 2147483000 | Should -BeFalse
    }
}

Describe 'Install-App' {
    BeforeAll {
        # Builds a release-shaped zip: app\Huddle.App.exe, tools\acp\package*.json, release-info.json.
        function New-TestArchive {
            param([string]$Path, [switch]$NoExe)
            $src = Join-Path $TestDrive ('src-' + [guid]::NewGuid().ToString('N'))
            New-Item -ItemType Directory -Path (Join-Path $src 'app') -Force | Out-Null
            New-Item -ItemType Directory -Path (Join-Path $src 'tools\acp') -Force | Out-Null
            if ($NoExe) {
                [System.IO.File]::WriteAllText((Join-Path $src 'app\readme.txt'), 'no exe here')
            }
            else {
                [System.IO.File]::WriteAllText((Join-Path $src 'app\Huddle.App.exe'), 'new exe')
            }
            [System.IO.File]::WriteAllText((Join-Path $src 'tools\acp\package.json'), '{}')
            [System.IO.File]::WriteAllText((Join-Path $src 'tools\acp\package-lock.json'), '{"lockfileVersion":3}')
            [System.IO.File]::WriteAllText((Join-Path $src 'release-info.json'), '{"nodeVersion":"24.21.0"}')
            Compress-Archive -Path (Join-Path $src '*') -DestinationPath $Path
        }

        # An installed tree from "the previous release".
        function New-OldInstall {
            param($Paths)
            New-Item -ItemType Directory -Path (Join-Path $Paths.InstallRoot 'app') -Force | Out-Null
            New-Item -ItemType Directory -Path (Join-Path $Paths.InstallRoot 'node') -Force | Out-Null
            New-Item -ItemType Directory -Path (Join-Path $Paths.InstallRoot 'tools\acp\node_modules') -Force | Out-Null
            [System.IO.File]::WriteAllText($Paths.ExePath, 'old exe')
            [System.IO.File]::WriteAllText((Join-Path $Paths.InstallRoot 'app\old.txt'), 'old only')
            [System.IO.File]::WriteAllText($Paths.NodeExe, 'old node')
            [System.IO.File]::WriteAllText((Join-Path $Paths.InstallRoot 'tools\acp\node_modules\marker.txt'), 'marker')
            [System.IO.File]::WriteAllText($Paths.LauncherPath, '@echo off')
            Write-InstallState -Path $Paths.StatePath -State @{ version = '0.9.0'; appSha256 = ('ab' * 32); nodeVersion = '24.21.0'; lockSha256 = ('cd' * 32) }
        }
    }

    BeforeEach {
        $script:RealMoveItem = Get-Command -Name Move-Item -CommandType Cmdlet
        $script:AppRoot = Join-Path $TestDrive ('app-' + [guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $script:AppRoot | Out-Null
        $script:AppPaths = Resolve-InstallPaths -InstallRoot (Join-Path $script:AppRoot 'install') -DataRoot (Join-Path $script:AppRoot 'data-root')
        $script:Staging = $script:AppPaths.InstallRoot + '.staging'
        $script:OldDir = $script:AppPaths.InstallRoot + '.old'
        $script:AppArchive = Join-Path $script:AppRoot 'huddle-win-x64.zip'
        New-TestArchive -Path $script:AppArchive
        $script:AppHash = (Get-FileHash -LiteralPath $script:AppArchive -Algorithm SHA256).Hash.ToLowerInvariant()
        Mock Invoke-Tar { Expand-Archive -LiteralPath $Archive -DestinationPath $Destination -Force }
        Mock Invoke-HuddleStop { return 0 }
        Mock Test-ProcessRunning { return $false }
        Mock Stop-HuddleProcess { }
    }

    It 'installs into an empty location and leaves no staging or old folder' {
        $result = Install-App -Paths $script:AppPaths -Archive $script:AppArchive -State $null
        @($result).Count | Should -Be 1
        $result | Should -BeTrue
        Test-Path -LiteralPath $script:AppPaths.ExePath | Should -BeTrue
        Test-Path -LiteralPath $script:AppPaths.ReleaseInfoPath | Should -BeTrue
        Test-Path -LiteralPath (Join-Path $script:AppPaths.InstallRoot 'tools\acp\package-lock.json') | Should -BeTrue
        Test-Path -LiteralPath $script:Staging | Should -BeFalse
        Test-Path -LiteralPath $script:OldDir | Should -BeFalse
    }

    It 'does not write the install state itself' {
        $null = Install-App -Paths $script:AppPaths -Archive $script:AppArchive -State $null
        Test-Path -LiteralPath $script:AppPaths.StatePath | Should -BeFalse
    }

    It 'skips the step when the state hash matches and the app host exists' {
        New-OldInstall -Paths $script:AppPaths
        $state = [pscustomobject]@{ appSha256 = $script:AppHash.ToUpperInvariant() }
        $lines = Get-HostLines { Install-App -Paths $script:AppPaths -Archive $script:AppArchive -State $state }
        ($lines -join "`n") | Should -Match '\[skip\] app'
        Should -Invoke Invoke-Tar -Times 0 -Exactly
        Should -Invoke Invoke-HuddleStop -Times 0 -Exactly
        [System.IO.File]::ReadAllText($script:AppPaths.ExePath) | Should -Be 'old exe'
    }

    It 'returns false when the step is skipped' {
        New-OldInstall -Paths $script:AppPaths
        $state = [pscustomobject]@{ appSha256 = $script:AppHash }
        $result = Install-App -Paths $script:AppPaths -Archive $script:AppArchive -State $state 6>$null
        @($result).Count | Should -Be 1
        $result | Should -BeFalse
    }

    It 'installs again when the hash matches but the app host is missing' {
        $state = [pscustomobject]@{ appSha256 = $script:AppHash }
        Install-App -Paths $script:AppPaths -Archive $script:AppArchive -State $state 6>$null | Should -BeTrue
        Test-Path -LiteralPath $script:AppPaths.ExePath | Should -BeTrue
    }

    It 'installs again when the state hash differs' {
        New-OldInstall -Paths $script:AppPaths
        $state = [pscustomobject]@{ appSha256 = ('ab' * 32) }
        Install-App -Paths $script:AppPaths -Archive $script:AppArchive -State $state 6>$null | Should -BeTrue
        [System.IO.File]::ReadAllText($script:AppPaths.ExePath) | Should -Be 'new exe'
    }

    It 'replaces app but carries node, the adapter node_modules and the state over on an upgrade' {
        New-OldInstall -Paths $script:AppPaths
        $null = Install-App -Paths $script:AppPaths -Archive $script:AppArchive -State (Read-InstallState -Path $script:AppPaths.StatePath) 6>$null
        [System.IO.File]::ReadAllText($script:AppPaths.ExePath) | Should -Be 'new exe'
        Test-Path -LiteralPath (Join-Path $script:AppPaths.InstallRoot 'app\old.txt') | Should -BeFalse
        [System.IO.File]::ReadAllText($script:AppPaths.NodeExe) | Should -Be 'old node'
        [System.IO.File]::ReadAllText((Join-Path $script:AppPaths.InstallRoot 'tools\acp\node_modules\marker.txt')) | Should -Be 'marker'
        Test-Path -LiteralPath (Join-Path $script:AppPaths.InstallRoot 'tools\acp\package.json') | Should -BeTrue
        (Read-InstallState -Path $script:AppPaths.StatePath).nodeVersion | Should -Be '24.21.0'
        Test-Path -LiteralPath $script:AppPaths.LauncherPath | Should -BeTrue
        Test-Path -LiteralPath $script:Staging | Should -BeFalse
        Test-Path -LiteralPath $script:OldDir | Should -BeFalse
    }

    It 'throws and leaves the previous install untouched when the archive has no app host' {
        New-OldInstall -Paths $script:AppPaths
        $bad = Join-Path $script:AppRoot 'bad-win-x64.zip'
        New-TestArchive -Path $bad -NoExe
        { Install-App -Paths $script:AppPaths -Archive $bad -State $null 6>$null } | Should -Throw '*Huddle.App.exe*'
        [System.IO.File]::ReadAllText($script:AppPaths.ExePath) | Should -Be 'old exe'
        Test-Path -LiteralPath (Join-Path $script:AppPaths.InstallRoot 'app\old.txt') | Should -BeTrue
        [System.IO.File]::ReadAllText($script:AppPaths.NodeExe) | Should -Be 'old node'
        Test-Path -LiteralPath (Join-Path $script:AppPaths.InstallRoot 'tools\acp\node_modules\marker.txt') | Should -BeTrue
        Test-Path -LiteralPath $script:Staging | Should -BeFalse
        Test-Path -LiteralPath $script:OldDir | Should -BeFalse
        Should -Invoke Invoke-HuddleStop -Times 0 -Exactly
    }

    It 'restores the previous install when renaming the staging folder fails' {
        New-OldInstall -Paths $script:AppPaths
        Mock Move-Item { & $script:RealMoveItem @PesterBoundParameters }
        Mock Move-Item { throw 'simulated rename failure' } -ParameterFilter { $LiteralPath -eq $script:Staging }
        { Install-App -Paths $script:AppPaths -Archive $script:AppArchive -State $null 6>$null } | Should -Throw '*simulated rename failure*'
        [System.IO.File]::ReadAllText($script:AppPaths.ExePath) | Should -Be 'old exe'
        [System.IO.File]::ReadAllText($script:AppPaths.NodeExe) | Should -Be 'old node'
        [System.IO.File]::ReadAllText((Join-Path $script:AppPaths.InstallRoot 'tools\acp\node_modules\marker.txt')) | Should -Be 'marker'
        Test-Path -LiteralPath $script:AppPaths.StatePath | Should -BeTrue
        Test-Path -LiteralPath $script:Staging | Should -BeFalse
        Test-Path -LiteralPath $script:OldDir | Should -BeFalse
    }

    It 'restores the previous install when renaming it away fails' {
        New-OldInstall -Paths $script:AppPaths
        Mock Move-Item { & $script:RealMoveItem @PesterBoundParameters }
        Mock Move-Item { throw 'simulated rename failure' } -ParameterFilter { $Destination -eq $script:OldDir }
        { Install-App -Paths $script:AppPaths -Archive $script:AppArchive -State $null 6>$null } | Should -Throw '*simulated rename failure*'
        [System.IO.File]::ReadAllText($script:AppPaths.ExePath) | Should -Be 'old exe'
        [System.IO.File]::ReadAllText($script:AppPaths.NodeExe) | Should -Be 'old node'
        [System.IO.File]::ReadAllText((Join-Path $script:AppPaths.InstallRoot 'tools\acp\node_modules\marker.txt')) | Should -Be 'marker'
        Test-Path -LiteralPath $script:Staging | Should -BeFalse
        Test-Path -LiteralPath $script:OldDir | Should -BeFalse
    }

    It 'stops a running instance before replacing an install that changes' {
        New-OldInstall -Paths $script:AppPaths
        $record = Join-Path $script:AppPaths.DataRoot 'data\instance.json'
        New-Item -ItemType Directory -Path (Split-Path -Parent $record) -Force | Out-Null
        [System.IO.File]::WriteAllText($record, '{"url":"http://127.0.0.1:5000","stopToken":"t","pid":4242}')
        $null = Install-App -Paths $script:AppPaths -Archive $script:AppArchive -State $null 6>$null
        Should -Invoke Invoke-HuddleStop -Times 1 -Exactly
        [System.IO.File]::ReadAllText($script:AppPaths.ExePath) | Should -Be 'new exe'
    }

    It 'removes a stale staging folder left by an interrupted run' {
        New-Item -ItemType Directory -Path (Join-Path $script:Staging 'app') -Force | Out-Null
        [System.IO.File]::WriteAllText((Join-Path $script:Staging 'app\junk.txt'), 'junk')
        $null = Install-App -Paths $script:AppPaths -Archive $script:AppArchive -State $null 6>$null
        Test-Path -LiteralPath $script:Staging | Should -BeFalse
        Test-Path -LiteralPath (Join-Path $script:AppPaths.InstallRoot 'app\junk.txt') | Should -BeFalse
        Test-Path -LiteralPath $script:AppPaths.ExePath | Should -BeTrue
    }

    It 'restores the old folder when a previous run died between the two renames' {
        New-OldInstall -Paths $script:AppPaths
        Move-Item -LiteralPath $script:AppPaths.InstallRoot -Destination $script:OldDir
        Mock Move-Item { & $script:RealMoveItem @PesterBoundParameters }
        Mock Move-Item { throw 'simulated rename failure' } -ParameterFilter { $LiteralPath -eq $script:Staging }
        { Install-App -Paths $script:AppPaths -Archive $script:AppArchive -State $null 6>$null } | Should -Throw '*simulated rename failure*'
        [System.IO.File]::ReadAllText($script:AppPaths.ExePath) | Should -Be 'old exe'
        Test-Path -LiteralPath $script:OldDir | Should -BeFalse
    }
}

Describe 'Get-InstalledNodeVersion' {
    It 'returns the first line of node --version, trimmed' {
        $cmd = Join-Path $TestDrive 'fake-node-ok.cmd'
        [System.IO.File]::WriteAllText($cmd, "@echo off`r`necho v24.21.0`r`nexit /b 0`r`n")
        Get-InstalledNodeVersion -NodeExe $cmd | Should -Be 'v24.21.0'
    }

    It 'returns $null when the executable does not exist' {
        Get-InstalledNodeVersion -NodeExe (Join-Path $TestDrive 'no-such-node.exe') | Should -BeNullOrEmpty
    }

    It 'returns $null when node exits with a non-zero code' {
        $cmd = Join-Path $TestDrive 'fake-node-bad.cmd'
        [System.IO.File]::WriteAllText($cmd, "@echo off`r`necho v24.21.0`r`nexit /b 3`r`n")
        Get-InstalledNodeVersion -NodeExe $cmd | Should -BeNullOrEmpty
    }
}

Describe 'Install-Node' {
    BeforeAll {
        # A folder that plays nodejs.org/dist/vX: the Windows zip plus SHASUMS256.txt listing it.
        function New-NodeRelease {
            param([string]$Dir, [string]$Version = 'v24.21.0', [switch]$NoExe, [switch]$OmitFromSums)
            New-Item -ItemType Directory -Path $Dir -Force | Out-Null
            $wrapper = 'node-' + $Version + '-win-x64'
            $src = Join-Path $TestDrive ('nsrc-' + [guid]::NewGuid().ToString('N'))
            New-Item -ItemType Directory -Path (Join-Path $src "$wrapper\node_modules\npm\bin") -Force | Out-Null
            if (-not $NoExe) {
                [System.IO.File]::WriteAllText((Join-Path $src "$wrapper\node.exe"), ('node ' + $Version))
            }
            [System.IO.File]::WriteAllText((Join-Path $src "$wrapper\node_modules\npm\bin\npm-cli.js"), '// npm cli')
            $zip = Join-Path $Dir ($wrapper + '.zip')
            Compress-Archive -Path (Join-Path $src $wrapper) -DestinationPath $zip -Force
            $hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
            $lines = @(('f' * 64) + '  node-' + $Version + '-darwin-arm64.tar.gz')
            if (-not $OmitFromSums) {
                $lines += ($hash + '  ' + $wrapper + '.zip')
            }
            [System.IO.File]::WriteAllText((Join-Path $Dir 'SHASUMS256.txt'), (($lines -join "`r`n") + "`r`n"))
            return $zip
        }

        function New-OldNode {
            param($Paths)
            New-Item -ItemType Directory -Path (Split-Path -Parent $Paths.NodeExe) -Force | Out-Null
            [System.IO.File]::WriteAllText($Paths.NodeExe, 'old node')
            [System.IO.File]::WriteAllText((Join-Path (Split-Path -Parent $Paths.NodeExe) 'old-only.txt'), 'old')
        }
    }

    BeforeEach {
        $script:NRoot = Join-Path $TestDrive ('node-' + [guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $script:NRoot | Out-Null
        $script:NPaths = Resolve-InstallPaths -InstallRoot (Join-Path $script:NRoot 'install') -DataRoot (Join-Path $script:NRoot 'data-root')
        $script:NSrc = Join-Path $script:NRoot 'dist'
        $script:NZip = New-NodeRelease -Dir $script:NSrc
        $script:NNodeDir = Split-Path -Parent $script:NPaths.NodeExe
        $script:NNpmCli = Join-Path $script:NNodeDir 'node_modules\npm\bin\npm-cli.js'
        $script:NState = [pscustomobject]@{ schema = '1'; nodeVersion = 'v24.21.0' }
        Mock Invoke-Tar { Expand-Archive -LiteralPath $Archive -DestinationPath $Destination -Force }
        Mock Stop-HuddleIfRunning { return $false }
        Mock Get-InstalledNodeVersion { return 'v24.21.0' }
    }

    It 'installs node.exe and the npm CLI under <InstallRoot>\node on a fresh machine' {
        $result = Install-Node -Paths $script:NPaths -NodeVersion 'v24.21.0' -State $null -NodeBaseUrl $script:NSrc 6>$null
        @($result).Count | Should -Be 1
        $result | Should -BeTrue
        Test-Path -LiteralPath $script:NPaths.NodeExe -PathType Leaf | Should -BeTrue
        Test-Path -LiteralPath $script:NNpmCli -PathType Leaf | Should -BeTrue
    }

    It 'leaves no wrapper folder, staging folder or old folder behind' {
        $null = Install-Node -Paths $script:NPaths -NodeVersion 'v24.21.0' -State $null -NodeBaseUrl $script:NSrc 6>$null
        Test-Path -LiteralPath (Join-Path $script:NNodeDir 'node-v24.21.0-win-x64') | Should -BeFalse
        Test-Path -LiteralPath (Join-Path $script:NPaths.InstallRoot 'node-v24.21.0-win-x64') | Should -BeFalse
        Test-Path -LiteralPath ($script:NNodeDir + '.staging') | Should -BeFalse
        Test-Path -LiteralPath ($script:NNodeDir + '.old') | Should -BeFalse
    }

    It 'prints an ok line for the node step' {
        $lines = Get-HostLines { Install-Node -Paths $script:NPaths -NodeVersion 'v24.21.0' -State $null -NodeBaseUrl $script:NSrc }
        ($lines -join "`n") | Should -BeLike '*[[]ok] node*'
    }

    It 'accepts a version without the v prefix and still finds the v-prefixed archive' {
        $result = Install-Node -Paths $script:NPaths -NodeVersion '24.21.0' -State $null -NodeBaseUrl $script:NSrc 6>$null
        $result | Should -BeTrue
        Test-Path -LiteralPath $script:NPaths.NodeExe | Should -BeTrue
    }

    It 'is skipped when the state matches, the exe exists and node --version prints the pinned version' {
        New-OldNode -Paths $script:NPaths
        Mock Get-VerifiedAsset { throw 'must not download' }
        $lines = Get-HostLines { $script:NResult = Install-Node -Paths $script:NPaths -NodeVersion 'v24.21.0' -State $script:NState -NodeBaseUrl $script:NSrc }
        $script:NResult | Should -BeFalse
        ($lines -join "`n") | Should -BeLike '*[[]skip] node*'
        Should -Invoke Get-VerifiedAsset -Times 0
        Should -Invoke Stop-HuddleIfRunning -Times 0
        [System.IO.File]::ReadAllText($script:NPaths.NodeExe) | Should -Be 'old node'
    }

    It 'is skipped when the state has no v prefix but the version matches' {
        New-OldNode -Paths $script:NPaths
        Mock Get-VerifiedAsset { throw 'must not download' }
        $state = [pscustomobject]@{ nodeVersion = '24.21.0' }
        Install-Node -Paths $script:NPaths -NodeVersion 'v24.21.0' -State $state -NodeBaseUrl $script:NSrc 6>$null | Should -BeFalse
    }

    It 'is not skipped when the state matches but node.exe is missing' {
        $result = Install-Node -Paths $script:NPaths -NodeVersion 'v24.21.0' -State $script:NState -NodeBaseUrl $script:NSrc 6>$null
        $result | Should -BeTrue
        Test-Path -LiteralPath $script:NPaths.NodeExe | Should -BeTrue
    }

    It 'is not skipped when node --version cannot be read' {
        New-OldNode -Paths $script:NPaths
        Mock Get-InstalledNodeVersion { return $null }
        $result = Install-Node -Paths $script:NPaths -NodeVersion 'v24.21.0' -State $script:NState -NodeBaseUrl $script:NSrc 6>$null
        $result | Should -BeTrue
        [System.IO.File]::ReadAllText($script:NPaths.NodeExe) | Should -Be 'node v24.21.0'
    }

    It 'is not skipped when node --version prints a different version than the pinned one' {
        New-OldNode -Paths $script:NPaths
        Mock Get-InstalledNodeVersion { return 'v22.1.0' }
        Install-Node -Paths $script:NPaths -NodeVersion 'v24.21.0' -State $script:NState -NodeBaseUrl $script:NSrc 6>$null | Should -BeTrue
    }

    It 'replaces an older node and removes everything of the old folder' {
        New-OldNode -Paths $script:NPaths
        $old = [pscustomobject]@{ nodeVersion = 'v22.1.0' }
        $result = Install-Node -Paths $script:NPaths -NodeVersion 'v24.21.0' -State $old -NodeBaseUrl $script:NSrc 6>$null
        $result | Should -BeTrue
        [System.IO.File]::ReadAllText($script:NPaths.NodeExe) | Should -Be 'node v24.21.0'
        Test-Path -LiteralPath (Join-Path $script:NNodeDir 'old-only.txt') | Should -BeFalse
        Test-Path -LiteralPath ($script:NNodeDir + '.old') | Should -BeFalse
        Test-Path -LiteralPath ($script:NNodeDir + '.staging') | Should -BeFalse
    }

    It 'stops a running instance before replacing an existing node folder' {
        New-OldNode -Paths $script:NPaths
        $old = [pscustomobject]@{ nodeVersion = 'v22.1.0' }
        $null = Install-Node -Paths $script:NPaths -NodeVersion 'v24.21.0' -State $old -NodeBaseUrl $script:NSrc -Force 6>$null
        Should -Invoke Stop-HuddleIfRunning -Times 1 -ParameterFilter { $Force }
    }

    It 'uses the SHASUMS256.txt line of the zip as the expected hash and refuses a tampered zip' {
        New-OldNode -Paths $script:NPaths
        [System.IO.File]::WriteAllText($script:NZip, 'not the zip the sums list')
        { Install-Node -Paths $script:NPaths -NodeVersion 'v24.21.0' -State $null -NodeBaseUrl $script:NSrc 6>$null } | Should -Throw '*Hash mismatch*'
        [System.IO.File]::ReadAllText($script:NPaths.NodeExe) | Should -Be 'old node'
    }

    It 'passes the SHASUMS256.txt hash of the zip to Get-VerifiedAsset' {
        Mock Get-VerifiedAsset { throw 'stop here' }
        $expected = (Get-FileHash -LiteralPath $script:NZip -Algorithm SHA256).Hash.ToLowerInvariant()
        { Install-Node -Paths $script:NPaths -NodeVersion 'v24.21.0' -State $null -NodeBaseUrl $script:NSrc 6>$null } | Should -Throw '*stop here*'
        Should -Invoke Get-VerifiedAsset -Times 1 -ParameterFilter { $Name -eq 'node-v24.21.0-win-x64.zip' -and $ExpectedSha256 -eq $expected -and $CacheDir -eq $script:NPaths.CacheDir }
    }

    It 'throws a clear message when SHASUMS256.txt does not list the zip' {
        $null = New-NodeRelease -Dir $script:NSrc -OmitFromSums
        { Install-Node -Paths $script:NPaths -NodeVersion 'v24.21.0' -State $null -NodeBaseUrl $script:NSrc 6>$null } | Should -Throw "*'node-v24.21.0-win-x64.zip' is not listed*"
    }

    It 'downloads SHASUMS256.txt and the zip from nodejs.org/dist/<version> by default' {
        Mock Invoke-Download { Copy-Item -LiteralPath (Join-Path $script:NSrc ($Url -replace '^.*/', '')) -Destination $OutFile -Force }
        $null = Install-Node -Paths $script:NPaths -NodeVersion 'v24.21.0' -State $null 6>$null
        Should -Invoke Invoke-Download -Times 1 -ParameterFilter { $Url -eq 'https://nodejs.org/dist/v24.21.0/SHASUMS256.txt' }
        Should -Invoke Invoke-Download -Times 1 -ParameterFilter { $Url -eq 'https://nodejs.org/dist/v24.21.0/node-v24.21.0-win-x64.zip' }
        Test-Path -LiteralPath $script:NPaths.NodeExe | Should -BeTrue
    }

    It 'throws and leaves the previous node untouched when the archive has no node.exe' {
        New-OldNode -Paths $script:NPaths
        $null = New-NodeRelease -Dir $script:NSrc -NoExe
        { Install-Node -Paths $script:NPaths -NodeVersion 'v24.21.0' -State $null -NodeBaseUrl $script:NSrc 6>$null } | Should -Throw '*node.exe*'
        [System.IO.File]::ReadAllText($script:NPaths.NodeExe) | Should -Be 'old node'
        Should -Invoke Stop-HuddleIfRunning -Times 0
        Test-Path -LiteralPath ($script:NNodeDir + '.staging') | Should -BeFalse
    }

    It 'restores the previous node when moving the new one into place fails' {
        New-OldNode -Paths $script:NPaths
        $script:RealMoveItem = Get-Command -Name Move-Item -CommandType Cmdlet
        Mock Move-Item { & $script:RealMoveItem @PesterBoundParameters }
        Mock Move-Item { throw 'simulated rename failure' } -ParameterFilter { $LiteralPath -like '*node-v24.21.0-win-x64' }
        { Install-Node -Paths $script:NPaths -NodeVersion 'v24.21.0' -State $null -NodeBaseUrl $script:NSrc 6>$null } | Should -Throw '*simulated rename failure*'
        [System.IO.File]::ReadAllText($script:NPaths.NodeExe) | Should -Be 'old node'
        Test-Path -LiteralPath ($script:NNodeDir + '.old') | Should -BeFalse
        Test-Path -LiteralPath ($script:NNodeDir + '.staging') | Should -BeFalse
    }

    It 'does not write the install state itself' {
        $null = Install-Node -Paths $script:NPaths -NodeVersion 'v24.21.0' -State $null -NodeBaseUrl $script:NSrc 6>$null
        Test-Path -LiteralPath $script:NPaths.StatePath | Should -BeFalse
    }
}

Describe 'Invoke-NpmCi seam' {
    BeforeAll {
        # A fake node (a .cmd) that prints what the seam gave it, so the log shows cwd, PATH and arguments.
        function New-FakeNode {
            param([string]$Dir, [int]$ExitCode = 0)
            New-Item -ItemType Directory -Path $Dir -Force | Out-Null
            $cmd = Join-Path $Dir 'node.cmd'
            $text = "@echo off`r`necho CWD=%CD%`r`necho PATH=%PATH%`r`necho ARGS=%*`r`necho some-error 1>&2`r`nexit /b $ExitCode`r`n"
            [System.IO.File]::WriteAllText($cmd, $text)
            return $cmd
        }
    }

    BeforeEach {
        $script:SRoot = Join-Path $TestDrive ('npm-' + [guid]::NewGuid().ToString('N'))
        $script:SNodeDir = Join-Path $script:SRoot 'node'
        $script:SWork = Join-Path $script:SRoot 'tools\acp'
        New-Item -ItemType Directory -Path $script:SWork -Force | Out-Null
        $script:SLog = Join-Path $script:SRoot 'cache\npm-ci.log'
    }

    It 'runs the bundled npm-cli.js with node, the node dir first on PATH and the working directory set' {
        $node = New-FakeNode -Dir $script:SNodeDir
        $before = $env:PATH
        $code = Invoke-NpmCi -NodeExe $node -WorkingDirectory $script:SWork -LogPath $script:SLog
        $code | Should -Be 0
        $log = [System.IO.File]::ReadAllText($script:SLog)
        $log | Should -BeLike ('*CWD=' + $script:SWork + '*')
        $log | Should -BeLike ('*PATH=' + $script:SNodeDir + ';*')
        $log | Should -BeLike '*ARGS=*node_modules\npm\bin\npm-cli.js" ci --omit=dev --no-audit --no-fund*'
        $env:PATH | Should -Be $before
    }

    It 'writes the error stream to the log too and returns the exit code' {
        $node = New-FakeNode -Dir $script:SNodeDir -ExitCode 7
        $code = Invoke-NpmCi -NodeExe $node -WorkingDirectory $script:SWork -LogPath $script:SLog
        $code | Should -Be 7
        [System.IO.File]::ReadAllText($script:SLog) | Should -BeLike '*some-error*'
    }

    It 'creates the log folder and does not change the session location' {
        $node = New-FakeNode -Dir $script:SNodeDir
        $here = (Get-Location).Path
        $null = Invoke-NpmCi -NodeExe $node -WorkingDirectory $script:SWork -LogPath $script:SLog
        Test-Path -LiteralPath $script:SLog | Should -BeTrue
        (Get-Location).Path | Should -Be $here
    }
}

Describe 'Install-Adapter' {
    BeforeEach {
        $script:ARoot = Join-Path $TestDrive ('adapter-' + [guid]::NewGuid().ToString('N'))
        $script:APaths = Resolve-InstallPaths -InstallRoot (Join-Path $script:ARoot 'install') -DataRoot (Join-Path $script:ARoot 'data-root')
        $script:AAcp = Join-Path $script:APaths.InstallRoot 'tools\acp'
        $script:ALock = Join-Path $script:AAcp 'package-lock.json'
        New-Item -ItemType Directory -Path $script:AAcp -Force | Out-Null
        New-Item -ItemType Directory -Path (Split-Path -Parent $script:APaths.NodeExe) -Force | Out-Null
        [System.IO.File]::WriteAllText($script:APaths.NodeExe, 'node')
        [System.IO.File]::WriteAllText($script:ALock, '{"lockfileVersion":3}')
        $script:ALockHash = (Get-FileHash -LiteralPath $script:ALock -Algorithm SHA256).Hash.ToLowerInvariant()
        $script:AState = [pscustomobject]@{ schema = '1'; lockSha256 = $script:ALockHash }
        Mock Stop-HuddleIfRunning { return $false }
        Mock Invoke-NpmCi {
            New-Item -ItemType Directory -Path (Split-Path -Parent $script:APaths.AdapterEntry) -Force | Out-Null
            [System.IO.File]::WriteAllText($script:APaths.AdapterEntry, '// adapter')
            return 0
        }
    }

    It 'runs npm ci when there is no state and returns $true' {
        $result = Install-Adapter -Paths $script:APaths -State $null 6>$null
        @($result).Count | Should -Be 1
        $result | Should -BeTrue
        Should -Invoke Invoke-NpmCi -Times 1
        Test-Path -LiteralPath $script:APaths.AdapterEntry | Should -BeTrue
    }

    It 'runs npm ci when the lock hash differs from the state' {
        $state = [pscustomobject]@{ lockSha256 = ('0' * 64) }
        Install-Adapter -Paths $script:APaths -State $state 6>$null | Should -BeTrue
        Should -Invoke Invoke-NpmCi -Times 1
    }

    It 'is skipped when the lock hash matches and the entry file exists' {
        New-Item -ItemType Directory -Path (Split-Path -Parent $script:APaths.AdapterEntry) -Force | Out-Null
        [System.IO.File]::WriteAllText($script:APaths.AdapterEntry, '// adapter')
        $lines = Get-HostLines { $script:AResult = Install-Adapter -Paths $script:APaths -State $script:AState }
        $script:AResult | Should -BeFalse
        ($lines -join "`n") | Should -BeLike '*[[]skip] adapter*'
        Should -Invoke Invoke-NpmCi -Times 0
        Should -Invoke Stop-HuddleIfRunning -Times 0
    }

    It 'runs npm ci when the hash matches but the entry file is missing' {
        Install-Adapter -Paths $script:APaths -State $script:AState 6>$null | Should -BeTrue
        Should -Invoke Invoke-NpmCi -Times 1
    }

    It 'calls the seam with the bundled node, tools\acp as the working directory and <Cache>\npm-ci.log' {
        $null = Install-Adapter -Paths $script:APaths -State $null 6>$null
        $log = Join-Path $script:APaths.CacheDir 'npm-ci.log'
        Should -Invoke Invoke-NpmCi -Times 1 -ParameterFilter {
            $NodeExe -eq $script:APaths.NodeExe -and $WorkingDirectory -eq $script:AAcp -and $LogPath -eq $log
        }
    }

    It 'stops a running instance before it replaces the adapter' {
        $null = Install-Adapter -Paths $script:APaths -State $null -Force 6>$null
        Should -Invoke Stop-HuddleIfRunning -Times 1 -ParameterFilter { $Force }
    }

    It 'throws with the last 15 log lines and a proxy fix when npm exits non-zero' {
        Mock Invoke-NpmCi {
            $lines = 1..20 | ForEach-Object { 'npm line ' + $_ }
            New-Item -ItemType Directory -Path (Split-Path -Parent $LogPath) -Force | Out-Null
            [System.IO.File]::WriteAllText($LogPath, (($lines -join "`r`n") + "`r`n"))
            return 1
        }
        $message = ''
        try {
            $null = Install-Adapter -Paths $script:APaths -State $null 6>$null
        }
        catch {
            $message = $_.Exception.Message
        }
        $message | Should -BeLike '*npm ci*exit code 1*'
        $message | Should -BeLike '*npm line 20*'
        $message | Should -BeLike '*npm line 6*'
        $message | Should -Not -BeLike '*npm line 5*'
        $message | Should -BeLike '*fix:*proxy*'
        $message | Should -BeLike '*npm config set proxy*'
        $message | Should -BeLike '*HTTPS_PROXY*'
    }

    It 'throws about a missing log when npm failed without writing one' {
        Mock Invoke-NpmCi { return 1 }
        { Install-Adapter -Paths $script:APaths -State $null 6>$null } | Should -Throw '*fix:*'
    }

    It 'throws "adapter entry not found" when npm succeeds but the entry file is missing' {
        Mock Invoke-NpmCi { return 0 }
        { Install-Adapter -Paths $script:APaths -State $null 6>$null } | Should -Throw '*adapter entry not found*'
    }

    It 'throws when the bundled node is missing, without running npm' {
        Remove-Item -LiteralPath $script:APaths.NodeExe -Force
        { Install-Adapter -Paths $script:APaths -State $null 6>$null } | Should -Throw '*node*'
        Should -Invoke Invoke-NpmCi -Times 0
    }

    It 'throws when tools\acp\package-lock.json is missing' {
        Remove-Item -LiteralPath $script:ALock -Force
        { Install-Adapter -Paths $script:APaths -State $null 6>$null } | Should -Throw '*package-lock.json*'
        Should -Invoke Invoke-NpmCi -Times 0
    }

    It 'does not write the install state itself' {
        $null = Install-Adapter -Paths $script:APaths -State $null 6>$null
        Test-Path -LiteralPath $script:APaths.StatePath | Should -BeFalse
    }
}

Describe 'Get-LauncherContent' {
    BeforeAll {
        $testPaths = [pscustomobject]@{
            InstallRoot = 'C:\Program Files\Agency.Huddle'
            DataRoot    = 'C:\Users\test\AppData\Local\Agency.Huddle'
            ExePath     = 'C:\Program Files\Agency.Huddle\app\Huddle.App.exe'
            NodeExe     = 'C:\Program Files\Agency.Huddle\node\node.exe'
        }

        $testPathsWithSpaces = [pscustomobject]@{
            InstallRoot = 'C:\Program Files\My Company\Agency.Huddle'
            DataRoot    = 'C:\Users\test user\AppData\Local\Agency.Huddle'
            ExePath     = 'C:\Program Files\My Company\Agency.Huddle\app\Huddle.App.exe'
            NodeExe     = 'C:\Program Files\My Company\Agency.Huddle\node\node.exe'
        }
    }

    It 'returns a string with the required lines in order' {
        $content = Get-LauncherContent -Paths $testPaths
        $lines = $content -split "`r`n"

        $lines[0] | Should -Be '@echo off'
        $lines[1] | Should -Be 'setlocal'
        $lines[2] | Should -Be 'set "Team__Desktop__Enabled=true"'
        $lines[3] | Should -Be 'set "Team__Acp__Enabled=true"'
        $lines[4] | Should -Be 'set "Team__Acp__Command=C:\Program Files\Agency.Huddle\node\node.exe"'
        $lines[5] | Should -Be 'set "Team__DataDir=C:\Users\test\AppData\Local\Agency.Huddle\data"'
        $lines[6] | Should -Be 'set "Team__PipeName=huddle-%USERNAME%"'
        $lines[7] | Should -Be 'set "ASPNETCORE_URLS=http://127.0.0.1:0"'
        $lines[8] | Should -Be 'set "ASPNETCORE_ENVIRONMENT=Production"'
        $lines[9] | Should -Be 'set "AllowedHosts=localhost;127.0.0.1"'
        $lines[10] | Should -Be 'set "PATH=C:\Program Files\Agency.Huddle\node;%PATH%"'
        $lines[11] | Should -Be '"C:\Program Files\Agency.Huddle\app\Huddle.App.exe" %*'
    }

    It 'quotes paths containing spaces' {
        $content = Get-LauncherContent -Paths $testPathsWithSpaces
        $lines = $content -split "`r`n"

        # Expected lines for paths with spaces: no quotes INSIDE the set values
        $lines[4] | Should -Be 'set "Team__Acp__Command=C:\Program Files\My Company\Agency.Huddle\node\node.exe"'
        $lines[5] | Should -Be 'set "Team__DataDir=C:\Users\test user\AppData\Local\Agency.Huddle\data"'
        $lines[10] | Should -Be 'set "PATH=C:\Program Files\My Company\Agency.Huddle\node;%PATH%"'
        $lines[11] | Should -Be '"C:\Program Files\My Company\Agency.Huddle\app\Huddle.App.exe" %*'

        # Verify no set lines contain =" (which would indicate nested quotes)
        foreach ($line in $lines) {
            if ($line -match '^set "') {
                # Find the first = and check if there's a =" after it
                $afterFirstEq = $line -replace '^[^=]*=', ''
                $afterFirstEq | Should -Not -Match '="'
            }
        }
    }

    It 'uses CRLF line endings' {
        $content = Get-LauncherContent -Paths $testPaths
        $content | Should -Match "`r`n"
        $content | Should -Not -Match "[^`r]`n"
    }

    It 'contains only ASCII characters' {
        $content = Get-LauncherContent -Paths $testPaths
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($content)
        $bad = @($bytes | Where-Object { $_ -gt 127 })
        $bad.Count | Should -Be 0
    }

    It 'does not contain bare LF without CR' {
        $content = Get-LauncherContent -Paths $testPaths
        $content | Should -Not -Match "[^`r]`n"
    }
}

Describe 'Resolve-InstallPaths shortcut path' {
    It 'defaults to the Start menu Programs folder under APPDATA' {
        $paths = Resolve-InstallPaths
        $paths.ShortcutPath | Should -Be (Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Agency Huddle.lnk')
    }

    It 'honours a ShortcutPath override' {
        $paths = Resolve-InstallPaths -InstallRoot 'C:\x\inst' -DataRoot 'C:\x\data' -ShortcutPath 'C:\x\menu\Agency Huddle.lnk'
        $paths.ShortcutPath | Should -Be 'C:\x\menu\Agency Huddle.lnk'
        $paths.LauncherPath | Should -Be 'C:\x\inst\huddle.cmd'
    }
}

Describe 'Install-Launcher' {
    BeforeEach {
        $script:LRoot = Join-Path $TestDrive ('launcher-' + [guid]::NewGuid().ToString('N'))
        $script:LPaths = Resolve-InstallPaths -InstallRoot (Join-Path $script:LRoot 'install dir') -DataRoot (Join-Path $script:LRoot 'data dir') -ShortcutPath (Join-Path $script:LRoot 'menu\Agency Huddle.lnk')
        $script:LUserPath = ''
        $script:LShortcutTarget = $null
        $script:LStamp = [datetime]::new(2020, 1, 2, 3, 4, 5, [System.DateTimeKind]::Utc)
        New-Item -ItemType Directory -Path $script:LPaths.InstallRoot -Force | Out-Null
        Mock New-HuddleShortcut {
            New-Item -ItemType Directory -Path (Split-Path -Parent $Path) -Force | Out-Null
            [System.IO.File]::WriteAllText($Path, 'lnk')
            $script:LShortcutTarget = $Target
        }
        Mock Get-ShortcutTarget {
            if (Test-Path -LiteralPath $Path -PathType Leaf) {
                return $script:LShortcutTarget
            }

            return $null
        }
        Mock Get-UserPathValue { return $script:LUserPath }
        Mock Set-UserPathValue { $script:LUserPath = $Value }
    }

    It 'first run writes huddle.cmd, creates the shortcut, appends InstallRoot to the user PATH and returns $true' {
        $lines = Get-HostLines { $script:LResult = Install-Launcher -Paths $script:LPaths }
        @($script:LResult).Count | Should -Be 1
        $script:LResult | Should -BeTrue
        Test-Path -LiteralPath $script:LPaths.LauncherPath -PathType Leaf | Should -BeTrue
        Should -Invoke New-HuddleShortcut -Times 1 -Exactly
        Should -Invoke Set-UserPathValue -Times 1 -Exactly
        $script:LUserPath | Should -Be $script:LPaths.InstallRoot
        $text = $lines -join "`n"
        $text | Should -BeLike '*[[]ok] launcher*'
        $text | Should -BeLike '*[[]ok] shortcut*'
        $text | Should -BeLike '*[[]ok] path*'
    }

    It 'writes the launcher as ASCII CRLF text that ends with a line break, byte for byte' {
        $null = Install-Launcher -Paths $script:LPaths 6>$null
        $expected = [System.Text.Encoding]::ASCII.GetBytes((Get-LauncherContent -Paths $script:LPaths) + "`r`n")
        $actual = [System.IO.File]::ReadAllBytes($script:LPaths.LauncherPath)
        [System.Convert]::ToBase64String($actual) | Should -Be ([System.Convert]::ToBase64String($expected))
    }

    It 'writes the exact launcher lines for paths that contain spaces' {
        $null = Install-Launcher -Paths $script:LPaths 6>$null
        $lines = [System.IO.File]::ReadAllText($script:LPaths.LauncherPath) -split "`r`n"
        $lines[4] | Should -Be ('set "Team__Acp__Command=' + (Join-Path $script:LPaths.InstallRoot 'node\node.exe') + '"')
        $lines[5] | Should -Be ('set "Team__DataDir=' + $script:LPaths.DataRoot + '\data"')
        $lines[10] | Should -Be ('set "PATH=' + (Join-Path $script:LPaths.InstallRoot 'node') + ';%PATH%"')
        $lines[11] | Should -Be ('"' + (Join-Path $script:LPaths.InstallRoot 'app\Huddle.App.exe') + '" %*')
        $script:LPaths.InstallRoot | Should -BeLike '* *'
    }

    It 'a second run writes nothing, invokes no seam and reports [skip] for every entry point' {
        $null = Install-Launcher -Paths $script:LPaths 6>$null
        $item = Get-Item -LiteralPath $script:LPaths.LauncherPath
        $item.LastWriteTimeUtc = $script:LStamp
        $lines = Get-HostLines { $script:LResult = Install-Launcher -Paths $script:LPaths }
        $script:LResult | Should -BeFalse
        (Get-Item -LiteralPath $script:LPaths.LauncherPath).LastWriteTimeUtc | Should -Be $script:LStamp
        Should -Invoke New-HuddleShortcut -Times 1 -Exactly
        Should -Invoke Set-UserPathValue -Times 1 -Exactly
        $text = $lines -join "`n"
        $text | Should -BeLike '*[[]skip] launcher*'
        $text | Should -BeLike '*[[]skip] shortcut*'
        $text | Should -BeLike '*[[]skip] path*'
        $text | Should -Not -BeLike '*[[]ok]*'
    }

    It 'rewrites a launcher whose text changed and leaves the shortcut and PATH alone' {
        $null = Install-Launcher -Paths $script:LPaths 6>$null
        [System.IO.File]::WriteAllText($script:LPaths.LauncherPath, "@echo off`r`nold`r`n")
        $lines = Get-HostLines { $script:LResult = Install-Launcher -Paths $script:LPaths }
        $script:LResult | Should -BeTrue
        $expected = [System.Text.Encoding]::ASCII.GetBytes((Get-LauncherContent -Paths $script:LPaths) + "`r`n")
        [System.Convert]::ToBase64String([System.IO.File]::ReadAllBytes($script:LPaths.LauncherPath)) | Should -Be ([System.Convert]::ToBase64String($expected))
        Should -Invoke New-HuddleShortcut -Times 1 -Exactly
        Should -Invoke Set-UserPathValue -Times 1 -Exactly
        ($lines -join "`n") | Should -BeLike '*[[]ok] launcher*'
    }

    It 'creates the shortcut with the launcher as target, InstallRoot as working directory, a minimized window and the app icon' {
        $null = Install-Launcher -Paths $script:LPaths 6>$null
        Should -Invoke New-HuddleShortcut -Times 1 -Exactly -ParameterFilter {
            $Path -eq $script:LPaths.ShortcutPath -and
            $Target -eq $script:LPaths.LauncherPath -and
            $WorkingDirectory -eq $script:LPaths.InstallRoot -and
            $WindowStyle -eq 7 -and
            $IconPath -eq $script:LPaths.ExePath
        }
    }

    It 'recreates a shortcut whose target is not this launcher' {
        $null = Install-Launcher -Paths $script:LPaths 6>$null
        $script:LShortcutTarget = 'C:\somewhere\else\huddle.cmd'
        Install-Launcher -Paths $script:LPaths 6>$null | Should -BeTrue
        Should -Invoke New-HuddleShortcut -Times 2 -Exactly
        Should -Invoke Set-UserPathValue -Times 1 -Exactly
    }

    It 'recreates a shortcut that was deleted' {
        $null = Install-Launcher -Paths $script:LPaths 6>$null
        Remove-Item -LiteralPath $script:LPaths.ShortcutPath -Force
        Install-Launcher -Paths $script:LPaths 6>$null | Should -BeTrue
        Should -Invoke New-HuddleShortcut -Times 2 -Exactly
    }

    It 'does not duplicate an InstallRoot already on the PATH (<Name>)' -ForEach @(
        @{ Name = 'same text'; Mode = 'same' }
        @{ Name = 'different case'; Mode = 'upper' }
        @{ Name = 'lower case'; Mode = 'lower' }
        @{ Name = 'trailing backslash'; Mode = 'slash' }
        @{ Name = 'trailing backslash and different case'; Mode = 'upperslash' }
    ) {
        $root = $script:LPaths.InstallRoot
        $entry = switch ($Mode) {
            'upper' { $root.ToUpperInvariant() }
            'lower' { $root.ToLowerInvariant() }
            'slash' { $root + '\' }
            'upperslash' { $root.ToUpperInvariant() + '\' }
            default { $root }
        }
        $script:LUserPath = 'C:\Windows;' + $entry + ';C:\Tools'
        $null = Install-Launcher -Paths $script:LPaths 6>$null
        Should -Invoke Set-UserPathValue -Times 0 -Exactly
        $script:LUserPath | Should -Be ('C:\Windows;' + $entry + ';C:\Tools')
    }

    It 'appends InstallRoot after the existing PATH entries (<Name>)' -ForEach @(
        @{ Name = 'plain'; Existing = 'C:\Windows;C:\Tools'; Prefix = 'C:\Windows;C:\Tools;' }
        @{ Name = 'trailing semicolon'; Existing = 'C:\Windows;'; Prefix = 'C:\Windows;' }
        @{ Name = 'a similar but different folder'; Existing = 'C:\Windows;{0}-other'; Prefix = 'C:\Windows;{0}-other;' }
        @{ Name = 'empty'; Existing = ''; Prefix = '' }
        @{ Name = 'null'; Existing = $null; Prefix = '' }
    ) {
        $root = $script:LPaths.InstallRoot
        $script:LUserPath = if ($null -eq $Existing) { $null } else { $Existing -f $root }
        $null = Install-Launcher -Paths $script:LPaths 6>$null
        Should -Invoke Set-UserPathValue -Times 1 -Exactly
        $script:LUserPath | Should -Be (($Prefix -f $root) + $root)
    }

    It 'creates InstallRoot when it does not exist yet' {
        Remove-Item -LiteralPath $script:LPaths.InstallRoot -Recurse -Force
        Install-Launcher -Paths $script:LPaths 6>$null | Should -BeTrue
        Test-Path -LiteralPath $script:LPaths.LauncherPath -PathType Leaf | Should -BeTrue
    }
}

Describe 'Launcher and uninstall seams' {
    It 'Get-UserPathValue and Set-UserPathValue exist with the documented parameters' {
        (Get-Command Get-UserPathValue).Parameters.Count | Should -BeGreaterThan 0
        (Get-Command Set-UserPathValue).Parameters.Keys | Should -Contain 'Value'
    }

    It 'New-HuddleShortcut and Get-ShortcutTarget round-trip a shortcut inside TestDrive' -Skip:(-not $IsWindows) {
        $lnk = Join-Path $TestDrive 'seam menu\Agency Huddle.lnk'
        $target = Join-Path $TestDrive 'seam install\huddle.cmd'
        $work = Join-Path $TestDrive 'seam install'
        $icon = Join-Path $TestDrive 'seam install\app\Huddle.App.exe'
        New-HuddleShortcut -Path $lnk -Target $target -WorkingDirectory $work -WindowStyle 7 -IconPath $icon
        Test-Path -LiteralPath $lnk -PathType Leaf | Should -BeTrue
        Get-ShortcutTarget -Path $lnk | Should -Be $target
        $shell = New-Object -ComObject WScript.Shell
        try {
            $link = $shell.CreateShortcut($lnk)
            $link.WindowStyle | Should -Be 7
            $link.WorkingDirectory | Should -Be $work
            $link.IconLocation | Should -BeLike ($icon + '*')
        }
        finally {
            [void][System.Runtime.InteropServices.Marshal]::ReleaseComObject($shell)
        }
    }

    It 'Get-ShortcutTarget returns $null for a missing shortcut' {
        Get-ShortcutTarget -Path (Join-Path $TestDrive 'no such shortcut.lnk') | Should -BeNullOrEmpty
    }
}

Describe 'Test-SafeToDelete' {
    BeforeAll {
        # Runs a block with USERPROFILE pointing somewhere else and always restores it.
        function Invoke-WithUserProfile {
            param([string]$Value, [scriptblock]$Block)
            $saved = $env:USERPROFILE
            try {
                $env:USERPROFILE = $Value
                & $Block
            }
            finally {
                $env:USERPROFILE = $saved
            }
        }
    }

    It 'refuses <Path> (<Why>)' -ForEach @(
        @{ Path = 'C:\'; Why = 'drive root' }
        @{ Path = 'C:/'; Why = 'drive root with a forward slash' }
        @{ Path = 'c:\'; Why = 'lower-case drive root' }
        @{ Path = '\\server\share'; Why = 'UNC share root' }
        @{ Path = '\\server\share\'; Why = 'UNC share root with a trailing backslash' }
        @{ Path = 'C:\one'; Why = 'two segments' }
        @{ Path = 'C:\one\'; Why = 'two segments with a trailing backslash' }
        @{ Path = 'C:\one\two\..'; Why = 'resolves to two segments' }
        @{ Path = 'C:\one\two\..\..'; Why = 'resolves to the drive root' }
        @{ Path = ''; Why = 'empty' }
        @{ Path = '   '; Why = 'blank' }
        @{ Path = 'C:\one\*\three'; Why = 'wildcard star' }
        @{ Path = 'C:\one\two\th?ee'; Why = 'wildcard question mark' }
    ) {
        Test-SafeToDelete -Path $Path | Should -BeFalse
    }

    It 'accepts a folder with three or more segments that is not special' {
        Test-SafeToDelete -Path 'C:\one\two' | Should -BeTrue
        Test-SafeToDelete -Path 'C:\Users\someone\AppData\Local\Programs\Agency.Huddle' | Should -BeTrue
        Test-SafeToDelete -Path 'C:\one\two\' | Should -BeTrue
    }

    It 'refuses the user profile, however it is spelled, and any folder that contains it' {
        Invoke-WithUserProfile 'D:\home\x\tester' {
            Test-SafeToDelete -Path 'D:\home\x\tester' | Should -BeFalse
            Test-SafeToDelete -Path 'D:\home\x\tester\' | Should -BeFalse
            Test-SafeToDelete -Path 'd:\HOME\X\Tester' | Should -BeFalse
            Test-SafeToDelete -Path 'D:\home\x\tester\other\..' | Should -BeFalse
            Test-SafeToDelete -Path 'D:\home\x' | Should -BeFalse
            Test-SafeToDelete -Path 'D:\home\x\tester\Documents' | Should -BeTrue
            Test-SafeToDelete -Path 'D:\home\x\tester2' | Should -BeTrue
        }
    }

    It 'requires install-state.json only when -RequireStateFile is given' {
        $dir = Join-Path $TestDrive ('safe-' + [guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
        Test-SafeToDelete -Path $dir | Should -BeTrue
        Test-SafeToDelete -Path $dir -RequireStateFile | Should -BeFalse
        [System.IO.File]::WriteAllText((Join-Path $dir 'install-state.json'), '{}')
        Test-SafeToDelete -Path $dir -RequireStateFile | Should -BeTrue
    }

    It 'does not accept a folder named install-state.json as the marker' {
        $dir = Join-Path $TestDrive ('marker-' + [guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path (Join-Path $dir 'install-state.json') -Force | Out-Null
        Test-SafeToDelete -Path $dir -RequireStateFile | Should -BeFalse
    }

    It 'returns exactly one boolean' {
        $result = @(Test-SafeToDelete -Path 'C:\')
        $result.Count | Should -Be 1
        $result[0] | Should -BeOfType [bool]
    }
}

Describe 'Invoke-Uninstall' {
    BeforeAll {
        function Invoke-WithUserProfile {
            param([string]$Value, [scriptblock]$Block)
            $saved = $env:USERPROFILE
            try {
                $env:USERPROFILE = $Value
                & $Block
            }
            finally {
                $env:USERPROFILE = $saved
            }
        }
    }

    BeforeEach {
        $script:URoot = Join-Path $TestDrive ('uninstall-' + [guid]::NewGuid().ToString('N'))
        $script:UPaths = Resolve-InstallPaths -InstallRoot (Join-Path $script:URoot 'Programs\Agency Huddle') -DataRoot (Join-Path $script:URoot 'Local\Agency Huddle') -ShortcutPath (Join-Path $script:URoot 'menu\Agency Huddle.lnk')
        New-Item -ItemType Directory -Path (Split-Path -Parent $script:UPaths.ExePath) -Force | Out-Null
        New-Item -ItemType Directory -Path (Split-Path -Parent $script:UPaths.NodeExe) -Force | Out-Null
        New-Item -ItemType Directory -Path (Join-Path $script:UPaths.DataRoot 'data') -Force | Out-Null
        New-Item -ItemType Directory -Path $script:UPaths.CacheDir -Force | Out-Null
        New-Item -ItemType Directory -Path (Split-Path -Parent $script:UPaths.ShortcutPath) -Force | Out-Null
        [System.IO.File]::WriteAllText($script:UPaths.ExePath, 'exe')
        [System.IO.File]::WriteAllText($script:UPaths.NodeExe, 'node')
        [System.IO.File]::WriteAllText($script:UPaths.LauncherPath, 'cmd')
        [System.IO.File]::WriteAllText($script:UPaths.StatePath, '{"schema":"1"}')
        $script:UNotes = Join-Path $script:UPaths.DataRoot 'data\notes.txt'
        [System.IO.File]::WriteAllText($script:UNotes, 'keep me')
        [System.IO.File]::WriteAllText($script:UPaths.ShortcutPath, 'lnk')
        $script:UShortcutTarget = $script:UPaths.LauncherPath
        $script:UPathValue = 'C:\Windows;' + $script:UPaths.InstallRoot + ';C:\Other'
        $script:UStopSawInstall = $null
        Mock Stop-HuddleIfRunning {
            $script:UStopSawInstall = Test-Path -LiteralPath $Paths.InstallRoot
            return $false
        }
        Mock Get-ShortcutTarget { return $script:UShortcutTarget }
        Mock Get-UserPathValue { return $script:UPathValue }
        Mock Set-UserPathValue { $script:UPathValue = $Value }
    }

    It 'removes InstallRoot, the shortcut and the PATH entry, keeps DataRoot and its files, and returns 0' {
        $lines = Get-HostLines { $script:URes = Invoke-Uninstall -Paths $script:UPaths }
        @($script:URes).Count | Should -Be 1
        $script:URes | Should -Be 0
        Test-Path -LiteralPath $script:UPaths.InstallRoot | Should -BeFalse
        Test-Path -LiteralPath $script:UPaths.ShortcutPath | Should -BeFalse
        $script:UPathValue | Should -Be 'C:\Windows;C:\Other'
        Test-Path -LiteralPath $script:UNotes | Should -BeTrue
        Test-Path -LiteralPath $script:UPaths.CacheDir | Should -BeTrue
        ($lines -join "`n") | Should -Not -BeLike '*[[]!!]*'
    }

    It 'with -PurgeData also removes DataRoot' {
        Invoke-Uninstall -Paths $script:UPaths -PurgeData 6>$null | Should -Be 0
        Test-Path -LiteralPath $script:UPaths.InstallRoot | Should -BeFalse
        Test-Path -LiteralPath $script:UPaths.DataRoot | Should -BeFalse
    }

    It 'a second uninstall prints [skip], returns 0 and changes nothing more' {
        $null = Invoke-Uninstall -Paths $script:UPaths 6>$null
        $lines = Get-HostLines { $script:URes = Invoke-Uninstall -Paths $script:UPaths }
        $script:URes | Should -Be 0
        ($lines -join "`n") | Should -BeLike '*[[]skip]*'
        ($lines -join "`n") | Should -Not -BeLike '*[[]!!]*'
        Should -Invoke Stop-HuddleIfRunning -Times 1 -Exactly
        Should -Invoke Set-UserPathValue -Times 1 -Exactly
        Test-Path -LiteralPath $script:UNotes | Should -BeTrue
    }

    It 'calls Stop-HuddleIfRunning first, while the install still exists, and forwards -Force' {
        $null = Invoke-Uninstall -Paths $script:UPaths -Force 6>$null
        Should -Invoke Stop-HuddleIfRunning -Times 1 -Exactly -ParameterFilter { $Force -and $Paths.InstallRoot -eq $script:UPaths.InstallRoot }
        $script:UStopSawInstall | Should -BeTrue
    }

    It 'refuses and deletes nothing when InstallRoot has no install-state.json' {
        Remove-Item -LiteralPath $script:UPaths.StatePath -Force
        $lines = Get-HostLines { $script:URes = Invoke-Uninstall -Paths $script:UPaths -PurgeData }
        $script:URes | Should -Be 1
        $text = $lines -join "`n"
        $text | Should -BeLike '*[[]!!]*'
        $text | Should -BeLike '*fix:*'
        Test-Path -LiteralPath $script:UPaths.ExePath | Should -BeTrue
        Test-Path -LiteralPath $script:UPaths.LauncherPath | Should -BeTrue
        Test-Path -LiteralPath $script:UNotes | Should -BeTrue
        Test-Path -LiteralPath $script:UPaths.ShortcutPath | Should -BeTrue
        $script:UPathValue | Should -BeLike '*Agency Huddle*'
        Should -Invoke Stop-HuddleIfRunning -Times 0 -Exactly
        Should -Invoke Set-UserPathValue -Times 0 -Exactly
    }

    It 'refuses when InstallRoot is a drive root, before it stops, deletes or edits anything' {
        Mock Remove-Item { }
        $rootPaths = Resolve-InstallPaths -InstallRoot 'C:\' -DataRoot $script:UPaths.DataRoot -ShortcutPath $script:UPaths.ShortcutPath
        $lines = Get-HostLines { $script:URes = Invoke-Uninstall -Paths $rootPaths -PurgeData }
        $script:URes | Should -Be 1
        ($lines -join "`n") | Should -BeLike '*[[]!!]*'
        Should -Invoke Remove-Item -Times 0 -Exactly
        Should -Invoke Stop-HuddleIfRunning -Times 0 -Exactly
        Should -Invoke Set-UserPathValue -Times 0 -Exactly
        Test-Path -LiteralPath $script:UNotes | Should -BeTrue
    }

    It 'refuses when InstallRoot is the user profile even if it carries a state file' {
        Mock Remove-Item { }
        Invoke-WithUserProfile $script:UPaths.InstallRoot {
            Invoke-Uninstall -Paths $script:UPaths 6>$null | Should -Be 1
        }
        Should -Invoke Remove-Item -Times 0 -Exactly
        Should -Invoke Stop-HuddleIfRunning -Times 0 -Exactly
        Test-Path -LiteralPath $script:UPaths.StatePath | Should -BeTrue
    }

    It 'with -PurgeData refuses an unsafe DataRoot before removing the install' {
        Mock Remove-Item { }
        Invoke-WithUserProfile $script:UPaths.DataRoot {
            Invoke-Uninstall -Paths $script:UPaths -PurgeData 6>$null | Should -Be 1
        }
        Should -Invoke Remove-Item -Times 0 -Exactly
        Should -Invoke Stop-HuddleIfRunning -Times 0 -Exactly
        Test-Path -LiteralPath $script:UPaths.StatePath | Should -BeTrue
        Test-Path -LiteralPath $script:UNotes | Should -BeTrue
    }

    It 'refuses to remove an InstallRoot that holds DataRoot unless -PurgeData is given' {
        $inner = Resolve-InstallPaths -InstallRoot $script:UPaths.InstallRoot -DataRoot (Join-Path $script:UPaths.InstallRoot 'userdata') -ShortcutPath $script:UPaths.ShortcutPath
        New-Item -ItemType Directory -Path (Join-Path $inner.DataRoot 'data') -Force | Out-Null
        $innerNotes = Join-Path $inner.DataRoot 'data\notes.txt'
        [System.IO.File]::WriteAllText($innerNotes, 'keep me')
        $lines = Get-HostLines { $script:URes = Invoke-Uninstall -Paths $inner }
        $script:URes | Should -Be 1
        ($lines -join "`n") | Should -BeLike '*fix:*'
        Test-Path -LiteralPath $innerNotes | Should -BeTrue
        Test-Path -LiteralPath $inner.StatePath | Should -BeTrue
    }

    It 'returns 1 with [!!] and fix: when Stop-HuddleIfRunning throws, and deletes nothing' {
        Mock Stop-HuddleIfRunning { throw 'Agency Huddle (pid 42) is still running.' }
        $lines = Get-HostLines { $script:URes = Invoke-Uninstall -Paths $script:UPaths }
        $script:URes | Should -Be 1
        $text = $lines -join "`n"
        $text | Should -BeLike '*[[]!!]*'
        $text | Should -BeLike '*fix:*'
        $text | Should -BeLike '*still running*'
        Test-Path -LiteralPath $script:UPaths.StatePath | Should -BeTrue
        Test-Path -LiteralPath $script:UPaths.ShortcutPath | Should -BeTrue
        Should -Invoke Set-UserPathValue -Times 0 -Exactly
    }

    It 'keeps a shortcut whose target is not this install launcher' {
        $script:UShortcutTarget = 'C:\somewhere\else\huddle.cmd'
        $lines = Get-HostLines { $script:URes = Invoke-Uninstall -Paths $script:UPaths }
        $script:URes | Should -Be 0
        Test-Path -LiteralPath $script:UPaths.ShortcutPath | Should -BeTrue
        Test-Path -LiteralPath $script:UPaths.InstallRoot | Should -BeFalse
        ($lines -join "`n") | Should -BeLike '*[[]skip] shortcut*'
    }

    It 'leaves the PATH alone when InstallRoot is not on it' {
        $script:UPathValue = 'C:\Windows;C:\Other'
        Invoke-Uninstall -Paths $script:UPaths 6>$null | Should -Be 0
        Should -Invoke Set-UserPathValue -Times 0 -Exactly
        $script:UPathValue | Should -Be 'C:\Windows;C:\Other'
    }

    It 'removes the PATH entry however it is spelled (case, trailing backslash)' {
        $script:UPathValue = 'C:\Windows;' + $script:UPaths.InstallRoot.ToUpperInvariant() + '\;C:\Other'
        Invoke-Uninstall -Paths $script:UPaths 6>$null | Should -Be 0
        $script:UPathValue | Should -Be 'C:\Windows;C:\Other'
    }

    It 'with -PurgeData and nothing installed still removes an existing DataRoot' {
        Remove-Item -LiteralPath $script:UPaths.InstallRoot -Recurse -Force
        Remove-Item -LiteralPath $script:UPaths.ShortcutPath -Force
        $script:UPathValue = 'C:\Windows'
        Invoke-Uninstall -Paths $script:UPaths -PurgeData 6>$null | Should -Be 0
        Test-Path -LiteralPath $script:UPaths.DataRoot | Should -BeFalse
        Should -Invoke Stop-HuddleIfRunning -Times 0 -Exactly
    }

    It 'keeps install-state.json and returns 1 when a file cannot be removed, so the uninstall can be retried' -Skip:(-not $IsWindows) {
        $stream = [System.IO.File]::Open($script:UPaths.ExePath, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::None)
        try {
            $lines = Get-HostLines { $script:URes = Invoke-Uninstall -Paths $script:UPaths }
        }
        finally {
            $stream.Dispose()
        }

        $script:URes | Should -Be 1
        ($lines -join "`n") | Should -BeLike '*[[]!!]*'
        Test-Path -LiteralPath $script:UPaths.StatePath | Should -BeTrue
        Test-Path -LiteralPath $script:UNotes | Should -BeTrue
    }
}

Describe 'Release-time repository replacement' {
    BeforeAll {
        $rawText = [IO.File]::ReadAllText($ScriptPath)
        $literal = '__HUDDLE_' + 'REPO__'
        # Runs a child Windows PowerShell that dot-sources a script with the guard on and resolves one asset.
        function Get-ResolvedLocation {
            param([string]$Path)
            $quoted = $Path.Replace("'", "''")
            $command = "`$env:HUDDLE_INSTALL_NO_MAIN='1'; Remove-Item Env:HUDDLE_REPO -ErrorAction SilentlyContinue; . '$quoted'; try { 'RESULT:' + (Resolve-AssetSource -Name 'x.zip' -Version 'latest').Location } catch { 'THROWN:' + `$_.Exception.Message }"
            return (& $Ps51Path -NoProfile -Command $command) -join "`n"
        }
    }

    It 'the placeholder literal appears exactly once in the file' {
        ([regex]::Matches($rawText, [regex]::Escape($literal))).Count | Should -Be 1
    }

    It 'a copy with the placeholder replaced as the release workflow does resolves latest downloads' -Skip:$script:NoPs51 {
        $copy = Join-Path $TestDrive 'install-replaced.ps1'
        [IO.File]::WriteAllText($copy, $rawText.Replace($literal, 'owner/repo'), [Text.Encoding]::ASCII)
        $output = Get-ResolvedLocation -Path $copy
        $output | Should -Match 'RESULT:https://github.com/owner/repo/releases/latest/download/x.zip'
        $output | Should -Not -Match 'THROWN'
    }

    It 'the unreplaced original still refuses to run without a base URL or repository' -Skip:$script:NoPs51 {
        $output = Get-ResolvedLocation -Path $ScriptPath
        $output | Should -Match 'THROWN:.*release'
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

    It 'under iex leaves no huddleExitCode variable behind in the session' -Skip:$script:NoPs51 {
        $quotedDir = $installerDir.Replace("'", "''")
        $quotedScript = $ScriptPath.Replace("'", "''")
        $command = "Remove-Item Env:HUDDLE_INSTALL_NO_MAIN -ErrorAction SilentlyContinue; `$env:HUDDLE_CHECK_ONLY='1'; `$env:HUDDLE_BASE_URL='$quotedDir'; iex ([IO.File]::ReadAllText('$quotedScript')) | Out-Null; 'LEAK:' + [bool](Get-Variable huddleExitCode -ErrorAction SilentlyContinue)"
        $output = (& $Ps51Path -NoProfile -Command $command) -join "`n"
        $output | Should -Match 'LEAK:False'
    }

    It 'under iex adds no variable to the session apart from LASTEXITCODE (script-scope names do not persist)' -Skip:$script:NoPs51 {
        $quotedDir = $installerDir.Replace("'", "''")
        $quotedScript = $ScriptPath.Replace("'", "''")
        $command = "Remove-Item Env:HUDDLE_INSTALL_NO_MAIN -ErrorAction SilentlyContinue; `$env:HUDDLE_CHECK_ONLY='1'; `$env:HUDDLE_BASE_URL='$quotedDir'; `$before = @(Get-Variable | ForEach-Object Name); iex ([IO.File]::ReadAllText('$quotedScript')) | Out-Null; 'ADDED:' + ((@(Get-Variable | ForEach-Object Name) | Where-Object { `$before -notcontains `$_ } | Sort-Object) -join ',')"
        $output = (& $Ps51Path -NoProfile -Command $command) -join "`n"
        $output | Should -Match 'ADDED:'
        $added = (($output -split "`n" | Where-Object { $_ -like 'ADDED:*' } | Select-Object -First 1) -replace '^ADDED:', '').Trim()
        $allowed = @('before', 'LASTEXITCODE')
        $unexpected = @($added -split ',' | Where-Object { $_ -and ($allowed -notcontains $_) })
        $unexpected | Should -BeNullOrEmpty
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
