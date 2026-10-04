# Installer

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
# macOS (Apple Silicon or Intel); Linux is not supported
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
| CPU | x86_64 (AMD64) or ARM64; Windows ARM64 runs x64 under emulation with a warning |
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
(`app_sha256`, `node_version`, `lock_sha256` on macOS). A second
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

On macOS the installer picks `osx-arm64` or `osx-x64` from the real CPU, including from a
Rosetta shell on Apple Silicon.

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

```sh
# macOS
sh installer/tests/smoke.sh dist
```

## Releasing a new version

The repository is a public mirror at `github.com/emreaydinceren/Agency.Huddle`.
Gitea `main` is the source of truth; nothing is pushed to GitHub by hand.

The mirror carries `main` only, so there is no tag to push: the release is started by
hand. To cut one:

1. Merge the work into Gitea `main`.
2. Run the Gitea workflow `sync-github` (Actions tab).
3. On GitHub, open Actions, choose `Release`, and run it from `main` with the `version`
   input (for example `1.0.0`; a suffix such as `1.0.0-rc.1` makes a pre-release).

The `Release` workflow (`.github/workflows/release.yaml`) runs these jobs in order:

1. `build`: `installer/build-release.sh` for `win-x64`, `osx-arm64` and `osx-x64`.
2. `assemble`: copies `install.ps1` and `install.sh` with `__HUDDLE_REPO__` replaced by the
   repository name (and fails if the placeholder is still there), writes `SHA256SUMS`, and
   records build provenance for every file.
3. `smoke-windows` and `smoke-macos`: install the assembled release for real on a hosted
   runner and run the whole life cycle (`installer/tests/smoke.ps1`, `smoke.sh`).
4. `publish`: only if both smoke jobs pass, creates the release and its tag `v<version>` from
   the exact commit that was built, and refuses a version that already exists.

Release files: `huddle-win-x64.zip`, `huddle-osx-arm64.tar.gz`, `huddle-osx-x64.tar.gz`,
`install.ps1`, `install.sh`, `SHA256SUMS`. Each archive holds `app/` (self-contained .NET),
`tools/acp/package.json` and `package-lock.json`, and `release-info.json` (schema, version,
RID, Node version). Node and the Claude adapter are **not** in the archive: the installer
fetches the pinned Node and runs `npm ci` on the user's machine.

**Manual steps not done yet** (they need the first sync, a Mac and real accounts):

- M1: run `sync-github`, then the `Release` workflow once; use the one-liner against it.
- M2: a clean Windows account.
- M3: an Apple Silicon Mac and, if there is one, an Intel Mac (the Intel build is not
  smoke-tested in CI).
- M4: behind a corporate proxy.
- M5: a real Claude sign-in and one Turn (spends money).

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
| `node-version.txt` | Pinned Node.js LTS version (read with CR and LF stripped) |
| `PSScriptAnalyzerSettings.psd1` | Settings for the PowerShell lint job |
| `tests/Install.Tests.ps1` | Pester tests for `install.ps1` |
| `tests/install_test.sh` | Shell tests for `install.sh` |
| `tests/run-all.sh` | Runner for all shell tests |
| `tests/smoke.ps1` | End-to-end smoke test (Windows) |
| `tests/smoke.sh` | End-to-end smoke test (macOS) |

Back to the hub: [Huddle.EngineeringGuide.md](../Huddle.EngineeringGuide.md).
