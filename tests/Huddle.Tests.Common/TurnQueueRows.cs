using Microsoft.Data.Sqlite;

namespace Agency.Huddle.Tests;

/// <summary>One <c>turn_queue</c> row as a test sees it, done rows included.</summary>
/// <param name="AgentId">The Agent that owes the Turn.</param>
/// <param name="MessageId">The Message.</param>
/// <param name="RoomId">The Room.</param>
/// <param name="Status">The stored status text.</param>
/// <param name="Outcome">The stored outcome text, or <see langword="null"/> while open.</param>
/// <param name="ConnectionId">The connection the Turn ran on, if any.</param>
public sealed record TurnQueueRowSnapshot(string AgentId, string MessageId, string RoomId, string Status, string? Outcome, string? ConnectionId);

/// <summary>Reads every <c>turn_queue</c> row straight from <c>team.db</c>, so a test sees the rows the store's open-row queries hide.</summary>
public static class TurnQueueRows
{
    /// <summary>Reads all rows, oldest first.</summary>
    /// <param name="dataDir">The data directory holding <c>team.db</c>.</param>
    /// <returns>The rows ordered by <c>seq</c>.</returns>
    public static IReadOnlyList<TurnQueueRowSnapshot> Read(string dataDir)
    {
        using SqliteConnection connection = new($"Data Source={Path.Combine(dataDir, "team.db")}");
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT agent_id, message_id, room_id, status, outcome, connection_id FROM turn_queue ORDER BY seq;";
        using SqliteDataReader reader = command.ExecuteReader();
        List<TurnQueueRowSnapshot> rows = [];
        while (reader.Read())
        {
            rows.Add(new TurnQueueRowSnapshot(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5)));
        }

        return rows;
    }

    /// <summary>Finds the one row of an Agent for a Message in a Room.</summary>
    /// <param name="dataDir">The data directory holding <c>team.db</c>.</param>
    /// <param name="agentId">The Agent.</param>
    /// <param name="roomId">The Room.</param>
    /// <param name="messageId">The Message.</param>
    /// <returns>The row.</returns>
    public static TurnQueueRowSnapshot Find(string dataDir, string agentId, string roomId, string messageId) =>
        Read(dataDir).Single(row => row.AgentId == agentId && row.RoomId == roomId && row.MessageId == messageId);
}
