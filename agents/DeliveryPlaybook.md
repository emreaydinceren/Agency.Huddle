# Running a multi-agent delivery in this repo

## Contents

- Preparing before the first dispatch
- Dispatching work
- Keeping the token cost down
- Spelling out the hard rules
- Reviewing each report
- Getting an independent review
- Holding retrospectives
- Moving work between worktrees and the main checkout
- Sharing the checkout with other sessions
- Keeping CI and lanes green when owners run in parallel
- Knowing the Windows and shell traps
- Notes

How to run a test-first project plan in Huddle with parallel coding subagents, and what the
delivery manager (the session that dispatches, reviews and commits) must do so the result can be
trusted. Read it before executing a plan with the `/Execute-Project-Plan` command or any similar
orchestration. Test technique is in [Testing.md](Testing.md) and [BlazorTesting.md](BlazorTesting.md).

Distilled from the Tasks delivery (147 tasks, eight retrospectives, September 2026). Every rule
below exists because its absence cost a fix round, a bug, or an hour.

## Preparing before the first dispatch

1. **Branch and worktrees.** Work on a feature branch. Give each parallel agent its own
   worktree (`git worktree add -b feat/x-d2 E:\Repos\Huddle-wt\d2 feat/x`) and a directory
   junction from `<worktree>\Conversation` to the main checkout's `Conversation\` (gitignored), so
   shared scripts and notes resolve from every worktree. **Before `git worktree remove`, delete the
   junction with `cmd /c rmdir <worktree>\Conversation`.** Removing a worktree with the junction still
   there follows it and permanently deletes the shared folder's contents; this happened on 2026-11-04.
