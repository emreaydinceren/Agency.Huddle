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
