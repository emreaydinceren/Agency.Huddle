using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Memory;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Helpers.MemoryTidy;

/// <summary>
/// The part of a Memory Tidy pass that asks the Helper (Memory Tidy spec §6.5 and §6.7): every Agent's own folder, least recently tidied first, is
/// judged when it holds enough files, has changed since its last Verdict and has not failed twice with its present content. It keeps the marker, the
/// failure count and the offset in <see cref="MemoryTidyStateStore"/>, and ends the pass at the Helper's daily cap or at three failed calls in a row.
/// Huddle-wide Memory is a later slice: it joins <see cref="Candidates"/>.
/// </summary>
/// <param name="personas">The catalog; every Persona's own folder is a candidate.</param>
/// <param name="paths">Resolves an Agent's own memory folder.</param>
/// <param name="builder">Builds what the Helper sees and gives the folder's fingerprint.</param>
/// <param name="store">Holds each folder's marker, failure count and offset.</param>
/// <param name="judge">Runs one judgement under the shared limits and the card's bookkeeping.</param>
/// <param name="helper">The Helper, whose settings are asked for the daily cap and whether it is on.</param>
/// <param name="settings">Resolves the Helper's effective settings.</param>
/// <param name="budget">Tells whether the daily cap is reached.</param>
/// <param name="options">Supplies <c>MinFiles</c>.</param>
/// <param name="timeProvider">The clock; the system clock when <see langword="null"/>.</param>
internal sealed class MemoryTidyJudging(
    PersonaStore personas,
    MemoryPaths paths,
    MemoryTidyInputBuilder builder,
    MemoryTidyStateStore store,
    HelperJudge judge,
    MemoryTidyHelper helper,
    HelperSettingsResolver settings,
    HelperBudget budget,
    IOptions<TeamOptions> options,
    TimeProvider? timeProvider = null)
{
    private const int FailedCallsThatEndAPass = 3;
    private const int FailuresThatParkAFolder = 2;

    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;

    /// <summary>What one folder came to, as far as the loop is concerned.</summary>
    private enum FolderResult
    {
        /// <summary>No call was made or none counts: the loop goes on.</summary>
        Skipped,

        /// <summary>The call ran and failed; the count is held back.</summary>
        Failed,

        /// <summary>The Helper's daily cap ended the pass.</summary>
        Cap,
    }

    /// <summary>Judges the folders in turn and says how the loop ended.</summary>
    /// <param name="start">What started the pass; a pass the Human did not start asks the budget before each call.</param>
    /// <param name="passFolder">The pass folder.</param>
    /// <param name="journal">The pass's journal.</param>
    /// <param name="cancellationToken">The pass's stopping token.</param>
    /// <returns><see cref="MemoryTidyPassEnd.Done"/>, <see cref="MemoryTidyPassEnd.Cap"/> or <see cref="MemoryTidyPassEnd.ThreeFailures"/>.</returns>
    public async Task<MemoryTidyPassEnd> RunAsync(MemoryTidyStart start, string passFolder, IMemoryTidyJournal journal, CancellationToken cancellationToken)
    {
        string passId = Path.GetFileName(Path.TrimEndingDirectorySeparator(passFolder));
        DateOnly today = DateOnly.FromDateTime(this.clock.GetLocalNow().Date);
        List<(string Key, string Fingerprint)> held = [];
        try
        {
            foreach (Candidate candidate in this.Candidates())
            {
                cancellationToken.ThrowIfCancellationRequested();
                MemoryTidyPassContext context = new(passId, MemoryTidyBackups.FolderFor(passFolder, candidate.Key), journal);
                (FolderResult result, string? failedFingerprint) = await this.JudgeAsync(candidate, start, passFolder, context, today, held, cancellationToken).ConfigureAwait(false);
                if (result == FolderResult.Cap)
                {
                    return MemoryTidyPassEnd.Cap;
                }

                if (result == FolderResult.Failed && failedFingerprint is not null)
                {
                    held.Add((candidate.Key, failedFingerprint));
                    if (held.Count >= FailedCallsThatEndAPass)
                    {
                        // The Adapter or the model is down: none of the three is the folder's fault, so none is counted.
                        held.Clear();
                        return MemoryTidyPassEnd.ThreeFailures;
                    }
                }
            }

            return MemoryTidyPassEnd.Done;
        }
        finally
        {
            // A pass that ends any way but on three failures counts the failures it still holds: no later call came to say the Adapter was down.
            this.CountHeld(held);
        }
    }

    /// <summary>Every Agent's own folder and Huddle-wide Memory, never-tidied first, then the longest ago; among folders never tidied the Agents come first.</summary>
    private List<Candidate> Candidates()
    {
        List<Candidate> candidates = [];
        foreach (string name in personas.Entries.Select(static entry => entry.Name).Order(StringComparer.Ordinal))
        {
            if (paths.OwnFolderOf(name) is { } folder)
            {
                string key = MemoryTidyInputBuilder.AgentKeyPrefix + name;
                candidates.Add(new Candidate(key, "the Agent " + name, folder, store.GetState(key)?.TidiedUtc ?? DateTimeOffset.MinValue));
            }
        }

        if (paths.TryResolveHuddleWide(out MemoryFolder? huddle))
        {
            candidates.Add(new Candidate(MemoryTidyInputBuilder.HuddleKey, "every Agent (Huddle-wide Memory)", huddle, store.GetState(MemoryTidyInputBuilder.HuddleKey)?.TidiedUtc ?? DateTimeOffset.MinValue));
        }

        return [.. candidates.OrderBy(static candidate => candidate.Tidied).ThenBy(static candidate => candidate.Key, StringComparer.Ordinal)];
    }

    /// <summary>Looks at one folder: the checks before a call, the call, and what is stored after it.</summary>
    private async Task<(FolderResult Result, string? FailedFingerprint)> JudgeAsync(
        Candidate candidate,
        MemoryTidyStart start,
        string passFolder,
        MemoryTidyPassContext context,
        DateOnly today,
        List<(string Key, string Fingerprint)> held,
        CancellationToken cancellationToken)
    {
        if (builder.Build(candidate.Folder, candidate.Key, candidate.Owner, today, context) is not { } build)
        {
            return (FolderResult.Skipped, null);
        }

        MemoryTidyState? state = store.GetState(candidate.Key);
        if (build.TopicCount < options.Value.Memory.Tidy.MinFiles || string.Equals(state?.Fingerprint, build.FolderFingerprint, StringComparison.Ordinal))
        {
            return (FolderResult.Skipped, null);
        }

        if (state is { Failures: >= FailuresThatParkAFolder } && string.Equals(state.FailedFingerprint, build.FolderFingerprint, StringComparison.Ordinal))
        {
            context.Journal.Append(new MemoryTidyEntry.FolderNotJudged(candidate.Key, "failed twice"));
            return (FolderResult.Skipped, null);
        }

        if (start != MemoryTidyStart.TidyNow && budget.IsPaused(MemoryTidyHelper.HelperId, settings.For(helper).DailyCap))
        {
            return (FolderResult.Cap, null);
        }

        HelperRunReport? report = await judge.JudgeAsync(MemoryTidyHelper.HelperId, candidate.Key, build.Input, cancellationToken).ConfigureAwait(false);
        return this.Settle(report, candidate, passFolder, context, build, state, today, held);
    }

    /// <summary>Stores what the report allows and says how the loop goes on.</summary>
    private (FolderResult Result, string? FailedFingerprint) Settle(
        HelperRunReport? report,
        Candidate candidate,
        string passFolder,
        MemoryTidyPassContext context,
        MemoryTidyBuild build,
        MemoryTidyState? state,
        DateOnly today,
        List<(string Key, string Fingerprint)> held)
    {
        if (report is null)
        {
            if (!settings.For(helper).Enabled)
            {
                context.Journal.Append(new MemoryTidyEntry.FolderNotJudged(candidate.Key, "the Helper was turned off"));
            }

            return (FolderResult.Skipped, null);
        }

        if (report.Kind == HelperRunKind.OverBudget)
        {
            return (FolderResult.Cap, null);
        }

        (bool judged, bool faulted) = Seen(passFolder, candidate.Key);
        if (report.Kind == HelperRunKind.Failed && report.CallRan && !judged)
        {
            return (FolderResult.Failed, build.FolderFingerprint);
        }

        if (judged && !faulted)
        {
            this.StoreMarker(candidate, build, state, report, today, context);
        }

        if (judged || report.Kind == HelperRunKind.Applied)
        {
            this.CountHeld(held);
            held.Clear();
        }

        return (FolderResult.Skipped, null);
    }

    private void StoreMarker(Candidate candidate, MemoryTidyBuild build, MemoryTidyState? state, HelperRunReport report, DateOnly today, MemoryTidyPassContext context)
    {
        // The fingerprint of the folder as the pass left it, so its own writes do not make it look changed next week.
        if (builder.Build(candidate.Folder, candidate.Key, candidate.Owner, today, context) is not { } after)
        {
            return;
        }

        int nextOffset = MemoryTidyInputBuilder.NextOffset(build.Offset, build.Shown, build.EligibleCount, build.AllFitted);
        store.PutState((state ?? new MemoryTidyState(candidate.Key)) with
        {
            // A folder larger than one call keeps no marker until the rotation has come round, so the next pass shows its next files.
            Fingerprint = nextOffset <= build.Offset ? after.FolderFingerprint : null,
            TidiedUtc = this.clock.GetUtcNow(),
            Outcome = report.Summary ?? report.Error ?? "Judged",
            Failures = 0,
            FailedFingerprint = null,
            Offset = nextOffset,
        });
    }

    /// <summary>Adds one failure to each folder whose failed call was held back, now that a later call in the same pass succeeded.</summary>
    private void CountHeld(List<(string Key, string Fingerprint)> held)
    {
        foreach ((string key, string fingerprint) in held)
        {
            MemoryTidyState state = store.GetState(key) ?? new MemoryTidyState(key);
            int before = string.Equals(state.FailedFingerprint, fingerprint, StringComparison.Ordinal) ? state.Failures : 0;
            store.PutState(state with { Failures = before + 1, FailedFingerprint = fingerprint });
        }
    }

    private static (bool Judged, bool Faulted) Seen(string passFolder, string key)
    {
        bool judged = false;
        bool faulted = false;
        foreach (MemoryTidyEntry entry in MemoryTidyJournal.Read(passFolder))
        {
            switch (entry)
            {
                case MemoryTidyEntry.FolderJudged entryJudged when string.Equals(entryJudged.FolderKey, key, StringComparison.Ordinal):
                    judged = true;
                    break;
                case MemoryTidyEntry.StepFaulted entryFaulted when string.Equals(entryFaulted.FolderKey, key, StringComparison.Ordinal):
                    faulted = true;
                    break;
                default:
                    break;
            }
        }

        return (judged, faulted);
    }

    private sealed record Candidate(string Key, string Owner, MemoryFolder Folder, DateTimeOffset Tidied);
}
