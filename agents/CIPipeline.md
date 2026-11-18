# CI Pipeline

## Contents

- Topology
- Reproducing a CI run locally
- Local runner
- Known failure modes
- Reflection — 2026-09-12: the first run died on a cached image
- Reading CI results from a session
- Related

How Gitea Actions validates this repo, and how to reproduce a failing run on your own
machine in about two minutes. Read this before debugging a red run — the failure modes
recorded here are environmental, and none of them are code regressions.

Verified end to end on 2026-09-21 against SDK 10.0.401 in the CI container: **1329 tests,
1319 passed, 10 skipped, 0 failed** (run 658, the PR that added the health-endpoint smoke
test). That is the whole suite minus the three quarantined tests below; an unfiltered run of
the same tree is 1332.

After the test split (2026-10-18) a local Windows run of `Run-Tests.ps1` reports **10714 tests,
10695 passed, 19 skipped, 0 failed**, spread over five test projects (listed test methods: Hosted 683,
Agents 470, Components 1592, SkillPacks 293, Domain 5032; `Huddle.Tests.Common` holds no tests). The
CI-container figures above were not re-measured; refresh them from the next green CI run's summary.

The figures in this file were **970/962/8 until 2026-09-21**, and **656/648/659 before
2026-09-15**. Refresh them when you next read a green run's summary rather than trusting them —
a count nobody updates stops being a check and becomes noise.

