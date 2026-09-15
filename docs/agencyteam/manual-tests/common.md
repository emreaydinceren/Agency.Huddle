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

All are PowerShell 7 (`pwsh`) opened at `E:\Repos\Huddle`. A `$env:` variable set
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
| **E-FRESH** | `E-STOPPED` plus `P-RESET-ALL` since, so `App_Data` is what a first run creates. |

## Procedures

### P-BUILD

In `T-A`: `dotnet build Huddle.slnx`. It must report 0 warnings and 0 errors —
warnings are errors in this solution. If it does not, **stop**: every test in the
area is *Inconclusive*, and the build failure is not a finding for that area.

### P-LAUNCH-FREE

The free lane. Run both lines in `T-A`, in this order, in the same window:

```powershell
$env:Team__Acp__Enabled = 'false'
dotnet run --project src/Huddle.App --urls http://localhost:5100
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

```powershell
Remove-Item Env:ANTHROPIC_API_KEY -ErrorAction SilentlyContinue   # clears an inherited key; no test sets one
Remove-Item Env:TEAM_E2E -ErrorAction SilentlyContinue
$env:Team__AgentMessageBudget = '4'        # the test names a smaller number if it needs one
$env:Team__Acp__TokenBudget   = '200000'   # a ceiling per Persona, not a target
$env:Team__Acp__Enabled       = 'true'
dotnet run --project src/Huddle.App --urls http://localhost:5100
```

Every Persona must already be Haiku / low before the first Message. After the last
paid test of a session, run `O-ADAPTERS` expecting `0`; any survivor is an orphaned
Adapter holding a live session open, so stop it with `Stop-Process -Id <ProcessId>`.

### P-STOP

Ctrl+C in `T-A` and **wait for the prompt to return**. Two instances cannot bind
port 5100, and SQLite connection pooling holds `team.db` for a few seconds after
shutdown — a delete attempted too early is refused. Wait five seconds and retry;
that is known, not a finding.

Changing any `Team__*` variable requires `P-STOP` and a relaunch. It is read at
startup only.

### The four resets

These are **not interchangeable**. Each says exactly what it destroys; a test
names the one it means.

| Id | Do | Destroys | Keeps |
| --- | --- | --- | --- |
| **P-RESET-ALL** | `P-STOP`, then `Remove-Item -Recurse -Force src\Huddle.App\App_Data` | Everything — Rooms, Transcripts, Personas, settings | Nothing |
| **P-RESET-ROOMS** | `P-STOP`, then delete `App_Data\rooms` and `App_Data\team.db`, `team.db-wal`, `team.db-shm` | Rooms, Member lists, Transcripts | Personas, settings |
| **P-RESET-TEAMS** | `P-STOP`, then delete every `.md` under `App_Data\Teams`, sub-folders included | The Persona library | `team.db`, Rooms, settings |
| **P-RESET-SETTINGS** | `P-STOP`, then delete `App_Data\hooks.json` and `App_Data\appearance.json` if present | Hook and Theme overrides | Everything else |

Relaunch with the lane the test names. `App_Data` is gitignored, so nothing you
delete here is recoverable from the repository — take the
[§0.4](../manual-tests.md#04-rollback) copy first.

Budget counters need no reset: they live in memory and a restart clears them.

### P-NEW-PERSONA

Always through the UI, never by writing the file: **Teammates** in the sidebar →
**New teammate** → fill **Name**, **Title**, **Alias**, **Teams** (blank unless the
test says otherwise), one sentence in **Persona body**, choose **Model** and
**Effort**, then the card's **Add teammate** submit button.

Writing `App_Data\Teams\<Name>.md` by hand skips the `team.db` rows that store
Model and Effort, so the Teammate runs on the account default — which may be far
more expensive than Haiku. Leaving **Model** on `Use the agent's default` does the
same thing and is not equivalent to choosing Haiku.

A file that fails to load appears on `/teammates` under **Files that didn't load**
with its reason beside it.

### P-ECHO-BOT

One external agent, one terminal: `pwsh tools/echo-bot.ps1 -Name "<name>"` from
the repo root (keep the quotes for a Name containing a space). It prints every
envelope it receives verbatim, one JSON line each — that console is `O-WIRE`.
Ctrl+C disconnects it; re-running re-registers it.

It replies **only** when the envelope says `"mentioned":true`, and it never
streams. Names allow letters, digits, `_`, `-` and single interior spaces, up to
64 characters; a leading, trailing or doubled space is refused with
`"code":"invalidName"` and no Room appears.