2. **A common brief** every agent reads first: commands, hard rules (below), the red-first
   procedure, and a fixed report shape that includes a **Coverage** table (each plan bullet,
   Spec clause and correction → a test name, or `NOT COVERED` with the task that owns it). The
   agent writes that report to a file under `Conversation\` and returns at most ten lines: status,
   red-file path, the verbatim test summary line, files touched, deviations, the report path.
3. **A living facts file** that only the manager edits: exact commands, API locations,
   helpers and their traps, settled decisions. Agents re-deriving the same facts was the largest
   avoidable cost.
4. **An architect review per risky batch** (a read-only Opus Plan agent against the live code),
   written up as a corrections file marked "settled — implement, do not re-litigate". It overrides
   the plan. Give the reviewer the plan line ranges, Spec sections and source folders the batch
   touches, and a word cap. Batches of pure records, enums and options skip it.
5. **A row-ownership map before a UI batch.** Map every Spec clause (every table row, every
   empty state, every notice) to exactly **one** task, and pin its user-facing text, date format
   and markup contract at the same time. Without it, fields nobody owned surfaced at review, and
   sibling agents built in parallel drifted into three wordings and three date formats.
6. **Script repeated chores before they repeat**: a test runner that serialises runs and extracts
   failing names, a red recorder, a mutation prover, a diff checker, a transcript audit.

## Dispatching work

- **Dispatch each test task red-only.** The prompt says, first line: write the tests, record the
  red, then STOP. Review the red and its Coverage table, then **resume the same agent** with
  "implement". Agents skipped the red three times when the STOP came mid-prompt or the prompt read
  like an implementation spec; a STOP at the start always held.
- **Freeze the tests when you approve the red.** In the worktree, run
  `pwsh -NoProfile -File agents/scripts/Freeze-Tests.ps1 -Freeze` before you resume the agent with
  "implement". It copies every changed or new file under `tests/` into the worktree's private git
  directory, and the `block-frozen-test-edits.sh` hook then refuses `Edit` and `Write` on them. An
  implementer who cannot get a row green now has one move: stop and send a **test change
  request**. You read it and decide. If the test has a real arrange defect, run `-Release -Path
  <file>` for that one file, let the agent fix it, and freeze again; otherwise send the agent back
  to the code. `-Clear` ends the freeze once the pair is committed. The hook cannot see a shell
  redirect, so `-Check` (see Reviewing each report) is the backstop.
- **One editor per file at a time.** Reds for tasks that share a file can be written in parallel
  (in separate test files); their implementations run one after another, in a stated order.
  Rebase the waiting worktree before its turn.
- **Two streams in two worktrees on disjoint files, merged by rebase at each deliverable
  boundary, never conflicted** (Second Brain). But a start-up or hosted-service change in one
  stream broke a test in the other: run the full suite after merging such a change.
- **Choose resumed vs fresh agents deliberately.** Resuming within the same component chain costs
  2–6 tool calls to reorient versus 45–56 for a fresh agent, but a long-lived context grows to
  3–4× the cost per turn. Resume for the next task on the same files; start fresh, with a precise
  prompt, for a new area. Retire an agent at about 150K context: it writes a hand-off note, and a
  fresh agent starts from that note. Agents cannot see their context size, so the prompt sets a
  **tool-call budget** instead (60 calls ≈ 150K for Sonnet here), counted across resumes; at the
  budget the agent finishes its step, writes the hand-off note and stops. Without it, agents were
  retired only after the fact, at 206K, 225K and 279K.
- **Resume or spawn? Decide per task, in this order; the first line that matches wins.**

  | Situation | Do | Why |
  | --- | --- | --- |
  | Reviewer, verifier or retrospective | Spawn fresh, every time | Independence is the point; it reads neither brief nor facts, so orientation is cheap |
  | Agent has used its tool-call budget (60) by the time its task is reported | Give it no new task: it writes a hand-off note, and the next task goes to a fresh agent started from that note | Past ~150K, each call costs 3-4x |
  | Next task touches the same files or component chain as the agent's last one | Resume with `SendMessage` | 2-6 calls to reorient against 45-56 for a fresh agent |
  | Next task is a new area, or the agent's last report had unresolved deviations | Spawn fresh with a precise prompt | A resumed agent carries stale assumptions into code it has not read |
  | Task is `data` (records, enums, options) | Spawn fresh Haiku; do not chain | Orientation is small and the context stays small |

  The budget is counted across resumes, so a resume spends the same 60 calls. It gates the
  *next* task; it never cuts a task off. An agent that reaches 60 mid-step finishes that step
  (the red, the implementation or the fix), leaves the build and tests in a known state, writes
  the hand-off note and stops. Stopping inside a step leaves a dirty worktree that a fresh agent
  must reverse-engineer, which costs more than the calls saved. To make a fresh
  spawn cheap, the prompt carries the signatures, the file paths and the line ranges (from
  `grep -n`), and names the facts file's Core section as the only thing to read first. Do not
  spawn a fresh agent "for hygiene": its orientation is the cost the table exists to avoid.
- **Pick the model by the plan's Risk tag.** In the Tasks delivery every Haiku result needed a
  fix, so Sonnet does `logic` and `boundary` work. Haiku still does `data` tasks (records, enums,
  options): in the Library delivery those came in at ~2M tokens a task against ~8M for Sonnet
  refactors. Opus for concurrency, drag-and-drop, architecture review and retrospectives.
- **Size a pair to the budget.** One component plus one test class of 20 rows or fewer, with the
  signatures in the prompt, fits 60 tool calls. A new fixture or a third-party probe is its own step.
- **Stop Razor and dialog pairs at red and resume them with SendMessage.** Chain a pair straight
  into its `.i` only for `data` and pure-logic work; UI arrange defects hide behind a compile red.
- **Split the corrections file per pair** (`corrections-<pair>.md`). One 16K file read whole by every
  agent held call-5 context at 46-61K although the facts Core was under 6K.
- **Regenerate every line range with `grep -n` when you write the prompt.** Ranges copied from a
  review written earlier were stale by the time the pair started. Give docs agents exact line
  numbers too: one spent its whole budget orienting.
- **Keep verification lean in prompts:** the touched test classes once, fix, once more; the checks;
  the full suite once. A fix-resume runs only the touched classes. The machine allows one test run
  at a time, so extra full runs queue every other agent.

## Keeping the token cost down

Every API call re-reads the caller's whole context, so an agent costs about *calls × context
size*. The Library delivery measured about 52M tokens re-read in its first 30 minutes:

| Where | Tokens re-read | Lesson |
| --- | --- | --- |
| Manager (Opus), context 119K → 310K | 19M | Its context is the most expensive in the run |
| Three architect reviews (Opus), up to 234K | 15M | They read whole documents |
| An implementer's first task (Haiku), context 49K → 123K | 3.6M | Orientation: 38 calls, mostly reading |
| Each later task, resumed in the same agent | 0.85–2.4M | Resuming was cheaper than starting fresh |
| A Sonnet refactor agent kept to 279K (150 calls) | 28.5M | The cap needs a call budget agents can count |

The Team Pages delivery (whole run, 320M re-read) put the same lesson in one table:

| Where | Tokens re-read | Lesson |
| --- | --- | --- |
| Manager | 135.4M (42%) | One session per stage never happened; the manager carried its whole history through every stage |
| Sonnet implementers | 88.3M (28%) | The bulk of the useful work; cost tracks calls before the first write |
| Opus reviews + retrospectives | 61.0M (19%) | Worth it for `logic`/UI stages, not for every stage |
| Haiku implementers | 21.0M (7%) | `data` pairs only |
| Haiku verifiers | 3.5M (1%) | Cheap because they read neither brief nor facts |

- **End the manager session at every stage boundary.** Commit, write the <=5K state file and stop
  with the resume line for the Human; a continued session started D7b at 237K and cost 286K per call.
- **The manager stays compactable.** Keep `delivery-state.md` current after every commit, compact
  at each batch boundary, and grep large files rather than reading them. Never paste agent reports
  into your own turn; open the report file only when the ten-line summary looks wrong.
- **Point at sections, never whole documents.** Prompts, the brief and review prompts name Spec
  sections and plan line ranges. Agents read those with `offset`/`limit` or `grep`. That is what
  makes a fresh agent's orientation cheap.
- **Keep command output small; it is most of an implementer's context.** Search in two steps
  (`grep -l`/`-c`, then `grep -n … | head -40`), with no `-A`/`-B` over 10 and no loops that print
  whole files. Read files over ~300 lines with `offset`/`limit` around the hit; one full read of
  `PersonaStore.cs` was 15K tokens. Unbounded greps over `tests/` cost 22–31K per agent.
- **The facts file opens with a short Core section** (under ~150 lines) and an index. Agents read
  Core and grep the rest by heading; the whole file read was 10K tokens for every fresh agent.
- **Disable MCP servers the delivery doesn't need** before starting. Each agent inherits their tool
  definitions, and every agent here started at 38–50K before its first read.
- **Dispatch implementers as the tool-limited `delivery-dev` agent type**
  (`.claude/agents/delivery-dev.md`: Read, Edit, Write, Grep, Glob, Bash, PowerShell), naming the
  brief and facts files in each prompt. The limited tool list cuts an agent's starting context from
  ~71K to ~23K.
- **The manager's own context dominated cost late in a run (600K+).** Chain logic pairs red→green
  in one dispatch once the red is approved, and keep check-ins short.
- **Measure at each retrospective** with the tally in the `/Execute-Project-Plan` command's
  Appendix D: calls, first and last context, and tokens re-read per agent.

## Spelling out the hard rules

Agents break unstated rules and under-report broken stated ones. Write these into the brief,
with the reason, and audit every transcript for them:

| Rule | Why |
| --- | --- |
| Never `find /` or search the whole disk; scope to `src/`, `tests/`, `docs/`; package docs are under `%USERPROFILE%\.nuget\packages\<id>\<version>\` | Git Bash's `/` spans every drive; the orphaned `find.exe` outlives the tool call |
| Edit files only with the Edit/Write tools — never `sed -i`, `python3`, `perl` or a shell redirect, tests included | The repo is CRLF; those writers emit LF. "Source files only" was read as "not tests" |
| Read and write only inside your own worktree | An agent edited the main checkout, where the manager commits and another session keeps uncommitted docs |
| Never commit, stage, stash, reset, checkout or restore | Other sessions share the checkout; `git stash` swallowed work twice |
| List every command the hook blocked, and every git command beyond status/diff/log/show | Omissions were more common than the commands themselves |
| Throwaway files go in the session scratchpad, never the repo | Scratch tests written into `tests/` by heredoc |
| In Bash, write `/e/Repos/...`; never `cd E:\...` | A Windows path ending `\"` breaks the command |
| Visibility changes and new packages are stop-and-ask | An agent made a service public on false reasoning |
| A failing test is the spec. Never edit, skip, loosen or delete a frozen test to get green; if you think the test is wrong, stop and file a **test change request** (the failing line, your evidence, the change you would make) | Agents under pressure to reach green relax the assert instead of fixing the code; the freeze hook enforces it |
| Before recording a compile-error red, build once against throwaway stubs of the missing names and fix the test file's analyzer errors, then delete the stubs | Analyzers do not run while a compile error exists: three frozen reds in the Long-Running Rooms delivery later failed on CA1859 and xUnit2029 and each blocked its implementation |
| Wrap every test stub in `// <stub …>` … `// </stub>` markers and remove them with `Remove-Stubs.ps1 -ExpectClean`; never put a stub in an LF or `-text` file | Unmarked stubs were left behind; the script cannot rewrite a file whose line endings it must not touch |
| A test that builds a RoomSession or RoomSessionPool cancels its run token before disposing it | The session consumer ends only when the run token is cancelled; a test on CancellationToken.None hung the Agents assembly for 22 minutes while holding the machine-wide test mutex |
| Never stop every dotnet process to clear a hung run; stop only your own test process | Killing every run abandoned the test mutex, and Run-Tests.ps1 then failed the next caller instead of taking ownership |
| Never run `python3` (the Windows Store stub blocks until the call times out), and never leave a server or `run.ps1` running in a tool call; use `-DryRun`, or `preview_start` for a dev server | A hung call ties up the agent and looks like a stuck delivery |

