# Known limits

What is absent on purpose. Read it before "fixing" something that looks missing
and before filing a bug: most entries below are decisions, three are known flakes,
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
  - **A Room now says why it stayed quiet, and here is what that still does not
  cover.** A Message naming no Teammate, and a **Continue** that wakes nobody, are
  both explained in the Room view now — [ADR-0012](../adr/0012-a-room-says-why-it-stayed-quiet.md).
  Three gaps are deliberate:
  - **The note does not survive a reload.** It describes what one delivery meant, not
    durable state, so F5 loses it. Re-deriving on load would use membership as it is
    *now* and would re-explain old Messages under rules that were not in force when
    they were sent. The next Message re-evaluates, and `PersonaRunner` now writes the
    context-only line to the log, which it did not before.
  - **A Room whose Teammates are all Offline or Degraded still explains nothing.** An
    unreachable Agent is not a recipient, so such a Room resolves to `NoRecipients` and
    renders no note — which keeps the note from telling the Human to name a Teammate
    that cannot answer, but leaves that silence unexplained. Deliberate: shipping a
    wrong instruction was not an acceptable way to avoid it.
  - **The note reports a label, not an outcome.** The Reply Gate is client-side and is
    permission rather than obligation, so a pipe client may reply to a Message the Room
    called context-only. This is why the copy instructs ("name one to ask for a reply")
    and never predicts.
- **A reply refused for Budget still vanishes without trace.** The Draft types out a
  full answer and then goes, because `PersonaRunner`'s terminator reaches
  `Drafts.Complete` on every path a Turn can end. Nothing on screen says the reply was
  refused. Left out of ADR-0012 deliberately: there is no Message to attach it to, and
  retaining the Draft would mean splitting `Drafts.Complete`, whose whole contract is
  that both the terminator and a successful post call it.
- **A Message declined for Budget is not kept as Catch-up.** It is held for
    re-delivery instead. If you leave a Room paused and then type something rather
    than clicking Continue, the Agent's prompt will not carry the Message it was
    paused on; it is still in the Transcript and still on screen.
- **Following is built, and here is what it does not cover.** An Agent can ask to be
  woken by every Message in a Room with `follow_room` — [ADR-0005](../adr/0005-agent-topologies-are-emergent.md),
  [Roadmap](roadmap.md) item 8. Four gaps are deliberate:
  - **A follow is in memory and per Agent, and does not survive a restart.** `RoomFollows`
    is a Singleton, and `PersonaRunner` clears its own Agent's follows after each
    handshake, so a forgotten `unfollow_room` self-heals. The cost is the other
    direction: a coordinator restarted mid-pipeline silently stops being woken, and
    nothing says so.
  - **A follower is woken by every Message in that Room**, including exchanges between
    two other Agents it has no part in, and **each wake is a billed Turn**. The Room's
    Budget is what bounds that, which is why ADR-0005 made item 2 a hard prerequisite
    rather than a companion improvement.
  - **The Human cannot see who is following what.** There is no surface for it anywhere —
    not the Room view, not the Teammate card. A Room that is quietly spending its Budget
    on a follower reads exactly like one that is not.
  - **A coordinator that forgets to call `follow_room` stalls silently**, which is the
    residual risk ADR-0005 names and accepts. No test can catch it: the suite answers
    through `FakeAgentHostFactory`, so it is a manual-checklist question in the same class
    as whether a real model finds any App Tool at all.
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
- **Renaming a Teammate now carries the whole Teammate with it**, and what
  remains absent is narrower than it was. [Roadmap](roadmap.md) item 10 made a
  rename an edit to one frontmatter field; item 1's cascade landed 2026-09-15
  ([ADR-0011](../adr/0011-a-rename-moves-the-teammate-not-its-history.md)). The
  Agent's row is renamed **in place, keeping its id**, so the Rooms, the
  memberships and the Transcripts follow; the Model and Effort rows move; the
  Work Dir moves; and Rooms still carrying an auto-derived name are re-derived
  while Rooms the Human named by hand are left alone. Four things it still does
  **not** do, each on purpose:
  - **History is not rewritten.** Messages already posted keep the Name they were
    posted under, because `ChatMessage` stores a denormalised `SenderName`
    alongside `SenderId`. Rewriting would need a rewrite path on the append-only
    `FileChatStore` and would edit a historical record in place.
  - **The session still restarts and still loses its conversation memory.** A
    system prompt is fixed at `session/new`; nothing here changes that.
  - **A rename whose new Name is already held by another Agent does nothing.**
    `RenameUser` returns false, the cascade stops before touching a Room or the
    Work Dir, and a warning is logged. Nothing says so on screen.
  - **The Work Dir move can lose a race and give up.** The old agent process may
    still hold it as its `cwd`; the move retries a handful of times and then logs
    a warning rather than failing the rename.
