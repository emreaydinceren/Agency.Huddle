# Installer

## Contents

- Purpose and quick start
- Installation layout
- Prerequisite checks
- Idempotency and state
- Options and environment variables
- Exit codes and output
- Starting and stopping
- Desktop host mode
- Testing
- Releasing a new version
- Verifying integrity
- First run and Claude sign-in
- Files in the installer folder

How to install and run Agency.Huddle, verify its integrity, and cut a release.

## Purpose and quick start

Agency.Huddle runs on your machine as a desktop app and stores data locally. The
installers download the latest release and set it up, with no admin required.

Start here:

```powershell
# Windows
irm https://github.com/emreaydinceren/Agency.Huddle/releases/latest/download/install.ps1 | iex
```

```sh
# macOS on Apple Silicon; Intel Macs and Linux are not supported
curl -fsSL https://github.com/emreaydinceren/Agency.Huddle/releases/latest/download/install.sh | sh
```

A pinned version:

```powershell
# Windows
& ([scriptblock]::Create((irm https://github.com/emreaydinceren/Agency.Huddle/releases/latest/download/install.ps1))) -Version v1.0.0
```

```sh
# macOS
curl -fsSL https://github.com/emreaydinceren/Agency.Huddle/releases/latest/download/install.sh | sh -s -- --version v1.0.0
```

## Installation layout

Files live in two places: the app and tools in a versioned install folder, and
your data separate so upgrades never touch it.