Three hooks in `.claude/settings.json` enforce the worst of these: one blocks disk-wide `find` and
`sed -i` in a command position, one converts files created by `Write` to CRLF, and one refuses
edits to a test frozen by `Freeze-Tests.ps1`.

## Reviewing each report

1. **Audit the transcript** for the forbidden commands, main-checkout writes and heredocs.
2. **Check the red**: it fails only on the new types or members, with no analyzer noise, and
   `git diff --stat -- src/` was empty when it was recorded. Read each failing row's first failure
   line. A row failing on a DI or bUnit exception, a seeded value or a missing service is a `.t`
   arrange defect: send it back before the `.i`. Know the test-host defaults: the host seeds a
   built-in Chief of Staff Agent, bUnit services are resolved after `Render`, and a class
   context registers a default fake for every service the `.i` will inject.
3. **Check the Coverage table.** Every `NOT COVERED` row must cite the task that owns the item; a
   gap nobody owns is assigned now, not after the batch.
4. **Run `Freeze-Tests.ps1 -Check`** after every implementation. Any `CHANGED` or `DELETED` line
   is a finding until you have read its diff: fewer asserts, `Equal` weakened to `Contains` or
   `NotEmpty`, `Skip`, a delay, a new `catch` or a widened timeout means the agent bent the test
   to reach green. A legitimate arrange fix is one you released first.
