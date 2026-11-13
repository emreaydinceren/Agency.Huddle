namespace Agency.Huddle.App.Teams;

/// <summary>One step-6 rewrite of a Team rename (Spec §5.3): the service pauses every Team a rewrite names, then runs each rewrite in registration order.</summary>
internal interface ITeamRenameRewrite
{
    /// <summary>The Teams whose Task files this rewrite writes, other than the renamed Team itself.</summary>
    /// <param name="rename">The Pending rename.</param>
    /// <returns>The Team names to pause.</returns>
    IReadOnlyList<string> TeamsTouched(TeamRename rename);

    /// <summary>Runs the rewrite. Idempotent: recovery may run it again.</summary>
    /// <param name="rename">The Pending rename.</param>
    /// <param name="ct">Cancels the work.</param>
    Task RunAsync(TeamRename rename, CancellationToken ct);
}
