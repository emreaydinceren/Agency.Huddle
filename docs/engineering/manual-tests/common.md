# Common procedures

The setup every area shares, written once. An area's own Setup section carries
only what is true of that area alone, and names a procedure here for the rest.

Read this with [the script](../manual-tests.md) before your first area; keep both
open. Every id below (`T-A`, `P-LAUNCH-FREE`, `O-LOG`…) is quoted verbatim by the
area files.

---

## Terminals

Areas used to name these four different ways. One naming, everywhere:

| Id | Role |
| --- | --- |
| **T-A** | Runs the app. Its scrolling output is `O-LOG`, the oracle most tests read. Keep it visible; never run anything else here. |
| **T-B** | Oracle window — processes, files, database, `curl.exe`. Never start the app from it. |
| **T-C**, **T-D**, … | One per external bot a test connects. The test says which name each carries. |

`<repo>` is the clone root, the folder that holds `Huddle.slnx` (on this machine
`E:\Repos\Huddle2`). Every path in the manual tests is written `<repo>\…`.

All are PowerShell 7 (`pwsh`) opened at `<repo>`. A `$env:` variable set
in one window is invisible to the others and gone when it closes — set it in the
window you will launch from, and re-set it after reopening.

## States

What a test's **Before you start** means by each name.

| Id | Means |
| --- | --- |
| **E-BUILT** | `P-BUILD` has passed in this working tree. |
| **E-FREE** | App running, adapters off at rest — started by `P-LAUNCH-FREE`. A picker probe can still start one short-lived Adapter (`O-ADAPTERS`). Spends nothing. |
| **E-PAID** | App running, adapters on with budget caps — started by `P-LAUNCH-PAID`. 💰 |
| **E-STOPPED** | No app running; nothing answers on `http://localhost:5100`. |
| **E-FRESH** | `E-STOPPED` plus `P-RESET-ALL` since, so `<data>` is what a first run creates. |

## Procedures

### P-BUILD

In `T-A`: `dotnet build Huddle.slnx`. It must report 0 warnings and 0 errors —
warnings are errors in this solution. If it does not, **stop**: every test in the
area is *Inconclusive*, and the build failure is not a finding for that area.

### P-LAUNCH-FREE

The free lane. Run both lines in `T-A`, in this order, in the same window:

```powershell
$env:Team__Acp__Enabled = 'false'
dotnet run --project src/Huddle.App --urls http://localhost:5100 -- --log-level Debug
```

Wait for `Now listening on: http://localhost:5100`. Confirm the guard took with
`O-ADAPTERS`, which must print `0`.

