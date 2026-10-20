# Huddle.HumanGate — Design Specification

**Date:** 2026-10-19 · **Status:** Built 2026-10-19 · **Decision records:**
[ADR-0061](adr/0061-a-human-gated-task-waits-in-needs-you-for-the-humans-acceptance.md),
[ADR-0062](adr/0062-a-teams-definition-of-done-is-a-proof-checklist-in-process-md.md); amends
[ADR-0039](adr/0039-process-gates-live-in-process-md-and-are-enforced-in-taskservice-and-the-permission-handler.md)
and [ADR-0050](adr/0050-needs-you-is-derived-from-room-events-and-held-in-memory.md) · **Vocabulary:**
[language.md](engineering/language.md) (**Human gate**, **Action**, **Proof**, **Definition of Done**, **Needs You**)

This is the design for the **Human gate** on Tasks: a Task that names something leaving the Team,
or that the Human marked, waits in **Needs You** until its Proof is complete and the Human accepts
it. It builds on the [Tasks specification](Huddle.Tasks-Specifications.md), the process gates in
the [Teams specification](engineering/teams-spec.md) (*Gates*), and the
[Needs You specification](Huddle.NeedsYou-Specifications.md).

It is written for the engineers or agents building it, with no memory of the conversation that
produced it. Read §5 for the shape, §6 for the subsystems, and Appendix A for the ordered,
test-first task list. Every design decision is in §9 with the alternative it beat; the decisions
still open are in §10.

> [!IMPORTANT]
> Two pages are binding before any code in this spec is written:
> [rules.md](engineering/rules.md) before editing `src/Huddle.App`, and
> [agents/CSharpPrinciples.md](../agents/CSharpPrinciples.md) for every C# file. Nothing here
> overrides either.

---

## 1. Goal

**Your Teammates do the work. You make the calls.**

Anything that leaves the Team or cannot easily be undone, such as sending, paying, signing,
publishing or merging, stops and waits for the Human. Huddle puts it in Needs You with the work,
the Proof and who checked it, so the Human can decide in one look.

AI Teammates are fast and tireless, but they are not accountable. The gate answers four problems:

| Problem | Without a gate | With the gate |
| --- | --- | --- |
| Accountability | "The AI sent it" is not an answer a customer, auditor or lawyer accepts | A named person accepted every outward Action, with a record of when |
| Actions that cannot be undone | A wrong email, payment, signature or merge is out before anyone looks | Teammates draft as much as they like; nothing gets out without the Human |
| Confident mistakes | A polished draft can hide a wrong number or a missing source | Proof is checked before it reaches the Human, who judges evidence, not tone |
| Runaway work | Teammates loop, argue or burn tokens unnoticed | The Room Budget pauses them and asks (already built, ADR-0006) |

Each decision is logged, so over time the Human learns which kinds of work they can accept
quickly and which need a closer look.

Concretely:

1. **One Task's journey: Draft, Check, Track, Gate, Decide.** The assignee drafts. Other members
   supply the Proof the Team's Definition of Done asks for. The Task's Change log records who did
   what. Once the Proof is complete the Task moves to Needs You. The Human accepts, requests
   changes or declines.
2. **Two rules hold however the Team's process is set up.** Accepting is always on: a process can
   add Proof, never remove the Human's decision. Incomplete Proof locks the gate: the Human sees
   "Done locked · Proof 2/3" and an "Ask Zoe for QA evidence" button, never a way to override.
3. **The roles, as RACI.** Responsible: the assignee. Consulted: reviewer Teammates (QA, Skeptic,
   Compliance Checker) who supply Proof. Accountable: the Human, and only the Human, through the
   gate. Informed: the Task's Room and its Change log.

### 1.1 Policies

These are stated to the Human in the User Guide and to Agents in their tool text.

1. **What is outward, and always gated:** customer messages, money, contracts, public posts,
   merges to main, deleting data. A Teammate about to do one of these sets the Task's Action first.
