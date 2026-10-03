# One-line installers for Windows and macOS: delivery plan

Ship Agency.Huddle so a user runs **one command**, answers nothing, and gets a working
app: a self-contained build, Node and the Claude adapter fetched at install time, a launcher,
and a Start-menu or `.app` entry. Written 2026-10-02. Decomposed for sub-agents with no
project context; run it with the Execute-Project-Plan skill.

> [!NOTE]
> This page is a **delivery plan**, not documentation. When the work lands, task 43 writes the
> permanent page (`installer.md`), swaps the `docs/Index.md` row, and this page is deleted, as
> `docs/Index.md` and the delivery playbook already require for delivered plans.

## Decisions already made (do not reopen)

| # | Decision | Why |
| --- | --- | --- |
| D1 | Install by a script a user runs from a release URL: `irm <url>/install.ps1 \| iex` on Windows, `curl -fsSL <url>/install.sh \| sh` on macOS. | No signing certificate and no notarization needed: files fetched by `irm`/`curl` carry no Mark of the Web or quarantine flag. |
| D2 | The release holds **only the .NET app** (self-contained, **not** trimmed, **not** Native AOT: Blazor Server and MudBlazor are reflection-based). | Smallest download; no per-OS adapter tree to build. |
| D3 | The installer fetches **Node** (pinned, from nodejs.org, hash-checked) and runs **`npm ci --omit=dev`** against the shipped `package-lock.json`. | Claude's own binary then comes from npm as Anthropic distributes it, not from our release. |
| D4 | Targets: `win-x64` (Windows on ARM64 runs it under emulation), `osx-arm64`, `osx-x64`. No Linux. | Product decision. |
| D5 | Per-user install, **no admin / sudo**, no .NET or Node prerequisite for the user. | "Minimum hassle." |
| D6 | The installer is **idempotent and non-interactive**; its checks decide skip-or-do from hashes, not from timestamps. | See Spec §6. |
| D7 | Releases are built and published by **GitHub Actions** from a `v*` tag. Until the repo moves to GitHub, nothing here runs on Gitea; the scripts take `-BaseUrl`/`--base-url` so everything is testable from a local folder. | The repo is on Gitea today and will be on GitHub. |
| D8 | Port 0 on `127.0.0.1`, a lock file for single-instance, and a token-guarded stop endpoint (Spec §8). A named-pipe HTTP listener was rejected: browsers cannot connect to one. | |

## Assumptions to confirm before shipping (none block starting)

- A1. The repo will be **public**; until then, `actions/attest-build-provenance` is skipped by an `if:` and anonymous downloads do not work. Trial the release on a throwaway public repo (manual step M1).
- A2. `<org>/<repo>` is unknown today, so scripts carry the placeholder `__HUDDLE_REPO__`, replaced by the release workflow with `${{ github.repository }}`. Override with `$env:HUDDLE_REPO` / `HUDDLE_REPO=`.
- A3. Redistribution: the release contains no Anthropic binary (D3). If that changes, check Anthropic's terms first.
- A4. v1 has **no in-app Quit button**. Windows users close the console window or run `huddle stop`; macOS users run `huddle stop`. Recorded as a known limit by task 43, and a candidate for `roadmap.md`.
- A5. Graceful stop disposes Persona adapters one after another (`PersonaSupervisor`, about 3 s grace each) and one thrown exception skips the rest, so `stop` can report "Stopped." while an adapter `node` process survives (found by the final Opus review, not fixed here: it is existing ACP shutdown code). The smoke tests (39, 40) therefore assert that **no `node` process from the install tree remains** after `stop`; a failure there is the trigger to harden the shutdown, and the upgrade step cannot replace `node/` while an adapter still runs.

---

## Conventions (cited by tasks as `Conv C1` ... `Conv C7`)

**C1. C# (every task under `src/Huddle.App` or `tests/Huddle.Tests`).**
- Build and test the **solution**: `dotnet build Huddle.slnx` then `dotnet test Huddle.slnx --` (the trailing `--` is required; `CLAUDE.md` "C# code"). Warnings are errors.
- Style: `agents/CSharpPrinciples.md` L43-L163 (rules that fail the build), L165-L200 (house conventions: `///` XML docs on everything, `sealed` classes, `this.field`, explicit types with target-typed `new()`), L143-L163 (tests).
- `docs/engineering/rules.md` (97 lines) is **binding** before editing `src/Huddle.App`; read it whole.
- `internal` types are testable: `src/Huddle.App/Huddle.App.csproj` L12-L14 already declares `InternalsVisibleTo Huddle.Tests`.
- Tests: `tests/Huddle.Tests/Desktop/`, namespace `Agency.Huddle.Tests.Desktop`, `public sealed class XxxTests`, every test has a `///` summary and passes `TestContext.Current.CancellationToken` to awaited calls. Use `TempDataDir` (`tests/Huddle.Tests/TempDataDir.cs`, whole file) for directories. Helpers: `agents/Testing.md` L112-L144.
- Line endings are CRLF (`.editorconfig`; `Check-Eol.ps1`). **The Write tool emits LF**: after writing a `.cs`, `.ps1` or `.md` file run `pwsh agents/scripts/Fix-Crlf.ps1 <paths>`. Shell scripts (`*.sh`) are the one exception: they must stay **LF** (task 5 teaches the checker).

**C2. PowerShell (`installer/install.ps1`, `installer/tests/*.ps1`).**
- Must parse and run on **Windows PowerShell 5.1 and pwsh 7**: no `&&`/`||`, no ternary `?:`, no `??`, no `-Parallel`, no `ForEach-Object -Parallel`, no `Join-String`.
- `Set-StrictMode -Version Latest` and `$ErrorActionPreference = 'Stop'` at the START OF `Invoke-Main` (function scope), never at the script's top level: under `irm | iex` a top-level setting would leak into the user's interactive session (found in review of task 17; a Pester test proves `EAP:Continue` afterwards). Tests set strict mode in `BeforeAll`.
- **ASCII only** in `install.ps1` (it is fed to `Invoke-Expression` as a string; non-ASCII breaks under some encodings). One self-contained file, no dot-sourced helpers (`irm | iex` fetches one file).
- Importable for tests: the last line is `if (-not $env:HUDDLE_INSTALL_NO_MAIN) { Invoke-Main }`. Tests set `$env:HUDDLE_INSTALL_NO_MAIN = '1'` and dot-source `installer/install.ps1`.
- Every external effect sits behind a **seam function** so Pester can `Mock` it: `Invoke-Tar`, `Invoke-NpmCi`, `Get-InstalledNodeVersion`, `Invoke-HuddleStop`, `Get-FreeDiskMb`, `Test-UrlReachable`, `Get-OsBuild`, `Get-HostArchitecture`, `Test-CommandAvailable`, `Get-UserPathValue`, `Set-UserPathValue`, `New-HuddleShortcut`, `Invoke-Download`. Nothing else shells out.
- Pester **5.5+** (`Install-Module Pester -MinimumVersion 5.5 -Scope CurrentUser -Force -SkipPublisherCheck`). Run: `Invoke-Pester installer/tests -CI`.

**C3. POSIX shell (`installer/*.sh`, `installer/tests/*.sh`).**
- `#!/bin/sh`, `set -eu`. macOS `/bin/sh` is bash 3.2 and Ubuntu's is dash: **no** `[[ ]]`, arrays, `<<<`, process substitution, `echo -e`, `${var,,}`, `local -`. `local` is allowed.
- `shellcheck -s sh` clean. **LF line endings only** (`.gitattributes`, task 4).
- No `jq`, `python` or `plutil` (not guaranteed): read flat JSON string values with `sed -n 's/.*"key" *: *"\([^"]*\)".*/\1/p'`.
- Hash with `sha256sum` if present, else `shasum -a 256`.
- Importable for tests: last line `[ -n "${HUDDLE_INSTALL_NO_MAIN:-}" ] || main "$@"`. Seams (functions tests override after sourcing): `run_tar`, `run_npm_ci`, `run_codesign`, `run_xattr`, `http_get`, `http_probe`, `node_version_of`, `free_disk_mb`, `host_arch`, `macos_major`, `run_huddle_stop`.
- Tests use `installer/tests/lib.sh` (task 28.t creates it): `t_assert_eq`, `t_assert_contains`, `t_assert_file`, `t_assert_no_file`, `t_run <name>`; a runner `installer/tests/run-all.sh`. Tests set `HOME` to a temp dir, never touch the real one.

**C4. Reading `installer/node-version.txt`.** The repo is CRLF (`Check-Eol.ps1`), so every reader strips `\r`, `\n` and spaces.

**C5. Docs.** Adding or deleting any page under `docs/` changes `docs/Index.md` in the same commit (`agents/scripts/Check-DocIndex.ps1`).

**C6. Shared root files.** `.gitattributes`, `.gitignore`, `agents/scripts/*`, `.github/**` are shared: say so in the task report. Never edit `.gitea/**` in this plan.

**C7. Never spend money.** No task sends a prompt to Claude or logs in. Tests use fakes; the smoke tests never start a Persona.

## Specification

### Spec §1. Goals and non-goals

Goal: from a clean user account, one command installs a working Agency.Huddle with a Start-menu or `.app` launcher, in a few minutes, with no prompts, and rerunning it is safe and fast.
Non-goals: code signing or notarization; Linux; an auto-updater (rerun the command); an in-app Quit; a GUI installer.

### Spec §2. Release artifacts

Attached to a GitHub release `vX.Y.Z`. File names are **fixed (no version in the name)** so `.../releases/latest/download/<name>` works with no API call.

| File | Content |
| --- | --- |
| `huddle-win-x64.zip` | Root entries: `app/` (self-contained `dotnet publish` output; host `app/Huddle.App.exe`), `tools/acp/package.json`, `tools/acp/package-lock.json`, `release-info.json` |
| `huddle-osx-arm64.tar.gz`, `huddle-osx-x64.tar.gz` | Same layout; host `app/Huddle.App` |
| `install.ps1`, `install.sh` | The installers, `__HUDDLE_REPO__` replaced |
| `SHA256SUMS` | One line per file above: `<64 lowercase hex>␠␠<name>` (two spaces) |

`release-info.json` (flat string values only): `{"schema":"1","version":"1.2.3","rid":"win-x64","nodeVersion":"v24.13.1"}`.
`installer/node-version.txt` holds the pinned Node version (Conv C4); `build-release.sh` copies it into `release-info.json`.
Pinned install: `.../releases/download/<tag>/<name>`; `-Version`/`--version` selects `<tag>`.

User commands:

```powershell
irm https://github.com/<org>/<repo>/releases/latest/download/install.ps1 | iex
& ([scriptblock]::Create((irm https://github.com/<org>/<repo>/releases/latest/download/install.ps1))) -Version v1.2.3
```

```sh
curl -fsSL https://github.com/<org>/<repo>/releases/latest/download/install.sh | sh
curl -fsSL https://github.com/<org>/<repo>/releases/latest/download/install.sh | sh -s -- --version v1.2.3
```

### Spec §3. Install layout

`app/` and `tools/` **must be siblings**: `AdapterLocator` (`src/Huddle.App/Acp/AdapterLocator.cs`) walks up from `AppContext.BaseDirectory` looking for `tools/acp/node_modules/@agentclientprotocol/claude-agent-acp/dist/index.js`.

