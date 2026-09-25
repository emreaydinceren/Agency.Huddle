# Running a multi-agent delivery in this repo

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
   shared scripts and notes resolve from every worktree.
2. **A common brief** every agent reads first: commands, hard rules (below), the red-first
   procedure, and a fixed report shape that includes a **Coverage** table (each plan bullet,
   Spec clause and correction → a test name, or `NOT COVERED` with the task that owns it).
3. **A living facts file** that only the manager edits: exact commands, API locations,
   helpers and their traps, settled decisions. Agents re-deriving the same facts was the largest
   avoidable cost.
4. **An architect review per batch** (a read-only Opus Plan agent against the live code), written
   up as a corrections file marked "settled — implement, do not re-litigate". It overrides the plan.
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
- **One editor per file at a time.** Reds for tasks that share a file can be written in parallel
  (in separate test files); their implementations run one after another, in a stated order.
  Rebase the waiting worktree before its turn.
- **Choose resumed vs fresh agents deliberately.** Resuming within the same component chain costs
  2–6 tool calls to reorient versus 45–56 for a fresh agent, but a long-lived context grows to
  3–4× the cost per turn. Resume for the next task on the same files; start fresh, with a precise
  prompt, for a new area.
- **Pick the model by risk.** Every Haiku result in this delivery needed a fix; Sonnet is the
  default; Opus for concurrency, drag-and-drop, architecture review and retrospectives.
- **Keep verification lean in prompts:** the touched test classes once, fix, once more; the checks;
  the full suite once. A fix-resume runs only the touched classes. The machine allows one test run
  at a time, so extra full runs queue every other agent.

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

Two PreToolUse hooks in `.claude/settings.json` enforce the worst of these: one blocks disk-wide
`find` and `sed -i` in a command position, and one converts files created by `Write` to CRLF.

## Reviewing each report

1. **Audit the transcript** for the forbidden commands, main-checkout writes and heredocs.
2. **Check the red**: it fails only on the new types or members, with no analyzer noise, and
   `git diff --stat -- src/` was empty when it was recorded.
3. **Check the Coverage table.** Every `NOT COVERED` row must cite the task that owns the item; a
   gap nobody owns is assigned now, not after the batch.
4. **Read the load-bearing diff**, then run the build and full suite yourself — two or three
   times for concurrency or UI work.
5. **Demand mutation proof** for any test that arrived green.
6. **Treat any new flaky test as a bug** to root-cause before commit. In this delivery that rule
   found a harness trap and three real store bugs.
7. **Settle ambiguity yourself and log it.** Record each judgement call with its reason in a
   numbered list; report them all at the end.

## Holding retrospectives

Every ~15 completed tasks, give a read-only Opus agent the transcripts since the last
retrospective and ask for: what to keep, what went wrong with evidence and root cause, cost
hot-spots, and concrete changes labelled FACT, BRIEF, PLAN and SCRIPT. Apply them before the next
dispatch. The same few findings recurred across all eight — plan for them from the start:

- behaviour implemented without a test, reported as a "design call";
- the same rule tested at one entry point only;
- `Assert.Contains` where an exact assert would have caught a real bug;
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

## Knowing the Windows and shell traps

| Trap | Workaround |
| --- | --- |
| `/tmp` differs between `python3` and Git Bash | Never hand a file between them through `/tmp` |
| Windows `python3` can't open `/c/Users/...` | Use `C:/Users/...` |
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
