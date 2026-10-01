# Manual test script

This is the browser test script for **Agency.Huddle** — every check a person or an agent can
make against the running app that the automated suite cannot make for you. Follow it cold: it
assumes a terminal, a browser and no knowledge of the codebase.

Applies to the repo as of 2026-09-13. Scope is the chat surface (`src/Huddle.App`) through a web
browser at `http://localhost:5100`. It does **not** cover the automated suite — you never run
`dotnet test` from this script — and it does not cover `Huddle.Console` or the ACP library in
isolation. For why a check is here rather than in code, see [Testing](testing.md); for the
vocabulary every step uses, see [Language](language.md).

**488 tests in 16 areas.** 426 are free. 62 spend real money and are marked 💰 everywhere they
appear; [Planning](manual-tests/planning.md) lists them together.

This page is the contract every run is held to: the cost guard, the Model and Effort convention,
and the rules for concluding a result. It is short on purpose. Read it with
[Common procedures](manual-tests/common.md), which defines the terminals, states, procedures and
oracles every area names instead of restating; those two plus one area file are all you need open.

---

## 0. Read this first

### 0.1 What you need

| Requirement | Detail |
| --- | --- |
| Shell | PowerShell 7 (`pwsh`), opened at the repository root |
| Browser | Chrome or Edge, with DevTools (F12) available |
| Build | `dotnet build Huddle.slnx` succeeds with zero warnings |
| App URL | `http://localhost:5100` |
| State directory | `src\Huddle.App\App_Data` |
| Node | `node --version` prints a version (only for tests that spawn an Adapter) |
| Claude login | `claude` starts without prompting you to log in. The Adapter authenticates through that CLI login — there is no API key to set (same scope as Node: every test that spawns an Adapter, paid or not) |

Two terminals are assumed throughout. **Terminal A** runs the app and is the log oracle —
keep its output visible. **Terminal B** runs `curl.exe` and filesystem checks so they never
disturb the app.

### 0.2 The cost guard

> [!CAUTION]
> A plain `dotnet run` spends real money. `launchSettings.json` pins
> `ASPNETCORE_ENVIRONMENT=Development`, and `appsettings.Development.json` sets
> `Team:Acp:Enabled: true` — so the app starts one real `node` Adapter per Persona at startup and
> bills your Claude subscription before you type anything. This is the opposite of the shipped
> `appsettings.json` default, and it is the single easiest way to spend money by accident.

Run this in Terminal A **before every `dotnet run`** except the tests marked 💰:

```powershell
$env:Team__Acp__Enabled = 'false'
dotnet run --project src/Huddle.App --urls http://localhost:5100
```

Confirm the guard took, in Terminal B:

```powershell
Get-CimInstance Win32_Process -Filter "Name='node.exe'" |
  Where-Object { $_.CommandLine -like '*claude-agent-acp*' } |
  Measure-Object | Select-Object -ExpandProperty Count
```

```text
0
```

Anything above `0` means the guard did not take. Stop the app, set the variable in the same
window, and relaunch. The variable lives only in that terminal session — reopen the terminal and
you must set it again.

> [!NOTE]
> Opening a Model or Effort picker — the New/Edit teammate form — spawns one short-lived probe
> Adapter even with the guard on. That is by design: the probe never starts a Turn, so it spends
> nothing, and gating it on `Acp:Enabled` would leave the picker empty in the default
> configuration (`docs/agencyteam/rules.md` has the reasoning). Expect that row to appear and
> disappear within seconds — it is not a guard failure. A row that persists is.

Three more rules that cost money when broken:

| Rule | Why |
| --- | --- |
| Never set `TEAM_E2E`. | It switches on eight tests that drive a real Adapter. This script never runs the suite, so leave it unset. |
| Set `Team__AgentMessageBudget` low before any paid test. | The default is 40 agent Messages between Human Messages. A test hunting a loop should cap at 2. |
| Unset `ANTHROPIC_API_KEY` before paid tests. | Nothing in this script ever sets it, and it is never needed — the check is against one you inherited from a shell profile or a machine-wide variable. If one is set it silently overrides the `claude` CLI login and bills that key's account instead of your subscription. The Appendix A pre-flight block clears it. |

> [!WARNING]
> `Team:Acp:TraceWire` prints the tool server's bearer token to the console. Enable it only in a
> throwaway terminal that is not shared or recorded, clear the scrollback afterwards, and unset it
> with `Remove-Item Env:Team__Acp__TraceWire` before any other test.

