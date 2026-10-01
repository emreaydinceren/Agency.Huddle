using System.Diagnostics.CodeAnalysis;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Services;

namespace Agency.Huddle.App.Teams;

/// <summary>
/// Adds or removes a Persona's Team label by rewriting only the <c>teams</c> field of its
/// definition and saving it through <see cref="PersonaStore.Update"/>, which keeps the Persona's
/// stored Model and Effort and raises <c>PersonasChanged</c> once.
/// </summary>
/// <param name="personas">The store the definitions are read from and written through.</param>
/// <param name="catalog">Supplies the display spelling of an existing Team.</param>
internal sealed class TeamMembership(PersonaStore personas, ITeamCatalog catalog) : ITeamMembership
{
    private const string TeamsKey = "teams";

    private const string UnwritableLabel = "This Team's name can't be written to a Teammate's definition.";

    // Serialises the whole read-modify-write: PersonaStore.Update's own gate only covers validate-and-write,
    // so two concurrent Adds would read the same text and the second write would drop the first label.
    // PersonasChanged subscribers run while this gate is held, so no subscriber may call ITeamMembership.
    private readonly Lock gate = new();

    /// <inheritdoc />
    public MembershipResult Add(string team, string personaName)
    {
        lock (this.gate)
        {
            if (!this.TryRead(personaName, out Persona? persona, out IReadOnlyList<string> current, out MembershipResult? failure))
            {
                return failure;
            }

            string spelling = catalog.Find(team)?.Name ?? team;
            if (!IsWritable(spelling))
            {
                return new MembershipResult(MembershipOutcome.Rejected, UnwritableLabel);
            }

            if (TeamLabels.Contains(current, spelling))
            {
                return new MembershipResult(MembershipOutcome.AlreadyMember);
            }

            return this.Save(persona, TeamLabels.Add(current, spelling), MembershipOutcome.Added);
        }
    }

    /// <inheritdoc />
    public MembershipResult Remove(string team, string personaName)
    {
        lock (this.gate)
        {
            if (!this.TryRead(personaName, out Persona? persona, out IReadOnlyList<string> current, out MembershipResult? failure))
            {
                return failure;
            }

            if (!TeamLabels.Contains(current, team))
            {
                return new MembershipResult(MembershipOutcome.NotMember);
            }

            return this.Save(persona, TeamLabels.Remove(current, team), MembershipOutcome.Removed);
        }
    }

    /// <summary>Whether a Team label survives a round trip through the <c>teams</c> line: not blank, not padded, no comma, semicolon or control character.</summary>
    /// <param name="label">The label as it would be written.</param>
    private static bool IsWritable(string label)
    {
        if (string.IsNullOrWhiteSpace(label) || char.IsWhiteSpace(label[0]) || char.IsWhiteSpace(label[^1]))
        {
            return false;
        }

        return !label.Any(static c => c is ',' or ';' || char.IsControl(c));
    }

    /// <summary>Looks the Persona up and reads its current labels from the very text a later write is built from.</summary>
    /// <param name="personaName">The Persona's Name.</param>
    /// <param name="persona">The Persona when found.</param>
    /// <param name="current">Its current Team labels.</param>
    /// <param name="failure">The result to return when the Persona is missing or unreadable.</param>
    private bool TryRead(
        string personaName,
        [NotNullWhen(true)] out Persona? persona,
        out IReadOnlyList<string> current,
        [NotNullWhen(false)] out MembershipResult? failure)
    {
        current = [];
        persona = personas.Get(personaName);
        if (persona is null)
        {
            failure = new MembershipResult(MembershipOutcome.NotFound);
            return false;
        }

        if (!PersonaFrontmatter.TryReadIdentity(persona.Text, out PersonaIdentity? identity, out string error))
        {
            failure = new MembershipResult(MembershipOutcome.Rejected, error);
            return false;
        }

        current = identity.Teams;
        failure = null;
        return true;
    }

    /// <summary>Writes the new label list into the Persona's text and saves it with its current Model and Effort.</summary>
    /// <param name="persona">The Persona as read.</param>
    /// <param name="labels">The complete new label list.</param>
    /// <param name="outcome">The outcome to report on success.</param>
    private MembershipResult Save(Persona persona, IReadOnlyList<string> labels, MembershipOutcome outcome)
    {
        string text = PersonaFrontmatter.WriteListField(persona.Text, TeamsKey, labels);
        try
        {
            personas.Update(persona.Name, text, persona.Model, persona.Effort, persona.WorkMode);
        }
        catch (ChatException ex)
        {
            // Update refuses text that will not load or a Persona that vanished: report it, never throw into the page.
            return new MembershipResult(MembershipOutcome.Rejected, ex.Message);
        }
        catch (IOException ex)
        {
            // The definition file could not be written; the message is the user-facing reason.
            return new MembershipResult(MembershipOutcome.Rejected, ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            // The definition file is read-only or locked; the message is the user-facing reason.
            return new MembershipResult(MembershipOutcome.Rejected, ex.Message);
        }

        return new MembershipResult(outcome);
    }
}
