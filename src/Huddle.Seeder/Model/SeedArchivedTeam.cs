namespace Agency.Huddle.Seeder.Model;

/// <summary>
/// A Team as the app's <c>ArchiveService</c> leaves it once archived (Seeder spec SEED-SC-16): its folder is
/// <c>Archive/&lt;Name&gt;</c> with the Projects inside it, its <c>_team.md</c> records the roster and the ids of the Rooms the
/// archive closed, no Agent carries its label any more, and every Room scoped to it is archived.
/// </summary>
/// <param name="Name">The Team name, which is also its folder under <c>Archive/</c>.</param>
/// <param name="Projects">The Projects that moved with it.</param>
/// <param name="Members">The Agents it had when it was archived, written to <c>_team.md</c> as <c>archivedMembers</c>.</param>
internal sealed record SeedArchivedTeam(string Name, IReadOnlyList<string> Projects, IReadOnlyList<string> Members)
{
    /// <summary>The Team's folder below the data folder, using <c>/</c>.</summary>
    public string Folder => "Archive/" + this.Name;
}
