namespace Agency.Huddle.App.Feed;

/// <summary>A device lost its access, or its token was replaced.</summary>
/// <param name="DeviceId">The device's id.</param>
/// <param name="DeviceName">The device's name, untrusted.</param>
/// <param name="Action">What happened: <c>revoked</c>, <c>removed</c> or <c>rotated</c>.</param>
internal sealed record DeviceRevokedPayload(string DeviceId, string DeviceName, string Action) : FeedPayload;
