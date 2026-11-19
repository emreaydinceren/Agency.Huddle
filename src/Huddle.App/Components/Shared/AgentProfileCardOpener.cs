using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Pipes;
using Agency.Huddle.App.Services;
using MudBlazor;

namespace Agency.Huddle.App.Components.Shared;

/// <summary>
/// Opens the Agent card dialog for an Agent by name, gathering the inputs the one way. Every
/// surface that opens the card (Global Search, the Team's Members tab) goes through this one door, so they cannot drift
/// (Global Search spec §9, OP-9). The inputs are gathered when the card opens and passed as a snapshot,
/// because an open dialog's parameters are frozen.
/// </summary>
/// <param name="personas">The Agent directory and definitions.</param>
/// <param name="directory">The Room and user directory, for the Agent's agent and its Room with the Human.</param>
/// <param name="gateway">The pipe gateway, for whether the agent is connected.</param>
/// <param name="health">Reported health, the other input to the displayed status.</param>
/// <param name="dialogs">The dialog service, scoped to the circuit.</param>
internal sealed class AgentProfileCardOpener(
    PersonaStore personas,
    ITeamDirectory directory,
    IAgentGateway gateway,
    PersonaHealth health,
    IDialogService dialogs)
{
    /// <summary>Shows the card of the Agent called <paramref name="name"/>.</summary>
    /// <param name="name">The Agent's Name, in any letter case.</param>
    /// <param name="ct">Cancels the lookups.</param>
    /// <returns><see langword="true"/> when the card was shown; <see langword="false"/> when no Agent has that Name.</returns>
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

        // Only an Agent that has connected has a Room to be messaged in, so the card's Message action is
        // absent rather than broken for one that never has.
        string? roomId = agent is null
            ? null
            : (await directory.FindRoomWithExactMembersAsync(KnownIds.Human, agent.Id, ct))?.Id;

        await dialogs.ShowAsync<AgentProfileCard>(
            entry.Name,
            AgentProfileCardOpener.BuildViewParameters(entry.Name, entry, persona, status, roomId, AgentProfileCardOpener.TryPathFor(personas, entry.Name)),
            AgentProfileCard.Options);
        return true;
    }

    /// <summary>
    /// Builds the <see cref="DialogParameters"/> <see cref="OpenAsync"/> shows <see cref="AgentProfileCard"/>
    /// with in <see cref="AgentProfileCardMode.View"/>. A pure function of its inputs, so a plain unit test
    /// proves it: <c>AgentDialogParametersTests</c> checks that every key names a real public <c>[Parameter]</c>
    /// on <see cref="AgentProfileCard"/>, because a key that does not is silently dropped.
    /// </summary>
    /// <param name="personaName">The Persona to show.</param>
    /// <param name="entry">Its directory entry, for Title/Alias/Teams - <see langword="null"/> if it somehow is not (yet) in <see cref="PersonaStore.Entries"/>.</param>
    /// <param name="persona">Its file-plus-database record, for Text/Model/Effort - <see langword="null"/> the same way.</param>
    /// <param name="status">The resolved status to show for it.</param>
    /// <param name="roomId">The Room it shares with the Human, if one exists yet.</param>
    /// <param name="filePath">Its file's absolute path, if it resolves to one.</param>
    internal static DialogParameters BuildViewParameters(
        string personaName,
        PersonaEntry? entry,
        Persona? persona,
        PersonaStatus status,
        string? roomId,
        string? filePath) => new()
    {
        [nameof(AgentProfileCard.Mode)] = AgentProfileCardMode.View,
        [nameof(AgentProfileCard.Name)] = personaName,
        [nameof(AgentProfileCard.Title)] = entry?.Title ?? string.Empty,
        [nameof(AgentProfileCard.Alias)] = entry?.Alias ?? string.Empty,
        [nameof(AgentProfileCard.Teams)] = entry is null ? string.Empty : string.Join(", ", entry.Teams),
        [nameof(AgentProfileCard.Text)] = persona?.Text ?? string.Empty,
        [nameof(AgentProfileCard.Model)] = persona?.Model,
        [nameof(AgentProfileCard.Effort)] = persona?.Effort,
        [nameof(AgentProfileCard.WorkMode)] = persona?.WorkMode,
        [nameof(AgentProfileCard.Status)] = status,
        [nameof(AgentProfileCard.RoomId)] = roomId,
        [nameof(AgentProfileCard.FilePath)] = filePath,
    };

    /// <summary>The <see cref="DialogParameters"/> that show a blank <see cref="AgentProfileCard"/> in <see cref="AgentProfileCardMode.Create"/>; see <see cref="BuildViewParameters"/> for why every key matters.</summary>
    internal static DialogParameters BuildCreateParameters() => new()
    {
        [nameof(AgentProfileCard.Mode)] = AgentProfileCardMode.Create,
    };

    /// <summary>The Agent's definition file path, or <see langword="null"/> when it does not resolve to one.</summary>
    /// <param name="personas">The Agent definitions.</param>
    /// <param name="name">The Agent's Name.</param>
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
