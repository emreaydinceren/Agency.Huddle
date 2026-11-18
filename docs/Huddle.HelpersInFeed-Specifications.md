# Huddle.HelpersInFeed — Design Specification

**Date:** 2026-10-20 · **Status:** Built 2026-10-20 on `feat/helpers-in-feed`; see Deviations at the end · **Decision record:**
[ADR-0065](adr/0065-huddle-posts-as-itself.md); builds on
[ADR-0063](adr/0063-the-feed-is-a-stored-history-that-each-domain-component-posts-to.md) (the Feed),
[ADR-0064](adr/0064-a-helper-judges-an-event-and-the-app-applies-its-verdict.md) (Helpers),
[ADR-0026](adr/0026-a-change-to-a-task-wakes-its-assignee.md) and
[ADR-0050](adr/0050-needs-you-is-derived-from-room-events-and-held-in-memory.md) · **Vocabulary:**
[language.md](engineering/language.md) (**Feed**, **Feed item**, **Notification**, **Helper**,
**Verdict**, **Human gate**, **Needs You**)

This is slice 3: **Helpers post to the Feed**. Slice 1 built the [Feed](Huddle.Feed-Specifications.md)
and slice 2 built [Helpers](Huddle.Helpers-Specifications.md) and the Needs Human Helper, each
without the other. This slice joins them. The Human sees each Helper call in the Feed and can
overrule it, and a Helper that stops working says so where the Human looks. The slice also fixes
the one place a Helper still speaks as the Human: the wake-up Message to the assignee.

It is written for the engineers or agents building it, with no memory of the conversation that
produced it. It names the code as it stands on `main` at `b176a88e` (2026-10-20). Read §4 for what
changes, §5 for each part, and Appendix A for the ordered, test-first task list. Decisions are in
§7; §8 records that none are open.

> [!IMPORTANT]
> Binding before any code: [rules.md](engineering/rules.md) for `src/Huddle.App`,
> [agents/CSharpPrinciples.md](../agents/CSharpPrinciples.md) for every C# file, and
> [traps.md](engineering/traps.md) before Task HF-9, which touches who may send a Message. UI work
> follows [UXDesignGuidelines.md](engineering/UXDesignGuidelines.md) and updates
> [wireframes.md](engineering/wireframes.md) in the same commit.

---

## 1. Goal

When the Needs Human Helper turns a Task's Human gate on, the Human hears about it once, with the
Helper's reason, and can overrule it. When a Helper is paused by its daily cap or keeps failing,
the Human hears about that too, instead of finding out from a Settings card days later. And a
Helper's wake-up Message no longer pretends to be the Human.

### 1.1 Policies

1. **The component that knows posts** (Feed spec Policy 1). Task rows still come from
   `TaskService` through `TaskFeedRules`; Helper rows come from the Helper code, through a small
   `HelperFeed` that the dispatcher and the Helpers card call.
2. **No duplicate rows.** A Helper's gate change is already a `task-gate-on` item, posted by
   `TaskService`. This slice makes that item say it was a Helper and why. It does not add a second
   "Verdict" item.
3. **A Helper is not the Human and not an Agent,** in the Feed or in a Room. It gets its own
   Feed actor kind, and its wake-up Messages are posted by Huddle.
4. **Read-only stays.** The Feed shows what happened and links to where it is fixed. Continue, turning
   a Helper off and turning a gate off stay where they are (Feed spec §3, v2).

## 2. Example use cases

- Nova writes a Task "Email the pricing sheet to Acme". The Needs Human Helper turns its gate on.
  The bell's count goes up by one: **Needs Human (Helper)** turned on Requires your acceptance for
  `#GRO-41 Email the pricing sheet to Acme` · "Sends a document outside the Team". The Human opens
  the Task, decides the gate is wrong and turns it off. The item dims as resolved.
- It is 16:00 and the Helper has made its 50 calls. The next Agent save is skipped, and the Feed
  shows **Huddle** paused `Needs Human`: its daily cap of 50 calls is reached. The Human follows
  the link to Settings > Helpers and presses Continue (+10 today). The item resolves.
