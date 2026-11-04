using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Agency.Huddle.App.Feed;

namespace Agency.Huddle.Tests.Feed;

/// <summary>Builders and raw-SQL helpers shared by the Feed backend tests.</summary>
internal static class FeedTestSupport
{
    /// <summary>A fixed instant the tests count from.</summary>
    public static readonly DateTimeOffset T0 = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    /// <summary>An Agent Agent actor.</summary>
    /// <param name="name">The Agent's Name.</param>
    /// <returns>The actor.</returns>
    public static FeedActor Agent(string name = "Nova") => new(FeedActorKind.Agent, name);

    /// <summary>A <c>task-created</c> message, not a Notification.</summary>
    /// <param name="id">The Task id, also the target.</param>
    /// <param name="team">The Task's Team.</param>
    /// <param name="actor">The Agent's Name.</param>
    /// <returns>The message.</returns>
    public static FeedMessage TaskCreated(string id = "GRO-1", string team = "Growth", string actor = "Nova") =>
        new(FeedSourceType.TaskCreated, id, Agent(actor), false, new TaskCreatedPayload(id, "Draft " + id, team, null));

    /// <summary>A <c>task-needs-you</c> message, a Notification.</summary>
    /// <param name="id">The Task id, also the target.</param>
    /// <param name="team">The Task's Team.</param>
    /// <param name="actor">The Agent's Name.</param>
    /// <returns>The message.</returns>
    public static FeedMessage NeedsYou(string id = "GRO-1", string team = "Growth", string actor = "Nova") =>
        new(FeedSourceType.TaskNeedsYou, id, Agent(actor), true, new TaskNeedsYouPayload(id, "Draft " + id, team, 1, 2));

    /// <summary>A <c>room-mention</c> message with no Room name or Team yet, a Notification.</summary>
    /// <param name="roomId">The Room id, also the target.</param>
    /// <param name="roomName">The Room name the sender knew, or <see langword="null"/>.</param>
    /// <param name="team">The Team the sender knew, or <see langword="null"/>.</param>
    /// <returns>The message.</returns>
    public static FeedMessage RoomMention(string roomId, string? roomName = null, string? team = null) =>
        new(FeedSourceType.RoomMention, roomId, Agent(), true, new RoomWaitPayload(roomId, roomName, team));

    /// <summary>One sample payload of every source type, with the given Team where the payload has one.</summary>
    /// <param name="type">The source type.</param>
    /// <param name="team">The Team to carry.</param>
    /// <returns>A payload of the type <see cref="FeedPayloads.TypeOf"/> names.</returns>
    public static FeedPayload SamplePayload(FeedSourceType type, string team = "Growth") => type switch
    {
        FeedSourceType.TaskCreated => new TaskCreatedPayload("GRO-1", "Title", team, "Nova"),
        FeedSourceType.TaskGateOn => new TaskGatePayload("GRO-1", "Title", team, "Review"),
        FeedSourceType.TaskNeedsYou => new TaskNeedsYouPayload("GRO-1", "Title", team, 1, 3),
        FeedSourceType.TaskAssignedToYou => new TaskAssignedPayload("GRO-1", "Title", team, "Sage"),
        FeedSourceType.RequestAwaitsYou => new RequestAwaitsPayload("GRO-1", "Title", team, "Sales", "review"),
        FeedSourceType.GateDecisionDue => new GateDecisionPayload("GRO-1", "Title", team, "gate-1", "Ship it?", "Doing", "Done"),
        FeedSourceType.RoomMention or FeedSourceType.RoomQuestion or FeedSourceType.RoomForm or FeedSourceType.RoomBudget or FeedSourceType.RoomJob
            => new RoomWaitPayload("room-1", "Plan", team),
        FeedSourceType.RoomCreated => new RoomCreatedPayload("room-1", "Plan", team, ["Nova", "You"]),
        FeedSourceType.RoomJoined => new RoomJoinedPayload("room-1", "Plan", team, "Sage"),
        FeedSourceType.RoomArchived => new RoomArchivedPayload("room-1", "Plan", team),
        FeedSourceType.DocumentChanged => new DocumentChangedPayload("C:/lib/a.md", "lib", "a.md", "edit", "Fixed a typo"),
        FeedSourceType.HelperPaused => new HelperPausedPayload("NeedsHuman", "Needs Human", 50, 0),
        FeedSourceType.HelperFailing => new HelperFailingPayload("NeedsHuman", "Needs Human", 3, "The adapter isn't signed in"),
        FeedSourceType.TeamMemoryNotFiled => new TeamMemoryNotFiledPayload("TeamMemory", "Team Memory", team, "Leo", "I prefer short summaries.", "A personal preference"),
        FeedSourceType.PairingRequested => new PairingRequestedPayload("req-1", "iPad", "100.64.0.7", "Managed"),
        FeedSourceType.AdminRequested => new AdminRequestedPayload("req-1", "iPad", "100.64.0.7", "Managed"),
        FeedSourceType.DevicePaired => new DevicePairedPayload("dev-1", "iPad", "approval"),
        FeedSourceType.DeviceRevoked => new DeviceRevokedPayload("dev-1", "iPad", "revoked"),
        FeedSourceType.SecondBrainOverCap => new SecondBrainOverCapPayload("Teams/Home/memory", "Teams/Home/memory has more Memory than Agents are shown (50 of 50 listed). Ask the Team's manager to tidy it."),
        FeedSourceType.TaskWakesPaused => new TaskWakesPausedPayload("GRO-44", "Ship the page", team, "Nova"),
        FeedSourceType.TeamArchived => new TeamArchivedPayload(team, "Team " + team + " archived"),
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };

