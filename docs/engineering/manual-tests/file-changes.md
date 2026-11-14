# File Changes, Watched Folders and Memory

Prove Watched Folders, `watch_folder`/`unwatch_folder`, File Changes and Memory end to end in a
real browser: that an Agent's next Turn lists what changed in its Watched Folders since it was
last in that Room, that its own edits are not listed back to it in the Room it made them in, that
what it chooses to remember survives a restart and a new Room, and — the one open question this
page exists to answer — whether isolating a Persona's session from the Human's own Claude Code
settings actually works. Two tests (FM-1, FM-2) have a free substitute: the mock adapter echoes the
prompt it was given, so the File Changes block can be read straight off its reply. The rest depend
on a real Adapter reporting `Edit`/`Delete`/`Move` tool calls correctly and on real Claude Code
settings resolution, which is why they spend money and none of them is in the automated suite.

**12 tests** · 2 free, 10 paid 💰 · about 3.2 hours · **none of them has been run yet.** This page
documents what to run, not a result — see [Known limits](../known-limits.md) for what that leaves
unverified today.

Read [the manual test script](../manual-tests.md) first — the cost guard, the Model and Effort
convention, and the rules for concluding a result — then [Common procedures](common.md), which
defines the terminals, states, procedures and oracles this page names. Both are assumed below.

Background: [Huddle.FileChanges-Specifications.md](../../Huddle.FileChanges-Specifications.md)
(§6.8 the runner, §6.9 the two tools, §6.13 the Prompts, §6.15 Memory, §8 what a Turn can miss,
§10 the FM table), [Huddle.RoomSessions-Specifications.md](../../Huddle.RoomSessions-Specifications.md)
Appendix B (V-1, V-2, V-4 — this page runs them as FC-V1, FC-V2, FC-V4),
[ADR-0023](../../adr/0023-an-agent-learns-of-file-changes-on-its-next-turn.md)
(File Changes and Memory) and the isolation remarks on `AdapterProfile.IsolateUserSettings` in
`src/Huddle.App/Acp/AdapterProfile.cs`.

## Setup

