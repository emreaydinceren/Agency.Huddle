namespace Agency.Huddle.App.Feed;

/// <summary>A device asked for admin access and waits for an admin's approval.</summary>
/// <param name="RequestId">The pairing request's id.</param>
/// <param name="DeviceName">The name the device gave itself, untrusted.</param>
/// <param name="Address">The address the request came from.</param>
/// <param name="Ingress">How it reached Huddle, such as <c>Direct</c> or <c>Managed</c>.</param>
internal sealed record AdminRequestedPayload(string RequestId, string DeviceName, string Address, string Ingress) : FeedPayload;