| | Windows | macOS |
| --- | --- | --- |
| InstallRoot | `%LOCALAPPDATA%\Programs\Agency.Huddle` | `~/Library/Application Support/Agency.Huddle/install` |
| Inside InstallRoot | `app\`, `node\`, `tools\acp\`, `huddle.cmd`, `install-state.json`, `release-info.json` | `app/`, `node/`, `tools/acp/`, `bin/huddle`, `install-state`, `release-info.json` |
| DataRoot | `%LOCALAPPDATA%\Agency.Huddle` | `~/Library/Application Support/Agency.Huddle` |
| DataRoot sub-folders | `data/` (the rooms and personas), `cache/` (npm downloads, logs) | same |
| Entry point | Start menu `Agency Huddle.lnk`; InstallRoot appended to user PATH | `~/Applications/Agency Huddle.app`; symlink `~/.local/bin/huddle` |

## Prerequisite checks

Run first on every install, `-CheckOnly` flags, and in this order. Every check
prints `[ok]` or `[!!]` with a one-line fix message. All failures are reported
at once, not just the first.

| Check | What it requires |
| --- | --- |
| OS version | Windows build >= 17763 (Windows 10 1809); macOS major >= 15 |
| CPU | Windows: x86_64 (AMD64) or ARM64, each with its own native build (`win-x64`, `win-arm64`). macOS: Apple Silicon only (`osx-arm64`); an Intel Mac fails this check |
| Tools | PowerShell >= 5.1 and `tar.exe` (Windows); `curl`, `tar`, `shasum` or `sha256sum`, `codesign`, `xattr` (macOS) |
| Disk space | >= 2048 MB free on the InstallRoot drive and DataRoot drive |
| Write access | InstallRoot parent and DataRoot must be creatable |
| Network | HTTPS reach to `github.com`, `nodejs.org`, `registry.npmjs.org` (skipped if assets are local) |

Requirements not checked: admin or sudo, .NET, Node, npm, git. `-CheckOnly` /
`--check-only` runs the table above and exits 0 (all pass) or 2 (any fail), no
changes made.

## Idempotency and state

State is stored in `install-state.json` (Windows) or an `install-state` file
(macOS), with the keys `version`, `appSha256`, `nodeVersion` and `lockSha256`
(`app_sha256`, `node_version`, `lock_sha256` on macOS), plus `rid` on Windows. A second
run with nothing new prints `[skip]` for every step, ends with `Already up to
date`, changes no file, and exits 0. An interrupted run resumes at the first
unsatisfied step: state keys are written last.

| Step | Skipped when |
| --- | --- |
| App | State SHA256 matches the release archive AND the host exists |
| Node | State version matches `release-info.json` AND `node --version` prints it |
| Adapter | State SHA256 matches `package-lock.json` AND the entry file exists |
| Launcher | Never skipped; rewritten only if content differs |

A running instance is stopped (via `<host> stop`) only if app, Node or adapter
will change. The `stop` verb itself waits up to 45 seconds for clean shutdown;
`-Force` / `--force` ends the process immediately.

Two installers at once: the second exits 1 immediately (Windows named mutex; macOS
`mkdir install.lock`). Uninstall of nothing installed exits 0 with `[skip]`.

## Options and environment variables

Every installer option has an environment variable. The option wins if both are
given. Set via `-Parameter` (Windows) or `--parameter` (macOS), or via
`HUDDLE_*` variable for hands-free use (`irm | iex` cannot pass parameters).

| Option | Env var | Meaning |
| --- | --- | --- |
| `-Version <tag>` | `HUDDLE_VERSION` | Release to install (default `latest`) |
| `-BaseUrl <url or path>` | `HUDDLE_BASE_URL` | Assets location; a folder for tests and CI |
| `-InstallRoot <path>` | `HUDDLE_INSTALL_ROOT` | Override default install folder |
| `-DataRoot <path>` | `HUDDLE_DATA_ROOT` | Override default data folder |
| `-CheckOnly` | `HUDDLE_CHECK_ONLY` | Run prerequisites only, no changes |
| `-Uninstall` | `HUDDLE_UNINSTALL` | Remove (data kept unless purged) |
| `-PurgeData` | `HUDDLE_PURGE_DATA` | Remove data with uninstall (invalid without `-Uninstall`) |
| `-Force` | `HUDDLE_FORCE` | Force stop a running instance; reinstall without checks |
| `-NoLaunch` | `HUDDLE_NO_LAUNCH` | Do not start the app at the end |
| `-NoShellIntegration` (Windows) | `HUDDLE_NO_SHELL_INTEGRATION` | Write only `huddle.cmd`: no Start-menu shortcut and no PATH change (used by the smoke test) |

A variable is on when it is set to anything other than empty or `0`. The version
option accepts `latest` or a version such as `v1.2.3` and nothing else.

Env var example (uninstall via `irm | iex`):

```powershell
$env:HUDDLE_UNINSTALL='1'; irm https://github.com/emreaydinceren/Agency.Huddle/releases/latest/download/install.ps1 | iex
```

```sh
curl -fsSL https://github.com/emreaydinceren/Agency.Huddle/releases/latest/download/install.sh | HUDDLE_UNINSTALL=1 sh
```

## Exit codes and output

Exit 0: success, already up to date, or check passed. Exit 1: a step failed
(permissions, hash mismatch, network timeout, stop timeout). Exit 2: prerequisites
unmet. Exit 3: bad usage (unknown flag, bad version format, purge without uninstall).

Output is one line per step: `[ok] name - detail`, `[skip] name - already satisfied`,
`[..] name - working`, `[!!] name - detail` then `fix: ...`. Final lines on success:
`Agency Huddle <version> installed.` (or `Already up to date (<version>).`), then how
to start it, then the Claude sign-in note (see below).

The script never prompts, except optional sign-in (only when stdin is a terminal).

## Starting and stopping

Start the app:

```powershell
# Windows: from Start menu or from PowerShell
huddle
```

```sh
# macOS: from Applications folder or from Terminal
huddle
```

Stop the running instance:

```powershell
huddle stop
```

```sh
huddle stop
```

## Desktop host mode

The app has a desktop mode, off unless `Team:Desktop:Enabled` is true (the launcher
sets it; tests and `run.ps1` never do). In that mode the app takes a file lock,
`<DataDir>/instance.lock`, and publishes `<DataDir>/instance.json` with its URL, a
random stop token and its process ID, where `<DataDir>` is `<DataRoot>/data`. The OS
releases the lock when the process ends, even in a crash, so there is no stale lock;
a leftover `instance.json` is deleted by the next instance as soon as it owns the lock.

The launcher sets `Team__Desktop__Enabled=true`, `Team__Acp__Enabled=true`,
`Team__Acp__Command` (the bundled Node), `Team__DataDir`, `ASPNETCORE_URLS=http://127.0.0.1:0`
(loopback only, kernel-chosen port), `AllowedHosts=localhost;127.0.0.1` and
`Team__PipeName=huddle-<user name>`. The pipe name is per user because the default,
`team`, is one machine-wide pipe: a second Windows user's instance, or a developer's
`run.ps1` instance, would collide with it.

