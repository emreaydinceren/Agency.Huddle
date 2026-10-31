# Instructions and Memory

Prove, in a real browser against a real Adapter, that the standing Instructions the Human writes reach an Agent's session, and that the `memory` tool and the Memory protocol hold together on a screen: that a Team's `Instructions.md` is in every member's next session; that a new topic file is written with a description and listed in the next index; that two Rooms writing one file cannot overwrite each other; that only a Team's manager writes its Team Memory, through the tool and through the Agent's own `Write`; that a Human edit in the Library is seen at the next read; that an Agent deletes only when the Human asks; that a restart recalls a memory; and that an Adapter with no file tools still gets the tool and the Memory block. The automated suite proves the parts; this page proves they hold against a model that really chooses what to save. **Written 2026-10-30; not yet run by a person.** INSTRMEM-07 is the spike S-1 of the design and **has not been run**: it needs a live, paid Claude Code session started from a data folder inside this repository.

**9 tests** · 1 free, 8 paid 💰 · about 2.6 hours.

Read [the manual test script](../manual-tests.md) first, then [Common procedures](common.md), which defines the terminals, procedures and oracles this page names. Both are assumed below. The design is [the Instructions and Memory spec](../../Huddle.InstructionsAndMemory-Specifications.md) (§9 lists these nine tests as IM-1 to IM-9) and [ADR-0070](../../adr/0070-an-agent-reads-the-instructions-the-human-writes.md) and [ADR-0071](../../adr/0071-memory-is-written-through-the-memory-tool-and-team-memory-by-its-manager.md).

| Test | Spec id | Cost |
| --- | --- | --- |
| INSTRMEM-01 | IM-1 | Paid 💰 |
| INSTRMEM-02 | IM-2 | Paid 💰 |
| INSTRMEM-03 | IM-3 | Paid 💰 |
| INSTRMEM-04 | IM-4 | Paid 💰 |
| INSTRMEM-05 | IM-5 | Paid 💰 |
| INSTRMEM-06 | IM-6 | Paid 💰 |
| INSTRMEM-07 | IM-7 | Paid 💰, the spike, not run |
| INSTRMEM-08 | IM-8 | Paid 💰 |
| INSTRMEM-09 | IM-9 | Free |

## Setup

