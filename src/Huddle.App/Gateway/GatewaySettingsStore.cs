using System.Globalization;
using Agency.Huddle.App.Data;
using Microsoft.Data.Sqlite;

namespace Agency.Huddle.App.Gateway;

/// <summary>
/// The <c>gateway_settings</c> table of <c>team.db</c> (Spec 7.1): one row holding the Instance id, the generated
/// token and the password hash. Every read goes to the database, so two stores over one directory agree.
/// </summary>
internal sealed class GatewaySettingsStore
{
    private const string DdlCreate = """
        CREATE TABLE IF NOT EXISTS gateway_settings (
            id            INTEGER PRIMARY KEY CHECK (id = 1),
            instance_id   TEXT NOT NULL,
            token         TEXT NULL,
            password_hash BLOB NULL,
            overrides     TEXT NOT NULL,
            desktop_port  INTEGER NULL,
            updated_at    TEXT NOT NULL
        );
        """;

    private readonly string connectionString;
    private readonly TimeProvider timeProvider;
    private readonly Lock gate = new();

    /// <summary>Opens <c>team.db</c> under <paramref name="dataDir"/>, creating the table and its single row when missing.</summary>
    /// <param name="dataDir">The data directory.</param>
    /// <param name="timeProvider">Stamps <c>updated_at</c>; defaults to <see cref="TimeProvider.System"/>.</param>
    public GatewaySettingsStore(string dataDir, TimeProvider? timeProvider = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDir);

        this.timeProvider = timeProvider ?? TimeProvider.System;
        this.connectionString = SqliteTable.Open(new TeamOptions { DataDir = dataDir }, DdlCreate);

        using SqliteConnection connection = this.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT OR IGNORE INTO gateway_settings (id, instance_id, token, password_hash, overrides, updated_at)
            VALUES (1, $instance, NULL, NULL, '{}', $now);
            """;
        command.Parameters.AddWithValue("$instance", GatewaySecrets.GenerateInstanceId());
        command.Parameters.AddWithValue("$now", this.Now());
        command.ExecuteNonQuery();
    }

    /// <summary>The Instance id, 8 lowercase hex characters, created once.</summary>
    public string InstanceId => (string)(this.Scalar("SELECT instance_id FROM gateway_settings WHERE id = 1") ?? string.Empty);

    /// <summary>The stored token, or <see langword="null"/> when none was generated or stored.</summary>
    public string? StoredToken => this.Scalar("SELECT token FROM gateway_settings WHERE id = 1") as string;

    /// <summary>The stored password as salt followed by hash, or <see langword="null"/>.</summary>
    public byte[]? StoredPasswordHash => this.Scalar("SELECT password_hash FROM gateway_settings WHERE id = 1") as byte[];

    /// <summary>The port desktop mode bound last time (C-11), or <see langword="null"/> when none was stored.</summary>
    public int? DesktopPort => this.Scalar("SELECT desktop_port FROM gateway_settings WHERE id = 1") is long port ? (int)port : null;

    /// <summary>Stores the port desktop mode bound (C-11).</summary>
    /// <param name="port">The port, 1 to 65535.</param>
    public void SetDesktopPort(int port)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(port, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(port, 65535);

        // Passed as text: the INTEGER column converts it, and it avoids boxing the number at this call site.
        this.Update("UPDATE gateway_settings SET desktop_port = $value, updated_at = $now WHERE id = 1", port.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>Returns the stored token, generating and storing one first when there is none.</summary>
    public string EnsureToken()
    {
        lock (this.gate)
        {
            return this.StoredToken ?? this.RotateToken();
        }
    }

    /// <summary>Replaces the stored token with a newly generated one and returns it.</summary>
    public string RotateToken()
    {
        string token = GatewaySecrets.GenerateToken();
        this.Update("UPDATE gateway_settings SET token = $value, updated_at = $now WHERE id = 1", token);
        return token;
    }

    /// <summary>Stores <paramref name="password"/> as PBKDF2-SHA256 (never the clear text).</summary>
    /// <param name="password">The clear password.</param>
    public void SetPassword(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        this.Update("UPDATE gateway_settings SET password_hash = $value, updated_at = $now WHERE id = 1", GatewaySecrets.HashPassword(password));
    }

    private string Now() => this.timeProvider.GetUtcNow().ToString("O", CultureInfo.InvariantCulture);

    private SqliteConnection Open()
    {
        SqliteConnection connection = new(this.connectionString);
        connection.Open();
        return connection;
    }

    private object? Scalar(string sql)
    {
        using SqliteConnection connection = this.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        object? value = command.ExecuteScalar();
        return value is DBNull ? null : value;
    }

    private void Update(string sql, object value)
    {
        using SqliteConnection connection = this.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$value", value);
        command.Parameters.AddWithValue("$now", this.Now());
        command.ExecuteNonQuery();
    }
}