A second launch finds the lock held, waits for `instance.json` and opens that URL in the
browser instead of starting a second app (if the old instance is shutting down, the new
launch becomes the owner). `stop` checks the lock first: with no owner it deletes any stale
record and reports "not running"; otherwise it sends `POST /_desktop/stop` with the token in
the `X-Huddle-Token` header (loopback only, token compared in constant time), then waits up
to 45 seconds for the lock to be released.

**Remote access.** The launcher mints a bootstrap token when it starts the app and opens
`http://127.0.0.1:<port>/auth?return=%2F#bootstrapToken=...`, so the browser connects without a typed token. A second
launch asks the running copy for a fresh link through `POST /_desktop/bootstrap` (authenticated with the same token in
`instance.json`), and falls back to `<url>/auth` if that fails. Because `ASPNETCORE_URLS=http://127.0.0.1:0` would give the
browser a new origin, and so a new device key, on every start, the app stores the first port it gets in `gateway_settings`
and binds it again on the next start, falling back to a free port when it is busy. A non-loopback bind
(`Team:Gateway:Bind` other than `loopback`) on an installed copy still needs `Team:Gateway:Port`; without it the copy
starts loopback-only and says why. `AllowedHosts` stays `localhost;127.0.0.1`, and the gateway adds the hosts it needs.

On macOS the installer installs `osx-arm64`, including from a Rosetta shell on Apple Silicon
(`sysctl hw.optional.arm64` decides). Intel Macs are not supported: the CPU check fails and
nothing is downloaded.

On Windows it picks `win-arm64` on an ARM64 PC and `win-x64` everywhere else, and downloads the
Node build of the same CPU, so npm installs the matching Claude binary
(`claude-agent-sdk-win32-arm64` or `-win32-x64`). Two cases around the switch:

- A release older than the `win-arm64` build (asked for with `-Version`) has only the x64 zip. An
  ARM64 PC then installs that one, says so, and runs it under Windows' x64 emulation.
- The state file's `rid` records the build that Node and the adapter were installed for; a state
  file without it is a `win-x64` install. When the build changes (an ARM64 PC moving from x64 to
  the native build), Node and the adapter install again instead of `[skip]`, and `rid` is written
  only after both succeed.

## Testing

### Pester (PowerShell unit tests)

```powershell
# Windows: run all tests
pwsh -NoProfile -Command "Invoke-Pester installer/tests -CI"

# Run one test class
pwsh -NoProfile -Command "Invoke-Pester installer/tests/Install.Tests.ps1 -CI"
```

### Shell integration tests

```sh
# Run all shell tests under POSIX sh and dash
sh installer/tests/run-all.sh
T_SHELL=dash dash installer/tests/run-all.sh

# Run one test file
sh installer/tests/install_test.sh
```

On Windows, the shell suite is slow (Git Bash). Set `T_ONLY` to run one test:

```sh
T_ONLY=test_main_first_run sh installer/tests/install_test.sh
```

### End-to-end smoke tests

```powershell
# Windows (GitHub Workflow also runs this)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File installer/tests/smoke.ps1 -ReleaseDir dist
```

`smoke.ps1` tests the build `install.ps1` picks for the PC it runs on (`huddle-win-arm64.zip` on
ARM64, `huddle-win-x64.zip` otherwise) and fails if the installed `release-info.json` names another.
`smoke.sh` does the same with `osx-arm64`, and fails on an Intel Mac.

```sh
# macOS
sh installer/tests/smoke.sh dist
```

The smoke test checks `GET /health` and that `MudBlazor.min.css` comes back non-empty. The
second check exists because `/health` passes when the app cannot find its `wwwroot`: the app
takes its content root from the current directory, so started from any other folder every
stylesheet and script answers `200` with an empty body and the page renders unstyled. Both
launchers `cd` into the `app` folder for that reason.

### A mock release on a clean machine

Before the first public release, the one-liner can be run end to end against a release you build
yourself, on a machine (or VM) that has nothing installed. `-BaseUrl` takes an `https://` URL or a
folder, so serve the files over plain HTTP from the build machine and let the clean machine copy
them into a folder that stands in for the GitHub release page.

On the build machine, from the repository root:

```sh
sh installer/build-release.sh --version 0.0.1 --rid win-x64 --out dist
sed "s|__HUDDLE_REPO__|emreaydinceren/Agency.Huddle|g" installer/install.ps1 > dist/install.ps1
sh installer/make-sums.sh dist huddle-win-x64.zip install.ps1
python -m http.server 8765 --directory dist
```

