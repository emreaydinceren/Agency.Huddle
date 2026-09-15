# Known limits

What is absent on purpose. Read it before "fixing" something that looks missing
and before filing a bug: most entries below are decisions, two are known flakes,
and one is a known bug left in place deliberately. Do not treat any of them as
oversights or quietly add them.

Two entries have since been planned and one has since been built; each one says
so, and names its [Roadmap](roadmap.md) item or its ADR. Back to the hub: [AgencyTeam.md](../AgencyTeam.md).

- **The runaway-loop guard is built, and here is what it does not cover.** A Room
  now has a Budget of agent-authored Messages between one Human Message and the
  next, and asks before granting another — [ADR-0006](../adr/0006-a-room-has-a-budget-for-agent-replies.md).
  Four gaps are deliberate:
  - **The counter is in memory and per Room.** A restart un-pauses every Room and
    shows a fresh Budget over a Transcript that already spent one. The same trade
    ADR-0004 accepted for the Catch-up buffer.
  - **An Agent can mint a fresh Budget by creating a Room.** `create_room` is
    uncapped, and each new Room starts with a full allowance. Only the per-Persona
    token Budget catches that, which is the strongest argument for it.
  - ~~**The token Budget is silent.**~~ **Closed 2026-09-13.** It is per Persona,
    so it still has no per-Room surface and no Continue prompt, but it no longer
    only writes to the log: a Persona that has spent it reads as Degraded on the
    Teammate tile, with the reason — exactly where this entry predicted the honest
    home was. See [ADR-0008](../adr/0008-a-turn-is-visible-stoppable-and-says-when-it-fails.md).
  - **A Message declined for Budget is not kept as Catch-up.** It is held for
    re-delivery instead. If you leave a Room paused and then type something rather
    than clicking Continue, the Agent's prompt will not carry the Message it was
    paused on; it is still in the Transcript and still on screen.
- **One session per Persona spans every Room it is in**, so context bleeds
  between Rooms. The `[Room: name (id: …)]` prefix on each prompt is a convention
  the model may ignore. A session per (Persona, Room) would multiply processes and
  cost.
- **One `AppToolServer` per Persona** — one loopback Kestrel each. Revisit past
  about four Personas.
- **`SystemPromptMode.Append` has never been evidenced against a live model.** If
  Personas do not take, suspect this first.
- **No test can prove a real model calls `mcp__team__get_help`.** The suite runs
  through `FakeAgentHostFactory`, which answers whatever it is told to, so it
  pins the *text* — that the prompt names the tool, and that the tool names the
  rest — and not the behaviour. Whether progressive discovery actually happens is
  a manual-checklist question, in the same class as whether a model finds any App
  Tool at all.
- **The inline guidance in the system prompt duplicates `get_help`.** Deliberate:
  the fixed block below the Persona still states the Reply Gate and the Mention
  rules, so an Agent that never calls a tool behaves correctly and `get_help` is
  an amplification rather than a precondition. The cost is two places to edit
  when a chat rule changes.
- **An Agent can invite into any Room whose id it holds**, including one it is
  not a Member of. `ChatService.InviteAsync` checks that the Room and the Agent
  exist, not who is asking. Acceptable while every Room contains the one Human.
- **Open-in-editor launches a process on the server, ungated in every
  environment.** Deliberate for a single-user PoC; unacceptable deployed.
- ~~**Streaming replies.**~~ **Built 2026-09-13**, and the reserved shape did
  exactly what it was reserved for: `messageDelta` needed no version change to
  activate. What it lands in is a **Draft** — in memory, never in the Transcript.
  Its own limits are below.
- **Authentication and authorisation.** None, by design, at this stage.
- **More than one Human.** The data model allows it; the seeding and UI do not.
  When it arrives, `/teammates` is already named correctly for it — add a filter
  toggle rather than a second page.
- **Renaming a Teammate is now an edit to one frontmatter field**, not a file
  rename — [Roadmap](roadmap.md) item 10 delivered that half. `PersonaStore.Update`
  moves the Model and Effort rows to the new Name. What remains is the
  no-cascade half, unchanged and deliberate: the old Agent, its Rooms and its
  Transcripts stay behind under the old Name, so a rename leaves a ghost in the
  Team Directory. That is [Roadmap](roadmap.md) item 1's to solve. The card also
  still exposes a rename only by editing the raw file, not as its own action.
- **Windows reserved device names are accepted as Names.** `CON`, `NUL`, `COM1`
  pass `NameRules` and become `CON.md`, which Windows will not create. Pre-dates
  the space change and is not guarded against.
- **Two Members whose Names differ only in case** resolve to whichever the
  longest-first ordering reaches first. `PersonaIndex` now prevents this between
  two *Personas* — `Jarvis` and `jarvis` are a case-insensitive collision and
  both files are rejected — but Members are not only Personas. Two raw pipe
  clients, or a pipe client and a Persona, can still register Names differing
  only in case, and nothing catches that.
- **The Model is per Persona.** Not per Room, not per Turn, and not switchable
  on a running session.
- **No live mid-session Effort switching.** Restart-on-change is the only
  mechanism, exactly like the Model and the system prompt it already applies to.
- **The Teammate card shows the stored Effort, not the session's live
  `currentValue`.** If the adapter clamps a requested level back to
  `"default"`, the card will not say so — it only ever reads back what
  `PersonaEffortStore` has on file.
- **The model catalog is probed once per app run and cached.** Upgrade the
  adapter and the picker can be stale until restart. Only the picker: the Model
  actually applied is re-resolved against the live session every time one starts.
  A deliberate deviation from `session-config-options.md`'s "caching across spawns is
  not fine", which protects selection rather than presentation.
