using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Teammates;

/// <summary>
/// Restores the Chief of Staff to its shipped default (Spec §6.12) - the Teammate card's "Reset to
/// default" action. A plain singleton, not the hosted <see cref="BuiltinTeammateSeeder"/> itself, so
/// a component can inject it directly rather than depending on an
/// <see cref="Microsoft.Extensions.Hosting.IHostedService"/>.
/// </summary>
/// <param name="personas">The Persona library to reset against.</param>
internal sealed class BuiltinTeammateReset(PersonaStore personas)
{
    /// <summary>
    /// Restores the Persona currently named <paramref name="currentName"/> to the shipped Chief of
    /// Staff default: its Title, Body, Teams, Skills and the <c>_builtin</c> marker all come from
    /// <see cref="BuiltinTeammate.DefaultText"/>, with only <c>name</c> and <c>alias</c> rewritten to
    /// the caller's current values, so a rename survives a Reset exactly as Spec §6.12 promises.
    /// Model and Effort are cleared to the agent's default - <see cref="PersonaStore.Update"/>'s
    /// <c>model: null, effort: null</c> means "clear", not "leave unchanged", because
    /// <c>PersonaModelStore.Set</c> and <c>PersonaEffortStore.Set</c> both delete the stored row for
    /// a <see langword="null"/> value rather than leaving it untouched.
    /// </summary>
    /// <param name="currentName">The Chief of Staff's current Name, whatever it has been renamed to.</param>
    /// <returns>The Persona after the reset.</returns>
    /// <exception cref="ChatException">
    /// No Persona is currently filed under <paramref name="currentName"/> - the marker's own entry
    /// vanished between the card reading it and this call, or <see cref="PersonaStore.Update"/>
    /// itself rejects the composed text.
    /// </exception>
    internal Persona ResetToDefault(string currentName)
    {
        var alias = personas.Entries
            .FirstOrDefault(entry => string.Equals(entry.Name, currentName, StringComparison.Ordinal))?.Alias
            ?? throw new ChatException(ErrorCodes.BadMessage, $"Persona '{currentName}' does not exist.");

        var text = PersonaFrontmatter.WriteScalarField(BuiltinTeammate.DefaultText, "name", currentName);
        text = PersonaFrontmatter.WriteScalarField(text, "alias", alias);

        return personas.Update(currentName, text, model: null, effort: null);
    }
}
