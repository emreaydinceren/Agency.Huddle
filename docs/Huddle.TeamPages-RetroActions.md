# Huddle.TeamPages — Retrospective actions

Every finding of a retrospective becomes a row here, with an owner, a *done when* and a *checked by*. The
manager updates the status when the action lands and re-checks it in the next retrospective's token tally
or transcript sample. A row is **Verified** only when a later batch shows the behaviour, not when the edit
was made. Rows marked **Open** are dispatched in the run, as chores, before the deliverable that needs
them. The plan and the
tracker were removed after delivery and are in the git history.

Status: **Verified** (seen working in a later batch) · **Done** (applied, not yet observed) · **Partial** ·
**Open** (not applied) · **Changed** (superseded).

## From R1 (after #15, 2026-09-28)

| ID | Finding | Action | Owner | Status | Evidence / next check |
| --- | --- | --- | --- | --- | --- |
| R1-1 | The brief pointed at stale `Conversation/scripts/` copies | Brief and facts point to `agents/scripts/` only | Manager | Verified | No agent since R1 used a stale script |
| R1-2 | Haiku edited a test to get green (2.1.i) | `.i` never edits a test; every test edit is reported under Deviations | Manager | Verified | D7a: 6 `.i`-side test edits (7.2.i selector, 7.4.i using + `_Ok_` arrange, 7.5 literal, 7.6 using + `Assert.Equal<string?>` typo, 7.7.i using), all under Deviations; the only Haiku implementer (7.1) wrote no test after its first src write (call 13). The 7.6 agent that fixed its own typo was Sonnet (transcript model), not Haiku |
| R1-3 | `-Force` used and not reported | Never `-Force`; `-RedDir` on every red | Manager | Verified | No `-Force` since R1 |
| R1-4 | Haiku pairs cost more than Sonnet pairs | Exact `-NewNames` and type-map lines in Haiku prompts; retag 5.4.t/i to Sonnet | Manager | Verified | 4.1 and 5.1–5.2 took one red run each |
| R1-5 | Repeated hand chores | `Check-All.ps1`, `Run-Red.ps1` | Chore agent | Verified | Used by every agent since; `Run-Red` splat bug found in 3.3.t and fixed |
| R1-6 | Shared fake copied per class | `FakeTeamCatalog` created once in 4.2.t | 4.2.t | Verified | 7.2.t read and reused `FakeTeamCatalog.cs` (no copy); 7.7.t created `FakeTeamMembership.cs` once as a shared file |
| R1-7 | Verifiers and preflights started at 49–65K context | Run them as `teampages-dev` | Manager | Verified | R3 evidence (2026-09-28): D5/D6 verifiers started at 22K and read neither file |
| R1-8 | The manager never audits transcripts for `-Force` etc. | Run `Audit-Transcript.ps1 -AgentId <id>` on each implementer, or add the R1 rules to its checks | Manager | **Changed** | R3 evidence (2026-09-28): `Audit-Transcript.ps1` audits shell commands only and would have missed both D6 problems; replaced by the protected-file hash guard in Check-All and by reading each report's Deviations against `git status` |
| R1-9 | Agents re-read the red file after `-RedTask` printed it | Rule in the brief | Manager | Verified | R3 evidence (2026-09-28): no D6 agent re-read a red file after `-RedTask` printed it |

## From R2 (after #30, 2026-09-28)

