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
- **What a Theme cannot do.** Roadmap item 6 shipped on 2026-09-13 and four things
  are absent on purpose:
  - **A Theme has no per-mode pair.** One selection means one `color-scheme`, so
    choosing **Dark** is dark on a light OS too. Following the device means choosing
    **System**, which layers no Theme at all and leaves the built-in pair to resolve
    each Token's `light-dark()` against the OS. A Theme that carried both halves
    would need two palettes in one file, which is the drift
    [ADR-0009](../adr/0009-a-theme-is-a-stylesheet-layered-over-the-tokens.md)
    rejected outright.
  - **Changing the Theme costs a full page load.** `<head>` belongs to the server and
    Blazor's render tree cannot reach it. Swapping the `href` over `IJSRuntime` would
    put back the JavaScript this design has none of, and add a second writer of
    `<head>` that can disagree with the file.
  - **An override value is allowlisted, so an exotic but perfectly valid CSS value
    may be refused.** `color-mix(...)` survives; anything needing `;`, `{`, `<`, `>`,
    `&`, `@`, `:` or a backslash does not, and `url(` is refused outright. The CSS
    reaches the document as a `MarkupString`, so the allowlist is the only thing
    between a hand-edited file and a blanked page — see [Rules](rules.md). A refusal
    is reported on the Appearance tab and the Theme's own value stands.
  - **The choice is per installation, not per browser.** It lives in
    `{DataDir}/appearance.json`, so a second browser, a private window and a phone on
    the same install all see the same Theme. That is the deliberate consequence of
    having no JavaScript: `localStorage` would be per browser and would need a
    pre-paint script to avoid a flash. One Human per installation is the assumption
    it rests on; it is the one to revisit if that ever changes.
- **Threads, reactions, edits, deletes, attachments, search, notifications.**
- **Known flake, pre-existing:** `PersonaSupervisorTests.Shutdown_DisposesEveryHost`
  fails roughly one run in four, always on a slow run — its 10-second token races
  `WaitUntilAsync`. It is a timing bug in the test, not in `PersonaSupervisor`.
- **Second known flake, pre-existing:** a test in `Huddle.Acp.Tests` fails roughly
  one run in five and passes on rerun —
  `PromptAsync_StreamsChunksInOrder_ThenTurnCompleted` is the one seen by name, a
  timing race in event ordering over the fake transport. Seen from two separate
  sessions; not diagnosed.
- **Known bug, pre-existing:** `Data/SqliteTeamDirectory.cs` is not
  `IDisposable`, and SQLite connection pooling keeps a handle on `team.db`, so
  tests leave about 83 temp directories behind per run. `TempDataDir.Dispose`
  swallows the resulting `IOException`, which is why it is invisible.
