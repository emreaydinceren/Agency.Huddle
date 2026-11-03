using System.Globalization;
using Agency.Huddle.App;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Feed;
using Agency.Huddle.App.Helpers;
using Agency.Huddle.Seeder.Model;
using Agency.Huddle.Seeder.Scenarios;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.Seeder.Writing;

/// <summary>
/// Stores the plan's Feed through Huddle's own <see cref="FeedStore"/> (so the schema stays the app's business), and gives the Helper
/// the usage and status its Feed items describe: at its daily cap, and failing.
/// Seeder spec §5, step 4; SEED-INV-13.
/// </summary>
internal static class FeedSeeder
{
    private const int HelperCap = 50;

    /// <summary>When the seeded Feed's newest item happened: the run's own time, or three in the afternoon of a pinned <c>--today</c>.</summary>
    /// <param name="plan">The plan, which holds the run date.</param>
    /// <param name="clock">The clock.</param>
    /// <returns>The instant every item's <c>MinutesAgo</c> is measured from.</returns>
    internal static DateTimeOffset SeedTime(SeedPlan plan, TimeProvider clock)
    {
        DateTimeOffset now = clock.GetUtcNow();
        return DateOnly.FromDateTime(now.UtcDateTime) == plan.Today
            ? now
            : new DateTimeOffset(plan.Today.ToDateTime(new TimeOnly(15, 0)), TimeSpan.Zero);
    }

    /// <summary>Writes the Feed items and the Helper's state. A plan with no Feed writes nothing.</summary>
    /// <param name="plan">What to create.</param>
    /// <param name="dataDir">The data folder, which already holds <c>team.db</c>.</param>
    /// <param name="ids">The Room ids the database assigned.</param>
    /// <param name="clock">The clock the seed time comes from.</param>
    internal static void Write(SeedPlan plan, string dataDir, DatabaseResult ids, TimeProvider clock)
    {
        if (plan.Feed.Count == 0)
        {
            return;
        }

        IOptions<TeamOptions> options = Options.Create(new TeamOptions { DataDir = dataDir, HumanName = plan.HumanName });
        DateTimeOffset seedTime = SeedTime(plan, clock);
        try
        {
            WriteFeed(plan, dataDir, ids, options, seedTime);
            WriteHelperState(options, seedTime, clock);
        }
        finally
        {
            SqliteConnection.ClearPool(new SqliteConnection($"Data Source={Path.Combine(dataDir, "team.db")}"));
        }
    }

    private static void WriteFeed(SeedPlan plan, string dataDir, DatabaseResult ids, IOptions<TeamOptions> options, DateTimeOffset seedTime)
    {
        // Chronological, so a Notification resolved before a newer one of the same type and target was posted resolves only the older.
        List<(DateTimeOffset At, FeedCommand Command)> commands = [];
        foreach (SeedFeedItem item in plan.Feed)
        {
            FeedMessage message = Message(plan, item, dataDir, ids);
            commands.Add((seedTime.AddMinutes(-item.MinutesAgo), new PostCommand(message, seedTime.AddMinutes(-item.MinutesAgo))));
            if (item.ResolvedMinutesAgo is { } resolved)
            {
                DateTimeOffset at = seedTime.AddMinutes(-resolved);
                commands.Add((at, new ResolveCommand(message.TargetKey, item.Type, at)));
            }
        }

        FeedStore store = new(options);
        FeedApplyResult result = store.Apply([.. commands.OrderBy(static entry => entry.At).Select(static entry => entry.Command)]);

        // The Feed Tab marks what it has shown as seen: leave the newest eight unseen, so the bell has a count and the cards show their bar.
        IReadOnlyList<FeedItem> added = result.Added;
        if (added.Count > 8)
        {
            _ = store.Apply([new MarkTabSeenCommand(added[^9].Id, seedTime)]);
        }
    }

