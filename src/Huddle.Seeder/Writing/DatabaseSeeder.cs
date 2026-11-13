using Agency.Huddle.App;
using Agency.Huddle.App.Data;
using Agency.Huddle.Contracts;
using Agency.Huddle.Seeder.Model;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Agency.Huddle.Seeder.Writing;

/// <summary>
/// Creates <c>team.db</c> and the Room transcripts through Huddle's own <see cref="ITeamDirectory"/> and
/// <see cref="IChatStore"/>, so the schema and the transcript format stay the app's business.
/// Seeder spec §5, step 3.
/// </summary>
internal static class DatabaseSeeder
{
    /// <summary>Creates the Human, the Agent users, the Rooms, their membership and their transcripts.</summary>
    /// <param name="plan">What to create.</param>
    /// <param name="dataDir">The data folder, which must already exist.</param>
    /// <param name="ct">Cancels the writes.</param>
    /// <returns>The ids that were assigned.</returns>
    internal static async Task<DatabaseResult> SeedAsync(SeedPlan plan, string dataDir, CancellationToken ct)
    {
        IOptions<TeamOptions> options = Options.Create(new TeamOptions { DataDir = dataDir, HumanName = plan.HumanName });
        SqliteTeamDirectory directory = new(options);
        FileChatStore chat = new(options, NullLogger<FileChatStore>.Instance);

        try
        {
            await directory.InitializeAsync(plan.HumanName, ct).ConfigureAwait(false);

            // A saved profile, so the seeded app opens on its Rooms rather than the first-run redirect to Settings > Profile.
            string profile = JsonSerializer.Serialize(new Dictionary<string, string>(StringComparer.Ordinal) { ["name"] = plan.HumanName });
            await File.WriteAllTextAsync(Path.Combine(dataDir, "profile.json"), profile, ct).ConfigureAwait(false);

            Dictionary<string, string> userIds = new(StringComparer.OrdinalIgnoreCase);
            foreach (SeedAgent agent in plan.Agents)
            {
                User user = await directory.UpsertAgentUserAsync(agent.Name, agent.Title, ct).ConfigureAwait(false)
                    ?? throw new InvalidOperationException($"Could not create the user '{agent.Name}'.");
                userIds[agent.Name] = user.Id;
            }

            // Rooms first, Messages second: a Message may link to a Room created after its own, and its link needs that Room's id.
            Dictionary<string, string> roomIds = new(StringComparer.Ordinal);
            foreach (SeedRoom room in plan.Rooms)
            {
                roomIds[room.Key] = await CreateRoomAsync(room, userIds, directory, ct).ConfigureAwait(false);
            }

            foreach (SeedRoom room in plan.Rooms)
            {
                await AppendMessagesAsync(plan, room, dataDir, userIds, roomIds, chat, ct).ConfigureAwait(false);
            }

            return new DatabaseResult(userIds, roomIds);
        }
        finally
        {
            SqliteConnection.ClearPool(new SqliteConnection($"Data Source={Path.Combine(dataDir, "team.db")}"));
        }
    }

    private static async Task<string> CreateRoomAsync(
        SeedRoom room,
        Dictionary<string, string> userIds,
        SqliteTeamDirectory directory,
        CancellationToken ct)
    {
        List<string> memberIds = [KnownIds.Human, .. room.Members.Select(m => userIds[m])];

        // A Team Room is named for its Team, as the app's own reconciler creates it; every other Room starts with the derived name.
        string initialName = room.IsTeamRoom ? room.Name : string.Join(", ", room.Members);
        Room created = await directory.CreateRoomAsync(initialName, memberIds, room.Team, room.IsTeamRoom, room.Project, ct).ConfigureAwait(false);

        if (!string.Equals(room.Name, initialName, StringComparison.Ordinal))
        {
            await directory.RenameRoomAsync(created.Id, room.Name, ct).ConfigureAwait(false);
        }

        if (room.Archived)
        {
            await directory.SetRoomArchivedAsync(created.Id, archived: true, ct).ConfigureAwait(false);
        }

        return created.Id;
    }

    private static async Task AppendMessagesAsync(
        SeedPlan plan,
        SeedRoom room,
        string dataDir,
        Dictionary<string, string> userIds,
        Dictionary<string, string> roomIds,
        FileChatStore chat,
        CancellationToken ct)
    {
        for (int index = 0; index < room.Messages.Count; index++)
        {
            SeedMessage message = room.Messages[index];
            bool human = string.Equals(message.Sender, plan.HumanName, StringComparison.Ordinal);
            string senderId = human ? KnownIds.Human : userIds[message.Sender];
            ExpandedMessage expanded = MessageReferences.Expand(message.Text, room, plan, dataDir, roomIds);
            ChatMessage chatMessage = new(
                MessageId(room.Key, index),
                Timestamp(plan.Today, message),
                senderId,
                message.Sender,
                expanded.Text)
            {
                References = expanded.References,
            };
            await chat.AppendAsync(roomIds[room.Key], chatMessage, ct).ConfigureAwait(false);
        }
    }

    /// <summary>A stable 32-character hex id, so the same scenario always writes the same message ids.</summary>
    /// <param name="roomKey">The Room key.</param>
    /// <param name="index">The message's position in the Room.</param>
    /// <returns>The id.</returns>
    internal static string MessageId(string roomKey, int index)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(string.Create(CultureInfo.InvariantCulture, $"{roomKey}|{index}")));
        return Convert.ToHexStringLower(hash.AsSpan(0, 16));
    }

    private static DateTimeOffset Timestamp(DateOnly today, SeedMessage message) => Timestamp(today, message.DaysAgo, message.Time);

    /// <summary>The instant a seeded offset names: <paramref name="daysAgo"/> days before <paramref name="today"/>, at <paramref name="time"/> UTC.</summary>
    /// <param name="today">The run date.</param>
    /// <param name="daysAgo">Whole days before it.</param>
    /// <param name="time">The time of day, <c>HH:mm</c>.</param>
    /// <returns>The instant.</returns>
    internal static DateTimeOffset Timestamp(DateOnly today, int daysAgo, string time)
    {
        TimeOnly at = TimeOnly.ParseExact(time, "HH:mm", CultureInfo.InvariantCulture);
        return new DateTimeOffset(today.AddDays(-daysAgo).ToDateTime(at), TimeSpan.Zero);
    }
}
