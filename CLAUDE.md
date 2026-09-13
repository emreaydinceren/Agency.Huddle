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
| Source | `src/Huddle.Acp`, `src/Huddle.Console` | `src/Huddle.App`, `src/Huddle.Contracts` |
| Tests | `tests/Huddle.Acp.Tests` | `tests/Huddle.Tests` |
| Docs | `docs/acp/**`, `README.md` | `docs/AgencyTeam.md`, `docs/agencyteam/**`, `docs/adr/**` |

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

Read `agents/CIPipeline.md` before debugging a red run. It carries the local Docker
repro and the environmental failure modes, including the two that look like code
regressions and are not: a stale SDK image against `global.json`'s `latestPatch` pin,
and the six tests that need `node` on PATH.

@agents/CSharpPrinciples.md
