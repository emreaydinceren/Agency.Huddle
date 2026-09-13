namespace Agency.Huddle.App.Acp;

/// <summary>
/// The structural identity fields read out of a Persona's frontmatter. <see cref="Name"/>,
/// <see cref="Title"/> and <see cref="Alias"/> are all required; <see cref="Teams"/> is optional
/// and defaults to an empty list. See
/// <see cref="PersonaFrontmatter.TryReadIdentity(string, out PersonaIdentity?, out string)"/> for
/// how this is parsed and validated out of a Persona's raw file text.
/// </summary>
/// <param name="Name">
/// The Persona's display name, validated with
/// <see cref="Agency.Huddle.Contracts.NameRules.IsValidAgentName(string?)"/>.
/// </param>
/// <param name="Title">
/// Free-form display text describing the Persona's role (for example "Chief of Staff"). Not
/// validated as a Name — a title is prose, not an identifier.
/// </param>
/// <param name="Alias">
/// A short working handle for the Persona (for example <c>jar</c> for "Jarvis"), validated the
/// same way as <see cref="Name"/> with
/// <see cref="Agency.Huddle.Contracts.NameRules.IsValidAgentName(string?)"/>.
/// </param>
/// <param name="Teams">
/// The Teams this Persona belongs to, in file order, with a case-insensitive repeat collapsed
/// into its first occurrence. Empty, never <see langword="null"/>, when the frontmatter has no
/// <c>Teams</c> field.
/// </param>
public sealed record PersonaIdentity(
    string Name,
    string Title,
    string Alias,
    IReadOnlyList<string> Teams);