Run [`P-BUILD`](common.md#p-build). FM-1 and FM-2 launch with [`P-LAUNCH-UAT`](common.md#p-launch-uat)
(free; Ada and Bo on the mock adapter). Every other test launches with `P-LAUNCH-PAID`. This area
adds:

1. **`Team:FileChanges:Enabled` defaults to `true` and needs no configuration**
   (`FileChangesOptions.cs:13`). The stock install's only Adapter profile has `ReadsFiles: true`
   and `IsolateUserSettings: true` (`AdapterCatalog.cs:70-72`); do not set `Team__Acp__Adapters__*`
   for any paid test except FC-V4, because a configured `Acp:Adapters` list replaces that profile
   and `IsolateUserSettings` goes back to `false`. An Adapter with `ReadsFiles: false` offers
   neither tool and no Watched Folder list, and every test below would be *Inconclusive* rather
   than a defect.
2. **The First-Turn rule.** The first Turn an Agent takes in a Room lists nothing: with no
   baseline for that Room the report is empty (`FileChangeTracker.cs:38-40,66-69`). **Every test
   below therefore takes a baseline Turn first** — a throwaway Message in each Room the test uses —
   before it changes anything. A Room of three or more Members answers only when the Agent is
   @Mentioned, so every Message in a shared Room below carries `@<Name>`.
3. **Define the Keeper.** The *Keeper* is an Agent you create for these tests with
   [`P-NEW-PERSONA`](common.md#p-new-persona): Name `Keeper`, Title `Keeper`, one sentence in
   **Persona body** ("You keep one short note per client in your Work Dir."), **Model** Sonnet
   (the owner's rule: never Haiku for a test Agent),
   **Effort** `low`, **Teams** blank. Its Work Dir is `<data>\Agents\Keeper\work\` and its memory
   folder is `<data>\Agents\Keeper\work\memory\` (`AgentPaths.cs`,
   `DotAcpAgentHostFactory.cs:131-136`). Several tests also use a second Agent, `Aide`, made the
   same way and never @Mentioned unless a step says so.
4. **No `TraceWire`.** It prints the tool server's bearer token into the log and stays forbidden
   (see [Standing conventions](common.md#standing-conventions)). The File Changes block is
   observed two ways: on the mock adapter, whose reply is the whole prompt; on a real Adapter, by
   asking the Agent in the Room "Which files were reported to you as changed since your last Turn
   in this Room? Quote their paths exactly, or say none." Its tool calls are in
   [`O-TOOLCALLS`](common.md#o-toolcalls). A model can paraphrase, so a quoted path that matches is
   a Pass and a vague answer is *Inconclusive*, never a Pass.
5. **Markers for FC-V1, FM-6 and FC-V2 only.** These need a distinctive, harmless marker in your
   own Claude Code configuration — an output style and a line in `~/.claude/CLAUDE.md` you would
   notice in a reply, and `"autoMemoryEnabled": true` in `~/.claude/settings.json`. Set them
   **after** FM-0 to FM-5 and FM-7 and remove them again once FC-V1/FC-V2 conclude; leaving them
   in place would make every other paid area's Adapter start under your own markers too.

## Tests

### FM-0 — An Agent's own memory edit in one Room is news in its other Rooms

**Paid 💰** · about 20 min · four short Turns

*Proves the headline case FC §10's F0 test pins in the automated suite, live: File Changes rides
on the next Turn, is scoped per Room, and never repeats in the Room the edit was made in. Also the
live check on §6.8's warning that a wrong attribution must only ever cost an extra line.*

**Before you start**

`P-LAUNCH-PAID` with the Keeper and Aide created. Room A is the Keeper's own direct Room. Room B is
**New room** with the Keeper and Aide (three Members, so every Message to the Keeper starts
`@Keeper`).

**Steps**

1. Baseline both Rooms: in Room A send "hello"; in Room B send "@Keeper hello".
2. In Room A ask: "remember that we are launching on the 30th."
3. In Room B send the question from Setup step 4: "@Keeper Which files were reported to you as
   changed since your last Turn in this Room? Quote their paths exactly, or say none."
4. In Room A send the same question without the `@Keeper`.

**Expect**

- Step 2: [`O-TOOLCALLS`](common.md#o-toolcalls) for `Keeper` has a write or edit line, and a new
  `.md` file exists in `<data>\Agents\Keeper\work\memory\`. If not, the Agent chose not to
  remember: *Inconclusive*, repeat step 2 with "please save that to your memory".
- Step 3: the reply quotes that file's path, ending `\Agents\Keeper\work\memory\<file>.md`, and
  the line carries the suffix `(by you, in Room '…')` (`turn.fileByYouSuffix`).
- Step 4: the reply does not list that file — the Agent's own edit is never reported back into the
  Room it was made in.

---

### FM-1 — A file edited outside Huddle is listed on the next Turn

**Free** · about 10 min · two short Turns on the mock adapter

*Proves the basic Watched Folder case: an edit made by a human, in an editor, outside any Turn,
still reaches the Agent's next Turn's prompt. The mock adapter's reply is the whole prompt, so the
block is read straight off it.*

**Before you start**

`P-LAUNCH-UAT`. Open Bo's Room.

**Steps**

1. Baseline: send "hello" and wait for the reply.
2. In an editor create `C:\seeds\huddle\uat\data\Agents\Bo\work\notes.txt` containing `one`.
3. Send "hello again".
4. Change the file's content to `two` and save.
5. Send "hello once more".

**Expect**

- Step 1's reply carries no File Changes block.
- Step 3's reply contains the header text "Since your last Turn in this Room, these files
  changed." followed by `added C:\seeds\huddle\uat\data\Agents\Bo\work\notes.txt`
  (`PromptCatalog.cs:338` `turn.fileChangesHeader`; `turn.fileAdded`).
- Step 5's reply contains `changed C:\seeds\huddle\uat\data\Agents\Bo\work\notes.txt`.

---

### FM-2 — The same edit, made while Huddle was closed

**Free** · about 10 min · one short Turn on the mock adapter

*Proves the snapshot survives a restart — the one case a `FileSystemWatcher` could not have
covered, and the reason ADR-0023 rejected one.*

**Before you start**

FM-1 has just run, so `notes.txt` exists in Bo's Work Dir and Bo has a baseline from FM-1 step 5
(or take one Turn in Bo's Room now).

**Steps**

1. `P-STOP`.
2. Change `notes.txt` to `three` and save.
3. Launch `P-LAUNCH-UAT` again **without** the seeder line (it wipes the data folder); run only the
   `dotnet run --project src/Huddle.App` command.
4. In Bo's Room send "hello".

**Expect**

The reply contains `changed C:\seeds\huddle\uat\data\Agents\Bo\work\notes.txt`, on the very first
Turn after the restart.

---

### FM-3 — A note the Agent writes itself is not listed back on its next Turn

**Paid 💰** · about 10 min · three short Turns

*Proves own-edit attribution inside one Room: `TouchedPaths` off the Agent's own `Edit`/`Write`
tool call keeps its own write out of its own next prompt in that Room.*

**Steps**

1. In the Keeper's Room send "hello" (baseline).
2. Ask the Keeper to write a note called `client-a.txt` in its Work Dir.
3. In the same Room send the question from Setup step 4.

**Expect**

- Step 2: [`O-TOOLCALLS`](common.md#o-toolcalls) shows the write, and
  `<data>\Agents\Keeper\work\client-a.txt` exists. If not, the Agent did not write it:
  *Inconclusive*.
- Step 3: the reply does not list `client-a.txt` — the Agent's own write is not reported back to
  it in this Room.

---

### FM-4 — `watch_folder` on a shared folder, then a restart, then an outside edit

**Paid 💰** · about 25 min · four short Turns plus a restart

*Proves the tool subscription survives a restart, per FC D-10 — the same argument that keeps a
`watch_folder` call from behaving like `follow_room`, which resets. A subscribed folder is baselined
only at the end of a later Turn, so the test takes that Turn explicitly.*

**Before you start**

Create the folder `<data>\shared-notes\` with a file `price.txt` containing `10`.

**Steps**

1. In the Keeper's Room send "hello" (Room baseline).
2. Ask: "watch the folder `shared-notes` with `watch_folder`." Read the reply.
3. Send any other Message, for example "thanks". This Turn baselines the new folder.
4. `P-STOP`, then `P-LAUNCH-PAID` again.
5. Change `price.txt` to `12` and save.
6. In the Keeper's Room send the question from Setup step 4.

**Expect**

- Step 2: [`O-TOOLCALLS`](common.md#o-toolcalls) has a `watch_folder` line. The tool's own text is
  `Now watching 'shared-notes' (…\shared-notes). …` (`FileChangeTracker.cs:181`); the reply may
  paraphrase it. Asking for `watch_folder` on the same folder a second time returns
  `Already watching 'shared-notes' (…). Nothing to do.` (`:173-191`).
- Step 6: the reply quotes `…\shared-notes\price.txt` as changed — the subscription was not
  forgotten across the restart.
- If step 3 is skipped the step 6 Turn only baselines the folder and lists nothing; that is the
  code (`FileChangeTracker.cs:84,106-110`), not a defect. Its tool text, "From your next Turn",
  overstates this by one Turn (`:181`): note it for the spec owner, do not fail the test on it.

---

### FM-5 — Renaming a watched Agent does not spuriously list anything

**Paid 💰** · about 20 min · three short Turns

*Proves FC §6.12: `PersonaRenameCascade` moves the renamed Persona's `FileStateStore` entry and
rewrites other Agents' `subscribed` entries (`PersonaRenameCascade.cs:154`), so another Agent
watching it by Name does not see a burst of false adds or deletes from the rename alone.*

**Before you start**

Put a file `plan.txt` in `<data>\Agents\Aide\work\`. Agent A is the Keeper, Agent B is Aide.

**Steps**

1. In the Keeper's Room send "hello" (baseline).
2. Ask the Keeper to `watch_folder` `Aide` (an Agent Name resolves to that Agent's Work Dir).
3. Send any other Message. This Turn baselines Aide's folder.
4. On Aide's card on the Agents page choose **Edit**, change **Name** to `Aide2`, and **Save**.
5. In the Keeper's Room send the question from Setup step 4.

**Expect**

The reply lists nothing for `plan.txt` and no `deleted` line for the old `Aide` path or `added`
line for the new `Aide2` path. If Aide's Work Dir genuinely changed content in the meantime, only
that content shows, not the rename itself.

---

### FM-6 — Isolation and Memory across a restart, with a real preference

**Paid 💰** · about 25 min · two short Turns plus a restart

*This is FC Appendix A's FC-V and RS Appendix B's V-2, run together: the live check that Memory
actually works, and that it does not depend on Claude Code's own auto-memory doing the same job
invisibly. It is also the manual test [ADR-0023](../../adr/0023-an-agent-learns-of-file-changes-on-its-next-turn.md)
names as the one that found the original problem.*

**Before you start**

Setup step 5 done: `"autoMemoryEnabled": true` set in `~/.claude/settings.json`, so this test is
checking that Huddle's own `autoMemoryEnabled: false` in the session `_meta` overrides it, not
that the Human never had auto-memory on to begin with. Use the Keeper.

**Steps**

1. In one Room, tell the Keeper "I prefer C# for any code. Please remember that." A standing rule
   belongs in an AGENTS.md, which an Agent does not edit, so the Keeper may propose the line for
   you to add and offer to keep it in its own notes meanwhile: reply "Yes, keep it in your own
   notes for now."
2. Restart the Keeper from the Agents page (not the whole app).
3. In a different Room (the Keeper and Aide Room B from FM-0, `@Keeper` in every Message), ask it
   for code and then "What do you remember about my coding preferences, and from which file?"
4. Check `<data>\Agents\Keeper\work\memory\` for a file recording the preference.
5. Check `~\.claude\projects\<a folder named after the Work Dir path>\memory\` on the machine
   running the Adapter for anything new since step 1.

**Expect**

- `<data>\Agents\Keeper\work\memory\` holds a file recording the C# preference (step 4), and the
  step 3 answer names a file in that folder.
- The code in step 3's reply is in C#.
- **Nothing new appears** under the Human's `~\.claude\projects\...\memory\` for the Keeper's Work
  Dir (step 5) — this is the clause that proves Claude Code's own auto-memory is out of the way, and
  the one most likely to fail given the known settings-replacement risk (see Known limits).

---

### FM-7 — Room-only instructions stay out of Memory

**Paid 💰** · about 10 min · one short Turn

*Proves the Prompt's steering ("do not copy the conversation itself... remember only what should
hold in every Room") is followed, not just stated — nothing enforces it in code (FC "What memory
is not").*

**Steps**

1. In the Keeper's Room, tell it "in this chat, answer only in French."
2. Look in `<data>\Agents\Keeper\work\memory\`.

**Expect**

No new file for it — a Room-scoped instruction is not written to Memory, even though nothing in
code would have stopped the Agent from doing so.

---

### FM-8 — An Agent describes its own memory honestly

**Paid 💰** · about 10 min · one short Turn

*Proves the Prompt's corrective sentence — "Do not claim to remember what is not in your memory or
in this conversation" — actually changes what the model says, after a live test found an Agent
denying it carries context between Rooms while doing so.*

**Before you start**

FM-6 has run, so the Keeper's `memory\` folder holds the C# preference file. Without it this test
is *Inconclusive*.

**Steps**

1. In a Room that did not receive the C# preference (create a fresh **New room** with the Keeper
   and Aide), send "@Keeper what do you remember, and from where?"

**Expect**

- It names its actual memory files rather than giving a generic "I have no memory" answer.
- It does not claim to have no memory of other Rooms while demonstrably carrying context from one
  (the known limit [Known limits](../known-limits.md) already documents this session-level bleed
  separately — this test is about the Agent's own *description* of its memory, not the bleed
  itself).

---

### FC-V1 — Does isolation actually drop the Human's own settings and `CLAUDE.md`?

**Paid 💰** · about 15 min · one short Turn

*RS Appendix B V-1: establishes, rather than assumes, that `settingSources: ["project", "local"]`
keeps a Persona's session off the Human's own output style and `~/.claude/CLAUDE.md`.*

**Before you start**

Setup step 5: a distinctive output style and a distinctive line in `~/.claude/CLAUDE.md` set on the
machine running the Adapter — something a reply would visibly carry if it leaked (for example "end
every reply with the word PINEAPPLE"). `P-LAUNCH-PAID` with the stock Adapter profile (no
`Team__Acp__Adapters__*` variable), so `IsolateUserSettings` is `true`
(`AdapterCatalog.cs:72`).

**Steps**

1. Ask the Keeper an ordinary question, for example "What is a good name for a spreadsheet of
   client notes?"
2. Read the reply for any trace of the output style or the `CLAUDE.md` line.

**Expect and record**

Record whether either leaked, with the reply text as evidence, in your test result and tell the
repo owner. No leak is a Pass. If either leaked, run **FC-V4** below and **stop**: report to the
repo owner before changing the mechanism (the open verification noted on
`AdapterProfile.IsolateUserSettings`).

---

### FC-V2 — Does `autoMemoryEnabled: false` actually stop auto-memory?

**Paid 💰** · about 15 min · shares FM-6's Turns

*RS Appendix B V-2. This is FM-6 above, read as the isolation mechanism's own pass/fail rather
than as a Memory feature test — run them together and record both conclusions from the one run.*

**Steps and Expect**

Run FM-6 in full. Record separately: did anything new appear under `~\.claude\projects\...\memory\`
for the Keeper's Work Dir with `"autoMemoryEnabled": true` set in the Human's own
`~/.claude/settings.json`? Record the outcome, with the Model and Effort used, in your test result
and tell the repo owner. If it failed, run **FC-V4** and stop.

---

### FC-V4 — Does a per-profile `CLAUDE_CONFIG_DIR` move the login? (only if FC-V1 or FC-V2 fails)

**Paid 💰** · about 20 min · one short Turn

*RS Appendix B V-4 — the fallback mechanism, run only when the recommended one fails, so its own
cost is conditional on FC-V1/FC-V2's outcome. `EnvironmentOverrides` belongs to an Adapter
profile, not to a Persona, so the directory is shared by every Persona on that profile.*

**Before you start**

`P-STOP`, then set a full replacement profile; a configured `Acp:Adapters` list replaces the stock
one, so every field the stock profile has is repeated (`AdapterCatalog.cs:60-74`) and
`IsolateUserSettings` is set explicitly (it defaults to `false`):

```powershell
$env:Team__Acp__Adapters__0__Id                = 'claude'
$env:Team__Acp__Adapters__0__Command           = 'node'
$env:Team__Acp__Adapters__0__UsesToolNamePrefix = 'true'
$env:Team__Acp__Adapters__0__ReadsFiles        = 'true'
$env:Team__Acp__Adapters__0__IsolateUserSettings = 'true'
$env:Team__Acp__Adapters__0__SessionPerRoom    = 'true'
$env:Team__Acp__Adapters__0__PromptBlocks      = 'true'
$env:Team__Acp__Adapters__0__Commands__0       = 'compact'
$env:Team__Acp__Adapters__0__EnvironmentOverrides__CLAUDE_CONFIG_DIR = '<data>\claude-config'
```

Create the empty folder `<data>\claude-config` first. Launch with `P-LAUNCH-PAID`'s command.

**Steps**

1. Open the Keeper's card on the Agents page and read its status.
2. Send the Keeper "hello" and read the reply.

**Expect and record**

The login moved with the config directory if the Keeper goes Offline or the reply is an
authentication error (the empty folder holds no login); it did not move if the Keeper answers
normally. Record which, in your test result, and report the finding to the repo owner. Do not
change `IsolateUserSettings`'s mechanism on the strength of this test alone. Afterwards
`P-STOP`, clear every `Team__Acp__Adapters__*` variable (`Get-ChildItem Env:Team__*`) and delete
`<data>\claude-config`.

---

Back to [the manual test script](../manual-tests.md).
