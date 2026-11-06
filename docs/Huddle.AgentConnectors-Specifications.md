# Huddle.AgentConnectors - Design Specification

**Date:** 2026-11-06 - **Status:** Proposed, awaiting the owner's approval. No product code until approved. -
**Issue:** #357 - **Decision record:** [ADR-0080](adr/0080-an-agent-reaches-the-humans-account-connectors-only-when-the-human-switches-them-on.md)
(Proposed) - **Spike results:** [acp-session-config.md](engineering/acp-session-config.md) ("Switching the claude.ai connectors
off for an Agent") - **Vocabulary:** [language.md](engineering/language.md) (**Agent**, **Adapter**, **Human**, **Turn**)

This page is for the owner, who approves it, and for the developer who builds it afterwards. It designs one switch on the
Agent card: by default no Agent can use the account's claude.ai connectors (mail, files, calendar and so on); the Human can
turn them on for one Agent. Section 0 lists what the owner must decide. Sections 1 to 14 are the design.

## 0. Decisions for the owner

The decisions already made are in section 2 and are not reopened here. These four are open. Each gives the recommendation
first, then what each choice costs.

| # | Question | Recommendation | What each choice costs |
| --- | --- | --- | --- |
| OQ-1 | With the switch on, should a connector call stop and ask the Human before it runs? Today nothing asks (section 4). | **Yes: every call to a connector tool asks, and the switch does not ship until it does.** | **Ask for every call:** safest; a build of its own, because no Human-approval path exists in the permission handler today (section 8.4); a busy Agent asks often. **Ask only for tools that write** (send, delete, create): fewer prompts, but needs a list of tool names kept up to date, and a new connector tool defaults to unguarded. **Ask for none:** the switch becomes "this Agent may send and delete mail with nobody asked"; the warning on the card must then say so in as many words. |
| OQ-2 | Run the paid Q5 check (one harmless read, such as `get_me`) now? | **No. Pin the handler's behaviour with a free test now, and run the paid read once, in stage 3, after OQ-1 is built.** | **Now:** about one Turn of spend (under 0.05 USD at the spike's prices) to confirm the code reading, which a free test already shows. **In stage 3:** the same read then proves the prompt appears, which is the check that matters. **Never:** the prompt is unproven end to end. |
| OQ-3 | How are existing installs told that Agents lost their connectors? | **A release note plus a one-time, dismissible notice on the Agents page.** | **Release note only:** nothing to build; an Agent that used a connector fails on its next Turn with no explanation. **Plus the notice:** one small stored flag and one `MudAlert`. **Per-Agent Feed item for each Agent that used a connector:** the app cannot tell which did, so this would need a scan of session logs; not recommended. |
| OQ-4 | When an Agent is renamed, does its access follow the name? | **No: the row is removed, and the Human switches it on again.** | **Drops:** one extra click after a rename, never a surprise grant. **Follows:** a rename keeps access, but any code path that renames a Persona then carries a grant, and the next rename bug becomes an access bug. |

## 1. Goal

No Agent can use the Human's account connectors unless the Human deliberately turned them on for that Agent, in the UI.

Evidence, from the first from-zero run: all 11 Agents listed three connector sets beside `mcp__team__`
(`mcp__claude_ai_ms365__`, `mcp__claude_ai_Claude_Docs__`, `mcp__claude_ai_Microsoft_Learn__`). The ms365 set includes mail
search, send and `outlook_batch_delete_messages`. No Agent called any of them in that run. Nothing in Huddle limited them.

| Session | User-level Claude Code settings | Account connectors | Other MCP servers |
| --- | --- | --- | --- |
| Helper (`HelperSessionMeta.cs:26-28`) | Off | Off: `env` `ENABLE_CLAUDEAI_MCP_SERVERS=false` | Off: `extraArgs` `strict-mcp-config` |
| Agent today (`BuildIsolationMeta`, `DotAcpAgentHostFactory.cs:590-608`) | Off: `settingSources: ["project","local"]` | **On** | Not restricted |
| Agent after this spec | Off | **Off unless the Human switched it on** | Not restricted (section 3.3) |

