# CI Pipeline

How Gitea Actions validates this repo, and how to reproduce a failing run on your own
machine in about two minutes. Read this before debugging a red run — the failure modes
recorded here are environmental, and none of them are code regressions.

Verified end to end on 2026-09-21 against SDK 10.0.401 in the CI container: **1329 tests,
1319 passed, 10 skipped, 0 failed** (run 658, the PR that added the health-endpoint smoke
test). That is the whole suite minus the three quarantined tests below; an unfiltered run of
the same tree is 1332.

The figures in this file were **970/962/8 until 2026-09-21**, and **656/648/659 before
2026-09-15**. Refresh them when you next read a green run's summary rather than trusting them —
a count nobody updates stops being a check and becomes noise.

These totals move whenever real work lands — they were 565/568 when this page was written,
before the Teams change added 91 tests. Treat a changed total as something to *confirm*,
not something to fear; see [Known failure modes](#known-failure-modes).

This page covers validation only. There is no packaging, publishing, or release step yet;
when one arrives it belongs in a new job in `ci-main.yaml`, not inside `validate`.

## Topology

> [!WARNING]
> `gitea-host.example` below is a redacted placeholder, not a name that resolves. The real
> internal hostname is kept out of this file because the `secret-scan` job fails the build
> on an `internal-mdns-host` finding. Read the real one from `git remote -v`.

Both workflows run on a self-hosted runner labelled `dotnet-10`, inside the
`mcr.microsoft.com/dotnet/sdk:10.0.401` container (Ubuntu 24.04), with `shell: bash`.

**The runner is `arm64`.** Test output reads `net10.0|arm64`, where a Windows development box
here is `x64`. That is a second axis, alongside line endings and the SDK patch below, on which a
green local run does not imply a green CI run — and it is the axis timing races are most
sensitive to, since core count and memory ordering both differ.

The image is pinned to an **exact SDK patch**, not the floating `:10.0` tag, and that pin is
half of a pair: `global.json` requires `10.0.400` with `rollForward: latestPatch`, so only a
`10.0.4xx` SDK satisfies it. A floating tag leaves you at the mercy of whatever the runner
happens to have cached — which is what broke the very first run on this repo (see
[Reflection](#reflection--2026-09-12-the-first-run-died-on-a-cached-image)). Bump the two
together, or not at all.

| Workflow | Trigger | Jobs |
| --- | --- | --- |
| `.gitea/workflows/ci-pr.yaml` | `pull_request` into `main` | `secret-scan`, `validate` |
| `.gitea/workflows/ci-main.yaml` | `push` to `main` | `secret-scan`, `validate` |

Both skip runs whose changes are confined to `docs/**`. A docs-only PR therefore produces
**no run at all** — if you make these checks required in branch protection, such a PR will
sit with a permanently pending check.

The two files' job definitions are byte-identical by construction. Keep them that way:

```bash
diff <(sed -n '/^jobs:/,$p' .gitea/workflows/ci-pr.yaml) \
     <(sed -n '/^jobs:/,$p' .gitea/workflows/ci-main.yaml)
```

```text
(no output — the jobs match)
```

### What validate does

Checkout, install Node.js, restore, vulnerable-dependency scan, build, test.

Two of those steps exist for reasons specific to this repo:

- **Install Node.js.** `AgentProcessLauncherTests` (marked `[Trait("Category", "Process")]`)
  launches a real `node -e` subprocess to prove the stdio pipe wiring works. The SDK image
  ships no Node, and without this step those six tests fail with `Failed to start 'node'`.
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
  gets exactly one attempt and a failure is a failure. The two known races that would
  otherwise justify retries are quarantined by name instead (below), which keeps the rest of
  the suite strict — a blanket retry would also have masked a genuine regression.
- **No `actions/checkout`, and no other JavaScript action.** Actions of that kind need Node
  in the container, and Node only arrives partway through `validate`. Both jobs clone by hand
  with a token-injected URL and then check out `$GITHUB_SHA`, so every run starts from a
  fresh clone with no warm working tree.

### Quarantined tests

Three tests are excluded by name in the test step. They are **quarantined, not fixed**, and
both underlying races are recorded in
[known-limits.md](../docs/agencyteam/known-limits.md) as pre-existing and undiagnosed.

> [!IMPORTANT]
> **A fourth test shows the same race and is not quarantined.** On 2026-09-15, run 607 failed on
> `DotAcpConcurrentHostTests.TwoHosts_ConcurrentPrompts_EachSessionOnlySeesItsOwnAgentsUpdates`
> with `Expected: MessageChunk … Actual: TurnCompleted` — event ordering over the fake transport,
> the same shape as the two `DotAcpAgentSessionTests` entries below. It passed on a re-run of the
> single job. The change under test touched no file under `src/Huddle.Acp` or
> `tests/Huddle.Acp.Tests`, and that test file is unchanged since the initial commit, so it is not
> attributable to the work it failed against. It is left unquarantined on purpose: the race is the
> ACP effort's to diagnose, and a third filter line would make it easier to forget than to fix.

| Test | Rate | Race |
| --- | --- | --- |
| `PersonaSupervisorTests.Shutdown_DisposesEveryHost` | ~1 run in 4 | Its 10-second token races `WaitUntilAsync`. A timing bug in the test, not in `PersonaSupervisor`. |
| `DotAcpAgentSessionTests.PromptAsync_StreamsChunksInOrder_ThenTurnCompleted` | ~1 run in 5 | Event ordering over the fake transport. Passes on rerun. |
| `DotAcpAgentSessionTests.PromptAsync_ThoughtAndToolCallEvents_ArePublished` | seen once | Same class, same fake transport. Run 582 received `TurnCompleted` where `ToolCallStarted` was expected — the two tool-call notifications never arrived. |

Left unquarantined, those two rates compound to roughly **40% of runs red** for reasons
unrelated to the change under test, which is how a team learns to ignore CI.

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
  -w /work -e TEAM_E2E=0 mcr.microsoft.com/dotnet/sdk:10.0.401 bash -c '
    set -euo pipefail
    apt-get update -qq && apt-get install -y -qq nodejs
    dotnet restore Huddle.slnx
    dotnet build   Huddle.slnx --configuration Release --no-restore
    dotnet test    Huddle.slnx --configuration Release --no-build -- \
      --filter-not-method "*.Shutdown_DisposesEveryHost" \
      --filter-not-method "*.PromptAsync_StreamsChunksInOrder_ThenTurnCompleted" \
      --filter-not-method "*.PromptAsync_ThoughtAndToolCallEvents_ArePublished"
    pwsh -NoProfile -NonInteractive -File ./test-health.ps1 -Configuration Release -TimeoutSeconds 120
  '
```

Drop the three `--filter-not-method` lines to run the full 1332 including the quarantined
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
`tests/Huddle.Tests` can check — they all host the app in-process. It builds nothing, so it has
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

## Known failure modes

Check these before reading the code.

| Symptom | Cause | Fix |
| --- | --- | --- |
| `Requested SDK version: 10.0.400 ... Install the [10.0.400] .NET SDK` at the restore step | `global.json` moved ahead of the image pinned in the workflows, so the container's SDK no longer satisfies `rollForward: latestPatch` | Bump `image:` in **both** workflows to a tag whose SDK matches. Do not loosen `global.json` to chase the image, and do not go back to the floating `:10.0` tag |
| Six `AgentProcessLauncherTests` fail with `Failed to start 'node'` | The Install Node.js step was removed, reordered after the test step, or its `apt-get` failed | Restore the step; confirm `node --version` printed in the log |
| `Zero tests ran`, job exits 5, and the step reads as a no-op rather than a failure | The trailing `--` was dropped from `dotnet test` | Put it back. This SDK's Microsoft Testing Platform CLI requires it; it is not a typo |
| `The following test projects are using VSTest test runner` | Restore assets are stale or missing, so the `xunit.v3` props never imported and the test projects evaluated as `Library` instead of `Exe`. The message names the wrong cause. | Delete `bin/` and `obj/`, then restore again in the same container as the build |
| `secret-scan` fails on `internal-mdns-host` | A real `*.local` hostname reached a tracked file — most often a doc or a workflow comment | Replace it with a `*.example` placeholder, or allowlist the path in `.gitleaks.toml` |
| The test total is not the figure at the top of this file | A test was added or removed, or a `--filter-not-method` line no longer matches anything | Expected after real work; confirm the delta is yours. A quarantine line that matches nothing fails silently — it does not error. Update the figure when you confirm it |
| A diagnostic appears only in CI, with an identical clean local build | The container's SDK is **ahead** of the local one. `global.json` pins `10.0.400` with `rollForward: latestPatch`, so the `10.0.401` image satisfies it and compiles with a newer Roslyn. `TreatWarningsAsErrors` turns any diagnostic that version added into a failed build | Do not chase it by loosening `global.json`. Reproduce it with the Docker command below, which uses the same image, or install the image's SDK locally. Seen 2026-09-15: `CS1574` on a `cref` to an internal framework type, which 10.0.400 accepted |
| A docs-only PR shows a check that never completes | Both workflows set `paths-ignore: docs/**`, so no run is queued at all | Push a non-docs change, or drop the required check for such PRs |
| `pwsh: command not found` at the Health endpoint smoke test step | The SDK image stopped shipping PowerShell. `10.0.401` installs it as a global tool and symlinks `/usr/bin/pwsh` (`PowerShell.Linux.arm64` on this runner), but that is the image's choice, not a guarantee across bumps | Add a step before it — `dotnet tool install --global PowerShell` and put `$HOME/.dotnet/tools` on `PATH` — in **both** workflows. Confirm with `command -v pwsh && pwsh --version` |
| The smoke test FAILs while the whole test suite is green | Read the stderr and stdout tails the script prints; that is what they are for. `AddressInUseException`, a missing `libe_sqlite3.so`, or a timeout waiting for `Now listening on:` are environmental — the app never bound. A `404 - the endpoint is not mapped` is **not** environmental | For 404, `app.MapHealthChecks("/health")` was removed from `Program.cs` and this step caught a real regression the suite cannot see. For the rest, re-run; if it persists, reproduce with the Docker command above |

### Line endings

The index stores LF and a Windows worktree checks out CRLF (`core.autocrlf=true`), so the
Linux runner compiles bytes you never compile locally. **Something does depend on that**, as of
2026-09-15, and it was live for longer than anyone noticed: C# raw string literals *preserve*
their source file's line endings rather than normalising them, so `PromptCatalog`'s defaults —
and therefore every prompt sent to a model — carried `\r\n` on a Windows build and `\n` in this
container. Two tests read source files as text (`TeammatesRazorSourceTests` over
`Teammates.razor`, and `AcpReferenceTests`) and are the shape that would notice, but neither
covers prompts; `PromptGoldenTests` and `PromptDefaultsFileTests` both normalise line endings on
*both* sides before comparing, so neither could see it either.

`PromptDefinition.Default` now normalises to `\n` once at construction, which makes model-facing
text independent of the checkout. `docs/agencyteam/traps.md` asserted the opposite mechanism
until 2026-09-15 and has been corrected; this section was right and that one was wrong, which is
worth knowing if the two ever disagree again.

If a test passes on Windows and fails in CI on a string comparison, flip the line endings
locally and rebuild before theorizing:

```bash
git ls-files --eol -- 'src/**/*.razor'
```

```text
i/lf    w/crlf  attr/                  src/Huddle.App/Components/Pages/Teammates.razor
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
  `dotnet-10` runner label, the hand-rolled clone, and Gitea's `GITHUB_SHA` handling on
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
- [Testing](../docs/agencyteam/testing.md) — how the suite is built, and the manual checklist
  covering what no test can prove
- [AgencyTeam.md](../docs/AgencyTeam.md) — the hub for the chat surface
