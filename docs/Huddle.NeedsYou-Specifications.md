# Huddle.NeedsYou — Design Specification

**Date:** 2026-10-12 · **Status:** Designed, not built · **Decision record:**
[ADR-0050](adr/0050-needs-you-is-derived-from-room-events-and-held-in-memory.md) · **Vocabulary:**
[language.md](engineering/language.md) (**Needs You**)

This is the design for **Needs You**: a card in the left sidebar that lists every Room
waiting on the Human. A Room joins the list when an Agent Mentions the Human, asks a
Question, opens a Form or runs out of Budget. It leaves the list when the Human posts
in it or dismisses it. Opening the Room does not clear it.

It is written for the engineers or agents building it, with no memory of the conversation
that produced it. Read §5 for the shape, §6 for the subsystems, and Appendix A for the
ordered, test-first task list. Every design decision is recorded in §9 with the
alternative it beat.

> [!IMPORTANT]
> Two pages are binding before any code in this spec is written:
> [rules.md](engineering/rules.md) before editing `src/Huddle.App`, and
> [agents/CSharpPrinciples.md](../agents/CSharpPrinciples.md) for every C# file. Nothing here
> overrides either.

---

## 1. Goal

The Human runs several Rooms at once, and Agents work in them unattended. Today the only
way to find out that one of them is blocked on the Human is to open each Room and read it.
Needs You puts those Rooms in one place.

Concretely:

1. **One list, across Rooms.** A Room appears once, however many reasons it has.
2. **Four reasons.** An Agent Mentioned the Human. A Question card is waiting. A Form is
   waiting. The Room's Budget is spent.
3. **The Human decides when it is handled.** Posting in the Room or pressing ✕ removes it.
   Looking at the Room does not.
4. **Survives a restart where it can.** A Mention is worked out again from the Transcript
   at startup. The other three reasons are in memory already and die with the app, so they
   need nothing.
5. **Placed by the layout, not built for it.** The list must move to a future right-hand
   sidebar by moving one line in `MainLayout.razor`.

---

## 2. Example use cases

| # | Situation | What must happen |
| --- | --- | --- |
| N1 | Nova posts *"@You which database should I use?"* in *Backend* | *Backend* appears in Needs You with a Mention reason, "Nova", and the Message time |
| N2 | The Human opens *Backend*, reads it, and leaves without replying | *Backend* stays in the list |
| N3 | The Human replies in *Backend* | *Backend* leaves the list |
| N4 | The Human presses ✕ on *Backend* | *Backend* leaves the list. Nothing is posted, and no Agent is told |
| N5 | After N4, Nova Mentions the Human in *Backend* again | *Backend* comes back |
| N6 | Coach calls `ask_human` in *Fitness* | *Fitness* appears with a Question reason |
| N7 | The Human answers Coach's card | The answer is a Human Message, so *Fitness* leaves the list |
| N8 | Claude opens two Forms in *Docs*; the Human answers one | *Docs* stays, because one Form still waits. Answering the second removes it |
| N9 | *Research* spends its Budget | *Research* appears with a Budget reason |
| N10 | The Human presses **Continue** in *Research* | The Budget reason clears. A Mention reason in the same Room stays, because Continue is not a reply |
| N11 | Nova Mentions the Human in *Backend* and asks a Question there too | One row, showing both reasons |
| N12 | The app restarts with N1 unanswered | *Backend* is back in the list after startup, with the same Message time |
| N13 | The app restarts with N6 unanswered | *Fitness* is not in the list: the Question card itself was lost (ADR-0022) |
| N14 | The Human archives *Backend* while it is listed | *Backend* disappears from the card. It reappears if the Room is unarchived and still waiting |
| N15 | The Human deletes *Backend* | Its entry is removed for good |
| N16 | The Human renames *Backend* to *API* | The row reads *API* |
| N17 | Nova replies in a one-to-one Room without Mentioning the Human | Nothing is listed. Only the four reasons count |
| N18 | Two browser tabs are open | Both cards show the same list. ✕ in one removes the row in both |
| N19 | No Room needs the Human | The card is not drawn at all |