2. **What needs no gate:** internal notes, drafts, research and Task updates.
3. **Proof before people:** a gated Task reaches the Human only once the Definition of Done is met.
4. **Declines teach:** a decline or a change request carries a reason, which reaches the Teammate
   and the Retro.
5. **Audit trail:** every decision is a line in a plain file: who, what, when, on which Proof.

---

## 2. Example use cases

| # | Situation | What must happen |
| --- | --- | --- |
| U-1 | Miriam drafts a research memo; the Human turned on **Requires your acceptance** | Miriam cannot mark it Done. When the Proof is 4/4 she moves it to Needs You; the Human accepts; it is Done, logged with the Human's name |
| U-2 | Leo drafts a customer reply and sets the Action "Send reply to Acme" | The gate is on and locked. After the Human accepts, Leo is woken to send it, then moves the Task to Done |
| U-3 | The Proof is 3/4: Ruth's citation check (#RSH-209) has 19 of 23 boxes ticked | The decision card shows "Proof 3/4 — citation check still open (Ruth, 19 of 23 in #RSH-209)". Accept is locked; Request changes works; "Ask Ruth" posts a Mention |
| U-4 | Miriam tries `update_task` with status Done before acceptance | Refused: the tool result says the Task waits for the Human's acceptance in Needs You |
| U-5 | The Human drags a gated card into Done on the board | From Review: refused with "RSH-208 goes to Needs you first (Proof 3/4)", and the card stays. From Needs You with complete Proof: the decision card opens as a dialog; Accept completes the move, Cancel leaves the card in Needs You |
| U-6 | Leo edits the reply after the Human accepted it | The acceptance no longer matches; the Task is back on the Needs You list, unaccepted |
| U-7 | The Human declines with "Wrong customer" | The Task is Rejected and closed; Leo is woken with the reason; the Change log has `declined: Wrong customer` |
| U-8 | An Agent edits the Task file on disk to remove `action:` | The old head is written back and the Change log says `outside edit refused by Human gate` |
| U-9 | Huddle restarts while two Tasks wait in Needs You | Both are on the Needs You card at once, read from their files |

---

## 3. Non-goals

| Not in v1 | Why |
| --- | --- |
| Huddle carrying out the Action (sending the mail, merging the PR) | The Teammate has the tools. ADR-0061, Rejected |
| Refusing an Agent's outward tool call before acceptance | The gate guards the Task status. See §10 OD-1 for merge and push |
| More than one approver, or approval by a Teammate | The Human is the one accountable party |
| Watchers on a Task | Not a Tasks concept today. The Room and Change log inform |
| Writing the decline into the Teammate's Memory | Memory belongs to the Agent (language.md). It gets the reason and decides |
| A per-Task checklist | ADR-0062, Rejected. A Task needing more gets a tagged sub-Task |
| Turning ordinary Tasks' Proof into a hard gate | ADR-0062 keeps plan decision D3 for ordinary work |

---

## 4. Design principles

