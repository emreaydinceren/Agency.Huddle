using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Feed;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Services;

namespace Agency.Huddle.App.Agents;

/// <summary>
/// Guarantees the Chief of Staff exists (Spec §6.12). Registered as a hosted service that runs
/// once at startup, before <see cref="Acp.PersonaSupervisor"/> starts any Agent: if no loaded
/// Persona already carries the <see cref="BuiltinAgent.ChiefOfStaffMarker"/>, this writes the
/// embedded default (<see cref="BuiltinAgent.DefaultText"/>) through <see cref="PersonaStore.Add"/>,
/// under the first free (Name, Alias) pair Spec §8.6's search finds when the shipped defaults are
/// already taken.
/// </summary>
/// <remarks>
/// Runs regardless of <c>Team:Acp:Enabled</c> - writing a file is free; only starting a session
/// spends. An existing Chief's model and effort are left alone, with one exception: a Chief still on
/// the old seeded default (model <c>haiku</c>, no effort) is moved once to <c>opus</c> at <c>medium</c>
/// (issue 560); a marker file in the data folder makes that happen once. Never reverts an edit: only an absent marker, or an untouched earlier shipped version (see <see cref="BuiltinAgentVersions"/>), triggers a write, and the check runs
/// once, at startup, never on <c>PersonaRemoved</c> (Spec §6.12 Constraints). A failed write -
/// no free (Name, Alias) pair within <see cref="MaxFreeNameAttempts"/> attempts, or
/// <see cref="PersonaStore.Add"/> itself throwing - logs an Error and lets startup continue
/// (Spec §8.7): nothing this hosted service does ever escapes <see cref="StartAsync"/>.
/// </remarks>
/// <param name="personas">The Persona library to check and seed.</param>
/// <param name="logger">Logs a failed parse, a failed free-name search, or a failed write, as an Error, without stopping the host.</param>
/// <param name="feed">Where the "update available" Notification goes; <see langword="null"/> posts none.</param>
/// <param name="knownVersions">The earlier shipped versions by hash; defaults to the list embedded in the assembly.</param>
internal sealed class BuiltinAgentSeeder(
    PersonaStore personas,
    ILogger<BuiltinAgentSeeder> logger,
    IFeedPublisher? feed = null,
    BuiltinAgentVersions? knownVersions = null) : IHostedService
{
    private readonly BuiltinAgentVersions versions = knownVersions ?? BuiltinAgentVersions.Embedded;

    /// <summary>The default Chief of Staff Name the free-name search starts from (Spec §8.6).</summary>
    private const string DefaultName = "Chief of Staff";

    /// <summary>The default Chief of Staff Alias the free-name search starts from (Spec §8.6).</summary>
    private const string DefaultAlias = "cos";

    /// <summary>
    /// The model a newly created Chief of Staff starts on, in the id the Model picker stores for the
    /// Claude Adapter. The owner decided (2026-11-14, issue 560) that the Chief of Staff, the Human's
    /// front door, runs on Opus at medium effort. The first-launch greeting therefore costs more than
    /// it did on Haiku (0.33 USD on the account default, under 0.01 USD on Haiku). The Human can change
    /// or clear it afterwards.
    /// </summary>
    private const string DefaultModel = "opus";

    /// <summary>The effort a newly created Chief of Staff starts on, in the id the Effort picker stores (issue 560).</summary>
    private const string DefaultEffort = "medium";

    /// <summary>
    /// The model earlier builds seeded (issue 390). An existing Chief still on exactly this model with
    /// no stored effort is moved once to <see cref="DefaultModel"/> and <see cref="DefaultEffort"/>.
    /// </summary>
    private const string OldDefaultModel = "haiku";

    /// <summary>The file in the data folder that records the one-time move has been decided.</summary>
    private const string ModelMovedMarkerFile = "chief-of-staff-model-moved.txt";

    /// <summary>
    /// How many <c>(Name, Alias)</c> pairs the free-name search tries before giving up. Spec §8.6
    /// does not itself bound the search, but an installation with a hundred Personas already named
    /// "Chief of Staff N" is not a free-name search away from a usable one - past this point,
    /// giving up and logging is more honest than searching forever.
    /// </summary>
    private const int MaxFreeNameAttempts = 100;

    /// <summary>
    /// Writes the default Chief of Staff, under the first free (Name, Alias) pair, when no loaded
    /// Persona carries the marker.
    /// </summary>
    /// <param name="cancellationToken">Unused; the write is synchronous local file I/O.</param>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var existing = personas.Entries.FirstOrDefault(
            entry => string.Equals(entry.Builtin, BuiltinAgent.ChiefOfStaffMarker, StringComparison.Ordinal));
        if (existing is not null)
        {
            this.UpdateExisting(existing);
            this.MoveOldDefaultModelOnce(existing.Name);
            return Task.CompletedTask;
        }

        if (!PersonaFrontmatter.TryReadIdentity(BuiltinAgent.DefaultText, out PersonaIdentity? defaultIdentity, out var parseError))
        {
            logger.LogError("Could not seed the Chief of Staff: the embedded default failed to parse ({Error}).", parseError);
            return Task.CompletedTask;
        }

        if (!this.TryFindFreeNameAndAlias(out var name, out var alias))
        {
#pragma warning disable HAA0601 // Boxing a value-type log argument is the cost of ILogger params object[]; accepted together with HAA0101.
            logger.LogError("Could not seed the Chief of Staff: no free Name/Alias pair was found within {MaxAttempts} attempts.", MaxFreeNameAttempts);
#pragma warning restore HAA0601
            return Task.CompletedTask;
        }

        var identity = defaultIdentity with { Name = name, Alias = alias };
        var (_, body) = PersonaFrontmatter.Parse(BuiltinAgent.DefaultText);

        try
        {
            personas.Add(identity, body, model: DefaultModel, effort: DefaultEffort);
        }
        catch (ChatException ex)
        {
            logger.LogError(ex, "Could not seed the Chief of Staff '{Name}': {Reason}.", identity.Name, ex.Message);
            return Task.CompletedTask;
        }
        catch (IOException ex)
        {
            logger.LogError(ex, "Could not seed the Chief of Staff '{Name}': {Reason}.", identity.Name, ex.Message);
            return Task.CompletedTask;
        }

        // A Chief seeded here is already on the new default, so the one-time move is decided too.
        this.WriteModelMovedMarker(identity.Name);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Brings an existing Chief of Staff up to the shipped text when it is an untouched earlier
    /// version: the file is replaced atomically with the default under its current Name and Alias, and
    /// its stored Model, Effort and Work Mode stay as they are. A file the Human edited is left
    /// byte-identical and, once per shipped version, a Notification says an update exists. The
    /// replacement checks the file is still the text just judged, so a Human's edit that lands
    /// between the check and the write wins.
    /// </summary>
    /// <param name="entry">The Persona carrying the marker.</param>
    private void UpdateExisting(PersonaEntry entry)
    {
        try
        {
            var fileText = File.ReadAllText(entry.Path);
            var currentHash = BuiltinAgentVersions.CurrentHash;
            if (string.Equals(BuiltinAgentVersions.Hash(fileText), currentHash, StringComparison.Ordinal))
            {
                return;
            }

            if (this.versions.IsEarlierVersion(fileText))
            {
                var text = PersonaFrontmatter.WriteScalarField(BuiltinAgent.DefaultText, "name", entry.Name);
                text = PersonaFrontmatter.WriteScalarField(text, "alias", entry.Alias);
                if (personas.TryReplaceText(entry.Name, fileText, text))
                {
                    return;
                }
            }

            this.NotifyOnce(entry.Name, currentHash);
        }
        catch (Exception ex) when (ex is ChatException or IOException or UnauthorizedAccessException)
        {
            logger.LogError(ex, "Could not update the Chief of Staff '{Name}': {Reason}.", entry.Name, ex.Message);
        }
    }

    /// <summary>Where the one-time move's marker lives, in the data folder.</summary>
    private string ModelMovedMarkerPath => Path.Combine(personas.Paths.DataDir, ModelMovedMarkerFile);

    /// <summary>
    /// Moves an existing Chief of Staff from the old seeded default (model <c>haiku</c>, no effort) to
    /// <c>opus</c> at <c>medium</c>, once (issue 560). A Chief with any other model or an effort is the
    /// Human's choice and stays. The marker file is written whether or not anything moved, so a later
    /// start never moves it again; its absence is the only trigger. Work Mode is preserved.
    /// </summary>
    /// <param name="name">The Chief of Staff's current Name.</param>
    private void MoveOldDefaultModelOnce(string name)
    {
        try
        {
            if (File.Exists(this.ModelMovedMarkerPath))
            {
                return;
            }

            var persona = personas.Get(name);
            if (persona is not null
                && string.Equals(persona.Model, OldDefaultModel, StringComparison.Ordinal)
                && persona.Effort is null)
            {
                _ = personas.Update(name, persona.Text, DefaultModel, DefaultEffort, persona.WorkMode);
                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("Moved the Chief of Staff '{Name}' from the old default model to Opus at medium effort.", name);
                }
            }

        }
        catch (Exception ex) when (ex is ChatException or IOException or UnauthorizedAccessException)
        {
            logger.LogError(ex, "Could not move the Chief of Staff '{Name}' to the new default model: {Reason}.", name, ex.Message);
            return;
        }

        this.WriteModelMovedMarker(name);
    }

    /// <summary>
    /// Records that the one-time move is decided, so no later start moves the Chief of Staff again. A
    /// failed write logs an Error and lets startup continue; the next start then decides again.
    /// </summary>
    /// <param name="name">The Chief of Staff's current Name, for the log line.</param>
    private void WriteModelMovedMarker(string name)
    {
        try
        {
            AtomicFile.WriteText(this.ModelMovedMarkerPath, "done");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogError(ex, "Could not record the Chief of Staff '{Name}' model move as done: {Reason}.", name, ex.Message);
        }
    }

    /// <summary>
    /// Posts the "update available" Notification unless one was already posted for this shipped
    /// version; the version is remembered in a small file in the data folder so a restart, or the
    /// Feed's own clean-up of old items, never brings it back.
    /// </summary>
    /// <param name="name">The Chief of Staff's current Name.</param>
    /// <param name="currentHash">The hash of the version this build ships.</param>
    private void NotifyOnce(string name, string currentHash)
    {
        if (feed is null or NullFeedPublisher)
        {
            // Nothing can be shown while the Feed is off, so remember nothing: a later enable still gets the notice.
            return;
        }

        var markerPath = Path.Combine(personas.Paths.DataDir, "chief-of-staff-notified.txt");
        if (File.Exists(markerPath) && string.Equals(File.ReadAllText(markerPath).Trim(), currentHash, StringComparison.Ordinal))
        {
            return;
        }

        var message = $"A new version of {name} is available. Your {name} has edits of its own, so it was left as it is. "
            + "Choose Reset to default on its profile to take the new version; that replaces your edits.";
        feed.Post(new FeedMessage(FeedSourceType.BuiltinAgentUpdateAvailable, "builtin:" + BuiltinAgent.ChiefOfStaffMarker, FeedActor.Huddle, true, new BuiltinAgentUpdatePayload(name, message)));
        AtomicFile.WriteText(markerPath, currentHash);
    }

    /// <summary>Does nothing; the seeder has no state to release.</summary>
    /// <param name="cancellationToken">Unused.</param>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Finds the first free <c>(Name, Alias)</c> pair from Spec §8.6's search:
    /// <c>Chief of Staff</c> / <c>cos</c>, then <c>Chief of Staff 2</c> / <c>cos2</c>, and so on. A
    /// pair is free when neither half equals any loaded Persona's Name or Alias - compared the way
    /// <see cref="PersonaIndex"/> itself compares them, case-insensitively - and the candidate's
    /// Agent folder holds no Markdown file at all, checked through
    /// <see cref="PersonaStore.Paths"/> rather than recomputing the Teams path.
    /// </summary>
    /// <param name="name">The first free Name, when this returns <see langword="true"/>; otherwise <see cref="string.Empty"/>.</param>
    /// <param name="alias">The matching free Alias, when this returns <see langword="true"/>; otherwise <see cref="string.Empty"/>.</param>
    /// <returns><see langword="true"/> if a free pair was found within <see cref="MaxFreeNameAttempts"/> attempts.</returns>
    private bool TryFindFreeNameAndAlias(out string name, out string alias)
    {
        for (var n = 1; n <= MaxFreeNameAttempts; n++)
        {
            var candidateName = n == 1 ? DefaultName : $"{DefaultName} {n}";
            var candidateAlias = n == 1 ? DefaultAlias : $"{DefaultAlias}{n}";

            var taken = personas.IsHuman(candidateName) || personas.IsHuman(candidateAlias) || personas.Entries.Any(entry =>
                string.Equals(entry.Name, candidateName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(entry.Alias, candidateName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(entry.Name, candidateAlias, StringComparison.OrdinalIgnoreCase)
                || string.Equals(entry.Alias, candidateAlias, StringComparison.OrdinalIgnoreCase));

            if (!taken && !HasAnyMarkdownFile(personas.Paths.AgentFolder(candidateName)))
            {
                name = candidateName;
                alias = candidateAlias;
                return true;
            }
        }

        name = string.Empty;
        alias = string.Empty;
        return false;
    }

    /// <summary>
    /// Whether a candidate's Agent folder already holds any Markdown file - not necessarily one
    /// named exactly <c>{Name}.md</c> - so that a folder someone else already wrote into (under a
    /// different file name) is still treated as taken, not as free for a colliding write
    /// (corrections-B2 #31).
    /// </summary>
    /// <param name="agentFolder">The candidate's Agent folder, from <see cref="Acp.AgentPaths.AgentFolder"/>.</param>
    /// <returns><see langword="true"/> if the folder exists and contains at least one <c>.md</c> file.</returns>
    private static bool HasAnyMarkdownFile(string agentFolder) =>
        Directory.Exists(agentFolder) && Directory.EnumerateFiles(agentFolder, "*.md").Any();
}
