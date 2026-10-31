using Agency.Huddle.SkillPacks.Checking;
using Agency.Huddle.SkillPacks.Files;

namespace Agency.Huddle.SkillPacks.Resolving;

/// <summary>A Source, as read from <c>sources/&lt;id&gt;/source.json</c>.</summary>
/// <param name="Id">The Source id as pack and catalog files write it.</param>
/// <param name="Folder">The Source's folder, or null when the id breaks the rule (nothing is read).</param>
/// <param name="File">Null when the Source has no valid <c>source.json</c> (O001).</param>
/// <param name="HasNotes">Whether <c>NOTES.md</c> exists.</param>
/// <param name="Findings">The loading findings about this Source.</param>
public sealed record ResolvedSource(string Id, string? Folder, SourceFile? File, bool HasNotes, IReadOnlyList<Finding> Findings)
{
    /// <summary>The Source's status, or active when it has no valid file.</summary>
    public SourceStatus Status => this.File?.Status ?? SourceStatus.Active;
}
