using System.Globalization;
using System.Text.RegularExpressions;
using Agency.Huddle.App.Gateway;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Time.Testing;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>Spec 7.1: the <c>gateway_settings</c> table in <c>team.db</c> holds the Instance id, the generated token and the password hash.</summary>
public sealed partial class GatewaySettingsStoreTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 30, 9, 0, 0, TimeSpan.Zero);

    [GeneratedRegex("^[0-9a-f]{48}$", RegexOptions.CultureInvariant)]
    private static partial Regex Token48Hex();

    [GeneratedRegex("^[0-9a-f]{8}$", RegexOptions.CultureInvariant)]
    private static partial Regex InstanceId8Hex();

    /// <summary>Runs a scalar query against the directory's <c>team.db</c>.</summary>
    private static object? Scalar(TempDataDir dir, string sql)
    {
        using SqliteConnection connection = new($"Data Source={Path.Combine(dir.Path, "team.db")}");
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        object? value = command.ExecuteScalar();
        return value is DBNull ? null : value;
    }

    /// <summary>A new store creates its Instance id: 8 lowercase hex characters.</summary>
    [Fact]
    public void Create_AssignsInstanceId_Of8LowercaseHex()
    {
        using TempDataDir dir = new();

        GatewaySettingsStore store = new(dir.Path, new FakeTimeProvider(Start));

        Assert.Matches(InstanceId8Hex(), store.InstanceId);
    }

    /// <summary>The Instance id is created once: reopening the same data directory returns the same id.</summary>
    [Fact]
    public void InstanceId_ReopenedStore_IsStable()
    {
        using TempDataDir dir = new();
        string first = new GatewaySettingsStore(dir.Path, new FakeTimeProvider(Start)).InstanceId;

        string second = new GatewaySettingsStore(dir.Path, new FakeTimeProvider(Start)).InstanceId;

        Assert.Equal(first, second);
    }

    /// <summary>Two data directories get different Instance ids, which is what tells two copies on one machine apart.</summary>
    [Fact]
    public void InstanceId_TwoDirectories_Differ()
    {
        using TempDataDir one = new();
        using TempDataDir two = new();

        string first = new GatewaySettingsStore(one.Path).InstanceId;
        string second = new GatewaySettingsStore(two.Path).InstanceId;

        Assert.NotEqual(first, second);
    }

    /// <summary>The table holds exactly one row, with id 1, however many times the store is opened.</summary>
    [Fact]
    public void Table_AfterReopening_HasOneRowWithId1()
    {
        using TempDataDir dir = new();
        _ = new GatewaySettingsStore(dir.Path);
        _ = new GatewaySettingsStore(dir.Path);

        Assert.Equal(1L, Scalar(dir, "SELECT COUNT(*) FROM gateway_settings WHERE id = 1"));
        Assert.Equal(1L, Scalar(dir, "SELECT COUNT(*) FROM gateway_settings"));
    }

    /// <summary>Nothing is generated until it is needed: a new store has no stored token and no password.</summary>
    [Fact]
    public void NewStore_HasNoTokenAndNoPassword()
    {
        using TempDataDir dir = new();

        GatewaySettingsStore store = new(dir.Path);

        Assert.Null(store.StoredToken);
        Assert.Null(store.StoredPasswordHash);
    }

    /// <summary>EnsureToken generates 48 lowercase hex characters, stores them, and hands back the same token on every later call and every reopen.</summary>
    [Fact]
    public void EnsureToken_GeneratesOnce_AndPersists()
    {
        using TempDataDir dir = new();
        GatewaySettingsStore store = new(dir.Path);

        string token = store.EnsureToken();

        Assert.Matches(Token48Hex(), token);
        Assert.Equal(token, store.EnsureToken());
        Assert.Equal(token, new GatewaySettingsStore(dir.Path).StoredToken);
        Assert.Equal(token, Scalar(dir, "SELECT token FROM gateway_settings"));
    }

    /// <summary>Rotation replaces the stored token with a new 48-hex one and keeps the Instance id.</summary>
    [Fact]
    public void RotateToken_ReplacesToken_AndKeepsInstanceId()
    {
        using TempDataDir dir = new();
        GatewaySettingsStore store = new(dir.Path);
        string before = store.EnsureToken();

        string after = store.RotateToken();

        Assert.NotEqual(before, after);
        Assert.Matches(Token48Hex(), after);
        GatewaySettingsStore reopened = new(dir.Path);
        Assert.Equal(after, reopened.StoredToken);
        Assert.Equal(store.InstanceId, reopened.InstanceId);
    }

    /// <summary>Rotating before any token exists still stores a token.</summary>
    [Fact]
    public void RotateToken_WithNoToken_StoresOne()
    {
        using TempDataDir dir = new();
        GatewaySettingsStore store = new(dir.Path);

        string token = store.RotateToken();

        Assert.Equal(token, new GatewaySettingsStore(dir.Path).StoredToken);
    }

    /// <summary>A change stamps <c>updated_at</c> from the injected clock.</summary>
    [Fact]
    public void RotateToken_StampsUpdatedAt_FromTheClock()
    {
        using TempDataDir dir = new();
        FakeTimeProvider clock = new(Start);
        GatewaySettingsStore store = new(dir.Path, clock);
        clock.Advance(TimeSpan.FromHours(2));

        store.RotateToken();

        string? stamp = Scalar(dir, "SELECT updated_at FROM gateway_settings") as string;
        Assert.NotNull(stamp);
        Assert.Equal(Start.AddHours(2), DateTimeOffset.Parse(stamp, CultureInfo.InvariantCulture));
    }

    /// <summary>A stored password is a salted PBKDF2 hash that verifies, never the clear text.</summary>
    [Fact]
    public void SetPassword_StoresSaltedHash_ThatVerifies()
    {
        using TempDataDir dir = new();
        GatewaySettingsStore store = new(dir.Path);

        store.SetPassword("a long passphrase");

        byte[]? hash = new GatewaySettingsStore(dir.Path).StoredPasswordHash;
        Assert.NotNull(hash);
        Assert.Equal(48, hash.Length);
        Assert.True(GatewaySecrets.VerifyPassword("a long passphrase", hash));
        Assert.False(GatewaySecrets.VerifyPassword("another passphrase", hash));
    }

    /// <summary>Setting a password leaves the token and the Instance id as they were.</summary>
    [Fact]
    public void SetPassword_KeepsTokenAndInstanceId()
    {
        using TempDataDir dir = new();
        GatewaySettingsStore store = new(dir.Path);
        string token = store.EnsureToken();
        string instanceId = store.InstanceId;

        store.SetPassword("pw-for-the-gateway");

        GatewaySettingsStore reopened = new(dir.Path);
        Assert.Equal(token, reopened.StoredToken);
        Assert.Equal(instanceId, reopened.InstanceId);
    }
}
