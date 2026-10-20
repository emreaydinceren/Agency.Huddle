---
status: proposed
date: 2026-10-20
---

# The Feed is a stored history that each domain component posts to

The **Feed** is one stored list of what Teammates and Huddle did. It is shown two ways: the
**Notifications** button in the Top Bar, which opens a panel in the Right sidebar, and the **Feed**
Tab, the full timeline. Each item is posted by the component that already knows the fact. The full
design is [Huddle.Feed-Specifications.md](../Huddle.Feed-Specifications.md).

## The problem

Needs You ([ADR-0050](0050-needs-you-is-derived-from-room-events-and-held-in-memory.md)) says which
Rooms and Tasks wait on the Human now, and forgets a Room once it clears. Nothing records what
happened while the Human was away: which Tasks were created, which Rooms started, which documents
changed, which asks came and went. A Task assigned to the Human, or a gate that refused a Teammate's
move and now needs the Human's call, shows nowhere the Human looks first.

## The decision

**One table, `feed_events` in `team.db`, written only by a background writer.** Columns: `id`
(autoincrement), `created_at`, `source_type`, `target_key` (the stable key of
[ADR-0055](0055-a-reference-is-text-with-a-stable-key.md): a Task id, a Room id, a Library file's
absolute path), `actor`, `notify`, `seen_at`, `dismissed_at`, `resolved_at` and a JSON `payload`.
A row with `notify = 1` is a **Notification** and also appears in the panel; the three state
columns apply only to those rows. Every other row is **Activity**. Plain `Microsoft.Data.Sqlite`,
as `CadenceStateStore` uses.

**Push, never infer.** Producers call one narrow interface:

```csharp
internal interface IFeedPublisher
{
    void Post(FeedMessage message);
    void Resolve(string targetKey, FeedSourceType type);
}
```

`FeedMessage(SourceType, TargetKey, Actor, Notify, Payload)` carries one typed payload record per
source type. **The sender decides `Notify`, and calls `Resolve` when its own condition clears.**
The Feed subscribes to no event and works out nothing. In slice 1 the producers are `TaskService`
(Task created, Human gate turned on by a non-Human, Needs You entered, assigned to the Human, a
Request only the Human can close, a process gate's refusal of an Agent), `NeedsYouTracker` (the
four reasons, posted on arrival and resolved on leaving), `ChatService` and `TeamRoomReconciler`
(Room created, Member joined, Room archived) and `DocHistoryStore` (an Agent's History entry).
Helpers will post through the same interface in a later slice.

**`Post` and `Resolve` only enqueue.** A bounded channel feeds one `FeedWriter`, which applies
commands in order in one transaction per batch and then raises an in-memory hub, `FeedEvents`, in
the shape of `TaskEvents`. Producers call after their own write and outside their own locks. A full
queue, a database error or a bad payload is logged and dropped. **A Feed failure never fails or
slows the domain operation.**

**One stored row per event, no Message text.** One row per event; the list may draw neighbouring
rows of one actor, kind and Team as one grouped line (amended 2026-10-20), but nothing is merged in
storage. A Room item names the Room and what happened,
never what anyone wrote.

**Seen, dismissed, resolved and cleaned up.** Opening the panel sets `seen_at`; the unseen count is
the badge. Dismiss sets `dismissed_at` and hides the row from the panel only. A resolved
Notification stays, dimmed, and leaves the count. A dismissed Notification is deleted 30 days after
its dismissal and an Activity row 30 days after its creation, both `Team:Feed` settings.

**The Feed Tab is read-only in v1.** It pages by `id`, not by offset, so new rows at the top do
not shift pages; they wait behind a "↑ N new items" pill unless the list is at its top (amended
2026-10-20). The Right sidebar panel shows the same Feed, opening on Needs you. A click opens the target in a Tab; an avatar or a Team chip filters.

## Why a table, when ADR-0050 rejected one

ADR-0050 refused a table for Needs You because it would be a second copy of what the Transcript says
about **who is waiting now**. The Feed answers a different question, **what happened**, and that
answer is in no other store once a Room clears. Needs You keeps deciding; the Feed only records the
start and the end its producer reports. A row can outlive its condition across a restart (a Question
card is lost when Huddle stops), so each producer resolves its own stale rows at startup.

## Rejected

| Alternative | Why not |
| --- | --- |
| The Feed subscribes to `RoomEvents` and `TaskEvents` and decides what each means | It would repeat the rules of Needs You, the Human gate and Requests in a second place, and drift from them. The component that decides is the one that knows |
| Producers insert directly | A locked or slow database would slow a Task write or a Message post, and its exceptions would reach domain code |
| Replace Needs You with the Feed's Notifications | Needs You is live state rebuilt from the Transcript and the Task files. A stored list can disagree with it (ADR-0050) |
| Merge repeated items into one stored row ("Nova created 15 Tasks") | Owner decision. Merging hides which item is which from counts, filters and `Resolve`; grouping is done only when drawn |
| Page by offset | A live insert at the top would shift every later page and repeat an item |
| A column per payload field | Each source type has different fields. A typed record in a JSON column keeps the table fixed |
| A separate `feed.db` | `team.db` already holds Huddle's own state in WAL mode ([ADR-0042](0042-cadence-state-is-persisted-so-a-restart-does-not-double-fire.md)) |
| Accept, Reply or Decline from a Feed item now | ADR-0061 binds Accept to a hash of what the Human saw. An honest inline Accept means expanding the whole decision card; v2 |

## Consequences

- **Producers gain an optional dependency.** `TaskService`, `NeedsYouTracker`, `ChatService`,
  `TeamRoomReconciler` and `DocHistoryStore` take `IFeedPublisher? feed = null`. Existing tests
  that build them compile and post nothing.
- **`ChatService`'s Room methods gain an actor parameter,** because only the caller knows who
  created, invited or archived. Every caller supplies it.
- **`TaskService` reports a refusal it never wrote.** `UpdateCore` hands back the refused move, so a
  process gate's refusal of an Agent can post "the Human's call is due".
- **The Right sidebar gains a second mode.** It draws Notifications whatever page is focused, and
  draws Subject panels as before otherwise ([Right sidebar spec](../Huddle.RightSidebar-Specifications.md) D-1 holds for Subject mode).
- **Needs You is unchanged** apart from posting and resolving. Its list, its card and its restart
  behaviour stay as ADR-0050 and [ADR-0061](0061-a-human-gated-task-waits-in-needs-you-for-the-humans-acceptance.md) say.
- **The table grows only by settings.** Undismissed Notifications are kept until dismissed; the
  specification lists a retention for resolved ones as an open decision.
- **Names are stored as they were.** A renamed Teammate's old items keep the old Name, as Document
  History does ([ADR-0060](0060-a-documents-history-is-what-its-editors-report.md)).
- **Nothing removes a Member from a Room today,** so "a Teammate left" has no producer in slice 1.