The guard is mandatory and is **not** the shipped default — see
[§0.2](../manual-tests.md#02-the-cost-guard) for why. `Team__Acp__Enabled` is the
right spelling: `Team:` is still the config root though the product is
Agency.Huddle. The equivalent command-line form, for a window you do not want to
carry the variable, is `dotnet run --project src/Huddle.App -- --Team:Acp:Enabled=false`.

### P-LAUNCH-PAID 💰

The paid lane. Only for tests marked 💰, and read
[§0.2](../manual-tests.md#02-the-cost-guard) and
[§0.3](../manual-tests.md#03-the-model-and-effort-convention) first.

First, make the built-in Chief of Staff cheap. It is seeded at every start, has no
stored Model (the account default), and with ACP on and an empty direct Room it takes
a Greeting Turn, which is paid
(`PersonaRunner.cs:481-495`, `BuiltinAgentSeeder.cs`):

1. Launch with `P-LAUNCH-FREE` and open **Agents** in the sidebar.
2. On the **Chief of Staff** card choose **Edit** (`AgentProfileCard.razor:165`), set **Model** to
   Sonnet and **Effort** to `low`, then **Save** (`AgentProfileCard.razor:561`).
3. `P-STOP`.
4. Launch paid, below.

```powershell
Remove-Item Env:ANTHROPIC_API_KEY -ErrorAction SilentlyContinue   # clears an inherited key; no test sets one
Remove-Item Env:TEAM_E2E -ErrorAction SilentlyContinue
$env:Team__AgentMessageBudget = '4'        # the test names a smaller number if it needs one
$env:Team__Acp__TokenBudget   = '200000'   # a ceiling per Persona, not a target
$env:Team__Acp__Enabled       = 'true'
dotnet run --project src/Huddle.App --urls http://localhost:5100 -- --log-level Debug
```

Every Persona must already be Sonnet / low before the first Message. After the last
paid test of a session, run `O-ADAPTERS` expecting `0`; any survivor is an orphaned
Adapter holding a live session open, so stop it with `Stop-Process -Id <ProcessId>`.

### P-CHECKPOINT

Saves a lane at a point, so a later run can go back there instead of rebuilding it. A checkpoint holds the lane's
data folder, its repositories folder and its Adapter session logs (`$HOME\.claude\projects\<lane>-*`). Two scripts
do it: [Save-LaneCheckpoint.ps1](../../../agents/scripts/Save-LaneCheckpoint.ps1) and
[Restore-LaneCheckpoint.ps1](../../../agents/scripts/Restore-LaneCheckpoint.ps1).

**Stop the app first** (`P-STOP`, then wait five seconds). Both scripts refuse while port 5100 answers, while `team.db`
is held open, or while any running process names the lane in its command line, because copying a live `team.db` is
unsafe. Run them in `T-B`, from `<repo>`. Name a checkpoint after the test that just
passed, for example `after-ZERO-16`.

Save:

```powershell
pwsh -NoProfile -File agents/scripts/Save-LaneCheckpoint.ps1 -LaneRoot C:\HuddleZero -Port 5100 -Name after-ZERO-16
```

Restore:

```powershell
pwsh -NoProfile -File agents/scripts/Restore-LaneCheckpoint.ps1 -LaneRoot C:\HuddleZero -Port 5100 -Name after-ZERO-16 -Force
```

**Expect**: save prints `Saved checkpoint 'after-ZERO-16'` with the folder it wrote. Restore prints
`Restored C:\HuddleZero to checkpoint 'after-ZERO-16'`.

- **Restore discards everything in the lane since the checkpoint was saved**: data, repositories and session logs. It
  refuses without `-Force`, so the flag is the acknowledgement. Nothing outside the lane comes back, and no real-world
  effect is undone, such as spend already charged.
- **Restore only to the same absolute path** the checkpoint was saved from. `-LaneRoot` must match it exactly. The
  Adapter names its project folders after that path, so a restore elsewhere would not resume the Agents' sessions.
- Checkpoints go to `$HOME\HuddleLaneCheckpoints\<lane>\<Name>` by default, outside the lane. `-CheckpointRoot` moves
  them, but never inside `C:\HuddleZero`.
- Saving an existing name refuses unless you pass `-Force`, which replaces that checkpoint.
- If a restore was interrupted, `restore-in-progress.json` stays in the lane and both scripts refuse, because the lane
  may be mixed. Restore again with `-Force -ResumeInterrupted`.

### P-LAUNCH-UAT

The free lane with someone to talk to. **Explicit opt-in:** nothing a normal install does
creates these Agents, and the shipped app has no demo agents (`Team:DemoAgent:Enabled`
is `false`). They exist only in the data folder this procedure builds. Use it for any test
that needs an Agent to answer without spending money; the test names it in **Before you
start**.

The cast is **Ada** and **Bo**, two Agents on the free mock adapter, in no Team, each
with an empty one-to-one Room named after itself. Run in `T-A`, from the repo root, after `P-BUILD`:

```powershell
dotnet run --project src\Huddle.Seeder -- --scenario uat --root C:\seeds\huddle\uat
$mock = (Resolve-Path src\Huddle.MockAdapter\bin\Debug\net11.0\mock-acp.exe).Path
dotnet run --project src/Huddle.App -- --urls http://localhost:5100 `
  --log-level Debug `
  --Team:DataDir=C:\seeds\huddle\uat\data `
  --Team:DemoAgent:Enabled=false `
  --Team:Acp:Enabled=true `
  --Team:Acp:Adapters:0:Id=mock `
  "--Team:Acp:Adapters:0:Command=$mock" `
  --Team:Acp:Adapters:0:Args:0=--mock `
  --Team:Acp:Adapters:0:UsesToolNamePrefix=false
```

The seeder wipes `C:\seeds\huddle\uat` first, so running it again **is** the reset: it
returns to exactly Ada, Bo and their two empty Rooms. It never touches `src\Huddle.App\App_Data`.
Wait for `Now listening on: http://localhost:5100`; Ada and Bo go **Online** within a few
seconds.

What the mock Agents do, so a Pass-if is never written against a model:

- A reply is the **whole prompt echoed back**, split into three streamed chunks. It is long,
  it contains the text you sent, and it is plain text with no bold prefix. Judge "a reply
  arrived and contains `<your text>`", never an exact body.
- It goes through the real Reply Gate, so a two-Member Room answers every Message and a Room
  with three or more Members answers only a Mention, exactly as a real Persona does.
- It streams, so a Draft row appears before the Message settles.
- It spends nothing. `O-ADAPTERS` still reports the `mock-acp` processes, and that is correct.

The built-in Chief of Staff is also present and is not part of the cast.

### P-STOP

Ctrl+C in `T-A` and **wait for the prompt to return**. Two instances cannot bind
port 5100, and SQLite connection pooling holds `team.db` for a few seconds after
shutdown — a delete attempted too early is refused. Wait five seconds and retry;
that is known, not a finding.

Changing any `Team__*` variable requires `P-STOP` and a relaunch. It is read at
startup only.

### The five resets

These are **not interchangeable**. Each says exactly what it destroys; a test
names the one it means. `<data>` is the data folder of the lane in use — see
[The data directory](#the-data-directory).

| Id | Do | Destroys | Keeps |
| --- | --- | --- | --- |
| **P-RESET-ALL** | `P-STOP`, then `Remove-Item -Recurse -Force <data>` | Everything — Rooms, Transcripts, Personas, Teams, settings | Nothing |
| **P-RESET-ROOMS** | `P-STOP`, then delete `<data>\rooms` and `<data>\team.db`, `team.db-wal`, `team.db-shm` | Rooms, Member lists, Transcripts | Personas, Teams, settings |
| **P-RESET-TEAMS** | `P-STOP`, then delete every folder under `<data>\Agents` | Every Persona and its Work Dir. The Chief of Staff is re-seeded at the next start | `team.db`, Rooms, Teams, settings |
| **P-RESET-TEAM-FOLDERS** | `P-STOP`, then delete `<data>\Teams` | Team folders, their notes and Tasks | Personas, `team.db`, Rooms, settings |
| **P-RESET-SETTINGS** | `P-STOP`, then delete `<data>\prompts.json` and `<data>\appearance.json` if present | Prompt and Theme overrides | Everything else |

Relaunch with the lane the test names. The data folder is gitignored, so nothing you
delete here is recoverable from the repository — take the
[§0.4](../manual-tests.md#04-rollback) copy first.

Budget counters need no reset: they live in memory and a restart clears them.

### P-NEW-PERSONA

Always through the UI, never by writing the file: **Agents** in the sidebar →
**New agent** → fill **Name**, **Title**, **Alias**, **Teams** (blank unless the
test says otherwise), one sentence in **Persona body**, choose **Model** and
**Effort**, then the card's **Add agent** submit button.

Writing `<data>\Agents\<Name>\<Name>.md` by hand (the definition must sit in the
Agent's own folder; a `.md` at the `Agents` root is rejected) still skips the
`team.db` rows that store Model and Effort, so the Agent runs on the account
default — which may be far more expensive than Sonnet. Leaving **Model** on `Agent default` does the
same thing and is not equivalent to choosing Sonnet.

A file that fails to load appears on `/agents` under **Files that didn't load**
with its reason beside it.

### P-ECHO-BOT

One external agent, one terminal: `pwsh tools/echo-bot.ps1 -Name "<name>"` from
the repo root (keep the quotes for a Name containing a space). It prints every
envelope it receives verbatim, one JSON line each — that console is `O-WIRE`.
Ctrl+C disconnects it; re-running re-registers it.

It replies when the Room has two or fewer Members **or** the envelope says
`"mentioned":true` (`tools/echo-bot.ps1:78`), and it never streams. Its console
is the only `O-WIRE`. Names allow letters, digits, `_`, `-` and single interior spaces, up to
64 characters; a leading, trailing or doubled space is refused with
`"code":"invalidName"` and no Room appears.

## Oracles

### O-LOG

`T-A`'s scrolling output. The console shows only errors unless the launch passes
`--log-level`, so every launch procedure above passes `--log-level Debug`: with it,
every log line these tests quote is visible, and adapter stderr arrives as
`[agent stderr] {Line}`. A test that launches with its own command line must add
`-- --log-level Debug` (or `--log-level Debug` after an existing `--`) before it reads
this oracle. The same lines are also in the run's file under `<DataDir>\logs`. That level buys nothing in the free lane: there
are no `Debug`-level log statements in `Huddle.App` or `Huddle.Contracts` at all, and
the only three in the solution live in `Huddle.Acp`. Every line quoted in this script
is `info:` or `warn:`, so a free-lane console with no `dbug:` line in it is correct. Scroll to the bottom before each test so you
can tell new lines from old, and never filter it — the *absence* of a line is the
oracle in several tests.

### O-ADAPTERS

Counts **Adapter** processes, not every `node.exe` — a developer machine normally
has a dozen unrelated ones, so a bare `Get-Process node` is not a valid count. It
counts `node.exe` processes running `claude-agent-acp` only; the free mock adapter
(`mock-acp`, `P-LAUNCH-UAT`) is invisible to it and is counted by `O-MOCK-ADAPTERS`.

```powershell
Get-CimInstance Win32_Process -Filter "Name='node.exe'" |
  Where-Object { $_.CommandLine -like '*claude-agent-acp*' } |
  Measure-Object | Select-Object -ExpandProperty Count
```

`0` is the resting value in `E-FREE`. In `E-PAID` it equals the number of Online Agents. In
BOTH lanes, opening a Model or Effort picker adds one short-lived extra row — the catalog probe
runs even with `Acp:Enabled=false`, by design (`docs/engineering/rules.md`), because it spends
nothing: it never starts a Turn. That row disappearing within seconds is the probe finishing,
not a cost-guard failure. A row that PERSISTS in `E-FREE` is not a probe — it is a real Adapter,
and that is the failure PERSONALIFECYCLE-01 exists to catch.

It returns `0` cleanly when nothing is running — unlike `Get-Process node`, which
errors with `Cannot find a process with the name "node"` and has to be read as a
zero.

### O-ADAPTERS-LIST

The same filter, listing rows instead of counting them. Use it wherever a test
needs a process **id** — to watch one survive, to see it replaced, or to kill it
with `Stop-Process -Id <ProcessId>` to simulate a crashed Adapter.

```powershell
Get-CimInstance Win32_Process -Filter "Name='node.exe'" |
  Where-Object { $_.CommandLine -like '*claude-agent-acp*' } |
  Select-Object ProcessId, CreationDate, CommandLine | Format-Table -Wrap
```

One row per running Persona Adapter, plus a short-lived extra row whenever a Model
or Effort picker probes. `CommandLine` names the Persona, so this is also how you
tell which Adapter belongs to which Agent.

### O-TRANSCRIPT

`<data>\rooms\{RoomId}.jsonl` — one JSON Message per line, appended only when a
Message is **accepted**. A refused Message is absent from it, which makes this the
definitive "did it persist" oracle. `{RoomId}` is the 32 hex characters after
`/rooms/` in the address bar.

The `rooms` folder does not exist until the first Message is posted anywhere. Its
absence on a fresh install is normal.

### O-DB

`<data>\team.db` — SQLite, tables `users(id,name,kind)`, `rooms(id,name,created)`,
`room_members(room_id,user_id)`, `persona_models`, `persona_efforts`. It is in WAL
mode, so it reads fine while the app runs.

`sqlite3` may not be on PATH. Unless a test names `O-DB` as its **only** oracle,
it is a secondary confirmation: skip the sub-check and judge on the browser and
`O-LOG`, recording that sub-check as not-checked. Never mark a test *Fail* for a
missing client. To install one: `winget install -e --id SQLite.SQLite`, then open
a fresh `T-B`.

### O-WIRE

An external bot's console (`P-ECHO-BOT`), one JSON line per envelope. In a
`"type":"messagePosted"` line: `"mentioned"` is whether *this* recipient was named,
`"mentions"` everyone the Message named, `"members"` the whole Room, plus
`"agentMessagesSinceHuman"` and `"budget"`.

No envelope at all means the Agent is not a Member or is not connected. An envelope
with `"mentioned":false` means it **is** a Member and the parser decided against it.
Those are different diagnoses.

`O-WIRE` is the echo-bot console only. It never sees an in-process Agent, so it
cannot observe a Persona's prompt or tool calls; use `O-TOOLCALLS` or the mock
adapter's echo for those (see [Never set TraceWire](#standing-conventions)).

### O-TOOLCALLS

`<data>\logs\tool-calls\<Name>\<yyyy-MM-dd>.jsonl` — one JSON line per tool call a
Persona made, one file per UTC day (`<Name>` has characters invalid in a file name
replaced by `_`). Read the newest line:

```powershell
Get-Content "<data>\logs\tool-calls\<Name>\$((Get-Date).ToUniversalTime().ToString('yyyy-MM-dd')).jsonl" -Tail 1
```

Fields, camelCase: `time` (UTC, ISO-8601 with a `Z`), `room`, `task` (`null` when
unknown), `tool`, `arguments` (the file the call touches, when known, then the
call's input as compact JSON; cut to 200 characters with a trailing ellipsis),
`status`, `durationMs`, `result` (the text one of Huddle's own App Tools returned,
such as a refusal; `null` for every other tool; cut to 200 characters). The writer
is `ToolCallLogWriter.Append` and the cut is its `Truncate`. The folder does not
exist until the Persona's first tool call.

### O-FILE

Open the named file in an editor and read it. Used where a test must confirm what is
stored on disk (a Persona's frontmatter, a file a Candidate produced) and the
database has no row for it.

### O-MOCK-ADAPTERS

Counts the free mock adapter processes started by `P-LAUNCH-UAT`:

```powershell
Get-Process mock-acp -ErrorAction SilentlyContinue | Measure-Object | Select-Object -ExpandProperty Count
```

It prints `0` when none is running. `O-ADAPTERS` never counts these.

## The data directory

`<data>` is `<repo>\src\Huddle.App\App_Data` on the free and paid lanes — note
`src\Huddle.App\`, never the repo root. `Team:DataDir` defaults to the relative path
`App_Data`, resolved against the process working directory, and
`dotnet run --project src/Huddle.App` sets that to the project folder. Under
`P-LAUNCH-UAT` the data folder is `C:\seeds\huddle\uat\data`, and that is `<data>`.

| Path | Holds |
| --- | --- |
| `Agents\<Name>\<Name>.md` | One Persona's definition. Nothing else but `work\` may sit in the folder |
| `Agents\<Name>\work\` | That Persona's Work Dir |
| `Teams\` | Team folders, their notes and Tasks (`Teams\<Team>\[<Project>\]_tasks\[_closed]`) — never Personas |
| `views.json` | Saved Task Views — **absent until first save** |
| `rooms\<RoomId>.jsonl` | One Room's Transcript |
| `team.db` (+ `-wal`, `-shm`) | The directory database |
| `logs\tool-calls\<Name>\<yyyy-MM-dd>.jsonl` | One Persona's tool-call log, one file per UTC day |
| `prompts.json`, `appearance.json` | Settings overrides — **absent until first save**, which is correct |

If the folder is not there, `Get-ChildItem -Recurse -Filter team.db <repo>`
finds where `Team:DataDir` actually resolved.

## Standing conventions

- **Model and Effort** are [§0.3](../manual-tests.md#03-the-model-and-effort-convention)
  in every area: Sonnet and `low` unless a test is the one exercising a switch. No
  area restates it.
- **Never set `TEAM_E2E`.** It gates eight money-spending tests in
  `tests/Huddle.Acp.Tests` and has nothing to do with the app.
- **Never set `Team__Acp__TraceWire`.** It prints the tool server's bearer token
  into `O-LOG`. It stays forbidden; use the oracle that replaces what you wanted
  from it: tool calls and the file-changes block through `O-TOOLCALLS`; the prompt
  text a Persona received through the mock adapter's echo under `P-LAUNCH-UAT`
  (the reply is the whole prompt); a Persona's stored definition through `O-FILE`.
- **Clear every variable a test set** before the next one, or it inherits and
  reports a false result. `Get-ChildItem Env:Team__*` lists what is set.
- **"Invite agent" and "Add agent" are two different controls.** The invite
  toggle in a Room header (an icon button; its tooltip and `aria-label` read
  "Invite agent") and the submit button of the Create card on `/agents`
  (**Add agent**). Find the Room-header one by container (`.invite-agent`
  wraps it) or `aria-label`, never by text alone.
- **Four controls are icon buttons with no visible text.** **Remove** on the
  Agent card (`PersonRemove`), **Stop** on a streaming Draft (`StopCircle`),
  **Settings** in the drawer (`Settings`, pinned to the bottom) and **Archived**
  in the drawer (`Archive`, directly above Settings). Each carries a `MudTooltip` and
  an `aria-label` spelling the name this script uses, so **find them by
  `aria-label`, never by button text** — in DevTools,
  `document.querySelector('[aria-label="Stop"]')`. Steps written before this change
  still name them in words; that is the same control. **New agent** on
  `/agents` is a text button (first in the toolbar), and **Agents** in the
  drawer and the **Add agent** submit button also show their text.
- **A Room can be renamed, and a renamed Room stops auto-renaming.** The Edit icon
  beside a Room's title puts it into edit mode; Enter or blur commits, Escape
  cancels. Until somebody renames it, a Room is still named after its Agent Members
  and is still re-derived on every Invitation — after a rename it is not. So a test
  that expects an invite to change a Room's name must use a Room nobody has
  renamed, and the reverse is a test of its own.
- **There is no send button.** The composer is a bare textarea with placeholder
  `Message {Room name}… (/invite @Agent)`; Enter posts, Shift+Enter adds a newline. Its absence
  is not a defect.
- **There are no demo agents in manual testing.** The built-in `echo` and `alpha`
  agents are off by default (`Team:DemoAgent:Enabled` is `false`) and exist for the
  automated tests only; no manual test turns them on. A test that needs an Agent to
  answer for free uses `P-LAUNCH-UAT` and **Ada** and **Bo**, which are real Personas
  on the mock adapter, appear on `/agents`, and exist only in the data folder that
  procedure builds. A test that needs an external pipe client uses `P-ECHO-BOT`.
- **Names are permanent; Rooms are not, since 2026-09-21.** Nothing in the UI
  deletes an *Agent*, so every Name you connect under still leaves a Room behind —
  but that Room can now be archived or deleted from the context menu on its sidebar
  row ([ADR-0018](../../adr/0018-a-room-can-be-archived-or-deleted.md)). Appending a
  digit to a reused Name is therefore a convenience rather than a necessity. Prefer
  it anyway when a test's oracle counts sidebar entries: deleting a Room is
  irreversible, and a test that deletes the wrong one cannot put it back.
- **`Team:` and `mcp__team__` keep the old code name on purpose.** Not a typo, not
  a branding bug.

---

Back to [the manual test script](../manual-tests.md).
