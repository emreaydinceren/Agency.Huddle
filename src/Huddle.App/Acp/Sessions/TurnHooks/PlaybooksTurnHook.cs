using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Teams;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Acp.Sessions.TurnHooks;

/// <summary>
/// Tells an Agent which Team Playbooks changed since its session last read them (Team Playbooks spec §6.4). The state is the JSON of two
/// maps from a part's key to the hash of its text: what the session <c>started</c> with and what it has <c>heard</c>. Before collects the
/// notice; After stores the heard map it carried. Stateless: the map travels as the carry.
/// </summary>
/// <param name="personas">Resolves the Persona's Teams line.</param>
/// <param name="catalog">Spells each Team as its folder is spelled and names its manager.</param>
/// <param name="prompts">Resolves the default Playbook texts and renders the notices.</param>
/// <param name="options">Supplies the Teams root and the Playbook size options.</param>
/// <param name="logger">Receives the warning for a state that cannot be parsed and for a Playbook file that cannot be read.</param>
internal sealed class PlaybooksTurnHook(PersonaStore personas, ITeamCatalog catalog, IPromptSource prompts, IOptions<TeamOptions> options, ILogger<PlaybooksTurnHook> logger)
    : TurnHook<IReadOnlyDictionary<string, string>>
{
    private const string HeaderKey = "turn.playbookHeader";
    private const string ChangedKey = "turn.playbookChanged";
    private const string GoneKey = "turn.playbookGone";
    private const string RulesPrefix = "rules";
    private const string DefaultSuffix = "default";

    private static readonly JsonSerializerOptions StateOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>The hook's name, as the pipeline's production order and the session's hook state know it.</summary>
    internal const string HookName = "Playbooks";

    /// <inheritdoc />
    public override string Name => HookName;

    /// <inheritdoc />
    public override bool AppliesTo(TurnHookScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        return !scope.Clean;
    }

    /// <inheritdoc />
    public override Task<string?> OnSessionOpeningAsync(SessionOpening opening, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(opening);

        switch (opening.Reason)
        {
            case SessionOpenReason.Fresh:
                Dictionary<string, string> map = this.Current(opening.Scope.PersonaName, opening.Scope.ReadsFiles).Map;
                return Task.FromResult<string?>(Serialize(map, map));
            case SessionOpenReason.Compacted:
                IReadOnlyDictionary<string, string>? started = this.Parse(opening.State)?.Started;
                return Task.FromResult(started is null ? null : Serialize(started, started));
            default:
                return Task.FromResult(opening.State);
        }
    }

    /// <inheritdoc />
    public override Task<TurnAddition?> BeforeTurnAsync(TurnStarting turn, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(turn);

        StateDocument? state = this.Parse(turn.State);
        CurrentParts current = this.Current(turn.Scope.PersonaName, turn.Scope.ReadsFiles);
        if (state?.Heard is null)
        {
            return Task.FromResult<TurnAddition?>(new TurnAddition(null, current.Map));
        }

        string? block = this.Block(state.Heard, current);
        bool unchanged = SameMap(state.Heard, current.Map);
        return Task.FromResult(block is null && unchanged ? null : new TurnAddition(block, current.Map));
    }

    /// <inheritdoc />
    protected override Task<string?> AfterTurnAsync(TurnEnded turn, IReadOnlyDictionary<string, string>? carry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(turn);

        if (carry is null)
        {
            return Task.FromResult(turn.State);
        }

        // A state that could not be read has no known start; what is told now becomes the start, so a later /compact has one to return to.
        return Task.FromResult<string?>(Serialize(this.Parse(turn.State, log: false)?.Started ?? carry, carry));
    }

    private static string Serialize(IReadOnlyDictionary<string, string>? started, IReadOnlyDictionary<string, string> heard) =>
        JsonSerializer.Serialize(new StateDocument(started, heard), StateOptions);

    private static bool SameMap(IReadOnlyDictionary<string, string> left, Dictionary<string, string> right) =>
        left.Count == right.Count
        && left.All(pair => right.TryGetValue(pair.Key, out string? value) && string.Equals(pair.Value, value, StringComparison.Ordinal));

    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16];

    private static string KindKey(PlaybookKind kind) => kind == PlaybookKind.Manager ? "manager" : "member";

    /// <summary>
    /// Builds the parts of one Persona as the system prompt sends them (through <see cref="PlaybookParts"/>: heading, cut and total cap), in
    /// prompt order. A part's state value is the hash of the whole text sent, then a space and its heading, which a later notice reuses.
    /// </summary>
    private CurrentParts Current(string personaName, bool readsFiles)
    {
        IReadOnlyList<string> labels = personas.ResolveByNameOrAlias(personaName)?.Teams ?? [];
        string teamsRoot = Path.GetFullPath(Path.Combine(options.Value.DataDir, options.Value.Teams.Dir));
        PlaybookSnapshot snapshot = PlaybookSnapshotBuilder.Build(prompts, catalog.Teams, personaName, labels, teamsRoot, options.Value.Playbooks, readsFiles, logger);

        List<PromptPart> sent = [];
        PlaybookParts.Add(sent, prompts, snapshot, HuddleRules.BareToolNames);
        Dictionary<PlaybookKind, Queue<PlaybookSection>> queues = new()
        {
            [PlaybookKind.Member] = new(snapshot.Sections.Where(static section => section.Kind == PlaybookKind.Member)),
            [PlaybookKind.Manager] = new(snapshot.Sections.Where(static section => section.Kind == PlaybookKind.Manager)),
        };
        List<Part> parts = [];
        foreach (PromptPart part in sent.Skip(1))
        {
            bool manager = part.Label.Contains("Manager", StringComparison.Ordinal);
            PlaybookKind kind = manager ? PlaybookKind.Manager : PlaybookKind.Member;
            string key = part.Label.StartsWith("Huddle rules", StringComparison.Ordinal)
                ? $"{RulesPrefix}:{KindKey(kind)}"
                : SectionKey(kind, queues[kind].Dequeue());
            int lineEnd = part.Text.IndexOf('\n', StringComparison.Ordinal);
            string heading = part.Text[..lineEnd].TrimStart('#', ' ');
            parts.Add(new Part(key, heading, part.Text[(lineEnd + 1)..], Hash(part.Text) + " " + heading));
        }

        Dictionary<string, string> map = new(StringComparer.Ordinal);
        foreach (Part part in parts)
        {
            map[part.Key] = part.Value;
        }

        return new CurrentParts(map, parts, snapshot.Sections);
    }

    private static string SectionKey(PlaybookKind kind, PlaybookSection section) =>
        $"{KindKey(kind)}:{(section.Source is PromptPartSource.File ? section.Teams[0] : DefaultSuffix)}";

    /// <summary>Renders the header and the changed then gone entries; null when nothing is told.</summary>
    private string? Block(IReadOnlyDictionary<string, string> heard, CurrentParts current)
    {
        List<string> entries = [];
        foreach (Part part in current.Parts)
        {
            if (!heard.TryGetValue(part.Key, out string? value) || !string.Equals(value, part.Value, StringComparison.Ordinal))
            {
                entries.Add(prompts.Render(ChangedKey, new Dictionary<string, string> { ["{{playbook}}"] = part.Heading, ["{{text}}"] = part.Text }));
            }
        }

        foreach ((string key, string value) in heard.Where(pair => !current.Map.ContainsKey(pair.Key)))
        {
            if (IsGone(key, current.Sections))
            {
                string heading = value[(value.IndexOf(' ', StringComparison.Ordinal) + 1)..];
                entries.Add(prompts.Render(GoneKey, new Dictionary<string, string> { ["{{playbook}}"] = heading }));
            }
        }

        if (entries.Count == 0)
        {
            return null;
        }

        string header = prompts.Render(HeaderKey, new Dictionary<string, string>());
        return header + "\n\n" + string.Join("\n\n", entries);
    }

    /// <summary>
    /// Whether a part the Agent heard and no longer has no longer applies to it; false when the Team still has a Playbook of that kind (a
    /// customized or reset Playbook is told as changed, not gone).
    /// </summary>
    private static bool IsGone(string key, IReadOnlyList<PlaybookSection> sections)
    {
        int colon = key.IndexOf(':', StringComparison.Ordinal);
        if (colon < 0)
        {
            return false;
        }

        string prefix = key[..colon];
        string suffix = key[(colon + 1)..];
        if (string.Equals(prefix, RulesPrefix, StringComparison.Ordinal))
        {
            return true;
        }

        PlaybookKind kind = string.Equals(prefix, KindKey(PlaybookKind.Manager), StringComparison.Ordinal) ? PlaybookKind.Manager : PlaybookKind.Member;
        List<PlaybookSection> ofKind = [.. sections.Where(section => section.Kind == kind)];
        return string.Equals(suffix, DefaultSuffix, StringComparison.Ordinal)
            ? ofKind.Count == 0
            : !ofKind.Any(section => section.Teams.Contains(suffix, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>Reads a stored state; null for no state, and for one that cannot be parsed, which is logged.</summary>
    private StateDocument? Parse(string? state, bool log = true)
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
            if (log)
            {
                logger.LogWarning(ex, "The Playbooks hook state could not be read; treating it as unknown.");
            }

            return null;
        }
    }

    /// <summary>One part an Agent reads: its state key, the name a notice gives it, its text and the hash of what the Agent was shown.</summary>
    private sealed record Part(string Key, string Heading, string Text, string Value);

    /// <summary>The parts an Agent has now, in prompt order, as a map from key to hash, and the snapshot they came from.</summary>
    private sealed record CurrentParts(Dictionary<string, string> Map, IReadOnlyList<Part> Parts, IReadOnlyList<PlaybookSection> Sections);

    /// <summary>The stored state: what the session started with (null when unknown) and what it has heard.</summary>
    private sealed record StateDocument(
        [property: JsonPropertyName("started")] IReadOnlyDictionary<string, string>? Started,
        [property: JsonPropertyName("heard")] IReadOnlyDictionary<string, string>? Heard);
}
