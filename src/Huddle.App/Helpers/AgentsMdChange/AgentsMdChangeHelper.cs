using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization.Metadata;
using Agency.Huddle.App.AgentsMdChanges;
using Agency.Huddle.App.Feed;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Helpers.AgentsMdChange;

/// <summary>
/// Tells running Agents what changed when an <c>AGENTS.md</c> is edited (AgentsMdChanges Spec §6.4): it judges one edit and
/// answers with the rules added, changed or removed. Huddle checks each quote against the file before anything is said.
/// </summary>
/// <param name="ledger">The ledger that owns the head of each file.</param>
/// <param name="feed">Receives the record of an edit that was not announced.</param>
/// <param name="options">The AGENTS.md caps on what the Helper is shown.</param>
/// <param name="logger">Records each verdict that is applied or refused.</param>
internal sealed class AgentsMdChangeHelper(
    AgentsMdLedger ledger,
    IFeedPublisher feed,
    IOptions<TeamOptions> options,
    ILogger<AgentsMdChangeHelper> logger)
    : IHelperAgent<AgentsMdChangeInput, AgentsMdChangeVerdict>
{
    /// <summary>The id the Helper is registered, configured and stored under.</summary>
    public static readonly HelperId HelperId = new("AgentsMdChange");

    /// <summary>The most changes a meaningful Verdict may list.</summary>
    private const int MaxChanges = 12;

    /// <summary>The longest quote a change may carry.</summary>
    private const int MaxQuoteChars = 1000;

    /// <summary>What a new text over the cap is replaced by.</summary>
    private const string TooLongToShow = "(too long to show)";

    private static readonly string[] Kinds = ["added", "changed", "removed"];

    /// <inheritdoc/>
    public HelperId Id => HelperId;

    /// <inheritdoc/>
    public string DisplayName => "AGENTS.md Changes";

    /// <inheritdoc/>
    public string Description => "Tells running Agents what changed when an AGENTS.md is edited: lists the rules added, changed or removed, quoted from the file.";

    /// <inheritdoc/>
    public IReadOnlyList<string> PromptKeys { get; } = ["helper.agentsMdChange.system", "helper.agentsMdChange.edit"];

    /// <inheritdoc/>
    public HelperDefaults Defaults { get; } = new(Enabled: true, Adapter: "claude", Model: "haiku", Effort: "low", DailyCap: 20, ContinueGrant: 10, WaitSeconds: 2, TimeoutSeconds: 25);

    /// <inheritdoc/>
    public JsonTypeInfo<AgentsMdChangeVerdict> VerdictType => AgentsMdChangeJsonContext.Default.AgentsMdChangeVerdict;

    /// <inheritdoc/>
    public string Fingerprint(AgentsMdChangeInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        // The pair of hashes names the edit: the head it starts from and the text being judged.
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input.FromHash + "\n" + input.ToHash)));
    }

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, string> PromptValues(AgentsMdChangeInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        AgentsMdOptions caps = options.Value.AgentsMd;
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["{{path}}"] = input.RelativePath,
            ["{{level}}"] = input.LevelLabel,
            ["{{comparison}}"] = input.Comparison.ContextText(caps.MaxHelperComparisonChars),
            ["{{newText}}"] = input.NewText.Length > caps.MaxHelperFileChars ? TooLongToShow : input.NewText,
        };
    }

    /// <inheritdoc/>
    public string? Validate(AgentsMdChangeVerdict verdict)
    {
        ArgumentNullException.ThrowIfNull(verdict);

        if (string.IsNullOrWhiteSpace(verdict.Reason))
        {
            return "the answer needs a reason";
        }

        int count = verdict.Changes?.Count ?? 0;
        if (verdict.Meaningful && (count == 0 || count > MaxChanges))
        {
            return $"a meaningful answer lists 1 to {MaxChanges} changes";
        }

        return verdict.Changes?.Select(ProblemWith).FirstOrDefault(static problem => problem is not null);
    }

    /// <inheritdoc/>
    public async Task<HelperApplied> ApplyAsync(string key, AgentsMdChangeInput input, AgentsMdChangeVerdict verdict, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(verdict);

        // Step 1: the file must still be the text the Helper judged; a newer text has its own run.
        string current = await this.CurrentHashAsync(input.RelativePath, cancellationToken).ConfigureAwait(false);
        if (!string.Equals(current, input.ToHash, StringComparison.Ordinal))
        {
            return new HelperApplied.Dropped("changed again");
        }

        if (!verdict.Meaningful)
        {
            return this.ApplySkip(input, verdict);
        }

        // Step 3: every quote is checked before anything is said; one bad quote refuses the whole Verdict.
        IReadOnlyList<AgentsMdRuleChange> changes = verdict.Changes ?? [];
        foreach (AgentsMdRuleChange change in changes)
        {
            string text = string.Equals(change.Kind, "removed", StringComparison.OrdinalIgnoreCase) ? input.OldText : input.NewText;
            if (!text.Contains(Normalise(change.Quote), StringComparison.Ordinal))
            {
                return new HelperApplied.Refused($"The quote isn't in the file: {Normalise(change.Quote)}");
            }
        }

        // Step 4.
        if (!ledger.CommitVerdict(input.RelativePath, input.FromHash, input.ToHash, input.NewText, new AgentsMdNotice.Summary(changes)))
        {
            return new HelperApplied.Dropped("settled another way");
        }

        return new HelperApplied.Changed($"Told Agents about {changes.Count} changes in {input.RelativePath}");
    }

    /// <summary>Step 2: settles a Verdict that called the edit minor. A large edit is overruled with a comparison.</summary>
    /// <param name="input">The edit.</param>
    /// <param name="verdict">The Verdict.</param>
    private HelperApplied ApplySkip(AgentsMdChangeInput input, AgentsMdChangeVerdict verdict)
    {
        int changedChars = input.Comparison.ChangedChars;
        if (changedChars > options.Value.AgentsMd.MaxSkipChangedChars)
        {
            (IReadOnlyList<string> lines, int notShown) = input.Comparison.Lines(options.Value.AgentsMd.MaxComparisonChars);
            AgentsMdNotice comparison = new AgentsMdNotice.Comparison(lines, notShown);
            return ledger.CommitVerdict(input.RelativePath, input.FromHash, input.ToHash, input.NewText, comparison)
                ? new HelperApplied.Changed($"Told Agents: the Helper called it minor, but {changedChars} characters changed")
                : new HelperApplied.Dropped("settled another way");
        }

        string reason = verdict.Reason ?? string.Empty;
        if (!ledger.CommitVerdict(input.RelativePath, input.FromHash, input.ToHash, input.NewText, new AgentsMdNotice.Skipped(reason)))
        {
            return new HelperApplied.Dropped("settled another way");
        }

        this.PostSkipped(input, reason);
        return new HelperApplied.NoChange();
    }

    /// <summary>Posts the Feed record of an edit that was not announced. A Feed that fails is logged, never raised.</summary>
    /// <param name="input">The edit.</param>
    /// <param name="reason">Why the Helper skipped it.</param>
    private void PostSkipped(AgentsMdChangeInput input, string reason)
    {
        try
        {
            feed.Post(new FeedMessage(
                FeedSourceType.AgentsMdChangeSkipped,
                "agents-md:" + input.RelativePath,
                FeedActor.Huddle,
                false,
                new AgentsMdChangeSkippedPayload(HelperId.Value, this.DisplayName, input.RelativePath, reason)));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The Feed is a report on the Helper and never part of its work: a failing Feed is logged and the skip stands.
            logger.LogWarning(ex, "The Feed record of a skipped AGENTS.md edit in {Path} could not be posted.", input.RelativePath);
        }
    }

    /// <summary>The hash of the file on disk now; empty when it is missing or has no text.</summary>
    /// <param name="relativePath">The file, relative to the Teams root.</param>
    /// <param name="cancellationToken">Stops the read.</param>
    private async Task<string> CurrentHashAsync(string relativePath, CancellationToken cancellationToken)
    {
        TeamOptions team = options.Value;
        string path = Path.Combine(team.DataDir, team.Teams.Dir, relativePath);
        try
        {
            // Shared as the ledger reads it, so an editor or a scanner holding the file does not fail the read.
            FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            await using var streamScope = stream.ConfigureAwait(false);
            using StreamReader reader = new(stream, Encoding.UTF8);
            return AgentsMdBody.Parse(await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false)).Hash;
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return string.Empty;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Unreadable now is not the text that was judged: the edit is dropped and the next change judges it again.
            logger.LogWarning(ex, "Could not read AGENTS.md '{Path}' to check it is still the judged text; the Verdict is dropped.", relativePath);
            return string.Empty;
        }
    }

    /// <summary>Changes CR LF to LF and trims the ends, nothing else: whitespace inside a quote must match.</summary>
    /// <param name="quote">The quote.</param>
    private static string Normalise(string quote) => quote.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();

    /// <summary>What is wrong with one change, or <see langword="null"/> when it is usable.</summary>
    /// <param name="change">The change.</param>
    private static string? ProblemWith(AgentsMdRuleChange change)
    {
        if (change is null || !Kinds.Contains(change.Kind, StringComparer.OrdinalIgnoreCase))
        {
            return "each change needs a kind of added, changed or removed";
        }

        if (string.IsNullOrWhiteSpace(change.Summary))
        {
            return "each change needs a summary";
        }

        if (string.IsNullOrWhiteSpace(change.Quote))
        {
            return "each change needs a quote";
        }

        return change.Quote.Length > MaxQuoteChars ? $"a quote is at most {MaxQuoteChars} characters" : null;
    }
}