- **Removing a Persona still does not cascade**, and that is unchanged and
  deliberate — see the entry below on `PersonaStore`. A removal means the
  Teammate is gone, so its Rooms and Transcripts are chat facts that outlive it;
  a rename means it is still here under another Name, which is why the same
  no-cascade rule read as correct for one and as a bug for the other.
- **A Room keeps auto-naming itself until somebody renames it, and that is
  detected by comparison rather than by a flag.** `ChatService.InviteAsync`
  re-derives a Room's name from its Agent Members only while the current name
  still equals what `RoomNaming.Derive` would have produced; a Human-chosen name
  therefore survives every later Invitation. There is no `name_is_custom` column
  and deliberately so — the schema is created with `CREATE TABLE IF NOT EXISTS`,
  so an existing `team.db` would never gain one (see [Traps](traps.md)). The
  accepted cost is one invisible edge: rename a Room to precisely the name
  auto-naming would have chosen and it stays auto-named, so the next Invitation
  re-derives over it. Nothing observable distinguishes the two states.
- **Two live 1:1 Rooms with the same Teammate are reachable, since 2026-09-21.**
  Archiving a Room excludes it from `FindRoomWithExactMembersAsync`, so starting a
  chat with that Teammate creates a fresh Room rather than resurrecting the
  archived one — a deliberate choice, recorded in
  [ADR-0018](../adr/0018-a-room-can-be-archived-or-deleted.md). Unarchive the first
  one afterwards and two non-archived Rooms now hold exactly `{Human, Teammate}`,
  both named by `RoomNaming.Derive` and therefore identical in the sidebar.
  [ADR-0003](../adr/0003-mention-gated-replies-and-membership-defined-direct-rooms.md)'s
  one-two-Member-Room-per-Agent invariant no longer holds in that case. Nothing
  breaks; the lookup gained an `ORDER BY r.created, r.id` so which Room wins is at
  least deterministic (the oldest) rather than whatever SQLite returns first.
- **Renaming a Room does not make two Rooms distinguishable on its own.** Rooms
  nobody has renamed are still named after their Members and still carry no
  timestamp, no last-message preview and no other mark, which is the other half
  of [product observation 6](product-observations.md) and is not built.
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
- ~~**A Persona's identity does not reach `agency-acp`.**~~ **Closed 2026-09-18.**
  `AgencyDotNet.Acp` 0.1.195 read only `_meta.model` and dropped the composed prompt
  silently, so every Persona there ran on the harness's baseline text and two
  Personas on that Adapter were indistinguishable. Fixed in **0.1.197**, which reads
  `_meta.systemPrompt` into `QueryContext.IdentityPrompt`. Verified end to end
  against a live model, not taken on report: a Persona's name and nonce codeword
  came back in the reply, and two concurrent sessions with different identities did
  not bleed. Recorded as D-1 in [Live findings](../Huddle.Adapters-LiveFindings.md).
- **A small local Model may not *inhabit* a Persona, even though the Persona now
  reaches it.** This is what is left of D-1 above, and it is a property of the Model
  rather than a defect anywhere. Observed 2026-09-18 on `gemma-4-e2b`: given two
  distinct identities, both replies opened *"I am Gemma 4, a Large Language Model
  developed by Google DeepMind"*; one then adopted its Persona correctly, the other
  volunteered its own codeword while still answering as Gemma. The identity is
  demonstrably present — the codewords prove it is being read — a 2B-class Model
  simply does not consistently hold a voice. Untested whether a larger local Model
  does. Expect local Models to suit narrow Personas long before they suit ones with
  a strong character, which is the honest limit [Roadmap](roadmap.md) item 12 named
  before there was evidence for it.
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
- **A waiting Proposal does not survive a restart, and the proposer is not told.**
  `ProposalStore` holds at most one Proposal per Room in memory, like a Draft, so a
  restart loses it and its card. The Agent that proposed is still waiting for an
  outcome Message that never comes; the Human has to ask again. Archiving or
  deleting the Room drops it on purpose. Deliberate for V1: persisting it would
  mean a table and a recovery path for a card the Human can recreate by asking.
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
  [ADR-0010](../adr/0010-a-theme-is-a-mudblazor-theme.md) — and the list was rewritten
  again on 2026-09-21 when a Theme became a single palette
  ([ADR-0017](../adr/0017-a-theme-is-a-palette-not-a-pair.md)). Four different things are
  absent now:
  - **The application does not follow the device's light/dark setting.** There is no
    System option and no light/dark control at all: a Theme *is* a light one or a dark
    one, and the one you pick is the one you get, on every device, until you pick
    another. Doing this honestly needs a **pair** of authored Themes and a rule for
    resolving between them, the way VS Code's *Preferred Light* / *Preferred Dark* and
    "Sync with OS" work. Six of the nineteen Themes already have a real counterpart, so
    the door is open; it is not built. This replaced a worse limit — until 2026-09-21 the
    preference could contradict the Theme, and selecting `Solarized Dark` with `Light`
    showed Huddle's palette under Solarized Dark's name.
  - **There is no per-Token customisation at all.** `appearance.json` holds a Theme id
    and nothing else. The override map, its allowlist and the inline `<style>` are gone.
    Changing one colour means editing `ThemeCatalog` in C# and rebuilding, or waiting for
    item 7's Theme import.
  - **A bad Theme id is logged, not shown.** `AppearanceStore` warns, names the unknown
    id, leaves the file untouched and falls back to the default Theme — but the
    Appearance tab no longer reports it, because the section that did belonged to the
    override layer. This is the one place the repo's "reported, never swallowed" habit
    is now weaker than it was; the log is the only surface.
  - **The choice is per installation, not per browser.** Unchanged, and still
    deliberate: it lives in `{DataDir}/appearance.json`, so a second browser, a private
    window and a phone on the same install all see the same Theme. One Human per
    installation is the assumption it rests on.