## 2. Binding decisions

These come from the owner and the issue. The design starts from them.

| # | Decision |
| --- | --- |
| B-1 | Off by default for every Agent, including the Chief of Staff and hand-written Agents. No exceptions list. |
| B-2 | Only the Human turns it on, in the UI. An Agent must not be able to grant itself access. |
| B-3 | The setting lives outside the Agent's own file: a SQLite table like `PersonaWorkModeStore`. Never in frontmatter. |
| B-4 | All or nothing. Per-connector choice is recorded as later (section 12). |
| B-5 | `requires` and `connectors.json` never grant access on their own. |
| B-6 | A Team duplicate or `propose_agents` must not copy "on" by accident. |

## 3. What the spike established

Full results, with session ids, are in `acp-session-config.md`. Only what the design depends on is repeated here.
All were run against `@agentclientprotocol/claude-agent-acp` 0.100.0.

### 3.1 The lever

`_meta.claudeCode.options.env.ENABLE_CLAUDEAI_MCP_SERVERS = "false"` on the session removes every `claude_ai` tool for that
session. `mcp__team__` keeps working, because Huddle passes it through ACP `mcpServers`.

The Adapter builds the session environment as `{ ...process.env, ...options.env }`, so the entry **adds** to the environment:
`git`, `dotnet`, `node` and sign-in still work (Bash printed all three versions in an env-off session).

### 3.2 When the connectors appear

The connectors are not in the tool set on Turn 1. They connect afterwards and arrive as a `deferred_tools_delta` attachment on
Turn 2. **Any check of the tool list must look at Turn 2 or later.** A Turn-1 check reads the same on and off. Three Turns and
the session log (`claude_ai` present or absent) are the proof, not the model's reply alone.

### 3.3 `strict-mcp-config` is not used

The variable alone removes the connectors, so strict adds nothing for them. Strict is harmful on an Agent: it hides the Work
Dir's own `.mcp.json` servers (without it the Agent listed `mcp__projsrv__update_task`; with it, only `mcp__team__`).
Helpers keep strict, because they have no MCP servers of their own.
**Not tested:** MCP servers a Human put in `~/.claude.json`. The isolation sets `settingSources` without `user`, but the spike
did not prove those servers stay out. Recorded as out of scope (section 12).

### 3.4 Resume

A session opened with connectors on, then resumed in a new Adapter process with the variable off, has no connectors, keeps its
conversation, and `mcp__team__update_task` still works. So a restart is how an existing Agent loses access.
**Not tested:** the reverse, a session opened with connectors off and resumed in a new process with them on. The design
assumes the connectors then appear from the second Turn, as in a fresh session. **Unverified**; stage 3 verification
checks it (section 11.2).

### 3.5 Cost

About 3,900 tokens of context per Turn from Turn 2 (36,238 / 40,136 / 42,233 on; 36,240 / 36,274 / 36,523 off), about 11
percent, and +0.015 USD on the Turn they first appear. The default also saves money.

### 3.6 Other Adapters

The `claudeCode` entry is read by `claude-agent-acp` only. A configured profile defaults to `IsolateUserSettings: false`, gets
no `_meta` at all and logs a warning (`AdapterCatalog.cs:33-43`). Its one lever is the profile's `EnvironmentOverrides`, which
is per profile, not per Agent. So the per-Agent switch cannot deliver control there (section 6).

### 3.7 A stopgap that works today

Setting `ENABLE_CLAUDEAI_MCP_SERVERS=false` in the environment before launching works, tested at host level
(`AgentProcessOptions.EnvironmentOverrides`, session `746bbc58`; a full Huddle launch was not run). It is all-or-nothing for
every Agent on that profile. It goes in the release note for anyone who needs it before this ships.

## 4. The risk this feature must face

**With the switch on, an Agent can use the Human's mail, files and calendar, and today nothing asks first.**

What the code shows (read, not run; spike question Q5 was not run):