Run [`P-BUILD`](common.md#p-build) then the lane named in each test. This area adds:

1. Use **scratch** Agents on the stock Claude Adapter, Haiku / `low` as [§0.3](../manual-tests.md#03-the-model-and-effort-convention) says. Make `Nova` (no Team) and, for INSTRMEM-04, a Team `Dev` with `Ada` as its manager (the Team's `_team.md` names her) and `Leo` as a member, with [`P-NEW-PERSONA`](common.md#p-new-persona).
2. `<data>` is the data folder of the lane. An Agent's memory is `<data>\Agents\<Name>\work\memory\`; a Team's is `<data>\Teams\<Team>\memory\`; a Project's is `<data>\Teams\<Team>\<Project>\memory\`.
3. Ask an Agent for its tool calls with [`O-TOOLCALLS`](common.md#o-toolcalls); read the files with [`O-FILE`](common.md#o-file). The `memory` tool's results are plain text, so the tool-call log shows the command and the file, and the Agent's reply shows what it understood.
4. An Instructions edit reaches a **new** session. Restart an Agent from its card (or `P-STOP` and relaunch) before the Turn that must see an edit, except where a test says a running session learns of it as a File Change.

## Tests

### INSTRMEM-01 — A Team Instructions file reaches every member on its next session

**Paid** 💰 · about 15 min

*Proves a Team's `Instructions.md` is in the system prompt of a member's next session, under a `Team <name>` heading, and that a running session hears of a later edit as a File Change. Spec IM-1, U-1.*

**Before you start**

- `P-LAUNCH-PAID`. Team `Dev` with member `Leo` (Haiku / `low`). Create `<data>\Teams\Dev\Instructions.md` containing the line `Always end your reply with the word PINEAPPLE.` Restart `Leo` from its card.

**Steps**

1. In a Room with `Leo`, ask: `What do your standing instructions say? Quote them.`
2. Edit `<data>\Teams\Dev\Instructions.md` in the Library to read `Always end your reply with the word MANGO.` Do not restart `Leo`.
3. Ask `Leo` again in the same Room: `Did anything change in your instructions? Reply briefly.`

**Pass if — all of these**

- Step 1's reply quotes the PINEAPPLE line and says it is the Team's.
- Step 3's reply knows of the edit (the File Changes block at the start of the Turn lists `Instructions.md` as changed) and says the rule now names MANGO. It may not yet follow it, because the system prompt is a snapshot.

**Fail if — any of these**

- Step 1's reply knows nothing of the file -> the Instructions part is missing from the prompt, or the file name did not match.
- `Leo` writes to the file at any step -> the write guard is not applied.

### INSTRMEM-02 — A new topic file is written with a description and appears in the next session index

**Paid** 💰 · about 15 min

*Proves an Agent starts a new topic with `write` and a `description`, and that the next session's Memory index lists it as `file - description`. Spec IM-2, U-3.*

**Before you start**

- `P-LAUNCH-PAID`. Scratch Agent `Nova`, Haiku / `low`, with an empty `memory` folder.

**Steps**

1. In a Room with `Nova` say: `Please remember this: my dentist is Dr Lee, appointments are on Tuesdays, and my insurance is Acme Dental.`
2. Read `<data>\Agents\Nova\work\memory\` and open the new file.
3. Restart `Nova` from its card, and ask: `What is in your memory index? List the file names and descriptions.`

**Pass if — all of these**

- Step 2 finds a `.md` file whose first lines are `---`, `description: ...`, `---`, and whose description says when to read the file and names Dr Lee, the dentist or the insurance. [`O-TOOLCALLS`](common.md#o-toolcalls) shows a `memory` call.
- Step 3's reply lists `<file>.md - <that description>`.

**Fail if — any of these**

- The file has no frontmatter, or the description is only the file name restated -> the tool did not compose it, or `Nova` wrote the file with its own `Write`.

### INSTRMEM-03 — Two Rooms write one memory file and the second write is refused with the current text

**Paid** 💰 · about 20 min

*Proves the version token: two Rooms of one Agent read a file at the same version, the first write moves it, and the second is refused with the current text so the Agent can merge. Spec IM-3, U-4.*

**Before you start**

- `P-LAUNCH-PAID`. `Nova` with a memory file `school-contacts.md` (created by INSTRMEM-02's method or by hand with a `description`), and two Rooms with `Nova`.

**Steps**

1. In Room A say: `Read school-contacts.md with the memory tool and wait; do not write yet.` In Room B say the same.
2. In Room A say: `Now add the line "- Ms Patel: Year 4" to it with write, using the version you read.` Wait for the reply.
3. In Room B say: `Now add the line "- Mr Khan: Year 5" to it with write, using the version you read.`

**Pass if — all of these**

- Step 3's tool result is the conflict text ("changed since you read it") followed by the current file and a new version, and `Nova` reads again or merges, then writes. The final file holds **both** lines.

**Fail if — any of these**

- The final file lacks the Room A line -> a stale write overwrote it.
- Step 3 succeeded at once -> the version was not required.

### INSTRMEM-04 — A member write to Team Memory is refused through the tool and through Write while its read is allowed

**Paid** 💰 · about 20 min

*Proves only the Team's manager writes its Team Memory: the tool refuses a member naming the manager, the permission handler refuses the member's own `Write`, and the member's read works. Spec IM-4, U-5.*

**Before you start**

- `P-LAUNCH-PAID`. Team `Dev`, manager `Ada`, member `Leo`. A file `<data>\Teams\Dev\memory\release-process.md` exists. Restart both Agents.

**Steps**

1. Ask `Leo`: `Read Dev's release-process.md with the memory tool and tell me what it says.`
2. Ask `Leo`: `Save "Releases ship on Mondays" as a new Dev Team Memory file called schedule.md with the memory tool.`
3. Ask `Leo`: `Use your Write tool to create <data>\Teams\Dev\memory\schedule.md containing the same line.`
4. Ask `Ada` to do step 2.
5. Read the application's console ([`O-LOG`](common.md#o-log)) after step 3.

**Pass if — all of these**

- Step 1 works. Step 2's result says only Dev's manager, Ada, writes it. Step 3 is refused (the console shows a "Refused a tool call" line naming Ada) and no `schedule.md` exists afterwards.
- Step 4 creates `schedule.md` with a `description`.

**Fail if — any of these**

- Step 2 or 3 creates the file -> the manager-only rule is not enforced where that door is.
- Step 1 is refused -> the guard is refusing reads.

### INSTRMEM-05 — A Human edit in the Library is seen at the next read and an old version is refused

**Paid** 💰 · about 15 min

*Proves the Human's side is unchanged and the tool cannot overwrite an edit it has not read. Spec IM-5, U-9.*

**Before you start**

- `P-LAUNCH-PAID`. `Nova` with `school-contacts.md`.

**Steps**

1. Ask `Nova` to read `school-contacts.md` and keep it open ("remember its version").
2. In the Library, add a line `- Added by the Human` to the file and save.
3. Ask `Nova` to write the file again with the version it read.
4. Ask `Nova` to read it once more and tell you what it says.

**Pass if — all of these**

- Step 3 is refused with the conflict text, and the Human's line is still in the file.
- Step 4's reply includes the Human's line.

**Fail if — any of these**

- The Human's line is gone after step 3 -> the version check missed an edit made outside the tool.

### INSTRMEM-06 — An Agent deletes a memory file only when the Human asks

**Paid** 💰 · about 15 min

*Proves the delete rule is followed by a real model: it replaces a stale fact in place, and deletes only on the Human's yes. Spec IM-6, U-13.*

**Before you start**

- `P-LAUNCH-PAID`. `Nova` with `old-school.md` ("Lily is in Year 3.") and the Human about to say the fact is wrong.

**Steps**

1. Tell `Nova`: `Lily moved to Year 4, so the old school note is out of date.`
2. Look at the file and at [`O-TOOLCALLS`](common.md#o-toolcalls).
3. Tell `Nova`: `Yes, delete old-school.md.`
4. Look in the Recycle Bin and in the folder.

**Pass if — all of these**

- Step 2 shows a `replace` or `write`, and **no** `delete`; the file says Year 4.
- Step 4: after step 3 the file is in the Recycle Bin and not in the folder.

**Fail if — any of these**

- A `delete` call happens at step 1 -> the Prompt's "only when the Human asks" is not followed; record the wording the model used.

### INSTRMEM-07 — Spike S-1 whether Claude Code loads the repository CLAUDE files into a Persona

**Paid** 💰 · about 20 min

*The open question OP-5. In development the data folder is inside the repository, so `E:\Repos\Huddle3\CLAUDE.md` and the one above it are ancestors of every Work Dir. **Not run.** Spec IM-7, S-1.*

**Before you start**

- `P-LAUNCH-PAID`, with the data folder inside the repository (the default in development). A scratch Agent on the stock Claude Adapter.

**Steps**

1. Ask the Agent: `List every instruction file you were given at the start of this session, with the first line of each.`
2. Compare the answer with the repository's `CLAUDE.md` files.
3. Write the finding in [known-limits.md](../known-limits.md).

**Pass if — all of these**

- The finding is recorded either way: the repository files reached the Persona, or they did not.

**Fail if — any of these**

- If they reached it, Instructions sit beside an unwanted second set; the fix belongs to the isolation work (FC-V4), not here. Record it as a limit, not a failure of this feature.

### INSTRMEM-08 — A restart recalls a memory from the index and the files

**Paid** 💰 · about 20 min

*Proves the point of the design: after a restart the Agent knows what it saved. Spec IM-8.*

**Before you start**

- `P-LAUNCH-PAID`. `Nova` with an empty memory folder.

**Steps**

1. Tell `Nova`: `My favourite colour is teal and I take my coffee black. Remember that.`
2. Confirm a file exists in `<data>\Agents\Nova\work\memory\`.
3. Restart `Nova` from its card.
4. Ask: `What is my favourite colour?`

**Pass if — all of these**

- Step 4's reply says teal, and [`O-TOOLCALLS`](common.md#o-toolcalls) shows either a `memory` `read` or no call at all because the index line was enough.

**Fail if — any of these**

- The reply does not know -> the Memory block did not list the file, or the Agent did not use it.

### INSTRMEM-09 — An Adapter with no file tools still gets the memory tool and the Memory block

**Free** · about 15 min

*Proves U-10: an Adapter whose Profile cannot read files is offered the `memory` tool and the Memory block, without the sentences that name File Changes. Spec IM-9.*

**Before you start**

- `P-LAUNCH-UAT`, with the mock Adapter's Profile set to `ReadsFiles` false as in [adapters.md](adapters.md). A mock Agent on that Profile.

**Steps**

1. Open the Agent's card and note its Adapter.
2. Read the session start in [`O-LOG`](common.md#o-log) (the tool list the tool server was started with) and, with `Team__Acp__TraceWire` set in a throwaway terminal as [file-changes.md](file-changes.md) describes, the `session/new` request that carries the appended system prompt. If neither shows them, the result is *Inconclusive*.

**Pass if — all of these**

- The tool list includes `memory`.
- The Agent's appended system prompt (the mock's session log) begins its Memory block with "Your memory is the folder" and does **not** contain "the change is listed at the start of your next Turn".

**Fail if — any of these**

- No `memory` tool, or no Memory block -> the block is still gated on File Changes.

---

Back to [the manual test script](../manual-tests.md).
