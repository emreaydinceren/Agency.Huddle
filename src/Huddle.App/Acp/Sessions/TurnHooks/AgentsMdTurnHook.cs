using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Agency.Huddle.App.AgentsMdChanges;
using Agency.Huddle.App.Library;

namespace Agency.Huddle.App.Acp.Sessions.TurnHooks;

/// <summary>
/// Tells an Agent which <c>AGENTS.md</c> files changed since its session last heard (Spec §6.10, §8.5). The state is the JSON of
/// two maps: what the session <c>started</c> with and what it has <c>heard</c>. Before collects the notice; After stores the
/// heard map it carried. Stateless: the map travels as the carry.
/// </summary>
/// <param name="scopes">Says which files apply to the Agent.</param>
/// <param name="source">Works out and renders what the session has not heard.</param>
/// <param name="logger">Receives the warnings for a file that cannot be read and for a state that cannot be parsed.</param>
internal sealed class AgentsMdTurnHook(AgentsMdScopes scopes, AgentsMdNoticeSource source, ILogger<AgentsMdTurnHook> logger)
    : TurnHook<IReadOnlyDictionary<string, string>>
{
    private static readonly JsonSerializerOptions StateOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <inheritdoc />
    public override string Name => "AgentsMd";

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
                Dictionary<string, string> hashes = this.ReadHashes(opening.Scope.PersonaName);
                return Task.FromResult<string?>(Serialize(hashes, hashes));
            case SessionOpenReason.Compacted:
                IReadOnlyDictionary<string, string>? started = this.Parse(opening.State)?.Started;
                return Task.FromResult(started is null ? null : Serialize(started, started));
            default:
                return Task.FromResult(opening.State);
        }
    }

    /// <inheritdoc />
    public override async Task<TurnAddition?> BeforeTurnAsync(TurnStarting turn, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(turn);

        StateDocument? state = this.Parse(turn.State);
        IReadOnlyList<AgentsMdFile> scope = scopes.For(turn.Scope.PersonaName);
        AgentsMdCollected collected = await source
            .CollectAsync(scope, state?.Heard, turn.Scope.ReadsFiles, cancellationToken)
            .ConfigureAwait(false);

        string? block = string.IsNullOrEmpty(collected.Block) ? null : collected.Block;
        bool unchanged = state?.Heard is not null && SameMap(state.Heard, collected.Heard);
        return block is null && unchanged ? null : new TurnAddition(block, collected.Heard);
    }

    /// <inheritdoc />
    protected override Task<string?> AfterTurnAsync(TurnEnded turn, IReadOnlyDictionary<string, string>? carry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(turn);

        if (carry is null)
        {
            return Task.FromResult(turn.State);
        }

        return Task.FromResult<string?>(Serialize(this.Parse(turn.State)?.Started, carry));
    }

    private static string Serialize(IReadOnlyDictionary<string, string>? started, IReadOnlyDictionary<string, string> heard) =>
        JsonSerializer.Serialize(new StateDocument(started, heard), StateOptions);

    private static bool SameMap(IReadOnlyDictionary<string, string> left, IReadOnlyDictionary<string, string> right) =>
        left.Count == right.Count
        && left.All(pair => right.TryGetValue(pair.Key, out string? value) && string.Equals(pair.Value, value, StringComparison.Ordinal));

    /// <summary>Reads a stored state; null for no state, and for one that cannot be parsed, which is logged.</summary>
    private StateDocument? Parse(string? state)
    {
        if (string.IsNullOrWhiteSpace(state))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<StateDocument>(state, StateOptions);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "The AgentsMd hook state could not be read; treating it as unknown.");
            return null;
        }
    }

    /// <summary>The hash of each in-scope file that exists and can be read, by relative path.</summary>
    private Dictionary<string, string> ReadHashes(string personaName)
    {
        StringComparer comparer = PathContainment.Comparison == StringComparison.OrdinalIgnoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        Dictionary<string, string> hashes = new(comparer);
        foreach (AgentsMdFile file in scopes.For(personaName))
        {
            if (!File.Exists(file.FullPath))
            {
                continue;
            }

            try
            {
                using FileStream stream = new(file.FullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using StreamReader reader = new(stream, Encoding.UTF8);
                hashes[file.RelativePath] = AgentsMdBody.Parse(reader.ReadToEnd()).Hash;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(ex, "AGENTS.md '{Path}' could not be read; leaving it out of the session's start.", file.RelativePath);
            }
        }

        return hashes;
    }

    /// <summary>The stored state: what the session started with (null when unknown) and what it has heard.</summary>
    private sealed record StateDocument(
        [property: JsonPropertyName("started")] IReadOnlyDictionary<string, string>? Started,
        [property: JsonPropertyName("heard")] IReadOnlyDictionary<string, string>? Heard);
}