- In `default` (Manual) mode the Adapter's permission check raises a permission request for an MCP tool.
- Huddle's `WorkDirPermissionHandler.DecideAsync` answers it. It refuses only: a write to the protected `~/.claude` directory
  (`WorkDirPermissionHandler.cs:169`), the four file guards (`:179-182`) and a gated `git merge` or `git push` (`:196-199`).
  Everything else ends at `return Task.FromResult(Approve(context.Options))` (`:210`).
- A connector call matches none of those guards. **It would be approved with no Human prompt.**
- ADR-0079 already records this shape: "approves it with no Human unless a guard or the merge gate refuses."
- `rules.md` says the same: "nothing asks the Human before a tool runs."

So the switch, built alone, means: "this Agent may send, reply to or delete mail, and write to files, with nobody asked."
The warning on the card cannot make that safe; it can only describe it. OQ-1 asks whether a connector call under the switch
should stop for approval. The recommendation is yes, and the design in section 8.4 shows what that takes.
**This spec does not assume the answer.** Sections 5 to 7 hold either way; section 8.4 and the verification change with it.

## 5. The default, and who it covers (decision 1)

Every Agent starts with connectors off. This covers:

- Agents created from the card, from `propose_agents`, by hiring a role, by Team duplication, or by hand-writing a file.
- The Chief of Staff and every built-in Agent. `BuiltinAgentReset` resets stored rows by deleting them
  (`BuiltinAgentReset.cs:23`); the new table is cleared the same way, so a reset returns an Agent to off.
- Every existing Agent after upgrade. There is no migration: no row means off.

"Off" is the absence of a row. There is no stored "off".

## 6. Who can turn it on, and how it is stored (decision 2)

### 6.1 The store

`PersonaConnectorsStore`, in `src/Huddle.App/Data/`, modelled on `PersonaWorkModeStore` and built on the same
`PersonaOverrideTable` (so a blank value deletes the row):

```sql
CREATE TABLE IF NOT EXISTS persona_connectors (
    persona_name TEXT PRIMARY KEY COLLATE NOCASE,
    enabled_at   TEXT NOT NULL      -- ISO 8601 UTC, written from TimeProvider
);
```

A new table, not a column, for the reason `PersonaWorkModeStore` records: `CREATE TABLE IF NOT EXISTS` ignores an added column
on a database that predates it. A row present means on; its value is the moment the Human switched it on. Registered as a
singleton next to `PersonaWorkModeStore` (`ServiceCollectionExtensions.cs:390`).

### 6.2 What an Agent can and cannot reach