- **One path changes a Task.** Every rule here lives in `TaskService`, so the panel, the board,
  the Agent tools and an outside edit meet the same refusal (ADR-0039's reason, unchanged).
- **The file is the record.** The gate, the Action and every decision are in the Task file's
  frontmatter and Change log, readable in any editor. Needs You reads them; it stores nothing.
- **A decision is bound to what was decided on.** An acceptance carries a hash of the title,
  description and Action. Change any of them and the acceptance is void.
- **The way past a locked gate is to ask, not to override.**
- **Only Proof Huddle can check.** Every Definition of Done rule points at a record: an Evidence
  line, a check run, a Done sub-Task, a document's review history.

---

## 5. Architecture overview

```text
Task file (frontmatter: human_gate, action; Change log: accepted / changes requested / declined)
        │ parse
        ▼
TaskItem ── HumanGate.IsGated ── ProofEvaluator ◄── ProcessDocument.DefinitionOfDone ◄── Process.md
        │                              │
        ▼                              ▼
TaskService.Update / Create / OnOutsideEdit ── HumanGateRules.Check(change, proof, acceptance)
        │                                         │ refusal: TaskResult.GateRefused (no override)
        ▼
TaskService.Decide(id, decision, note, baseVersion)  ◄── Task panel decision card, Needs You page
        │ TaskChanged
        ├──► TaskTriggerService (wakes the assignee, ADR-0026; the note rides in the wake Message)
        └──► NeedsYouTaskSource ──► NeedsYouList / NeedsYouCard / NeedsYouPage
```

New types live in `src/Huddle.App/Tasks/HumanGate/` except the Process parser addition
(`Teams/ProcessDocument.cs`) and the components (`Components/Tasks/`, `Components/NeedsYou/`).

---

## 6. Subsystems

### 6.1 The Task file

Two new optional frontmatter keys, written after `sprint` and before the description:

```yaml
human_gate: true
action: 'Send reply to Acme about the renewal'
```

- `human_gate` is `true` or absent. `false` is read as absent and never written.
- `action` is one line, at most 200 characters, scalar-quoted by the existing
  `ComposeScalarLine` rules.
- `TaskItem` gains `HumanGate` (`bool`) and `Action` (`string?`). `IsGated => HumanGate || Action is not null`.
- `TaskFileFormat`'s known-key list gains both, so neither lands in `UnknownFields`.

**New Change log summaries** (the Actor column is who did it, as today):

| Summary | Written by |
| --- | --- |
| `human gate on` / `human gate off` | Any member / the Human |
| `action set: <text>` / `action cleared` | Any member / the Human |
| `accepted <hash>` | The Human's Accept. `<hash>` is the first 12 hex characters of SHA-256 over `title + "\n" + action + "\n" + description`, normalised to LF |
| `changes requested: <note>` | The Human's Request changes |
| `declined: <reason>` | The Human's Decline |
| `outside edit refused by Human gate: <message>` | `TaskService.OnOutsideEdit` |

A status move is logged as today. A move into Needs You is logged with its Proof count:
`status: Review → Needs You (Proof 4/4)`.

### 6.2 The status

`TaskState.NeedsYou` is added between `Review` and `Done`, wire name `Needs You`. It is not
terminal and not Won't do. `TaskStates.All` keeps declaration order, so the board gains a
column between Review and Done.

**Views.** When `ViewStore` reads a View whose state filter contains Review and not Needs You,
it adds Needs You in memory. It does not write the file back. A View the Human saves afterwards
keeps whatever the Human chose.

### 6.3 Proof: the Definition of Done

`ProcessDocument` gains `DefinitionOfDone` (`IReadOnlyList<DoneItem>`), parsed from the
`## Definition of Done` body section (ADR-0062 has the grammar). `DoneItem` is a record of
`Label`, `Rule` (a closed hierarchy: `EvidenceFrom(step)`, `CommandExits(command)`,
`TaskTagged(tag)`, `DocumentReviewedBy(step)`, `Unreadable(text)`) and `Source` (`Process` or the ADR-0039 gate it came from).
The section is optional and is not added to the PR-6 outline headings, so its absence raises no
"missing section" warning.

`ProofEvaluator.Evaluate(TaskItem task, ProofContext context)` is pure. It returns a `Proof`:
each item with `Met` (`bool`), `Owner` (`string?`) and `Detail` (`string?`), plus `MetCount`,
`Total` and `IsComplete`. `ProofContext` carries the Task's Evidence, each Evidence author's role
coverage (the same map `GateContext.AuthorRoleCovers` holds), the Team's members with their
coverage, the Task's children, and the document history (ADR-0060) of each Markdown document
attached to the Task (a Library path line in its description, as the Task attachments spec defines).

| Rule | Met | Owner when open | Detail when open |
| --- | --- | --- | --- |
| `EvidenceFrom(step)` | Evidence line with a command, by an author covering `step` | First member covering `step`, by roster order | none |
| `CommandExits(cmd)` | Latest Evidence line whose command trims equal to `cmd` has exit 0 | The assignee | `last run exit <n>` or `not run` |
| `TaskTagged(tag)` | A child with `tag` is Done | The child's assignee | `<checked> of <boxes> in #<id>` (counting `- [x]` and `- [ ]` lines in the child's description, outside fences), or `no Task yet` |
| `DocumentReviewedBy(step)` | An attached Markdown document has a `Reviewed` history entry by an author covering `step`, later than its latest `Created` or `Edited`, with no later `Objected` | First member covering `step` | `no document attached`, `<file> — not reviewed yet`, `<file> — edited after <name>'s review` or `<file> — <name> objected: <summary>` |
| `Unreadable(text)` | Never | none | `Huddle can't read this rule` |

