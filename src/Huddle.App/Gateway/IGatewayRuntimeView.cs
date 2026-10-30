namespace Agency.Huddle.App.Gateway;

/// <summary>What the running gateway looks like from outside, for Settings and the Security check (Spec 6.12, 8.9).</summary>
internal interface IGatewayRuntimeView
{
    /// <summary>The state of the local Tailscale node.</summary>
    TailscaleState TailscaleState { get; }

    /// <summary>The Tailscale login of the node owner, or <see langword="null"/>.</summary>
    string? TailscaleOwnerLogin { get; }

    /// <summary>The origin other devices open, such as <c>https://desktop-a.tail1234.ts.net:5101</c>, or <see langword="null"/> when there is none.</summary>
    string? PublicUrl { get; }

    /// <summary>Why the listener is not fully up (bind refused, port busy), or <see langword="null"/> when it is running.</summary>
    string? ListenerReason { get; }

    /// <summary>Tailscale Serve routes that point at the app port, set up by hand.</summary>
    IReadOnlyList<TailscaleServeStatus.Route> StrayRoutes { get; }

    /// <summary>Whether a request arrived under a bare-name host (a name with no dot) since start.</summary>
    bool BareNameHostInUse { get; }
}