### 0.3 The Model and Effort convention

These are the app's own per-Persona settings on the Teammate card, not your own tooling. They
decide what every paid test costs, so treat them as binding.

| Situation | Model | Effort |
| --- | --- | --- |
| **Every test, unless it says otherwise** | **Haiku** | **low** |
| A test that exercises *switching the Model* | Haiku **→ Sonnet** | leave at low |
| A test that exercises *switching the Effort* | leave on Haiku | low **→ medium** |

Three rules follow from that table:

- **Never select Opus**, anywhere, including when a test says "walk every entry in the list".
  Skip any entry whose label contains Opus.
- **Never raise Effort above `medium`.** `high`, `xhigh` and `max` are out of scope.
- **Restore afterwards.** A test that switches to Sonnet or to `medium` ends by switching back to
  Haiku and `low`. Later tests state Haiku/low as a precondition and their cost estimates assume it.

If the Model list offers no Haiku entry, that is an **inconclusive** result for any test that
names one — record it and move on rather than substituting a larger model.

### 0.4 Rollback

Many tests delete or edit files under `App_Data`. Take a copy before you start, and restore it
between areas:

```powershell
# Back up before the first test.
Copy-Item src\Huddle.App\App_Data "$env:TEMP\App_Data.bak" -Recurse -Force

# Restore: stop the app first, or SQLite will hold team.db open.
Remove-Item src\Huddle.App\App_Data -Recurse -Force
Copy-Item "$env:TEMP\App_Data.bak" src\Huddle.App\App_Data -Recurse -Force
```

> [!NOTE]
> `App_Data` is not tracked by git, so nothing there is recoverable from the repository. The copy
> above is the only rollback you have.

Two files are **absent on a clean install and that is correct**: `App_Data\prompts.json` appears
only on the first save from Settings, and `App_Data\appearance.json` only on the first Theme
change. Finding neither is a pass, not a defect.

### 0.5 How to conclude a result

Every test records exactly one of four outcomes. Choose by this order — the first row that
applies wins.

| Outcome | Choose it when | What to do |
| --- | --- | --- |
| **Blocked** | A precondition could not be met, or an earlier test this one depends on failed. | Record the blocking test id. Do not guess the result. |
| **Inconclusive** | You ran the steps but the environment cannot answer — no Adapter installed, no Haiku in the list, `curl.exe` missing. | Record which condition was absent. Not a defect. |
| **Fail** | Any line under *Fail if* was observed, **or** any line under *Pass if* was not. | Record what you saw, verbatim, plus the implication the *Fail if* line gives, then open a Gitea issue and link it from the [Tracker](manual-tests/tracker.md). |
| **Pass** | Every line under *Pass if* held, and no *Fail if* line was observed. | Record it and move on. |

Four rules make results comparable between testers:

1. **Pass requires all of the pass conditions**, not most. A partial pass is a Fail.
2. **Quote what you saw.** "the tile read `Degraded: the last Turn ended without a reply`" is a
   result; "the tile looked wrong" is not.
3. **A silent failure is still a failure.** Several tests in this script exist because the defect
   they hunt produces no error message anywhere. Absence of an error is never evidence of a pass —
   only the stated pass condition is.
4. **Check section 0.6 before filing a defect.** Some behaviour that looks broken is a documented
   decision.
5. **A Fail is tracked as a Gitea issue**, one per failing test, titled with the test id first —
   `SHELLNAV-01: app.css href has no fingerprint and 404s`. The
   [Tracker](manual-tests/tracker.md) is the index of which tests have one; the issue holds what
   you saw. Blocked and Inconclusive get no issue: nothing is known to be wrong yet.

### 0.6 Not a defect

These are documented decisions in [Known limits](known-limits.md). Observing one is a pass.

