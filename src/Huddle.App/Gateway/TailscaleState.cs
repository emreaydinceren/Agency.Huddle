namespace Agency.Huddle.App.Gateway;

/// <summary>What <c>tailscale status --json</c> says about the local node (Spec 6.3).</summary>
internal enum TailscaleState
{
    /// <summary>The CLI could not be started.</summary>
    Missing,

    /// <summary>The CLI failed, timed out or printed something unreadable.</summary>
    NotRunning,

    /// <summary><c>BackendState</c> is <c>NeedsLogin</c>.</summary>
    NeedsLogin,

    /// <summary><c>BackendState</c> is <c>Stopped</c>.</summary>
    Stopped,

    /// <summary><c>BackendState</c> is <c>Running</c>.</summary>
    Running,
}