| Path | Can an Agent grant itself access? |
| --- | --- |
| Its own file's frontmatter | No. The flag is not read from there, and the frontmatter schema gets no such key. |
| `propose_agents` | No. `Candidate` carries only a name, text and a few fields; its own comment says it "cannot set Model, Effort, Adapter or Skills" (`Candidate.cs:6`). It gets no connectors field. |
| Any App Tool (`mcp__team__`) | No. No App Tool calls `PersonaStore.Add`, `Update` or `Remove`; verified by search of `src/Huddle.App/Acp/Tools`. The `Add` callers are Human-driven (`ProposalService.cs:131` on the Human's accept, `HireService`, the card). |
| The card, over Blazor | Only a Human session. The card is the one caller of `PersonaStore.SetConnectors`. |
| A shell command writing `team.db` | **Yes, in principle.** The guards see path arguments only; a shell command gets past them (`RawGuard.cs` remarks: "that boundary is soft and documented"). No guard names `team.db`. This is the same soft boundary that protects `AGENTS.md` and Raw today. Recorded as accepted; stage 3 adds a test that pins the guards do not claim to cover it (section 10). |

### 6.3 Where it is written

`PersonaStore.SetConnectors(name, enabled)`: takes `writeGate`, sets or removes the row, publishes a fresh index, and raises
`PersonasChanged` so the supervisor restarts the host (section 8). It is the **only** writer. `PersonaStore.Update` does not take
the flag, which keeps `TeamMembership.cs:182`, `RetroProposalService.cs:359` and `PackUpdater.cs:47` (all call
`Update(..., persona.WorkMode)`) unable to change it.

`Add` and `Remove` follow the existing pattern: `Remove` deletes the row (`PersonaStore.cs:515` removes the sibling rows, so a
re-created Persona of the same name cannot resurrect old access). A rename removes the old row and writes none (OQ-4).

### 6.4 The Persona record

`Persona` gains a trailing `bool Connectors = false` (`Persona.cs:35`), filled from the store at `PersonaStore.cs:304`. A scalar
on the record, so `NeedsRestart` (`PersonaSupervisor.cs:399`, `persona != started`) restarts the host on a change through
record equality, exactly as it does for Work Mode. The name `Connectors` is used in code; the card says "Account connectors".

## 7. The switch on the Agent card (decision 3)

### 7.1 Placement and shape

A new row on `AgentProfileCard`, named **`P6a` Account connectors** in `wireframes.md`, under Work mode in View mode. It is a
Group label "Account connectors" and a `MudSwitch<bool>` (precedent: `HelperCard.razor:30`, `RemoteAccessPanel.razor:38`; Switch
in `agents/MudBlazorDesign.md:134`), with a helper line under it.

It acts **immediately and does not wait for Save**: it is a grant with an audit record, not a field of the persona text.
Turning it **on** swaps in an `InlineConfirm` (`O6`): "Give {Name} access to all your connected accounts?" with Cancel then
**Give access**. Turning it **off** applies at once.

Edit and New agent modes do not show the row: a new Agent is always off, and the grant is a deliberate act on an existing Agent.

### 7.2 Copy

| State | Copy |
| --- | --- |
| Off | Helper: "Off. {Name} cannot use your connected accounts." |
| On | Helper: "On since {date}. {Name} can use **all** of your connected accounts." |
| Confirm | "{Name} will be able to use every account connector on this computer's claude.ai sign-in. {warning}" |
| Warning, if OQ-1 is yes | "Each use will ask you first." |
| Warning, if OQ-1 is no | "It will not ask you before it sends or deletes anything." |
| Applying | "Restarting {Name} to apply this." |

Naming what "all" includes: the spike saw Claude Docs, Microsoft Learn and ms365 (mail, calendar, files, Teams), 61 tools, among
them `outlook_send_mail` and `outlook_batch_delete_messages`. The confirm line says "for example, mail, calendar and files"
and **never lists connectors as fact**, because the set is whatever the Human's account holds. Where a list is wanted, the
developer reads it from the Adapter's tool list, not from `connectors.json` (section 9).

### 7.3 Adapters where it cannot be controlled (Q7)

The switch must never claim a protection it cannot deliver. The card chooses one of three states from the Agent's resolved
`AdapterProfile`:

| Adapter | Control | The row shows |
| --- | --- | --- |
| `IsolateUserSettings` true and `AdapterCatalog.RunsClaude` (the stock `claude-agent-acp`) | Per Agent, by `_meta` env | The switch |
| Runs Claude, `IsolateUserSettings` false (a configured profile with no `_meta`) | Per profile only | No switch. Text: "Huddle cannot switch this Adapter's account connectors per Agent. Set `ENABLE_CLAUDEAI_MCP_SERVERS=false` in the Adapter's `EnvironmentOverrides`, or turn on isolation for it." |
| Anything else (`agency-acp` and other Adapters) | None needed in the spike's evidence | Text: "{Adapter} does not load your claude.ai connectors." **Unverified for every Adapter but the two tested; the text must not ship until each shipped preset has been checked.** |

In the first two rows a stored "on" is ignored with a warning in the log. The off-by-default promise is only claimed in the
first row; the other two say what Huddle cannot do.

Open for stage 3: whether `IsolateUserSettings: true` on an Adapter that is not `claude-agent-acp` should count as the first
row. The spike says the entry is ignored there, so the code uses `RunsClaude` as well, not isolation alone.

## 8. Mechanism

### 8.1 Building the session `_meta`

`DotAcpAgentHostFactory.BuildIsolationMeta(profile)` (`:590`) gains a second parameter, `bool connectors`. It is
`internal static`, called at `:144` and by `HelperSessionMeta.cs:18`.

- `connectors == false`: add `options.env = { "ENABLE_CLAUDEAI_MCP_SERVERS": "false" }` next to `settingSources` and `settings`.
- `connectors == true`: add nothing. The account connectors load as today.
- No `strict-mcp-config`, no `tools` change.

`HelperSessionMeta` passes `false` and keeps its own `env` and `strict` entries; its assignment of `sessionOptions["env"]`
(`HelperSessionMeta.cs:27`) replaces rather than merges, which is correct there and a trap here (section 8.2).

### 8.2 Merging `env`

Q2 showed the Adapter merges `options.env` over `process.env`, so the Adapter side needs no merge. Huddle's side: nothing else
writes `options.env` for Agents today; a future writer must add keys to the existing dictionary, not replace it. A test pins
that the Agent `_meta` carries exactly the one key (section 10).

### 8.3 When it takes effect (decision 4)

`_meta` is minted **once per host** (`DotAcpAgentHostFactory.cs:144`; the remarks above `BuildIsolationMeta` say so), so a
change must reach a new host. It does:

1. `SetConnectors` raises `PersonasChanged`.
2. `PersonaSupervisor.OnPersonasChanged` sees `persona != started` (`:384-386`) and calls `RestartHostAsync`.
3. The new host mints a new `_meta`. Its first session, or a resume in the new Adapter process, takes the new setting (section 3.4).

So the effect is on the Agent's next Turn after the restart, the same as changing the Model. The row says "Restarting {Name} to
apply this." and then the Helper line. A same-process resume ignores new `_meta` (`DotAcpPersonaHost.cs:186-193`); the restart
is what avoids that case, so the spec adds no wording about "next fresh session" to the Human. **Unverified:** what a
restart does to a Turn already running. The Model change takes the same path, so the behaviour is the existing one; stage 3 checks
it with a Turn running (section 11.2).

### 8.4 Stopping for approval (if OQ-1 is yes)

The handler has no Human-approval path: it decides from the request alone and returns. A prompt needs one.

- A connector tool is recognised by name: the Adapter prefixes MCP tools `mcp__<server>__<tool>`, and connector servers are
  `claude_ai_*` (observed in the spike logs). The check is a prefix test on the tool title or name. **Unverified** which field
  the Adapter fills with the name in a permission request; the spike's handler matched on title or input containing the name
  (`ConnectorProbeTests`), which suggests either. A free test with a captured request settles it.
- The handler awaits a Human answer through the existing Needs-you machinery (`NeedsYouTracker`, an Agent card `C8`). Which of
  the existing cards fits, or whether a new `ConnectorApprovalCard` is needed, is a stage-3 design question. **This spec does not
  size it.** It is the largest piece of work in the feature.
- Without an answer within the Turn's wait, the request is **refused**, never approved.
- No "always allow".

If OQ-1 is no, this section is dropped and the confirm copy changes as section 7.2 shows.

## 9. `requires` and `connectors.json` (decision 6)

Nothing changes in who gets access. `RoleSpec.Requires` (`RoleSpec.cs:14`) only feeds the **Needs setup** label through
`CardResolver.Resolve` (`CardResolver.cs:42-46`) and `FileConnectorStatus.IsConnected`. They name the connectors the Human has
set up in Huddle's own file. They are not the claude.ai connectors the switch controls: two lists with one word.

| Question | Answer |
| --- | --- |
| Does a role needing `email:read` switch the Human's setting on? | No. Hiring such a role does not write a row. |
| Does it ask the Human to? | Yes, once, on the hire card: a line "This role reads your mail. Turn on account connectors for {Name} after it is hired." No button, no prefilled switch. |
| Should **Needs setup** change? | It stays a label about Huddle's `connectors.json`. It does not read the switch. The label is not an access check and must not look like one. |
| Does a listed connector grant anything? | No. |

The hire-card line is a stage-3 detail and may be dropped without changing the design.

## 10. Teams and proposals (decision 7)

| Path | What it does with the setting | Why it cannot copy "on" |
| --- | --- | --- |
| Team duplication (`TeamDuplicator.CopyOne`, `TeamDuplicator.cs:143`) | Hires each member afresh from its role spec (`CardResolver.Resolve`), through `PersonaStore.Add`. The source's rows are never read. | `Add` writes no connectors row. A copied Agent is off. |
| `propose_agents` (`ProposeAgentsTool.cs`, `Candidate.cs`) | Cannot carry the flag. | No such field exists in `Candidate`, and none is added. |
| Retro and Pack updates (`RetroProposalService`, `PackUpdater`, `TeamMembership`) | Call `Update` with the Persona's own values. | `Update` neither reads nor writes the flag (section 6.3). |
| Library import or a copied Persona file | Frontmatter is the only thing carried. | The flag is not in frontmatter. |
| Built-in reset (`BuiltinAgentReset`) | Deletes the row. | Reset returns to off. |

One test per row pins this (section 12). Stage 3 re-greps for any other caller of `Personas.Add` or `Update` at build time and
adds a row for each.

## 11. What is visible (decision 8)

Two places, both written by `SetConnectors`:

1. **The card**: "On since {date}" (section 7.2). `enabled_at` is the source. "By whom" is always the Human, so the card does
   not say it.
2. **The Feed**: a new kind `AgentConnectorsChanged` (`agent-connectors-changed`), not a Needs-you item, source
   `PersonaStore`, key the Agent's name, payload `AgentConnectorsPayload(Name, On)`. Rendered "You turned on account
   connectors for Nova" and "You turned off account connectors for Nova". It is added to the table in
   [Huddle.Feed-Specifications.md](Huddle.Feed-Specifications.md) section 6.3 in the same change. The Feed row is what records a
   turn-off, which the stored row cannot (a deleted row has no date).

The Feed text names the Human and not an actor id, because the actor is always the Human by construction (section 6.2).

## 12. Out of scope, recorded (decision 9)

| Idea | Why later |
| --- | --- |
| Per-connector choice ("Milo gets calendar only") | Needs a list of connectors and upkeep, and the claude.ai connector set is the account's, not Huddle's. The store could take a list of server names in the value column with no schema change, which is why the value is a timestamp today and not a boolean. |
| A wider MCP restriction | Servers in `~/.claude.json` (section 3.3) and Work Dir servers (`.mcp.json`) stay as they are. The spike proved `strict-mcp-config` harms Agents; a narrower option would need its own spike. |
| A per-profile switch for other Adapters | The `EnvironmentOverrides` lever exists; a UI for it is a separate Settings change. |

## 13. Existing installs (decision 5)

On upgrade nothing is migrated, so every Agent is off. Each Agent loses its connectors the next time its host starts, which is
the first start after the update. A resumed session keeps its conversation and loses the tools (section 3.4). The
conversation history still holds the old connector names, but the model is not offered the tools, so it cannot call them.

| Step | What happens |
| --- | --- |
| Upgrade, app starts | Every Agent's host starts with the new `_meta`. No `claude_ai` tool is offered from the second Turn. |
| An Agent that relied on a connector | Its next Turn cannot use it. The model sees no such tool and says so, or carries on without. |
| The Human | Sees the notice (OQ-3) and the release note. Turns connectors on from the card for any Agent that needs them. |

**Release note text (draft):** "Agents no longer reach your claude.ai account connectors (mail, files, calendar) unless you turn
them on. Open an Agent's card and use Account connectors. Until you do, an Agent that used a connector will not find it. To
change every Agent on a profile at once, set `ENABLE_CLAUDEAI_MCP_SERVERS=false` in the Adapter's environment."

The notice (if OQ-3 is yes) is a `MudAlert` Info, dense, `role="status"`, on the Agents page, dismissible, and stored as a flag
in the same SQLite file. It shows only when at least one Persona exists at upgrade. Its wireframe name is `P1a`, assigned in
stage 3 to match the page's current numbering.

## 14. Test plan

Every task is test first, per the house rule. Tests are free unless marked. Test names follow `Method_Scenario_Expectation`.

### 14.1 Free tests

| # | Test | Pins |
| --- | --- | --- |
| T-1 | `PersonaConnectorsStoreTests`: no row reads off; `Set` writes `enabled_at`; blank deletes; keys are case-insensitive; a database without the table gets one | Section 6.1 |
| T-2 | `BuildIsolationMeta_ConnectorsOff_CarriesTheOneEnvKey` (and the isolation keys are unchanged) | Sections 8.1, 8.2 |
| T-3 | `BuildIsolationMeta_ConnectorsOn_CarriesNoEnv` | Section 8.1 |
| T-4 | `BuildIsolationMeta_ProfileNotIsolating_ReturnsNull_ForEitherSetting` | Section 3.6 |
| T-5 | `HelperSessionMetaTests` still pins `tools`, `env` and `strict-mcp-config`, and adds that the Agent's `_meta` has no `extraArgs` and no `tools` | Section 3.3 |
| T-6 | `rules.md` row for Agents, quoted by a test: a build of an Agent `_meta` without the env key fails | Rule |
| T-7 | `PersonaStore`: `SetConnectors` raises `PersonasChanged` once; `Update` leaves the row alone; `Remove` and rename delete it; `Add` writes none | Sections 6.3, 10 |
| T-8 | `PersonaSupervisor`: a `Persona` with `Connectors` flipped restarts its host (`NeedsRestart`) | Section 8.3 |
| T-9 | Team duplicate of a source with the row set: every new Agent is off | Section 10 |
| T-10 | `propose_agents` with a `connectors` property in the JSON: ignored, nothing stored | Section 10 |
| T-11 | A frontmatter key `connectors: true` in a Persona file: ignored | Section 6.2 |
| T-12 | `WorkDirPermissionHandler` given a `mcp__claude_ai_ms365__outlook_send_mail` request: today **Approve** (a characterization test that fails when OQ-1 is built, by design) | Section 4 |
| T-13 | If OQ-1 is yes: the handler returns a pending state for a connector call and **refuses** on timeout; never approves without an answer | Section 8.4 |
| T-14 | bUnit `AgentConnectorsRow`: off by default; Give access shows the confirm with the warning; Cancel leaves it off; Confirm calls `SetConnectors(name, true)`; turning off applies at once; the row is absent in Edit and New modes | Section 7.1 |
| T-15 | bUnit: the three Adapter states render the switch, the "cannot control" text and the "does not load" text | Section 7.3 |
| T-16 | Feed: `SetConnectors` publishes one `AgentConnectorsChanged`; the same value twice publishes none | Section 11 |
| T-17 | `Check-All`: the Feed spec table, `Index.md` and the manual test page match | Docs |

### 14.2 Paid tests (spend)

| # | Test | Cost |
| --- | --- | --- |
| P-1 | A new Agent's session log lists no `claude_ai_*` tool by Turn 3; `mcp__team__`, `git`, `dotnet` work | about 0.15 USD |
| P-2 | Switch on, wait for the restart, three Turns: the connectors are named in the log. Use one harmless read only (`get_me`); if OQ-1 is yes, the prompt appears and the Human answers | about 0.10 USD |
| P-3 | Switch off again: the connectors are gone from Turn 2 of the resumed session | about 0.10 USD |
| P-4 | An Agent asked, in a Room, to turn the switch on for itself: it has no tool for it and says so | about 0.05 USD |
| P-5 | A Turn running when the switch flips: what the Human sees (section 8.3, **unverified**) | about 0.05 USD |
| P-6 | #346's full run: no `claude_ai_*` tool in any Agent's log, added to ZERO-27 | in that run's budget |

Each checks the log at Turn 2 or later. None sends, changes or deletes anything. P-2 and P-3 need the owner's yes before the
first paid Turn, as the spike did.

## 15. Verification

The build is done when all of these hold:

1. `dotnet build Huddle.slnx` and `dotnet test Huddle.slnx --` pass, and `agents/scripts/Check-All.ps1` is green.
2. **Tool list, Turn 2 or later.** A new Agent's session log has no `claude_ai` string after at least three Turns; the same Agent
   with the switch on has them from Turn 2. The log is the proof (`~/.claude/projects/<lane>/`), not the model's reply. A one-Turn
   check passes vacuously and does not count.
3. Section 3.4's reverse resume (off then on) is observed once, and the result is written in `acp-session-config.md`.
4. The manual test section (section 16) passes, with its paid cases run.
5. A search of `src/Huddle.App` finds `PersonaConnectorsStore.Set` called from `PersonaStore.SetConnectors` only.

## 16. Stage 3: files

| File | Change |
| --- | --- |
| `src/Huddle.App/Data/PersonaConnectorsStore.cs` | New: the store (6.1) |
| `src/Huddle.App/ServiceCollectionExtensions.cs` | Register the store (next to `:390`) |
| `src/Huddle.App/Acp/Persona.cs` | Trailing `bool Connectors = false` (6.4) |
| `src/Huddle.App/Acp/PersonaStore.cs` | Read the row at `:304`; `SetConnectors`; delete on `Remove`/rename; take the store in the constructor (`:121`) |
| `src/Huddle.App/Acp/DotAcpAgentHostFactory.cs` | `BuildIsolationMeta(profile, connectors)` (8.1); pass `persona.Connectors` at `:144` |
| `src/Huddle.App/Helpers/HelperSessionMeta.cs` | Call with `false` |
| `src/Huddle.App/Acp/WorkDirPermissionHandler.cs` | Only if OQ-1 is yes (8.4) |
| `src/Huddle.App/Components/Shared/AgentProfileCard.razor` and a new `AgentConnectorsRow.razor` | Row `P6a` (7) |
| `src/Huddle.App/Components/Pages/Agents.razor` | Pass the value (`:235`); the notice if OQ-3 is yes |
| Feed: a new kind and renderer | Section 11 |
| `docs/Huddle.Feed-Specifications.md` | Add the kind to the section 6.3 table |
| `docs/engineering/rules.md` | Add the Agent rule beside the Helper row at `:59`; pinned by T-6 |
| `docs/engineering/acp-session-config.md` | Record section 3.4's reverse resume and P-5 |
| `docs/engineering/wireframes.md`, `UXDesignGuidelines.md` | Name `P6a` (and `P1a`); list `AgentConnectorsRow` in the component catalog |
| `docs/Huddle.UserGuide.md`, `docs/Huddle.EngineeringGuide.md`, `docs/engineering/known-limits.md` | The switch, the stored table, the limits (3.3, 3.6, 6.2) |
| `docs/engineering/manual-tests/` and `planning.md` | A new page `agent-connectors.md`, added to `planning.md` |
| `docs/Index.md`, `docs/adr/0080-...md` | Feature row; ADR status to Accepted when approved |
| Tests | T-1 to T-17; `HelperSessionMetaTests` extended |

The project plan, tracker and slicing come after approval, from this list.

## 17. Rationale

| Decision | Alternative it beat |
| --- | --- |
| A table next to the Work Mode table | Frontmatter (an Agent or a proposal writes it); `appsettings` (not per Agent; breaks "Human in the UI") |
| `enabled_at` as the stored value | A bare `1` (no date to show; no room for a connector list later) |
| The flag on `Persona` as a scalar | A separate lookup at host start (the restart check would not see a change) |
| `SetConnectors` as the only writer | A parameter on `Update` (three non-Human callers would carry it) |
| The row acts at once with a confirm | Saving with the card (a grant hidden inside an edit; no clear moment to record) |
| The variable, not `strict-mcp-config` | Strict (removes the Work Dir's own servers; Q3) |
| Restart on change, no "next session" wording | Telling the Human to restart (a step they can forget, leaving an Agent half-changed) |
| Drop the row on rename (OQ-4) | Carry it (a rename path becomes an access path) |