These totals move whenever real work lands — they were 565/568 when this page was written,
before the Teams change added 91 tests. Treat a changed total as something to *confirm*,
not something to fear; see [Known failure modes](#known-failure-modes).

This page covers validation only. There is no packaging, publishing, or release step yet;
when one arrives it belongs in a new job in `ci-main-local.yaml` (`needs: ci`), not inside the
shared `validate`.

## Topology

> [!WARNING]
> `gitea-host.example` below is a redacted placeholder, not a name that resolves. The real
> internal hostname is kept out of this file because the `secret-scan` job fails the build
> on an `internal-mdns-host` finding. Read the real one from `git remote -v`.

Both workflows run on a self-hosted runner labelled `dotnet-10-local`, on the Windows development
desktop (see [Local runner](#local-runner)), inside the
`mcr.microsoft.com/dotnet/sdk:11.0.100` container (Ubuntu 26.04), with `shell: bash`.

**The runner is `x64`.** Test output reads `net11.0|x64`. Until 2026-10-09 CI ran on a Mac mini
runner labelled `dotnet-10`, which is `arm64`, so any run number or timing recorded in this file
from before that date was measured on `arm64`. Timing races are sensitive to core count and memory
ordering, which differ between the two; a flake that only ever showed on the old runner may not
reproduce here, and the reverse. A green run on the Windows desktop still does not imply a green run
on other hardware or other line endings.

The image is pinned to an **exact SDK patch**, not the floating `:10.0` tag, and that pin is
half of a pair: `global.json` requires `11.0.100` with `rollForward: latestPatch`, so only a
`11.0.1xx` SDK satisfies it. A floating tag leaves you at the mercy of whatever the runner
happens to have cached — which is what broke the very first run on this repo (see
[Reflection](#reflection--2026-09-12-the-first-run-died-on-a-cached-image)). Bump the two
together, or not at all.

| Workflow | Trigger | Jobs |
| --- | --- | --- |
| `.gitea/workflows/ci-pr-local.yaml` | `pull_request` into `main` | `ci` (calls `ci-shared.yaml`) |
| `.gitea/workflows/ci-main-local.yaml` | `push` to `main` | `ci` (calls `ci-shared.yaml`) |
| `.gitea/workflows/ci-shared.yaml` | `workflow_call` only; never runs alone | `docs`, `secret-scan`, `validate` (in parallel) |

The checks are defined once, in `ci-shared.yaml`; the PR and main workflows are thin callers that
differ only in trigger and `concurrency`. There is no second copy to keep in step, so the old
`diff` of the two `jobs:` blocks is gone. Change a step in `ci-shared.yaml` and both gates change.

The callers have **no `paths-ignore`**, so every PR and every push to `main` runs all three jobs,
docs-only changes included. A docs-only PR therefore never strands a required check, and the `docs`
job runs `agents/scripts/Check-DocIndex.ps1` on every change. The price is that a docs-only PR pays
for a full `validate` run (about 3 minutes). Required checks on `main` are the three
`CI (Shared) / <job> (pull_request)` contexts.

### What validate does

Checkout, install Node.js, restore, vulnerable-dependency scan, build, test.

Two of those steps exist for reasons specific to this repo:

- **Install Node.js.** `AgentProcessLauncherTests` (marked `[Trait("Category", "Process")]`)
  launches a real `node -e` subprocess to prove the stdio pipe wiring works. The SDK image
  ships no Node, and without this step those six tests fail with `Failed to start 'node'`.
  `ComposerScriptTests` (6 test methods; the picker harness `tests/Huddle.Tests.Agents/Ui/References/composer-picker.harness.js`
  loads the real `composer.js` into a `vm` context under `node`) need it too, and so does
  `MarkdownCommandsScriptTests` (the harness `tests/Huddle.Tests.Agents/Ui/Library/markdown-commands.harness.mjs`
  imports the pure `wwwroot/markdown-commands.js` as an ES module; plain Node 18 runs it). One
  `node` process serves the whole class, so a missing `node` fails all its methods together.
  Ubuntu's `nodejs` package (v18) is enough for `node -e`; the ACP adapter wants something
  newer, but it only runs under `TEAM_E2E=1`, which CI pins off.
- **Build is also the style gate.** `Directory.Build.props` sets `TreatWarningsAsErrors`,
  so an analyzer complaint fails the build rather than printing a warning.

### What CI deliberately does not do

- **No LLM calls, and no money spent.** The eight tests under `tests/Huddle.Acp.Tests/E2E/`
  drive a real adapter against a real model. `E2E.Enabled` reads `TEAM_E2E == "1"`, and the
  `validate` job pins `TEAM_E2E: "0"` so a runner-level environment variable can never switch
  them on by accident. Expect all eight to report as skipped in every run.
- **No retry loop.** The sibling Agency repo wraps its test steps in three attempts because
  its functional suite talks to a live model. Nothing here talks to anything, so every test
  gets exactly one attempt and a failure is a failure. A blanket retry would also mask a
  genuine regression; a race is fixed at its cause instead (see Quarantined tests below).
- **No `actions/checkout`, and no other JavaScript action.** Actions of that kind need Node
  in the container, and Node only arrives partway through `validate`. Both jobs clone by hand
  with a token-injected URL and then check out `$GITHUB_SHA`, so every run starts from a
  fresh clone with no warm working tree.

### Quarantined tests

None, since 2026-11-01. Two `DotAcpAgentSessionTests` cases were excluded by name for an
event-ordering race: `PromptAsync_StreamsChunksInOrder_ThenTurnCompleted` (about 1 run in 5) and
`PromptAsync_ThoughtAndToolCallEvents_ArePublished`. A third, in `DotAcpConcurrentHostTests`, showed
the same race unquarantined. The race was a real content-loss bug in the ACP client, not a test
flake. The fix is the `WireOrderLedger` dispatch barrier, and
[known-limits.md](../docs/engineering/known-limits.md) has the history.

A quarantine line still needs a known-limits entry, the same line in both Gitea workflows and in
`.github/workflows/ci.yaml`, and a plan to delete it. Treat a red run as a bug to fix at its cause,
whichever subtree it sits in, never as a reason to add a filter.

> [!NOTE]
> **Lifted 2026-09-27: `PersonaSupervisorLifecycleTests.Shutdown_DisposesEveryHost`.** It was recorded as
> "its 10-second token races `WaitUntilAsync`". That was the wrong diagnosis: every failure in
> that class spent the *whole* budget, whatever the budget was — raised to 30 s, the same runs
> still failed, 30 s later. The condition never arrived, because on Linux `PersonaStore`'s
> watcher can miss a Persona file written into a just-created sub-folder (see
> [traps.md](../docs/engineering/traps.md)). Fixed in `PersonaStore.AffectsATeamsFile`. On current
> `main`, the test ran 80 times under 1-CPU, 8-process load in the CI container with no failure.

### Reproducing a flake: load, not loops

A sequential loop is the wrong tool. `PersonaSupervisorTests` (since split into `PersonaSupervisorLifecycleTests`, `PersonaSupervisorHealthTests` and `PersonaSupervisorRestartTests`) passed 10 out of 10 isolated runs, and
3 concurrent full-suite runs, on both branches, while under load the same class failed 9 out of 16
runs. Cap the container's CPU and run the **built test executable** in parallel processes:

```bash
docker run --rm --cpus=1 -v "$PWD:/repo:ro" -v huddle-nuget:/root/.nuget/packages \
  -w /repo mcr.microsoft.com/dotnet/sdk:11.0.100 bash -c '
    cp -r /repo /src && cd /src && rm -rf */*/bin */*/obj
    dotnet build tests/Huddle.Tests.Agents/Huddle.Tests.Agents.csproj -c Release -v q
    for p in $(seq 8); do
      ( for i in 1 2; do tests/Huddle.Tests.Agents/bin/Release/net11.0/Huddle.Tests.Agents \
          -class Agency.Huddle.Tests.Acp.PersonaSupervisorLifecycleTests | grep "Total:"; done ) &
    done; wait'
```

Copying to `/src` keeps the Linux `bin`/`obj` out of your Windows checkout. A failure that costs
exactly the full timeout on every run is an event that never came, not a slow run. Raising the
budget only makes those failures slower.

Check that claim before acting on it: a budget that also covers the fixture's start fails at its
timeout too. A process runs its tests in the same order every time, so a failure that needs the
test to run first in a cold process never shows under `-class`. Put it first with `-method`, beside
one other class for load. `PersonaSupervisorLifecycleTests.Enabled_StartsOneHostPerPersonaFile`
was this. With the test first, 24 processes and 4 CPUs, `PipeHostFixture.StartAsync` took 6-8 s
of its shared 10 s token, and 3 of 120 runs failed at 10.0 s. The Persona files had already been
found and both hosts were a few milliseconds from running. Every `PipeHostFixture` test now takes a
`BehaviourBudget`: its clock starts once the fixture is up, and the start has its own
`TestBudgets.Slowest` bound. Before that change, 21 of 72 such runs failed with one of three Restart
tests first; after it, 216 runs passed. Most of the cold start is JIT across the whole composition
root. The Skill Sources move each fixture used to repeat is now copied from a per-process template
(`SkillSourcesTemplate`), but that removed only about 1 s of the 6-8 s.

Delete a `--filter-not-method` line as its race is diagnosed. Do not add one without a
matching `known-limits.md` entry — the filter is where flakes go to be forgotten, and the
entry is what stops that.

> [!NOTE]
> The third test was not previously named in `known-limits.md`; CI found it. Its siblings in
> `DotAcpAgentSessionTests` drain events the same way and are presumably exposed to the same
> race, so expect this list to grow until the transport is diagnosed rather than the tests.

## Reproducing a CI run locally

This is the whole pipeline, on your machine, in the same container CI uses. It needs Docker
and about two minutes. Run it from the repo root.

```bash
# A fresh container each time, but a named volume keeps the NuGet cache warm between runs.
docker volume create huddle-nuget

docker run --rm -v "$PWD:/work" -v huddle-nuget:/root/.nuget/packages \
  -w /work -e TEAM_E2E=0 mcr.microsoft.com/dotnet/sdk:11.0.100 bash -c '
    set -euo pipefail
    apt-get update -qq && apt-get install -y -qq nodejs
    dotnet restore Huddle.slnx
    dotnet build   Huddle.slnx --configuration Release --no-restore
    dotnet test    Huddle.slnx --configuration Release --no-build --
    pwsh -NoProfile -NonInteractive -File ./test-health.ps1 -Configuration Release -TimeoutSeconds 120
  '
```

Drop the two `--filter-not-method` lines to run the full suite including the quarantined
tests — worth doing when you are trying to reproduce one of the races on purpose.

```text
Test run summary: Passed!
  total: 1329
  failed: 0
  succeeded: 1319
  skipped: 10

======================================================================
  PASS   /health answered 200 Healthy in 0.5s
======================================================================
```

The last line of the container script is the same smoke test `validate` ends with. It runs the
Release build as a real process and GETs `/health`, which is the one thing no test in
`tests/Huddle.Tests.*` can check; the tests that touch the app host it in-process. It builds nothing, so it has
to follow the build step.

Run it from **PowerShell**, not Git Bash. In Git Bash on Windows `$PWD` expands to an MSYS
path (`/e/Repos/Huddle`) that Docker Desktop cannot resolve to a host directory, so it
silently mounts an **empty** volume and the run fails with `MSB1009: Project file does not
exist. Switch: Huddle.slnx`. That message means the mount is empty, not that the repo is
broken; substituting the Windows-style path also works.

> [!CAUTION]
> Mounting `$PWD` writes Linux `bin/` and `obj/` over your Windows build output, and the
> absolute paths baked into `obj/*.nuget.g.props` will point at container paths afterwards.
> Copy the tree to a scratch directory first if you care about your working tree, and run
> `dotnet build` again on Windows afterwards either way.

To reproduce the CI checkout faithfully rather than approximately, convert the copy's line
endings to LF first — see [Line endings](#line-endings) for why that can matter.

## Local runner

CI runs on one Gitea Actions runner on the Windows desktop, in Docker Desktop, with the label
`dotnet-10-local`. It replaced the Mac mini's `dotnet-10` runner on 2026-10-09: the workflows were
first run side by side for a PR, and the Mac mini copies (`ci-pr.yaml`, `ci-main.yaml`) were then
deleted. `sync-github.yaml` kept naming `dotnet-10` until 2026-11-16, so its first run after the
move (1642) waited for a runner that was gone; it now runs on `dotnet-10-local` too.

The consequence is that **CI is only as available as that desktop.** With the PC off, or Docker Desktop
stopped, `validate` stays queued and the required check never reports. See the failure-mode table.

The required status check on `main` is built from the caller workflow's `name:` (`CI (Pull Request)`),
its job id (`ci`) and the called job's name (`validate`), so renaming any of them strands every PR.
Before `ci-shared.yaml` existed it was `CI (Pull Request) / validate (pull_request)`; with the
reusable workflow the text is longer and Gitea decides the exact form, so **read it off the first
finished run's commit status** and put that string in branch protection, replacing the old one. Until
that is done the old check never reports.

The runner is not in the repo. It is a `gitea/act_runner` container (`huddle-local-runner`) defined
by a `compose.yaml` and a `config.yaml` in `C:\Users\user\gitea-runner\`, registered to this repo only
with a token from `POST /actions/runners/registration-token`. Labels and options live in
`config.yaml`, which overrides the label environment variable on `compose.yaml`.

**The runner config must give job containers a RAM-backed `/tmp`:** `container.options:
"--tmpfs /tmp:exec"`. Without it the suite takes about 7 minutes instead of 40 seconds.
`PersonaSpendTests.ConcurrentAdds_SumExactly` makes 48,000 SQLite writes, each flushed to disk, and a
flush through Docker Desktop's WSL2 disk costs 3-10 ms, against 0.001 ms in RAM. The Mac mini did not
pay that cost: macOS acknowledges a flush without waiting for the SSD (unverified). Measured on
2026-10-09, `validate` took 112 s on this runner against 254 s on the Mac mini, with the tests at 38 s
against 159 s. If you recreate the runner, set the option first and confirm the test step takes
well under a minute.

## Known failure modes

Check these before reading the code.

| Symptom | Cause | Fix |
| --- | --- | --- |
| `Requested SDK version: 11.0.100 ... Install the [11.0.100] .NET SDK` at the restore step | `global.json` moved ahead of the image pinned in the workflows, so the container's SDK no longer satisfies `rollForward: latestPatch` | Bump `image:` in **both** workflows to a tag whose SDK matches. Do not loosen `global.json` to chase the image, and do not go back to the floating `:10.0` tag |
| Six `AgentProcessLauncherTests` and the `ComposerScriptTests` fail with `Failed to start 'node'` | The Install Node.js step was removed, reordered after the test step, or its `apt-get` failed | Restore the step; confirm `node --version` printed in the log |
| `Zero tests ran`, job exits 5, and the step reads as a no-op rather than a failure | The trailing `--` was dropped from `dotnet test` | Put it back. This SDK's Microsoft Testing Platform CLI requires it; it is not a typo |
| `The following test projects are using VSTest test runner` | Restore assets are stale or missing, so the `xunit.v3` props never imported and the test projects evaluated as `Library` instead of `Exe`. The message names the wrong cause. | Delete `bin/` and `obj/`, then restore again in the same container as the build |
| `secret-scan` fails on `internal-mdns-host` | A real `*.local` hostname reached a tracked file — most often a doc or a workflow comment | Replace it with a `*.example` placeholder, or allowlist the path in `.gitleaks.toml` |
| The test total is not the figure at the top of this file | A test was added or removed, or a `--filter-not-method` line no longer matches anything | Expected after real work; confirm the delta is yours. A quarantine line that matches nothing fails silently — it does not error. Update the figure when you confirm it |
| A diagnostic appears only in CI, with an identical clean local build | The container's SDK is **ahead** of the local one. `global.json` pins `11.0.100` with `rollForward: latestPatch`, so a newer patch of the image satisfies it and compiles with a newer Roslyn. `TreatWarningsAsErrors` turns any diagnostic that version added into a failed build | Do not chase it by loosening `global.json`. Reproduce it with the Docker command below, which uses the same image, or install the image's SDK locally. Seen 2026-09-15: `CS1574` on a `cref` to an internal framework type, which 10.0.400 accepted |
| A local build fails with `MSB3021` or `MSB3027`: `Could not copy apphost.exe to Huddle.App.exe`, `The file is locked by Huddle.App (<pid>)` | The Human is running the app (it listens on port 1113) and the process holds its own `bin` output open. It is not a code error and a retry will not help | Check before building: `Get-Process Huddle.App -ErrorAction SilentlyContinue`. If it is running, ask the Human once, up front, to stop it, and do not stop it yourself (the auto-mode classifier refused it once as interfering with their work). Seen in two Huddle3 sessions, each costing an extra approval round trip |
| `validate` sits queued and never starts | The runner is offline: the desktop is off or asleep, Docker Desktop is stopped, or the `huddle-local-runner` container is not running. Gitea shows it under Settings → Actions → Runners | Start Docker Desktop and run `docker compose up -d` in `C:\Users\user\gitea-runner\`. The container restarts on its own once Docker is up |
| The test step takes about 7 minutes and `PersonaSpendTests.ConcurrentAdds_SumExactly` dominates it | The runner lost its RAM-backed `/tmp`: `config.yaml` was regenerated or not mounted, so jobs write SQLite to Docker's disk | Restore `container.options: "--tmpfs /tmp:exec"` in `config.yaml` and the `labels:` entry beside it, then restart the container |
| A docs-only PR shows a check that never completes | Only an old branch can do this now: it still carries the pre-`ci-shared.yaml` workflows with `paths-ignore: docs/**`, so no run is queued | Merge `main` into the branch |
| `pwsh: command not found` at the Health endpoint smoke test step | The SDK image stopped shipping PowerShell. `11.0.100` (PowerShell 7.7.0) installs it as a global tool and symlinks `/usr/bin/pwsh` (`PowerShell.Linux.arm64` on this runner), but that is the image's choice, not a guarantee across bumps | Add a step before it — `dotnet tool install --global PowerShell` and put `$HOME/.dotnet/tools` on `PATH` — in **both** workflows. Confirm with `command -v pwsh && pwsh --version` |
| The smoke test FAILs while the whole test suite is green | Read the stderr and stdout tails the script prints; that is what they are for. `AddressInUseException`, a missing `libe_sqlite3.so`, or a timeout waiting for `Now listening on:` are environmental — the app never bound. A `404 - the endpoint is not mapped` is **not** environmental | For 404, `app.MapHealthChecks("/health")` was removed from `Program.cs` and this step caught a real regression the suite cannot see. For the rest, re-run; if it persists, reproduce with the Docker command above |
| The build step fails with `error MSB3883: Unexpected exception` and `ReleaseMutex failed. WaitOne Id: N Release Id: N`, on a project the change does not touch, with no `CS` diagnostic | The Roslyn compiler server (`VBCSCompiler`) lost a race on its own mutex. It is the runner, not the code. Seen once, on 2026-10-18 (run 1014, job 1702), on `Huddle.Contracts` | Re-run the job. If it recurs, add `-p:UseSharedCompilation=false` to `dotnet build` in **both** workflows, which trades the server's speed for no shared state |
| Several tests that host the app (`TeamWebApplicationFactory`: `AgentsPageTests`, `TasksPageTests`, `AvatarEndpointTests`) fail with `TaskCanceledException`, `The client aborted the request`, each after 12-38 s, and the `Huddle.Tests` step takes about twice its usual ~1 m 55 s | The runner was starved for CPU, so app start-up plus one request outran the tests' 10-second `CancellationTokenSource`. The runner shares the desktop with everything else running there. A plain avatar 404 failing the same way is the tell, because it renders no component. Seen on 2026-10-18 (run 1014, job 1704): 9 failures in a 4 m 00 s step, then green in 1 m 58 s on re-run (job 1706) with no change. These tests run in `Huddle.Tests.Hosted` at `maxParallelThreads: 2` | Check the step's duration first. If it is about double the usual, re-run once the desktop is quiet. If these fail at a normal duration, it is real: the request never completed, so read the first failure's stack rather than raising the budget |
| `RoomSessionResumeTests.Start_SeedsTheContextMeterFromAResumableEntry` fails in about 200 ms with `IOException: Broken pipe` on the Hello write, or `Connection reset by peer` on the Welcome read, at `PersonaRunner.RegisterAsync`. The runner is a second one, started right after the first was disposed. Or a `PersonaSupervisorLifecycleTests` case times out at 10 s waiting for `RunningHostCount` | A product race, fixed 2026-11-01. `PipeServer` created the next instance only after `AgentConnection.RunAsync` returned, and `RunAsync` runs synchronously until an await is pending. Under load the first runner's whole connection ran synchronously, from Hello to the EOF of its dispose. The instance it disposed was the last owner of the shared Unix listening socket, so `SharedServer` closed it. The second runner had already connected into the kernel backlog, so it got `Broken pipe` or `Connection reset`. The client was never accepted, so `AgentConnection` logged nothing. The same race caused `PersonaSupervisorLifecycleTests.OnePersonaFailingToStart_DoesNotStopTheOthers` (run 1251): `good` was reset while `bad` disposed, and a failed start is not retried. Windows only makes the connect wait. Reproduced 14 times in 752 class runs in the container under 16-24-process load, then none in 1,728 after the fix | `PipeAcceptLoop` creates the next instance before it hands off the connected one; `PipeAcceptLoopTests` pins it (a mutant gives `Broken pipe` on Linux and a connect timeout on Windows). The pre-Welcome Warning `AgentConnection` logs since run 1246 cannot see this race, because the reset client was never accepted. If this symptom returns, check that ordering first |

### Line endings

The index stores LF and a Windows worktree checks out CRLF (`core.autocrlf=true`), so the
Linux runner compiles bytes you never compile locally. **Something does depend on that**, as of
2026-09-15, and it was live for longer than anyone noticed: C# raw string literals *preserve*
their source file's line endings rather than normalising them, so `PromptCatalog`'s defaults —
and therefore every prompt sent to a model — carried `\r\n` on a Windows build and `\n` in this
container. Two tests read source files as text (`AgentsRazorSourceTests` over
`Agents.razor`, and `AcpReferenceTests`) and are the shape that would notice, but neither
covers prompts; `PromptGoldenTests` and `PromptDefaultsFileTests` both normalise line endings on
*both* sides before comparing, so neither could see it either.

`PromptDefinition.Default` now normalises to `\n` once at construction, which makes model-facing
text independent of the checkout. `docs/engineering/traps.md` asserted the opposite mechanism
until 2026-09-15 and has been corrected; this section was right and that one was wrong, which is
worth knowing if the two ever disagree again.

If a test passes on Windows and fails in CI on a string comparison, flip the line endings
locally and rebuild before theorizing:

```bash
git ls-files --eol -- 'src/**/*.razor'
```

```text
i/lf    w/crlf  attr/                  src/Huddle.App/Components/Pages/Agents.razor
```

The sibling Agency repo added a root `.gitattributes` forcing `*.cs text eol=crlf` after
exactly this class of bug cost it a debugging session. This repo considered one on 2026-09-15
and **declined it deliberately**: normalising at the boundary, in `PromptDefinition`, makes the
property true however the repo is checked out, whereas a `.gitattributes` only makes every
checkout agree and would rewrite line endings in everyone's working tree on the next pull.
Revisit it if a second consumer of source-file bytes appears — the code-level fix does not
generalise, and two of them would be a pattern rather than a fix.

## Reflection — 2026-09-12: the first run died on a cached image

**Failure:** run 581, the very first CI run this repo ever had (PR #1, the commit that added
these workflows). `secret-scan` passed; `validate` failed at "Restore dependencies" with
`Requested SDK version: 10.0.400 ... Installed SDKs: 10.0.300`.

**Root cause:** the workflows asked for the floating `mcr.microsoft.com/dotnet/sdk:10.0` tag,
and the runner had a cached image from that tag holding SDK **10.0.300**. `global.json` pins
`10.0.400` with `rollForward: latestPatch`, which rolls forward only inside the `10.0.4xx`
feature band, so 10.0.300 does not satisfy it. Nothing about the code was wrong — no compile
ran at all.

**Proof:** the same failure reproduced locally before the PR was even opened. A stale local
`sdk:10.0` (10.0.301) refused the restore with the identical message; `docker pull` brought
10.0.401 and every subsequent local run was green. The CI log then showed the same message
with 10.0.300, one patch band lower still.

**Fix:** pinned both jobs in both workflows to `mcr.microsoft.com/dotnet/sdk:10.0.401`. An
exact tag the runner does not have forces a pull, so the SDK CI uses is stated in the repo
rather than inherited from whatever the host last cached.

**Lessons for future agents:**

- A floating image tag is not a version. Two machines on `:10.0` can hold SDKs a whole feature
  band apart, and `rollForward: latestPatch` turns that gap into a hard failure.
- `global.json` and the workflow `image:` are two halves of one decision. Bumping either alone
  breaks CI, and the error names `global.json` — the file that is *correct* — which points
  debugging at the wrong half.
- Everything upstream of the failing step is still evidence. This run confirmed the
  `dotnet-10` runner label (the Mac mini, since replaced by `dotnet-10-local`), the hand-rolled clone, and Gitea's `GITHUB_SHA` handling on
  `pull_request` all work, because `secret-scan` passed end to end and Node installed cleanly.

## Reading CI results from a session

You do not need a browser to investigate a run, and you should not ask the user for a
credential that is already on the machine.

**The token is the `GITEA_ACCESS_TOKEN` environment variable**, set at Windows **User**
scope. Any shell started after it was set inherits it, so it is simply present — read it
from the environment and never prompt for it. It is deliberately *not* in git config: the
`Create-PR` skill mentions `git config gitea.token` as a fallback, and that fallback is not
configured in this repo.

> [!CAUTION]
> Never write the value into a file, a commit message, a PR body, or a log line. Pass it only
> as a header. The `secret-scan` job exists to catch exactly this, and it gates every PR.

This works as-is, from the repo root:

```bash
# Derive the host from the remote. Never hardcode it — a literal internal *.local name in a
# tracked file trips .gitleaks.toml's internal-mdns-host rule and fails the build.
REMOTE=$(git remote get-url origin)          # http://<host>/<owner>/<repo>.git
BASE=${REMOTE%.git}
API="$(echo "$BASE" | cut -d/ -f1-3)/api/v1/repos/$(echo "$BASE" | cut -d/ -f4)/$(echo "$BASE" | cut -d/ -f5)"

curl -sS -H "Authorization: token $GITEA_ACCESS_TOKEN" "$API/actions/runs?limit=1"
```

```text
{"total_count":1,"workflow_runs":[{"id":585,"event":"push","status":"completed",
"conclusion":"success", ...}]}
```

### Endpoints that work

All are relative to the `$API` above, and all take the same `Authorization: token …` header.

| Call | Endpoint |
| --- | --- |
| Recent runs | `GET /actions/runs?limit=N` |
| Jobs in a run | `GET /actions/runs/{run_id}/jobs` |
| A job's full log | `GET /actions/jobs/{job_id}/logs` |
| Re-run one job | `POST /actions/runs/{run_id}/jobs/{job_id}/rerun` |
| Open a pull request | `POST /pulls` with `{title, body, head, base}` |

Fetch the log and grep it; do not scrape the web UI. A failing `validate` job's log carries
the `::group::` marker for every step, so `grep -n "::group::\|Failure - Main"` gives you the
step boundaries and the one that died in a single pass.

### Three API traps already hit

- **`/actions/tasks` reports a `total_count` but returns an empty list.** It looks like the
  run has no jobs. Use `/actions/runs/{run_id}/jobs` instead, which is populated.
- **`/actions/runs/{run_id}/rerun` returns 400 `this workflow run is not done`** whenever any
  job in the run is still going. The per-job form above works regardless, and re-running a
  single job is what you want for a suspected flake anyway.
- **A run can report `in_progress` while the failure is already in the log.** If someone says
  CI failed and the API disagrees, fetch the running job's log rather than waiting.

## Related

- [C# Principles](CSharpPrinciples.md) — the house style the build enforces
- [Testing](../docs/engineering/testing.md) — how the suite is built, and the manual checklist
  covering what no test can prove
- [Huddle.EngineeringGuide.md](../docs/Huddle.EngineeringGuide.md) — the hub for the chat surface
