# CI Pipeline

How Gitea Actions validates this repo, and how to reproduce a failing run on your own
machine in about two minutes. Read this before debugging a red run — the failure modes
recorded here are environmental, and none of them are code regressions.

Verified end to end on 2026-09-12 against SDK 10.0.401 in the CI container: **565 tests,
557 passed, 8 skipped, 0 failed**. That is the whole suite minus the three quarantined tests
below; an unfiltered run of the same tree is 568.

This page covers validation only. There is no packaging, publishing, or release step yet;
when one arrives it belongs in a new job in `ci-main.yaml`, not inside `validate`.

## Topology

> [!WARNING]
> `gitea-host.example` below is a redacted placeholder, not a name that resolves. The real
> internal hostname is kept out of this file because the `secret-scan` job fails the build
> on an `internal-mdns-host` finding. Read the real one from `git remote -v`.

Both workflows run on a self-hosted runner labelled `dotnet-10`, inside the
`mcr.microsoft.com/dotnet/sdk:10.0.401` container (Ubuntu 24.04), with `shell: bash`.

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
  otherwise justify retries are quarantined by name instead (below), which keeps the other
  565 strict — a blanket retry would also have masked a genuine regression.
- **No `actions/checkout`, and no other JavaScript action.** Actions of that kind need Node
  in the container, and Node only arrives partway through `validate`. Both jobs clone by hand
  with a token-injected URL and then check out `$GITHUB_SHA`, so every run starts from a
  fresh clone with no warm working tree.

### Quarantined tests

Three tests are excluded by name in the test step. They are **quarantined, not fixed**, and
both underlying races are recorded in
[known-limits.md](../docs/agencyteam/known-limits.md) as pre-existing and undiagnosed:

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
  -w /work -e TEAM_E2E=0 mcr.microsoft.com/dotnet/sdk:10.0.401 bash -c "
    set -euo pipefail
    apt-get update -qq && apt-get install -y -qq nodejs
    dotnet restore Huddle.slnx
    dotnet build   Huddle.slnx --configuration Release --no-restore
    dotnet test    Huddle.slnx --configuration Release --no-build --       --filter-not-method "*.Shutdown_DisposesEveryHost"       --filter-not-method "*.PromptAsync_StreamsChunksInOrder_ThenTurnCompleted"       --filter-not-method "*.PromptAsync_ThoughtAndToolCallEvents_ArePublished"
  "
```

Drop the three `--filter-not-method` lines to run the full 568 including the quarantined
tests — worth doing when you are trying to reproduce one of the races on purpose.

```text
Test run summary: Passed!
  total: 565
  failed: 0
  succeeded: 557
  skipped: 8
```

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
| The test total is not 565 | A test was added or removed, or a `--filter-not-method` line no longer matches anything | Expected after real work; confirm the delta is yours. A quarantine line that matches nothing fails silently — it does not error |
| A docs-only PR shows a check that never completes | Both workflows set `paths-ignore: docs/**`, so no run is queued at all | Push a non-docs change, or drop the required check for such PRs |

### Line endings

The index stores LF and a Windows worktree checks out CRLF (`core.autocrlf=true`), so the
Linux runner compiles bytes you never compile locally. As of 2026-09-12 nothing in the suite
depends on that — the full run is green at LF — but two tests read source files as text
(`TeammatesRazorSourceTests` over `Teammates.razor`, and `AcpReferenceTests`), and those are
the shape that would notice. If a test passes on Windows and fails in CI on a string
comparison, flip the line endings locally and rebuild before theorizing:

```bash
git ls-files --eol -- 'src/**/*.razor'
```

```text
i/lf    w/crlf  attr/                  src/Huddle.App/Components/Pages/Teammates.razor
```

The sibling Agency repo added a root `.gitattributes` forcing `*.cs text eol=crlf` after
exactly this class of bug cost it a debugging session. This repo has not needed one.

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

## Related

- [C# Principles](CSharpPrinciples.md) — the house style the build enforces
- [Testing](../docs/agencyteam/testing.md) — how the suite is built, and the manual checklist
  covering what no test can prove
- [AgencyTeam.md](../docs/AgencyTeam.md) — the hub for the chat surface
