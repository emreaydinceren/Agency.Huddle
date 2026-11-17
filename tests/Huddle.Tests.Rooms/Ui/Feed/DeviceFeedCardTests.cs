using Agency.Huddle.App.Components.Feed;
using Agency.Huddle.App.Feed;
using Bunit;

namespace Agency.Huddle.Tests.Ui;

/// <summary>The words and the link of the four remote-access Feed cards (Remote access spec 6.14): each reads as one short sentence and opens Settings > Remote access.</summary>
public sealed class DeviceFeedCardTests
{
    private const string SettingsAddress = "/settings/remoteaccess";

    private static string Words(string text) => string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static FeedItem Item(FeedSourceType type, FeedPayload payload, bool notify, DateTimeOffset? resolved = null) =>
        FeedTestData.Item(type, payload, notify, resolved: resolved, actor: FeedActor.Huddle) with { TargetKey = "pairing:req-1" };

    private static IRenderedComponent<FeedCard> Render(MudBunitContext ctx, FeedItem item) =>
        ctx.Render<FeedCard>(parameters =>
        {
            parameters.Add(card => card.Group, new FeedGroup([item]));
            parameters.Add(card => card.TabSeenUpTo, 0L);
            parameters.Add(card => card.Expanded, false);
        });

    /// <summary>Each of the four items reads as the pinned sentence, with the device's name as a chip that links to Settings > Remote access.</summary>
    [Theory]
    [InlineData(FeedSourceType.PairingRequested, "iPad wants to use Huddle")]
    [InlineData(FeedSourceType.AdminRequested, "iPad asks for admin access")]
    [InlineData(FeedSourceType.DevicePaired, "iPad was paired")]
    [InlineData(FeedSourceType.DeviceRevoked, "iPad was revoked")]
    public async Task Card_DeviceItem_ReadsAsThePinnedSentenceAndLinksToSettings(FeedSourceType type, string sentence)
    {
        using FeedCardHost host = await FeedCardHost.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        await using MudBunitContext ctx = host.NewContext();
        FeedItem item = Item(type, SamplePayload(type), notify: type is FeedSourceType.PairingRequested or FeedSourceType.AdminRequested);

        IRenderedComponent<FeedCard> card = Render(ctx, item);

        Assert.Equal(sentence, Words(card.Find(".feed-card-summary").TextContent));
        Assert.Equal("iPad", Words(card.Find(".feed-chip-device").TextContent));
        Assert.Equal(SettingsAddress, card.Find(".feed-chip-device a").GetAttribute("href"));
    }

    /// <summary>A removed or a rotated device says so in its own words.</summary>
    [Theory]
    [InlineData("removed", "iPad was removed")]
    [InlineData("rotated", "iPad got a new token")]
    public async Task Card_DeviceRevoked_NamesTheAction(string action, string sentence)
    {
        using FeedCardHost host = await FeedCardHost.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        await using MudBunitContext ctx = host.NewContext();
        FeedItem item = Item(FeedSourceType.DeviceRevoked, new DeviceRevokedPayload("dev-1", "iPad", action), notify: false);

        IRenderedComponent<FeedCard> card = Render(ctx, item);

        Assert.Equal(sentence, Words(card.Find(".feed-card-summary").TextContent));
    }

    /// <summary>The meta line says "Devices", and the card has no Task, Room or Team chip.</summary>
    [Fact]
    public async Task Card_DeviceItem_ContextWordIsDevices()
    {
        using FeedCardHost host = await FeedCardHost.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        await using MudBunitContext ctx = host.NewContext();

        IRenderedComponent<FeedCard> card = Render(ctx, Item(FeedSourceType.DevicePaired, SamplePayload(FeedSourceType.DevicePaired), notify: false));

        Assert.Equal("Devices", Words(card.Find(".feed-meta-context").TextContent));
        Assert.Empty(card.FindAll(".feed-team-chip"));
        Assert.Empty(card.FindAll(".feed-meta-room"));
    }

