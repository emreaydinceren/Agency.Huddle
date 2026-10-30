using Agency.Huddle.App.Feed;

namespace Agency.Huddle.App.Gateway;

/// <summary>
/// Tells the Feed about pairing requests and devices (Spec 6.14). Every item is from Huddle, in the Devices Category. A Feed that fails
/// is never allowed to take pairing down with it.
/// </summary>
/// <param name="feed">Where the items go.</param>
internal sealed class GatewayFeed(IFeedPublisher feed)
{
    /// <summary>A pairing request opened: posts <c>PairingRequested</c>, or <c>AdminRequested</c> when it asks for admin. Both are Notifications.</summary>
    /// <param name="request">The request.</param>
    public void RequestOpened(PairingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        FeedSourceType type = TypeOf(request);
        FeedPayload payload = type == FeedSourceType.AdminRequested
            ? new AdminRequestedPayload(request.RequestId, request.Name, request.Address, request.Ingress)
            : new PairingRequestedPayload(request.RequestId, request.Name, request.Address, request.Ingress);
        Try(() => feed.Post(new FeedMessage(type, RequestKey(request), FeedActor.Huddle, true, payload)));
    }

    /// <summary>A pairing request ended (approved, rejected, expired or superseded): resolves its item.</summary>
    /// <param name="request">The request.</param>
    public void RequestEnded(PairingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        Try(() => feed.Resolve(RequestKey(request), TypeOf(request)));
    }

    /// <summary>A device was paired: posts <c>DevicePaired</c> unless it paired as <c>loopback</c>. Not a Notification.</summary>
    /// <param name="device">The device.</param>
    public void Paired(DeviceRecord device)
    {
        ArgumentNullException.ThrowIfNull(device);

        if (string.Equals(device.PairedVia, "loopback", StringComparison.Ordinal))
        {
            return;
        }

        Try(() => feed.Post(new FeedMessage(
            FeedSourceType.DevicePaired,
            DeviceKey(device),
            FeedActor.Huddle,
            false,
            new DevicePairedPayload(device.DeviceId, device.Name, device.PairedVia))));
    }

    /// <summary>A device was revoked, removed or rotated: posts <c>DeviceRevoked</c>. Not a Notification.</summary>
    /// <param name="device">The device as it was.</param>
    /// <param name="action"><c>revoked</c>, <c>removed</c> or <c>rotated</c>.</param>
    public void Ended(DeviceRecord device, string action)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(action);

        Try(() => feed.Post(new FeedMessage(
            FeedSourceType.DeviceRevoked,
            DeviceKey(device),
            FeedActor.Huddle,
            false,
            new DeviceRevokedPayload(device.DeviceId, device.Name, action))));
    }

    private static FeedSourceType TypeOf(PairingRequest request) =>
        request.IsAdminRequest ? FeedSourceType.AdminRequested : FeedSourceType.PairingRequested;

    private static string RequestKey(PairingRequest request) => "pairing:" + request.RequestId;

    private static string DeviceKey(DeviceRecord device) => "device:" + device.DeviceId;

    private static void Try(Action post)
    {
        try
        {
            post();
        }
        catch (Exception)
        {
            // The Feed is a courtesy to the Human; a publisher that throws must not stop a pairing or a revocation.
        }
    }
}
