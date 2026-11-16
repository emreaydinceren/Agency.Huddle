using Microsoft.Data.Sqlite;

namespace Agency.Huddle.Tests;

/// <summary>
/// Writes the <c>room_teams</c> and <c>room_projects</c> rows that say which Team and Project a Room belongs to, straight into a
/// test data folder's <c>team.db</c>. The tables must already exist, which constructing a <c>RoomSpendStore</c> over the same folder guarantees.
/// </summary>
public static class RoomScopeSeed
{
    /// <summary>Places a Room in a Team and, optionally, a Project, replacing any earlier placement of the same Room.</summary>
    /// <param name="dir">The test data folder holding <c>team.db</c>.</param>
    /// <param name="roomId">The Room's id.</param>
    /// <param name="team">The Team the Room belongs to.</param>
    /// <param name="project">The Project the Room belongs to, or <see langword="null"/> for none.</param>
    public static void Place(TempDataDir dir, string roomId, string team, string? project)
    {
        ArgumentNullException.ThrowIfNull(dir);
        using SqliteConnection connection = new($"Data Source={System.IO.Path.Combine(dir.Path, "team.db")}");
        connection.Open();

        // The tables declare REFERENCES rooms(id), and the rooms table is the directory's, not this store's, so the seed switches enforcement off.
        using (SqliteCommand pragma = connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA foreign_keys = OFF;";
            _ = pragma.ExecuteNonQuery();
        }

        Run(connection, "INSERT OR REPLACE INTO room_teams(room_id, team, team_room) VALUES($room, $value, 0);", roomId, team);
        if (project is null)
        {
            Run(connection, "DELETE FROM room_projects WHERE room_id = $room;", roomId, team);
        }
        else
        {
            Run(connection, "INSERT OR REPLACE INTO room_projects(room_id, project) VALUES($room, $value);", roomId, project);
        }
    }

    private static void Run(SqliteConnection connection, string sql, string roomId, string value)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        _ = command.Parameters.AddWithValue("$room", roomId);
        _ = command.Parameters.AddWithValue("$value", value);
        _ = command.ExecuteNonQuery();
    }
}
