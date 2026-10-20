using Microsoft.Data.Sqlite;
using Agency.Huddle.App.Data;
using Agency.Huddle.Tests.NeedsYou;

namespace Agency.Huddle.Tests.Feed;

/// <summary>The Feed never stores Message text (Feed spec policy 2), proven end to end from a posted Message to the stored row.</summary>
public sealed class FeedNoMessageTextTests
{
    /// <summary>A Mention whose Message holds a sentinel leaves a stored row, and no column of any row contains the sentinel.</summary>
    [Fact]
    public async Task Mention_SentinelInMessage_IsInNoStoredRow()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        const string Sentinel = "ZEBRA-SENTINEL-4417";
        using FeedHarness harness = new();
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct, harness.Queue);
        await harness.Directory.InitializeAsync("You", ct);
        await harness.Writer.StartAsync(ct);
        User nova = await fixture.AddAgentAsync("Nova", ct);
        Room room = await fixture.CreateRoomAsync("Pricing", [nova], ct);

        _ = await fixture.Service.PostAsync(room.Id, nova.Id, $"@You please decide {Sentinel}", ct: ct);
        await harness.DrainAsync(ct);
        await harness.Writer.StopAsync(ct);

        using SqliteConnection connection = new($"Data Source={Path.Combine(harness.DataDir, "team.db")}");
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM feed_events WHERE source_type = 'room-mention';";
        long mentions = (long)(command.ExecuteScalar() ?? 0L);
        command.CommandText = "SELECT COUNT(*) FROM feed_events WHERE payload LIKE $needle OR target_key LIKE $needle OR actor LIKE $needle;";
        _ = command.Parameters.AddWithValue("$needle", "%" + Sentinel + "%");
        long leaks = (long)(command.ExecuteScalar() ?? 0L);

        Assert.Equal(1L, mentions);
        Assert.Equal(0L, leaks);
    }
}