5. **Read the load-bearing diff**, then run the build and the touched test classes yourself. Run
   the full suite at each batch boundary and before a push, and two or three times for concurrency
   or UI work. For each row that was red, name the line in `src/` that turned it green; a row
   that went green with no `src/` change was satisfied by something else.
6. **Run `Report-Duplication.ps1` before you commit.** It is advice, not a gate, and never fails.
   Read each row under "Added on this branch": a block the implementer copied from existing code.
   Either send it back to extract a shared helper, or keep the copy when it reads better (two tools
   of the same shape, a data table) and log that call with its reason. The "Existing" rows are not
   this task's to fix.
7. **Demand mutation proof** for any test that arrived green.
8. **Treat any new flaky test as a bug** to root-cause before commit. In this delivery that rule
   found a harness trap and three real store bugs. A retry, a longer wait or a wider tolerance
   is not a root cause.
9. **Settle ambiguity yourself and log it.** Record each judgement call with its reason in a
   numbered list; report them all at the end.

## Getting an independent review

For `logic` and `boundary` pairs, and for any concurrency or UI work, dispatch the
`delivery-reviewer` agent (`.claude/agents/delivery-reviewer.md`, Opus, read-only) once the
manager's own checks pass. It exists because the manager's context is the most expensive in the
run and is already anchored on the implementer's report. Skip it for `data` pairs, as for the
architect review.

