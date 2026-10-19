using Agency.Huddle.App.Acp;

namespace Agency.Huddle.Tests.Factories;

/// <summary>Builds the Persona values, frontmatter text and identities tests share.</summary>
internal static class TestPersonas
{
    /// <summary>The lowercase "nova" Persona with the body "You are Nova." (OP-4); a site that needs the "Nova" casing calls <see cref="Named"/>.</summary>
    public static Persona Nova => new("nova", "You are Nova.");

    /// <summary>Builds a Persona named <paramref name="name"/> exactly as spelled.</summary>
    /// <param name="name">The Persona's name, case preserved.</param>
    /// <param name="body">The prompt text; <see langword="null"/> gives "You are {name}.".</param>
    public static Persona Named(string name, string? body = null) => new(name, body ?? $"You are {name}.");

    /// <summary>
    /// Builds raw Persona file text: frontmatter (Name, Title, Alias, then Teams and Adapter when given) around a body.
    /// Title and Alias default to <paramref name="name"/>; the alias casing is kept as given.
    /// </summary>
    /// <param name="name">The Persona's name.</param>
    /// <param name="body">The body under the frontmatter; <see langword="null"/> gives "You are {name}.".</param>
    /// <param name="title">The title; <see langword="null"/> uses the name.</param>
    /// <param name="alias">The alias; <see langword="null"/> uses the name.</param>
    /// <param name="teams">The Team labels; <see langword="null"/> writes no Teams line.</param>
    /// <param name="adapter">The adapter id; <see langword="null"/> writes no Adapter line.</param>
    public static string FrontmatterText(string name, string? body = null, string? title = null, string? alias = null, IEnumerable<string>? teams = null, string? adapter = null)
    {
        string teamsLine = teams is null ? string.Empty : $"Teams: {string.Join(", ", teams)}\n";
        string adapterLine = adapter is null ? string.Empty : $"Adapter: {adapter}\n";
        string text = body ?? $"You are {name}.";
        return $"---\nName: {name}\nTitle: {title ?? name}\nAlias: {alias ?? name}\n{teamsLine}{adapterLine}---\n{text}";
    }

    /// <summary>Builds a <see cref="PersonaIdentity"/>; title and alias default to the name and teams to none.</summary>
    /// <param name="name">The Persona's name.</param>
    /// <param name="title">The title; <see langword="null"/> uses the name.</param>
    /// <param name="alias">The alias; <see langword="null"/> uses the name.</param>
    /// <param name="teams">The Team labels; <see langword="null"/> means none.</param>
    public static PersonaIdentity Identity(string name, string? title = null, string? alias = null, IReadOnlyList<string>? teams = null) =>
        new(name, title ?? name, alias ?? name, teams ?? []);
}