    private static FeedMessage Message(SeedPlan plan, SeedFeedItem item, string dataDir, DatabaseResult ids)
    {
        FeedActor actor = new(item.ActorKind, item.Actor);
        (string target, FeedPayload payload) = item.Type switch
        {
            FeedSourceType.DocumentChanged => Document(item, dataDir),
            FeedSourceType.HelperPaused => (HelperTarget(item), new HelperPausedPayload(item.Target, "Needs Human", HelperCap, 0)),
            FeedSourceType.HelperFailing => (HelperTarget(item), new HelperFailingPayload(item.Target, "Needs Human", 3, item.Detail ?? string.Empty)),
            FeedSourceType.RoomMention or FeedSourceType.RoomQuestion or FeedSourceType.RoomForm or FeedSourceType.RoomBudget or FeedSourceType.RoomJob
                or FeedSourceType.RoomCreated or FeedSourceType.RoomJoined or FeedSourceType.RoomArchived => Room(plan, item, ids),
            _ => TaskPayload(plan, item),
        };

        return new FeedMessage(item.Type, target, actor, item.Notify, payload);
    }

    private static string HelperTarget(SeedFeedItem item) => HelperFeed.TargetOf(new HelperId(item.Target));

    private static (string Target, FeedPayload Payload) Document(SeedFeedItem item, string dataDir)
    {
        string path = Path.GetFullPath(Path.Combine(dataDir, "Teams", item.Target));
        return (path, new DocumentChangedPayload(path, "Teams", item.Target, item.Extra ?? "edited", item.Detail ?? string.Empty));
    }

    private static (string Target, FeedPayload Payload) Room(SeedPlan plan, SeedFeedItem item, DatabaseResult ids)
    {
        SeedRoom room = plan.Rooms.First(r => string.Equals(r.Key, item.Target, StringComparison.Ordinal));
        string id = ids.RoomIds[room.Key];
        FeedPayload payload = item.Type switch
        {
            FeedSourceType.RoomCreated => new RoomCreatedPayload(id, room.Name, room.Team, [plan.HumanName, .. room.Members]),
            FeedSourceType.RoomJoined => new RoomJoinedPayload(id, room.Name, room.Team, item.Actor),
            FeedSourceType.RoomArchived => new RoomArchivedPayload(id, room.Name, room.Team),
            _ => new RoomWaitPayload(id, room.Name, room.Team),
        };

        return (id, payload);
    }

    private static (string Target, FeedPayload Payload) TaskPayload(SeedPlan plan, SeedFeedItem item)
    {
        SeedTask task = plan.Tasks.First(t => string.Equals(t.Id, item.Target, StringComparison.Ordinal));
        string team = task.Location.Team;
        FeedPayload payload = item.Type switch
        {
            FeedSourceType.TaskAssignedToYou => new TaskAssignedPayload(task.Id, task.Title, team, null),
            FeedSourceType.TaskGateOn => item.ActorKind == FeedActorKind.Helper
                ? new TaskGatePayload(task.Id, task.Title, team, null, item.Detail)
                : new TaskGatePayload(task.Id, task.Title, team, item.Detail),
            FeedSourceType.TaskNeedsYou => new TaskNeedsYouPayload(task.Id, task.Title, team, 4, 4),
            FeedSourceType.RequestAwaitsYou => new RequestAwaitsPayload(task.Id, task.Title, team, item.Extra ?? team, "bug"),
            FeedSourceType.GateDecisionDue => new GateDecisionPayload(task.Id, task.Title, team, item.Extra ?? "gate", item.Detail ?? string.Empty, "Review", "Done"),
            _ => new TaskCreatedPayload(task.Id, task.Title, team, task.Assignee),
        };

        return (task.Id, payload);
    }

    /// <summary>Gives the Needs Human Helper the day its items describe: its calls used up with two saves skipped, three failures in a row after a good run.</summary>
    private static void WriteHelperState(IOptions<TeamOptions> options, DateTimeOffset seedTime, TimeProvider clock)
    {
        HelperId id = new(SoftwareCoFeed.HelperId);
        HelperUsageStore usage = new(options);
        string day = clock.GetLocalNow().Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        for (int call = 0; call < HelperCap; call++)
        {
            usage.AddCall(id, day);
        }

        usage.AddSkipped(id, day);
        usage.AddSkipped(id, day);

        HelperStatusStore status = new(options);
        status.RecordSuccess(id, seedTime.AddMinutes(-38), "Turned the gate on for SUPP-0008");
        _ = status.RecordFailure(id, seedTime.AddMinutes(-13), "The adapter isn't signed in");
        _ = status.RecordFailure(id, seedTime.AddMinutes(-11), "The adapter isn't signed in");
        _ = status.RecordFailure(id, seedTime.AddMinutes(-9), "The adapter isn't signed in");
    }
}