- **Give it the code, not the conclusion.** The prompt names the diff command (`git diff
  <branch>...HEAD -- src tests`), the Spec sections and plan line ranges, and the red file. It
  does not get the implementer's report, the Coverage table or your opinion of the work. A
  reviewer handed "all green, looks right" agrees.
- **Ask it to find what is wrong.** The prompt says so in the first line and allows `NO
  FINDINGS`. It checks, in order: test integrity (including `Freeze-Tests.ps1 -Check`), passing by
  special case, Spec coverage, scope, and the house rules the build cannot see.
- **Bound it.** Section pointers and `offset`/`limit` only, a 40-call budget, a return of at most
  fifteen ranked lines plus a `COVERAGE` line. A reviewer that reads whole files costs what an
  architect review costs.
- **You triage, it never edits.** Each `BLOCKER` and `MAJOR` goes back to the same implementer by
  resume, or is dismissed in your judgement log with the reason. Do not commit with a `BLOCKER`
  open.
- **Keep it for boundary, composer and UI work.** In the Second Brain delivery it found real
  MAJORs on most boundary pairs: weakened replacement tests, clobbered settings, and crash recovery
  that did not converge.
- **Measure it.** At the next retrospective count what the reviewer found that your own checks
  missed. If that is near zero for a risk tag, stop dispatching it for that tag.
- **Know its limit.** It shares a model family with the implementers, so it can share their
  blind spots. Mutation proof and reading the load-bearing diff remain yours.

## Holding retrospectives

Every ~15 completed tasks, give a read-only Opus agent the token tally and the paths of the
transcripts since the last retrospective. It should grep them, not read them end to end. Ask for:
what to keep, what went wrong with evidence and root cause, cost hot-spots, and concrete changes labelled FACT, BRIEF, PLAN and SCRIPT. Apply them before the next
dispatch. Use one tally unit for the whole run (input + cache_read + cache_creation), detect MAIN
by exact path, and window MAIN from the previous retro commit. The same few findings recurred
across all eight — plan for them from the start:

- behaviour implemented without a test, reported as a "design call";
- the same rule tested at one entry point only;
- `Assert.Contains` where an exact assert would have caught a real bug;
- a test weakened, skipped or retried to reach green instead of the code being fixed;
- forbidden commands used and left out of the report;
- scope that no task owned, found late.

## Moving work between worktrees and the main checkout

- **Commit in the worktree, cherry-pick into the branch** in the main checkout, then run the
  combined build and suite there.
- **`git reset --hard <branch>` keeps untracked files**, so a worktree holding a new, untracked red
  test can be rebased that way. If the red also modified tracked files, commit it as a temporary
  WIP commit, `git rebase <branch>`, and fold it in later with `git reset --soft HEAD~1`.
- **Compare against `origin/main`**, not a stale local `main`, for any branch-wide check.
- **After an API rate-limit stop**, resume each agent from its own context and tell it to run
  `git status` first; none lost work.

## Sharing the checkout with other sessions

