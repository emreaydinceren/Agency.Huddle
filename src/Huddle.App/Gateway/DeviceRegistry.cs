using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Agency.Huddle.App.Data;
using Microsoft.Data.Sqlite;

namespace Agency.Huddle.App.Gateway;

/// <summary>The <c>gateway_devices</c> table of <c>team.db</c> (Spec 7.1): paired devices and their hashed tokens.</summary>
internal sealed class DeviceRegistry
{
    /// <summary>How long a loopback or bootstrap device may go unseen before housekeeping deletes it (C-11).</summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromDays(30);

    private const int MaxNameLength = 60;

    private const string DdlCreate = """
        CREATE TABLE IF NOT EXISTS gateway_devices (
            device_id       TEXT PRIMARY KEY,
            public_key      BLOB NOT NULL,
            name            TEXT NOT NULL,
            scopes          TEXT NOT NULL,
            token_hash      BLOB NULL UNIQUE,
            paired_via      TEXT NOT NULL,
            tailscale_login TEXT NULL,
            paired_at       TEXT NOT NULL,
            last_seen_at    TEXT NOT NULL,
            last_seen_from  TEXT NOT NULL,
            user_agent      TEXT NULL
        );
        """;

    private const string Columns =
        "device_id, public_key, name, scopes, token_hash IS NOT NULL, paired_via, tailscale_login, paired_at, last_seen_at, last_seen_from, user_agent";

    private readonly string connectionString;
    private readonly TimeProvider timeProvider;
    private readonly Lock gate = new();
    private readonly List<Action<string>> endedCallbacks = [];

    /// <summary>Opens <c>team.db</c> under <paramref name="dataDir"/>, creating the table when missing.</summary>
    /// <param name="dataDir">The data directory.</param>
    /// <param name="timeProvider">Defaults to <see cref="TimeProvider.System"/>.</param>
    public DeviceRegistry(string dataDir, TimeProvider? timeProvider = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDir);