## Oracles

### O-LOG

`T-A`'s scrolling output. `Agency.Huddle` logs at `Debug` in Development, so every
log line these tests quote is visible without configuration, and adapter stderr
arrives as `[agent stderr] {Line}`. Scroll to the bottom before each test so you
can tell new lines from old, and never filter it — the *absence* of a line is the
oracle in several tests.

### O-ADAPTERS

Counts **Adapter** processes, not every `node.exe` — a developer machine normally
has a dozen unrelated ones, so a bare `Get-Process node` is not a valid count.

```powershell
Get-CimInstance Win32_Process -Filter "Name='node.exe'" |
  Where-Object { $_.CommandLine -like '*claude-agent-acp*' } |
  Measure-Object | Select-Object -ExpandProperty Count
```

`0` is the resting value in `E-FREE`. In `E-PAID` it equals the number of Online Teammates. In
BOTH lanes, opening a Model or Effort picker adds one short-lived extra row — the catalog probe
runs even with `Acp:Enabled=false`, by design (`docs/agencyteam/rules.md`), because it spends
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
tell which Adapter belongs to which Teammate.

### O-TRANSCRIPT

`App_Data\rooms\{RoomId}.jsonl` — one JSON Message per line, appended only when a
Message is **accepted**. A refused Message is absent from it, which makes this the
definitive "did it persist" oracle. `{RoomId}` is the 32 hex characters after
`/rooms/` in the address bar.

The `rooms` folder does not exist until the first Message is posted anywhere. Its
absence on a fresh install is normal.

### O-DB

`App_Data\team.db` — SQLite, tables `users(id,name,kind)`, `rooms(id,name,created)`,
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

## The data directory

`E:\Repos\Huddle\src\Huddle.App\App_Data` — note `src\Huddle.App\`, never the repo
root. `Team:DataDir` defaults to the relative path `App_Data`, resolved against the
process working directory, and `dotnet run --project src/Huddle.App` sets that to
the project folder.

| Path | Holds |
| --- | --- |
| `Teams\` | The Persona library, `.md` files, scanned recursively |
| `work\<Persona>\` | That Persona's Work Dir |
| `rooms\<RoomId>.jsonl` | One Room's Transcript |
| `team.db` (+ `-wal`, `-shm`) | The directory database |
| `hooks.json`, `appearance.json` | Settings overrides — **absent until first save**, which is correct |

If the folder is not there, `Get-ChildItem -Recurse -Filter team.db E:\Repos\Huddle`
finds where `Team:DataDir` actually resolved.

## Standing conventions

- **Model and Effort** are [§0.3](../manual-tests.md#03-the-model-and-effort-convention)
  in every area: Haiku and `low` unless a test is the one exercising a switch. No
  area restates it.
- **Never set `TEAM_E2E`.** It gates eight money-spending tests in
  `tests/Huddle.Acp.Tests` and has nothing to do with the app.
- **Never set `Team__Acp__TraceWire`.** It prints the tool server's bearer token
  into `O-LOG`.
- **Clear every variable a test set** before the next one, or it inherits and
  reports a false result. `Get-ChildItem Env:Team__*` lists what is set.
- **"Add teammate" is two different buttons.** The invite toggle in a Room header
  and the submit button of the Create card on `/teammates`. Both are `MudButton`s
  now, so neither carries a distinguishing class of its own — disambiguate by
  **page**, or by container (`.invite-teammate` wraps the Room-header one), never by
  text alone. Note MudBlazor renders button labels upper-case, so both read
  "ADD TEAMMATE".
- **There is no send button.** The composer is a bare textarea with placeholder
  `Message… (/invite @agent)`; Enter posts, Shift+Enter adds a newline. Its absence
  is not a defect.
- **`echo` and `alpha` are demo agents** — pipe clients, not Personas
  (`Team:DemoAgent:Enabled` is `true` in `appsettings.json`). They hold Rooms and
  spend Budget, and must never appear on `/teammates`. Turn them off with
  `$env:Team__DemoAgent__Enabled = 'false'`.
- **Names are permanent.** Nothing in the UI deletes a Room or an Agent, so every
  Name you connect under leaves a Room behind. When a test needs a new Name and it
  is already in the sidebar, append a digit and use that spelling throughout.
- **`Team:` and `mcp__team__` keep the old code name on purpose.** Not a typo, not
  a branding bug.

---

Back to [the manual test script](../manual-tests.md).