With several attached documents, the item is met when any one of them meets it, and the detail
names the one closest to meeting it. An edit made outside Huddle leaves no history entry
(ADR-0060), so a review survives it: a known limit, listed in the User Guide.

The enabled ADR-0039 Done gates are merged in by `ProofEvaluator`, not by the parser:
`qa-evidence-before-done` as "QA evidence" (skipped when an item already says
`evidence from verify`) and, for a bug, `failing-test-before-fix` as "A test failed before the fix".

### 6.4 The rules in `TaskService`

`HumanGateRules.Check(TaskItem before, TaskItem after, TaskActor actor, Proof proof)` is pure and
returns a `GateRefusal?`. `TaskService.UpdateCore`, `CreateCore` and `OnOutsideEdit` call it
**before** `ProcessGateService.CheckTaskChange`. For a gated Task (`before` or `after` gated), a
refusal from either is returned with no override: `humanOverridesGates` is ignored. `GateId`
gains `HumanGate` and `ProofIncomplete` so the refusal names its reason; `ProcessDocument.TryParseGate`
never returns them, so they can never be written in `Process.md`.

| # | Change | Refused unless |
| --- | --- | --- |
| H-1 | `human_gate` true → absent | The actor is the Human and `action` is absent |
| H-2 | `action` set → absent | The actor is the Human |
| H-3 | Any status → Needs You | The Task is gated, and `proof.IsComplete` |
| H-4 | Any status → Done, on a gated Task | Status was Needs You, `proof.IsComplete`, and the acceptance is valid (§6.5) |
| H-5 | A gated Task created in Needs You or Done | Never allowed: a gated Task is created in an earlier status |

Refusal messages (shown on the board, in the panel and in tool results):

- H-1, H-2: `Only <Human> can turn off the Human gate on <id>.`
- H-3 on an ungated Task: `<id> isn't Human-gated, so it goes from Review to Done.`
- H-3: `<id> can go to Needs You once its Proof is complete (3/4: citation check is open).`
- H-4: `<id> needs <Human>'s acceptance. Move it to Needs You when the Proof is complete; you'll be woken when <Human> decides.`

Moving a gated Task out of Needs You to an earlier status is allowed for anyone, and voids any
acceptance (§6.5) because a new entry is needed.

### 6.5 Acceptance

An acceptance is **valid** when the Task is in Needs You and the latest `accepted <hash>` Change
log entry after the Task's latest move into Needs You carries the hash of the Task's current
title, Action and description. `Acceptance.Of(TaskItem)` is pure and returns
`None`, `Valid(at, by)` or `Stale(at, by)`.

An edit to the title, Action or description of an accepted Task is allowed, makes the acceptance
`Stale`, and puts the Task back on the Needs You list. The Human sees "Changed since you accepted
it" on the card.

### 6.6 Decisions