- The Claude adapter's login expired. The Helper fails three times in a row; the Feed shows
  **Huddle**: `Needs Human` failed 3 times in a row · "The adapter isn't signed in". The Human signs
  in again; the next good run resolves it.
- The Helper's wake-up Message reaches Nova in the Room Nova shares with the Human. An earlier
  Mention of the Human in that Room is still unanswered, and it **stays** in Needs You, because the
  wake-up was posted by Huddle, not by the Human.

## 3. Non-goals

| Not in this slice | Why |
| --- | --- |
| A Feed item for a Verdict that changes nothing ("no gate needed") | Noise: most Tasks are not gated. The Helpers card keeps the last result |
| Continue, turn off, or gate-off buttons in the Feed | Inline actions are v2 (Feed spec §3). The Feed links to the Helpers card or the Task |
| A Feed item for every Helper failure | One item after three in a row (Helpers spec §6.11); single failures stay on the card |
| Other Helpers | The parts here are per Helper and generic, so the next Helper gets them free |
| Moving any other Huddle-made Message to the Huddle sender | Only the Helper's wake-up is in scope; outside edits follow in a follow-up (HF-D9) |

## 4. What changes, in one table

| Part | Today (`b176a88e`) | After this slice |
| --- | --- | --- |
| Feed actor for a Helper | `TaskFeedRules.ActorOf` maps `TaskActorKind.Helper` to `FeedActorKind.OutsideHuddle` (the `_` arm) | New `FeedActorKind.Helper`, wire name `helper` |
| A Helper's `task-gate-on` | Activity, no reason | A **Notification** carrying the Helper's reason, resolved by the Human's call (§5.2) |
| Helper health | Only on the Helpers card | Two new source types, `helper-paused` and `helper-failing`, both Notifications (§5.3) |
| Categories | Six: All, Needs you, Tasks, Documents, Rooms, Gates | Seven: **Helpers** after Gates (§5.4) |
| Feed targets | Task, Room, Document | Also **Helper**, key `helper:<id>`, opening `/settings/helpers#helper-<id>` (§5.5) |
| The wake-up after a Helper's change | Posted as the Human (`TaskTriggerService.ResolveSenderIdAsync`) | Posted by **Huddle**, a sender that is neither the Human nor an Agent (§5.6, ADR-0065) |

## 5. Parts

### 5.1 The Helper actor

- `FeedActorKind` gains `Helper`. `FeedPayloads` writes and reads it as `"helper"` in the
  envelope's `actorKind`. A row written before this slice has no `helper` value and reads as before.
- `TaskFeedRules.ActorOf` maps `TaskActorKind.Helper` to `FeedActorKind.Helper`. The `_` arm stays
  for `OutsideHuddle` only and becomes an explicit `TaskActorKind.OutsideHuddle` arm, so the next new
  `TaskActorKind` value fails the build (CS8509) rather than falling silently into "outside Huddle".
- The Name stays the Task actor's Name, `Needs Human (Helper)` (`TaskActors.Helper`).
- **Drawing.** A Helper item's avatar is the Helper mark: `Icons.Material.Outlined.Psychology` (the
  Settings > Helpers icon) on a Quiet tile, in `FeedItemRow` (20px) and `FeedCard` (40px). Clicking
  it does not filter by Agent (a Helper is not one); it does nothing. In the Agent filter's
  terms, a Helper is never an Agent.

### 5.2 A Helper's gate change becomes a Notification

**Payload.** `TaskGatePayload` gains a trailing `string? Reason = null`. It is additive: rows already
stored deserialise with `Reason` null, so the envelope version stays 1.

**Rule.** In `TaskFeedRules.For`, the existing `task-gate-on` post (gated now, not before, actor not
the Human) changes in two ways when `change.Actor.Kind == TaskActorKind.Helper`:

- `notify` is **true**. An Agent's gate-on stays Activity, as slice 1 decided.
- `Reason` is the patch's reason. A Task has no Reason field: `NeedsHumanHelper.ApplyAsync` puts its
  cut Verdict reason in `TaskPatch.Reason`, and `TaskService` only writes it into the Change log
  summary as `(reason: …)`. So `TaskChange` gains a trailing `string? Reason = null`, which
  `TaskService.UpdateCore` fills from `resolvedPatch.Reason` beside the existing `Decision` and
  `Note`. The rule reads `change.Reason`; it never parses the summary.

**Resolved when** (`TaskFeedRules.For` adds a `task-gate-on` resolution) any of these is true for the
change:

| Condition | Why |
| --- | --- |
| The gate goes off (`before.IsGated && !after.IsGated`) | The Human overruled the Helper (only the Human can, rule H-1) |
| `change.Decision` is set (Accept, Request changes or Decline) | The Human made the call the gate asked for |
| The Task starts waiting in Needs You (the `task-needs-you` post fires) | That Notification supersedes this one: one ask per Task at a time |
| The Task closes | Nothing is left to decide |

`TaskFeedRules.Holds(task-gate-on, …)` (startup, Feed spec §6.10) is: the Task exists, is open, is
gated, has no recorded decision since the item was posted, and is not waiting in Needs You. An Agent's
gate-on item is never a Notification, so it is never resolved, as before.

**Copy** (Feed spec §7, new row):

| Source type | Summary (chips in braces) | Status line |
| --- | --- | --- |
| `task-gate-on`, Helper actor | `turned on Requires your acceptance for {Task}` · `<reason>` | `Asked <hh:mm> · you can turn it off on the Task` |

The kind badge is the existing lock. The card's expanded Task preview (`FeedTaskPreview.razor`)
gains the Helper's line in its footer, from the same `HelperGateNote` the Task panel uses ("Turned
on by the Needs Human Helper: …"), when the Task's latest gate-on entry is a Helper's.

### 5.3 Helper health: two new source types

| Source type | Wire name | Posted by | Notify | Target | Payload | Category |
| --- | --- | --- | --- | --- | --- | --- |
| `HelperPaused` | `helper-paused` | `HelperFeed`, from `HelperDispatcher` | Yes | `helper:<id>` | `HelperPausedPayload(HelperId, Name, DailyCap, Granted)` | Helpers |
| `HelperFailing` | `helper-failing` | `HelperFeed`, from `HelperDispatcher` | Yes | `helper:<id>` | `HelperFailingPayload(HelperId, Name, Failures, LastError)` | Helpers |

Both are posted with the actor `FeedActor.Huddle`: Huddle paused the Helper, and Huddle noticed the
failures. `Name` is the Helper's `DisplayName` at the time. Each pairs with exactly one payload record
in `FeedPayloads.TypeOf`, and `FeedPayloads.Matches` is extended with both.

**`HelperFeed`** (new, `Helpers/HelperFeed.cs`, a singleton taking `IFeedPublisher`,
`HelperStatusStore`, `HelperBudget` and `TimeProvider`) owns when to post and when to resolve, so the
dispatcher stays a dispatcher:

```csharp
/// <summary>Tells the Feed about one Helper's health (Helpers in Feed spec §5.3). Never throws.</summary>
internal sealed class HelperFeed
{
    public void Paused(IHelperAgent helper, HelperSettings settings);   // an OverBudget run
    public void Continued(HelperId id);                                 // Continue was pressed
    public void Ran(IHelperAgent helper, HelperRunKind kind, HelperStatus status); // after RecordSuccess / RecordFailure
    public void TurnedOff(HelperId id);                                 // the Helper was switched off in Settings
}
```

| Event | Where it is called | Effect |
| --- | --- | --- |
| A run ends `OverBudget` | `HelperDispatcher.JudgeAsync`, the `default` arm of the run-kind switch (today it does nothing) | Post `helper-paused`, **once per pause**: only when no `helper-paused` for this Helper is unresolved (an in-memory set, seeded at startup from `FeedStore.Unresolved`) |
| Continue | `HelperCard.razor`, after `Budget.Grant` | Resolve `helper-paused` |
| The first run of a new day that is not `OverBudget` | `HelperFeed.Ran` | Resolve `helper-paused`: the cap reset at midnight |
| A failure makes `ConsecutiveFailures` reach the threshold (3) | `HelperFeed.Ran`, after `RecordFailure` | Post `helper-failing`, once per streak (on reaching 3, not on 4 or 5) |
| A good run | `HelperFeed.Ran`, after `RecordSuccess` | Resolve `helper-failing` |
| The Helper is turned off | `HelperCard.razor`, after saving `Enabled = false` | Resolve both: nothing is left to watch |