---

## 3. Non-goals

| Not in scope | Why |
| --- | --- |
| **Unread counts or "seen" state** | A different feature with a different rule. Needs You is about who is blocked on the Human, not what the Human has read |
| **Every Agent reply in a one-to-one Room** | Every Agent Message there is addressed to the Human, so every one-to-one Room would be listed all the time. A Mention is the Agent saying "this needs you" |
| **Persisting the list or a dismissal** | The Mention reason can be worked out again from the Transcript, and the other three die with their stores. A dismissal lost on restart only makes a Room come back, which is the safe direction |
| **Browser or OS notifications, sounds, a tab-title badge** | Possible later on top of the tracker's `Changed` event. V1 is the card |
| **A dot on the Room's row in the Chats list** | It costs one more subscriber of the tracker, nothing more. Left out of V1 to keep the change small. §6.5 says how to add it |
| **The right-hand sidebar itself** | Only the component split that lets the card move there (§6.4) |
| **A Mention more than 50 Messages back at startup** | The startup scan reads the end of each Transcript (§6.3). Anything older than that window is accepted as stale |

---

## 4. Design principles

1. **The tracker observes; nothing reports to it.** `ChatService`, `QuestionStore` and
   `ElicitationStore` do not change to feed it. It subscribes to events they already raise.
   Delete the tracker and nothing else notices.
2. **One rule, two callers.** Whether a Mention is still unanswered is a pure function over
   Messages. The live path and the startup scan both call it, so they cannot disagree.
3. **Keyed by Room id, never by Name.** Rooms are renamed, and a stored Name goes stale.
4. **The tracker knows nothing about where it is drawn.** The service has no UI. The list
   does not know which sidebar holds it, and the card does not know the list's rules.