- **What an imported Theme cannot do.** The catalog gained seventeen of Visual Studio
  Code's bundled Themes on 2026-09-16 — see
  [ADR-0016](../adr/0016-vs-codes-bundled-themes-are-converted-once-not-imported.md).
  Three things about them are worth knowing before reporting a bug:
  - **A single-mode Theme has no counterpart in the picker.** A VS Code Theme is authored
    for light *or* dark, so it appears once, under one heading. There is no light Monokai
    to select: there is no such thing upstream, and deriving one would invent colours
    nobody designed. Until 2026-09-21 asking for one showed Huddle's light palette instead,
    which was worse than not offering it
    ([ADR-0017](../adr/0017-a-theme-is-a-palette-not-a-pair.md)).
  - **The High Contrast pair is not high contrast.** VS Code draws HC borders throughout
    from `contrastBorder`, which MudBlazor's palette has no equivalent for. `Dark High
    Contrast` and `Light High Contrast` land here as strong-contrast ordinary Themes. They
    are also the most derived of the set: `hc_light.json` carries five colour keys, so
    eighteen of its twenty slots come from VS Code's own registry defaults. Both sit under
    the picker's own **High contrast** heading (`ThemeGroup`), not under Light or Dark.
  - **Three Themes miss the contrast floor, on purpose.** Themes are imported unmodified,
    so `light-plus` and `quiet-light` (4.40:1) and `solarized-light` (3.98:1) fall just
    under 4.5:1 for `TextSecondary` on `Surface`. `ThemeCatalogTests` records each with its
    measured ratio and asserts it *still* falls short, so a fixed Theme forces its entry to
    be deleted rather than leaving a stale excuse behind.
- **A Teammate chooses its own Avatar, and here is what that does not cover.**
  Initials, a short label or an uploaded image, over a background colour —
  [ADR-0019](../adr/0019-an-avatar-is-chosen-and-is-not-part-of-the-persona.md). Six
  gaps are deliberate:
  - **An Avatar does not travel with a copied `.md` file.** It is in
    `avatars.json`, keyed by Name, so copying a Persona to another machine brings its
    text and not its face. Neither its Model nor its Effort travels either, for the
    same reason, and nobody has asked for those.
  - **Images are stored as uploaded — no resizing, no dimension cap.** Only the byte
    count is bounded, at 512 000. Checking pixels means decoding, which means an image
    decoder, a new package and a decompression-bomb surface; the browser scales the
    image down with CSS instead. A 500 KB image stays 500 KB on disk.
  - **An emoji outside the Basic Multilingual Plane is stored escaped.**
    `System.Text.Json` escapes surrogate pairs whatever encoder it is given, so `🦊`
    is written `🦊` — verified at the byte level, not assumed. It round-trips
    exactly and a Human may type the literal character in; only the file's readability
    suffers. See [Traps](traps.md).
  - **An orphaned entry is never pruned.** Delete a Persona's `.md` while the app is
    *not* running and nothing observes the deletion, so its entry and image stay. The
    same tolerance `appearance.json` gives a `theme` id naming nothing: keep the file,
    ignore what cannot be resolved. A deletion made while the app *is* running is
    caught by the watcher and does cascade.
  - **`RoomList` shows no avatar.** A Room is named after *all* its Agent Members and
    `Room` carries no member list, so no single Teammate is recoverable from a sidebar
    row without a per-row membership query. A wrong face on a Room is worse than none.
  - **After a rename, Messages already posted keep the old Name** — history is never
    rewritten, and `ChatMessage.SenderName` is denormalised — so their avatars fall back
    to the old monogram while the Teammate's current Name carries the chosen one. The
    Transcript records what was said at the time, faces included.
- **Threads, reactions, message edits, message deletes, attachments, search,
  notifications.** *Room* archive and delete arrived 2026-09-21
  ([ADR-0018](../adr/0018-a-room-can-be-archived-or-deleted.md)); nothing here
  edits or removes an individual Message, and a Transcript is still never
  rewritten in place.
- **Known flake, pre-existing:** `PersonaSupervisorTests.Shutdown_DisposesEveryHost`
  fails roughly one run in four, always on a slow run — its 10-second token races
  `WaitUntilAsync`. It is a timing bug in the test, not in `PersonaSupervisor`.
- **Second known flake, pre-existing — DIAGNOSED 2026-09-16, and it is not only a
  flake.** A timing race in event ordering over the fake transport in
  `Huddle.Acp.Tests`, roughly one run in five, passing on rerun. **Three tests were
  known to show it**, which was the argument that the race is in the transport rather
  than in any one test: `PromptAsync_StreamsChunksInOrder_ThenTurnCompleted` and
  `PromptAsync_ThoughtAndToolCallEvents_ArePublished` (both quarantined by name in CI),
  and `DotAcpConcurrentHostTests.TwoHosts_ConcurrentPrompts_EachSessionOnlySeesItsOwnAgentsUpdates`,
  seen on 2026-09-15 in CI run 607 and **not quarantined**. All three fail the same way:
  a `TurnCompleted` arrives where a `MessageChunk` or a tool-call notification was
  expected.

  **The mechanism, caught with instrumented reproduction while building the Tier 3
  conformance suite.** The peer writes its `session/update` notifications and the final
  `session/prompt` response to the wire **strictly in order**. The client does not
  *dispatch* them in that order: StreamJsonRpc completes a response through a different
  path from the one that invokes notification handlers, and nothing sequences a
  response's continuation against notification handlers already in flight. One captured
  failing run showed chunk 1 dispatched on thread 6, the **response** dispatched on
  thread 7 fractions of a millisecond later, and chunks 2 and 3 only entering thread 6
  *after* that. `PersonaRunner.RunEventReaderAsync` then reads its channel in arrival
  order — correctly — but arrival order is no longer wire order, so `TurnCompleted`
  clears `activeTurn` and `AppendAndPublishDeltaAsync`'s null-turn guard (right for the
  "stream ended mid-turn" case it was written for) **silently drops the remaining
  chunks**. Every component behaves correctly on its own; the defect is the unstated
  assumption that wire order survives dispatch.

  **It is a content-loss bug, not just a test flake.** The same `activeTurn.Text`
  accumulator that feeds the live Draft is what gets posted, so a Turn affected by this
  loses text from the Message that lands in the Transcript, not merely from the
  in-flight render. It needs a warm thread pool to surface, which is why it reads as a
  flake: cold and quiet it does not race. A real adapter's process-pipe latency widens
  the gaps and makes it rarer than it is in-proc, but not impossible.

  **Not fixed**, because the fix belongs in `DotAcpAgentSession`/`DotAcpClientAdapter`
  (sequencing a session's notification publishes ahead of its response's
  `TurnCompleted`) and both `PersonaRunner` and that pipeline were explicitly out of
  scope for the work that found it. `tests/Huddle.Tests/Conformance/ChunkedReplyFirstOrderer.cs`
  quarantines it for one Tier 3 test and carries the full evidence in its remarks.
- **Third known flake, pre-existing — DIAGNOSED 2026-09-17.** A full
  `dotnet test Huddle.slnx --` occasionally crashes the test host *after* every
  assertion has passed, with an `ObjectDisposedException` raised from
  `PersonaStore.OnWatcherError`. The cause is an ordering one, not a timing one:
  that handler calls `this.logger.LogWarning(...)` **before** it takes `watchGate`
  and checks `this.disposed` (`PersonaStore.cs:586-600`). During host teardown the
  logging provider can already be disposed when a `FileSystemWatcher` raises a
  late `Error` event, and because the handler runs on a watcher callback thread the
  throw is unhandled and takes the process with it. It reads as a flake because it
  needs a dropped-event overflow to land inside the teardown window. **Fixed
  2026-09-22** (Skills D8): the log moved inside the existing `disposed` guard, the
  same order `SkillStore.OnWatcherError` uses, and
  `PersonaStoreTests.OnWatcherError_AfterDispose_DoesNotLog` fails if it moves back.
  A test-host crash naming `OnWatcherError` is now a regression, not this flake.
- **Known bug, pre-existing:** `Data/SqliteTeamDirectory.cs` is not
  `IDisposable`, and SQLite connection pooling keeps a handle on `team.db`, so
  tests leave about 83 temp directories behind per run. `TempDataDir.Dispose`
  swallows the resulting `IOException`, which is why it is invisible.
