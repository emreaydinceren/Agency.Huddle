# Known limits

What is absent on purpose. Read it before "fixing" something that looks missing
and before filing a bug: most entries below are decisions, two are known flakes,
and one is a known bug left in place deliberately. Do not treat any of them as
oversights or quietly add them.

Three entries have since been planned; each one says so and names its
[Roadmap](roadmap.md) item. Back to the hub: [AgencyTeam.md](../AgencyTeam.md).

- **No runaway-loop guard.** Two Agents that tag each other will spend tokens
  until you stop the app. Now planned — [Roadmap](roadmap.md) item 2, which also
  explains why `ReplyGate` on its own cannot be the whole fix.
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
- **Streaming replies.** The `messageDelta` Envelope is reserved and answered
  with `notSupported`. The shape exists so adding it later is not breaking.
  Now planned — [Roadmap](roadmap.md) item 3.
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