| | Windows | macOS |
| --- | --- | --- |
| InstallRoot | `%LOCALAPPDATA%\Programs\Agency.Huddle` | `~/Library/Application Support/Agency.Huddle/install` |
| Inside InstallRoot | `app\`, `node\`, `tools\acp\`, `huddle.cmd`, `install-state.json`, `release-info.json` | `app/`, `node/`, `tools/acp/`, `bin/huddle`, `install-state`, `release-info.json` |
| DataRoot (`Team:DataDir` = `<DataRoot>\data`) | `%LOCALAPPDATA%\Agency.Huddle` | `~/Library/Application Support/Agency.Huddle` |
| Cache (downloads, npm log) | `<DataRoot>\cache` | `<DataRoot>/cache` |
| Entry points | Start menu `Agency Huddle.lnk`; InstallRoot appended to the **user** PATH | `~/Applications/Agency Huddle.app`; symlink `~/.local/bin/huddle` -> `<InstallRoot>/bin/huddle` |

User data is never inside InstallRoot, so replacing the install never touches it.

### Spec §4. Runtime environment

The launcher (`huddle.cmd` / `bin/huddle`) sets these, then runs the app host with the launcher's arguments appended:

| Variable | Value |
| --- | --- |
| `Team__Desktop__Enabled` | `true` |
| `Team__Acp__Enabled` | `true` (off by default in code; Spec §8, `AcpOptions.Enabled`) |
| `Team__Acp__Command` | absolute path of the bundled `node` / `node.exe` |
| `Team__DataDir` | `<DataRoot>/data` |
| `Team__PipeName` | `huddle-<user name>` (Windows `huddle-%USERNAME%`, macOS `huddle-$(id -un)`). The default `team` is one machine-wide pipe: a second Windows user's instance, or a developer's `run.ps1` instance, would collide with it (found by the final Opus review) |
| `ASPNETCORE_URLS` | `http://127.0.0.1:0` (loopback, kernel-chosen port) |
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `AllowedHosts` | `localhost;127.0.0.1` (closes DNS-rebinding; `appsettings.json` has `*`) |
| `PATH` | bundled node directory prepended (the adapter's children must find `node`) |

Anything else already in the environment is inherited, which is how the smoke tests set `Team__Desktop__OpenBrowser=false`.
Launcher verbs: no argument = start (or hand off to the running instance and open its URL); `stop` = stop the running instance.

### Spec §5. Prerequisite checks

Run first, read-only, in this order. Every item prints `[ok]` or `[!!]` plus a one-line `fix:`; **all** failures are reported, not just the first.

| Check | Windows | macOS |
| --- | --- | --- |
| OS version | build >= `MIN_WINDOWS_BUILD` (Findings F1) | major >= `MIN_MACOS_MAJOR` (F1) |
| CPU | `AMD64`; `ARM64` allowed with a warning that it runs under x64 emulation | `uname -m` is `arm64` or `x86_64`; a Rosetta shell on Apple Silicon (`sysctl -n hw.optional.arm64` = `1`) selects `osx-arm64` |
| Tools | PowerShell >= 5.1, `tar.exe` | `curl`, `tar`, `shasum` or `sha256sum`, `codesign`, `xattr` |
| Disk | >= 2048 MB free on the InstallRoot drive and the Cache drive (`MIN_FREE_MB`) | same, for `$HOME` |
| Write access | InstallRoot parent and DataRoot creatable | same |
| Network (skipped when `BaseUrl` is a folder) | HTTPS reach `github.com`, `nodejs.org`, `registry.npmjs.org`, 10 s each | same |

Not required: admin or sudo, .NET, Node, npm, git. `-CheckOnly` / `--check-only` runs this table, prints it, changes nothing (creates no directory), exits 0 or 2.

### Spec §6. Idempotency contract

State is `install-state.json` (Windows) / `install-state` with `key=value` lines (macOS) in InstallRoot. Keys: `schema`, `version`, `appSha256`, `nodeVersion`, `lockSha256` (macOS uses `app_sha256`, `node_version`, `lock_sha256`).

| Step | Skipped when |
| --- | --- |
| App | state `appSha256` equals this OS's archive line in `SHA256SUMS` **and** the app host exists |
| Node | state `nodeVersion` equals `release-info.json` `nodeVersion` **and** running `node --version` prints it |
| Adapter | state `lockSha256` equals the SHA-256 of `tools/acp/package-lock.json` **and** the adapter entry file exists |
| Launcher, shortcut / `.app`, PATH / symlink | Re-asserted every run; rewritten **only if the content differs** |
| Data | Never touched, except `-PurgeData` together with `-Uninstall` |

Rules (each has a test):
1. A second run with nothing new prints `[skip]` for every step, ends `Already up to date (vX.Y.Z).`, exits 0 and **changes no file** (a path+length+mtime snapshot of InstallRoot is identical).
2. A step writes its state key **last**, so an interrupted run resumes at the first unsatisfied step.
3. Work happens in a staging folder next to the target; the swap renames `InstallRoot` to `InstallRoot.old`, then staging to `InstallRoot`, deleting `.old` after; if the second rename fails the first is undone. A failed upgrade leaves the previous install runnable.
4. A running instance is stopped (`<host> stop`) **only if** app, node or adapter will change. The `stop` verb itself waits up to 45 s for the process to exit (many Persona adapters stop one after another); if it is still running after that the run fails with a clear message; `-Force`/`--force` ends the process.
5. Two installers at once: the second exits 1 immediately (Windows named mutex `Local\Agency.Huddle.Installer`; macOS `mkdir <cache>/install.lock`, removed on exit).
6. Uninstall with nothing installed prints `[skip]` and exits 0.
7. `-Uninstall` refuses to delete a folder that lacks the state file, is a drive/filesystem root, or has fewer than 3 path segments.

### Spec §7. Installer interface

| Windows | macOS | Meaning |
| --- | --- | --- |
| `-Version <tag>` (default `latest`) | `--version <tag>` | Release to install |
| `-BaseUrl <https url or existing folder>` | `--base-url <...>` | Where assets come from; a folder is used by tests and CI smoke jobs |
| `-InstallRoot <path>` / `-DataRoot <path>` | `--install-root` / `--data-root` | Overrides for Spec §3 |
| `-CheckOnly` | `--check-only` | Spec §5 only |
| `-Uninstall` [`-PurgeData`] | `--uninstall` [`--purge-data`] | Remove (data kept unless purged) |
| `-Force` | `--force` | Spec §6 rule 4 |
| `-NoLaunch` | `--no-launch` | Do not start the app at the end |

**Environment variables (found in review of task 17).** `irm | iex` cannot pass parameters, so every option also has an environment variable, used when the parameter is not given (an explicit parameter wins): `HUDDLE_VERSION`, `HUDDLE_BASE_URL`, `HUDDLE_INSTALL_ROOT`, `HUDDLE_DATA_ROOT`, and, switched on by any non-empty value other than `0`, `HUDDLE_CHECK_ONLY`, `HUDDLE_UNINSTALL`, `HUDDLE_PURGE_DATA`, `HUDDLE_FORCE`, `HUDDLE_NO_LAUNCH`. So an uninstall from the one-liner is `$env:HUDDLE_UNINSTALL='1'; irm <url> | iex`. A script run through `iex` (or as a scriptblock) **must never call `exit`**: that would close the user's PowerShell window. `install.ps1` calls `exit` only when it runs as a file (`$PSCommandPath` is set); otherwise it sets `$global:LASTEXITCODE` and returns. `install.sh` has no such problem under `curl | sh` but honours the same variables.

Output, one line per step: `[ok] name - detail`, `[skip] name - already satisfied`, `[..] name - working`, `[!!] name - detail` then `     fix: ...`. Exit codes: **0** success, up to date, or check passed; **1** a step failed; **2** prerequisites unmet; **3** bad usage. The script never prompts, except the optional sign-in step (Findings F3) when stdin is a terminal.
Final lines on success: `Agency Huddle <version> installed.` (or `Already up to date (<version>).`), then how to start it, then the Claude sign-in note.

### Spec §8. Desktop host mode (`src/Huddle.App/Desktop/`, namespace `Agency.Huddle.App.Desktop`)

Off unless `Team:Desktop:Enabled=true`, so tests and `run.ps1` are unaffected.

| Type | Contract |
| --- | --- |
| `DesktopOptions` | `public const string SectionName = "Team:Desktop"`; `bool Enabled { get; init; }` (false); `bool OpenBrowser { get; init; } = true` |
| `DesktopSession` | Singleton. `string StopToken { get; }`: 43-char base64url of `RandomNumberGenerator.GetBytes(32)`, new per instance |
| `InstanceLock` | `internal sealed class`, `IDisposable`. `internal static InstanceLock? TryAcquire(string dataDir)`: creates `dataDir`, opens `{dataDir}/instance.lock` with `FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None`; returns `null` on `IOException` (held by another process). The OS releases it when the process dies |
| `InstanceInfo` / `InstanceFile` | `internal sealed record InstanceInfo(string Url, string StopToken, int Pid)`. `InstanceFile`: `Write(dataDir, info)` (write `instance.json.tmp`, `File.Move(..., overwrite: true)`), `TryRead(dataDir)` -> `InstanceInfo?` (`null` if missing, empty, malformed or any field missing), `Delete(dataDir)` (idempotent). JSON camelCase |
| `IBrowserOpener` | `void Open(string url)`. `SystemBrowserOpener` implements it; `NoopBrowserOpener` does nothing. `SystemBrowserOpener.BuildStartInfo(Uri url, DesktopPlatform platform)`: Windows = `FileName=url, UseShellExecute=true`; macOS = `open <url>`; Linux = `xdg-open <url>`. Throws `ArgumentException` unless scheme is `http` and host is exactly `127.0.0.1` or `localhost` |
| `DesktopHandOff` | `internal static Task<HandOffResult> AcquireOrHandOffAsync(string dataDir, IBrowserOpener opener, TimeSpan timeout, TimeSpan pollInterval, CancellationToken ct)` with `internal sealed record HandOffResult(InstanceLock? Lock, bool Opened)`. Each poll first tries `InstanceLock.TryAcquire` (returns the lock: we are the owner, which also covers a relaunch while the old instance is shutting down), else `InstanceFile.TryRead` (then `opener.Open(info.Url)`, `Opened = true`), else waits until `timeout` (`Lock = null`, `Opened = false`). (The first version, `OpenExistingAsync`, was replaced after the final review.) |
| `DesktopLifetimeService` | `IHostedService`. On `ApplicationStarted`: first address of `IServerAddressesFeature` (Kestrel rewrites port 0 to the bound port); if none, log a warning and do nothing; else `InstanceFile.Write(dataDir, new InstanceInfo(url, session.StopToken, Environment.ProcessId))`, then `opener.Open(url)` only if `DesktopOptions.OpenBrowser`. On `StopAsync`: `InstanceFile.Delete(dataDir)` |
| Stop endpoint | `POST /_desktop/stop`, mapped only when enabled, `.DisableAntiforgery()`. 403 if `RemoteIpAddress` is non-null and not loopback; 401 unless header `X-Huddle-Token` equals `StopToken` (`CryptographicOperations.FixedTimeEquals` over UTF-8 bytes, length mismatch handled); else `IHostApplicationLifetime.StopApplication()` and 202. A null `RemoteIpAddress` is allowed (in-memory test server; Kestrel TCP always sets it) |
| `DesktopStopClient` | `internal static Task<int> RunAsync(string dataDir, HttpMessageHandler? handler, TextWriter output, TimeSpan stopTimeout, CancellationToken ct)`. **Lock first**: if `InstanceLock.TryAcquire` succeeds nobody owns the instance, so delete any stale `instance.json`, print `Agency Huddle is not running.`, return 0 (no HTTP call, so a stale record whose port was reused cannot make `stop` fail). If the lock is held but there is no record yet (the app is starting), poll for the record up to min(`stopTimeout`, 15 s), else print `Agency Huddle is starting or not answering.` and return 1. Connection refused with the lock held: same message, return 1. 401/403: print the reason, return 1. 2xx: wait up to `stopTimeout` for `InstanceLock.TryAcquire` to succeed (the owner is gone), print `Stopped.`, return 0; on timeout print `Did not stop within N s.`, return 1 |

Program wiring (task 14; `src/Huddle.App/Program.cs`, 102 lines, `CreateBuilder` L11, `AddTeamServices` L25, `Build` L35, `MapHealthChecks` L94, `Run` L99):
1. Before L11: `if (args is ["stop"])` return `await DesktopStopClient.RunAsync(<full path of env Team__DataDir, default App_Data>, null, Console.Out, 45 s, ...)`. Read the data dir from the **environment** (the launcher always sets it); do not pass `stop` to `CreateBuilder`.
2. After L11: bind `DesktopOptions`; `HandOffResult handOff = await DesktopHandOff.AcquireOrHandOffAsync(...)` when enabled (opener = `SystemBrowserOpener` if `OpenBrowser` else `NoopBrowserOpener`; timeout 15 s, poll 200 ms) and `using InstanceLock? instanceLock = handOff.Lock;`. If enabled and `handOff.Lock is null`: `return handOff.Opened ? 0 : 1`. If acquired: `InstanceFile.Delete(dataDir)` **immediately** (a crashed predecessor's stale file must not be handed off).
3. After L25: `builder.Services.AddDesktopHost(builder.Configuration);`. After L94: `app.MapDesktopEndpoints();`. After L99: `return 0;`.

### Spec §9. Release workflow (`.github/workflows/release.yaml`)

Trigger: push of tag `v*`, and `workflow_dispatch` with a `version` input. Jobs:
1. `build` (ubuntu-latest, matrix `rid` = `win-x64`, `osx-arm64`, `osx-x64`): checkout, `actions/setup-dotnet` with `global-json-file: global.json`, `installer/build-release.sh --version <v> --rid <rid> --out dist`, upload artifact `dist-<rid>`.
2. `assemble` (needs `build`): merge artifacts; copy `installer/install.ps1` and `install.sh` into `dist/` replacing `__HUDDLE_REPO__` with `${{ github.repository }}`; `installer/make-sums.sh dist <the five files>`; `actions/attest-build-provenance` over `dist/*` **only** `if: ${{ !github.event.repository.private }}`; upload artifact `release-dist`.
3. `smoke-windows` (windows-latest, needs `assemble`): `powershell.exe -NoProfile -ExecutionPolicy Bypass -File installer/tests/smoke.ps1 -ReleaseDir dist` (Windows PowerShell 5.1 on purpose: that is what users run).
4. `smoke-macos` (macos-latest, Apple Silicon, needs `assemble`): `sh installer/tests/smoke.sh dist`.
5. `publish` (needs both smokes): `gh release create "$TAG" dist/* --generate-notes`. The release exists **only after both smokes pass**.

Least privilege: workflow `permissions: {}`; `build`/`assemble` get `contents: read`; `assemble` adds `id-token: write` and `attestations: write`; `publish` alone gets `contents: write`. Every third-party action pinned to a **full commit SHA** with the tag in a trailing comment. The Intel build is not smoke-tested in CI (no free Intel runner): manual step M3.

### Spec §10. Verification

`installer/tests/smoke.ps1` and `smoke.sh` are the end-to-end tests (tasks 39, 40). They run against a real release folder built by `build-release.sh`, both in CI and locally. Unit tests (Pester, shell) cover every function by seam fakes.

---

## Execution plan

**Lanes** (after tasks 1-5, these are independent and can run in parallel):

| Lane | Tasks | Notes |
| --- | --- | --- |
| A. Findings | 1, 2, 3 | Task 2 feeds task 15; tasks 1-3 feed 18, 22, 27, 29, 33 and 38 (OS constants, Node URLs, sign-in step) |
| B. C# host mode | 6-14 | Sequential (shared folder, task 14 wires all) |
| C. Packaging | 15-16 | Needs task 2 (`node-version.txt`) |
| D. `install.ps1` | 17-27 | **One file: strictly sequential** |
| E. `install.sh` | 28-38 | **One file: strictly sequential** |
| F. Proof | 39-44 | Needs B, C, D, E |

**Model per risk tag:** `data` -> Haiku, `logic` and `boundary` -> Sonnet; only `boundary` deliverables get an Opus architect review. Haiku tasks are about a quarter of the plan: 1, 2, 4, 6, 9, 16, 20, 24, 31, 35, 43, 44.

**Retrospectives.** After every **15th completed task** (a `.t` and a `.i` each count as one) stop dispatching and run an Opus retrospective: at 15, 30, 45, 60 and 75 completed tasks.
- Give it the token tally (Execute-Project-Plan, Appendix D) and have it grep the logs of the three most expensive agents for repeated reads and searches. Do not hand it raw transcripts.
- It reports (1) what repeated cognitive load the upcoming tasks can be given up front, and (2) token cost: which agents grew their context most and why (whole-document orientation reads, agents kept past the cap, oversized reports, Opus used where Sonnet would do), with a fix for each.
- The manager then **edits this plan** (Conventions, `Read first`, task splits) and appends a row to the Retrospective log at the bottom.

**Manual steps (not delegated):** M1 run the release workflow on a throwaway public GitHub repo and use the one-liner against it. M2 a clean Windows 11 account. M3 an Apple Silicon Mac and, if one exists, an Intel Mac. M4 behind a corporate proxy. M5 real Claude sign-in plus one Turn (paid).

---

## Deliverable A. Findings (research, no TDD)

### Task 1 — Findings F1: minimum OS versions

- **Goal:** Record the minimum Windows build and macOS major that .NET 10 supports, so `MIN_WINDOWS_BUILD` and `MIN_MACOS_MAJOR` in Spec §5 are facts, not guesses.
- **Read first:** `https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md` (fetch it); this plan, "Findings" appendix at the bottom.
- **Risk:** `data` (Haiku).
- **Deliverable:** Fill **Appendix F1** with: the macOS versions supported for `osx-arm64` and `osx-x64`; the Windows 10/11 versions supported for x64 and ARM64; the resulting constants as `MIN_MACOS_MAJOR=<n>` and `MIN_WINDOWS_BUILD=<n>` (Windows 10 1809 is build 17763); the source URL and the date you read it. If the page does not say, write `UNKNOWN` and what you tried; do not guess.
- **Acceptance:** Appendix F1 has both constants (or `UNKNOWN` with reasons) and a source URL.

### Task 2 — Findings F2: pin Node and confirm download names

- **Goal:** Choose the Node version the installers fetch (Spec §2, §5) and create `installer/node-version.txt`.
- **Read first:** `tools/acp/node_modules/@agentclientprotocol/claude-agent-acp/package.json` (`engines`), `tools/acp/node_modules/@anthropic-ai/claude-agent-sdk/package.json` (`engines`), `https://nodejs.org/dist/index.json`.
- **Risk:** `data` (Haiku).
- **Deliverable:** Pick the newest **LTS** release satisfying both `engines` ranges. Confirm these exist for it and record the exact URLs in **Appendix F2**: `https://nodejs.org/dist/<v>/node-<v>-win-x64.zip`, `node-<v>-darwin-arm64.tar.gz`, `node-<v>-darwin-x64.tar.gz`, `SHASUMS256.txt`. Record where `npm-cli.js` sits inside each archive (needed to run `npm ci` without a global npm). Create `installer/node-version.txt` containing only the version, e.g. `v24.13.1`.
- **Acceptance:** `installer/node-version.txt` exists with one line; Appendix F2 lists the four verified URLs and the `npm-cli.js` relative paths for the Windows zip and a macOS tarball.

### Task 3 — Findings F3: detecting and performing the Claude sign-in

- **Goal:** Learn how to tell whether the bundled Claude binary is signed in and how to start sign-in, so tasks 27 and 38 implement the last installer step exactly.
- **Read first:** `tools/acp/node_modules/@anthropic-ai/claude-agent-sdk-win32-x64/claude.exe` (run only `--help` and `<sub> --help`), `https://code.claude.com/docs` (authentication pages).
- **Risk:** `logic` (Sonnet).
- **Deliverable:** In **Appendix F3** record, with at most 15 lines of verbatim output per item: (1) the command that reports signed-in state and its exit codes, if one exists; (2) the command that starts interactive sign-in and whether it opens a browser; (3) where credentials are stored on Windows and on macOS (file or Keychain) and any environment-variable alternative; (4) whether the adapter can be started without credentials. **Do not sign in, send a prompt, or print any token or credential file's contents** (Conv C7). If detection is impossible, write `NO-DETECTION` and the instruction text the installer should print instead.
- **Acceptance:** Appendix F3 answers all four questions or states `UNKNOWN` with what was tried.

---

## Deliverable B. Repo plumbing (shared root files, Conv C6)

### Task 4.t — Test: `.gitattributes` and `.gitignore` rules

- **Goal:** Pin the line-ending and ignore rules the installer files depend on (Spec §3, Conv C3).
- **Read first:** `.gitignore` (whole, ~30 lines); Conv C3.
- **Risk:** `data` (Haiku).
- **Deliverable:** Create `installer/tests/Plumbing.Tests.ps1` (Pester 5, Conv C2). Test `Describe '.gitattributes'`: `git check-attr eol -- installer/install.sh` and `installer/build-release.sh` and `installer/tests/lib.sh` each report `eol: lf`; `git check-attr eol -- installer/install.ps1` does **not** report `lf`. Test `Describe '.gitignore'`: `git check-ignore dist/x` and `git check-ignore artifacts/x` exit 0. Run via `Push-Location` to the repo root (`git rev-parse --show-toplevel`).
- **Acceptance:** `Invoke-Pester installer/tests/Plumbing.Tests.ps1 -CI` runs and the three groups fail (Red).

### Task 4.i — Add `.gitattributes` and ignore entries

- **Goal:** Make task 4.t green.
- **Read first:** `.gitignore`; task 4.t.
- **Risk:** `data` (Haiku).
- **Deliverable:** Create `.gitattributes` (CRLF endings) with exactly: `*.sh text eol=lf` and `installer/tests/*.sh text eol=lf`. Append `/dist/` and `/artifacts/` to `.gitignore` (keep its line ending style).
- **Acceptance:** Task 4.t passes. `git status` shows only these two files plus the test.

### Task 5.t — Test: `Check-Eol.ps1` treats `.sh` as LF-only

- **Goal:** Pin the new rule before changing the checker (Conv C1 line-ending note).
- **Read first:** `agents/scripts/Check-Eol.ps1` (whole, ~120 lines); `agents/scripts/Fix-Crlf.ps1` L1-L20.
- **Risk:** `logic` (Sonnet).
- **Deliverable:** Add `Describe 'Check-Eol'` to `installer/tests/Plumbing.Tests.ps1`. Each case builds a **temp git repo** (`git init`, set user.name/email locally), writes files with exact bytes, runs `agents/scripts/Check-Eol.ps1` as a child `pwsh -NoProfile -File` with the temp repo as working directory, and asserts the exit code: (a) `a.sh` with LF only -> 0; (b) `a.sh` with CRLF -> non-zero and output names `a.sh`; (c) `a.cs` with LF only -> non-zero (existing behaviour kept); (d) `a.cs` CRLF -> 0; (e) `-Fix` on case (b) rewrites `a.sh` to LF (bytes verified) and does not add `\r` to it.
- **Acceptance:** Cases (b) and (e) fail, (a),(c),(d) pass today (Red for the new behaviour only).

### Task 5.i — `Check-Eol.ps1`: shell scripts must be LF

- **Goal:** Make task 5.t green without changing behaviour for any other extension.
- **Read first:** `agents/scripts/Check-Eol.ps1` L21-L120; task 5.t.
- **Risk:** `logic` (Sonnet).
- **Deliverable:** In `Check-Eol.ps1`, for a path ending `.sh` invert the test: a `\r` byte is the violation; report it as `contains CRLF (shell scripts must be LF)`. With `-Fix`, rewrite such a file with `\r\n` -> `\n` (UTF-8, no BOM added) **inside this script** (do not route `.sh` through `Fix-Crlf.ps1`). Update the script's `.DESCRIPTION` to state the exception. Keep the file CRLF.
- **Acceptance:** Task 5.t passes; `pwsh agents/scripts/Check-Eol.ps1` on the real working tree is not worse than before.

---

## Deliverable C. Desktop host mode in `Huddle.App` (Spec §8)

All tasks in this deliverable follow Conv C1. Folder `src/Huddle.App/Desktop/`; tests `tests/Huddle.Tests/Desktop/`.

### Task 6.t — Test: `DesktopOptions` and `DesktopSession`

- **Goal:** Pin the options defaults and the stop-token shape (Spec §8 rows `DesktopOptions`, `DesktopSession`).
- **Read first:** `src/Huddle.App/Logging/FileLogOptions.cs` L1-L35 (the binding pattern to copy); Spec §8.
- **Risk:** `data` (Haiku).
- **Deliverable:** `tests/Huddle.Tests/Desktop/DesktopOptionsTests.cs`: (1) empty config binds to `Enabled=false`, `OpenBrowser=true`; (2) in-memory config `Team:Desktop:Enabled=true`, `Team:Desktop:OpenBrowser=false` binds both; (3) `SectionName == "Team:Desktop"`; (4) `new DesktopSession().StopToken` matches `^[A-Za-z0-9_-]{43}$`; (5) two sessions have different tokens.
- **Acceptance:** Fails to build before 6.i, because the types do not exist yet (Red).

### Task 6.i — Implement `DesktopOptions` and `DesktopSession`

- **Goal:** Make task 6.t green.
- **Read first:** Task 6.t; `FileLogOptions.cs` L1-L35; Spec §8.
- **Risk:** `data` (Haiku).
- **Deliverable:** `src/Huddle.App/Desktop/DesktopOptions.cs` and `DesktopSession.cs` exactly as the Spec §8 table. Both `public sealed`. Token: `Convert.ToBase64String(...).TrimEnd('=').Replace('+','-').Replace('/','_')`.
- **Acceptance:** `dotnet build Huddle.slnx` clean; `dotnet test Huddle.slnx --` passes the new tests.

### Task 7.t — Test: `InstanceLock`

- **Goal:** Pin single-instance behaviour (Spec §8 `InstanceLock`).
- **Read first:** Spec §8; `tests/Huddle.Tests/TempDataDir.cs` (whole).
- **Risk:** `boundary` (Sonnet).
- **Deliverable:** `InstanceLockTests.cs`: (1) first `TryAcquire(dir)` non-null and `{dir}/instance.lock` exists; (2) a second `TryAcquire(dir)` while the first is held returns `null`; (3) after the first is disposed a third succeeds; (4) different directories are independent; (5) a missing directory is created; (6) disposing twice does not throw.
- **Acceptance:** Fails to build before 7.i (Red).

### Task 7.i — Implement `InstanceLock`

- **Goal:** Make task 7.t green.
- **Read first:** Task 7.t; Spec §8.
- **Risk:** `boundary` (Sonnet; Opus review).
- **Deliverable:** `src/Huddle.App/Desktop/InstanceLock.cs`, `internal sealed`, `IDisposable`, holds the `FileStream`. `TryAcquire` catches `IOException` only. Do not delete the lock file on dispose (deleting races a new owner). XML doc must state: .NET on Unix implements `FileShare.None` with `flock`, so this works across processes on macOS.
- **Acceptance:** `dotnet build Huddle.slnx`; `dotnet test Huddle.slnx --` passes 7.t.

### Task 8.t — Test: `InstanceFile`

- **Goal:** Pin the instance record's persistence, including corruption and torn writes (Spec §8).
- **Read first:** Spec §8; Conv C1.
- **Risk:** `boundary` (Sonnet).
- **Deliverable:** `InstanceFileTests.cs`: (1) `Write` then `TryRead` round-trips `Url`, `StopToken`, `Pid`; (2) file is camelCase JSON with keys `url`, `stopToken`, `pid`; (3) `TryRead` returns `null` for: no file, empty file, `{`, `{"url":"x"}` (field missing), `[]`; (4) `Delete` removes it and a second `Delete` does not throw; (5) overwrite replaces the previous record; (6) 300 iterations of `Write` on one task while another task `TryRead`s in a loop: every read is `null` or a complete record, never throws.
- **Acceptance:** Fails to build before 8.i (Red).

### Task 8.i — Implement `InstanceInfo` and `InstanceFile`

- **Goal:** Make task 8.t green.
- **Read first:** Task 8.t; Spec §8.
- **Risk:** `boundary` (Sonnet; Opus review).
- **Deliverable:** `src/Huddle.App/Desktop/InstanceFile.cs` containing `internal sealed record InstanceInfo(string Url, string StopToken, int Pid)` and `internal static class InstanceFile`. `JsonSerializerOptions` in one `private static readonly` field (`JsonSerializerDefaults.Web`). `Write` creates the directory, writes `instance.json.tmp`, then `File.Move(tmp, final, overwrite: true)`. `TryRead` catches `JsonException`, `IOException`, `UnauthorizedAccessException` and returns `null`; rejects empty strings.
- **Acceptance:** Build clean; 8.t green.

### Task 9.t — Test: browser opener

- **Goal:** Pin which commands open the browser per OS and that arbitrary URLs are refused (Spec §8 `IBrowserOpener`).
- **Read first:** Spec §8.
- **Risk:** `data` (Haiku).
- **Deliverable:** `BrowserOpenerTests.cs`: `BuildStartInfo(new Uri("http://127.0.0.1:5123/"), DesktopPlatform.Windows)` has `FileName == "http://127.0.0.1:5123/"` and `UseShellExecute`; `MacOS` gives `FileName "open"` with `ArgumentList` = the URL; `Linux` gives `xdg-open`. `[Theory]` of rejected URLs throwing `ArgumentException`: `https://127.0.0.1/`, `http://evil.example/`, `http://127.0.0.1.evil.example/`, `file:///C:/Windows/notepad.exe`, `http://localhost.evil.example/`. `http://localhost:8080/` is accepted. `NoopBrowserOpener.Open` does not throw.
- **Acceptance:** Fails to build before 9.i (Red).

### Task 9.i — Implement `IBrowserOpener` and implementations

- **Goal:** Make task 9.t green.
- **Read first:** Task 9.t; Spec §8.
- **Risk:** `data` (Haiku).
- **Deliverable:** `src/Huddle.App/Desktop/BrowserOpener.cs`: `public enum DesktopPlatform { Windows, MacOS, Linux }`, `internal interface IBrowserOpener`, `internal sealed class SystemBrowserOpener` (`Open` picks the platform from `OperatingSystem.IsWindows()/IsMacOS()`, calls `Process.Start(BuildStartInfo(...))`, catches `Win32Exception` and ignores it: the instance is already running and its URL is in `instance.json`), `internal sealed class NoopBrowserOpener`.
- **Acceptance:** Build clean; 9.t green.

### Task 10.t — Test: `DesktopHandOff`

- **Goal:** Pin the second-launch behaviour (Spec §8 `DesktopHandOff`).
- **Read first:** Spec §8; `InstanceFile` from task 8.i.
- **Risk:** `logic` (Sonnet).
- **Deliverable:** `DesktopHandOffTests.cs` with a recording `IBrowserOpener` fake in its own file `tests/Huddle.Tests/Desktop/RecordingBrowserOpener.cs` (task 11.t reuses it): (1) record present -> `true`, opener called once with its `Url`; (2) no record, timeout 300 ms, poll 20 ms -> `false`, opener never called; (3) the record appears after 100 ms (written by a delayed task) -> `true`; (4) a cancelled token throws `OperationCanceledException`.
- **Acceptance:** Fails to build before 10.i (Red).

### Task 10.i — Implement `DesktopHandOff`

- **Goal:** Make task 10.t green.
- **Read first:** Task 10.t; Spec §8.
- **Risk:** `logic` (Sonnet).
- **Deliverable:** `src/Huddle.App/Desktop/DesktopHandOff.cs` per Spec §8. Use `Task.Delay(pollInterval, ct)` between polls and a `Stopwatch.StartNew()` for the timeout.
- **Acceptance:** Build clean; 10.t green.

### Task 11.t — Test: `DesktopLifetimeService`

- **Goal:** Pin what a running instance publishes and cleans up (Spec §8 `DesktopLifetimeService`).
- **Read first:** Spec §8; Conv C1 (`ManualTimeProvider` not needed).
- **Risk:** `logic` (Sonnet).
- **Deliverable:** `DesktopLifetimeServiceTests.cs` with fakes: `FakeServer : IServer` whose `Features` holds an `IServerAddressesFeature` with chosen addresses, `FakeLifetime : IHostApplicationLifetime` in its own file `tests/Huddle.Tests/Desktop/FakeLifetime.cs` (three `CancellationTokenSource`s, so it is `IDisposable`), and the `RecordingBrowserOpener` from task 10.t. Cases: (1) after `StartAsync` then firing `ApplicationStarted`, `InstanceFile.TryRead` returns `Url` = first address, `StopToken` = `session.StopToken`, `Pid == Environment.ProcessId`; (2) `OpenBrowser=true` -> opener called once with that URL; (3) `OpenBrowser=false` -> not called, file still written; (4) no addresses -> no file, no open; (5) `StopAsync` deletes the file.
- **Acceptance:** Fails to build before 11.i (Red).

### Task 11.i — Implement `DesktopLifetimeService`

- **Goal:** Make task 11.t green.
- **Read first:** Task 11.t; Spec §8; `src/Huddle.App/TeamOptions.cs` L9-L13 (`DataDir`).
- **Risk:** `logic` (Sonnet).
- **Deliverable:** `src/Huddle.App/Desktop/DesktopLifetimeService.cs`: `internal sealed class DesktopLifetimeService(IServer server, IHostApplicationLifetime lifetime, IOptions<DesktopOptions> options, IOptions<TeamOptions> team, DesktopSession session, IBrowserOpener opener, ILogger<DesktopLifetimeService> logger) : IHostedService`. Register the callback in `StartAsync`; do not block startup. The data directory is `team.Value.DataDir` (already absolute after `PostConfigure`, `ServiceCollectionExtensions.cs` L69-L73).
- **Acceptance:** Build clean; 11.t green.

### Task 12.t — Test: stop endpoint

- **Goal:** Pin the stop endpoint's authentication (Spec §8 "Stop endpoint").
- **Read first:** Spec §8.
- **Risk:** `boundary` (Sonnet).
- **Deliverable:** `DesktopStopEndpointTests.cs` calling `DesktopStopEndpoint.HandleAsync(HttpContext, DesktopSession, IHostApplicationLifetime)` with a `DefaultHttpContext` and a recording `FakeDesktopShutdown : IDesktopShutdown` in its own file (see `corrections-C.md`, section 12; `FakeLifetime` is not used here). Cases: correct token + loopback -> 202 and `StopApplication` called once; wrong token -> 401, not called; no header -> 401; correct token + `RemoteIpAddress` `192.0.2.7` -> 403, not called; correct token + null remote -> 202; token that is a prefix of the real one -> 401; empty header value -> 401.
- **Acceptance:** Fails to build before 12.i (Red).

### Task 12.i — Implement the stop endpoint

- **Goal:** Make task 12.t green.
- **Read first:** Task 12.t; Spec §8.
- **Risk:** `boundary` (Sonnet; Opus review: security).
- **Deliverable:** `src/Huddle.App/Desktop/DesktopStopEndpoint.cs`: `internal static class DesktopStopEndpoint` with `internal static Task<IResult> HandleAsync(...)` (or `IResult` if synchronous). Compare with `CryptographicOperations.FixedTimeEquals` over the SHA-256 hashes of the two tokens (equal-length inputs, so no length-dependent early exit). Never log the token.
- **Acceptance:** Build clean; 12.t green.

### Task 13.t — Test: `DesktopStopClient`

- **Goal:** Pin the `stop` verb's outcomes (Spec §8 `DesktopStopClient`).
- **Read first:** Spec §8; `InstanceLock` and `InstanceFile` (tasks 7.i, 8.i).
- **Risk:** `boundary` (Sonnet).
- **Deliverable:** `DesktopStopClientTests.cs` with a fake `HttpMessageHandler` recording requests. Cases: no `instance.json` -> prints `Agency Huddle is not running.`, returns 0, no request sent; record present, handler returns 202, and the test releases a held `InstanceLock` 150 ms later -> request was `POST {url}/_desktop/stop` with header `X-Huddle-Token: <token>`, output contains `Stopped.`, returns 0; handler 202 but lock never released, `stopTimeout` 300 ms -> returns 1 and prints the timeout; handler throws `HttpRequestException` -> stale file deleted, returns 0; handler 401 -> returns 1.
- **Acceptance:** Fails to build before 13.i (Red).

### Task 13.i — Implement `DesktopStopClient`

- **Goal:** Make task 13.t green.
- **Read first:** Task 13.t; Spec §8.
- **Risk:** `boundary` (Sonnet; Opus review).
- **Deliverable:** `src/Huddle.App/Desktop/DesktopStopClient.cs`. Create the `HttpClient` from `handler ?? new SocketsHttpHandler()` with a 5 s request timeout. Poll the lock every 100 ms until `stopTimeout`.
- **Acceptance:** Build clean; 13.t green.

### Task 14.t — Test: wiring through the real host

- **Goal:** Pin that desktop mode is invisible when off and works end to end in-process when on (Spec §8 "Program wiring").
- **Read first:** `tests/Huddle.Tests/Ui/TeamWebApplicationFactory.cs` L18-L30 and L107-L140 (`ConfigureWebHost`; how settings are injected); Spec §8.
- **Risk:** `boundary` (Sonnet).
- **Deliverable:** `DesktopWiringTests.cs` using `TeamWebApplicationFactory` and `factory.WithWebHostBuilder(b => b.UseSetting("Team:Desktop:Enabled", "true").UseSetting("Team:Desktop:OpenBrowser", "false"))`. Cases: (1) default host: `POST /_desktop/stop` -> 404 and no `instance.json` exists in `factory.DataDirPath`; (2) enabled: `POST /_desktop/stop` with no token -> 401; with a wrong token -> 401; with `X-Huddle-Token` = `Services.GetRequiredService<DesktopSession>().StopToken` -> 202 and `IHostApplicationLifetime.ApplicationStopping.IsCancellationRequested` becomes true; (3) enabled: `GET /_desktop/stop` -> 405; (4) enabled: `GET /health` still 200.
- **Acceptance:** Fails before 14.i (Red).

### Task 14.i — Wire desktop mode into `Program.cs`

- **Goal:** Make task 14.t green and implement Spec §8 "Program wiring" steps 1-3.
- **Read first:** `src/Huddle.App/Program.cs` L9-L12, L23-L36, L92-L102; Spec §8 (all of "Program wiring"); `src/Huddle.App/Logging/LoggingBuilderExtensions.cs` L30-L40 (how DataDir is resolved before the container exists).
- **Risk:** `boundary` (Sonnet; Opus review).
- **Deliverable:** Create `src/Huddle.App/Desktop/DesktopServiceCollectionExtensions.cs`: `AddDesktopHost(this IServiceCollection, IConfiguration)` registers `Configure<DesktopOptions>`, singleton `DesktopSession`, `IBrowserOpener` -> `SystemBrowserOpener`, and `AddHostedService<DesktopLifetimeService>()` **only when enabled**; and `MapDesktopEndpoints(this WebApplication)` mapping `POST /_desktop/stop` **only when enabled**. Edit `Program.cs` per Spec §8 steps 1-3 (resolve the data dir with `Path.GetFullPath(configuration["Team:DataDir"] ?? "App_Data")`, the same rule `PostConfigure` applies). Keep `public partial class Program`.
- **Acceptance:** `agents/scripts/Build.ps1` clean and the full `agents/scripts/Run-Tests.ps1` run green (no regression in the existing suite); `./test-health.ps1 -Configuration Debug` still passes.

---

## Deliverable D. Release packaging (Spec §2, §9)

### Task 15.t — Test: archive staging

- **Goal:** Pin the archive layout the installers depend on (Spec §2, §3).
- **Read first:** Spec §2; Conv C3; `tools/acp/package.json` (whole).
- **Risk:** `logic` (Sonnet).
- **Deliverable:** `installer/tests/build_release_test.sh`. The shared `lib.sh` does not exist yet (task 28.t), so use plain `[ ... ] || { echo FAIL; exit 1; }` checks, as `make_sums_test.sh` in task 16.t also does. Build a fake publish dir (`Huddle.App.exe`, `Huddle.App.dll`, `wwwroot/x.txt`) in a temp dir and call `installer/build-release.sh --version 1.2.3 --rid win-x64 --publish-dir <fake> --out <out>`. Assert: `<out>/huddle-win-x64.zip` exists; it lists `app/Huddle.App.exe`, `tools/acp/package.json`, `tools/acp/package-lock.json`, `release-info.json`; `release-info.json` contains `"version":"1.2.3"`, `"rid":"win-x64"` and `"nodeVersion":"<contents of installer/node-version.txt, trimmed>"`. Repeat for `--rid osx-arm64`: `<out>/huddle-osx-arm64.tar.gz` with `app/` etc. Bad `--rid linux-x64` exits non-zero. Missing `--out` exits non-zero.
- **Acceptance:** Fails because the script does not exist (Red).

### Task 15.i — `installer/build-release.sh`

- **Goal:** Make task 15.t green and produce real archives.
- **Read first:** Task 15.t; Spec §2 and §9 job `build`; Conv C3; `src/Huddle.App/Huddle.App.csproj` (whole, 32 lines).
- **Risk:** `logic` (Sonnet).
- **Deliverable:** `installer/build-release.sh` (LF) with `--version`, `--rid` (`win-x64|osx-arm64|osx-x64` only), `--out`, optional `--publish-dir` (skip publishing; the test hook). Without it run `dotnet publish src/Huddle.App/Huddle.App.csproj -c Release -r <rid> --self-contained true -p:PublishReadyToRun=true -p:DebugType=none -p:DebugSymbols=false -o <tmp>/app`. **Do not** trim or AOT (decision D2). If `PublishReadyToRun` fails when cross-publishing from Linux, drop only that property and write the reason in a comment. Stage `<tmp>/stage/{app,tools/acp/{package.json,package-lock.json},release-info.json}`; archive: `zip -qr` for `win-x64` (error with a clear `fix:` if `zip` is missing), `tar -czf ... -C <stage> .` for the others. Repo root is `$(cd "$(dirname "$0")/.." && pwd)`. Clean temp on exit with `trap`.
- **Acceptance:** 15.t passes; `sh installer/build-release.sh --version 0.0.0-local --rid win-x64 --out artifacts` (a real publish) produces a zip whose `app/Huddle.App.exe` exists.

### Task 16.t — Test: `make-sums.sh`

- **Goal:** Pin the `SHA256SUMS` format (Spec §2).
- **Read first:** Spec §2; Conv C3.
- **Risk:** `data` (Haiku).
- **Deliverable:** `installer/tests/make_sums_test.sh`: a dir with `a.txt` containing exactly `abc` (no newline) and `b.txt` empty. `installer/make-sums.sh <dir> a.txt b.txt` writes `<dir>/SHA256SUMS` with exactly the lines `ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad  a.txt` and `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855  b.txt` (two spaces, sorted by name, LF). A missing file argument exits non-zero and writes no `SHA256SUMS`.
- **Acceptance:** Fails because the script does not exist (Red).

### Task 16.i — `installer/make-sums.sh`

- **Goal:** Make task 16.t green.
- **Read first:** Task 16.t; Conv C3.
- **Risk:** `data` (Haiku).
- **Deliverable:** `installer/make-sums.sh <dir> <file>...`: hash with `sha256sum` or `shasum -a 256`, write `<dir>/SHA256SUMS` atomically (temp file then `mv`).
- **Acceptance:** 16.t passes; `shellcheck -s sh installer/make-sums.sh` clean.

---

## Deliverable E. `installer/install.ps1` (Windows)

All tasks edit the **one file** `installer/install.ps1` and its test file `installer/tests/Install.Tests.ps1`. Run them in order. Conv C2 applies to every task. Function names below are fixed; later tasks call them.

### Task 17.t — Test: skeleton, paths, asset source

- **Goal:** Pin parameters, exit codes, step output and path resolution (Spec §3, §7).
- **Read first:** Spec §3, §7; Conv C2.
- **Risk:** `logic` (Sonnet).
- **Deliverable:** `installer/tests/Install.Tests.ps1`, `BeforeAll { $env:HUDDLE_INSTALL_NO_MAIN='1'; . "$PSScriptRoot/../install.ps1" }`. Cases: (1) the file parses with **Windows PowerShell 5.1** (`powershell.exe -NoProfile -Command "[void][System.Management.Automation.Language.Parser]::ParseFile(...)"`, assert zero errors; skip on non-Windows); (2) file is pure ASCII; (3) `Resolve-InstallPaths` with default env returns `InstallRoot` = `$env:LOCALAPPDATA\Programs\Agency.Huddle`, `DataRoot` = `$env:LOCALAPPDATA\Agency.Huddle`, `ExePath` ending `app\Huddle.App.exe`, `NodeExe` ending `node\node.exe`, `AdapterEntry` ending `tools\acp\node_modules\@agentclientprotocol\claude-agent-acp\dist\index.js`, `StatePath` ending `install-state.json`, `LauncherPath` ending `huddle.cmd`, `Rid` `win-x64`; with `-InstallRoot`/`-DataRoot` overrides they are honoured; (4) `Resolve-AssetSource -Name x -BaseUrl 'C:\somedir' ` (existing dir) -> Kind `file`; an `https://` BaseUrl -> Kind `url` with `Location` `https://.../x`; no BaseUrl and `-Version latest` -> `https://github.com/<repo>/releases/latest/download/x`; `-Version v1.2.3` -> `.../releases/download/v1.2.3/x`; (5) `Write-Step` formats `[ok] name - detail`, `[skip] ...`, `[!!] ...` with a `     fix: ...` line; (6) importing the script with the env var set runs nothing (no output, no files).
- **Acceptance:** Fails because `install.ps1` does not exist (Red).

### Task 17.i — `install.ps1` skeleton

- **Goal:** Make task 17.t green.
- **Read first:** Task 17.t; Spec §3, §7; Conv C2.
- **Risk:** `logic` (Sonnet).
- **Deliverable:** Create `installer/install.ps1` (ASCII, CRLF): `[CmdletBinding()] param(...)` with the Spec §7 parameters; strict mode and `Stop`; `$script:HuddleRepo = if ($env:HUDDLE_REPO) { $env:HUDDLE_REPO } else { '__HUDDLE_REPO__' }`; `$script:ExitCodes`; `Write-Step -Status -Name -Detail -Fix`; `Resolve-InstallPaths`; `Resolve-AssetSource`; a stub `Invoke-Main` that returns 0; the final guard line from Conv C2. When the repo placeholder is unreplaced and no `-BaseUrl` is given, `Resolve-AssetSource` throws a message saying to run the installer from a release.
- **Acceptance:** 17.t passes; `Invoke-ScriptAnalyzer installer/install.ps1` has no errors.

### Task 18.t — Test: prerequisite checks

- **Goal:** Pin Spec §5 for Windows.
- **Read first:** Spec §5; the `Resolve-InstallPaths` shape from 17.i.
- **Risk:** `logic` (Sonnet).
- **Deliverable:** Cases mocking `Get-OsBuild`, `Get-HostArchitecture`, `Test-CommandAvailable`, `Get-FreeDiskMb`, `Test-UrlReachable`: all-good returns items all `Ok=$true`; build 17000 -> `Ok=$false` for `OS` with a `Fix`; arch `ARM64` -> `Ok=$true` with `Detail` containing `emulation`; arch `x86` -> `Ok=$false`; `tar` missing -> fails; free disk 500 -> fails; `Test-UrlReachable` false for `nodejs.org` -> that item fails and the others still run (all failures reported); `BaseUrl` is a folder -> no network probes made (`Should -Invoke Test-UrlReachable -Times 0`); `Show-Prerequisites` prints one line per item; `-CheckOnly` path returns exit code 0 when all ok, 2 otherwise, and creates no directory (assert `InstallRoot` does not exist).
- **Acceptance:** Red until 18.i.

### Task 18.i — `Test-Prerequisites`

- **Goal:** Make task 18.t green.
- **Read first:** Task 18.t; Spec §5; Findings F1 constants.
- **Risk:** `logic` (Sonnet).
- **Deliverable:** Seams `Get-OsBuild` (`[Environment]::OSVersion.Version.Build`), `Get-HostArchitecture` (`$env:PROCESSOR_ARCHITEW6432` else `$env:PROCESSOR_ARCHITECTURE`), `Test-CommandAvailable`, `Get-FreeDiskMb -Path` (`Get-PSDrive`/`DriveInfo` of the nearest existing ancestor), `Test-UrlReachable -Url -TimeoutSec 10`; `Test-Prerequisites -Paths -BaseUrl` returning `[pscustomobject]@{Name;Ok;Detail;Fix}` items; `Show-Prerequisites`; `$script:MinWindowsBuild`/`$script:MinFreeMb = 2048` constants from F1. `Invoke-Main` honours `-CheckOnly` (print table, exit 0/2) and `Write-Step` is used for output.
- **Acceptance:** 18.t green; running the file with `-CheckOnly -BaseUrl <folder>` on this machine prints a sensible table.

### Task 19.t — Test: verified asset download

- **Goal:** Pin hash verification, caching and retry (Spec §2, §6).
- **Read first:** Spec §2, §6; Conv C2.
- **Risk:** `boundary` (Sonnet).
- **Deliverable:** Cases in a temp cache dir with `Invoke-Download` mocked to copy a fixture: `Read-Sums` parses a two-line `SHA256SUMS` (two-space separator, CRLF tolerated, blank lines ignored) into a hashtable; `Get-ExpectedHash` returns the hash or throws `'<name>' is not listed in SHA256SUMS`; `Get-VerifiedAsset` returns the cache path when the hash matches; **mismatch** -> throws, deletes the bad file, and the message contains both hashes; **already cached with correct hash** -> `Invoke-Download` not called (`Should -Invoke -Times 0`); **cached but wrong hash** -> re-downloads; transient failure twice then success -> 3 calls, succeeds; failing 3 times -> throws; `file` Kind sources are copied, not downloaded.
- **Acceptance:** Red until 19.i.

### Task 19.i — `Get-VerifiedAsset` and sums helpers

- **Goal:** Make task 19.t green.
- **Read first:** Task 19.t; Spec §2.
- **Risk:** `boundary` (Sonnet; Opus review).
- **Deliverable:** `Read-Sums -Text`, `Get-ExpectedHash -Sums -Name`, `Get-FileSha256 -Path` (lower-case hex via `Get-FileHash -Algorithm SHA256`), `Invoke-Download -Url -OutFile` (seam; `Invoke-WebRequest -UseBasicParsing`, `$ProgressPreference='SilentlyContinue'` because progress rendering makes 5.1 downloads very slow, TLS 1.2 forced on 5.1), `Get-VerifiedAsset -Name -ExpectedSha256 -Source -CacheDir` with 3 attempts and 1 s / 3 s backoff. Download to `<name>.partial`, verify, then rename to `<name>` (never leave an unverified file under the final name).
- **Acceptance:** 19.t green.

### Task 20.t — Test: install state

- **Goal:** Pin the idempotency state file (Spec §6).
- **Read first:** Spec §6.
- **Risk:** `data` (Haiku).
- **Deliverable:** Cases: `Read-InstallState -Path` returns `$null` for a missing, empty or malformed file; round-trip `Write-InstallState` of `version, appSha256, nodeVersion, lockSha256` with `schema = '1'`; a write leaves no `.tmp` file; `Test-StepSatisfied -State -Key appSha256 -Expected X` is true only on exact (case-insensitive) match and false when state is `$null`.
- **Acceptance:** Red until 20.i.

### Task 20.i — `Read-InstallState` / `Write-InstallState` / `Test-StepSatisfied`

- **Goal:** Make task 20.t green.
- **Read first:** Task 20.t; Spec §6.
- **Risk:** `data` (Haiku).
- **Deliverable:** The three functions. JSON via `ConvertTo-Json`/`ConvertFrom-Json`; write to `<path>.tmp` then `Move-Item -Force`. ASCII output.
- **Acceptance:** 20.t green.

### Task 21.t — Test: stop and install the app

- **Goal:** Pin the safe swap and the stop-before-replace rule (Spec §6 rules 3, 4).
- **Read first:** Spec §3, §6; the shape of `Resolve-InstallPaths`.
- **Risk:** `boundary` (Sonnet).
- **Deliverable:** Fixtures: a zip built in the test with `app\Huddle.App.exe` (dummy text), `tools\acp\package.json`, `tools\acp\package-lock.json`, `release-info.json`. `Invoke-Tar` mocked to really extract (use `Expand-Archive` inside the mock). Cases: fresh install -> `ExePath` exists, `release-info.json` present, no `.staging` / `.old` left; app step **skipped** when state `appSha256` matches and the exe exists; upgrade with an existing install: old `node\` and `tools\acp\node_modules\marker.txt` survive into the new tree, old `app\` replaced; archive missing `app\Huddle.App.exe` -> throws and the previous install is untouched; rename of the staging folder failing (mock `Move-Item` to throw on the second move) -> previous install restored; `Stop-HuddleIfRunning` calls `Invoke-HuddleStop` only when an instance record exists, and with `-Force` ends a process that survives; a run where nothing will change never calls `Invoke-HuddleStop`.
- **Acceptance:** Red until 21.i.

### Task 21.i — `Stop-HuddleIfRunning` and `Install-App`

- **Goal:** Make task 21.t green.
- **Read first:** Task 21.t; Spec §6; Spec §3.
- **Risk:** `boundary` (Sonnet; Opus review).
- **Deliverable:** `Invoke-Tar -Archive -Destination` (seam; `tar.exe -xf`), `Invoke-HuddleStop -Paths` (seam; runs `<launcher> stop` with a 60 s cap, above the verb's own 45 s), `Stop-HuddleIfRunning -Paths -Force`, `Install-App -Paths -Archive -State` implementing Spec §6 rule 3 exactly (stage next to InstallRoot, carry over `node\` and `tools\acp\node_modules`, validate, swap, rollback). Return whether anything changed. Do not write state here; `Invoke-Main` writes it (task 27).
- **Acceptance:** 21.t green.

### Task 22.t — Test: install Node

- **Goal:** Pin the Node step (Spec §6, Findings F2).
- **Read first:** Spec §6; Findings F2.
- **Risk:** `boundary` (Sonnet).
- **Deliverable:** With `Get-InstalledNodeVersion` and `Get-VerifiedAsset` mocked and a fake `node-vX-win-x64.zip` (contains `node-vX-win-x64\node.exe`, `...\node_modules\npm\bin\npm-cli.js`): fresh -> `NodeExe` and the npm CLI exist under `<InstallRoot>\node\`; skipped when the state matches **and** `Get-InstalledNodeVersion` returns the pinned version; **not** skipped when state matches but the exe is missing; replaced when the pinned version differs; the `SHASUMS256.txt` line for the zip is the expected hash (a tampered zip -> throws); no `node-vX-win-x64` wrapper folder left behind.
- **Acceptance:** Red until 22.i.

### Task 22.i — `Install-Node`

- **Goal:** Make task 22.t green.
- **Read first:** Task 22.t; Findings F2 (URLs, npm-cli path).
- **Risk:** `boundary` (Sonnet; Opus review).
- **Deliverable:** `Get-InstalledNodeVersion -NodeExe` (seam; `& $NodeExe --version`, `$null` on failure), `Install-Node -Paths -NodeVersion -State`: fetch `SHASUMS256.txt` and the zip via `Get-VerifiedAsset`, extract with `Invoke-Tar` into a temp dir, move the inner folder to `<InstallRoot>\node` (replace any old one).
- **Acceptance:** 22.t green.

### Task 23.t — Test: install the adapter

- **Goal:** Pin the `npm ci` step (Spec §6, §3).
- **Read first:** Spec §3, §6.
- **Risk:** `boundary` (Sonnet).
- **Deliverable:** `Invoke-NpmCi` mocked (the mock creates `AdapterEntry`). Cases: runs when the lock hash differs from state; **skipped** when the hash matches and the entry exists; runs when the hash matches but the entry is missing; invoked with the node dir first on `PATH`, working directory `tools\acp`, and arguments `ci --omit=dev --no-audit --no-fund`; a non-zero exit -> throws with the last lines of the npm log and a `fix:` mentioning proxy settings (`npm config set proxy`/`HTTPS_PROXY`); after success the entry file is verified (a mock that creates nothing -> throws `adapter entry not found`).
- **Acceptance:** Red until 23.i.

### Task 23.i — `Install-Adapter`

- **Goal:** Make task 23.t green.
- **Read first:** Task 23.t; Spec §3; `tools/acp/install.ps1` (whole, 11 lines: the dev equivalent).
- **Risk:** `boundary` (Sonnet; Opus review).
- **Deliverable:** `Invoke-NpmCi -NodeExe -WorkingDirectory -LogPath` (seam; `& $NodeExe <node>\node_modules\npm\bin\npm-cli.js ci ...`, output to `<Cache>\npm-ci.log`, returns exit code), `Install-Adapter -Paths -State`. Never use a global `npm`. Compute `lockSha256` with `Get-FileSha256`.
- **Acceptance:** 23.t green.

### Task 24.t — Test: launcher content

- **Goal:** Pin the launcher text exactly (Spec §4).
- **Read first:** Spec §4.
- **Risk:** `data` (Haiku).
- **Deliverable:** `Get-LauncherContent -Paths` returns a string; assert it contains, each on its own line in this order: `@echo off`, `setlocal`, then `set "Team__Desktop__Enabled=true"`, `set "Team__Acp__Enabled=true"`, `set "Team__Acp__Command=<NodeExe>"`, `set "Team__DataDir=<DataRoot>\data"`, `set "Team__PipeName=huddle-%USERNAME%"`, `set "ASPNETCORE_URLS=http://127.0.0.1:0"`, `set "ASPNETCORE_ENVIRONMENT=Production"`, `set "AllowedHosts=localhost;127.0.0.1"`, `set "PATH=<NodeDir>;%PATH%"`, and last `"<ExePath>" %*`; CRLF line endings; ASCII; paths with spaces are quoted correctly.
- **Acceptance:** Red until 24.i.

### Task 24.i — `Get-LauncherContent`

- **Goal:** Make task 24.t green.
- **Read first:** Task 24.t; Spec §4.
- **Risk:** `data` (Haiku).
- **Deliverable:** The pure function. No I/O.
- **Acceptance:** 24.t green.

### Task 25.t — Test: launcher, shortcut and PATH

- **Goal:** Pin idempotent entry points (Spec §3, §6).
- **Read first:** Spec §3, §6.
- **Risk:** `boundary` (Sonnet).
- **Deliverable:** Seams mocked: `New-HuddleShortcut`, `Get-UserPathValue`, `Set-UserPathValue`. Cases: first run writes `huddle.cmd`, creates the shortcut, appends InstallRoot to the user PATH; second run writes **nothing** (launcher mtime unchanged; `New-HuddleShortcut` and `Set-UserPathValue` not invoked) and reports `[skip]`; a changed launcher text is rewritten; PATH already containing InstallRoot (different case, trailing backslash) is not duplicated; an empty PATH works; shortcut parameters: target = launcher, working directory = InstallRoot, window style 7 (minimized), icon = `ExePath`.
- **Acceptance:** Red until 25.i.

### Task 25.i — `Install-Launcher`

- **Goal:** Make task 25.t green.
- **Read first:** Task 25.t; Spec §3.
- **Risk:** `boundary` (Sonnet; Opus review: persistent user environment change).
- **Deliverable:** `Install-Launcher -Paths`, plus seams `New-HuddleShortcut` (`WScript.Shell` COM; `$null` check on the COM object), `Get-UserPathValue`/`Set-UserPathValue` (`[Environment]::GetEnvironmentVariable('Path','User')` / `SetEnvironmentVariable`, preserving `REG_EXPAND_SZ` is not required). Compare existing launcher text byte-for-byte before writing.
- **Acceptance:** 25.t green.

### Task 26.t — Test: uninstall

- **Goal:** Pin safe, idempotent removal (Spec §6 rules 6, 7).
- **Read first:** Spec §6.
- **Risk:** `boundary` (Sonnet).
- **Deliverable:** Cases in temp roots: uninstall removes InstallRoot, the shortcut and the PATH entry, **keeps** DataRoot and its files; with `-PurgeData` also removes DataRoot; second uninstall prints `[skip]`, returns 0; refuses (returns 1, deletes nothing) when InstallRoot exists without `install-state.json`; refuses for a drive root (`C:\`), for a path with fewer than 3 segments, and for `$env:USERPROFILE`; calls `Stop-HuddleIfRunning` first.
- **Acceptance:** Red until 26.i.

### Task 26.i — `Invoke-Uninstall` and `Test-SafeToDelete`

- **Goal:** Make task 26.t green.
- **Read first:** Task 26.t; Spec §6.
- **Risk:** `boundary` (Sonnet; Opus review: destructive).
- **Deliverable:** `Test-SafeToDelete -Path` and `Invoke-Uninstall -Paths -PurgeData -Force`. Resolve paths with `[IO.Path]::GetFullPath` before any check; never use a wildcard.
- **Acceptance:** 26.t green.

### Task 27.t — Test: orchestration and idempotency

- **Goal:** Pin the whole run, in particular "second run changes nothing" (Spec §6, §7).
- **Read first:** Spec §6, §7; Findings F3.
- **Risk:** `logic` (Sonnet).
- **Deliverable:** A fake release folder (zip + `SHA256SUMS` + `release-info.json` inside the zip) and a fake Node zip, with the seams from Conv C2 mocked. Cases: first `Invoke-Main` -> exit 0, every step ran, `install-state.json` has all four keys, output ends `Agency Huddle <version> installed.`; **second run** -> exit 0, every step `[skip]`, ends `Already up to date (<version>).`, and a path+length+mtime snapshot of InstallRoot is identical; run with a changed archive -> only the app step runs, node/adapter `[skip]`, data file `marker.txt` preserved; interrupted run (mock `Install-Adapter` throws once) -> exit 1, state has `appSha256` and `nodeVersion` but not `lockSha256`, next run resumes at the adapter; two simultaneous runs (hold the mutex in the test) -> exit 1; unmet prerequisite -> exit 2 and nothing created; `-CheckOnly` -> 0/2; bad parameter combination (`-PurgeData` without `-Uninstall`) -> exit 3; sign-in step prints the instruction from F3 (or nothing if F3 says detection is impossible and instruction-only); `-NoLaunch` omits the launch, otherwise `Start-Process` of the launcher is invoked once (mocked).
- **Acceptance:** Red until 27.i.

### Task 27.i — `Invoke-Main`

- **Goal:** Make task 27.t green and finish `install.ps1`.
- **Read first:** Task 27.t; Spec §6, §7; Findings F3 (sign-in step).
- **Risk:** `logic` (Sonnet).
- **Deliverable:** `Invoke-Main` sequence: mutex -> `Test-Prerequisites` (`-CheckOnly` exits) -> `-Uninstall` branch -> fetch `SHA256SUMS` -> compare against state to decide which steps run -> `Stop-HuddleIfRunning` only if something will change -> `Install-App` -> read `release-info.json` -> `Install-Node` -> `Install-Adapter` -> `Install-Launcher` -> write state keys (each after its step) -> summary -> sign-in note (per F3) -> launch unless `-NoLaunch`. Release the mutex in `finally`. Map failures to the Spec §7 exit codes; print `[!!]` with `fix:` for any exception, not a stack trace.
- **Acceptance:** 27.t green; the whole `Install.Tests.ps1` green; `Invoke-ScriptAnalyzer installer/install.ps1` clean.

---

## Deliverable F. `installer/install.sh` (macOS)

All tasks edit the **one file** `installer/install.sh` and `installer/tests/install_test.sh`. Run in order. Conv C3 applies to every task. Files are **LF**. Function names are fixed.

### Task 28.t — Test: test harness, skeleton, paths

- **Goal:** Create the shell test harness and pin parameters, paths and asset source (Spec §3, §7).
- **Read first:** Spec §3, §7; Conv C3.
- **Risk:** `logic` (Sonnet).
- **Deliverable:** `installer/tests/lib.sh` (functions `t_assert_eq`, `t_assert_contains`, `t_assert_file`, `t_assert_no_file`, `t_run`, a pass/fail counter, non-zero exit if any failed), `installer/tests/run-all.sh` (runs every `installer/tests/*_test.sh` with `sh`), and `installer/tests/install_test.sh` which sets `HUDDLE_INSTALL_NO_MAIN=1`, `HOME=$(mktemp -d)` and sources `../install.sh`. Cases: `resolve_paths` gives `INSTALL_ROOT="$HOME/Library/Application Support/Agency.Huddle/install"`, `DATA_ROOT=".../Agency.Huddle"`, `APP_EXE=".../app/Huddle.App"`, `NODE_BIN=".../node/bin/node"`, `ADAPTER_ENTRY` ending `tools/acp/node_modules/@agentclientprotocol/claude-agent-acp/dist/index.js`, `STATE_FILE=".../install-state"`; `--install-root`/`--data-root` honoured; `parse_args` accepts every Spec §7 flag and an unknown flag exits 3; `resolve_asset` with `--base-url /existing/dir` is `file`, with `https://x` is `url`, default `latest` -> `https://github.com/$HUDDLE_REPO/releases/latest/download/NAME`, `--version v1.2.3` -> `.../releases/download/v1.2.3/NAME`; `log_step` formats `[ok] name - detail`, `[skip]`, `[!!]` plus `     fix: ...`; sourcing with the guard variable runs nothing.
- **Acceptance:** Red because `install.sh` does not exist; `sh installer/tests/run-all.sh` runs the harness.

### Task 28.i — `install.sh` skeleton

- **Goal:** Make task 28.t green.
- **Read first:** Task 28.t; Spec §3, §7.
- **Risk:** `logic` (Sonnet).
- **Deliverable:** `installer/install.sh`: `HUDDLE_REPO="${HUDDLE_REPO:-__HUDDLE_REPO__}"`, `parse_args`, `resolve_paths`, `resolve_asset`, `log_step`, a `main` that returns 0, the guard line. Unreplaced placeholder with no `--base-url` -> exit 3 with the "run from a release" message.
- **Acceptance:** 28.t green; `shellcheck -s sh installer/install.sh installer/tests/*.sh` clean.

### Task 29.t — Test: prerequisite checks

- **Goal:** Pin Spec §5 for macOS.
- **Read first:** Spec §5; Findings F1.
- **Risk:** `logic` (Sonnet).
- **Deliverable:** Overriding the seams (`macos_major`, `host_arch`, `free_disk_mb`, `http_probe`, and a stub `command -v` via a temp `PATH`): all good -> every item ok; macOS 11 -> fail with a `fix:`; `host_arch` `arm64`/`x86_64` select `osx-arm64`/`osx-x64`; an `x86_64` shell on a machine where `sysctl -n hw.optional.arm64` prints `1` selects `osx-arm64`; missing `curl` / `tar` / `codesign` / `xattr` / hash tool -> fail; disk 500 MB -> fail; probe failing for `nodejs.org` -> that item fails, the others still run; `--base-url <dir>` -> `http_probe` never called; `--check-only` exits 0/2 and creates no directory.
- **Acceptance:** Red until 29.i.

### Task 29.i — `check_prerequisites`

- **Goal:** Make task 29.t green.
- **Read first:** Task 29.t; Spec §5; Findings F1.
- **Risk:** `logic` (Sonnet).
- **Deliverable:** `macos_major` (`sw_vers -productVersion`), `host_arch`, `free_disk_mb` (`df -Pk`), `http_probe` (`curl -fsSI --max-time 10`), `check_prerequisites` filling a results list and `print_prerequisites`; constants `MIN_MACOS_MAJOR`, `MIN_FREE_MB=2048`; `main` honours `--check-only`.
- **Acceptance:** 29.t green.

### Task 30.t — Test: verified download

- **Goal:** Pin hash verification, caching, retry (Spec §2, §6).
- **Read first:** Spec §2, §6.
- **Risk:** `boundary` (Sonnet).
- **Deliverable:** Seam `http_get URL OUTFILE` overridden. Cases: `parse_sums` / `expected_hash NAME` return the hash for a name and fail (non-zero, message `not listed in SHA256SUMS`) for an absent one; CRLF-tolerant; `get_verified_asset` returns the cache path on a match; mismatch -> exit non-zero, bad file deleted, message has both hashes; cached with correct hash -> `http_get` not called (counter file); cached with wrong hash -> re-downloaded; two failures then success -> 3 calls; three failures -> non-zero; `file` sources are copied.
- **Acceptance:** Red until 30.i.

### Task 30.i — `get_verified_asset`

- **Goal:** Make task 30.t green.
- **Read first:** Task 30.t; Spec §2.
- **Risk:** `boundary` (Sonnet; Opus review).
- **Deliverable:** `file_sha256`, `parse_sums`, `expected_hash`, `http_get` (`curl -fsSL --retry 0 -o`), `get_verified_asset NAME SHA SOURCE CACHE` with 3 attempts (1 s, 3 s). Download to `NAME.partial`, verify, `mv` to `NAME`.
- **Acceptance:** 30.t green.

### Task 31.t — Test: install state

- **Goal:** Pin the macOS state file (Spec §6).
- **Read first:** Spec §6.
- **Risk:** `data` (Haiku).
- **Deliverable:** Cases: `read_state_key FILE app_sha256` prints nothing and returns 1 for a missing file or key; round-trip of the four keys plus `schema=1` through `write_state_key`; writing one key keeps the others; an existing key is replaced, not duplicated; values containing `=` survive; no `.tmp` left behind; `step_satisfied KEY EXPECTED` true only on an exact match.
- **Acceptance:** Red until 31.i.

### Task 31.i — state functions

- **Goal:** Make task 31.t green.
- **Read first:** Task 31.t; Spec §6.
- **Risk:** `data` (Haiku).
- **Deliverable:** `read_state_key`, `write_state_key` (rewrite via temp file + `mv`, parse with `sed`/`grep`, never `source` the file), `step_satisfied`.
- **Acceptance:** 31.t green.

### Task 32.t — Test: install the app

- **Goal:** Pin extract, permissions, signing and quarantine (Spec §6, Findings notes in the conversation: arm64 needs signed native code).
- **Read first:** Spec §3, §6.
- **Risk:** `boundary` (Sonnet).
- **Deliverable:** A fake `huddle-osx-arm64.tar.gz` built in the test (`app/Huddle.App`, `app/libx.dylib`, `tools/acp/package.json`, `tools/acp/package-lock.json`, `release-info.json`); `run_codesign` and `run_xattr` overridden to record calls. Cases: fresh install -> `app/Huddle.App` executable (`[ -x ]`), no `.staging`/`.old`; `run_xattr` called with `-dr com.apple.quarantine <InstallRoot>`; `run_codesign` called with `--force --sign -` for files that are Mach-O **and** fail `codesign --verify` (stub `codesign -v` by a function returning failure for `libx.dylib` only); skipped when state matches and the host exists; upgrade preserves `node/` and `tools/acp/node_modules/marker`; archive without `app/Huddle.App` -> non-zero, old install untouched; second `mv` failing -> rolled back; `stop_huddle_if_running` calls `run_huddle_stop` only when `instance.json` exists, `--force` ends a survivor; nothing-will-change never stops.
- **Acceptance:** Red until 32.i.

### Task 32.i — `stop_huddle_if_running` and `install_app`

- **Goal:** Make task 32.t green.
- **Read first:** Task 32.t; Spec §6.
- **Risk:** `boundary` (Sonnet; Opus review).
- **Deliverable:** `run_tar` (`tar -xzf`), `run_codesign`, `run_xattr`, `run_huddle_stop` (runs `<InstallRoot>/bin/huddle stop`, 60 s cap), `stop_huddle_if_running`, `install_app` per Spec §6 rule 3, `ensure_signed DIR` (for each file whose `file -b` mentions `Mach-O`: if `codesign --verify` fails, `run_codesign --force --sign - FILE`), `chmod +x` on the host. Return whether anything changed.
- **Acceptance:** 32.t green.

### Task 33.t — Test: install Node

- **Goal:** Pin the Node step (Findings F2).
- **Read first:** Spec §6; Findings F2.
- **Risk:** `boundary` (Sonnet).
- **Deliverable:** `node_version_of` and `get_verified_asset` overridden; fake `node-vX-darwin-arm64.tar.gz` (`node-vX-darwin-arm64/bin/node`, `.../lib/node_modules/npm/bin/npm-cli.js`). Cases: fresh -> `NODE_BIN` executable and the npm CLI path exists under `INSTALL_ROOT/node`; skipped when state and `node_version_of` agree; not skipped when state matches but the binary is missing; replaced on a version change; a tampered tarball (hash mismatch from `SHASUMS256.txt`) -> non-zero; no wrapper folder left; arch `x86_64` fetches the `darwin-x64` name.
- **Acceptance:** Red until 33.i.

### Task 33.i — `install_node`

- **Goal:** Make task 33.t green.
- **Read first:** Task 33.t; Findings F2.
- **Risk:** `boundary` (Sonnet; Opus review).
- **Deliverable:** `node_version_of BIN`, `install_node`. Verify via the `SHASUMS256.txt` line, extract to a temp dir, move the inner folder to `INSTALL_ROOT/node`.
- **Acceptance:** 33.t green.

### Task 34.t — Test: install the adapter

- **Goal:** Pin the `npm ci` step (Spec §6).
- **Read first:** Spec §3, §6.
- **Risk:** `boundary` (Sonnet).
- **Deliverable:** `run_npm_ci` overridden (creates `ADAPTER_ENTRY`). Cases mirror 23.t: runs when the lock hash differs; skipped when it matches and the entry exists; runs when the entry is missing; invoked with the node `bin` directory first on `PATH`, working directory `tools/acp`, args `ci --omit=dev --no-audit --no-fund`; non-zero -> non-zero return with the last log lines and a proxy `fix:`; entry verified afterwards.
- **Acceptance:** Red until 34.i.

### Task 34.i — `install_adapter`

- **Goal:** Make task 34.t green.
- **Read first:** Task 34.t; Spec §3.
- **Risk:** `boundary` (Sonnet; Opus review).
- **Deliverable:** `run_npm_ci`, `install_adapter`. Use the bundled `node` to run `npm-cli.js`; never a global `npm`.
- **Acceptance:** 34.t green.

### Task 35.t — Test: launcher and Info.plist content

- **Goal:** Pin the generated text (Spec §4).
- **Read first:** Spec §4.
- **Risk:** `data` (Haiku).
- **Deliverable:** `launcher_content` output, line by line: `#!/bin/sh`, `set -eu`, then `export Team__Desktop__Enabled=true`, `export Team__Acp__Enabled=true`, `export Team__Acp__Command='<NODE_BIN>'`, `export Team__DataDir='<DATA_ROOT>/data'`, `export Team__PipeName="huddle-$(id -un)"`, `export ASPNETCORE_URLS='http://127.0.0.1:0'`, `export ASPNETCORE_ENVIRONMENT=Production`, `export AllowedHosts='localhost;127.0.0.1'`, `export PATH='<node bin dir>':"$PATH"`, and last `exec '<APP_EXE>' "$@"`; a path containing a space and one containing a single quote are quoted safely; `app_stub_content` (the `.app`'s executable) is `#!/bin/sh` then `exec '<INSTALL_ROOT>/bin/huddle' "$@" >>'<DATA_ROOT>/launcher.log' 2>&1`; `plist_content VERSION` is valid XML containing `CFBundleExecutable` = `AgencyHuddle`, `CFBundleIdentifier` = `com.agency.huddle`, `CFBundleName` = `Agency Huddle`, `CFBundlePackageType` = `APPL`, `CFBundleVersion` = the argument, `LSUIElement` = `<true/>`; LF only.
- **Acceptance:** Red until 35.i.

### Task 35.i — `launcher_content`, `app_stub_content`, `plist_content`

- **Goal:** Make task 35.t green.
- **Read first:** Task 35.t; Spec §4.
- **Risk:** `data` (Haiku).
- **Deliverable:** Three pure functions (a `shell_quote` helper escapes `'` as `'\''`). No I/O.
- **Acceptance:** 35.t green.

### Task 36.t — Test: launcher, `.app` and symlink

- **Goal:** Pin idempotent entry points (Spec §3, §6).
- **Read first:** Spec §3, §6.
- **Risk:** `logic` (Sonnet).
- **Deliverable:** Cases under a temp `HOME`: first run creates `bin/huddle` (executable), `~/Applications/Agency Huddle.app/Contents/{Info.plist,MacOS/AgencyHuddle}` (executable) and the symlink `~/.local/bin/huddle`; second run rewrites nothing (mtimes unchanged) and prints `[skip]`; changed content is rewritten; a symlink pointing elsewhere is replaced; a regular file at the symlink path is **not** overwritten (warn, continue); when `~/.local/bin` is not on `PATH` a one-line hint is printed (no shell-rc editing).
- **Acceptance:** Red until 36.i.

### Task 36.i — `install_launcher`

- **Goal:** Make task 36.t green.
- **Read first:** Task 36.t; Spec §3.
- **Risk:** `logic` (Sonnet).
- **Deliverable:** `install_launcher`, writing only when content differs (`cmp -s`).
- **Acceptance:** 36.t green.

### Task 37.t — Test: uninstall

- **Goal:** Pin safe, idempotent removal (Spec §6 rules 6, 7).
- **Read first:** Spec §6.
- **Risk:** `boundary` (Sonnet).
- **Deliverable:** Mirrors 26.t: removes InstallRoot, the `.app` and the symlink; **keeps** DataRoot; `--purge-data` also removes it; second run `[skip]`, exit 0; refuses without `install-state`; refuses for `/`, `$HOME`, a path with fewer than 3 segments; a symlink that points elsewhere is left alone; stops a running instance first.
- **Acceptance:** Red until 37.i.

### Task 37.i — `do_uninstall` and `safe_to_delete`

- **Goal:** Make task 37.t green.
- **Read first:** Task 37.t; Spec §6.
- **Risk:** `boundary` (Sonnet; Opus review: destructive).
- **Deliverable:** `safe_to_delete`, `do_uninstall`. Canonicalise with `cd ... && pwd -P` before any check; quote everything; no `rm -rf` on an unchecked variable.
- **Acceptance:** 37.t green.

### Task 38.t — Test: orchestration and idempotency

- **Goal:** Pin the whole run (Spec §6, §7) on macOS.
- **Read first:** Spec §6, §7; Findings F3.
- **Risk:** `logic` (Sonnet).
- **Deliverable:** Mirrors 27.t with the shell seams overridden: first run installs everything, exit 0, `Agency Huddle <version> installed.`; **second run** all `[skip]`, `Already up to date (<version>).`, a snapshot of INSTALL_ROOT (`find . -type f -exec` path, size, mtime) identical; changed archive runs only the app step and keeps `marker.txt` in the data root; interrupted run (override `install_adapter` to fail once) -> exit 1 with the state missing only `lock_sha256`, resumes on the next run; concurrent run (pre-create `install.lock`) -> exit 1; unmet prerequisite -> exit 2, nothing created; `--check-only` -> 0/2; `--purge-data` without `--uninstall` -> exit 3; sign-in note per F3; `--no-launch` omits `open`, otherwise `open` of the `.app` is invoked once (override `run_open`).
- **Acceptance:** Red until 38.i.

### Task 38.i — `main`

- **Goal:** Make task 38.t green and finish `install.sh`.
- **Read first:** Task 38.t; Spec §6, §7; Findings F3.
- **Risk:** `logic` (Sonnet).
- **Deliverable:** `main` in the same order as task 27.i, with the lock `mkdir "$CACHE/install.lock"` and `trap 'rmdir ...' EXIT`. Map failures to the Spec §7 exit codes and print `[!!]` with `fix:`, not shell traces (no `set -x`).
- **Acceptance:** 38.t green; the whole `install_test.sh` green; `shellcheck -s sh` clean.

---

## Deliverable G. End-to-end proof, CI and docs

### Task 39 — `installer/tests/smoke.ps1` (Windows end-to-end test)

- **Goal:** Prove the real release works on a clean Windows profile and is idempotent (Spec §6, §8, §10).
- **Read first:** Spec §4, §6, §8, §10; `test-health.ps1` L1-L80 (how it starts the app and waits for the port).
- **Risk:** `boundary` (Sonnet; Opus review).
- **Deliverable:** `installer/tests/smoke.ps1 -ReleaseDir <dir>` (ASCII, Conv C2, runs under 5.1). It first builds `<dir>` if absent by calling `installer/build-release.sh --rid win-x64` through Git Bash, else expects `huddle-win-x64.zip`, `SHA256SUMS` already there. Using temp `-InstallRoot`/`-DataRoot` and `$env:Team__Desktop__OpenBrowser='false'`: (1) `install.ps1 -BaseUrl <dir> -NoLaunch` exits 0; (2) `-CheckOnly` exits 0; (3) rerun: every line `[skip]`, snapshot identical; (4) start `huddle.cmd` hidden, wait <= 90 s for `<data>\instance.json`, `GET <url>/health` = 200; (5) start `huddle.cmd` again: exits 0 within 20 s and exactly one `Huddle.App` process exists; (6) rerun the installer while running: all `[skip]`, the app is **not** restarted (same PID); (7) build a second release folder with a different `release-info.json` version, install over the running app: the app is stopped, replaced, node/adapter `[skip]`, a marker file in the data dir survives; (8) start again, `huddle.cmd stop` exits 0, the process and instance file are gone, no `node.exe` under InstallRoot remains; (9) `-Uninstall`: InstallRoot, shortcut, PATH entry gone, data kept; (10) `-Uninstall` again exits 0. Any failed step prints the step number and exits 1.
- **Acceptance:** Passes locally on this Windows machine against a release folder from `build-release.sh`; the failure messages name the step.

### Task 40 — `installer/tests/smoke.sh` (macOS end-to-end test)

- **Goal:** The macOS equivalent of task 39 (Spec §10).
- **Read first:** Task 39; Spec §4, §6, §8; Conv C3.
- **Risk:** `boundary` (Sonnet; Opus review).
- **Deliverable:** `installer/tests/smoke.sh <release-dir>` with the same 10 steps using `HOME=$(mktemp -d)`, `bin/huddle`, `bin/huddle stop`, `pgrep -f`. Additionally: (a) after install, `codesign --verify --deep` passes on the host and on every `*.dylib`; (b) the `.app` bundle contains a valid plist (`plutil -lint` on macOS); (c) step 8 asserts no `node` process from the install tree remains. Runs on macOS; on other systems prints `SKIP: macOS only` and exits 0.
- **Acceptance:** Syntax-checks with `sh -n` and `shellcheck -s sh`; the first real run is on a Mac runner (task 42) or manual step M3. If it cannot be executed here, say so in the report.

### Task 41 — `.github/workflows/installer-ci.yaml` (PR checks)

- **Goal:** Run the installer unit tests wherever they are meaningful (Conv C2, C3).
- **Read first:** Conv C2, C3; Spec §9 (permissions rules).
- **Risk:** `boundary` (Sonnet; Opus review).
- **Deliverable:** Trigger `pull_request` and `push` on paths `installer/**`, `agents/scripts/Check-Eol.ps1`, `.gitattributes`. `permissions: contents: read`. Jobs: `pester` (windows-latest: install Pester 5.5+, `Invoke-Pester installer/tests -CI`), `psscriptanalyzer` (windows-latest: `Invoke-ScriptAnalyzer installer -Recurse -Severity Warning,Error`, fail on any), `shell` (matrix ubuntu-latest, macos-latest: `sh installer/tests/run-all.sh`), `shellcheck` (ubuntu-latest: `shellcheck -s sh installer/*.sh installer/tests/*.sh`). Actions pinned to full SHAs with tag comments. Validate with `actionlint`.
- **Acceptance:** `actionlint` clean; every command in it runs locally (state which you could not run).

### Task 42 — `.github/workflows/release.yaml`

- **Goal:** Build, verify and publish the release exactly as Spec §9 says.
- **Read first:** Spec §2, §9; `global.json` (whole); Task 41 for the pinning convention.
- **Risk:** `boundary` (Sonnet; Opus review: supply chain, secrets, permissions).
- **Deliverable:** The five jobs and permissions of Spec §9. `assemble` replaces the placeholder with `sed "s|__HUDDLE_REPO__|${GITHUB_REPOSITORY}|g"` and fails if `__HUDDLE_REPO__` remains in either published installer. `publish` runs `gh release create` with `GH_TOKEN: ${{ github.token }}` and uses `--verify-tag`. The attestation step has `if: ${{ !github.event.repository.private }}`. No secrets other than `github.token`. Do not add signing steps (decision D1).
- **Acceptance:** `actionlint` clean; the report lists each action's pinned SHA and the permissions of each job. Manual step M1 runs it for real.

### Task 43 — Permanent docs, Index, user guide

- **Goal:** Replace this plan with documentation (Conv C5).
- **Read first:** `docs/Index.md` L1-L60 and L80-L95 (table shape); `docs/Huddle.EngineeringGuide.md` (find the `Team:` configuration table by heading); `docs/engineering/known-limits.md` (whole); `docs/Huddle.UserGuide.md` (find "Getting started" or the first heading).
- **Risk:** `data` (Haiku).
- **Deliverable:** (1) `docs/engineering/installer.md`: layout (Spec §3), the two commands, flags (Spec §7), idempotency rules (Spec §6), how to run the tests (`Invoke-Pester installer/tests -CI`, `sh installer/tests/run-all.sh`, the smoke scripts), how a release is cut (tag `v*`), the manual steps M1-M5. (2) `docs/Huddle.EngineeringGuide.md`: add rows for `Team:Desktop:Enabled` and `Team:Desktop:OpenBrowser` to the configuration table. (3) `docs/Huddle.UserGuide.md`: an "Install" section with the two one-liners, `huddle stop`, and the Claude sign-in note (per Findings F3). (4) `docs/engineering/known-limits.md`: A4 (no in-app Quit; closing the Windows console window or `huddle stop`), no code signing, Intel Mac not smoke-tested in CI. (5) `docs/Index.md`: add an `installer.md` row and **remove** the `installer-plan.md` row; delete `docs/engineering/installer-plan.md`. (6) `docs/engineering/roadmap.md`: one line for the in-app Quit button.
- **Acceptance:** `pwsh agents/scripts/Check-DocIndex.ps1` exits 0; `pwsh agents/scripts/Check-Eol.ps1` clean; links resolve.

### Task 44 — Final verification (Haiku runs; manager reads output only)

- **Goal:** Confirm everything is green together before the manager reviews diffs.
- **Read first:** `CLAUDE.md` "C# code" section; `agents/scripts/README.md` (whole).
- **Risk:** `data` (Haiku).
- **Deliverable:** Run and report pass/fail with the last 20 lines of each failing command: `dotnet build Huddle.slnx`; `dotnet test Huddle.slnx --`; `pwsh agents/scripts/Check-All.ps1`; `Invoke-Pester installer/tests -CI`; `sh installer/tests/run-all.sh` (Git Bash); `./test-health.ps1`; `installer/tests/smoke.ps1`. Do not fix anything; report.
- **Acceptance:** A table of seven commands with PASS/FAIL.

---

## Findings appendix (filled by tasks 1-3)

### F1. Minimum OS versions (task 1, checked by the manager 2026-10-02)

Source: https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md, read 2026-10-02.

- **macOS:** .NET 10 supports macOS 27, 26 and 15 on Arm64 and x64. macOS 14 reached end of life on 2026-09-14 and is listed as out of support. **`MIN_MACOS_MAJOR=15`**; below it the check fails with that reason. Loosen the constant if real users on 14 turn up.
- **Windows:** the page lists Windows 11 23H2 and later, Windows 10 21H2 (Enterprise/IoT), 1809 and 1607 (Enterprise). Consumer Windows 10 22H2 is not listed but runs .NET; `tar.exe` (needed by the installer) ships from Windows 10 1803. **Settled: `MIN_WINDOWS_BUILD=17763`** (Windows 10 1809).
- Architectures: Arm64 and x64 on both; x64 builds run on Windows 11 Arm64 under emulation.

### F2. Node pin and download names (task 2, checked by the manager 2026-10-02)

- Pin **`v24.21.0`** ("Krypton", LTS, 2026-09-07; the newest LTS on `https://nodejs.org/dist/index.json`, while v26 is not yet LTS). `claude-agent-acp` requires node >= 22; `claude-agent-sdk` >= 18. Stored in `installer/node-version.txt`.
- All four URLs return HTTP 200: `https://nodejs.org/dist/v24.21.0/node-v24.21.0-win-x64.zip`, `.../node-v24.21.0-darwin-arm64.tar.gz`, `.../node-v24.21.0-darwin-x64.tar.gz`, `.../SHASUMS256.txt`.
- Archive root folder is `node-v24.21.0-<platform>/`. `npm-cli.js`: Windows `node_modules/npm/bin/npm-cli.js` (and `node.exe` at the folder root); macOS `lib/node_modules/npm/bin/npm-cli.js` (and `bin/node`). So `Install-Node` on Windows puts `NodeExe` at `<InstallRoot>\node\node.exe` and npm at `<InstallRoot>\node\node_modules\npm\bin\npm-cli.js`; on macOS `NODE_BIN` is `<InstallRoot>/node/bin/node` and npm at `<InstallRoot>/node/lib/node_modules/npm/bin/npm-cli.js`.

### F3. Claude sign-in detection (task 3)

Read 2026-10-02 from the bundled binary's `--help` output (`--version` 2.1.257), https://code.claude.com/docs/en/authentication, and the adapter source. `auth status`, `auth login` and `setup-token` were **never run**, so the exit codes are documented, not observed; keep the status call behind a seam and fake it in tests.

- **Detection exists:** `claude auth status`. The docs say it exits 0 if logged in and 1 if not (the logged-out case still prints valid JSON). **Rule: run it with a ~10 s timeout, output discarded; exit 0 = signed in; anything else (non-zero, timeout, binary missing) = not signed in. Do not parse or print the JSON** (it contains the email and organisation).
- **Sign-in:** `claude auth login` (`--claudeai` is the default; `--console` is for API billing). It opens a browser and needs an attached terminal. Its exit codes are undocumented, so after it runs, call `auth status` again.
- **Never** run `claude setup-token` (it prints a secret) and never read `.credentials.json`.
- **Where credentials live:** Windows `%USERPROFILE%\.claude\.credentials.json`; macOS the Keychain, with `~/.claude/.credentials.json` as a fallback. `CLAUDE_CONFIG_DIR` relocates them, so the launcher and the installer must not set it differently. Environment alternatives (names only): `ANTHROPIC_API_KEY`, `ANTHROPIC_AUTH_TOKEN`, `CLAUDE_CODE_OAUTH_TOKEN`, `CLAUDE_CODE_USE_BEDROCK`, `CLAUDE_CODE_USE_VERTEX`, `CLAUDE_CODE_USE_FOUNDRY`.
- **The adapter starts and answers `initialize` with no credentials;** a prompt then fails with an ACP `authRequired` error. So the installer and the smoke tests (which start no Persona) need no sign-in.
- **Locating the binary:** `<InstallRoot>\tools\acp\node_modules\@anthropic-ai\claude-agent-sdk-win32-x64\claude.exe` on Windows; `<InstallRoot>/tools/acp/node_modules/@anthropic-ai/claude-agent-sdk-darwin-arm64/claude` or `...-darwin-x64/claude` on macOS (package names from the SDK's `optionalDependencies`).
- **Last installer step (tasks 27 and 38), interactive session only:** run `auth status`; exit 0 prints `Claude: signed in.`; otherwise print (never fail the install over it):

  ```text
  Claude is not signed in yet. Sign in once, then start Agency Huddle:
    <path-to-claude> auth login
  (This opens your browser. Add --console to use API billing instead of a Claude subscription.)
  ```

  Non-interactive runs (no TTY, `NO_BROWSER`, `SSH_CONNECTION`, `SSH_CLIENT`, `SSH_TTY` or `CLAUDE_CODE_REMOTE` set) only print the instruction. An opt-in `-SignIn` / `--sign-in` flag may run `auth login` itself.

## Retrospective log

| After task | Date | Main findings | Plan edits made |
| --- | --- | --- | --- |
| 15 | 2026-10-02 | Agents re-read the whole plan (7 of 7), `rules.md` is 44K characters not "97 lines", commands found by trial (red file flags, one class per run, PowerShell has no `head`), reports claimed checks that were not run, manager context 86K to 353K | Facts Core and brief rewritten (red-file flags, trimmed full-suite pipe, rules digest, report integrity); agent type `installer-dev` added; fakes `RecordingBrowserOpener`, `FakeLifetime`, `FakeDesktopShutdown` named up front in 10.t, 11.t and 12.t; 14.i acceptance uses the runner scripts; one fresh agent per pair from task 10 |
| 30 | | | |
| 45 | | | |
| 60 | | | |
| 75 | | | |

## Summary of tasks

79 tasks: 35 test/implementation pairs (70 tasks) plus 9 single tasks (1, 2, 3, 39, 40, 41, 42, 43, 44). Risk mix: `data` 20 (Haiku, 25%), `logic` 23, `boundary` 36 (Sonnet; the `boundary` implementations also get an Opus review).
