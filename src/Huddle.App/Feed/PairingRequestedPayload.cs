namespace Agency.Huddle.App.Feed;

/// <summary>A device asked to pair and waits for the Human's approval.</summary>
/// <param name="RequestId">The pairing request's id.</param>
/// <param name="DeviceName">The name the device gave itself, untrusted.</param>
/// <param name="Address">The address the request came from.</param>
/// <param name="Ingress">How it reached Huddle, such as <c>Direct</c> or <c>Managed</c>.</param>
internal sealed record PairingRequestedPayload(string RequestId, string DeviceName, string Address, string Ingress) : FeedPayload;