On the clean Windows machine, in a normal PowerShell window (`<host>` is the build machine's
address, for a Hyper-V Default Switch VM the host's `vEthernet (Default Switch)` address):

```powershell
$m = "$env:USERPROFILE\Downloads\mock-github-release"; New-Item $m -ItemType Directory -Force > $null
'huddle-win-x64.zip', 'SHA256SUMS', 'install.ps1' | ForEach-Object { Invoke-WebRequest "http://<host>:8765/$_" -OutFile "$m\$_" -UseBasicParsing }
$env:HUDDLE_BASE_URL = $m; irm http://<host>:8765/install.ps1 | iex
```

Run this way on 2026-11-15 on a Windows 11 VM with no .NET, no Node, Windows PowerShell 5.1, the
execution policy `Restricted` and only Claude Code installed and signed in: every step passed,
the browser opened on the styled first-run page, and the Chief of Staff answered a message.

The Claude Code native installer leaves `%USERPROFILE%\.local\bin` off the user PATH on such a
machine, so `claude` is "not recognized" there. Huddle does not depend on it: the sign-in check
uses the copy of Claude Code that the adapter brings with it, which reads the same sign-in.

The same day, on an Apple Silicon Mac with macOS 27.0.1, the `osx-arm64` archive (cross-built on
Windows) installed with `install.sh` the same way (`HUDDLE_BASE_URL` set to the downloaded folder),
started from `Agency Huddle.app`, and the Chief of Staff answered. That run found the reason the
macOS host is not named after the assembly: **macOS kills any executable whose name ends in
`.app`**, case-insensitively and with no crash report (`SIGKILL`, exit code 137), even a copy of
`/bin/echo` named `Echo.App`. The apphost `Huddle.App` therefore never started. `build-release.sh`
renames it to `app/HuddleApp` in the macOS archives; it still finds `Huddle.App.dll` by its embedded
name. A hosted macOS runner older than macOS 27 may not show this, so the CI smoke test alone does
not prove it.

Over SSH, `claude auth status` reports "not signed in" on a Mac that is signed in: an SSH session
cannot read the login keychain. The app started from the Mac's own session (`open`, Spotlight or
Launchpad) reads it fine.

## Releasing a new version

The repository is a public mirror at `github.com/emreaydinceren/Agency.Huddle`.
Gitea `main` is the source of truth; nothing is pushed to GitHub by hand.

The mirror carries `main` only, so there is no tag to push: the release is started by
hand. To cut one:

1. Merge the work into Gitea `main`.
2. Run the Gitea workflow `sync-github` (Actions tab).
3. On GitHub, open Actions, choose `Release`, and run it from `main`. Leave `version` empty to
   get the next version, or type one.

**Versions only go up.** `installer/next-version.sh` decides the version from the `v*` tags that
already exist:

| `version` input | Result |
| --- | --- |
| Empty | The latest tag with `bump` applied: `patch` (default) `1.2.3` → `1.2.4`, `minor` → `1.3.0`, `major` → `2.0.0`. With no tags yet, `0.1.0` |
| Empty, the latest is only a pre-release (`1.3.0-rc.1`, no `1.3.0`) | Its final release, `1.3.0` |
| Typed (`2.0.0`, `1.5.0-rc.1`) | That version, if it is newer than every tag; otherwise the run fails before anything is built. A suffix such as `-rc.1` makes a pre-release |

`build-release.sh` passes the version to `dotnet publish` as `-p:Version`, so the app's file and
product version and its `AssemblyInformationalVersion` carry it as well as `release-info.json`.

The `Release` workflow (`.github/workflows/release.yaml`) runs these jobs in order:

1. `version`: checks the run is from `main` and decides the version as above.
2. `build`: `installer/build-release.sh` for `win-x64`, `win-arm64` and `osx-arm64`, all
   cross-built on Linux. There is no `osx-x64` build: Intel Macs are not supported.
3. `assemble`: copies `install.ps1` and `install.sh` with `__HUDDLE_REPO__` replaced by the
   repository name (and fails if the placeholder is still there), writes `SHA256SUMS`, and
   records build provenance for every file.
