namespace Agency.Huddle.App.HiringPool;

/// <summary>A parsed <c>hiringpool.json</c>: the team types worth naming and the jobs a team can hire.</summary>
/// <param name="SchemaVersion">The file format version.</param>
/// <param name="TeamTypes">The team types, or null when omitted.</param>
/// <param name="Jobs">The jobs, or null when omitted.</param>
internal sealed record HiringPoolFile(int SchemaVersion, IReadOnlyList<PoolTeamType>? TeamTypes = null, IReadOnlyList<PoolJob>? Jobs = null)
{
    /// <summary>The team types; empty when omitted.</summary>
    public IReadOnlyList<PoolTeamType> TeamTypes { get; init; } = TeamTypes ?? [];

    /// <summary>The jobs; empty when omitted.</summary>
    public IReadOnlyList<PoolJob> Jobs { get; init; } = Jobs ?? [];
}
