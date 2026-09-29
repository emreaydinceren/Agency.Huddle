# Agency.Huddle

The product is **Agency.Huddle**. Two identifiers still carry the old code name
`Team` and must not be renamed to match the brand: the `mcp__team__` tool prefix
(model-facing prompt text in 12 code files and every persona; a test pins it; no
brand benefit) and the `Team:` config root (a breaking change for any running
install). Everything else moved: namespaces moved to `Agency.Huddle.*` on
2026-09-12 — the separate, compiler-verified decision — and projects, assemblies,
folders and the solution followed, so the project `Huddle.App` declares
`Agency.Huddle.App`, and the two former solution files are now the single
`Huddle.slnx` at the repo root.

## Orientation

`docs/AgencyTeam.md` is the source of truth for the chat surface. It is a hub:
read it, then follow only the rows in its map that your task needs. Two of those
pages are binding — `docs/agencyteam/rules.md` before editing `src/Huddle.App`,
and `docs/agencyteam/traps.md` before touching `Huddle.Acp`, `Huddle.Contracts` or
the wire protocol.

Do **not** orient from `README.md`. It belongs to the ACP effort, and its chat
section is several milestones behind — it still reports the app as "not yet
functional" and links a spec that no longer exists.

## Two projects, one build root

Both efforts build from the single solution `Huddle.slnx` at the repo root.
Beyond that they are independently owned; neither side edits the other's
subtree, and root files are shared — announce changes before making them.

| | ACP effort | Chat surface |
| --- | --- | --- |
| Source | `src/Huddle.Acp`, `src/Huddle.Console` | `src/Huddle.App`, `src/Huddle.Contracts`, `src/Huddle.MockAdapter` |
| Tests | `tests/Huddle.Acp.Tests` | `tests/Huddle.Tests` |
| Docs | `docs/acp/**`, `README.md` | `docs/AgencyTeam.md`, `docs/agencyteam/**`, `docs/adr/**`, `docs/Huddle.Adapters-*.md` |

`src/Huddle.MockAdapter` (assembly `mock-acp`) is the one place the two subtrees touch by
design: it **links** `FakeAcpAgent.cs`, `PromptContext.cs` and `FakeRpcError.cs` out of
`tests/Huddle.Acp.Tests/Fakes/` with `<Compile Include=… Link=…>` rather than copying them, so
there stays one implementation of what an ACP agent does and it is the ACP effort's. **Editing
any of those three files changes two assemblies** — verify `Huddle.Acp.Tests` *and*
`tests/Huddle.Tests/MockAdapter/` before concluding.

## C# code

Whenever you write or edit a C# file in this repo (`.cs`, `.csx`, `.razor`, `.cshtml`),
follow `agents/CSharpPrinciples.md`. It is not advisory — it defines the house style for
this solution, and a `PreToolUse` hook in `.claude/settings.json` re-states it on every
C# write or edit.

Warnings are errors and nullable is on (`Directory.Build.props`). Package versions
live only in `Directory.Packages.props` — a `Version` on a `PackageReference` is an
error. The solution has tests, and the trailing `--` is required or the run
reports "Zero tests ran" and reads as a no-op:

```powershell
dotnet build Huddle.slnx
dotnet test  Huddle.slnx --
```

## CI

Gitea Actions validates every PR into `main` and every push to `main`: restore,
build and test `Huddle.slnx` (trailing `--` included), plus a gitleaks secret scan
and a vulnerable-dependency check. The workflows are `.gitea/workflows/ci-pr.yaml`
and `ci-main.yaml`; their `validate` jobs are byte-identical on purpose, so change
both or neither. `.gitea/**` and `.gitleaks.toml` are shared root-level files —
announce changes the same way as any other.

Do **not** run the Linux Docker repro as a routine check, and do not add it to a delivery
plan's verification steps: a run rebuilds the whole solution from a clean copy, takes 4-20
minutes and a lot of tokens, and CI already runs the same restore, build and test on Linux at
check-in. Verify on Windows (build, tests, the `Check-*` scripts) and let CI catch a
Linux-only failure. Run the repro only to debug a red CI run, and only when the CI log is not
enough.

Read `agents/CIPipeline.md` before debugging a red run. It carries the local Docker
repro and the environmental failure modes, including the two that look like code
regressions and are not: a stale SDK image against `global.json`'s `latestPatch` pin,
and the six tests that need `node` on PATH.

Read `agents/GiteaOperations.md` before talking to the remote directly — opening a PR,
listing or deleting a branch, or re-running a workflow via the Gitea API rather than the
web UI. `origin` is a self-hosted Gitea instance, not GitHub, so `gh` does not work here.

## Agent guides

Read the one that matches the stage you're in:

| Guide | Read it when |
| --- | --- |
| `agents/MudBlazorDesign.md` | Writing a spec or plan with UI: does MudBlazor already ship it, and where is the example |
| `agents/MudBlazorImplementation.md` | Coding or testing a component: the house pattern to copy, the rules MudBlazor examples break, the API facts already checked |
| `agents/Testing.md` | Writing or changing any test: what a test must pin, proving it can fail, flaky-test triage, the shared helpers, prompts and goldens |
| `agents/BlazorTesting.md` | Testing a Razor component with bUnit, and the Razor traps those tests catch |
| `agents/DeliveryPlaybook.md` | Running a project plan with parallel subagents; its scripts are in `agents/scripts/` |

@agents/CSharpPrinciples.md
