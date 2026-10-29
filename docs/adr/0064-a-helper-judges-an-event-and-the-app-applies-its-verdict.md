---
status: proposed
date: 2026-10-20
---

# A Helper judges an event and the app applies its verdict

Some decisions need a model but no conversation: does this Task need the Human's acceptance,
does this Evidence meet that Proof rule, what should this Room be called. A **Helper** (code word
`HelperAgent`) makes one such judgement about one event and answers in JSON. Huddle parses the
answer into a typed **Verdict** and applies it through the ordinary domain service. The first
Helper is the **Needs Human Helper**, which turns on a Task's Human gate
([ADR-0061](0061-a-human-gated-task-waits-in-needs-you-for-the-humans-acceptance.md)). The full
design is [Huddle.Helpers-Specifications.md](../Huddle.Helpers-Specifications.md).

## The problem

The Human gate only works when someone turns it on. An Agent that sets an Action before an
outward step turns it on itself. An Agent that writes "Reply to Acme about the renewal price"
and forgets does not, and the Task can reach Done without the Human seeing it.

Making an Agent do the checking is the wrong shape. An Agent has a Room, a Persona, a Room
Budget and memory, and it talks. A yes-or-no judgement on every Task save needs none of that, and
an Agent posting "I checked RSH-208" in a Room on every save would be noise and spend.

## The decision

**A Helper is not an Agent.** It has no Room, no Persona, no Room Budget, and it posts no
Messages. If it talks, keeps memory or works in a Room, it is an Agent (the Retro agent stays
one, [ADR-0041](0041-retro-proposals-are-applied-only-by-the-humans-click.md)). If it judges an
event, it is a Helper.

**Helpers judge, the app acts.** A Helper gets no tools. Huddle builds everything it needs into
the prompt, and the Helper returns JSON. Huddle parses it into the Helper's Verdict record,
checks it, and the Helper's own code applies it through the same service the Human and the
Agents use. A Helper therefore meets every rule an Agent meets, and adds none of its own.

**One fresh adapter process per call.** The runner, generalised from the model catalog's
throwaway probe session, starts the Adapter, opens a session with the Helper's Model and Effort
and a replaced system prompt, sends one prompt, reads the answer, and disposes the process. The
outcome is Ok, Unparseable, Failed or OverBudget. A runner that keeps a process warm is a later
optimisation behind the same interface.

**Shared infrastructure, generic.** A dispatcher takes `Enqueue(helperId, key, input)`, waits a
few seconds per key so a burst of saves is judged once, reuses a cached Verdict for content it has
already judged, and limits how many runs happen at once. None of it knows about Tasks.

**Each Helper has a daily call cap, and reaching it asks the Human.** The Helper pauses, alone;
its card in **Settings › Helpers** reads "Paused — daily cap reached" with **Continue (+N today)**,
a bounded grant. This is the same "ask before spending" rule as the Task wake budget
([ADR-0026](0026-a-change-to-a-task-wakes-its-assignee.md)).

**A failure never blocks or fails a save.** The Task stays as it was saved, the failure is
logged, and the Helper's card shows the last error.

**Configuration in three layers.** Code defaults, `Team:Helpers:<Id>` in appsettings, then the
Human's choice on the card, stored in SQLite: on or off, Adapter, Model, Effort and daily cap.
Changing the Adapter clears Model and Effort
([ADR-0015](0015-model-and-effort-reset-when-the-adapter-changes.md)). The prompt is a
`PromptCatalog` key, edited and reset on the Prompts panel
([ADR-0007](0007-model-facing-text-is-configuration.md)).

**The Needs Human Helper:**

- Judges only a **Agent's** save that creates a Task or changes its title, Action or
  description. It skips status changes, Evidence lines, Tasks already gated or closed, its own
  writes, and content it has already judged.
- Can only turn the gate **on**. It writes through `TaskService.Update` as a new actor kind,
  `TaskActorKind.Helper`, named `Needs Human (Helper)`, with its reason in the Change log:
  `human gate on (reason: Sends a reply to a customer)`. Rule H-1 is unchanged: only the Human
  turns the gate off.
- **Treats the Human's override as final.** A Task on which the Human turned the gate off is
  recorded and never judged again, whatever is later written in it.
- Drops a Verdict whose Task has been saved with new content since it was judged; that newer
  save is judged on its own.
- Ships **on**, on the Claude Adapter with a Haiku-class Model, low Effort and a cap of 50 calls
  a day, and runs only when Adapters are enabled (`Team:Acp:Enabled`).

## Rejected

| Alternative | Why not |
| --- | --- |
| A hidden Agent that checks Tasks | It needs a Room and a Budget, and its Messages are noise. It could also do more than judge |
| Giving the Helper `update_task` | The model would act, not judge. Its refusals would come back as tool text, and its scope would be whatever the tool allows |
| A rule or keyword list instead of a model | "Reply to Acme" and "Draft a reply for review" differ in meaning, not in words. The Human gate's own policy list is the prompt's guide |
| A warm process shared by every call | State could leak between judgements, and a long-lived process needs supervising. Kept for later, behind the same seam |
| A token or money budget instead of a call cap | Adapters do not all report cost; a call count is always known |
| Stopping silently at the cap | The Human would not know judgements stopped. The owner's rule is that a cost guard asks |
| Letting the Helper turn the gate off too | An Agent-run judgement would then be able to ungate outward work, which H-1 forbids for every non-Human actor |
| Judging a Task again after the Human turned the gate off | The Helper would argue with the Human, and the Human would learn to ignore it |
| Reusing `TaskActorKind.Agent` for the Helper | Its changes would count against and be blocked by the per-Task wake budget, and its Name could collide with a Persona's |
| Judging every save | Status moves and Evidence lines do not change what the Task is, and would spend calls for nothing |

## Consequences

- **The Human gate gets a second way on.** An Agent's Action, the Human's switch, and now a
  Helper's Verdict. The Change log says which, and the Task panel tells the Human that turning
  off a Helper's gate is final.
- **A Helper's gate wakes the assignee.** It is a Task change, so ADR-0026 applies: the assignee
  learns the Task now needs the Human's acceptance. The wake is posted with the Human as sender,
  as an outside edit's is, and uses none of the Task's wake budget. That resets the Room Budget
  and the assignee's token Budget, at most once per Task; the specification leaves whether to
  accept that as an open decision.
- **Shipping it on costs money.** With Adapters on, each qualifying Agent save starts one
  Claude adapter process and makes one short Haiku-class call, up to the cap. Each gate it turns
  on also costs one Agent Turn for the wake, which is usually the larger cost.
- **The Helper is a safety net, not the guard.** Task text is data written by an Agent, and a
  model can be talked out of a verdict. The worst a misled Helper can do is leave a gate off,
  which is where the Task started. Agents are still told to set the Action themselves.
- **`TaskActorKind` gains a value**, and `TaskService` accepts a Change log reason when the gate
  turns on, not only with Cancelled or Rejected.
- **Settings gains a Helpers tab**, and the Adapter, Model and Effort selects move out of the
  Agent card into a shared picker, so ADR-0015's reset lives in one place.
- **Slice 3 adds the Feed.** Verdicts, a paused Helper and repeated failures will post there
  through its `IFeedPublisher`
  ([ADR-0063](0063-the-feed-is-a-stored-history-that-each-domain-component-posts-to.md)); this
  decision does not depend on it.
