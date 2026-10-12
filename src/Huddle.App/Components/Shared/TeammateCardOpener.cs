using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Pipes;
using Agency.Huddle.App.Services;
using MudBlazor;
using TeammatesPage = Agency.Huddle.App.Components.Pages.Teammates;

namespace Agency.Huddle.App.Components.Shared;

/// <summary>
/// Opens the Teammate card dialog for a Teammate by name, gathering the inputs exactly as the Teammates
/// page does. The page and Global Search both open the card through this one door, so the two cannot drift
/// (Global Search spec §9, OP-9). The inputs are gathered when the card opens and passed as a snapshot,
/// because an open dialog's parameters are frozen.
/// </summary>
/// <param name="personas">The Teammate directory and definitions.</param>
/// <param name="directory">The Room and user directory, for the Teammate's agent and its Room with the Human.</param>
/// <param name="gateway">The pipe gateway, for whether the agent is connected.</param>
/// <param name="health">Reported health, the other input to the displayed status.</param>
/// <param name="dialogs">The dialog service, scoped to the circuit.</param>
internal sealed class TeammateCardOpener(
    PersonaStore personas,
    ITeamDirectory directory,
    IAgentGateway gateway,
    PersonaHealth health,
    IDialogService dialogs)
{
    /// <summary>Shows the card of the Teammate called <paramref name="name"/>.</summary>
    /// <param name="name">The Teammate's Name, in any letter case.</param>
    /// <param name="ct">Cancels the lookups.</param>
    /// <returns><see langword="true"/> when the card was shown; <see langword="false"/> when no Teammate has that Name.</returns>
    public async Task<bool> OpenAsync(string name, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        PersonaEntry? entry = personas.Entries.FirstOrDefault(candidate => string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            return false;
        }

        Persona? persona = personas.Get(entry.Name);
        User? agent = await directory.FindUserByNameAsync(entry.Name, ct);
        bool connected = agent is not null && gateway.IsOnline(agent.Id);
        PersonaStatus status = PersonaStatusResolver.Resolve(connected, health.Get(entry.Name));

        // Only a Teammate that has connected has a Room to be messaged in, so the card's Message action is
        // absent rather than broken for one that never has.
        string? roomId = agent is null
            ? null
            : (await directory.FindRoomWithExactMembersAsync(KnownIds.Human, agent.Id, ct))?.Id;

        await dialogs.ShowAsync<TeammateCard>(
            entry.Name,
            TeammatesPage.BuildViewParameters(entry.Name, entry, persona, status, roomId, TeammateCardOpener.TryPathFor(personas, entry.Name)),
            TeammateCard.Options);
        return true;
    }

    /// <summary>The Teammate's definition file path, or <see langword="null"/> when it does not resolve to one.</summary>
    /// <param name="personas">The Teammate definitions.</param>
    /// <param name="name">The Teammate's Name.</param>
    private static string? TryPathFor(PersonaStore personas, string name)
    {
        try
        {
            return personas.PathFor(name);
        }
        catch (ChatException)
        {
            return null;
        }
    }
}
