# Delivery scripts

These PowerShell scripts automate the chores a multi-agent delivery repeats: building, running tests
one-at-a-time, checking diffs for house-style smells, checking public API surface, reseeding golden
files, proving mutation coverage, and auditing a dispatched subagent's transcript. They're the tooling
`agents/DeliveryPlaybook.md` refers to when it says "script repeated chores before they repeat" — read
that page first if you haven't run a delivery in this repo before.

Every script is worktree-aware: run any of them with `pwsh -NoProfile -File agents/scripts/<Script>.ps1`
from any worktree's root, on any machine, in any Claude session, and it resolves the right solution,
main-checkout log location, and sibling script paths on its own. Pass `-?` to any script for its full
parameter list and behaviour.

## Script reference

| Script | What it does | When to use it | Key parameters | Exit codes |
| --- | --- | --- | --- | --- |
| `Build.ps1` | Runs `dotnet build` against the solution, logs full output, prints de-duplicated diagnostics. | Before any test run; after any src change. | `-Solution`, `-MainRepoRoot`, `-LogDir` | dotnet's own exit code |
| `Run-Tests.ps1` | Runs `dotnet test` behind a machine-global mutex, logs output, summarises pass/fail. Also records TDD "red" baselines. | Every test run — filtered while iterating, full before a PR. | `-FilterClass`, `-FilterMethod`, `-Label`, `-NoBuild`, `-RedTask`, `-ExpectFail`, `-NewNames`, `-Force`, `-ParseOnly` | 0 pass · non-zero fail · 3 red-required-but-green · 4 red polluted by analyzer errors · 5 expected/actual mismatch · 6 red file exists · 8 red rejected (bad code/name) · 9 red refused (dirty stash) |
| `Check-Eol.ps1` | Scans changed/untracked files for non-CRLF line endings. | Before committing/handing back any change. | `-Fix` (also runs Fix-Crlf.ps1) | 0 all CRLF · 1 offenders found |
| `Fix-Crlf.ps1` | Normalises given files to CRLF, preserving a BOM if present. | Called by `Check-Eol.ps1 -Fix`, or directly on a specific file. | `-Path` | n/a (throws on failure) |
| `Check-Diff.ps1` | Scans a diff for null-forgiving `!`, reasonless pragmas, weak asserts, untested user-facing text, and markup/string `Assert.Contains`. | Before handing work back (`-Scope Mine`); before a PR (`-Scope Branch`). | `-Scope`, `-Base`, `-SpecPath`, `-AllowUntested`, `-AllowContains`, `-GroupByFile`, `-AcceptedUntestedPath` | 0 clean/warn-only · 1 a failing category found |
| `Check-Visibility.ps1` | Scans configured folders for public top-level types not on an allowlist. | After adding/renaming a public type in a scanned feature area. | `-ScanDirs`, `-AllowlistPath` | 0 all allowed · 1 violation(s) found |
| `Check-DocIndex.ps1` | Checks that `docs/Index.md` links every Markdown page under `docs/` and links only to files that exist. | After adding, renaming or deleting any doc. | none | 0 index complete · 1 missing or unlisted pages |
| `Check-TestDocs.ps1` | Fails when a test method added on this branch has no `///` summary above it. Scans added lines only; about a tenth of the older tests predate the rule. | Before handing work back or opening a PR, after adding any test. | `-Base` | 0 every added test documented · 1 offenders found |
| `Check-Analyzers.ps1` | Builds the two probe projects (`tests/Huddle.AnalyzerProbes*`, outside `Huddle.slnx`) and fails if any enabled Sonar rule stopped firing, an enabled rule has neither a probe nor a reason in `Unprobed.txt`, the `NoWarn` list is malformed or contradicts `.editorconfig`, or `analyzer-decisions.md` disagrees with the denylist. About a minute. | After changing `Directory.Build.props`, `.editorconfig`, `BannedSymbols.txt` or the Sonar version. `Check-All.ps1` runs it only then. | `-SkipBuild` | 0 all four counts zero · 1 otherwise |
| `List-SonarRules.cs` | Prints every rule the pinned Sonar analyzer ships: id, default severity, on-by-default, title. Run with `dotnet run`, not `pwsh`. | Before enabling, disabling or judging an analyzer rule; see `docs/engineering/analyzer-decisions.md`. | none | 0 listed · 1 package not restored or version mismatch |
| `Set-Tracker.ps1` | Flips a tracker table's Active/InProgress/Done checkmarks and refreshes its status line. | After a task or retrospective completes. | `-Task`, `-State`, `-TrackerPath` (required) | 0 success (throws on an unknown task id) |
| `Prove-Mutation.ps1` | Mutates one line/string in a source file, runs the filtered tests, restores the file, reports whether the mutation was caught. | Proving one test actually exercises the line it claims to cover. | `-File`, `-Find`/`-Replace` or `-Line`/`-Replace`, `-FilterClass`, `-FilterMethod`, `-Label` | 0 caught · 2 bad `-Find`/`-Line` target · 7 survived · 10 INVALID (mutant did not build) |
| `Prove-Mutations.ps1` | Runs a batch of mutations from a `.psd1` spec under one mutex acquisition, reporting CAUGHT/SURVIVED per mutation. | Proving a whole test class's coverage in one pass instead of one `Prove-Mutation.ps1` call per line. | `-Spec`, `-MainRepoRoot`, `-LogDir` | 0 all caught · 2 a mutation's build failed · 7 a mutation survived |
| `Reseed-Goldens.ps1` | Deletes named golden files, regenerates them, re-runs to confirm they now pass, prints the diff. | A `PromptGoldenTests` golden is stale after an intentional prompt-text change. | `-Names`, `-DryRun` | 0 success · non-zero reseed-2 still failing |
| `Regenerate-PromptDefaults.ps1` | Rebuilds `prompts.default.json` from `PromptCatalog.All` via the `RegeneratePromptDefaults` helper project, then verifies with `PromptDefaultsFileTests`. | After adding/editing a prompt default in `PromptCatalog`. | (none) | Run-Tests.ps1's exit code |
| `Find-PackageApi.ps1` | Prints a NuGet package type's XML-doc summary and every member's summary, from the local NuGet cache — no disk-wide search. | Looking up a MudBlazor (or any package) API without guessing at IntelliSense. | `-Package`, `-Type`, `-Version` | 0 found · throws if not found/not restored |
| `Audit-Transcript.ps1` | Scans a dispatched subagent's JSONL transcript for risky/forbidden commands (bare `find`, `python`, `sed -i`, main-checkout writes, heredoc bypasses, bare `dotnet`, blocked-by-hook commands, path errors). | Reviewing every subagent report before trusting it. | `-TranscriptPath` or (`-SessionDir` + `-AgentId`) | 0 clean or only informational findings · 1 a blocking category found |