| ID | Finding | Action | Owner | Status | Evidence / next check |
| --- | --- | --- | --- | --- | --- |
| R2-1 | Missing red-gate exit codes, `-RedDir` on `Run-Red`, IDE0005 on the Teams using, CA1062/CA1859 behind the compile red, stale pointers | 13-line R2 block in the facts Core; brief R1 bullet corrected and an R2 block added | Docs agent | Verified | D7a reds: one run each except 7.1 (2 runs) and 7.3.t (exit 6, root cause R4-1); no exit-4/8 loops |
| R2-2 | Corrections-D5 #16/#20 stale | Fixed; #26/#27 added | Docs agent | Verified | 5.6.t and 5.6.i used them without rework |
| R2-3 | No plan text for the 6.0 pair | Stub added; D7 lag, `@key`, `FakeTeamFolders` notes | Docs agent | Verified | 7.4.t created `FakeTeamFolders.cs` per the note; 7.4.i used it |
| R2-4 | Third copy of the ACL helper would follow | `tests/Huddle.Tests/TestListing.cs`; both users switched | Chore agent | Verified | R3 evidence (2026-09-28): `TestListing.cs` exists and D6 tests use it |
| R2-5 | Linux repro cost a verifier 17 failed calls | `agents/scripts/Run-LinuxRepro.sh` | Chore agent | Verified | Ran at the D5 boundary (2026-09-28): worked as written, Linux 4300 total / 0 failed / 42 skipped, in one Haiku verifier turn of 10 calls |
| R2-6 | Get-FileHash demanded although `Prove-Mutation` prints RESTORED | Dropped from prompts | Manager | Verified | 5.6.i, 5.7.t prompts |
| R2-7 | Manager re-reads about 360K per call | Retro edits go to one agent (done for R2); `/compact` | Manager and Human | **Not working** | R3 evidence (2026-09-28): no compaction since R2 (context 418K->565K); replaced by R3-6 |
| R2-8 | D7 has no review | Two architect reviews: 7.1–7.7 (running) and 7.8–7.13 before R4 | Manager | Verified | `corrections-D7a.md` (36 items) used by all 7.x prompts; `corrections-D7b.md` (34 items) written before R4 |
| R2-9 | Every agent reads about 20K of brief plus facts | Verifiers skip both; implementers get a brief of 6K or less | Chore agent | Partial | Brief 5,997 / Core 5,828 chars (goal met); ctx at call 5 now 46-61K via `corrections-D7b.md` (-> R5-4) |
| R2-10 | The facts reference's 15.9K "Repo-wide conventions" block is read whole by agents | Split it by `###` and have prompts name the subsection | Chore agent | **Changed** | No D7a agent read the facts reference; superseded by R3-4 / R4-6 |
| R2-11 | Ten separate `Set-Tracker` calls | Fold into the review call | Manager | Verified | Superseded by `Mark-Task.ps1`: 6 manager calls covered all 7 pairs (7.4 marked Done in the tracker) |
| R2-12 | Flake notes for the final docs pass | Written into Task 8.3; Task 8.5 uses the Bash Docker form | Docs agent | Done | Runs at D8 |
| R2-13 | New at the D5 commit: `core.autocrlf=true` and no `.gitattributes`, so git will rewrite `agents/scripts/Run-LinuxRepro.sh` to CRLF the next time it touches it in a Windows checkout, and bash rejects CRLF | Add `agents/scripts/*.sh text eol=lf` to a root `.gitattributes` (a shared root file: announce it to the Human first) | Manager | **Open** | Still no root `.gitattributes`; carry to the final report and the next plan's D0 |

## From R3 (after #45, 2026-09-28)

