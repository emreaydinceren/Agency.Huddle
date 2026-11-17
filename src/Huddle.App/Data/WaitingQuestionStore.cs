using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Agency.Huddle.App.Questions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Data;

/// <summary>
/// Keeps each Room's waiting <c>ask_human</c> card in <c>team.db</c> so a restart does not lose it (Turn Queue
/// spec §5a). <see cref="QuestionStore"/> writes through to it and loads it at start-up. Deliberately synchronous
/// like <see cref="JobStateStore"/>: each call is one small statement.
/// </summary>
internal sealed class WaitingQuestionStore
{
    private const string DdlCreateWaitingQuestions = """
        CREATE TABLE IF NOT EXISTS waiting_questions (
            room_id        TEXT PRIMARY KEY,
            card_id        TEXT NOT NULL,
            asker_agent_id TEXT NOT NULL,
            asker_name     TEXT NOT NULL,
            questions_json TEXT NOT NULL,
            asked_utc      TEXT NOT NULL
        );
        """;

    private static readonly JsonSerializerOptions Json = new()
    {
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) },
    };

    private readonly string connectionString;
    private readonly ILogger<WaitingQuestionStore> logger;

    /// <summary>Opens the database and creates the table when it is missing.</summary>
    /// <param name="options">Supplies the data folder holding <c>team.db</c>.</param>
    /// <param name="logger">Receives a row that could not be read; <see langword="null"/> logs nothing.</param>
    public WaitingQuestionStore(IOptions<TeamOptions> options, ILogger<WaitingQuestionStore>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.connectionString = SqliteTable.Open(options.Value, DdlCreateWaitingQuestions);
        this.logger = logger ?? NullLogger<WaitingQuestionStore>.Instance;
    }

    /// <summary>Stores a Room's waiting card, replacing the row that was there.</summary>
    /// <param name="card">The card.</param>
    public void Put(PendingQuestions card)
    {
        ArgumentNullException.ThrowIfNull(card);

        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO waiting_questions(room_id, card_id, asker_agent_id, asker_name, questions_json, asked_utc)
            VALUES($room, $card, $agent, $name, $json, $asked)
            ON CONFLICT(room_id) DO UPDATE SET
                card_id = excluded.card_id,
                asker_agent_id = excluded.asker_agent_id,
                asker_name = excluded.asker_name,
                questions_json = excluded.questions_json,
                asked_utc = excluded.asked_utc;
            """;
        _ = command.Parameters.AddWithValue("$room", card.RoomId);
        _ = command.Parameters.AddWithValue("$card", card.Id);
        _ = command.Parameters.AddWithValue("$agent", card.AskerAgentId);
        _ = command.Parameters.AddWithValue("$name", card.AskerName);
        _ = command.Parameters.AddWithValue("$json", JsonSerializer.Serialize(card.Questions, Json));
        _ = command.Parameters.AddWithValue("$asked", card.AskedAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
        _ = command.ExecuteNonQuery();
    }

    /// <summary>Forgets a Room's waiting card.</summary>
    /// <param name="roomId">The Room's id.</param>
    public void Delete(string roomId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);

        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "DELETE FROM waiting_questions WHERE room_id = $room;";
        _ = command.Parameters.AddWithValue("$room", roomId);
        _ = command.ExecuteNonQuery();
    }

    /// <summary>
    /// Reads every stored card. Rows for a Room that no longer exists or is archived are deleted first (when this
    /// database has the Room tables), and a row that cannot be parsed is logged once, skipped and deleted.
    /// </summary>
    /// <returns>One card per readable row.</returns>
    public IReadOnlyList<PendingQuestions> LoadAll()
    {
        List<PendingQuestions> cards = [];
        List<string> corrupt = [];

        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        DeleteStaleRooms(connection);
        using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText = "SELECT room_id, card_id, asker_agent_id, asker_name, questions_json, asked_utc FROM waiting_questions;";
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                this.ReadRow(reader, cards, corrupt);
            }
        }

        foreach (string roomId in corrupt)
        {
            using SqliteCommand delete = connection.CreateCommand();
            delete.CommandText = "DELETE FROM waiting_questions WHERE room_id = $room;";
            _ = delete.Parameters.AddWithValue("$room", roomId);
            _ = delete.ExecuteNonQuery();
        }

        return cards;
    }

    private static void DeleteStaleRooms(SqliteConnection connection)
    {
        using SqliteCommand probe = connection.CreateCommand();
        probe.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name IN ('rooms', 'archived_rooms');";
        if (Convert.ToInt32(probe.ExecuteScalar(), CultureInfo.InvariantCulture) != 2)
        {
            return;
        }

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM waiting_questions
            WHERE room_id NOT IN (SELECT id FROM rooms) OR room_id IN (SELECT room_id FROM archived_rooms);
            """;
        _ = command.ExecuteNonQuery();
    }

    private void ReadRow(SqliteDataReader reader, List<PendingQuestions> cards, List<string> corrupt)
    {
        string roomId = reader.GetString(0);
        try
        {
            List<Question?>? stored = JsonSerializer.Deserialize<List<Question?>>(reader.GetString(4), Json);
            if (stored is null or { Count: 0 } || stored.Exists(static q => q is null || q.Text is null || q.Options is null))
            {
                throw new JsonException("The stored Questions are empty or incomplete.");
            }

            List<Question> questions = [.. stored.OfType<Question>()];
            DateTimeOffset asked = DateTimeOffset.Parse(
                reader.GetString(5),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
            cards.Add(new PendingQuestions(reader.GetString(1), roomId, reader.GetString(2), reader.GetString(3), questions, asked));
        }
        catch (Exception ex) when (ex is JsonException or FormatException or NotSupportedException)
        {
            this.logger.LogWarning(ex, "Dropping the stored Question card for room {RoomId}: it cannot be read.", roomId);
            corrupt.Add(roomId);
        }
    }
}
