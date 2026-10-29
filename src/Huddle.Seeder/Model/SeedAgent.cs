namespace Agency.Huddle.Seeder.Model;

/// <summary>An Agent definition file: <c>Agents/&lt;Name&gt;/&lt;Name&gt;.md</c>.</summary>
/// <param name="Name">The Agent's Name; also its folder and file name.</param>
/// <param name="Title">The Agent's job title.</param>
/// <param name="Specialty">One line shown to other Agents as part of the job description.</param>
/// <param name="Teams">The Teams the Agent belongs to.</param>
/// <param name="Skills">The Skills the Agent holds.</param>
/// <param name="Personality">The Markdown body: the Agent's private system prompt.</param>
internal sealed record SeedAgent(
    string Name,
    string Title,
    string Specialty,
    IReadOnlyList<string> Teams,
    IReadOnlyList<string> Skills,
    string Personality)
{
    /// <summary>The lower-case alias other Agents use to mention this one.</summary>
    internal string Alias => this.Name.ToLowerInvariant();
}