| ID | Finding | Action | Owner | Status | Evidence / next check |
| --- | --- | --- | --- | --- | --- |
| R3-1 | `Prove-Mutation.ps1` reports a mutant that did not build as "caught" (6.0.i, 6.3.i) | Scripted: prints `INVALID: mutant did not build`, exit 10 | Chore agent | Verified | `Prove-Mutation.ps1` prints INVALID/exit 10 (help lines 25-31); 7.2.i and 7.3.i hit non-building mutants and reported them as not caught |
| R3-2 | An agent edited the shared `wwwroot/app.css` (6.5.i) | Protected-file rule in the brief, the agent file and the facts; hash guard in `Check-All.ps1` | Chore agent | Verified | Protected hash guard in Check-All; `app.css` modified by the other session, no agent edit, guard passed |
| R3-3 | The tree is not refreshed after `LibraryFileOps.NewNoteAsync` | Pair 6.7 (tests, then the refresh) | Manager | Verified | 6.7.t/6.7.i reports exist; D6 committed as 21c76fb after them |
| R3-4 | D6 agents read big files whole (6.5.t 151K, 6.4 131K, 6.6 128K) | The big-files line in the facts Core plus fixture line ranges in prompts | Manager | Verified | No code/test file read whole over 20K chars in 12 D7b/D8 agents; new offender `rules.md` (-> R5-6) |
| R3-5 | Manager bookkeeping takes ~30 calls | `Mark-Task.ps1` | Chore agent | Verified | `Mark-Task.ps1 -Task 7.x.t,7.x.i -State Done` once per pair |
| R3-6 | Manager cost: 84 calls re-read 41.3M, 85% from carried context | Commit D6, apply R3, END this manager session; new session per stage (D7a, D7b, D8) from a <=5K state file | Manager and Human | Changed | Superseded by R5-1 (third miss) |
| R3-7 | A second pair continued in the same agent costs more (6.3: 1.99M vs ~1.2M fresh) | Continue an agent into a new pair only when that pair is ~10 calls or fewer | Manager | Changed | Holds for a NEW pair; for the same pair's `.i`, resuming is ~half the cost: 7.2/7.3 pairs 2.3M each vs fresh 7.4 pair 4.3M (R4-4) |
| R3-8 | D7b review (7.8-7.13) must check: bUnit 2.11.3 stub API and PageTitle assertions for 7.13; whether a tab remount re-runs the search; reuse of the shared test context (7.2) and `FakeTeamMembership` (7.7); catalog lag (event-driven wait D7a #20 vs the `ITeamFolders` "must poll" remark); drag coverage and `BoardLayout.DefaultColumns` in 7.12; line ranges for every big file | Put the list in the D7b review prompt | Manager | Verified | All listed items reached D7b prompts; #26/#27/#34 drove the 7.13a/7.13b split that worked |

## From R4 (after #60, 2026-09-29)

| ID | Finding | Action | Owner | Status | Evidence / next check |
| --- | --- | --- | --- | --- | --- |
| R4-1 | A compile red with an allowed code but a wrong name (7.3.t CS1061 `TryGetValue`) is saved when `-NewNames` is absent, then the fixed rerun is refused with exit 6 | `Test-RedGate`: without `-NewNames` every CS error is disallowed (exit 8, no file) | Chore agent | Verified | Exit 8 in 7.8.t and 7.11.t, fixed on the next run; no exit 6 anywhere since R4 |
| R4-2 | `failed (canceled)` lines are not captured: 7.3.t runtime red saved with 0 failing names | `Get-RedLines` line 280 regex `'^\s*failed\s+(?:\(\w+\)\s+)?\S+\s+\('`, same for the summary parser | Chore agent | Done | Regex at `Run-Tests.ps1:237`; no canceled failure occurred to observe |
| R4-3 | Check-Visibility throws `Count` on files with no public type when strict mode is on (verifier ran without `-NoProfile`) | `return , $found` (line 110); `Set-StrictMode -Version Latest` in the Check-* scripts; verifier prompts use `pwsh -NoProfile -File` | Chore agent + Manager | Verified | Strict mode in all four Check-* scripts, `return , $found` at :111; Check-All clean in 7.8.i, 7.12, D7b verifier |
| R4-4 | Fresh `.i` re-explores what its `.t` read (7.4.i: 26 calls before first edit, 2.3M vs ~1.2M resumed) | Always resume the `.t` agent for its `.i`; if fresh, paste a handover block (report section 6 + signatures) | Manager | Partial | Resumed pairs 1.7-1.9M; fresh 7.8.i with handover still 37 tool calls before first write, 3.1M |
| R4-5 | `.t` defects J14/J15 and correction #24 hid behind reds that could not show them | 3-line `.t` self-check (anchored selectors, arrange state at open vs submit, pre-existing markers first); red-review rule; reviewer grep-verifies quoted literals | Docs agent + Manager | Changed | Self-check not effective: 5 of 6 D7b pairs had a `.t` defect at green (-> R5-3) |
| R4-6 | Facts Core (17K chars) + brief (8.7K) read whole by every implementer, ~0.4M each | Core <=6K; prompts name the sections | Docs agent | Partial | Core 5.8K done; call-5 ctx 46-61K, target <=32K missed (-> R5-4) |
| R4-7 | 7.4.t: 42 calls before first edit (guides read whole, non-existent tool, grep of Run-Red for multi-class syntax) | Prompts carry dialog-test precedent ranges and the render helper; facts: Run-Red takes `-FilterClass "*A,*B"` | Docs agent + Manager | Changed | Every D7b/D8 `.t` over 20 calls before first edit (23-55) (-> R5-2) |
| R4-8 | D7a manager was the continued session (first call 112K) despite the hand-off saying fresh | D7b in a new session from a <=5K state file | Manager and Human | Changed | D7b/D8 ran in the continued session, first call 237K (-> R5-1) |
| R4-9 | `TeamsNav.razor`, `NewTeamDialogTests.cs`, `TaskToolbarTests.cs` read whole | Add to the big-files line with a range map; ranges in D7b prompts | Docs agent | Verified | `TeamsNav.razor`, `TeamsNavTests.cs`, `TeamMembersTests.cs` read by range only (max 16K chars per read) |

## From R5 (after #75, 2026-09-29)

| ID | Finding | Action | Owner | Status | Evidence / next check |
| --- | --- | --- | --- | --- | --- |
| R5-1 | Manager ran D7b+D8 in the continued session (first call 237K, avg 286K); manager = 42% of the run (135.4M of 320M) | Next plan: a stage boundary is a HARD STOP — the manager commits, writes the <=5K state file and ENDS its turn with "run /clear, then /Execute-Project-Plan resume <state file>"; it never dispatches the next stage in the same session | Human + skill text | Open | Next run: every stage's first manager call <=40K |
| R5-2 | Every `.t` spent 23-55 tool calls before its first write (namespaces grepped one by one, stale ranges, 28 small greps) | Architect review emits a per-pair API card (namespaces, signatures, fixture helpers) and ranges are regenerated by grep at dispatch time, not copied from a review written earlier | Manager + review prompt | Open | Next run: median `.t` <=20 tool calls before first write |
| R5-3 | `.t` arrange defects found at green in 5 of 6 D7b pairs (J16, J17, 7.10 NewContext, 7.12 x3, 7.13a breadcrumb) | Run-Red prints each failing row's first failure line and flags non-assertion failures as RED SUSPECT; facts Core lists test-host defaults (built-in Chief of Staff seed, bUnit AddTo-before-resolve, class context registers a default fake for every service the `.i` will inject); Razor pairs always red-stop | Chore agent + Docs agent | Open | Next run: <=1 `.t` defect found at green per stage |
| R5-4 | Call-5 context 46-61K although Core is 5.8K: every agent read the whole 16K corrections file | Split corrections per pair (`corrections-<pair>.md`) or give line ranges per item in the prompt | Manager | Open | Next run: call-5 ctx <=35K |
| R5-5 | Architect review D7b: ~20 of 34 items prevented defects; 2 wrong (#30 MudTabs behaviour, #18 ranges); missed every test-host trap | Keep reviews for `logic`/UI stages; each behavioural claim about a third-party component needs a probe or a cited test, each range a fresh grep; add a test-host traps checklist to the review prompt | Skill text | Open | Next run: 0 wrong corrections |
| R5-6 | Binding `rules.md` read whole (39K, 27K chars) by 7.13a and 7.8.i | Facts Core carries a Razor-rules digest with rule ids; prompts name the rule sections to grep | Docs agent | Open | Next run: no whole read of rules.md |
| R5-7 | 8.2 Spec reconcile: 7.5M, 88 tool calls, 34 before the first edit, 24 separate Edits on one file | Manager hands the reconcile agent the list of code-over-Spec deviations from reports' section 6 and judgements; edits grouped per Spec section; split if >10 sections | Manager | Open | Next run: reconcile <=3M |
| R5-8 | Tally script bugs: R4 main detection by suffix, Appendix D has no MAIN window and sums cache_read only | Appendix D snippet = tally5b.py (exact main path, window from the last retro commit, read = input+cache_read+cache_creation, whole-run role table) | Skill text | Open | Next run's R1 uses it |

R5 is the closing retrospective; its rows are carried into the next plan's register, not dispatched in this run.

## How a retrospective feeds this register

1. The retrospective report is split into rows, one per finding, each with an action.
2. Actions that change docs, facts, briefs or plan text go to **one** Sonnet agent with an exact edit list.
3. Actions that add or change a script or a test helper are chore tasks dispatched before the deliverable that
   needs them, and are added to the tracker as unnumbered rows.
4. At the next retrospective the Opus agent is asked to check each open and unverified row against its
   token tally and transcript sample, and the manager updates the status here.
