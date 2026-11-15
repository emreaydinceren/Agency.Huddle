namespace Agency.Huddle.App.Teams;

/// <summary>
/// How much of a scope's work waits for the Human (ADR-0087). A scope that sets no level inherits the one above it, so "unset" is a
/// <see langword="null"/> level and never a member of this enum.
/// </summary>
internal enum NeedsYouLevel
{
    /// <summary>Everything that waits today still waits. The default.</summary>
    AskMe,

    /// <summary>A local merge into main, a Sprint whose Tasks all run inside the Team and new Backlog Tasks proceed without the Human.</summary>
    RunInsideTheTeam,
}
