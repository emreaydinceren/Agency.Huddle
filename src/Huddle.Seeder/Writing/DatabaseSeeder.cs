using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Data;
using Agency.Huddle.Contracts;
using Agency.Huddle.Seeder.Model;

namespace Agency.Huddle.Seeder.Writing;

/// <summary>The ids the database assigned, which Task files and the manifest need.</summary>
/// <param name="UserIds">Teammate Name to user id.</param>
/// <param name="RoomIds">Room key to Room id.</param>
internal sealed record DatabaseResult(IReadOnlyDictionary<string, string> UserIds, IReadOnlyDictionary<string, string> RoomIds);

/// <summary>
/// Creates <c>team.db</c> and the Room transcripts through Huddle's own <see cref="ITeamDirectory"/> and
/// <see cref="IChatStore"/>, so the schema and the transcript format stay the app's business.
/// </summary>
internal static class DatabaseSeeder
{
    /// <summary>Creates the Human, the Teammate users, the Rooms, their membership and their transcripts.</summary>
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
            await directory.InitializeAsync(plan.HumanName, ct);

            Dictionary<string, string> userIds = new(StringComparer.OrdinalIgnoreCase);
            foreach (SeedTeammate teammate in plan.Teammates)
            {
                User user = await directory.UpsertAgentUserAsync(teammate.Name, teammate.Title, ct)
                    ?? throw new InvalidOperationException($"Could not create the user '{teammate.Name}'.");
                userIds[teammate.Name] = user.Id;
            }

            Dictionary<string, string> roomIds = new(StringComparer.Ordinal);
            foreach (SeedRoom room in plan.Rooms)
            {
                roomIds[room.Key] = await CreateRoomAsync(plan, room, userIds, directory, chat, ct);
            }

            return new DatabaseResult(userIds, roomIds);
        }
        finally
        {
            SqliteConnection.ClearPool(new SqliteConnection($"Data Source={Path.Combine(dataDir, "team.db")}"));
        }
    }

    private static async Task<string> CreateRoomAsync(
        SeedPlan plan,
        SeedRoom room,
        Dictionary<string, string> userIds,
        SqliteTeamDirectory directory,
        FileChatStore chat,
        CancellationToken ct)
    {
        List<string> memberIds = [KnownIds.Human, .. room.Members.Select(m => userIds[m])];
        string derivedName = string.Join(", ", room.Members);
        Room created = await directory.CreateRoomAsync(derivedName, memberIds, ct);

        if (!string.Equals(room.Name, derivedName, StringComparison.Ordinal))
        {
            await directory.RenameRoomAsync(created.Id, room.Name, ct);
        }

        if (room.Archived)
        {
            await directory.SetRoomArchivedAsync(created.Id, archived: true, ct);
        }

        for (int index = 0; index < room.Messages.Count; index++)
        {
            SeedMessage message = room.Messages[index];
            bool human = string.Equals(message.Sender, plan.HumanName, StringComparison.Ordinal);
            string senderId = human ? KnownIds.Human : userIds[message.Sender];
            ChatMessage chatMessage = new(
                MessageId(room.Key, index),
                Timestamp(plan.Today, message),
                senderId,
                message.Sender,
                message.Text);
            await chat.AppendAsync(created.Id, chatMessage, ct);
        }

        return created.Id;
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

    private static DateTimeOffset Timestamp(DateOnly today, SeedMessage message)
    {
        TimeOnly time = TimeOnly.ParseExact(message.Time, "HH:mm", CultureInfo.InvariantCulture);
        return new DateTimeOffset(today.AddDays(-message.DaysAgo).ToDateTime(time), TimeSpan.Zero);
    }
}