`TaskService.Decide(TaskId id, TaskDecision decision, string? note, string baseVersion, TaskActor actor)`
returns a `TaskResult`. `actor` must be the Human; anyone else gets `TaskResult.Refused`.
`baseVersion` is the version the Human was shown. A mismatch returns the existing conflict
result, and the card reloads with "This Task changed while you were reading it".

| Decision | Precondition | Writes | Then |
| --- | --- | --- | --- |
| `Accept` | Needs You; Proof complete; acceptance not already valid | `accepted <hash>`; for a Task with no Action, also status → Done | `TaskChanged`, which wakes the assignee (ADR-0026). For an Action, the wake Message says "Accepted: go ahead and <action>, then move <id> to Done" |
| `RequestChanges` | Review or Needs You; `note` non-empty | `changes requested: <note>`; status → In Progress | Wake Message carries the note |
| `Decline` | Review or Needs You; `note` non-empty | `declined: <note>`; status → Rejected; then `Close` | Wake Message carries the reason |

`TaskTriggerService` already builds the wake Message from a `TaskChange`. It gains the decision
and its note so the Message can quote them; its §10.2 guards (no self-trigger, coalescing,
Budget) are unchanged.

### 6.7 Needs You

**Task entries.** `NeedsYouTaskSource` lists every open Task that is gated, in Needs You, and
whose acceptance is not `Valid`, oldest first by the move into Needs You. It reads `TaskStore`'s
snapshot and listens to `TaskEvents`. It holds no state of its own, so a restart loses nothing.
A Task entry has no ✕. It leaves the list only by a decision or a status change.

`NeedsYouReason` is unchanged: Task entries are a second list, not a fifth Room reason.

**The card.** `NeedsYouCard` shows Room entries, then Task entries, with one count for both. A
Task row reads `<id> <title>` with `Waiting on you · Proof 4/4` and links to `/needs-you#<id>`.

