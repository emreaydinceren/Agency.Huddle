# File Changes, Watched Folders and Memory

Prove Watched Folders, `watch_folder`/`unwatch_folder`, File Changes and Memory end to end in a
real browser: that an Agent's next Turn lists what changed in its Watched Folders since it was
last in that Room, that its own edits are not listed back to it in the Room it made them in, that
what it chooses to remember survives a restart and a new Room, and — the one open question this
page exists to answer — whether isolating a Persona's session from the Human's own Claude Code
settings actually works. None of this has a mock-adapter substitute: it depends on a real Adapter
reporting `Edit`/`Delete`/`Move` tool calls correctly and on real Claude Code settings resolution,
which is why every test here spends money and none of it is in the automated suite.

**11 tests** · 0 free, 11 paid 💰 · **none of them has been run yet.** This page documents what to
run, not a result — see [Known limits](../known-limits.md) for what that leaves unverified today.

Read [the manual test script](../manual-tests.md) first — the cost guard, the Model and Effort
convention, and the rules for concluding a result — then [Common procedures](common.md), which
defines the terminals, states, procedures and oracles this page names. Both are assumed below.

Background: [Huddle.FileChanges-Specifications.md](../../Huddle.FileChanges-Specifications.md)
(§6.8 the runner, §6.9 the two tools, §6.13 the Prompts, §6.15 Memory, §8 what a Turn can miss,
§10 the FM table), [Huddle.RoomSessions-Specifications.md](../../Huddle.RoomSessions-Specifications.md)
Appendix B (V-1, V-2, V-4), [ADR-0023](../../adr/0023-an-agent-learns-of-file-changes-on-its-next-turn.md)
(File Changes and Memory) and the isolation remarks on `AdapterProfile.IsolateUserSettings` in
`src/Huddle.App/Acp/AdapterProfile.cs`.

## Setup

