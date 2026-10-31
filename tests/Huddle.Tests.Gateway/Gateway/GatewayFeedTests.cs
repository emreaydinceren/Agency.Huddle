using Agency.Huddle.App.Feed;
using Agency.Huddle.App.Gateway;
using Agency.Huddle.Tests.Feed;
using Microsoft.Extensions.Time.Testing;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>Remote access spec 6.14: when pairing requests and devices post to the Feed, who posts, whether it notifies, and when it resolves.</summary>
public sealed class GatewayFeedTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 10, 30, 9, 0, 0, TimeSpan.Zero);
    private static readonly string[] Limited = [GatewayScopes.Read, GatewayScopes.Write];

    private readonly TempDataDir dir = new();
    private readonly RecordingFeedPublisher feed = new();
    private readonly FakeTimeProvider clock = new(Start);
    private readonly DeviceRegistry devices;
    private readonly PairingService pairing;

    /// <summary>Wires a registry and a pairing service to one recording publisher.</summary>
    public GatewayFeedTests()
    {
        GatewayFeed gatewayFeed = new(this.feed);
        this.devices = new DeviceRegistry(this.dir.Path, this.clock, gatewayFeed);
        this.pairing = new PairingService(this.devices, this.clock, gatewayFeed);
    }

    /// <inheritdoc />
    public void Dispose() => this.dir.Dispose();

    private static PairingService.Candidate Candidate(TestDevice device, string[]? scopes = null, string name = "iPad") =>
        new(device.DeviceId, device.PublicKey, scopes ?? Limited, name, "100.64.0.7", "ua/1", "Managed", null);

    private static DeviceRegistry.Registration Register(TestDevice device, string via) =>
        new(device.DeviceId, device.PublicKey, "iPad", Limited, via, null, "100.64.0.7", "ua/1");

    private PairingRequest Open(TestDevice device, string[]? scopes = null)
    {
        PairingRequest? request = this.pairing.Open(Candidate(device, scopes));
        Assert.NotNull(request);
        return request;
    }

    /// <summary>A new request posts one <c>PairingRequested</c> Notification from Huddle, about that request, in the Devices Category.</summary>
    [Fact]
    public void Open_LimitedRequest_PostsPairingRequestedAsHuddleWithNotify()
    {
        PairingRequest request = this.Open(new TestDevice());

        FeedMessage post = Assert.Single(this.feed.Posts);
        Assert.Equal(FeedSourceType.PairingRequested, post.SourceType);
        Assert.True(post.Notify);
        Assert.Equal(FeedActor.Huddle, post.Actor);
        Assert.Equal("pairing:" + request.RequestId, post.TargetKey);
        Assert.Equal(new PairingRequestedPayload(request.RequestId, "iPad", "100.64.0.7", "Managed"), post.Payload);
        Assert.Equal(FeedCategory.Devices, FeedCategories.Of(post.SourceType));
    }

    /// <summary>A request that asks for admin posts <c>AdminRequested</c> instead, also a Notification.</summary>
    [Fact]
    public void Open_AdminRequest_PostsAdminRequestedWithNotify()
    {
        PairingRequest request = this.Open(new TestDevice(), [GatewayScopes.Admin]);

        FeedMessage post = Assert.Single(this.feed.Posts);
        Assert.Equal(FeedSourceType.AdminRequested, post.SourceType);
        Assert.True(post.Notify);
        Assert.Equal(FeedActor.Huddle, post.Actor);
        Assert.Equal(new AdminRequestedPayload(request.RequestId, "iPad", "100.64.0.7", "Managed"), post.Payload);
    }

    /// <summary>The same device opening the identical request again returns the pending one and posts nothing new.</summary>
    [Fact]
    public void Open_IdenticalPending_PostsOnce()
    {
        TestDevice device = new();
        _ = this.Open(device);
        _ = this.Open(device);

        _ = Assert.Single(this.feed.Posts);
    }

    /// <summary>Approving a request resolves its item and posts <c>DevicePaired</c>, quietly, because the ingress is not loopback.</summary>
    [Fact]
    public void Approve_ResolvesTheRequestAndPostsDevicePairedWithoutNotify()
    {
        TestDevice device = new();
        PairingRequest request = this.Open(device);

        PairingService.ApprovalResult result = this.pairing.Approve(request.RequestId, [GatewayScopes.Admin, GatewayScopes.Pairing, GatewayScopes.Read, GatewayScopes.Write]);

        Assert.Equal(PairingService.ApprovalOutcome.Approved, result.Outcome);
        Assert.Contains(new FeedResolution("pairing:" + request.RequestId, FeedSourceType.PairingRequested), this.feed.Resolutions);
        FeedMessage paired = Assert.Single(this.feed.Posts, post => post.SourceType == FeedSourceType.DevicePaired);
        Assert.False(paired.Notify);
        Assert.Equal(FeedActor.Huddle, paired.Actor);
        Assert.Equal("device:" + device.DeviceId, paired.TargetKey);
        Assert.Equal(new DevicePairedPayload(device.DeviceId, "iPad", "approval"), paired.Payload);
    }

    /// <summary>Approving an admin request resolves the <c>AdminRequested</c> item, not a <c>PairingRequested</c> one.</summary>
    [Fact]
    public void Approve_AdminRequest_ResolvesAdminRequested()
    {
        PairingRequest request = this.Open(new TestDevice(), [GatewayScopes.Admin]);

        _ = this.pairing.Approve(request.RequestId, [GatewayScopes.Admin, GatewayScopes.Pairing, GatewayScopes.Read, GatewayScopes.Write]);

        FeedResolution resolution = Assert.Single(this.feed.Resolutions);
        Assert.Equal(new FeedResolution("pairing:" + request.RequestId, FeedSourceType.AdminRequested), resolution);
    }

    /// <summary>Rejecting a request resolves its item and posts no <c>DevicePaired</c>.</summary>
    [Fact]
    public void Reject_ResolvesTheRequestAndPostsNoDevicePaired()
    {
        PairingRequest request = this.Open(new TestDevice());

        bool rejected = this.pairing.Reject(request.RequestId, [GatewayScopes.Pairing]);

        Assert.True(rejected);
        FeedResolution resolution = Assert.Single(this.feed.Resolutions);
        Assert.Equal(new FeedResolution("pairing:" + request.RequestId, FeedSourceType.PairingRequested), resolution);
        Assert.DoesNotContain(this.feed.Posts, post => post.SourceType == FeedSourceType.DevicePaired);
    }

    /// <summary>A request nobody answers resolves its item once the five minutes pass, and the next look at the queue is what notices.</summary>
    [Fact]
    public void Expiry_ResolvesTheRequestOnTheNextLook()
    {
        PairingRequest request = this.Open(new TestDevice());
        Assert.Empty(this.feed.Resolutions);

        this.clock.Advance(PairingService.RequestLifetime + TimeSpan.FromSeconds(1));
        Assert.Empty(this.pairing.Pending());

        FeedResolution resolution = Assert.Single(this.feed.Resolutions);
        Assert.Equal(new FeedResolution("pairing:" + request.RequestId, FeedSourceType.PairingRequested), resolution);
    }

    /// <summary>An expired request is resolved once, however often the queue is looked at.</summary>
    [Fact]
    public void Expiry_LookedAtTwice_ResolvesOnce()
    {
        _ = this.Open(new TestDevice());
        this.clock.Advance(PairingService.RequestLifetime + TimeSpan.FromSeconds(1));

        _ = this.pairing.Pending();
        _ = this.pairing.Pending();

        _ = Assert.Single(this.feed.Resolutions);
    }

    /// <summary>A request replaced by one with different scopes resolves its item, and the new one posts its own.</summary>
    [Fact]
    public void Open_Superseded_ResolvesTheOldItemAndPostsTheNew()
    {
        TestDevice device = new();
        PairingRequest first = this.Open(device);

        PairingRequest second = this.Open(device, [GatewayScopes.Admin]);

        Assert.Contains(new FeedResolution("pairing:" + first.RequestId, FeedSourceType.PairingRequested), this.feed.Resolutions);
        Assert.Equal(2, this.feed.Posts.Count);
        Assert.Equal("pairing:" + second.RequestId, this.feed.Posts[1].TargetKey);
        Assert.Equal(FeedSourceType.AdminRequested, this.feed.Posts[1].SourceType);
    }

    /// <summary>A device that pairs through a non-loopback route (tailscale here) posts <c>DevicePaired</c> without a Notification.</summary>
    [Fact]
    public void Pair_Tailscale_PostsDevicePairedQuietly()
    {
        TestDevice device = new();

        _ = this.devices.Pair(Register(device, "tailscale"));

        FeedMessage post = Assert.Single(this.feed.Posts);
        Assert.Equal(FeedSourceType.DevicePaired, post.SourceType);
        Assert.False(post.Notify);
        Assert.Equal(new DevicePairedPayload(device.DeviceId, "iPad", "tailscale"), post.Payload);
    }

    /// <summary>A device paired from this machine's own loopback posts nothing.</summary>
    [Fact]
    public void Pair_Loopback_PostsNothing()
    {
        _ = this.devices.Pair(Register(new TestDevice(), "loopback"));

        Assert.Empty(this.feed.Posts);
    }

    /// <summary>A device that is already paired and pairs again (a new name or scope) is not announced a second time.</summary>
    [Fact]
    public void Pair_AlreadyPaired_PostsOnce()
    {
        TestDevice device = new();
        _ = this.devices.Pair(Register(device, "tailscale"));
        _ = this.devices.Pair(Register(device, "tailscale"));

        _ = Assert.Single(this.feed.Posts, post => post.SourceType == FeedSourceType.DevicePaired);
    }

    /// <summary>Revoke, remove and rotate each post <c>DeviceRevoked</c> with the matching action, from Huddle, quietly.</summary>
    [Theory]
    [InlineData("revoked")]
    [InlineData("removed")]
    [InlineData("rotated")]
    public void Ended_EachAction_PostsDeviceRevokedQuietly(string action)
    {
        TestDevice device = new();
        _ = this.devices.Pair(Register(device, "loopback"));

        switch (action)
        {
            case "revoked":
                Assert.True(this.devices.Revoke(device.DeviceId));
                break;
            case "removed":
                Assert.True(this.devices.Remove(device.DeviceId));
                break;
            default:
                Assert.NotNull(this.devices.Rotate(device.DeviceId));
                break;
        }

        FeedMessage post = Assert.Single(this.feed.Posts);
        Assert.Equal(FeedSourceType.DeviceRevoked, post.SourceType);
        Assert.False(post.Notify);
        Assert.Equal(FeedActor.Huddle, post.Actor);
        Assert.Equal("device:" + device.DeviceId, post.TargetKey);
        Assert.Equal(new DeviceRevokedPayload(device.DeviceId, "iPad", action), post.Payload);
    }

    /// <summary>Revoking a device that is not there posts nothing.</summary>
    [Fact]
    public void Revoke_UnknownDevice_PostsNothing()
    {
        Assert.False(this.devices.Revoke("nobody"));

        Assert.Empty(this.feed.Posts);
    }

    /// <summary>A Feed that throws never takes pairing down: the request still opens and the device still pairs.</summary>
    [Fact]
    public void FailingFeed_DoesNotBreakPairing()
    {
        RecordingFeedPublisher broken = new() { Throws = true };
        GatewayFeed gatewayFeed = new(broken);
        DeviceRegistry registry = new(this.dir.Path, this.clock, gatewayFeed);
        PairingService service = new(registry, this.clock, gatewayFeed);

        PairingRequest? request = service.Open(Candidate(new TestDevice()));

        Assert.NotNull(request);
        Assert.Equal(PairingService.State.Pending, service.StateOf(request.RequestId));
    }
}
