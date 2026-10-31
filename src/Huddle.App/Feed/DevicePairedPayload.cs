namespace Agency.Huddle.App.Feed;

/// <summary>A device was paired from somewhere other than this machine.</summary>
/// <param name="DeviceId">The device's id.</param>
/// <param name="DeviceName">The device's name, untrusted.</param>
/// <param name="PairedVia">How it was paired, such as <c>approval</c> or <c>tailscale</c>.</param>
internal sealed record DevicePairedPayload(string DeviceId, string DeviceName, string PairedVia) : FeedPayload;