**The page.** `/needs-you` (`NeedsYouPage.razor`, added to the shell's navigation) lists Task
entries first, each as a `TaskDecisionCard`, then the Room entries in the existing
`NeedsYouList`.

### 6.8 The Task panel

**Human gate section** (`TaskHumanGate.razor`, in `TaskDetail` under the fields; mockup 1):

- Header chip "Human gate" and the line **You decide whether this is done**.
- **Requires your acceptance** switch. Locked on, with a lock icon, when an Action is set: "This
  Task names an Action that leaves the team."
- **What will happen** field: the Action, one line, placeholder "Nothing leaves the team".
- Helper line: "<assignee> can move it to Needs you once every line has Proof. Teammates can't mark it Done."
- **Can move to Done** with a chip "<Human> only" and a lock.
- Explanation: for no Action, "Teammates draft, check and track: you decide. Accepting marks it
  Done; nothing is sent or published. Done also needs the team's Proof to be complete." For an
  Action, the second sentence is "Accepting lets <assignee> <action>."

**Proof checklist** (`TaskProofList.razor`), on every Task with a non-empty Definition of Done:
one row per item with a tick or an open circle, the label, the owner and the detail. On an open
item with an owner, an **Ask <owner>** button posts a Message from the Human into the Task's Room
(the Room rule in Tasks spec §10.4) reading `@<owner>, <id> needs <label> before it can come to me.`
On an ordinary Task the heading reads "Definition of Done (for information)".

**Decision card** (`TaskDecisionCard.razor`, mockup 2), on a gated Task in Review or Needs You:

- Header chip "Your decision".
- The first line: "<n> of <total> Proof items are in." or "All Proof is in."
- The Accept sentence from the Human gate section.
- When Proof is incomplete, a locked notice: "Proof 3/4 — citation check still open (Ruth, 19 of
  23 in #RSH-209). Accept unlocks when it's in."
- When the Task is still in Review: "Accept unlocks when <assignee> moves it to Needs you."
- Buttons **Request changes** and **Accept** (disabled with a lock until §6.6 allows it), and an
  overflow menu holding **Decline**. Request changes and Decline open the existing `ReasonDialog`
  with the note required.
- The trail: drafted by the assignee, checked by each Evidence author, tracked as `<id>`.

**Move anyway** is not rendered for a gated Task, whatever the refusal.

### 6.9 The board

The Needs You column sits between Review and Done. A gated card shows a small lock chip, "Needs
you" in Needs You or "Accepted" when the acceptance is valid. A drop the rules refuse leaves the
card where it was and shows the refusal, as today.

**A drop into Done is a shortcut to the decision, never the decision itself.** For a gated card
dropped into Done by the Human:

| Card is in | Proof | Drop |
| --- | --- | --- |
| Review or earlier | Any | Refused: `<id> goes to Needs you first (Proof <m>/<n>).` |
| Needs You | Incomplete | Refused, with the locked notice and its **Ask \<owner>** button |
| Needs You | Complete | `TaskDecisionCard` opens in a dialog over the board, with the Proof and the Task text in view. **Accept** calls `Decide` and the card lands in Done (or stays in Needs You marked Accepted, for a Task with an Action). **Request changes** and **Decline** work as in the panel. **Cancel** or Escape leaves the card where it was |

The drop itself never calls `Update` with status Done, so H-4 still holds: the only way into
Done is an acceptance, or the assignee's move after one.

### 6.10 Agents

- `update_task` and `create_task` accept `human_gate` (true only) and `action`. A refused change
  returns the §6.4 message.
- `get_task` returns `human_gate`, `action`, the Proof (`3/4`, and each open item with its owner)
  and the acceptance (`none`, `accepted by <Human> at <time>`, or `stale`).
- `list_tasks` accepts the status `Needs You`.
- `TaskToolText` gains a paragraph, reached through `get_help` (progressive discovery): what
  counts as outward (§1.1), that a Teammate sets the Action before doing it, that only the Human
  accepts, and that after acceptance the assignee carries out the Action and then moves the Task
  to Done. The tool names keep the `mcp__team__` prefix.

### 6.11 The Retro

`RecordsBundleBuilder` includes `changes requested:` and `declined:` lines in the Task records it
hands the retro role, so repeated reasons can become a proposed process change.

---

## 7. Interface copy

| Where | Text |
| --- | --- |
| Status | `Needs you` (the wire value is `Needs You`) |
| Panel section | `Human gate` · `You decide whether this is done` |
| Switch | `Requires your acceptance` |
| Locked switch tooltip | `This Task names an Action that leaves the team.` |
| Action field | `What will happen` · placeholder `Nothing leaves the team` |
| Done rule | `Can move to Done` · `<Human> only` |
| Decision card | `Your decision` · `Request changes` · `Accept` · `Decline` |
| Locked notice | `Proof <m>/<n> — <label> still open (<owner>, <detail>). Accept unlocks when it's in.` |
| Stale | `Changed since you accepted it.` |
| Ask button | `Ask <owner>` (the button's accessible name adds `for <label>`) |
| Card row | `Waiting on you · Proof <m>/<n>` |

Use **Proof** for the checklist and **Evidence** for one line. Never "approval", "sign-off" or
"override" in this feature's copy.

---

## 8. Testing

Functional tests with the real `TaskService`, `TaskStore` on a temp folder and a
`ManualTimeProvider`, per [agents/Testing.md](../agents/Testing.md); bUnit for the components per
[agents/BlazorTesting.md](../agents/BlazorTesting.md).

| Area | Must pin |
| --- | --- |
| File format | Both keys round-trip; `false` is not written; an over-long Action is refused |
| `ProofEvaluator` | Each rule met and open; a document edited or objected to after its review is open; owner and detail; ADR-0039 gates merged once; Unreadable never met; empty list complete |
| `ProcessDocument` | The section parses; bad bullets become Unreadable; CRLF and LF; absent section is empty |
| `HumanGateRules` | H-1 to H-5 for Human and Agent actors; ungated Tasks are untouched |
| No override | `humanOverridesGates: true` does not pass any refusal on a gated Task; still passes on an ordinary one |
| `Acceptance` | Valid, Stale after each of title, Action, description; None after leaving and re-entering Needs You |
| `Decide` | Each decision's precondition, writes and resulting status; non-Human refused; version conflict |
| Outside edit | Clearing a key and a forbidden status move are each reverted and logged |
| Needs You | Task entries appear and leave on each decision; survive a new `TaskStore` (restart) |
| Components | Locked switch; no Move anyway on a gated Task; Accept disabled until allowed; Ask posts the Mention |

Manual tests go in a new page, `engineering/manual-tests/human-gate.md`, written with the build.

---

## 9. Decisions

| # | Decision | Alternative it beat |
| --- | --- | --- |
| D-1 | Gated by `human_gate` or a non-empty `action` | A fifth `Process.md` gate (a Team could turn it off); gating every Task |
| D-2 | An Action locks the gate on; only the Human turns it off or clears the Action | Anyone may toggle (an Agent could ungate its own outward work) |
| D-3 | Needs You is a ninth status between Review and Done | Review with a flag (the board and Views could not tell them apart) |
| D-4 | Accept moves a Task without an Action to Done; with an Action, the assignee moves it after carrying it out | Accept always means Done (Done before the thing happened) |
| D-5 | Acceptance is bound to a hash of title, Action and description | Accepting a status (a later edit would ride on the old acceptance) |
| D-6 | No override on a gated Task; "Ask <owner>" instead | Keep Move anyway for the Human |
| D-7 | Definition of Done in a `Process.md` body section with four rules (evidence, command, tagged sub-Task, reviewed document) | Frontmatter list; per-Task checklist; free text read by a model |
| D-8 | Proof enforced only on gated Tasks | Enforce everywhere (overturns plan decision D3) |
| D-9 | Needs You Task entries are read from Task files, no ✕ | A fifth in-memory reason (lost on restart, dismissable) |
| D-10 | A Human's drop into Done opens the decision card as a dialog; from Review it is refused | The drop is the acceptance (decided without the Proof in view); the board always refuses (a dead end the Human cannot act on) |
| D-11 | Decline sits in an overflow menu and closes as Rejected | A third main button (mockup 2 has two) |
| D-12 | Decline reasons reach the Teammate and the Retro, not its Memory | Huddle writes the Agent's Memory |

---

## 10. Open decisions

| # | Question | Recommendation |
| --- | --- | --- |
| OD-1 | Should `WorkDirPermissionHandler` refuse an Agent's `git merge` or `git push` until a gated Task is accepted? | **Defer.** A command names no Task, so the handler cannot tell which acceptance it needs. Tying it to "the Persona has any accepted Task" would refuse unrelated merges and pass the wrong one. `checks-before-merge` stays as it is |
| OD-2 | Should a Task carry its own Accept sentence ("makes the memo the team's current answer", mockup 2)? | A fixed sentence in v1 (§6.8). Add an optional `outcome:` key later if Humans ask for it |
| OD-3 | Should Huddle move a gated Task to Needs You by itself when the Proof completes? | No in v1: the assignee moves it, as mockup 1 says. Revisit after use |

---

## 11. Files touched

| File | Change |
| --- | --- |
| `Tasks/TaskState.cs`, `Tasks/TaskStates.cs` | `NeedsYou` |
| `Tasks/TaskItem.cs`, `Tasks/TaskFileFormat.cs`, `Tasks/TaskPatch.cs`, `Tasks/TaskDraft.cs` | `HumanGate`, `Action` |
| `Tasks/HumanGate/` (new) | `HumanGateRules`, `Acceptance`, `ProofEvaluator`, `Proof`, `ProofItem`, `ProofContext`, `TaskDecision` |
| `Tasks/TaskService.cs` | Call the rules; ignore the override on gated Tasks; `Decide` |
| `Tasks/TaskTriggerService.cs` | Decision and note in the wake Message |
| `Tasks/Views/ViewStore.cs` | Read-time Review → Needs You rule |
| `Teams/ProcessDocument.cs`, `Teams/DoneItem.cs` (new), `Teams/GateId.cs` | Definition of Done; two refusal ids |
| `Teams/RecordsBundleBuilder.cs` | Decision lines in the Retro records |
| `NeedsYou/NeedsYouTaskSource.cs` (new) | Task entries |
| `Acp/Tools/UpdateTaskTool.cs`, `CreateTaskTool.cs`, `GetTaskTool.cs`, `ListTasksTool.cs`, `TaskToolText.cs` | §6.10 |
| `Components/Tasks/TaskDetail.razor`, `TaskBoard.razor`, `TaskCard.razor` | Section, column, chip, no Move anyway |
| `Components/Tasks/TaskHumanGate.razor`, `TaskProofList.razor`, `TaskDecisionCard.razor` (new) | §6.8 |
| `Components/NeedsYou/NeedsYouCard.razor`, `NeedsYouPage.razor` (new) | §6.7 |
| `TeamTypes/Builtin/*/processes/*.md` | A Definition of Done per process template |
| `docs/Huddle.UserGuide.md`, `docs/engineering/wireframes.md`, `docs/engineering/code-map.md` | With the build |

---

## Appendix A. Tasks, in order, test first

Each task is one commit: a failing test, the code, a green `dotnet build Huddle.slnx` and
`dotnet test Huddle.slnx --`.

| # | Task | Tests |
| --- | --- | --- |
| HG-1 | `TaskState.NeedsYou` and its wire name; board column and filters render it | `TaskStatesTests`; board column order |
| HG-2 | `human_gate` and `action` in `TaskItem` and `TaskFileFormat` | Round-trip, `false` not written, length limit |
| HG-3 | `ViewStore` read-time rule | A View with Review gains Needs You; the file is not rewritten |
| HG-4 | `ProcessDocument.DefinitionOfDone` and `DoneItem` | Grammar, Unreadable, CRLF, absent section |
| HG-5 | `ProofEvaluator` | Every row of §6.3, gate merging, empty list |
| HG-6 | `Acceptance` and the hash | Valid, Stale per field, None after re-entry |
| HG-7 | `HumanGateRules` H-1 to H-5 | Per rule, Human and Agent |
| HG-8 | `TaskService` wiring and no override on gated Tasks | Override ignored on gated, honoured on ordinary |
| HG-9 | `TaskService.OnOutsideEdit` for the gate | Revert and log for each forbidden edit |
| HG-10 | `TaskService.Decide` | §6.6 table, non-Human, version conflict |
| HG-11 | Wake Message carries the decision and note | `TaskTriggerServiceTests` |
| HG-12 | Agent tools and `TaskToolText` | Tool results for each refusal; `get_task` fields |
| HG-13 | `NeedsYouTaskSource` | Appears, leaves on each decision, survives restart |
| HG-14 | `TaskProofList` and Ask | Rows, owner, detail, the posted Mention |
| HG-15 | `TaskHumanGate` section | Locked switch with an Action; Human-only toggle |
| HG-16 | `TaskDecisionCard` | Button states for each status and Proof; Decline in the menu; stale notice |
| HG-17 | `TaskDetail` hides Move anyway on gated Tasks; board chip, refusals and the decision dialog on a drop into Done | bUnit: each §6.9 row; Cancel leaves the card; Accept calls `Decide`, never `Update` |
| HG-18 | `NeedsYouCard` Task rows and `/needs-you` page | bUnit; one count |
| HG-19 | Retro records include decision lines | `RecordsBundleBuilderTests` |
| HG-20 | Definition of Done sections in the built-in process templates | Every template parses with no Unreadable item |
| HG-21 | User Guide, wireframes, code-map, manual tests page | `Check-All.ps1` |