`HelperStatusStore.RecordFailure` returns the new `HelperStatus` (today `void`), so `Ran` sees the count
the store wrote rather than reading it back. The threshold is `HelperFeed.FailureThreshold = 3`, the
number the Helpers spec §12 named.

**Copy:**

| Source type | Summary (chips in braces) | Status line |
| --- | --- | --- |
| `helper-paused` | `paused {Helper}: its daily cap of <n> calls is reached` · `Agents' Tasks are not judged until you continue or the day ends` | `Paused <hh:mm> · Continue is on the Helper's card` |
| `helper-failing` | `{Helper} failed <n> times in a row` · `<last error, cut to 120 characters>` | `Since <hh:mm> · Tasks are not being judged` |

A resolved one ends `· done <hh:mm>`, as every Notification does. The kind badges are the existing
gauge (paused) and a new `alert` badge (`Icons.Material.Outlined.ErrorOutline`) for failing.

### 5.4 The Helpers Category

- `FeedCategory.Helpers`, wire name `helpers`, label "Helpers", ordered after Gates in
  `FeedCategories.Order`. `FeedCategories.Of` maps `HelperPaused` and `HelperFailing` to it. A
  Helper's `task-gate-on` stays in **Gates**: it is about a Task.
- `FeedCounts` gains `Helpers`; `FeedStore.Counts` gains its `SUM(CASE …)` column. The unit test
  that every `FeedSourceType` is in exactly one type Category now has five Categories.
- The Feed Tab's `SegmentedControl` gets a seventh option, and the panel's chip set gets a
  seventh chip. The header wireframe gains a "Helpers" segment (§6).

### 5.5 The Helper target

- `FeedTargetKind.Helper`; `FeedTarget.Of` maps both new payloads to it, with `Id` the Helper id.
  `Address` is `/settings/helpers#helper-<id>`.
- `HelperCard.razor`'s root gets `id="helper-<id>"`, as `NeedsYouPage.razor` gives each Task entry
  the id that `/needs-you#<id>` names.
