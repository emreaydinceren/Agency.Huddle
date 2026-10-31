using System.Buffers.Text;
using System.Security.Cryptography;
using Agency.Huddle.App.Gateway;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Time.Testing;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>Spec 7.1 and 6.7: the <c>gateway_devices</c> table of <c>team.db</c>, device tokens stored hashed, and revoke, remove, rotate and prune.</summary>
public sealed class DeviceRegistryTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 30, 9, 0, 0, TimeSpan.Zero);

    private static DeviceRegistry.Registration Register(TestDevice device, string via = "approval", string name = "Windows Desktop")
        => new(device.DeviceId, device.PublicKey, name, [GatewayScopes.Read, GatewayScopes.Write], via, null, "203.0.113.9", "ua/1");

    private static object? Scalar(TempDataDir dir, string sql)
    {
        using SqliteConnection connection = new($"Data Source={Path.Combine(dir.Path, "team.db")}");
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        object? value = command.ExecuteScalar();
        return value is DBNull ? null : value;
    }

    /// <summary>A new registry creates <c>gateway_devices</c> with every column of Spec 7.1.</summary>
    [Fact]
    public void Create_MakesTheSpecTable()
    {
        using TempDataDir dir = new();
        _ = new DeviceRegistry(dir.Path, new FakeTimeProvider(Start));

        object? columns = Scalar(dir, """
            SELECT group_concat(name) FROM (SELECT name FROM pragma_table_info('gateway_devices') ORDER BY name)
            """);

        Assert.Equal(
            "device_id,last_seen_at,last_seen_from,name,paired_at,paired_via,public_key,scopes,tailscale_login,token_hash,user_agent",
            columns);
    }

    /// <summary>Pairing a new device returns a 32-byte base64url token and stores the device with its scopes joined and sorted.</summary>
    [Fact]
    public void Pair_NewDevice_ReturnsTokenAndStoresDevice()
    {
        using TempDataDir dir = new();
        DeviceRegistry registry = new(dir.Path, new FakeTimeProvider(Start));
        TestDevice device = new();

        string? token = registry.Pair(Register(device));
        DeviceRecord? found = registry.Find(device.DeviceId);

        Assert.NotNull(token);
        Assert.Equal(32, Base64Url.DecodeFromChars(token).Length);
        Assert.NotNull(found);
        Assert.Equal([GatewayScopes.Read, GatewayScopes.Write], found.Scopes);
        Assert.Equal("approval", found.PairedVia);
        Assert.True(found.HasToken);
        Assert.Equal(device.PublicKey, found.PublicKey);
        Assert.Equal(Start, found.PairedAt);
        Assert.Equal("operator.read,operator.write", Scalar(dir, "SELECT scopes FROM gateway_devices"));
    }

    /// <summary>The token is stored only as its SHA-256 hash: the table holds 32 hash bytes and the clear token appears in no column.</summary>
    [Fact]
    public void Pair_StoresOnlyTheTokenHash()
    {
        using TempDataDir dir = new();
        DeviceRegistry registry = new(dir.Path, new FakeTimeProvider(Start));
        TestDevice device = new();

        string token = registry.Pair(Register(device)) ?? string.Empty;

        Assert.Equal(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)), Scalar(dir, "SELECT token_hash FROM gateway_devices"));
        Assert.Equal(0L, Scalar(dir, $"SELECT count(*) FROM gateway_devices WHERE instr(name || user_agent || last_seen_from, '{token}') > 0"));
    }

    /// <summary>A token finds its device; a wrong or empty token finds nothing.</summary>
    [Fact]
    public void FindByToken_MatchesOnlyTheCurrentToken()
    {
        using TempDataDir dir = new();
        DeviceRegistry registry = new(dir.Path, new FakeTimeProvider(Start));
        TestDevice device = new();
        string token = registry.Pair(Register(device)) ?? string.Empty;

        Assert.Equal(device.DeviceId, registry.FindByToken(token)?.DeviceId);
        Assert.Null(registry.FindByToken("wrong"));
        Assert.Null(registry.FindByToken(string.Empty));
    }

    /// <summary>Pairing an already paired device updates scopes and keeps its token: no new token is returned and the old one still works.</summary>
    [Fact]
    public void Pair_ExistingDevice_WidensScopesAndKeepsTheToken()
    {
        using TempDataDir dir = new();
        DeviceRegistry registry = new(dir.Path, new FakeTimeProvider(Start));
        TestDevice device = new();
        string token = registry.Pair(Register(device)) ?? string.Empty;

        string? second = registry.Pair(Register(device) with { Scopes = GatewayScopes.AdminSet });

        Assert.Null(second);
        Assert.Equal(device.DeviceId, registry.FindByToken(token)?.DeviceId);
        DeviceRecord? widened = registry.Find(device.DeviceId);
        Assert.NotNull(widened);
        Assert.True(GatewayScopes.Holds(widened.Scopes, GatewayScopes.Admin));
    }

    /// <summary>A device name is cut to 60 characters (the column is 1 to 60).</summary>
    [Fact]
    public void Pair_LongName_IsCutTo60()
    {
        using TempDataDir dir = new();
        DeviceRegistry registry = new(dir.Path, new FakeTimeProvider(Start));
        TestDevice device = new();

        _ = registry.Pair(Register(device, name: new string('n', 200)));

        Assert.Equal(60, registry.Find(device.DeviceId)?.Name.Length);
    }

    /// <summary>The device survives a new registry over the same directory: the table is the store.</summary>
    [Fact]
    public void Find_AfterReopen_ReadsTheSameRow()
    {
        using TempDataDir dir = new();
        TestDevice device = new();
        string token = new DeviceRegistry(dir.Path, new FakeTimeProvider(Start)).Pair(Register(device)) ?? string.Empty;

        DeviceRegistry reopened = new(dir.Path, new FakeTimeProvider(Start));

        Assert.Equal(device.DeviceId, reopened.FindByToken(token)?.DeviceId);
    }

    /// <summary>Touch records the last handshake's time, address and user agent.</summary>
    [Fact]
    public void Touch_UpdatesLastSeen()
    {
        using TempDataDir dir = new();
        FakeTimeProvider clock = new(Start);
        DeviceRegistry registry = new(dir.Path, clock);
        TestDevice device = new();
        _ = registry.Pair(Register(device));
        clock.Advance(TimeSpan.FromHours(2));

        registry.Touch(device.DeviceId, "198.51.100.1", "ua/2");

        DeviceRecord? found = registry.Find(device.DeviceId);
        Assert.Equal(Start.AddHours(2), found?.LastSeenAt);
        Assert.Equal("198.51.100.1", found?.LastSeenFrom);
        Assert.Equal("ua/2", found?.UserAgent);
    }

    /// <summary>Revoke removes the token (the device stays listed, scopes unchanged) and the old token stops working.</summary>
    [Fact]
    public void Revoke_RemovesTheToken()
    {
        using TempDataDir dir = new();
        DeviceRegistry registry = new(dir.Path, new FakeTimeProvider(Start));
        TestDevice device = new();
        string token = registry.Pair(Register(device)) ?? string.Empty;

        bool revoked = registry.Revoke(device.DeviceId);

        Assert.True(revoked);
        Assert.Null(registry.FindByToken(token));
        DeviceRecord? found = registry.Find(device.DeviceId);
        Assert.NotNull(found);
        Assert.False(found.HasToken);
        Assert.Null(Scalar(dir, "SELECT token_hash FROM gateway_devices"));
    }

    /// <summary>Remove deletes the device; revoking or removing an unknown device reports false.</summary>
    [Fact]
    public void Remove_DeletesTheDevice()
    {
        using TempDataDir dir = new();
        DeviceRegistry registry = new(dir.Path, new FakeTimeProvider(Start));
        TestDevice device = new();
        string token = registry.Pair(Register(device)) ?? string.Empty;

        Assert.True(registry.Remove(device.DeviceId));

        Assert.Null(registry.Find(device.DeviceId));
        Assert.Null(registry.FindByToken(token));
        Assert.False(registry.Remove(device.DeviceId));
        Assert.False(registry.Revoke(device.DeviceId));
    }

    /// <summary>Rotate issues a new token within the same scopes; the old token stops working and the new one works.</summary>
    [Fact]
    public void Rotate_IssuesNewTokenAndKillsTheOldOne()
    {
        using TempDataDir dir = new();
        DeviceRegistry registry = new(dir.Path, new FakeTimeProvider(Start));
        TestDevice device = new();
        string oldToken = registry.Pair(Register(device)) ?? string.Empty;

        string? newToken = registry.Rotate(device.DeviceId);

        Assert.NotNull(newToken);
        Assert.NotEqual(oldToken, newToken);
        Assert.Null(registry.FindByToken(oldToken));
        DeviceRecord? found = registry.FindByToken(newToken);
        Assert.Equal([GatewayScopes.Read, GatewayScopes.Write], found?.Scopes);
        Assert.Null(registry.Rotate("unknown"));
    }

    /// <summary>List returns every device, newest pairing first.</summary>
    [Fact]
    public void List_ReturnsNewestPairingFirst()
    {
        using TempDataDir dir = new();
        FakeTimeProvider clock = new(Start);
        DeviceRegistry registry = new(dir.Path, clock);
        TestDevice first = new();
        TestDevice second = new();
        _ = registry.Pair(Register(first));
        clock.Advance(TimeSpan.FromMinutes(1));
        _ = registry.Pair(Register(second));

        IReadOnlyList<DeviceRecord> devices = registry.List();

        Assert.Equal([second.DeviceId, first.DeviceId], devices.Select(device => device.DeviceId));
    }

    /// <summary>C-11: loopback and bootstrap devices unseen for 30 days are deleted; seen ones and other pairings are kept.</summary>
    [Fact]
    public void PruneStale_DeletesOnlyOldLoopbackAndBootstrapDevices()
    {
        using TempDataDir dir = new();
        FakeTimeProvider clock = new(Start);
        DeviceRegistry registry = new(dir.Path, clock);
        TestDevice oldLoopback = new();
        TestDevice oldBootstrap = new();
        TestDevice oldApproved = new();
        TestDevice seenLoopback = new();
        _ = registry.Pair(Register(oldLoopback, via: "loopback"));
        _ = registry.Pair(Register(oldBootstrap, via: "bootstrap"));
        _ = registry.Pair(Register(oldApproved, via: "approval"));
        _ = registry.Pair(Register(seenLoopback, via: "loopback"));
        clock.Advance(TimeSpan.FromDays(29));
        registry.Touch(seenLoopback.DeviceId, "127.0.0.1", null);
        clock.Advance(TimeSpan.FromDays(2));

        int pruned = registry.PruneStale();

        Assert.Equal(2, pruned);
        Assert.Null(registry.Find(oldLoopback.DeviceId));
        Assert.Null(registry.Find(oldBootstrap.DeviceId));
        Assert.NotNull(registry.Find(oldApproved.DeviceId));
        Assert.NotNull(registry.Find(seenLoopback.DeviceId));
    }

    /// <summary>A loopback device unseen for 29 days is not yet pruned.</summary>
    [Fact]
    public void PruneStale_Under30Days_KeepsTheDevice()
    {
        using TempDataDir dir = new();
        FakeTimeProvider clock = new(Start);
        DeviceRegistry registry = new(dir.Path, clock);
        TestDevice device = new();
        _ = registry.Pair(Register(device, via: "loopback"));
        clock.Advance(TimeSpan.FromDays(29));

        Assert.Equal(0, registry.PruneStale());
        Assert.NotNull(registry.Find(device.DeviceId));
    }
}
