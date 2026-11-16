using Agency.Huddle.App.Feed;
using Agency.Huddle.Seeder.Model;

namespace Agency.Huddle.Seeder.Scenarios;

/// <summary>
/// The Feed of the <c>software-co</c> seed (Feed spec §2): what Agents and Huddle did, the asks that are waiting and the asks
/// that came and went. Every unresolved Notification is true of the rest of the seed, because Huddle resolves the ones that are not
/// when it starts: the Tasks assigned to the Human exist, the Launch crew Mention is unanswered, the gated Task carries the Helper's
/// line, and the Helper's usage and status say it is at its cap and failing.
/// Seeder spec SEED-SC-11, SEED-INV-13; Appendix A.2 (Add a Feed item).
/// </summary>
internal static class SoftwareCoFeed
{
    /// <summary>The Helper the Helper items are about.</summary>
    internal const string HelperId = "NeedsHuman";

    /// <summary>The Helper's name as it appears on a Task's timeline.</summary>
    internal const string HelperActor = "Needs Human (Helper)";

    /// <summary>The Feed items, in no particular order: each says how long ago it happened.</summary>
    internal static IReadOnlyList<SeedFeedItem> All { get; } =
    [
        // Just now: a document an Agent edited, and a Helper that has started to fail.
        new(FeedSourceType.DocumentChanged, "Grace", FeedActorKind.Agent, "Platform/Engineering-Handbook.md", 3, Detail: "Added the on-call escalation steps", Extra: "edited"),
        new(FeedSourceType.HelperFailing, "Huddle", FeedActorKind.Huddle, HelperId, 9, Notify: true, Detail: "The adapter isn't signed in"),

        // Owen drafts the whole pricing breakdown: five items that the Feed Tab draws as one grouped card.
        new(FeedSourceType.TaskCreated, "Owen", FeedActorKind.Agent, "GROW-0008", 16),
        new(FeedSourceType.TaskCreated, "Owen", FeedActorKind.Agent, "GROW-0009", 15),
        new(FeedSourceType.TaskCreated, "Owen", FeedActorKind.Agent, "GROW-0010", 14),
        new(FeedSourceType.TaskCreated, "Owen", FeedActorKind.Agent, "GROW-0011", 13),
        new(FeedSourceType.TaskCreated, "Owen", FeedActorKind.Agent, "GROW-0012", 12),

        // Asks that are waiting for the Human.
        new(FeedSourceType.TaskAssignedToYou, "Grace", FeedActorKind.Agent, "PLAT-0012", 22, Notify: true),
        new(FeedSourceType.RoomMention, "Owen", FeedActorKind.Agent, "launch-crew", 31, Notify: true),
        new(FeedSourceType.TaskGateOn, SoftwareCoFeed.HelperActor, FeedActorKind.Helper, "SUPP-0008", 38, Notify: true, Detail: "Sends a follow-up to customers outside the Team"),
        new(FeedSourceType.HelperPaused, "Huddle", FeedActorKind.Huddle, HelperId, 47, Notify: true),
        new(FeedSourceType.TaskAssignedToYou, "Owen", FeedActorKind.Agent, "GROW-0013", 55, Notify: true),

        // Asks that came and went: the item stays, dimmed.
        new(FeedSourceType.TaskNeedsYou, "Maya", FeedActorKind.Agent, "GROW-0011", 70, Notify: true, ResolvedMinutesAgo: 52),
        new(FeedSourceType.GateDecisionDue, "Marcus", FeedActorKind.Agent, "PLAT-0007", 85, Notify: true, ResolvedMinutesAgo: 80, Detail: "Done needs a passing test run in the Task's Evidence.", Extra: "qa-evidence-before-done"),
        new(FeedSourceType.TaskGateOn, "Maya", FeedActorKind.Agent, "GROW-0012", 92, Detail: "Publish the pricing page"),
        new(FeedSourceType.RoomBudget, "Huddle", FeedActorKind.Huddle, "incident-0412", 120, Notify: true, ResolvedMinutesAgo: 104),
        new(FeedSourceType.HelperFailing, "Huddle", FeedActorKind.Huddle, HelperId, 600, Notify: true, ResolvedMinutesAgo: 560, Detail: "Timed out after 60 s"),

        // What happened in Rooms and documents earlier.
        new(FeedSourceType.RoomCreated, "Marcus", FeedActorKind.Agent, "chat-platform-pagination", 150),
        new(FeedSourceType.RoomJoined, "Lena", FeedActorKind.Agent, "chat-platform-pagination", 148),
        new(FeedSourceType.RoomArchived, "You", FeedActorKind.Human, "q2-retro", 200),
        new(FeedSourceType.DocumentChanged, "Alan", FeedActorKind.Agent, "Platform/API v2/Pagination.md", 260, Detail: "Checked the maximum page size against the gateway", Extra: "reviewed"),
        new(FeedSourceType.TaskCreated, "Marcus", FeedActorKind.Agent, "PLAT-0005", 300),
        new(FeedSourceType.TaskCreated, "Marcus", FeedActorKind.Agent, "PLAT-0002", 310),
        new(FeedSourceType.RequestAwaitsYou, "Dana", FeedActorKind.Agent, "PLAT-0018", 340, Notify: true, ResolvedMinutesAgo: 330, Extra: "Support"),
    ];
}
