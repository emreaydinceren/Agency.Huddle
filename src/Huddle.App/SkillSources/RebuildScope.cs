namespace Agency.Huddle.App.SkillSources;

/// <summary>Which part of the registry to re-read: which cached file texts and folder hashes to drop (§6.5, §9).</summary>
/// <param name="Everything">Drop every cache entry.</param>
/// <param name="Files">Pack, catalog and Source files whose cached text is dropped.</param>
/// <param name="SkillFolders">Skill folders whose cached hash is dropped.</param>
internal sealed record RebuildScope(bool Everything, IReadOnlyList<string> Files, IReadOnlyList<string> SkillFolders)
{
    /// <summary>Everything, as at start-up or on a change to SKILLS.md.</summary>
    public static RebuildScope All { get; } = new(true, [], []);

    /// <summary>True for <b>Reload</b>: a file that fails to parse is reported even within its two-second grace.</summary>
    public bool IgnoreGrace { get; init; }

    /// <summary>The union of two scopes; a burst of requests merges into one follow-up.</summary>
    public RebuildScope Union(RebuildScope other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return new RebuildScope(
            this.Everything || other.Everything,
            [.. this.Files.Union(other.Files, StringComparer.OrdinalIgnoreCase)],
            [.. this.SkillFolders.Union(other.SkillFolders, StringComparer.OrdinalIgnoreCase)])
        {
            IgnoreGrace = this.IgnoreGrace || other.IgnoreGrace,
        };
    }
}