## Setting up for a new delivery

1. **Branch and worktrees.** Work on a feature branch in the main checkout, then give each parallel
   agent its own worktree: `git worktree add -b feat/x-d2 E:\Repos\Huddle-wt\d2 feat/x`. Every script
   here works unmodified from any of those worktrees — no per-worktree configuration needed.

2. **Where logs and red files go.** `Build.ps1`, `Run-Tests.ps1` and `Prove-Mutations.ps1` always write
   to the **main checkout's** `Conversation\logs` (and, for `Run-Tests.ps1 -RedTask`,
   `Conversation\red`), never the worktree's own — `Conversation/` is gitignored tooling shared across
   every worktree. Each script resolves the main checkout via
   `git rev-parse --path-format=absolute --git-common-dir` (a worktree's common-dir always points back
   at the main checkout's `.git`), so this works whether you run it from the main checkout or any
   worktree. Override with `-MainRepoRoot`/`-LogDir`/`-RedDir` if you genuinely need somewhere else.

3. **The one-test-run-at-a-time mutex.** `Run-Tests.ps1` and `Prove-Mutations.ps1` both take the
   machine-global mutex `Global\HuddleTestRun` before running `dotnet test`, and hold it for the whole
   run. Two or more `dotnet test` invocations at once on the same machine collide over a test fixture's
   named pipe and produce phantom failures — always go through these scripts rather than calling
   `dotnet test` directly (see `Audit-Transcript.ps1`'s `BareDotnet` category, which flags exactly that).
   Expect to see "waiting for another test run" when several agents are dispatched in parallel; that's
   the mutex working as intended.

4. **Finding a session's transcript for `Audit-Transcript.ps1`.** A subagent's transcript lives at
   `%USERPROFILE%\.claude\projects\<repo-slug>\<session-id>\subagents\agent-<AgentId>.jsonl`, where
   `<repo-slug>` is the repo's working-copy path with each path separator turned into a dash (e.g.
   `E--Repos-Huddle` for `E:\Repos\Huddle`) and `<session-id>` is the top-level session's own id. Pass
   the `...\subagents` folder as `-SessionDir` together with `-AgentId`, or pass the full `.jsonl` path
   directly as `-TranscriptPath`.

5. **Allowlists and accepted-exception files are project-specific.** `Check-Visibility.ps1` and
   `Check-Diff.ps1` ship with the Tasks feature's own allowlist (`public-types.example.txt`) and accepted
   list (`accepted-untested.example.txt`) as worked examples. A new feature or project should copy the
   shape, not the contents — pass `-AllowlistPath`/`-AcceptedUntestedPath` to your own file, reviewed and
   grown by that delivery's own manager after reading the flagged code.

> [!WARNING]
> Never edit files with `sed -i`, `python3`, `perl`, or a shell redirect — this repo is CRLF and those
> writers emit LF. Use the Edit/Write tools, or `Fix-Crlf.ps1`/`Check-Eol.ps1 -Fix` to repair a file that
> already went wrong. `Audit-Transcript.ps1`'s `SedInPlace`/`EditBypass` categories exist to catch this
> after the fact.