Several sessions work in this repo at once. They leave each other dated notes in the gitignored
`Conversation\` folder (`YYYY-MM-DD-topic.md`): read new ones at the start and before the PR, and
reply the same way. A request can change your scope — the Library session asked Tasks to move its
storage layout before shipping.

- **Never stage another session's files**, even when they sit uncommitted in your checkout.
- **Committing your changes to a file another session has dirty:** make your edit in a worktree
  and commit it there; in the main checkout apply that patch to the **index only**
  (`git apply --cached`) and commit. Then bring your lines into their working copy with a
  three-way `git merge-file <their-working-file> <base> <yours>`, after normalising all three to
  CRLF (`git show` emits LF, which otherwise conflicts on every line). Finish with `git diff`
  showing only their hunks.

## Keeping CI and lanes green when owners run in parallel

Each of these came back several times in the Turn Queue and Memory Tidy deliveries (#617-#621),
which ran two owners side by side. Put all three in every owner brief.

| Rule | Why |
| --- | --- |
| A fake e-mail address in a spec, doc or test uses `@example.com` only. Build a mixed-case or unusual address at run time, never as a literal. Before every push, run `gitleaks dir . --config .gitleaks.toml --redact --no-banner` and clear what it finds | `secret-scan` went red three times (runs 1630, 1666, 1677) on placeholder addresses under `generic-personal-email`; each one cost a red PR or a red `main` and a fix commit |
| A red `main` run after a green PR is a flaky test to fix now, before the next merge: read the failing test in the CI log, root-cause it, and grep for the same shape. Never re-run past it, and never merge another PR onto a red `main` | `main` went red five times in one day (runs 1681, 1683, 1688, 1691, 1697), each after its PR had passed. All five failures were in `Huddle.Tests.Agents`, the timing tests that fail under Linux load (fixed by #663 and the wait in `StartAfreshEndToEndTests`) |
| When two lanes use the browser pane, each owner opens its own tab (`tabs_create`), passes its `tabId` on every call, and checks `location.port` is its lane's port before any click, type or post | The pane is shared: the two owners navigated each other's tab, and one paid-case post was interrupted halfway |

## Knowing the Windows and shell traps

| Trap | Workaround |
| --- | --- |
| `/tmp` differs between `python3` and Git Bash | Never hand a file between them through `/tmp` |
| Windows `python3` can't open `/c/Users/...` | Use `C:/Users/...` |
| `python3` on `PATH` may be the Microsoft Store stub in `WindowsApps`, which blocks (an empty heredoc made a call hang until its 120s timeout) | Never call it; use the Edit/Write/Grep tools, or the full path to a real interpreter with `-I` and a script file |
| A call that outlives its timeout moves to a background task and the agent carries on without noticing | Read its output file and stop it with `TaskStop` at once; a "stuck" delivery is often one of these |
| `run.ps1` and dev servers run until Ctrl+C, so a tool call that starts one never returns | Use `./run.ps1 -DryRun` to check a change, or `preview_start` for a server you need up |
| PowerShell has no `head`/`tail` | `Select-Object -First/-Last` |
| `grep -r` over `src/` also matches `bin/*.dll` and `*.xml` | Use `--include=*.cs` or the Grep tool |
| `pwsh -File script.ps1 -Names a,b` from Bash binds one string | Build a real PowerShell array first |
| Passing `$env:` from Bash into `powershell -Command` | Gets eaten — look package APIs up with a script, not a one-liner |

## Notes

- The delivery scripts the Tasks delivery wrote live in version control at
  [`agents/scripts/`](scripts/README.md). Logs and red files still go to the gitignored main
  checkout's `Conversation/`; allowlists like `public-types.txt` are `*.example.txt` there — copy one
  per project rather than sharing the Tasks feature's.
- `Check-Diff.ps1 -Scope Branch -Base origin/main -GroupByFile` lists untested user-facing texts
  and loose `Contains` asserts per file — run it before the PR and clear it.