4. Three smoke jobs, each installing the assembled release for real on a hosted runner of its own
   CPU and running the whole life cycle (`installer/tests/smoke.ps1`, `smoke.sh`):

   | Job | Runner | Build it installs |
   | --- | --- | --- |
   | `smoke-windows` | `windows-latest` | `win-x64` |
   | `smoke-windows-arm64` | `windows-11-arm` (public preview) | `win-arm64` |
   | `smoke-macos` | `macos-latest` | `osx-arm64` |

5. `publish`: only if all three smoke jobs pass, creates the release and its tag `v<version>` from
   the exact commit that was built, and refuses a version that already exists.

Release files: `huddle-win-x64.zip`, `huddle-win-arm64.zip`, `huddle-osx-arm64.tar.gz`,
`install.ps1`, `install.sh`, `SHA256SUMS`. Each archive holds `app/` (self-contained .NET),
`tools/acp/package.json` and `package-lock.json`, and `release-info.json` (schema, version,
RID, Node version). Node and the Claude adapter are **not** in the archive: the installer
fetches the pinned Node and runs `npm ci` on the user's machine.

**Manual steps not done yet** (they need the first sync, a Mac and real accounts):

- M1: run `sync-github`, then the `Release` workflow once; use the one-liner against it.
- M2: a clean Windows account. Done against a mock release on 2026-11-15 (see
  [A mock release on a clean machine](#a-mock-release-on-a-clean-machine)); repeat it with the first real release.
- M3: an Apple Silicon Mac. Done against a mock release on 2026-11-15; repeat it with the first
  real release. The native ARM64 Windows build is smoke-tested in CI (`smoke-windows-arm64`)
  but has not been tried on real hardware.
- M4: behind a corporate proxy.
- M5: a real Claude sign-in and one Turn (spends money). Done on Windows and on the Apple Silicon Mac
  with the same mock releases.

Until M1 has run, the workflows under `.github/workflows/` have never executed, and
`shellcheck`, PSScriptAnalyzer and `actionlint` were not available when they were written.

## Verifying integrity

Every installer download is hash-checked before anything is extracted or run: the release
archive against `SHA256SUMS`, and the Node archive against the `SHASUMS256.txt` that
nodejs.org publishes for the pinned version. A file that fails the check is deleted.

`SHA256SUMS` comes from the same release as the archives, so on its own it only proves the
download was not corrupted: someone who could replace a release file could replace the
checksum too. The independent check is the build provenance recorded for every release file.
It shows the file was built by the `Release` workflow from a given commit, and anyone can
verify it with the GitHub CLI:

```sh
gh attestation verify huddle-osx-arm64.tar.gz --repo emreaydinceren/Agency.Huddle
```

Hashes can also be checked by hand (`certUtil -hashfile <file> SHA256` on Windows,
`shasum -a 256 <file>` on macOS) against the matching line of `SHA256SUMS`. Node's own
checksum file is trusted over TLS only.

## First run and Claude sign-in

After successful install, the app starts and opens in your browser. A one-time
sign-in is required:

```sh
claude auth login
```

The installer checks for existing credentials (`claude auth status`). If none are
found, it prints the sign-in instruction but does not fail — you can sign in after
starting the app for the first time.

For scripted/non-interactive installs (SSH, CI), the sign-in step is skipped.

## Files in the installer folder

| File | Purpose |
| --- | --- |
| `install.ps1` | Windows installer (PowerShell) |
| `install.sh` | macOS installer (POSIX shell, runs under `sh` and `dash`) |
| `build-release.sh` | Builds one release archive for a runtime id (called by the Release workflow) |
| `make-sums.sh` | Writes `SHA256SUMS` atomically, sorted in C-locale order |
| `next-version.sh` | Decides the next release version from the existing tags; versions only go up |
| `node-version.txt` | Pinned Node.js LTS version (read with CR and LF stripped) |
| `PSScriptAnalyzerSettings.psd1` | Settings for the PowerShell lint job |
| `tests/Install.Tests.ps1` | Pester tests for `install.ps1` |
| `tests/install_test.sh` | Shell tests for `install.sh` |
| `tests/run-all.sh` | Runner for all shell tests |
| `tests/smoke.ps1` | End-to-end smoke test (Windows) |
| `tests/smoke.sh` | End-to-end smoke test (macOS) |

Back to the hub: [Huddle.EngineeringGuide.md](../Huddle.EngineeringGuide.md).