- **That probe runs even with `Team:Acp:Enabled` false.** Opening a New/Edit
  teammate card, and changing its Model select, spawns a throwaway Adapter
  regardless of the money flag — see [Rules](rules.md). It spends nothing,
  because it never starts a Turn; the flag exists for the thing that does.
  Gating the probe on `Acp:Enabled` would leave the picker empty in the default
  configuration and defeat the feature, so the row is expected, and it is
  expected to be short-lived: it appears and disappears within seconds. An
  Adapter from this path that PERSISTS is not the probe — that is a real
  Adapter, spending the operator's money in a configuration they switched off.
- **`session/set_model` is unavailable.** The vendored adapter advertises only
  `configOptions`, never the unstable `models`/`SessionModelState`. If a future
  adapter adds it, `ModelConfigOptions` is the single place to teach.
- **A failed probe and a cancelled one look alike in the log.** Both return an
  empty catalog; the warning names the cause, but an authentication failure and a
  missing adapter both present to the user as "this agent advertises no models".
  Narrowed 2026-09-13: a *Persona that failed to start* for either reason now says
  which, on its tile. The model **picker** is still the ambiguous surface.
- **What a Draft does not survive.** It is in memory, per process, so a restart
  loses every Turn in flight and the Room shows only what had been posted. Its
  text is capped at 256 KB; past that it stops growing rather than truncating what
  is already there. Both are deliberate — it is a Singleton holding model output
  for the life of the process, and an Agent killed without a clean disconnect
  never sends the terminator that would clear it.
- **Stopping an Agent stops it in every Room.** One ACP session spans every Room
  its Agent is in, so there is nothing narrower to stop. `StopTurn` carries the
  Room the Human asked from as a label, not as a selector — the same
  one-session-per-Persona limit that makes context bleed between Rooms.
- **Tool activity is never written to the Transcript.** It belongs to the Draft
  and goes when the Draft does, so scrollback shows what an Agent said and not
  what it did.
- **Quota, a network failure, and credentials expiring mid-session are
  indistinguishable.** All three arrive as one exception type carrying
  Adapter-authored wording, which nothing here pattern-matches, because it is not
  ours and it will change. They are reported by *persistence* instead: one failed
  Turn is noise, three in a row escalates the reason to say so. The Agent stays
  Degraded rather than Offline, because the session and the pipe may both be fine
  while the provider refuses.
- **A Degraded Agent is never restarted automatically.** The Human clicks Restart
  on the Teammate card. Automatic recovery would need a policy nobody has asked
  for, and a restart clears what that Teammate remembers.
- **What a Theme cannot do.** Roadmap item 6's four limits were retired on 2026-09-14
  when theming moved to MudBlazor — see
  [ADR-0010](../adr/0010-a-theme-is-a-mudblazor-theme.md). A Theme now carries **both**
  palettes, so light/dark is a separate preference rather than a second Theme, and
  changing it no longer reloads the page. Four different things are absent now:
  - **There is no per-Token customisation at all.** `appearance.json` holds a Theme id
    and a light/dark preference, and nothing else. The override map, its allowlist and
    the inline `<style>` are gone. Changing one colour means editing `ThemeCatalog` in
    C# and rebuilding, or waiting for item 7's Theme import.
  - **A bad Theme id is logged, not shown.** `AppearanceStore` warns, names the unknown
    id, leaves the file untouched and falls back to the built-in Theme — but the
    Appearance tab no longer reports it, because the section that did belonged to the
    override layer. This is the one place the repo's "reported, never swallowed" habit
    is now weaker than it was; the log is the only surface.
  - **Under System, a flash of the wrong Theme is possible on first paint.** The server
    cannot know the device's preference at render time, so
    `MudThemeProvider.GetSystemDarkModeAsync()` reads it over JavaScript after the first
    render. This is the cost ADR-0009 avoided and ADR-0010 accepted.
  - **The choice is per installation, not per browser.** Unchanged, and still
    deliberate: it lives in `{DataDir}/appearance.json`, so a second browser, a private
    window and a phone on the same install all see the same Theme. One Human per
    installation is the assumption it rests on.
- **Threads, reactions, edits, deletes, attachments, search, notifications.**
- **Known flake, pre-existing:** `PersonaSupervisorTests.Shutdown_DisposesEveryHost`
  fails roughly one run in four, always on a slow run — its 10-second token races
  `WaitUntilAsync`. It is a timing bug in the test, not in `PersonaSupervisor`.
- **Second known flake, pre-existing:** a timing race in event ordering over the
  fake transport in `Huddle.Acp.Tests`, roughly one run in five, passing on rerun.
  **Three tests are now known to show it**, which is the argument that the race is in
  the transport rather than in any one test:
  `PromptAsync_StreamsChunksInOrder_ThenTurnCompleted` and
  `PromptAsync_ThoughtAndToolCallEvents_ArePublished` (both quarantined by name in CI),
  and `DotAcpConcurrentHostTests.TwoHosts_ConcurrentPrompts_EachSessionOnlySeesItsOwnAgentsUpdates`,
  seen on 2026-09-15 in CI run 607 and **not quarantined**. All three fail the same way:
  a `TurnCompleted` arrives where a `MessageChunk` or a tool-call notification was
  expected. Not diagnosed.
- **Known bug, pre-existing:** `Data/SqliteTeamDirectory.cs` is not
  `IDisposable`, and SQLite connection pooling keeps a handle on `team.db`, so
  tests leave about 83 temp directories behind per run. `TempDataDir.Dispose`
  swallows the resulting `IOException`, which is why it is invisible.
