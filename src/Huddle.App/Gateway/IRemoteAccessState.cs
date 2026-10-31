namespace Agency.Huddle.App.Gateway;

/// <summary>Whether remote access is configured as running; the one question every host-side action asks (Spec 6.15).</summary>
internal interface IRemoteAccessState
{
    /// <summary>Whether <c>Bind</c> is not <c>loopback</c> or <c>Tailscale:Mode</c> is not <c>off</c>.</summary>
    bool IsConfigured { get; }
}
