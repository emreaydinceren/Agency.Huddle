using System.Text.Json;
using System.Text.Json.Serialization;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Acp.Sessions.TurnHooks;

/// <summary>
/// Tells an Agent that a Team or Project was renamed (Team Rename Spec §6.2). The state holds the id of the last journal row the
/// session has heard, or a marker that a fresh session has not yet worked that out because it needs the Room. Before collects the
/// notice and the highest id it included; After stores that id, so a rename that completes during the Turn is heard on the next one.
/// </summary>
/// <param name="renames">The rename journal.</param>
/// <param name="chat">Reads a Room's Messages, to find where a fresh session's Catch-up starts.</param>
/// <param name="settings">Says how many Messages a fresh session rereads.</param>
/// <param name="prompts">Supplies the notice's wording.</param>
/// <param name="timeProvider">Supplies the current time; the system clock when null.</param>
internal sealed class TeamRenamedTurnHook(
    ITeamRenames renames,
    IChatStore chat,
    ISessionSettings settings,
    IPromptSource prompts,
    TimeProvider? timeProvider = null)
    : TurnHook<TeamRenamedTurnHook.HeardCarry>
{
    /// <summary>The hook's name, as the pipeline's production order and the session's hook state know it.</summary>
    internal const string HookName = "TeamRenamed";

    private static readonly TimeSpan PreFeatureWindow = TimeSpan.FromDays(14);

    private static readonly JsonSerializerOptions StateOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;

    /// <summary>The highest journal id a Turn's notice included.</summary>
    /// <param name="Id">The journal id.</param>
    internal sealed record HeardCarry(long Id);

    /// <inheritdoc />
    public override string Name => HookName;

    /// <inheritdoc />
    public override bool AppliesTo(TurnHookScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        return true;
    }

    /// <inheritdoc />
    public override Task<string?> OnSessionOpeningAsync(SessionOpening opening, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(opening);

        switch (opening.Reason)
        {
            case SessionOpenReason.Fresh:
                return Task.FromResult<string?>(Serialize(new StateDocument(true, null)));
            case SessionOpenReason.Compacted:
                return Task.FromResult(opening.State);
            default:
                string? stored = Parse(opening.State) is null ? Serialize(new StateDocument(null, this.PreFeatureId())) : opening.State;
                return Task.FromResult<string?>(stored);
        }
    }

    /// <inheritdoc />
    public override async Task<TurnAddition?> BeforeTurnAsync(TurnStarting turn, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(turn);

        StateDocument? state = Parse(turn.State);
        long heard;
        if (state?.Fresh == true)
        {
            heard = await this.FreshHeardAsync(turn.RoomId, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            heard = state?.Heard ?? this.PreFeatureId();
        }

        IReadOnlyList<TeamRename> rows = renames.DoneAfter(heard);
        if (rows.Count == 0)
        {
            return new TurnAddition(null, new HeardCarry(heard));
        }

        string text = string.Join("\n\n", rows.Select(this.Render));
        return new TurnAddition(text, new HeardCarry(rows.Max(static row => row.Id)));
    }

    /// <inheritdoc />
    protected override Task<string?> AfterTurnAsync(TurnEnded turn, HeardCarry? carry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(turn);

        return Task.FromResult(carry is null ? turn.State : Serialize(new StateDocument(null, carry.Id)));
    }

    private static string Serialize(StateDocument state) => JsonSerializer.Serialize(state, StateOptions);

    /// <summary>Reads a stored state; null for none, and for text that is not this hook's state, which then counts as no state.</summary>
    private static StateDocument? Parse(string? state)
    {
        if (string.IsNullOrWhiteSpace(state))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<StateDocument>(state, StateOptions);
        }
        catch (JsonException)
        {
            // Not this hook's state: the session is treated as one from before the feature, which tells only the last 14 days.
            return null;
        }
    }

    /// <summary>The newest Done row older than the pre-feature window; 0 when none.</summary>
    private long PreFeatureId() => renames.LatestDoneBefore(this.clock.GetUtcNow() - PreFeatureWindow);

    /// <summary>
    /// What a fresh session has heard: the newest Done row older than the first Message of its Catch-up, so it hears the renames its
    /// Catch-up predates. A Room with no Messages has nothing to predate, so it has heard everything done so far.
    /// </summary>
    private async Task<long> FreshHeardAsync(string roomId, CancellationToken cancellationToken)
    {
        IReadOnlyList<ChatMessage> messages = await chat.ReadAllAsync(roomId, cancellationToken).ConfigureAwait(false);
        if (messages.Count == 0)
        {
            IReadOnlyList<TeamRename> done = renames.DoneAfter(0);
            return done.Count == 0 ? 0 : done.Max(static row => row.Id);
        }

        int cap = Math.Max(1, settings.Current.CatchUpMessages);
        DateTimeOffset oldest = messages.Skip(Math.Max(0, messages.Count - cap)).Min(static message => message.Timestamp);
        return renames.LatestDoneBefore(oldest);
    }

    private string Render(TeamRename row) => row.Kind == TeamRenameKind.Project
        ? prompts.Say("turn.projectRenamed", ("team", row.Team), ("oldProject", row.OldName), ("newProject", row.NewName))
        : prompts.Say("turn.teamRenamed", ("oldName", row.OldName), ("newName", row.NewName));

    /// <summary>The stored state: a marker for a fresh session that has not resolved its start, or the last journal id heard.</summary>
    private sealed record StateDocument(
        [property: JsonPropertyName("fresh")] bool? Fresh,
        [property: JsonPropertyName("heard")] long? Heard);
}
