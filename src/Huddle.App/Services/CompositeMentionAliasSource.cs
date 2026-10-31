using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Profile;

namespace Agency.Huddle.App.Services;

/// <summary>
/// Every Alias in force: each Persona's, plus the Human's when they have set one on the Settings
/// Profile tab, so <c>@alias</c> Mentions the Human the way it Mentions an Agent.
/// </summary>
/// <remarks>
/// The Human's Alias is checked when it is saved, but a Persona file written later could still take
/// the same Name or Alias. When that happens the Human's Alias is left out and a warning logged, so a
/// Mention never resolves to two Members at once.
/// </remarks>
/// <param name="personas">Supplies every Persona's Alias.</param>
/// <param name="human">Supplies the Human's Name and Alias.</param>
/// <param name="logger">Warns when the Human's Alias is left out.</param>
internal sealed class CompositeMentionAliasSource(PersonaStore personas, IHumanIdentity human, ILogger<CompositeMentionAliasSource> logger) : IMentionAliasSource
{
    /// <inheritdoc />
    public IReadOnlyList<MentionAlias> Aliases
    {
        get
        {
            var personaAliases = personas.Aliases;
            var alias = human.Current.Alias;
            if (alias is null)
            {
                return personaAliases;
            }

            foreach (var entry in personaAliases)
            {
                if (string.Equals(entry.Alias, alias, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(entry.Name, alias, StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogWarning("The Human's Alias '{Alias}' also belongs to the Agent {Name}; it is ignored until one of them changes.", alias, entry.Name);
                    return personaAliases;
                }
            }

            return [.. personaAliases, new MentionAlias(alias, human.Name)];
        }
    }
}
