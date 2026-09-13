# CI Pipeline

How Gitea Actions validates this repo, and how to reproduce a failing run on your own
machine in about two minutes. Read this before debugging a red run — the failure modes
recorded here are environmental, and none of them are code regressions.

Verified end to end on 2026-09-12 against SDK 10.0.401 in the CI container: **593 tests,
585 passed, 8 skipped, 0 failed** — identical to a Windows `dotnet test` run.

This page covers validation only. There is no packaging, publishing, or release step yet;
when one arrives it belongs in a new job in `ci-main.yaml`, not inside `validate`.

## Topology

> [!WARNING]
> `gitea-host.example` below is a redacted placeholder, not a name that resolves. The real
> internal hostname is kept out of this file because the `secret-scan` job fails the build
> on an `internal-mdns-host` finding. Read the real one from `git remote -v`.

Both workflows run on a self-hosted runner labelled `dotnet-10`, inside the
`mcr.microsoft.com/dotnet/sdk:10.0` container (Ubuntu 24.04), with `shell: bash`.

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
  its functional suite talks to a live model. This suite is offline and deterministic, so a
  retry would hide a genuine flake instead of surfacing it. If timing-sensitive tests start
  flaking on a loaded runner, fix the test rather than adding attempts.
- **No `actions/checkout`, and no other JavaScript action.** Actions of that kind need Node
  in the container, and Node only arrives partway through `validate`. Both jobs clone by hand
  with a token-injected URL and then check out `$GITHUB_SHA`, so every run starts from a
  fresh clone with no warm working tree.

## Reproducing a CI run locally

This is the whole pipeline, on your machine, in the same container CI uses. It needs Docker
and about two minutes. Run it from the repo root.

```bash
# A fresh container each time, but a named volume keeps the NuGet cache warm between runs.
docker volume create huddle-nuget

docker run --rm -v "$PWD:/work" -v huddle-nuget:/root/.nuget/packages \
  -w /work -e TEAM_E2E=0 mcr.microsoft.com/dotnet/sdk:10.0 bash -c "
    set -euo pipefail
    apt-get update -qq && apt-get install -y -qq nodejs
    dotnet restore Huddle.slnx
    dotnet build   Huddle.slnx --configuration Release --no-restore
    dotnet test    Huddle.slnx --configuration Release --no-build --
  "
```

```text
Test run summary: Passed!
  total: 593
  failed: 0
  succeeded: 585
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
| `Requested SDK version: 10.0.400 ... Install the [10.0.400] .NET SDK` at the restore step | `global.json` pins `10.0.400` with `rollForward: latestPatch`, which only accepts a `10.0.4xx` SDK. The runner's cached image is older. | `docker pull mcr.microsoft.com/dotnet/sdk:10.0` on the runner host. Do not edit `global.json` to chase the image. |
| Six `AgentProcessLauncherTests` fail with `Failed to start 'node'` | The Install Node.js step was removed, reordered after the test step, or its `apt-get` failed | Restore the step; confirm `node --version` printed in the log |
| `Zero tests ran`, job exits 5, and the step reads as a no-op rather than a failure | The trailing `--` was dropped from `dotnet test` | Put it back. This SDK's Microsoft Testing Platform CLI requires it; it is not a typo |
| `The following test projects are using VSTest test runner` | Restore assets are stale or missing, so the `xunit.v3` props never imported and the test projects evaluated as `Library` instead of `Exe`. The message names the wrong cause. | Delete `bin/` and `obj/`, then restore again in the same container as the build |
| `secret-scan` fails on `internal-mdns-host` | A real `*.local` hostname reached a tracked file — most often a doc or a workflow comment | Replace it with a `*.example` placeholder, or allowlist the path in `.gitleaks.toml` |
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
i/lf    w/crlf  attr/                 	src/Huddle.App/Components/Pages/Teammates.razor
```

The sibling Agency repo added a root `.gitattributes` forcing `*.cs text eol=crlf` after
exactly this class of bug cost it a debugging session. This repo has not needed one.

## Related

- [C# Principles](CSharpPrinciples.md) — the house style the build enforces
- [Testing](../docs/agencyteam/testing.md) — how the suite is built, and the manual checklist
  covering what no test can prove
- [AgencyTeam.md](../docs/AgencyTeam.md) — the hub for the chat surface
