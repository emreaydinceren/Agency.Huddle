# Huddle.TeamPages — Retrospective actions

Every finding of a retrospective becomes a row here, with an owner, a *done when* and a *checked by*. The
manager updates the status when the action lands and re-checks it in the next retrospective's token tally
or transcript sample. A row is **Verified** only when a later batch shows the behaviour, not when the edit
was made. Rows marked **Open** are dispatched in the run, as chores, before the deliverable that needs
them. See [the plan](Huddle.TeamPages-ProjectPlan.md#retrospectives-every-15-completed-tasks) and the
[tracker](Huddle.TeamPages-Tracker.md).

Status: **Verified** (seen working in a later batch) · **Done** (applied, not yet observed) · **Partial** ·
**Open** (not applied) · **Changed** (superseded).

## From R1 (after #15, 2026-09-28)

| ID | Finding | Action | Owner | Status | Evidence / next check |
| --- | --- | --- | --- | --- | --- |
| R1-1 | The brief pointed at stale `Conversation/scripts/` copies | Brief and facts point to `agents/scripts/` only | Manager | Verified | No agent since R1 used a stale script |
| R1-2 | Haiku edited a test to get green (2.1.i) | `.i` never edits a test; every test edit is reported under Deviations | Manager | Verified | Later edits were analyzer-only and reported (4.2.i, 5.5.t) |
| R1-3 | `-Force` used and not reported | Never `-Force`; `-RedDir` on every red | Manager | Verified | No `-Force` since R1 |
| R1-4 | Haiku pairs cost more than Sonnet pairs | Exact `-NewNames` and type-map lines in Haiku prompts; retag 5.4.t/i to Sonnet | Manager | Verified | 4.1 and 5.1–5.2 took one red run each |
| R1-5 | Repeated hand chores | `Check-All.ps1`, `Run-Red.ps1` | Chore agent | Verified | Used by every agent since; `Run-Red` splat bug found in 3.3.t and fixed |
| R1-6 | Shared fake copied per class | `FakeTeamCatalog` created once in 4.2.t | 4.2.t | Done | Check that 7.2.t reuses it, at R3 |
| R1-7 | Verifiers and preflights started at 49–65K context | Run them as `teampages-dev` | Manager | Partial | D3/D4 verifiers started at 21K, but the agent file still tells them to read the brief and facts whole (see R2-9) |
| R1-8 | The manager never audits transcripts for `-Force` etc. | Run `Audit-Transcript.ps1 -AgentId <id>` on each implementer, or add the R1 rules to its checks | Manager | Open | Not run on any agent; do it from D6 |
| R1-9 | Agents re-read the red file after `-RedTask` printed it | Rule in the brief | Manager | Open | Ignored by 5 agents; consider making `Run-Red.ps1` print "do not re-read" (it does) and audit it |

## From R2 (after #30, 2026-09-28)

| ID | Finding | Action | Owner | Status | Evidence / next check |
| --- | --- | --- | --- | --- | --- |
| R2-1 | Missing red-gate exit codes, `-RedDir` on `Run-Red`, IDE0005 on the Teams using, CA1062/CA1859 behind the compile red, stale pointers | 13-line R2 block in the facts Core; brief R1 bullet corrected and an R2 block added | Docs agent | Done | Check at R3 that 6.x reds have no exit-4/6/8 loops |
| R2-2 | Corrections-D5 #16/#20 stale | Fixed; #26/#27 added | Docs agent | Verified | 5.6.t and 5.6.i used them without rework |
| R2-3 | No plan text for the 6.0 pair | Stub added; D7 lag, `@key`, `FakeTeamFolders` notes | Docs agent | Done | Check when D6 and 7.4 run |
| R2-4 | Third copy of the ACL helper would follow | `tests/Huddle.Tests/TestListing.cs`; both users switched | Chore agent | Done | Uncommitted: lands in the D5 commit; 6.3.t must use it |
| R2-5 | Linux repro cost a verifier 17 failed calls | `agents/scripts/Run-LinuxRepro.sh` | Chore agent | Verified | Ran at the D5 boundary (2026-09-28): worked as written, Linux 4300 total / 0 failed / 42 skipped, in one Haiku verifier turn of 10 calls |
| R2-6 | Get-FileHash demanded although `Prove-Mutation` prints RESTORED | Dropped from prompts | Manager | Verified | 5.6.i, 5.7.t prompts |
| R2-7 | Manager re-reads about 360K per call | Retro edits go to one agent (done for R2); `/compact` | Manager and Human | Partial | `/compact` requested by the Human; check the MAIN row at R3 |
| R2-8 | D7 has no review | Two architect reviews: 7.1–7.7 (running) and 7.8–7.13 before R4 | Manager | Partial | `corrections-D7a.md` pending; D7b not started |
| R2-9 | Every agent reads about 20K of brief plus facts | Verifiers skip both; implementers get a brief of 6K or less | Chore agent | Done | 2026-09-28: `delivery-brief-slim.md` (8.3K chars), `teampages-dev.md` reads it plus the Core, and a "verification runner" prompt reads neither. Measure the first-call context of D6 agents and verifiers against 21–25K at R3 |
| R2-10 | The facts reference's 15.9K "Repo-wide conventions" block is read whole by agents | Split it by `###` and have prompts name the subsection | Chore agent | Done | 2026-09-28: the reference file now has an INDEX and nine `##` headings; Core says to read only the section needed. Measure orientation calls before the first edit at R3 |
| R2-11 | Ten separate `Set-Tracker` calls | Fold into the review call | Manager | Deferred | Deferred at the D6 gate (2026-09-28): a handful of manager calls, well under 1% of the manager's cost; I batch `Set-Tracker` calls with the review command when it is free to do so |
| R2-12 | Flake notes for the final docs pass | Written into Task 8.3; Task 8.5 uses the Bash Docker form | Docs agent | Done | Runs at D8 |
| R2-13 | New at the D5 commit: `core.autocrlf=true` and no `.gitattributes`, so git will rewrite `agents/scripts/Run-LinuxRepro.sh` to CRLF the next time it touches it in a Windows checkout, and bash rejects CRLF | Add `agents/scripts/*.sh text eol=lf` to a root `.gitattributes` (a shared root file: announce it to the Human first) | Manager | **Open** | The script is LF in the working copy now, so it works until a checkout rewrites it; raise in the final report if not done |

## How a retrospective feeds this register

1. The retrospective report is split into rows, one per finding, each with an action.
2. Actions that change docs, facts, briefs or plan text go to **one** Sonnet agent with an exact edit list.
3. Actions that add or change a script or a test helper are chore tasks dispatched before the deliverable that
   needs them, and are added to the tracker as unnumbered rows.
4. At the next retrospective the Opus agent is asked to check each open and unverified row against its
   token tally and transcript sample, and the manager updates the status here.