- **Expanded card** (Feed spec §6.13): a new `FeedHelperPreview.razor`, read-only. It shows the
  Helper's name and whether it is on; Adapter, Model and Effort; today's usage ("50 of 50 calls,
  12 Tasks not judged"), from `HelperBudget.Today`; the last result and the last error with their
  times, from `HelperStatusStore`. Its one button is **Open Helper settings** (filled), to the
  address above. "In the Feed" lists the Helper's own items (same target).

### 5.6 Huddle posts the Helper's wake-up (ADR-0065)

**The problem.** `TaskTriggerService.ResolveSenderIdAsync` returns `KnownIds.Human` for every actor
that is not an Agent, a Helper included. A Message from the Human counts as the Human's answer:
`NeedsYouRule` treats the Human's last Message as answering every Mention before it, and
`NeedsYouTracker` clears the Room on a Human post. So a Helper's wake-up in a Room with an
unanswered Mention of the Human silently clears it from Needs You, and since slice 1, also resolves
its `room-mention` Notification.

**The change.**

- `KnownIds.Huddle = "huddle"`: a sender id that is neither the Human nor an Agent. There is no
  `User` row and it is never a Room Member.
- `ChatService.PostAsHuddleAsync(string roomId, string text, CancellationToken ct)`: the one way to
  post as Huddle. It goes through `PostCoreAsync` with the membership check skipped **for this sender
  only**; every other sender still needs to be a Member. The Room's post lock, the reference snapshot,
  the Transcript and `RoomEvents` are the same. Two steps are **not** the same (HF-9 finding 1): the Room
  Budget is left alone and the Room's waiting Questions and forms are left alone.
- `TaskTriggerService` posts a wake-up whose actor is a Helper with `PostAsHuddleAsync`, Mentioning
  the assignee as today. The Room choice (`ChooseRoomAsync`) no longer needs the Human as a
  Member for these.
- **What follows from the code as it is,** to be proved by tests:
  - `NeedsYouRule` and `NeedsYouTracker` compare the sender with `KnownIds.Human`, so a Huddle post
    neither answers a Mention nor clears a Room.
  - `PersonaRunner.SenderIsHuman` finds no Member for `huddle`, so the post counts as not-Human:
    it does not reset the Room's Budget or the assignee's token Budget (Helpers spec §10 OD-1 is
    closed by this).
  - The Helper's wake-up still uses none of the Task's Agent wake budget (slice 2's rule).
- **HF-9 findings** (the spike over every reader of `ChatMessage.SenderId` and `SenderName`, run on
  `main` at `a9a0b274`; 57 occurrences in `src`). The ones that assume a Member, a `User` or "not an
  Agent means the Human" and so need a change in HF-10 or HF-11:
  1. **`ChatService.PostCoreAsync`** reads "not an Agent" as "the Human": `SpendBudget` resets the
     Room's Budget for any non-Agent sender and `DropWaitingCards` drops the Room's Questions and
     forms for one. A Huddle post would do both. HF-10 gives the core an explicit sender kind (Human,
     Agent or Huddle) so a Huddle post spends nothing, resets nothing and drops nothing.
  2. **`NeedsYouTracker.OnMessagePosted`** sends every non-Human post down the branch that can set
     a Mention or Budget reason. HF-10 returns early for the Huddle sender: Needs You neither clears
     nor sets on a Huddle post (`NeedsYouRule` and `NeedsYouStartupScan` compare with `KnownIds.Human`
     only and need no change).
  3. **`MessageList.razor`** draws a Task wake-up as the muted system row only inside its Human
     branch, so a Huddle wake-up would fall into the Agent branch with an initials avatar and the
     agent-facing "Call get_task" line. HF-10 draws a Huddle-posted wake-up as the same system row and
     any other Huddle Message as an ordinary row with the Huddle mark (`AutoAwesome`, as the Feed
     draws Huddle) and the name "Huddle".
  4. **`ReadRecordsTool`** refuses an unscoped Room that holds a sender who is neither an Agent
     nor the Human ("participants outside Team"). A Huddle Message is not a participant, so HF-10
     exempts `KnownIds.Huddle` beside the Human.
  5. **`TaskTriggerService`**: `ResolveSenderIdAsync` returns the Human for every non-Agent;
     `ChooseRoomAsync` and `FindUsableOriginAsync` require the sender to be a Member and add the
     sender to the fallback member set. HF-11 resolves a Helper's actor to `KnownIds.Huddle` and, for that
     sender, accepts the origin Room without a sender seat and leaves `KnownIds.Huddle` out of every
     member set.

  Readers that need **no change**, each pinned by a test in HF-10: `MessagePostedEvent.IsRecipient`
  and `AgentGateway.DeliverAsync` (every Agent but the sender receives it), `ReplyGate.Decide` (it
  takes only numbers and a Mention flag, never the sender), `RoomReplyResolver` and `Chat.razor` (a
  sender with no Member is not Human, so no Room note is drawn), `PersonaRunner.SenderIsHuman` (no
  Member means not Human: no command, no Budget reset), `SenderDisplay` (it rewrites only the Human's
  Name), Transcript Catch-up, prompt rendering, `read_transcript` and Global Search's Message provider
  (they print `SenderName`, "Huddle"), and `RedeliveryFacts` (Continue re-delivers a Huddle Message
  like any other). `PostAsync` with the sender id `huddle` keeps failing `NotMember`, so no pipe
  client, tool or card can post as Huddle: the only callers of `PostAsync` pass an Agent's own id or
  the Human's.

  **Wire.** `ChatMessage.SenderId` and `SenderName` are strings in `Huddle.Contracts`: no type, no
  property and no protocol version changes. A pipe client receives a Message whose sender is absent from
  that delivery's `members` list, as it already can when a sender has left the Room; it must not assume
  the sender is listed.

## 6. Interface and wireframes

- **Feed spec §7** gains the three copy rows of §5.2 and §5.3, and the Category label "Helpers".
- **wireframes.md Figure 13:** `F3a` lists seven segments, and `F10b`'s badge list adds `alert`.
  Figure 13a gains the Helpers segment. No new codes.
- **Helpers card (`G` family):** no visible change except the anchor id.
- **A Room:** a Huddle-posted Message is drawn like other Messages, with "Huddle" and the Huddle
  mark as its sender. It needs no code of its own if `SenderDisplay` is the one place that draws a
  sender (confirmed in HF-9).
- **User Guide:** the Feed section names the Helper items; the Helpers section says the Feed tells
  the Human when a Helper pauses or keeps failing.

## 7. Decisions

| # | Decision | Alternative rejected |
| --- | --- | --- |
| HF-D1 | No separate Verdict item: a Helper's `task-gate-on` carries the reason and is the Feed item (owner, 2026-10-20) | A `helper-verdict` source type beside `task-gate-on` (two rows for one change) |
| HF-D2 | A Helper's gate-on is a Notification, resolved when the Human turns the gate off, decides, the Task waits in Needs You, or it closes (owner, 2026-10-20) | Activity only, like an Agent's |
| HF-D3 | A new Helpers Category holds `helper-paused` and `helper-failing` (owner, 2026-10-20) | Gates; no Category |
| HF-D4 | The Helper's wake-up is posted by Huddle, a sender that is neither the Human nor an Agent, in this slice (owner, 2026-10-20; ADR-0065) | Keep posting as the Human; a separate follow-up |
| HF-D5 | `FeedActorKind.Helper`, and an explicit arm for every `TaskActorKind` in `ActorOf` | Leaving Helpers as "outside Huddle" |
| HF-D6 | `HelperFeed` decides when to post and resolve; the dispatcher and the card only call it | Feed calls spread through the dispatcher, the budget and the card |
| HF-D7 | One `helper-paused` per pause and one `helper-failing` per streak, kept by an in-memory set seeded from `FeedStore.Unresolved` at startup | A post on every skipped save or every failure |
| HF-D8 | No Feed item for a Verdict that changes nothing | An Activity item for every judgement |
| HF-D9 | A wake-up after an edit made outside Huddle also moves to the Huddle sender, in a follow-up after this slice (owner, 2026-10-20) | Keep posting outside edits as the Human |
| HF-D10 | A Helper's gate-on Notification can be dismissed with the panel's ✕ before the Human decides, like every Notification; it stays in the Feed until resolved (owner, 2026-10-20) | Undismissable until decided |
| HF-D11 | The failure threshold stays a constant, 3, not a setting (owner, 2026-10-20) | A `Team:Helpers` setting |

## 8. Open decisions

None. The three this slice raised were answered by the owner on 2026-10-20 and are HF-D9 to HF-D11 in §7.

## 9. Testing

Functional tests per [agents/Testing.md](../agents/Testing.md), each with a `///` summary.

| Area | What the tests pin |
| --- | --- |
| `TaskFeedRules` | A Helper's gate-on is a Notification with the reason; an Agent's is still Activity; each of the four resolution conditions; `Holds` at startup; every `TaskActorKind` has an explicit arm |
| `FeedPayloads` and the envelope | `helper` round-trips; an old row without `Reason` reads `null`; both new payloads round-trip and match their source types |
| `FeedCategories` and `FeedStore.Counts` | Every source type is in exactly one of five type Categories; the Helpers count equals the Helpers list |
| `HelperFeed` | One pause item per pause, resolved by Continue, by a new day's first run, and by turning the Helper off; one failing item per streak (at 3, not 4), resolved by the next good run; seeded from unresolved items after a restart; a Feed failure never escapes |
| `HelperDispatcher` | An `OverBudget` run calls `HelperFeed.Paused`; a run's status reaches `HelperFeed.Ran` |
| Huddle sender | `PostAsHuddleAsync` posts without membership; any other non-Member is still refused; a Huddle post leaves an unanswered Mention in Needs You and does not resolve its `room-mention` item; it does not reset a Room's Budget; the assignee is woken and replies |
| Components | The Helper avatar, the alert badge, the seventh segment and chip, `FeedHelperPreview`, the Helper line in `FeedTaskPreview`, the card's anchor |

Manual tests: add a "Helpers in the Feed" block to `engineering/manual-tests/feed.md` and a paused
and failing case to the Helpers manual page. The paid ones (a real gate-on and a real failure) are
marked as such.

## 10. Files touched

| File | Change |
| --- | --- |
| `Feed/FeedActorKind.cs`, `Feed/FeedPayloads.cs` | `Helper` and its wire name |
| `Feed/FeedSourceType.cs`, `Feed/FeedSourceTypes.cs`, `Feed/FeedPayloads.cs` | Two source types, wire names, payload pairing |
| `Feed/HelperPausedPayload.cs`, `Feed/HelperFailingPayload.cs` (new) | The payloads |
| `Feed/TaskGatePayload.cs` | `Reason` |
| `Feed/FeedCategory.cs`, `Feed/FeedCategories.cs`, `Feed/FeedCounts.cs`, `Feed/FeedStore.cs` | The Helpers Category and its count |
| `Tasks/TaskChange.cs`, `Tasks/TaskService.cs` | `TaskChange.Reason`, filled from the patch |
| `Tasks/TaskFeedRules.cs` | Actor mapping, Notification and resolutions, `Holds` |
| `Helpers/HelperFeed.cs` (new), `Helpers/HelperDispatcher.cs`, `Data/HelperStatusStore.cs` | Posting and resolving Helper health |
| `Components/Settings/Helpers/HelperCard.razor`, `HelpersPanel.razor` | Continue and turn-off hooks, anchor and scroll |
| `Components/Feed/FeedTarget.cs`, `FeedTargetKind.cs`, `FeedSummary.cs`, `FeedBadge.cs`, `FeedStatusLine.cs`, `FeedCard.razor`, `FeedItemRow.razor`, `FeedTaskPreview.razor`, `FeedHelperPreview.razor` (new), the Category control and the panel chips | Drawing |
| `Data/KnownIds.cs`, `Services/ChatService.cs`, `Services/SenderDisplay.cs`, `Tasks/TaskTriggerService.cs`, and whatever HF-9 finds | The Huddle sender |
| `docs/Huddle.Feed-Specifications.md` §6.3, §6.9, §7; `docs/Huddle.Helpers-Specifications.md` §12; `docs/engineering/wireframes.md`; `docs/Huddle.UserGuide.md`; manual test pages | Docs, in the same commits as the code |

---

## Appendix A. Tasks, in order, test first

Each task is one commit: a failing test, the code, a green `dotnet build Huddle.slnx` and
`dotnet test Huddle.slnx --`.

| # | Task | Tests |
| --- | --- | --- |
| HF-1 | `FeedActorKind.Helper`, wire name, `ActorOf` with explicit arms | Envelope round-trip; a Helper's change posts with the Helper kind |
| HF-2 | `TaskChange.Reason` and `TaskGatePayload.Reason`; `TaskFeedRules`: Notification for a Helper's gate-on, the four resolutions, `Holds` | §9 `TaskFeedRules` row |
| HF-3 | `HelperPaused`, `HelperFailing`: source types, wire names, payloads, `Matches` | §9 `FeedPayloads` row |
| HF-4 | `FeedCategory.Helpers`, `FeedCategories`, `FeedCounts`, `FeedStore.Counts` | §9 Categories row |
| HF-5 | `HelperStatusStore.RecordFailure` returns the new status; `HelperFeed` | §9 `HelperFeed` row |
| HF-6 | `HelperDispatcher` calls `HelperFeed`; `HelperCard` calls it on Continue and on turning off | §9 `HelperDispatcher` row; card tests |
| HF-7 | Startup: `HelperFeed` seeds its sets and resolves items whose condition ended while Huddle was stopped | Restart cases of §9 `HelperFeed` |
| HF-8 | Drawing: Helper avatar, alert badge, copy, `FeedTarget.Helper`, `FeedHelperPreview`, the Helper line in `FeedTaskPreview`, seventh segment and chip, the card anchor | §9 Components row |
| HF-9 | **Spike, no product code:** every reader of `SenderId`/`SenderName` that assumes a Member or a `User`, and the `Huddle.Contracts` wire types; write the list into §5.6 | None; the list in §5.6 |
| HF-10 | `KnownIds.Huddle`, `ChatService.PostAsHuddleAsync`, `SenderDisplay`, and the fixes HF-9 listed | §9 Huddle sender row |
| HF-11 | `TaskTriggerService` posts a Helper's wake-up as Huddle | §9 Huddle sender row, end to end |
| HF-12 | Docs: Feed and Helpers specs, wireframes, User Guide, manual tests | `Check-All.ps1` |

---

## Deviations from this design

What differs between the text above and the code as built. The code is right where they disagree.

| # | Spec | As built | Why |
| --- | --- | --- | --- |
| B-1 | §5.1: an explicit `TaskActorKind.OutsideHuddle` arm so a new `TaskActorKind` fails the build (CS8509) | `TaskFeedRules.ActorOf` has the explicit arms **and** a closing `_ => throw`; `TaskFeedRulesTests.For_EachActorKind_PostsItsOwnFeedActorKind` walks `Enum.GetValues<TaskActorKind>()` | A switch over an enum without a `_` arm is CS8524 for the unnamed values, an error here, so the compiler can't be the guard; the test is |
| B-2 | §5.3: `HelperFeed` takes `IFeedPublisher`, `HelperStatusStore`, `HelperBudget`, `TimeProvider` | Takes `IFeedPublisher`, `HelperStatusStore`, `HelperBudget`, `HelperRegistry`, `HelperSettingsResolver`, `ILogger`; no clock. `HelperFeed.Reconcile(FeedStore)` seeds the open sets, run once by `HelperFeedStartup` (registered with the Feed) | The reconciler needs a Helper's effective settings and the registry; nothing reads the time |
| B-3 | §5.3: payloads carry a `HelperId` | The payloads carry the id as a `string` | A `readonly record struct` in a JSON payload would need a converter for no gain |
| B-4 | §5.3: `HelperStatusStore.RecordFailure` returns the new status | Same, written with `RETURNING` so the count is the one this write produced. The dispatcher assigns it **before** calling `?.Ran`, because a null-conditional call skips its arguments | A first version put the call in the argument and recorded no failure when no `HelperFeed` was given |
| B-5 | §5.5: the Helper preview shows "50 of 50 calls, 12 Tasks not judged" | `FeedHelperPreview` reuses `HelpersCopy`: `50 of 50 calls today · 12 skipped while paused`, plus a "Runs on" line and the last run and last error with their times | One wording for the Helpers card and the Feed |
| B-6 | §5.6: "every other step is the same" for `PostAsHuddleAsync` | Two steps differ: no Budget spend or reset and no dropped Question or form (HF-9 finding 1) | `PostCoreAsync` read "not an Agent" as "the Human" |
| B-7 | §5.6 listed `SenderDisplay` as the place that draws a sender | `MessageList.razor` draws it; `SenderDisplay` only renames the Human. A Huddle wake-up is the muted system row and any other Huddle Message wears `.huddle-mark` | HF-9 finding 3 |
| B-8 | §5.4: the pill and pause icon | A `helper-paused` card wears the gauge badge, but its pill is the ordinary "Waiting for you"; only `room-budget` draws the pause icon in the pill | Not worth a second rule |
| B-9 | The Helpers anchor "and scroll" | `HelpersPanel` scrolls once, after the first render, with the existing `intoView` in `scroll.js` | Same helper as the Prompts and Packs highlights |