    /// <summary>An open request shows the Waiting pill, and a resolved one shows Done; an Activity item (paired, revoked) shows neither.</summary>
    [Fact]
    public async Task Card_Request_WaitingThenDone_AndActivityHasNoPill()
    {
        using FeedCardHost host = await FeedCardHost.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        await using MudBunitContext ctx = host.NewContext();
        FeedItem open = Item(FeedSourceType.PairingRequested, SamplePayload(FeedSourceType.PairingRequested), notify: true);
        FeedItem done = open with { ResolvedAt = FeedTestData.Now };
        FeedItem activity = Item(FeedSourceType.DeviceRevoked, SamplePayload(FeedSourceType.DeviceRevoked), notify: false);

        Assert.True(Render(ctx, open).Find(".feed-pill").ClassList.Contains("feed-pill-waiting"));
        Assert.Equal($"Waiting for you · {FeedTime.CardAge(open.At, open.At)}", FeedCardText.PillOf(open, open.At, TimeZoneInfo.Utc, null)?.Text);
        Assert.True(Render(ctx, done).Find(".feed-pill").ClassList.Contains("feed-pill-done"));
        Assert.Empty(Render(ctx, activity).FindAll(".feed-pill"));
    }

    /// <summary>The status line under a Human's give-up trace says it was missed and removed without running.</summary>
    [Fact]
    public void StatusOf_JobGaveUp_SaysItWasRemovedWithoutRunning()
    {
        FeedItem item = Item(FeedSourceType.JobGaveUp, new JobGaveUpPayload("job-1", "Digest", "it failed three times", null), notify: true);

        FeedStatusLine? line = FeedSummary.StatusOf(item, TimeZoneInfo.Utc);

        string missed = item.At.ToUniversalTime().ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        Assert.NotNull(line);
        Assert.Equal($"Missed {missed} · it was removed without running", line.Text);
    }

    /// <summary>The status line under a pairing request says who has to act; once resolved it says when it was done.</summary>
    [Fact]
    public void StatusOf_PairingRequest_WaitsForYouThenDone()
    {
        FeedItem open = Item(FeedSourceType.PairingRequested, SamplePayload(FeedSourceType.PairingRequested), notify: true);

        FeedStatusLine? waiting = FeedSummary.StatusOf(open, TimeZoneInfo.Utc);
        FeedStatusLine? done = FeedSummary.StatusOf(open with { ResolvedAt = open.At.AddMinutes(2) }, TimeZoneInfo.Utc);

        string asked = open.At.ToUniversalTime().ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        string resolved = open.At.AddMinutes(2).ToUniversalTime().ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        Assert.NotNull(waiting);
        Assert.Equal($"Asked {asked} · waiting for you", waiting.Text);
        Assert.NotNull(done);
        Assert.Equal($"Asked {asked} · done {resolved}", done.Text);
    }

    /// <summary>A device item's target is Settings > Remote access, whatever the device id.</summary>
    [Theory]
    [InlineData(FeedSourceType.PairingRequested)]
    [InlineData(FeedSourceType.AdminRequested)]
    [InlineData(FeedSourceType.DevicePaired)]
    [InlineData(FeedSourceType.DeviceRevoked)]
    public async Task Target_DeviceItem_OpensSettingsRemoteAccess(FeedSourceType type)
    {
        using FeedCardHost host = await FeedCardHost.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        FeedItem item = Item(type, SamplePayload(type), notify: false);

        string? address = FeedTarget.Of(item).Address(host.Library.Resolver);

        Assert.Equal(SettingsAddress, address);
    }

    private static FeedPayload SamplePayload(FeedSourceType type) => type switch
    {
        FeedSourceType.PairingRequested => new PairingRequestedPayload("req-1", "iPad", "100.64.0.7", "Managed"),
        FeedSourceType.AdminRequested => new AdminRequestedPayload("req-1", "iPad", "100.64.0.7", "Managed"),
        FeedSourceType.DevicePaired => new DevicePairedPayload("dev-1", "iPad", "approval"),
        _ => new DeviceRevokedPayload("dev-1", "iPad", "revoked"),
    };
}
