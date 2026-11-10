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
| `Build-SkillsCatalog.ps1` | Generates `src/Huddle.App/Skills/SkillsCatalog.json` (metadata only) from `SKILLS-CATALOG.md` and the local skill clones; exits 1 listing rows with missing clone folders or a count mismatch. | After the Markdown catalog or a clone changes. | `-CatalogMd`, `-ClonesRoot`, `-OutFile`, `-WhatIf` | 0 written · 1 missing rows or count mismatch |
| `Check-Eol.ps1` | Scans changed/untracked files for non-CRLF line endings. | Before committing/handing back any change. | `-Fix` (also runs Fix-Crlf.ps1) | 0 all CRLF · 1 offenders found |
| `Fix-Crlf.ps1` | Normalises given files to CRLF, preserving a BOM if present. | Called by `Check-Eol.ps1 -Fix`, or directly on a specific file. | `-Path` | n/a (throws on failure) |
| `Check-Diff.ps1` | Scans a diff for null-forgiving `!`, reasonless pragmas, weak asserts, untested user-facing text, and markup/string `Assert.Contains`. | Before handing work back (`-Scope Mine`); before a PR (`-Scope Branch`). | `-Scope`, `-Base`, `-SpecPath`, `-AllowUntested`, `-AllowContains`, `-GroupByFile`, `-AcceptedUntestedPath` | 0 clean/warn-only · 1 a failing category found |
| `Check-Visibility.ps1` | Scans configured folders for public top-level types not on an allowlist. | After adding/renaming a public type in a scanned feature area. | `-ScanDirs`, `-AllowlistPath` | 0 all allowed · 1 violation(s) found |
| `Check-DocIndex.ps1` | Checks that `docs/Index.md` links every Markdown page under `docs/` and links only to files that exist. | After adding, renaming or deleting any doc. | none | 0 index complete · 1 missing or unlisted pages |
| `Check-ManualTests.ps1` | Checks the manual-test set under `docs/engineering/manual-tests/`: header counts and hours, hub and planning links, tracker rows and run-log sections against headings, duplicate ids. | After adding, renaming or editing any manual-test area file or the tracker. `Check-All.ps1` runs it. | `-Root`, `-Area`, `-Summary` | 0 no errors · 1 errors found |
| `Check-TestDocs.ps1` | Fails when a test method added on this branch has no `///` summary above it. Scans added lines only; about a tenth of the older tests predate the rule. | Before handing work back or opening a PR, after adding any test. | `-Base` | 0 every added test documented · 1 offenders found |
| `Report-Duplication.ps1` | **Advisory, never a gate.** Lists copy-pasted C# blocks under `src/` (100+ identical tokens over 10+ lines, literals normalised: SonarQube's CPD definition), the ones this branch added first, then the existing ones longest first, and the duplicated-line density. A pair counts as added when half of one side's lines are new on the branch. About 10 seconds. Judge each row: a copy can read better than a helper. | Before every delivery commit (`agents/DeliveryPlaybook.md`, "Reviewing each report"), and whenever you want refactoring advice. `Check-All.ps1` does not run it. | `-Base`, `-IncludeTests`, `-MinTokens`, `-MinLines`, `-Top` | 0 reported · 2 the matcher failed |
| `Find-Duplicates.cs` | The Roslyn token matcher behind `Report-Duplication.ps1`: reads `.cs` paths on stdin, prints one row per duplicated pair and a density summary. Run with `dotnet run`, not `pwsh`. | Directly only when you need its raw output. | `--min-tokens`, `--min-lines` | 0 always |
| `Check-Analyzers.ps1` | Builds the two probe projects (`tests/Huddle.AnalyzerProbes*`, outside `Huddle.slnx`) and fails if any enabled Sonar rule stopped firing, an enabled rule has neither a probe nor a reason in `Unprobed.txt`, the `NoWarn` list is malformed or contradicts `.editorconfig`, or `analyzer-decisions.md` disagrees with the denylist. About a minute. | After changing `Directory.Build.props`, `.editorconfig`, `BannedSymbols.txt` or the Sonar version. `Check-All.ps1` runs it only then. | `-SkipBuild` | 0 all four counts zero · 1 otherwise |
| `List-SonarRules.cs` | Prints every rule the pinned Sonar analyzer ships: id, default severity, on-by-default, title. Run with `dotnet run`, not `pwsh`. | Before enabling, disabling or judging an analyzer rule; see `docs/engineering/analyzer-decisions.md`. | none | 0 listed · 1 package not restored or version mismatch |
| `Set-Tracker.ps1` | Flips a tracker table's Active/InProgress/Done checkmarks and refreshes its status line. | After a task or retrospective completes. | `-Task`, `-State`, `-TrackerPath` (required) | 0 success (throws on an unknown task id) |
| `Prove-Mutation.ps1` | Mutates one line/string in a source file, runs the filtered tests, restores the file, reports whether the mutation was caught. | Proving one test actually exercises the line it claims to cover. | `-File`, `-Find`/`-Replace` or `-Line`/`-Replace`, `-FilterClass`, `-FilterMethod`, `-Label` | 0 caught · 2 bad `-Find`/`-Line` target · 7 survived · 10 INVALID (mutant did not build) |
| `Prove-Mutations.ps1` | Runs a batch of mutations from a `.psd1` spec under one mutex acquisition, reporting CAUGHT/SURVIVED per mutation. | Proving a whole test class's coverage in one pass instead of one `Prove-Mutation.ps1` call per line. | `-Spec`, `-MainRepoRoot`, `-LogDir` | 0 all caught · 2 a mutation's build failed · 7 a mutation survived |
| `Reseed-Goldens.ps1` | Deletes named golden files, regenerates them, re-runs to confirm they now pass, prints the diff. | A `PromptGoldenTests` golden is stale after an intentional prompt-text change. | `-Names`, `-DryRun` | 0 success · non-zero reseed-2 still failing |
| `Regenerate-PromptDefaults.ps1` | Rebuilds `prompts.default.json` from `PromptCatalog.All` via the `RegeneratePromptDefaults` helper project, then verifies with `PromptDefaultsFileTests`. | After adding/editing a prompt default in `PromptCatalog`. | (none) | Run-Tests.ps1's exit code |
| `Find-PackageApi.ps1` | Prints a NuGet package type's XML-doc summary and every member's summary, from the local NuGet cache — no disk-wide search. | Looking up a MudBlazor (or any package) API without guessing at IntelliSense. | `-Package`, `-Type`, `-Version` | 0 found · throws if not found/not restored |
| `Freeze-Tests.ps1` | Freezes the test files an approved red wrote (copies under the worktree's git directory) so the `block-frozen-test-edits.sh` hook refuses `Edit`/`Write` on them; `-Check` diffs them against the copies and flags fewer asserts, loosened asserts, `Skip`, delays, `catch`, edited timeouts and deletions. | `-Freeze` when you approve a red, before resuming the agent to implement; `-Check` after every implementation; `-Release` for a real arrange defect; `-Clear` after the pair is committed. | `-Freeze`, `-Check`, `-Release`, `-Clear`, `-Status`, `-Path` | 0 ok · 1 nothing to freeze, or a frozen file changed or was deleted · 2 bad switch combination |
| `Remove-Stubs.ps1` | Deletes every block from a line holding `// <stub` to the next `// </stub>` (inclusive) in `*.cs` under `src`, also drops one blank line before the opener when the line after the block is blank, `}` or EOF, keeps CRLF and a BOM, prints each removed line; a file that then differs from HEAD only by line endings or final newline (e.g. LF Program.cs without final newline) is restored byte for byte from `git show HEAD:` and reported, so `-ExpectClean` passes; exits 1 on a closer without opener, an opener without closer, or both markers on one line. `Run-Tests.ps1` refuses a red while a `// <stub` marker is left under `src/`. | After recording a compile red built against throwaway stubs, before the red run. | `-Root`, `-WhatIf` (list only), `-ExpectClean` (exit 1 and print if `git diff --stat -- src/` is not empty afterwards) | 0, 1 |
| `Find-Constructions.ps1` | Lists `file:line` of every `new <Type>(` and, in files that mention the type, every target-typed `new(` marked "target-typed, check"; skips bin/obj. | Before adding a constructor parameter, so every call site (tests included) is updated. | `-Type`, `-Root` (default `src,tests`) | 0 |
| `Find-PinnedTests.ps1` | Lists `file:line: text` for every test line matching a regex, grouped per test project, and marks files frozen in this worktree (`[FROZEN]`, from the `Freeze-Tests.ps1` manifest). | Before changing a prompt wording, constant or name that tests may pin, so no pinned test is found only when CI goes red. | `-Pattern`, `-Root` | 0 always (prints nothing on no match) |
| `Rerun-Failed.ps1` | Parses the `failed <Namespace.Class.Method>` lines of a `Run-Tests.ps1` log and reruns each failed class alone `-Times` times; prints `FLAKY (n/N passed)`, `REAL (failed every run)` or `PASSED ALONE`. | After a red full run, to tell a flaky class from a broken one before debugging. | `-Log`, `-Times`, `-ListOnly` | 0 no class is REAL · 1 a class failed every rerun |
| `Test-HookCases.sh` | Runs every `.claude/hooks/*.cases.txt` (or the files named) against its hook: pipes each payload in and compares the hook's deny/allow answer with the case's `BLOCK`/`allow`. `{TMP}` in a payload is a scratch worktree that freezes `{TMP}/tests/Frozen.cs`. | After editing a file in `.claude/hooks/`. Run it with `bash`. | file paths (optional) | 0 all cases match · 1 a case differs |
| `Measure-TestSuite.ps1` | Records the Test Project Split metrics under a label into `Conversation/test-split/metrics-<label>.md`: clean build, incremental test build (touches `TaskStoreTests.cs`), full run, per-assembly tests and duration, `tests/*/bin` size. | Before and after the split (plan tasks 3, 13, 36). Not beside another build or test run. | `-Label`, `-SkipClean`, `-ParseOnly <log>`, `-MainRepoRoot` | 0 measured · Run-Tests.ps1's exit code if the run was red · throws on a build failure |
| `Css-Snapshot.ps1` | Proves a CSS change altered nothing visible, or shows exactly what it did change. `-Mode Capture` seeds a fresh instance once (`huddle-seed --today 2026-11-01`), keeps it as `<SeedRoot>.golden` and restores it for every Capture, starts it on the mock adapter (port 5300, pipe `huddle-<leaf of -SeedRoot>`, override with `-PipeName`; `Team:Tasks:WakeEnabled=false`), then drives headless Chrome (DevTools protocol, no npm packages) over about 46 routes in all 8 Themes at widths 1280 and 768, and writes one gzip JSON of computed styles per page (about 120 properties for every element under `body`, plus `::before`, `::after` and `::placeholder`). `-Screens` also saves one PNG per page beside its JSON. `-Mode Diff` compares two snapshot folders and lists every difference by route, Theme and width, then element path and property. `-Mode Report` makes the same comparison and writes a self-contained HTML report into `-Out` (`index.html`, one page per changed route, Theme and width with the before and after screenshots, a pixel-difference image and the property diff); `-Declared <file>` sorts every difference as declared or undeclared (format in `-?`, example `css-snapshot-declared.example.txt`) and Report exits 1 only when something is undeclared; `-Pages <regex>` limits Diff or Report to matching pages. Reads the rendered page only, so it survives a stylesheet split. About 10 minutes and 20 MB for a full Capture. The exclusions and what it cannot see (hover, focus, transient states, motion) are in `-?`. `css-snapshot.mjs` is its Node half and `css-png.mjs` its pixel compare; run them through the `.ps1`. | Before and after any CSS refactor: Capture a baseline (with `-Screens` if you want a Report with pictures), change, rebuild (`-Build`), Capture a candidate on the same calendar day, then Diff must be empty, or Report with `-Declared` must have nothing undeclared. | `-Mode` (required), `-Out`, `-Screens`, `-Port`, `-PipeName`, `-SeedRoot`, `-Reseed`, `-Attach` with `-DataDir`, `-Build`, `-Themes`, `-Widths`, `-Only`, `-Concurrency`, `-Baseline`, `-Candidate`, `-Pages`, `-Declared`, `-Full` | 0 captured, no differences, or nothing undeclared · 1 a page failed, differences found or an undeclared difference · 2 bad arguments or setup |
| `Get-IssueBrief.ps1` | Prints one Gitea issue (title, state, body) and every comment as plain text. Reads `GITEA_ACCESS_TOKEN` from the process, then the User scope; derives the API base from `origin`; never prints the token or the host. | Reading an issue brief before starting a task, instead of calling the API by hand. | `-Number` (required) | 0 printed · non-zero on a missing token or a failed request (message holds the status code only) |
| `Save-LaneCheckpoint.ps1` | Copies a stopped test lane (data folder, repositories folder, `<HomePath>\.claude\projects\<lane>-*`) to a named checkpoint outside the lane, built under a temporary name and renamed only when complete. Shares `LaneCheckpointCommon.ps1` (dot-sourced helper, not run alone). | After a manual test case, to rewind later (#360). | `-LaneRoot`, `-Port`, `-Name` (required); `-DataDir`, `-ReposDir`, `-HomePath`, `-CheckpointRoot`, `-Force` (replace a same-named checkpoint) | 0 saved · non-zero when the port answers, the copy differs or the name exists |
| `Restore-LaneCheckpoint.ps1` | Returns a lane to a checkpoint, discarding everything since. Validates the checkpoint, stages the copies beside the targets, then swaps by rename (rolls back on failure). Same absolute lane path only. | To retry a test case from a saved state (#360). | `-LaneRoot`, `-Port`, `-Name` (required); `-Force` (required; acknowledges the discard); `-HomePath`, `-CheckpointRoot` | 0 restored · non-zero without `-Force`, while the port answers, on a different path or a damaged checkpoint |
| `Audit-Transcript.ps1` | Scans a dispatched subagent's JSONL transcript for risky/forbidden commands (bare `find`, `python`, `sed -i`, main-checkout writes, heredoc bypasses, bare `dotnet`, blocked-by-hook commands, path errors). | Reviewing every subagent report before trusting it. | `-TranscriptPath` or (`-SessionDir` + `-AgentId`) | 0 clean or only informational findings · 1 a blocking category found |
| `Split-TestProject.ps1` | Reads `test-split-map.psd1` and reports, lists or `git mv`s the test files of the old `Huddle.Tests.Domain` / `Huddle.Tests.Components` projects into the per-feature projects. `-Report` prints `<Feature>  old: <files> files, <n> tests  new: <n> tests` per feature, then unmapped files. Never moves Keep files, csprojs or `bin/obj`. `-Apply` prints one `moved N files to tests/Huddle.Tests.<F>/` line (`-Verbose` lists old -> new). `-Create` writes the new project (csproj, runner json, setup test) and its `Huddle.slnx` and `Huddle.TestAssemblies.props` lines; idempotent. | Moving a feature's tests in the Test Project Split; `-Report` to check what is left. Pinned by `Test-SplitTestProject.ps1`. | `-Report`, `-Feature`, `-Create`, `-Apply`, `-Verbose`, `-Root`, `-MapPath` | 0 success · 1 unmapped file (`-Report`), a refused `-Apply` or a refused/partial `-Create` |

## Colour helpers for tests

Not scripts, but the tooling the Theme colour rules lean on. They live in `tests/Huddle.Tests.Common/Themes/Skins/` and
are used by `SkinColourRulesTests` and `DeltaETests` in `Huddle.Tests.Shell`:

| Helper | What it does |
| --- | --- |
| `SkinColours` | Resolves a shipped skin's tokens and palette to concrete sRGB colours without a browser: `Token(key)`, `Palette(name)`, `Surface`, `Difference(a, b)` and `All` (the eight Themes). A token that is not a colour resolves to `null` |
| `DeltaE` | `Ciede2000(first, second)`: the CIEDE2000 colour difference. About 1 is the least an eye can tell apart; the Theme rules use a floor of 3 |
| `SrgbColour` | An sRGB colour with `Hex` and `ToLab()`, the value type the other two trade in |

Use them to assert a colour relationship on resolved colours (two grounds, two states, a rung order) rather than comparing
token strings.

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