Run [`P-BUILD`](common.md#p-build), then `P-LAUNCH-PAID` — every test here spends money, so there
is no free lane to start from. This area adds:

1. **`Team:FileChanges:Enabled` defaults to `true` and needs no configuration for FM-0 through
   FM-8.** Confirm the resolved Adapter's `ReadsFiles` is `true` (the default) before starting —
   an Adapter with `ReadsFiles: false` offers neither tool and no Watched Folder list, and every
   test below would be *Inconclusive* rather than a defect.
2. **V-1 and V-2 need a distinctive, harmless marker in your own Claude Code configuration** —
   an output style and a line in `~/.claude/settings.json` and `~/.claude/CLAUDE.md` you would
   notice in a reply, and `"autoMemoryEnabled": true` in `~/.claude/settings.json`. Set these
   before `P-LAUNCH-PAID` and remove them again once V-1/V-2 conclude; leaving them in place
   would make every other paid area's Adapter start under your own markers too.
3. **`Team__Acp__TraceWire`** is required for FM-0, FM-1, FM-2 and FM-4 — the pass condition is
   the `TraceWire` log showing the file-changes block, not only the reply's content. Follow the
   [§0.2](../manual-tests.md#02-the-cost-guard) warning: a throwaway terminal, cleared
   afterwards, unset before any other test.
4. **A Keeper-shaped Teammate** is assumed for FM-1 through FM-4: create one directly
   (`P-NEW-PERSONA`, free), Haiku/low, with a Work Dir a test can reach in an editor
   (`App_Data\work\<Name>\`).

## Tests

### FM-0 — An Agent's own memory edit in one Room is news in its other Rooms

**Paid** · about 15 min · two short Turns

*Proves the headline case FC §10's F0 test pins in the automated suite, live: File Changes rides
on the next Turn, is scoped per Room, and never repeats in the Room the edit was made in. Also the
live check on §6.8's warning that a wrong attribution must only ever cost an extra line.*

**Steps**

1. Put an Agent in two Rooms with the Human (invite it into a second Room, or create one).
2. In the first Room, ask it to remember a decision — for example, "remember that we are
   launching on the 30th."
3. In the second Room, ask it anything unrelated.
4. Back in the first Room, ask it something else.

**Expect**

- The second Room's prompt lists `changed …\work\<Name>\memory\<file>.md` — confirm in the
  `TraceWire` log, not only from the reply's content.
- The first Room's next Turn (step 4) lists nothing for that file — the Agent's own edit is never
  reported back into the Room it was made in.

---

### FM-1 — A file edited outside Huddle is listed on the Keeper's next Turn

**Paid** · about 15 min · one short Turn

*Proves the basic Watched Folder case: an edit made by a human, in an editor, outside any Turn,
still reaches the Agent's next Turn's prompt.*

**Steps**

1. With the Keeper Teammate running, open a file in its Work Dir (`App_Data\work\<Name>\`) in a
   text editor and change it.
2. In its Room, ask it anything.

**Expect**

- The reply shows the Agent knows that file changed — it mentions the file or what changed in it
  without being told directly.
- The `TraceWire` log shows the file-changes block naming that file as `changed`.

---

### FM-2 — The same edit, made while Huddle was closed

**Paid** · about 15 min · one short Turn

*Proves the snapshot survives a restart — the one case a `FileSystemWatcher` could not have
covered, and the reason ADR-0023 rejected one.*

**Steps**

1. `P-STOP`.
2. Edit a file in the Keeper's Work Dir.
3. `P-LAUNCH-PAID` again.
4. In its Room, ask it anything.

**Expect**

Same as FM-1: the reply and the `TraceWire` log both show the file as changed, on the very first
Turn after the restart.

---

### FM-3 — A note the Agent writes itself is not listed back on its next Turn

**Paid** · about 10 min · two short Turns

*Proves own-edit attribution inside one Room: `TouchedPaths` off the Agent's own `Edit`/`Write`
tool call keeps its own write out of its own next prompt in that Room.*

**Steps**

1. Ask the Keeper to write a note to a file in its Work Dir.
2. In the same Room, ask it something else.

**Expect**

The second Turn's prompt has no file-changes line for the note the Agent itself just wrote — check
`TraceWire`.

---

### FM-4 — `watch_folder` on a shared folder, then a restart, then an outside edit

**Paid** · about 20 min · two short Turns

*Proves the tool subscription survives a restart, per FC D-10 — the same argument that keeps a
`watch_folder` call from behaving like `follow_room`, which resets.*

**Steps**

1. Ask an Agent to `watch_folder` a shared folder — a folder Name inside `App_Data`, or another
   Teammate's Name.
2. Confirm the reply text: "already watching" on a repeat call, an ordinary confirmation on the
   first.
3. `P-STOP`, then `P-LAUNCH-PAID` again.
4. Change a file in that folder from outside Huddle.
5. Ask the Agent anything in its Room.

**Expect**

- The change is listed on the Turn after the restart (step 5), confirmed in `TraceWire` — the
  subscription was not forgotten across the restart.

---

### FM-5 — Renaming a watched Teammate does not spuriously list anything

**Paid** · about 15 min · one short Turn

*Proves FC §6.12: `PersonaRenameCascade` moves the renamed Persona's `FileStateStore` entry, so
another Agent watching it by Name does not see a burst of false adds or deletes from the rename
alone.*

**Steps**

1. Have Agent A `watch_folder` Agent B by Name (or use a `watches` frontmatter entry naming B).
2. Rename Agent B from its Teammate card.
3. In Agent A's Room, ask it anything.

**Expect**

Nothing spurious is listed for the renamed folder — no burst of `deleted` lines for the old path
and `added` lines for the new one. If Agent B's Work Dir genuinely changed content in the
meantime, only that content shows, not the rename itself.

---

### FM-6 — Isolation and Memory across a restart, with a real preference

**Paid** · about 25 min · two short Turns plus a restart

*This is FC Appendix A's FC-V and RS Appendix B's V-2, run together: the live check that Memory
actually works, and that it does not depend on Claude Code's own auto-memory doing the same job
invisibly. It is also the manual test [ADR-0023](../../adr/0023-an-agent-learns-of-file-changes-on-its-next-turn.md)
names as the one that found the original problem.*

**Before you start**

`"autoMemoryEnabled": true` set in `~/.claude/settings.json` (Setup step 2), so this test is
checking that Huddle's own `autoMemoryEnabled: false` in the session `_meta` overrides it, not
that the Human never had auto-memory on to begin with.

**Steps**

1. In one Room, tell Alpha (or any Teammate) "I prefer C# for any code."
2. Restart Alpha from the Teammates page (not the whole app).
3. In a different Room, ask it for code.
4. Check `App_Data\work\Alpha\memory\` for a file recording the preference (`TraceWire` also shows
   the index in the new session's system prompt).
5. Check `~\.claude\projects\<derived from the Work Dir>\memory\` on the machine running the
   Adapter for anything new since step 1.

**Expect**

- `App_Data\work\Alpha\memory\` holds a file recording the C# preference (step 4), and the new
  session's system prompt lists it in `TraceWire`.
- The code in step 3's reply is in C#.
- **Nothing new appears** under the Human's `~\.claude\projects\...\memory\` for Alpha's Work Dir
  (step 5) — this is the clause that proves Claude Code's own auto-memory is out of the way, and
  the one most likely to fail given the known settings-replacement risk (see Known limits).

---

### FM-7 — Room-only instructions stay out of Memory

**Paid** · about 10 min · one short Turn

*Proves the Prompt's steering ("do not copy the conversation itself... remember only what should
hold in every Room") is followed, not just stated — nothing enforces it in code (FC "What memory
is not").*

**Steps**

1. In one Room, tell an Agent "in this chat, answer only in French."
2. Look in its `memory\` folder.

**Expect**

No new file for it — a Room-scoped instruction is not written to Memory, even though nothing in
code would have stopped the Agent from doing so.

---

### FM-8 — An Agent describes its own memory honestly

**Paid** · about 10 min · one short Turn

*Proves the Prompt's corrective sentence — "Do not claim to remember what is not in your memory or
in this conversation" — actually changes what the model says, after a live test found an Agent
denying it carries context between Rooms while doing so.*

**Steps**

1. In a second Room (one that did not receive the original instruction), ask the Agent "what do
   you remember, and from where?"

**Expect**

- It names its actual memory files rather than giving a generic "I have no memory" answer.
- It does not claim to have no memory of other Rooms while demonstrably carrying context from one
  (the known limit [Known limits](../known-limits.md) already documents this session-level bleed
  separately — this test is about the Agent's own *description* of its memory, not the bleed
  itself).

---

### V-1 — Does isolation actually drop the Human's own settings and `CLAUDE.md`?

**Paid** · about 15 min · one short Turn

*RS Appendix B V-1: establishes, rather than assumes, that `settingSources: ["project", "local"]`
keeps a Persona's session off the Human's own output style and `~/.claude/CLAUDE.md`.*

**Before you start**

A distinctive output style and a distinctive line in `~/.claude/CLAUDE.md` set on the machine
running the Adapter (Setup step 2) — something a reply would visibly carry if it leaked.

**Steps**

1. With `Team:Acp:Enabled=true` and `TraceWire` on, ask any Persona on an Adapter with
   `IsolateUserSettings: true` an ordinary question.
2. Read the reply for any trace of the output style or the `CLAUDE.md` line.

**Expect and record**

Record whether either leaked, with the reply text as evidence, in
the Adapters design (Huddle.Adapters-Specifications.md) (FC Appendix A's
acceptance criterion for FC-V). If either leaked, run **V-4** below and **stop**: report to the
repo owner before changing the mechanism, per Task 14.3.m.

---

### V-2 — Does `autoMemoryEnabled: false` actually stop auto-memory?

**Paid** · about 15 min · shares FM-6's Turns

*RS Appendix B V-2. This is FM-6 above, read as the isolation mechanism's own pass/fail rather
than as a Memory feature test — run them together and record both conclusions from the one run.*

**Steps and Expect**

Run FM-6 in full. Record separately: did anything new appear under `~\.claude\projects\...\memory\`
for the Persona's Work Dir with `"autoMemoryEnabled": true` set in the Human's own
`~/.claude/settings.json`? Record the outcome, with the Model and Effort used, in
the Adapters design (Huddle.Adapters-Specifications.md). If it failed, run **V-4**
and stop, per Task 14.3.m.

---

### V-4 — Does a per-Persona `CLAUDE_CONFIG_DIR` move the login? (only if V-1 or V-2 fails)

**Paid** · about 20 min · one short Turn

*RS Appendix B V-4 — the fallback mechanism, run only when the recommended one fails, so its own
cost is conditional on V-1/V-2's outcome.*

**Steps**

1. Set `CLAUDE_CONFIG_DIR` to a per-Persona directory under `DataDir` on the Adapter's process
   environment (`Acp:Adapters:*:EnvironmentOverrides`), pointing away from the Human's own
   `~/.claude`.
2. Start a session and confirm whether the Adapter still finds the Human's own login, or asks to
   authenticate again.

**Expect and record**

Record whether the login moved with the config directory, in
the Adapters design (Huddle.Adapters-Specifications.md). Do not change
`IsolateUserSettings`'s mechanism on the strength of this test alone — report the finding to the
repo owner, per Task 14.3.m's acceptance criterion.

---

Back to [the manual test script](../manual-tests.md).
