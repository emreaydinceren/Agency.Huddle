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
    /// <summary>The gender the scenario reads from <see cref="Name"/>; <see cref="AvatarFile"/> must show the same.</summary>
    public required SeedGender Gender { get; init; }

    /// <summary>The picture's bare file name in the bundled Avatar Library, such as <c>designer_tan_f_01.png</c>.</summary>
    public required string AvatarFile { get; init; }

    /// <summary>The team-type role the Agent was "hired" into, written <c>type/role@version</c>, or <see langword="null"/>; written as <c>_role</c>.</summary>
    public string? Role { get; init; }

    /// <summary>The lower-case alias other Agents use to mention this one.</summary>
    internal string Alias => this.Name.ToLowerInvariant();
}