| You will see | Why it is correct |
| --- | --- |
| An Agent answering in one Room seems to know about another Room | One session per Persona spans every Room it is in, so context bleeds. A session per Room would multiply processes and cost. |
| A restart un-pauses a Room that had spent its Budget | The Budget counter is in memory and per Room, by decision. |
| A restart loses a Turn that was mid-stream | A Draft is in memory only and is never written to the Transcript. |
| The application does not follow the OS switching to dark | Deliberate since 2026-09-21: a Theme *is* a light one or a dark one, and there is no System option any more. Pick a Theme from the Dark group. See [ADR-0017](../adr/0017-a-theme-is-a-palette-not-a-pair.md). |
| Changing the Theme repaints with no page load | `MudThemeProvider` lives in the render tree, so a Theme change is an ordinary re-render. The full reload the CSS-based system needed is gone. |
| There is no light Monokai, and no dark Solarized Light | An imported VS Code Theme is single-mode upstream, so it ships as one Theme in one group. The Themes that do have both are listed as two — `Huddle Light` and `Huddle Dark`, `Dark+` and `Light+`, and so on. |
| `Dark High Contrast` does not look like VS Code's high contrast | VS Code draws HC borders everywhere from `contrastBorder`, which MudBlazor's palette has no equivalent for. It lands as a strong-contrast ordinary Theme. |
| The same Theme appears in a second browser and a private window | The choice lives in `appearance.json`, per installation, not in `localStorage`. |
| Renaming a Teammate leaves its old Rooms and Transcripts behind | Removing or renaming a Persona does not cascade into the chat surface. Only its Model and Effort follow. |
| Stopping an Agent stops it in every Room | One session spans every Room, so there is nothing narrower to stop. |
| Tool activity never appears in scrollback | It belongs to the Draft and goes when the Draft does. |
| An editing Prompt does not restart a running Teammate | A `Next session` Prompt is deliberately inert until that Teammate restarts. The badge says so. |
| The Model picker is stale after upgrading the Adapter | The catalog is probed once per app run and cached. Restart the app. |
| A `node` row appears in `O-ADAPTERS` right after New/Edit, in a lane the guard says spends nothing | The Model/Effort probe runs even with `Acp:Enabled` false — it never starts a Turn, so it costs nothing. Only a row that persists is a real Adapter. |
| `mcp__team__` appears in tool names | Deliberate. The `Team:` config root and the `mcp__team__` prefix are the two identifiers that keep the old code name. |

### 0.7 Recording results

Results go in the [Tracker](manual-tests/tracker.md) — one row per test, all 472 of them,
carrying a status and the issue number of anything that failed.

Set a row to **Testing** when you pick a test up, so a second tester does not start the same
one, then to **Pass** or **Fail** when you conclude. A Blocked or Inconclusive run leaves the
row **Active** with the reason in Notes, because the test is still unanswered. The Tracker's
own header carries the full status vocabulary and how it maps onto the four outcomes above.

---

## 1. Choosing what to run

The 488 tests live one file per area under [`manual-tests/`](manual-tests/) — a
single file holding all of them would be too large for a git web UI to render. Each
area file carries only what is true of that area alone and names
[Common procedures](manual-tests/common.md) for the rest.

[**File Changes, Watched Folders and Memory**](manual-tests/file-changes.md) is a newer area, not
yet folded into the counts and estimates below: 11 tests, all paid, covering FC §10's FM-0 through
FM-8 plus RS Appendix B's V-1, V-2 and V-4. None of them has been run — the code shipped
2026-09-23, and these paid checks are deferred to the Human's own user acceptance testing. See
[Known limits](known-limits.md) for what stays unverified until they run.

[**Room Sessions**](manual-tests/room-sessions.md) is also newer, and also not yet folded into the
counts and estimates below: 12 tests, all paid, covering RS §10's RS-M1 through RS-M10 plus RS
Appendix B's V-3 and V-5. None of them has been run — the code shipped 2026-09-23, and these paid
checks are deferred the same way File Changes' are. See [Known limits](known-limits.md).

[**Teams: Sidebar, Members, Files, Tasks and Shared Memory**](manual-tests/team-pages.md) is a newer area with 18 tests, 16 free and 2 paid 💰, covering Spec §2's T0–T15 (sidebar navigation, Members, Files, Tasks tabs, Team creation and Project creation) plus TP-REAL-01 and TP-REAL-02 (UI behavior that cannot be automated). None of them has been run — the code shipped 2026-09-23, and the two paid checks are deferred to the Human's own user acceptance testing. See [Known limits](known-limits.md) for what stays unverified until they run.

