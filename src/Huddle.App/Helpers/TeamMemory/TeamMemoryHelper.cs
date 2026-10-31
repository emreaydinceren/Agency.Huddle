using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization.Metadata;
using Agency.Huddle.App.DocHistory;
using Agency.Huddle.App.Feed;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Memory;

namespace Agency.Huddle.App.Helpers.TeamMemory;

/// <summary>
/// Files what members propose into their Team's shared Memory (Team Memory Helper spec §6.4 to §6.6): it judges one proposal and
/// Huddle writes the file through <see cref="MemoryWriter"/>, with the version the Helper was shown, then records the proposer in
/// the file's History. A stale Verdict is judged once more with fresh input, then dropped. An invalid one is refused, never repaired.
/// </summary>
/// <param name="paths">Resolves the Verdict's folder, only inside the proposing Team.</param>
/// <param name="writer">Writes the file under the memory rules.</param>
/// <param name="history">Records the write in the file's History.</param>
/// <param name="resolver">Finds the written file in the Library, for its History.</param>
/// <param name="feed">Receives the record of a proposal that was not filed.</param>
/// <param name="settings">Tells whether the Helper has been turned off since the run started.</param>
/// <param name="trigger">Queues the second judgement of a stale Verdict.</param>
/// <param name="logger">Records each filing and each refusal.</param>
internal sealed class TeamMemoryHelper(
    MemoryPaths paths,
    MemoryWriter writer,
    DocHistoryRecorder history,
    LibraryPathResolver resolver,
    IFeedPublisher feed,
    HelperSettingsResolver settings,
    TeamMemoryTrigger trigger,
    ILogger<TeamMemoryHelper> logger)
    : IHelperAgent<TeamMemoryInput, TeamMemoryVerdict>
{
    /// <summary>The id the Helper is registered, configured and stored under.</summary>
    public static readonly HelperId HelperId = new("TeamMemory");

    /// <summary>The name in a file's History: the display name and <c>(Helper)</c>, which no Agent's Name can contain.</summary>
    internal const string ActorName = "Team Memory (Helper)";

    /// <summary>The longest reason kept, as for a Task Reason.</summary>
    private const int MaxReasonLength = 200;

    /// <summary>The most characters of the fact a Feed record carries.</summary>
    private const int FeedFactLength = 120;

    /// <inheritdoc/>
    public HelperId Id => HelperId;

    /// <inheritdoc/>
    public string DisplayName => "Team Memory";

    /// <inheritdoc/>
    public string Description => "Files what members propose into their Team's shared Memory: creates or updates a topic file, or files nothing.";

    /// <inheritdoc/>
    public IReadOnlyList<string> PromptKeys { get; } = ["helper.teamMemory.system", "helper.teamMemory.proposal"];

    /// <inheritdoc/>
    public HelperDefaults Defaults { get; } = new(Enabled: true, Adapter: "claude", Model: "haiku", Effort: "low", DailyCap: 30, TimeoutSeconds: 90);

    /// <inheritdoc/>
    public JsonTypeInfo<TeamMemoryVerdict> VerdictType => TeamMemoryJsonContext.Default.TeamMemoryVerdict;

    /// <inheritdoc/>
    public string Fingerprint(TeamMemoryInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        StringBuilder text = new();
        _ = text.Append(input.Proposal.Team).Append('\n').Append(input.Proposal.Fact).Append('\n').Append(input.Proposal.Why).Append('\n');
        foreach (string line in input.Index)
        {
            _ = text.Append(line).Append('\n');
        }

        foreach (TeamMemoryCandidate candidate in input.Candidates)
        {
            _ = text.Append(candidate.Folder).Append('/').Append(candidate.File).Append(' ').Append(candidate.Version).Append('\n');
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, string> PromptValues(TeamMemoryInput input) => ValuesFor(input);

    /// <summary>The prompt values for <paramref name="input"/>.</summary>
    /// <param name="input">The input.</param>
    internal static Dictionary<string, string> ValuesFor(TeamMemoryInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        string candidates = input.Candidates.Count == 0
            ? "none"
            : string.Join("\n\n", input.Candidates.Select(static candidate => $"<file folder=\"{candidate.Folder}\" name=\"{candidate.File}\">\n{candidate.Text.TrimEnd()}\n</file>"));
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["{{team}}"] = input.Proposal.Team,
            ["{{proposer}}"] = input.Proposal.Proposer,
            ["{{fact}}"] = input.Proposal.Fact,
            ["{{why}}"] = string.IsNullOrWhiteSpace(input.Proposal.Why) ? "not given" : input.Proposal.Why,
            ["{{folders}}"] = input.Folders.Count == 0 ? input.Proposal.Team : string.Join(", ", input.Folders),
            ["{{index}}"] = input.Index.Count == 0 ? "(empty)" : string.Join('\n', input.Index),
            ["{{candidates}}"] = candidates,
        };
    }

    /// <inheritdoc/>
    public string? Validate(TeamMemoryVerdict verdict)
    {
        ArgumentNullException.ThrowIfNull(verdict);

        if (ActionOf(verdict) is not { } action)
        {
            return "the action must be none, create or update";
        }

        if (string.IsNullOrWhiteSpace(verdict.Reason))
        {
            return "the answer needs a reason";
        }

        if (action == TeamMemoryAction.None)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(verdict.Folder) || string.IsNullOrWhiteSpace(verdict.File)
            || string.IsNullOrWhiteSpace(verdict.Description) || string.IsNullOrWhiteSpace(verdict.Text)
            ? "a create or an update needs a folder, a file, a description and a text"
            : null;
    }

    /// <inheritdoc/>
    public async Task<HelperApplied> ApplyAsync(string key, TeamMemoryInput input, TeamMemoryVerdict verdict, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(verdict);

        string reason = CutReason(verdict.Reason ?? string.Empty);
        TeamMemoryAction? action = ActionOf(verdict);
        if (action is not (TeamMemoryAction.Create or TeamMemoryAction.Update))
        {
            this.NotFiled(input.Proposal, reason);
            return new HelperApplied.NoChange();
        }

        string label = (verdict.Folder ?? string.Empty).Trim();
        string file = (verdict.File ?? string.Empty).Trim();
        if (!paths.TryResolveTeamFolder(input.Proposal.Team, label, out MemoryFolder? folder))
        {
            return this.Refuse($"The answer names a folder outside {input.Proposal.Team}: {label}");
        }

        string? expectedVersion = null;
        if (action == TeamMemoryAction.Update)
        {
            TeamMemoryCandidate? shown = input.Candidates.FirstOrDefault(candidate =>
                string.Equals(candidate.Folder, folder.Label, StringComparison.OrdinalIgnoreCase) && string.Equals(candidate.File, file, StringComparison.OrdinalIgnoreCase));
            if (shown is null)
            {
                return this.Refuse($"The answer updates {folder.Label}/{file}, which it was not shown");
            }

            expectedVersion = shown.Version;
        }

        if (!settings.For(this).Enabled)
        {
            return new HelperApplied.Dropped("the Helper was turned off");
        }

        MemoryWriteResult result = await writer.WriteAsync(folder, file, verdict.Description, verdict.Text, expectedVersion, cancellationToken).ConfigureAwait(false);
        return this.Settle(input, folder, file, reason, result);
    }

    /// <summary>The action a Verdict names, ignoring case, or <see langword="null"/> when it names none of the three.</summary>
    /// <param name="verdict">The Verdict.</param>
    private static TeamMemoryAction? ActionOf(TeamMemoryVerdict verdict) => verdict.Action?.Trim().ToUpperInvariant() switch
    {
        "NONE" => TeamMemoryAction.None,
        "CREATE" => TeamMemoryAction.Create,
        "UPDATE" => TeamMemoryAction.Update,
        _ => null,
    };

    /// <summary>Turns the writer's result into what the Helper did: a filing, a refusal, or a stale Verdict to judge once more.</summary>
    /// <param name="input">The input the Verdict was made on.</param>
    /// <param name="folder">The resolved folder.</param>
    /// <param name="file">The file name.</param>
    /// <param name="reason">The Verdict's reason, cut.</param>
    /// <param name="result">What the writer did.</param>
    private HelperApplied Settle(TeamMemoryInput input, MemoryFolder folder, string file, string reason, MemoryWriteResult result)
    {
        switch (result)
        {
            case MemoryWriteResult.Written written:
                this.Record(input.Proposal, written, reason);
                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("Team Memory filed '{File}' in '{Folder}' for {Proposer}.", file, folder.Label, input.Proposal.Proposer);
                }

                return new HelperApplied.Changed($"{(written.Created ? "Created" : "Updated")} {folder.Label}/{file}");
            case MemoryWriteResult.BadName:
                return this.Refuse($"The answer's file name is not a memory file name: {file}");
            case MemoryWriteResult.BadDescription bad:
                return this.Refuse($"The answer's description for {file} is not usable: {bad.Problem}");
            case MemoryWriteResult.TooLarge tooLarge:
                return this.Refuse(string.Create(CultureInfo.InvariantCulture, $"The answer's {file} would be {tooLarge.Size} bytes; the most is {tooLarge.Max}"));
            case MemoryWriteResult.Failed failed:
                logger.LogWarning(failed.Error, "Team Memory could not write '{Path}'.", failed.Path);
                return new HelperApplied.Refused($"Could not write {folder.Label}/{file}");
            default:
                // Exists, Missing or Stale: someone wrote the file after the Helper was shown it.
                return this.Stale(input, folder, file);
        }
    }

    /// <summary>A stale Verdict: judged once more with fresh input and the named file shown in full, then dropped.</summary>
    /// <param name="input">The input the Verdict was made on.</param>
    /// <param name="folder">The resolved folder.</param>
    /// <param name="file">The file name.</param>
    private HelperApplied.Dropped Stale(TeamMemoryInput input, MemoryFolder folder, string file)
    {
        if (input.Attempt < 2)
        {
            trigger.Rejudge(input.Proposal, folder.Label, file);
            return new HelperApplied.Dropped($"re-judging: {folder.Label}/{file} changed");
        }

        string why = $"{folder.Label}/{file} changed twice while it was being filed";
        this.NotFiled(input.Proposal, why);
        return new HelperApplied.Dropped(why);
    }

    /// <summary>Records the write in the file's History, as the Helper on the proposer's behalf.</summary>
    /// <param name="proposal">The proposal.</param>
    /// <param name="written">The write.</param>
    /// <param name="reason">The Verdict's reason.</param>
    private void Record(TeamMemoryProposal proposal, MemoryWriteResult.Written written, string reason)
    {
        if (resolver.TryResolveAbsolute(written.Path, out LibraryPath? file))
        {
            DocChangeKind kind = written.Created ? DocChangeKind.Created : DocChangeKind.Edited;
            history.HelperWrote(file, ActorName, kind, $"Proposed by {proposal.Proposer}: {reason}");
        }
    }

    /// <summary>Posts the Feed record of a proposal that was not filed. A Feed that fails is logged, never raised.</summary>
    /// <param name="proposal">The proposal.</param>
    /// <param name="reason">Why it was not filed.</param>
    private void NotFiled(TeamMemoryProposal proposal, string reason)
    {
        string fact = proposal.Fact.Length > FeedFactLength ? string.Concat(proposal.Fact.AsSpan(0, FeedFactLength - 1), "…") : proposal.Fact;
        try
        {
            feed.Post(new FeedMessage(
                FeedSourceType.TeamMemoryNotFiled,
                "team-memory:" + proposal.Team,
                FeedActor.Huddle,
                false,
                new TeamMemoryNotFiledPayload(HelperId.Value, this.DisplayName, proposal.Team, proposal.Proposer, fact, reason)));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The Feed is a report on the Helper and never part of its work: a failing Feed is logged and the Helper carries on.
            logger.LogWarning(ex, "The Feed record of a Team Memory proposal by {Proposer} could not be posted.", proposal.Proposer);
        }
    }

    /// <summary>Refuses an invalid Verdict: logged, written nowhere, and counted as a failure on the card.</summary>
    /// <param name="message">What was wrong.</param>
    private HelperApplied.Refused Refuse(string message)
    {
        logger.LogWarning("Team Memory refused an answer: {Message}.", message);
        return new HelperApplied.Refused(message);
    }

    /// <summary>Cuts a reason to <see cref="MaxReasonLength"/>: 197 characters and an ellipsis.</summary>
    /// <param name="reason">The reason.</param>
    private static string CutReason(string reason)
    {
        string trimmed = reason.Trim();
        return trimmed.Length <= MaxReasonLength ? trimmed : string.Concat(trimmed.AsSpan(0, MaxReasonLength - 3), "...");
    }
}
