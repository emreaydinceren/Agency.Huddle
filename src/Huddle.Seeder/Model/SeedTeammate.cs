namespace Agency.Huddle.Seeder.Model;

/// <summary>A Teammate definition file: <c>Teammates/&lt;Name&gt;/&lt;Name&gt;.md</c>.</summary>
/// <param name="Name">The Teammate's Name; also its folder and file name.</param>
/// <param name="Title">The Teammate's job title.</param>
/// <param name="Specialty">One line shown to other Teammates as part of the job description.</param>
/// <param name="Teams">The Teams the Teammate belongs to.</param>
/// <param name="Skills">The Skills the Teammate holds.</param>
/// <param name="Personality">The Markdown body: the Teammate's private system prompt.</param>
internal sealed record SeedTeammate(
    string Name,
    string Title,
    string Specialty,
    IReadOnlyList<string> Teams,
    IReadOnlyList<string> Skills,
    string Personality)
{
    /// <summary>The lower-case alias other Teammates use to mention this one.</summary>
    internal string Alias => this.Name.ToLowerInvariant();
}