[**Turn detail and Spend**](manual-tests/turn-detail.md) is a newer area with 5 tests, 1 free and 4 paid 💰, covering the Turn detail spec's TD-M1 to TD-M5 (an edit's live preview, Spend against the Adapter's own figure, an `agency-acp` Persona with no preview and no Spend, phone width and Themes, and a burst of fast calls). None of them has been run — the code shipped 2026-09-30, and the paid checks are deferred to the Human's own user acceptance testing. See [Known limits](known-limits.md).

[**Adapter commands**](manual-tests/adapter-commands.md) is a newer area with 5 tests, 1 free and 4 paid 💰, covering the Adapter commands spec's C1, C5 to C7, C10, C13 and C15 (`@Nova /compact` end to end, a bare slash staying Huddle's own, a disallowed command falling through as text, the Teammate card's command line, and Stop during a compaction). None of them has been run — the code shipped 2026-09-30, and the paid checks, including the spec's V-2 and V-4, are deferred to the Human's own user acceptance testing. See [Known limits](known-limits.md).

[**Prompt blocks**](manual-tests/prompt-blocks.md) is a newer area, registered in the [Tracker](manual-tests/tracker.md) and not yet in [Planning](manual-tests/planning.md): 5 tests, all paid 💰 and cheap (an image cost about 1,000 tokens), covering the Prompt blocks spec's P1, P5, P6, P11 and P18 (a named image seen without a tool call, six images and a cap of four, an image over the size limit falling back to its path, an image mentioned only in catch-up not being re-sent, and an Adapter Profile turning blocks off). PROMPTBLOCKS-01, 02, 03 and 05 passed in the app on 2026-10-01; PROMPTBLOCKS-04, which needs two Teammates in one Room, is deferred to the Human's own user acceptance testing. See [Known limits](known-limits.md).

[**Work Mode**](manual-tests/work-mode.md) is a newer area, also not yet folded into the counts above (it is registered in [Planning](manual-tests/planning.md) and the [Tracker](manual-tests/tracker.md)): 9 tests, 5 free and 4 paid 💰, covering the Work Modes spec's MW-1 to MW-9 (the picker and its hidden list, an Adapter change clearing the mode, a hand-written hidden row being dropped, `Team:Acp:HiddenModes` lifting the block, an Adapter with no mode, `Accept edits` stopping the edit permission request, `Plan` refusing to leave plan mode, the `~/.claude` guard under `Accept edits`, and a mode re-applied after a restart). None of them has been run in the app — the code shipped 2026-09-30, and the paid checks are deferred to the Human's own user acceptance testing. The spec's OQ-2 and OQ-3 were answered outside the app on 2026-10-01 (the plan does not reach the Room, so `plan` is hidden by default; the `~/.claude` guard survives `Accept edits`). See [Known limits](known-limits.md).

[**Questions**](manual-tests/questions.md) is a newer area, also not yet folded into the counts above (it is registered in [Planning](manual-tests/planning.md) and the [Tracker](manual-tests/tracker.md)): 6 tests, all paid 💰, covering the Questions spec's QM-1 to QM-6 (a model framing its ask and calling `ask_human`, ending its Turn without guessing, leaving the tool alone for a fact or an opinion, the same on `agency-acp`, an answer waking only the asker in a Room of three, and a Claude Teammate having no built-in `AskUserQuestion`). Run once in the app on 2026-10-01 (Haiku): QM-2, QM-5 and QM-6 pass, QM-3 passes weakly, QM-1 **failed** (the Claude Adapter defers MCP tools, so the model never saw `ask_human`'s description) until a `systemPrompt.askHuman` paragraph was added, and then passed on a one-sample re-run, and QM-4 was not run for want of an `agency-acp` Profile. See the [Tracker](manual-tests/tracker.md#questions) and [Known limits](known-limits.md).

Two pages exist for the run around the run, and neither is needed while executing:
[**Planning**](manual-tests/planning.md) to pick what to run — the 16 areas with
their counts and estimates, the 20-test smoke pass, the paid-test register, the
corrections already applied and the known gaps — and the
[**Tracker**](manual-tests/tracker.md) to record what came of it, one row per test
with its status and any issue. Open them at either end of a run and close them in
between; this page, Common procedures and one area file are what you keep open.

> [!TIP]
> If you are running the smoke pass, start with `SHELLNAV-01`. It catches the one
> defect in this application that produces no error anywhere: `@Assets["..."]`
> returns an unresolved key verbatim instead of throwing, so a stale stylesheet
> name renders as an ordinary-looking `href` that 404s silently. That exact
> failure shipped live for a month.
