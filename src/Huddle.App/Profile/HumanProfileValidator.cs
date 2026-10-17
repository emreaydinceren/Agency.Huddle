using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Profile;

/// <summary>
/// Checks a proposed <see cref="HumanProfile"/> before <see cref="HumanProfileStore.Save"/>: the Name
/// and Alias must be valid Names, must differ from each other, and must not belong to any Teammate,
/// as a Name or an Alias, ignoring case (<c>rules.md</c>: Name and Alias collisions are
/// case-insensitive, and an Alias is accepted anywhere a Name is).
/// </summary>
/// <param name="personas">Supplies every Persona's Name and Alias.</param>
/// <param name="directory">Supplies Agent rows with no Persona, such as a pipe client or the demo agent.</param>
internal sealed class HumanProfileValidator(PersonaStore personas, ITeamDirectory directory)
{
    /// <summary>Every problem with <paramref name="profile"/>, in the order a person would fix them; empty when it can be saved.</summary>
    /// <param name="profile">The profile as entered.</param>
    public IReadOnlyList<string> Validate(HumanProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        List<string> problems = [];
        var name = profile.Name.Trim();
        var alias = string.IsNullOrWhiteSpace(profile.Alias) ? null : profile.Alias.Trim();

        if (!NameRules.IsValidAgentName(name))
        {
            problems.Add("Name must be 1-64 letters, digits, '-' or '_', starting with a letter or digit, with single spaces only between words.");
        }
        else
        {
            this.AddCollision(problems, "Name", name);
        }

        if (alias is null)
        {
            return problems;
        }

        if (!NameRules.IsValidAgentName(alias))
        {
            problems.Add("Alias must be 1-64 letters, digits, '-' or '_', starting with a letter or digit, with single spaces only between words.");
        }
        else if (string.Equals(alias, name, StringComparison.OrdinalIgnoreCase))
        {
            problems.Add("Alias must be different from your Name.");
        }
        else
        {
            this.AddCollision(problems, "Alias", alias);
        }

        return problems;
    }

    /// <summary>Adds a problem when <paramref name="value"/> is a Teammate's Name or Alias, or an Agent row's Name.</summary>
    /// <param name="problems">The list to add to.</param>
    /// <param name="field">The field being checked, for the message.</param>
    /// <param name="value">The Name or Alias to check.</param>
    private void AddCollision(List<string> problems, string field, string value)
    {
        foreach (var entry in personas.Entries)
        {
            if (string.Equals(entry.Name, value, StringComparison.OrdinalIgnoreCase)
                || string.Equals(entry.Alias, value, StringComparison.OrdinalIgnoreCase))
            {
                problems.Add($"{field} '{value}' already belongs to the Teammate {entry.Name}.");
                return;
            }
        }

        var user = directory.FindUserByName(value);
        if (user is not null && user.Kind == UserKind.Agent)
        {
            problems.Add($"{field} '{value}' already belongs to the Agent {user.Name}.");
        }
    }
}