5. **Archiving stays a display filter.** The tracker keeps tracking an Archived Room. Only
   the list hides it, the same way `RoomList` does (rules.md, "Archiving a Room is a display
   filter").

---

## 5. Architecture overview

```text
 RoomEvents.MessagePosted ───────┐
   Agent Message mentions Human → set Mention
   Agent Message, Budget spent  → set Budget
   Human Message                → clear Mention, Budget
 RoomEvents.MessageRedelivered ──┤  (Continue granted)  → clear Budget
 RoomEvents.QuestionsChanged ────┤  QuestionStore.Get(roomId)    → set or clear Question
 RoomEvents.ElicitationsChanged ─┤  ElicitationStore.Get(roomId) → set or clear Form
 RoomEvents.RoomsChanged ────────┤  a Room no longer exists      → remove
                                 ▼
                     NeedsYouTracker (singleton, in memory)
                       Snapshot() : IReadOnlyList<NeedsYouEntry>
                       Dismiss(roomId)
                       event Changed
                                 ▲                       │
 NeedsYouStartupScan ────────────┘                       ▼
 (hosted, runs once in the background)      NeedsYouList.razor  (rows, ✕, opens the Room)
   reads the end of each Transcript,              ▲
   asks NeedsYouRule, Seed(...)             NeedsYouCard.razor  (left-sidebar chrome)
                                                  ▲
                                            MainLayout.razor: <NeedsYouCard /> above <RoomList />
```

| Component | Kind | New or changed |
| --- | --- | --- |
| `NeedsYou/NeedsYouReason.cs` | `[Flags]` enum: `Mention`, `Question`, `Form`, `Budget` | New |
| `NeedsYou/NeedsYouEntry.cs` | Record: `RoomId`, `Reasons`, `Since`, `AskedBy` | New |
| `NeedsYou/NeedsYouRule.cs` | Static, pure | New |
| `NeedsYou/NeedsYouTracker.cs` | Singleton, in memory | New |
| `NeedsYou/NeedsYouStartupScan.cs` | `BackgroundService`, runs once | New |
| `Components/NeedsYou/NeedsYouList.razor` | Component, no chrome | New |
| `Components/NeedsYou/NeedsYouCard.razor` | Component, the card around the list | New |
| `Data/IChatStore.cs` | Gains `ReadTailAsync` with a default body | Changed |
| `Data/FileChatStore.cs` | Overrides `ReadTailAsync` to read from the end of the file | Changed |
| `Services/RoomEvents.cs` | The `MessageRedelivered` comment names the tracker as an allowed subscriber | Changed (comment only) |
| `ServiceCollectionExtensions.cs` | Registers the tracker and the scan | Changed |
| `Components/Layout/MainLayout.razor` | Places `<NeedsYouCard />` | Changed |

All new code lives in `src/Huddle.App`, in the chat surface's subtree. Nothing crosses the
pipe and no Envelope changes.

---

## 6. Subsystems

### 6.1 The rule

```csharp
internal static class NeedsYouRule
{
    /// The oldest Agent Message that Mentions the Human and comes after the Human's last
    /// Message, or null when no Mention follows the Human's last Message.
    internal static ChatMessage? OldestUnansweredMention(
        IReadOnlyList<ChatMessage> messages,
        Func<ChatMessage, bool> mentionsHuman);
}
```

- Walk `messages` from the end, and stop at the first Message whose `SenderId` is
  `KnownIds.Human`, or at the start of the list. Return the last Message seen on the way for
  which `mentionsHuman` is true, or `null` if there was none.
- It returns the **oldest** unanswered Mention, not the newest, because `Since` and `AskedBy`
  come from it: the Room has been waiting since the first ask.
- `mentionsHuman` is passed in, so the rule needs no Members, no Aliases and no
  `MentionParser`. The live path already has `MessagePostedEvent.Mentions`. The startup scan
  builds the delegate from `MentionParser.Parse(text, members, aliases)`.

### 6.2 The tracker

`NeedsYouTracker` is a singleton holding `Dictionary<string, State>` (ordinal keys) under a
`private readonly Lock gate`. `State` holds the current `NeedsYouReason` flags, `Since`,
`AskedBy`, and a `Dismissed` flag.

| Event | Condition | Effect |
| --- | --- | --- |
| `MessagePosted` | Sender is an Agent and `Mentions` contains `KnownIds.Human` | Add `Mention`. Keep `Since`/`AskedBy` if the Room already had a Mention; otherwise take them from this Message |
| `MessagePosted` | Sender is an Agent and `Budget.Exhausted` | Add `Budget` |
| `MessagePosted` | Sender is the Human (any post, including a Form answer) | Remove `Mention` and `Budget` |
| `MessageRedelivered` | `Budget.Exhausted` is false | Remove `Budget` |
| `QuestionsChanged` | `QuestionStore.Get(roomId)` is not null | Add `Question` |
| `QuestionsChanged` | It is null | Remove `Question` |
| `ElicitationsChanged` | `ElicitationStore.Get(roomId).Count` went up | Add `Form` |
| `ElicitationsChanged` | It is now 0 | Remove `Form` |
| `RoomsChanged` | A tracked Room id is no longer returned by `ITeamDirectory.GetRoomsAsync` | Remove the entry |

Rules that hold across the table:

- **An entry with no reasons is removed.** `Snapshot()` never returns an empty entry.
- **Adding a reason clears `Dismissed`.** Something new happened, so the Room comes back
  (N5). Removing a reason never clears it.
- **`Dismiss(roomId)` sets `Dismissed`.** It removes the `Mention` reason, because the
  tracker owns that one, and hides the entry while the mirrored reasons (`Question`, `Form`,
  `Budget`) are still true in their stores. The Question card or Form in the Room is not
  touched: dismissing the reminder is not answering.
- **A replaced Question card counts as an add.** `QuestionsChanged` fires on a replace, and
  a replace is a new ask. This is why `Question` is set on every non-null `Get`, not only on
  a change from null.
- **`Since` for a mirrored reason** is `PendingQuestions.AskedAt` for a Question, and for a
  Form or a Budget the time the tracker saw it, from the injected `TimeProvider`. `AskedBy` is
  `AskerName` from the card or the oldest Form; for a Budget it is the sender of the Message
  that spent it. When several reasons
  are present, the entry shows the earliest `Since`.
- **`Changed` is raised after the lock is released, never while it is held**, the same
  rule `QuestionStore` follows. It is raised only when `Snapshot()` would return something
  different.
- **Handlers are synchronous.** `RoomsChanged` needs an async directory read, so that one
  handler starts the read with `_ = this.PruneAsync()`, catches and logs its own failure, and
  takes the lock only to apply the result.

`Snapshot()` returns entries that are not `Dismissed`, sorted by `Since`, oldest first:
the Room that has waited longest is at the top.

The tracker is constructed at startup, not on first use, because it must hear events
before any browser is open. `NeedsYouStartupScan` takes it as a constructor dependency,
which is enough to force that.

### 6.3 The startup scan

`NeedsYouStartupScan` is a `BackgroundService`. `ExecuteAsync` runs once and does not block
host startup:

1. Read the Rooms with `ITeamDirectory.GetRoomsAsync`, including Archived ones (principle 5).
2. For each Room, read its Members, then `IChatStore.ReadTailAsync(roomId, 50)`.
3. Call `NeedsYouRule` with a delegate that runs `MentionParser.Parse` against those Members
   and `IMentionAliasSource.Aliases`, and checks for `KnownIds.Human`.
4. For a hit, call `tracker.Seed(roomId, since, askedBy)`.
5. Catch and log a failure per Room, and go on to the next.

`Seed` only adds a `Mention` to a Room the tracker has heard **no live event for** since it
was constructed. A live Message that arrived during the scan is newer than the Transcript the
scan read, so it wins.

`ReadTailAsync(string roomId, int count, CancellationToken ct)` is added to `IChatStore` with
a default body: `ReadAllAsync`, then the last `count`. Three test doubles implement
`IChatStore` and keep working unchanged. `FileChatStore` overrides it by reading the file
backwards in blocks until it has `count` complete lines. Bad lines are skipped, as
`ReadAllAsync` skips them.

Questions, Forms and Budgets are in memory and empty after a restart, so the scan only
rebuilds `Mention`.

### 6.4 The components

The split is what lets the list move to a right-hand sidebar later:

| Component | Owns | Does not know |
| --- | --- | --- |
| `NeedsYouTracker` | Which Rooms wait, why, since when | That a UI exists |
| `NeedsYouList` | Rows, Room Names, ✕, opening the Room, hiding Archived Rooms | Which sidebar holds it, or that it sits in a card |
| `NeedsYouCard` | The left sidebar's chrome: title, count, and not drawing anything when the list is empty | The tracker's rules |

**`NeedsYouList`** has no parameters in V1.

- It subscribes to `NeedsYouTracker.Changed` and `RoomEvents.RoomsChanged` in
  `OnInitialized`, and unsubscribes in `Dispose` (rules.md: every `RoomEvents` subscriber
  unsubscribes).
- It marshals every update through `RendererDispatch.RunAsync(this.InvokeAsync, ...)`, as
  `RoomList` does.
- It reads Room Names and `Archived` from `ITeamDirectory.GetRoomsAsync`, and reloads them
  on `RoomsChanged`.
- It exposes `EventCallback<int> CountChanged`, so whatever holds it can show a count
  without reading the tracker itself.

Each row is:

```text
 [reason icons]  Backend                    ✕
                 Nova · 14:32
```

- **Room Name.** A `NavTabLink` to `/rooms/{id}` with `TabTitle` set to the Room's Name, so
  it opens or focuses the same tab the Chats list would.
- **Reason icons.** `AlternateEmail` for a Mention, `HelpOutline` for a Question,
  `Assignment` for a Form, `PauseCircleOutline` for a Budget. Each one has a `MudTooltip`
  and an `aria-label`, for example "Nova mentioned you".
- **Second line.** `AskedBy` and the time of `Since`. Use `HH:mm` if it was today, otherwise
  `ddd HH:mm`, in the Human's culture: this is text for a person (CSharpPrinciples, culture).
  No relative time, so no timer is needed to keep it fresh.
- **✕.** A small `MudIconButton` with `aria-label="Dismiss"` that calls
  `tracker.Dismiss(roomId)`.

**`NeedsYouCard`** wraps the list in an outlined `MudPaper` with the title "Needs you" and a
count. It does not draw the paper while the count is 0 (N19). Check
[agents/MudBlazorDesign.md](../agents/MudBlazorDesign.md) before choosing between `MudPaper`
and `MudCard`; use whichever the catalogue already uses for a sidebar card.

All styling goes through `var(--mud-palette-*)`. A colour literal fails the build
(rules.md, "A colour or font literal belongs in `MainLayout.razor.css` and nowhere else").
Give the Mention icon `Color.Warning`, so the Human can tell a waiting Room from a sidebar
heading.

**Placement.** `MainLayout.razor` gets `<NeedsYouCard />` as the first child of the
`MudDrawer`, above `<RoomList />`. A future right-hand sidebar moves that line, or wraps
`<NeedsYouList />` in chrome of its own.

### 6.5 Later: a dot in the Chats list

`RoomList` can inject `NeedsYouTracker`, subscribe to `Changed`, and draw a small dot on any
row whose id is in `Snapshot()`. No change to the tracker is needed. This is out of V1 (§3).

---

## 7. Interface copy

| Where | Text |
| --- | --- |
| Card title | Needs you |
| Mention tooltip | {AskedBy} mentioned you |
| Question tooltip | {AskedBy} asked you a question |
| Form tooltip | {AskedBy} is waiting on a form |
| Budget tooltip | Paused: the Room's Budget is spent |
| ✕ button | Dismiss |

The copy addresses the Human as "you" ([language.md](engineering/language.md), **Human**).

---

## 8. Testing

Follow [agents/Testing.md](../agents/Testing.md) and, for the two components,
[agents/BlazorTesting.md](../agents/BlazorTesting.md). Every test has a `///` summary.

| Area | Tests in `tests/Huddle.Tests/NeedsYou/` |
| --- | --- |
| `NeedsYouRule` | No Messages; Human last; one Mention after the Human; two Mentions after the Human, so the older one is returned; a Mention before the Human's last Message is ignored |
| `NeedsYouTracker` | One test per row of the §6.2 table, driven through a real `RoomEvents`; Dismiss hides and a new reason brings the entry back; Dismiss leaves the `QuestionStore` card in place; an entry with no reasons is gone from `Snapshot`; `Changed` is not raised when nothing changed; `Snapshot` order is oldest first |
| `NeedsYouStartupScan` | Seeds a Room whose Transcript ends with an unanswered Mention; skips a Room where the Human spoke last; a live event before `Seed` wins; one Room throwing does not stop the rest |
| `FileChatStore.ReadTailAsync` | Fewer lines than `count`; exactly `count`; a block boundary splitting a line; a bad line skipped; an empty file |
| `NeedsYouList` (bUnit) | Renders a row per entry; ✕ calls `Dismiss`; an Archived Room's row is hidden; a rename updates the row after `RoomsChanged`; `Dispose` unsubscribes from both events |
| `NeedsYouCard` (bUnit) | Not drawn at count 0; drawn with the count otherwise |

Prove each new test can fail before you trust it (Testing.md).

A manual test page, `docs/engineering/manual-tests/needs-you.md`, covers N1–N19 against the
mock adapter (no paid Turns), and gets an Index row in the same commit.

---

## 9. Decisions

| # | Decision | Alternative it beat |
| --- | --- | --- |
| D-1 | A singleton tracker observes `RoomEvents` | Each Room writes itself into the list. There is no Room object to hold the state: a `Room` is a record read fresh from SQLite |
| D-2 | Keyed by Room id | Keyed by Name. Names change on rename |
| D-3 | Four reasons: Mention, Question, Form, Budget | Mention only. The other three block the Human more firmly than a Mention, and they are already tracked |
| D-4 | Opening a Room does not clear it; posting or ✕ does | Clearing on open. The Human may glance at a Room without time to answer |
| D-5 | ✕ hides the entry until a new reason arrives | ✕ removes it for good. A new ask after a dismissal is new information |
| D-6 | ✕ does not touch the Question card or Form | ✕ also drops them. Dismissing a reminder is not answering, and the Agent is still waiting |
| D-7 | Continue clears only the Budget reason | Continue clears everything. Pressing Continue lets the Agents run again; it is not a reply to a Mention |
| D-8 | In memory; the Mention reason is worked out again at startup | A table in `team.db`. The Transcript already holds the truth, and a second copy can disagree with it |
| D-9 | The scan reads the last 50 Messages per Room | Reading the whole Transcript. Startup cost would grow with history; an ask older than 50 Messages without an answer is stale |
| D-10 | The scan runs in the background | Inside `StartAsync`. A large data folder would delay the window opening |
| D-11 | Tracker, list and card are three pieces | One component. A right-hand sidebar is planned; it must reuse the list without the left sidebar's chrome |
| D-12 | Archived Rooms are tracked but hidden by the list | Stop tracking on archive. Archiving is a display filter (rules.md), and unarchiving must bring the entry back |
| D-13 | Oldest first | Newest first. The Room that has waited longest is the most overdue |
| D-14 | The card is not drawn while the list is empty | An empty card reading "Nothing needs you". It costs sidebar space every minute the answer is "nothing" |

---

## 10. Files touched

| File | Owner | Note |
| --- | --- | --- |
| `src/Huddle.App/NeedsYou/*` | chat | New |
| `src/Huddle.App/Components/NeedsYou/*` | chat | New |
| `src/Huddle.App/Data/IChatStore.cs`, `FileChatStore.cs` | chat | `ReadTailAsync` |
| `src/Huddle.App/Services/RoomEvents.cs` | chat | Comment on `MessageRedelivered` |
| `src/Huddle.App/ServiceCollectionExtensions.cs` | chat | Registration |
| `src/Huddle.App/Components/Layout/MainLayout.razor` | chat | Placement |
| `tests/Huddle.Tests/NeedsYou/*` | chat | New |
| `docs/Huddle.UserGuide.md` | chat | A short "Needs you" section (Index: anything a Human sees) |
| `docs/engineering/code-map.md` | chat | The `NeedsYou/` folder |
| `docs/engineering/known-limits.md` | chat | D-9's 50-Message window; a dismissal does not survive a restart |
| `docs/engineering/manual-tests/needs-you.md`, `docs/Index.md` | chat | New page and its row |

Nothing in the ACP effort's subtree changes.

---

## Appendix A. Tasks, in order, test first

Each task is one commit-sized change. Write the failing test, watch it fail, make it pass,
then run `dotnet build Huddle.slnx` and the class's tests.

1. **`NeedsYouReason`, `NeedsYouEntry`, `NeedsYouRule`.** The rule tests in §8 first.
2. **`IChatStore.ReadTailAsync` with its default body**, then the `FileChatStore` override
   and its tests.
3. **`NeedsYouTracker`: the Mention and Human-post rows of §6.2**, with `Snapshot`, `Changed`
   and ordering.
4. **The tracker: Budget and `MessageRedelivered`.** Update the `MessageRedelivered` comment in
   `RoomEvents.cs`: Blazor components must not subscribe, and the tracker may because it draws
   nothing.
5. **The tracker: Question and Form**, from `QuestionStore` and `ElicitationStore`.
6. **The tracker: `Dismiss`, and pruning on `RoomsChanged`.**
7. **`NeedsYouStartupScan` and `Seed`**, then the registration in
   `ServiceCollectionExtensions.cs`. Check that the integration fixture still starts (both
   fixtures pin `DemoAgent:Enabled=false` and `Acp:Enabled=false`; the scan needs neither).
8. **`NeedsYouList`**, with its bUnit tests.
9. **`NeedsYouCard`**, and its placement in `MainLayout.razor`. Run the app and look at it in
   the browser pane, light and dark Theme, because bUnit cannot see layout.
10. **Docs:** UserGuide section, code-map row, known-limits rows, the manual test page and
    its Index row. Run `agents/scripts/Check-All.ps1`.
