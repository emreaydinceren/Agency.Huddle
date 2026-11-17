using System.Text.Json.Serialization.Metadata;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.DocHistory;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Memory;

namespace Agency.Huddle.App.Helpers.MemoryTidy;

/// <summary>
/// Tidies one memory folder on the Verdict of a Helper (Memory Tidy spec §6.9 to §6.11). The Helper judges; Huddle checks every step (A-1 to A-7,
/// <see cref="MemoryTidyChecks"/>), writes the journal before and after it, and applies it through <see cref="MemoryWriter"/>, which copies each
/// file first (A-8 to A-10). A step that fails a check is refused and the others still run.
/// </summary>
/// <param name="paths">Resolves an Agent's own memory folder.</param>
/// <param name="writer">Applies a step under the folder's gate.</param>
/// <param name="history">Records each written file in its History.</param>
/// <param name="resolver">Finds a written file in the Library, for its History.</param>
/// <param name="store">Holds the day each file was written.</param>
/// <param name="settings">Tells whether the Helper has been turned off since the call started.</param>
/// <param name="logger">Records each fault.</param>
internal sealed class MemoryTidyHelper(
    MemoryPaths paths,
    MemoryWriter writer,
    DocHistoryRecorder history,
    LibraryPathResolver resolver,
    MemoryTidyStateStore store,
    HelperSettingsResolver settings,
    ILogger<MemoryTidyHelper> logger)
    : IHelperAgent<MemoryTidyInput, MemoryTidyVerdict>
{
    /// <summary>The id the Helper is registered, configured and stored under.</summary>
    public static readonly HelperId HelperId = new("MemoryTidy");

    /// <summary>The name in a file's History: the display name and <c>(Helper)</c>, which no Agent's Name can contain.</summary>
    internal const string ActorName = "Memory Tidy (Helper)";

    /// <inheritdoc/>
    public HelperId Id => HelperId;

    /// <inheritdoc/>
    public string DisplayName => "Memory Tidy";

    /// <inheritdoc/>
    public string Description =>
        "Tidies each Agent's Memory and Huddle-wide Memory once a week: merges files on one topic, removes what is out of date and fixes descriptions. "
        + "It is told to add nothing, Huddle refuses new numbers, dates and links, and a copy of every file it changes is kept.";

    /// <inheritdoc/>
    public IReadOnlyList<string> PromptKeys { get; } = ["helper.memoryTidy.system", "helper.memoryTidy.folder"];

    /// <inheritdoc/>
    public HelperDefaults Defaults { get; } = new(Enabled: true, Adapter: "claude", Model: "sonnet", Effort: "low", DailyCap: 20, ContinueGrant: 10, WaitSeconds: 1, TimeoutSeconds: 180);

    /// <summary>A Verdict is large and its folder's fingerprint already stops a second call, so the cache is neither read nor filled.</summary>
    public bool CachesVerdicts => false;

    /// <inheritdoc/>
    public JsonTypeInfo<MemoryTidyVerdict> VerdictType => MemoryTidyJsonContext.Default.MemoryTidyVerdict;

    /// <inheritdoc/>
    public string Fingerprint(MemoryTidyInput input) => MemoryTidyInputBuilder.Fingerprint(input);

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, string> PromptValues(MemoryTidyInput input) => MemoryTidyPrompt.Values(input);

    /// <summary>Checks the Verdict's shape and that no file is used twice. The caps need the input and are checked again in <see cref="ApplyAsync"/>.</summary>
    /// <param name="verdict">The Verdict.</param>
    /// <returns>The problem, or <see langword="null"/> when the Verdict can be applied.</returns>
    public string? Validate(MemoryTidyVerdict verdict) =>
        MemoryTidyValidation.Validate(verdict, new MemoryTidyLimits(int.MaxValue, int.MaxValue, int.MaxValue, DescriptionsLocked: false));

    /// <inheritdoc/>
    public async Task<HelperApplied> ApplyAsync(string key, MemoryTidyInput input, MemoryTidyVerdict verdict, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(verdict);

        if (!settings.For(this).Enabled)
        {
            return new HelperApplied.Dropped("the Helper was turned off");
        }

        if (MemoryTidyValidation.Validate(verdict, input.Limits) is { } problem)
        {
            return new HelperApplied.Refused(problem);
        }

        if (this.FolderOf(input.FolderKey) is not { } folder)
        {
            // Huddle-wide Memory is a later slice, and an Agent's folder behind a junction is never written.
            return new HelperApplied.NoChange();
        }

        input.Context.Journal.Append(new MemoryTidyEntry.FolderJudged(input.FolderKey));
        Tally tally = new();
        MemoryTidyDays days = new(store, input.FolderKey);
        foreach (MemoryTidyAction action in verdict.Actions ?? [])
        {
            if (MemoryTidyStep.TryParse(action, out _) is { } step)
            {
                tally.Number++;
                await this.ApplyStepAsync(step, folder, input, days, tally, cancellationToken).ConfigureAwait(false);
            }
        }

        if (tally.Faulted)
        {
            return new HelperApplied.Refused($"Could not tidy {LabelOf(input.FolderKey)}: a copy or the disk failed");
        }

        return tally.Changed ? new HelperApplied.Changed($"Tidied {LabelOf(input.FolderKey)}: {tally.Summary()}") : new HelperApplied.NoChange();
    }

    private static string KindOf(MemoryTidyStep step) => step switch
    {
        MemoryTidyStep.Merge => "merge",
        MemoryTidyStep.Rewrite => "rewrite",
        MemoryTidyStep.Describe => "describe",
        _ => "drop",
    };

    private static string LabelOf(string folderKey) => folderKey.StartsWith(MemoryTidyInputBuilder.AgentKeyPrefix, StringComparison.Ordinal)
        ? $"{folderKey[MemoryTidyInputBuilder.AgentKeyPrefix.Length..]}'s Memory"
        : "Huddle-wide Memory";

    /// <summary>Where a folder's copies sit in the pass folder, as the list names them: <c>Agents/&lt;Name&gt;</c> or <c>Huddle</c>.</summary>
    private static string CopyPrefixOf(string folderKey) => folderKey.StartsWith(MemoryTidyInputBuilder.AgentKeyPrefix, StringComparison.Ordinal)
        ? "Agents/" + folderKey[MemoryTidyInputBuilder.AgentKeyPrefix.Length..]
        : "Huddle";

    private static MemoryTidyChange ChangeOf(MemoryTidyStep step, MemoryTidyCheck check)
    {
        MemoryTidyFile kept = check.Files[0];
        MemoryTidyRemoval[] others = [.. check.Files.Skip(1).Select(static file => new MemoryTidyRemoval(file.File, file.Version))];
        return step switch
        {
            MemoryTidyStep.Merge merge => new MemoryTidyChange(new MemoryTidyWrite(kept.File, check.Description, merge.Text, kept.Version), others),
            MemoryTidyStep.Rewrite rewrite => new MemoryTidyChange(new MemoryTidyWrite(kept.File, check.Description, rewrite.Text, kept.Version), []),
            MemoryTidyStep.Describe => new MemoryTidyChange(new MemoryTidyWrite(kept.File, check.Description, null, kept.Version), []),
            _ => new MemoryTidyChange(null, [new MemoryTidyRemoval(kept.File, kept.Version)]),
        };
    }

    private static string HistorySummaryOf(MemoryTidyStep step, MemoryTidyCheck check) => step switch
    {
        MemoryTidyStep.Merge => $"Merged {string.Join(", ", check.Files.Skip(1).Select(static file => file.File))} into this file: {step.Reason}",
        MemoryTidyStep.Rewrite => $"Shortened: {step.Reason}",
        _ => $"New description: {step.Reason}",
    };

    private MemoryFolder? FolderOf(string folderKey)
    {
        if (!folderKey.StartsWith(MemoryTidyInputBuilder.AgentKeyPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        string name = folderKey[MemoryTidyInputBuilder.AgentKeyPrefix.Length..];
        return string.IsNullOrWhiteSpace(name) ? null : paths.OwnFolderOf(name);
    }

    private async Task ApplyStepAsync(MemoryTidyStep step, MemoryFolder folder, MemoryTidyInput input, MemoryTidyDays days, Tally tally, CancellationToken cancellationToken)
    {
        IMemoryTidyJournal journal = input.Context.Journal;
        MemoryTidyCheck check = MemoryTidyChecks.Run(step, input, tally.Drops);
        if (check.Refusal is { } refusal)
        {
            journal.Append(new MemoryTidyEntry.StepRefused(tally.Number, input.FolderKey, KindOf(step), step.Sources, step.Reason, refusal));
            tally.Refused++;
            return;
        }

        if (check.CountsAsDrop)
        {
            tally.Drops++;
        }

        journal.Append(new MemoryTidyEntry.StepStarted(
            tally.Number,
            input.FolderKey,
            KindOf(step),
            [.. check.Files.Select(static file => new MemoryTidyEntry.StepFile(file.File, file.Version))],
            step.Reason));
        MemoryTidyResult result = await writer.ReplaceAndRemoveAsync(folder, ChangeOf(step, check), input.Context.BackupFolder, cancellationToken).ConfigureAwait(false);
        this.Settle(result, step, check, folder, input, days, tally);
    }

    private void Settle(MemoryTidyResult result, MemoryTidyStep step, MemoryTidyCheck check, MemoryFolder folder, MemoryTidyInput input, MemoryTidyDays days, Tally tally)
    {
        IMemoryTidyJournal journal = input.Context.Journal;
        switch (result)
        {
            case MemoryTidyResult.Done done:
                this.Applied(done, step, check, folder, input, days, tally);
                break;
            case MemoryTidyResult.Stale stale:
                journal.Append(new MemoryTidyEntry.StepSkipped(tally.Number, input.FolderKey, stale.File));
                tally.Skipped++;
                break;
            case MemoryTidyResult.Refused refused:
                journal.Append(new MemoryTidyEntry.StepRefused(tally.Number, input.FolderKey, KindOf(step), step.Sources, step.Reason, refused.Why));
                tally.Refused++;
                break;
            case MemoryTidyResult.CopyFailed copyFailed:
                logger.LogWarning("Memory Tidy could not copy {File} of {Folder} to the backup; the step was not applied.", copyFailed.File, input.FolderKey);
                journal.Append(new MemoryTidyEntry.StepFaulted(tally.Number, input.FolderKey, copyFailed.File));
                tally.Faulted = true;
                break;
            case MemoryTidyResult.Failed failed:
                logger.LogWarning(failed.Error, "Memory Tidy could not write {File} of {Folder}.", failed.File, input.FolderKey);
                journal.Append(new MemoryTidyEntry.StepFaulted(tally.Number, input.FolderKey, failed.File));
                tally.Faulted = true;
                break;
        }
    }

    private void Applied(MemoryTidyResult.Done done, MemoryTidyStep step, MemoryTidyCheck check, MemoryFolder folder, MemoryTidyInput input, MemoryTidyDays days, Tally tally)
    {
        MemoryTidyFile kept = check.Files[0];
        bool writes = step is not MemoryTidyStep.Drop;
        string prefix = CopyPrefixOf(input.FolderKey);
        string[] removed = [.. step is MemoryTidyStep.Drop ? check.Files.Select(static file => file.File) : check.Files.Skip(1).Select(static file => file.File)];
        string[] leftBehind = [.. done.LeftBehind.Select(static path => Path.GetFileName(path))];
        input.Context.Journal.Append(new MemoryTidyEntry.StepApplied(
            tally.Number,
            input.FolderKey,
            writes ? [kept.File] : [],
            [.. removed.Except(leftBehind, StringComparer.Ordinal)],
            [.. done.Copies.Select(copy => prefix + "/" + Path.GetFileName(copy))],
            RemovedTokensOf(step, kept),
            leftBehind,
            step is MemoryTidyStep.Rewrite && check.CountsAsDrop,
            check.Note));
        tally.Applied(step, check);
        if (writes && done.WrittenVersion is { } version)
        {
            this.Record(folder, kept.File, step, check);
            if (step is MemoryTidyStep.Merge)
            {
                days.Merged(kept.File, [.. check.Files.Select(static file => file.File)], version);
            }
            else
            {
                days.Kept(kept.File, version);
            }
        }

        foreach (string file in removed.Except(leftBehind, StringComparer.Ordinal))
        {
            days.Removed(file);
        }
    }

    /// <summary>For a rewrite, the checked tokens of the old body that the new text no longer holds.</summary>
    private static string[] RemovedTokensOf(MemoryTidyStep step, MemoryTidyFile kept) => step is MemoryTidyStep.Rewrite rewrite
        ? [.. MemoryTidyTokens.NotIn(MemoryTidyChecks.BodyOf(kept.Text), rewrite.Text).Select(static token => token.Text)]
        : [];

    private void Record(MemoryFolder folder, string file, MemoryTidyStep step, MemoryTidyCheck check)
    {
        if (resolver.TryResolveAbsolute(Path.Combine(folder.Path, file), out LibraryPath? path))
        {
            history.HelperWrote(path, ActorName, DocChangeKind.Edited, HistorySummaryOf(step, check), postToFeed: false);
        }
    }

    /// <summary>What a call has done so far; one per <see cref="ApplyAsync"/>.</summary>
    private sealed class Tally
    {
        private int merged;
        private int shortened;
        private int described;
        private int dropped;

        /// <summary>The number of the step being applied, from 1; every journal entry of a step carries it.</summary>
        public int Number { get; set; }

        /// <summary>Drops the Verdict has counted so far: explicit drops and heavy rewrites that passed the checks.</summary>
        public int Drops { get; set; }

        public int Refused { get; set; }

        public int Skipped { get; set; }

        public bool Faulted { get; set; }

        /// <summary>Whether any file was written or removed.</summary>
        public bool Changed => this.merged + this.shortened + this.described + this.dropped > 0;

        public void Applied(MemoryTidyStep step, MemoryTidyCheck check)
        {
            switch (step)
            {
                case MemoryTidyStep.Merge:
                    this.merged++;
                    break;
                case MemoryTidyStep.Rewrite when check.CountsAsDrop:
                case MemoryTidyStep.Drop:
                    this.dropped++;
                    break;
                case MemoryTidyStep.Rewrite:
                    this.shortened++;
                    break;
                default:
                    this.described++;
                    break;
            }
        }

        /// <summary>The counts in words, such as <c>2 merged, 1 shortened, 1 dropped, 1 refused</c>.</summary>
        public string Summary()
        {
            List<string> parts = [];
            Add(parts, this.merged, "merged");
            Add(parts, this.shortened, "shortened");
            Add(parts, this.described, "described");
            Add(parts, this.dropped, "dropped");
            Add(parts, this.Refused, "refused");
            Add(parts, this.Skipped, "skipped");
            return string.Join(", ", parts);
        }

        private static void Add(List<string> parts, int count, string word)
        {
            if (count > 0)
            {
                parts.Add(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{count} {word}"));
            }
        }
    }
}
