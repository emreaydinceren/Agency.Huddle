namespace Agency.Huddle.App.Gateway;

/// <summary>A running <c>tailscale</c> child (D-11): stdout and stderr merged into <see cref="Lines"/>.</summary>
internal interface ITailscaleChild
{
    /// <summary>The process id.</summary>
    int Id { get; }

    /// <summary>The output lines, ending when the process closes its streams.</summary>
    IAsyncEnumerable<string> Lines { get; }

    /// <summary>Completes with the exit code when the process ends.</summary>
    Task<int> Exited { get; }

    /// <summary>Kills the process and its tree at once (no Ctrl-C on Windows).</summary>
    ValueTask KillAsync();
}
