namespace Agency.Huddle.App.Gateway;

/// <summary>Which listener accepted the connection (Spec 6.2).</summary>
internal enum IngressListenerKind
{
    /// <summary>The app listener.</summary>
    App,

    /// <summary>The loopback listener that Tailscale Serve or Funnel proxies to.</summary>
    Managed,
}