    /// <summary>A message of any source type, a Notification where the table of spec 6.3 says so.</summary>
    /// <param name="type">The source type.</param>
    /// <param name="target">The target key.</param>
    /// <param name="actor">The actor's Name.</param>
    /// <param name="team">The Team to carry.</param>
    /// <returns>The message.</returns>
    public static FeedMessage Of(FeedSourceType type, string target, string actor = "Nova", string team = "Growth")
    {
        bool notify = type is FeedSourceType.TaskNeedsYou or FeedSourceType.TaskAssignedToYou or FeedSourceType.RequestAwaitsYou
            or FeedSourceType.GateDecisionDue or FeedSourceType.RoomMention or FeedSourceType.RoomQuestion
            or FeedSourceType.RoomForm or FeedSourceType.RoomBudget or FeedSourceType.HelperPaused or FeedSourceType.HelperFailing;
        return new FeedMessage(type, target, Agent(actor), notify, SamplePayload(type, team));
    }

    /// <summary>Applies one Post and returns the stored item.</summary>
    /// <param name="store">The store.</param>
    /// <param name="message">The message.</param>
    /// <param name="at">The command time.</param>
    /// <returns>The inserted item.</returns>
    public static FeedItem Post(FeedStore store, FeedMessage message, DateTimeOffset? at = null) =>
        store.Apply([new PostCommand(message, at ?? T0)]).Added.Single();

    /// <summary>Inserts a row the store did not write, as an older or newer build might have.</summary>
    /// <param name="dataDir">The data folder holding <c>team.db</c>.</param>
    /// <param name="sourceType">The raw <c>source_type</c> text.</param>
    /// <param name="payload">The raw <c>payload</c> text.</param>
    /// <param name="notify">Whether it is a Notification.</param>
    public static void InsertRaw(string dataDir, string sourceType, string payload, bool notify = false)
    {
        using SqliteConnection connection = new($"Data Source={Path.Combine(dataDir, "team.db")}");
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "INSERT INTO feed_events(created_at, source_type, target_key, actor, notify, payload) VALUES($at, $type, 'raw', 'Nova', $notify, $payload);";
        _ = command.Parameters.AddWithValue("$at", T0.UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        _ = command.Parameters.AddWithValue("$type", sourceType);
        _ = command.Parameters.AddWithValue("$notify", notify ? 1 : 0);
        _ = command.Parameters.AddWithValue("$payload", payload);
        _ = command.ExecuteNonQuery();
    }

    /// <summary>Runs one statement against <c>team.db</c> directly.</summary>
    /// <param name="dataDir">The data folder holding <c>team.db</c>.</param>
    /// <param name="sql">The statement.</param>
    public static void Exec(string dataDir, string sql)
    {
        using SqliteConnection connection = new($"Data Source={Path.Combine(dataDir, "team.db")}");
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        _ = command.ExecuteNonQuery();
    }

    /// <summary>Adapts a plain <see cref="ILogger"/> to <see cref="ILogger{TCategoryName}"/>.</summary>
    /// <typeparam name="T">The category type.</typeparam>
    /// <param name="inner">The logger every call is forwarded to.</param>
    public sealed class TypedLogger<T>(ILogger inner) : ILogger<T>
    {
        /// <inheritdoc/>
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => inner.BeginScope(state);

        /// <inheritdoc/>
        public bool IsEnabled(LogLevel logLevel) => inner.IsEnabled(logLevel);

        /// <inheritdoc/>
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            inner.Log(logLevel, eventId, state, exception, formatter);
    }
}