        this.timeProvider = timeProvider ?? TimeProvider.System;
        this.connectionString = SqliteTable.Open(new TeamOptions { DataDir = dataDir }, DdlCreate);
    }

    /// <summary>
    /// Pairs a device, or updates one already paired (new scopes, name, address), keeping its token.
    /// </summary>
    /// <param name="registration">The device to pair.</param>
    /// <returns>A new device token (32 bytes, base64url) when the device had none; <see langword="null"/> when it keeps its current token. The token is stored only as a SHA-256 hash.</returns>
    public string? Pair(Registration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);

        string now = this.Now();
        string scopes = GatewayScopes.Join(GatewayScopes.Normalize(registration.Scopes));
        string name = CleanName(registration.Name);
        lock (this.gate)
        {
            using SqliteConnection connection = this.Open();
            DeviceRecord? existing = ReadOne(connection, registration.DeviceId);
            string? token = existing is { HasToken: true } ? null : NewToken();
            using SqliteCommand command = connection.CreateCommand();
            if (existing is null)
            {
                command.CommandText = """
                    INSERT INTO gateway_devices (device_id, public_key, name, scopes, token_hash, paired_via, tailscale_login, paired_at, last_seen_at, last_seen_from, user_agent)
                    VALUES ($id, $key, $name, $scopes, $hash, $via, $login, $now, $now, $from, $ua);
                    """;
                command.Parameters.AddWithValue("$key", registration.PublicKey);
            }
            else
            {
                command.CommandText = """
                    UPDATE gateway_devices
                    SET name = $name, scopes = $scopes, last_seen_at = $now, last_seen_from = $from, user_agent = $ua,
                        tailscale_login = COALESCE($login, tailscale_login),
                        token_hash = COALESCE($hash, token_hash),
                        paired_via = CASE WHEN $hash IS NULL THEN paired_via ELSE $via END,
                        paired_at = CASE WHEN $hash IS NULL THEN paired_at ELSE $now END
                    WHERE device_id = $id;
                    """;
            }

            command.Parameters.AddWithValue("$id", registration.DeviceId);
            command.Parameters.AddWithValue("$name", name);
            command.Parameters.AddWithValue("$scopes", scopes);
            command.Parameters.AddWithValue("$hash", token is null ? DBNull.Value : HashToken(token));
            command.Parameters.AddWithValue("$via", registration.PairedVia);
            command.Parameters.AddWithValue("$login", (object?)registration.TailscaleLogin ?? DBNull.Value);
            command.Parameters.AddWithValue("$now", now);
            command.Parameters.AddWithValue("$from", registration.From);
            command.Parameters.AddWithValue("$ua", (object?)registration.UserAgent ?? DBNull.Value);
            _ = command.ExecuteNonQuery();
            return token;
        }
    }

    /// <summary>The device, or <see langword="null"/>.</summary>
    /// <param name="deviceId">The device id.</param>
    public DeviceRecord? Find(string deviceId)
    {
        ArgumentNullException.ThrowIfNull(deviceId);

        lock (this.gate)
        {
            using SqliteConnection connection = this.Open();
            return ReadOne(connection, deviceId);
        }
    }

    /// <summary>The device whose current token is <paramref name="token"/>, or <see langword="null"/>.</summary>
    /// <param name="token">The presented device token.</param>
    public DeviceRecord? FindByToken(string token)
    {
        ArgumentNullException.ThrowIfNull(token);

        if (token.Length == 0)
        {
            return null;
        }

        lock (this.gate)
        {
            using SqliteConnection connection = this.Open();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"SELECT {Columns} FROM gateway_devices WHERE token_hash = $hash;";
            command.Parameters.AddWithValue("$hash", HashToken(token));
            using SqliteDataReader reader = command.ExecuteReader();
            return reader.Read() ? ReadRecord(reader) : null;
        }
    }

    /// <summary>Every device, newest pairing first.</summary>
    public IReadOnlyList<DeviceRecord> List()
    {
        lock (this.gate)
        {
            using SqliteConnection connection = this.Open();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"SELECT {Columns} FROM gateway_devices ORDER BY paired_at DESC, rowid DESC;";
            using SqliteDataReader reader = command.ExecuteReader();
            List<DeviceRecord> devices = [];
            while (reader.Read())
            {
                devices.Add(ReadRecord(reader));
            }

            return devices;
        }
    }

    /// <summary>Records a handshake: last seen time, address and user agent.</summary>
    /// <param name="deviceId">The device id.</param>
    /// <param name="from">The client address.</param>
    /// <param name="userAgent">The user agent.</param>
    public void Touch(string deviceId, string from, string? userAgent)
    {
        ArgumentNullException.ThrowIfNull(deviceId);
        ArgumentNullException.ThrowIfNull(from);

        _ = this.Execute(
            "UPDATE gateway_devices SET last_seen_at = $now, last_seen_from = $from, user_agent = $ua WHERE device_id = $id;",
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["$id"] = deviceId,
                ["$from"] = from,
                ["$ua"] = userAgent,
            });
    }

    /// <summary>Issues a new token within the approved scopes; the old one stops working.</summary>
    /// <param name="deviceId">The device id.</param>
    /// <returns>The new token, or <see langword="null"/> when the device is unknown.</returns>
    public string? Rotate(string deviceId)
    {
        ArgumentNullException.ThrowIfNull(deviceId);

        string token = NewToken();
        int changed = this.Execute(
            "UPDATE gateway_devices SET token_hash = $hash WHERE device_id = $id;",
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["$id"] = deviceId, ["$hash"] = HashToken(token) });
        if (changed == 0)
        {
            return null;
        }

        this.NotifyEnded(deviceId);
        return token;
    }

    /// <summary>
    /// Registers a callback run, outside the registry's lock, after a device is revoked, removed or rotated (Spec 6.8): the session registry
    /// ends the device's sessions and the connection guard closes its sockets.
    /// </summary>
    /// <param name="onDeviceEnded">Receives the device id.</param>
    public void Subscribe(Action<string> onDeviceEnded)
    {
        ArgumentNullException.ThrowIfNull(onDeviceEnded);

        lock (this.gate)
        {
            this.endedCallbacks.Add(onDeviceEnded);
        }
    }

    /// <summary>Removes the device's token; it stays listed, unable to present a device token.</summary>
    /// <param name="deviceId">The device id.</param>
    /// <returns>Whether the device exists.</returns>
    public bool Revoke(string deviceId)
    {
        ArgumentNullException.ThrowIfNull(deviceId);

        bool changed = this.Execute(
            "UPDATE gateway_devices SET token_hash = NULL WHERE device_id = $id;",
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["$id"] = deviceId }) > 0;
        if (changed)
        {
            this.NotifyEnded(deviceId);
        }

        return changed;
    }

    /// <summary>Deletes the device.</summary>
    /// <param name="deviceId">The device id.</param>
    /// <returns>Whether the device existed.</returns>
    public bool Remove(string deviceId)
    {
        ArgumentNullException.ThrowIfNull(deviceId);

        bool existed = this.Execute(
            "DELETE FROM gateway_devices WHERE device_id = $id;",
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["$id"] = deviceId }) > 0;
        if (existed)
        {
            this.NotifyEnded(deviceId);
        }

        return existed;
    }

    /// <summary>Deletes devices paired via <c>loopback</c> or <c>bootstrap</c> and unseen for <see cref="StaleAfter"/> (C-11).</summary>
    /// <returns>How many were deleted.</returns>
    public int PruneStale()
    {
        string cutoff = Format(this.timeProvider.GetUtcNow() - StaleAfter);
        return this.Execute(
            "DELETE FROM gateway_devices WHERE paired_via IN ('loopback', 'bootstrap') AND last_seen_at < $cutoff;",
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["$cutoff"] = cutoff });
    }

    private static string Format(DateTimeOffset moment) => moment.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

    private static string NewToken() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    private static byte[] HashToken(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));

    private static string CleanName(string name)
    {
        string trimmed = name.Trim();
        if (trimmed.Length == 0)
        {
            return "Browser";
        }

        return trimmed.Length > MaxNameLength ? trimmed[..MaxNameLength] : trimmed;
    }

    private static DeviceRecord? ReadOne(SqliteConnection connection, string deviceId)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"SELECT {Columns} FROM gateway_devices WHERE device_id = $id;";
        command.Parameters.AddWithValue("$id", deviceId);
        using SqliteDataReader reader = command.ExecuteReader();
        return reader.Read() ? ReadRecord(reader) : null;
    }

    private static DeviceRecord ReadRecord(SqliteDataReader reader) => new(
        reader.GetString(0),
        (byte[])reader.GetValue(1),
        reader.GetString(2),
        reader.GetString(3).Split(',', StringSplitOptions.RemoveEmptyEntries),
        reader.GetInt64(4) != 0,
        reader.GetString(5),
        reader.IsDBNull(6) ? null : reader.GetString(6),
        ParseTime(reader.GetString(7)),
        ParseTime(reader.GetString(8)),
        reader.GetString(9),
        reader.IsDBNull(10) ? null : reader.GetString(10));

    private static DateTimeOffset ParseTime(string text) => DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private void NotifyEnded(string deviceId)
    {
        Action<string>[] callbacks;
        lock (this.gate)
        {
            callbacks = [.. this.endedCallbacks];
        }

        foreach (Action<string> callback in callbacks)
        {
            callback(deviceId);
        }
    }

    private string Now() => Format(this.timeProvider.GetUtcNow());

    private SqliteConnection Open()
    {
        SqliteConnection connection = new(this.connectionString);
        connection.Open();
        return connection;
    }

    private int Execute(string sql, Dictionary<string, object?> parameters)
    {
        lock (this.gate)
        {
            using SqliteConnection connection = this.Open();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.AddWithValue("$now", this.Now());
            foreach ((string name, object? value) in parameters)
            {
                command.Parameters.AddWithValue(name, value ?? DBNull.Value);
            }

            return command.ExecuteNonQuery();
        }
    }

    /// <summary>A device to pair.</summary>
    /// <param name="DeviceId">The SHA-256 hex of the key.</param>
    /// <param name="PublicKey">The 32 key bytes.</param>
    /// <param name="Name">The name; cut to 60 characters.</param>
    /// <param name="Scopes">The approved scopes.</param>
    /// <param name="PairedVia">How it was paired.</param>
    /// <param name="TailscaleLogin">The Tailscale login, if any.</param>
    /// <param name="From">The client address.</param>
    /// <param name="UserAgent">The user agent.</param>
    public sealed record Registration(
        string DeviceId,
        byte[] PublicKey,
        string Name,
        IReadOnlyList<string> Scopes,
        string PairedVia,
        string? TailscaleLogin,
        string From,
        string? UserAgent);
}
