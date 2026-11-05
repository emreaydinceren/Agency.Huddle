using System.Text;
using Agency.Huddle.App.Library;

namespace Agency.Huddle.App.AgentsMdChanges;

/// <summary>
/// Works out, at Turn start, which AGENTS.md changes a session has not heard and renders them (Spec §8.3). The new heard map is
/// built only from what was read from the ledger and the files: it never reads the head separately, because another Room may
/// commit while the block is rendered.
/// </summary>
/// <param name="ledger">The ledger of settled revisions.</param>
/// <param name="renderer">Renders the block.</param>
/// <param name="options">The AGENTS.md limits; <c>NoticeWaitSeconds</c> bounds the wait for a summary.</param>
/// <param name="logger">Records a file that could not be read.</param>
internal sealed class AgentsMdNoticeSource(AgentsMdLedger ledger, AgentsMdNoticeRenderer renderer, AgentsMdOptions options, ILogger<AgentsMdNoticeSource> logger)
{
    /// <summary>Collects what the session has not heard.</summary>
    /// <param name="scope">The files that apply to the session, general to specific.</param>
    /// <param name="heard">The hash per relative path the session last heard; null for a session from before the feature.</param>
    /// <param name="readsFiles">Whether the Adapter can read files, so the block points at them instead of carrying their text.</param>
    /// <param name="cancellationToken">Stops the collection.</param>
    /// <returns>The block, or null when there is nothing to tell, and the map after this Turn.</returns>
    public async Task<AgentsMdCollected> CollectAsync(IReadOnlyList<AgentsMdFile> scope, IReadOnlyDictionary<string, string>? heard, bool readsFiles, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        cancellationToken.ThrowIfCancellationRequested();

        StringComparer comparer = PathContainment.Comparison == StringComparison.OrdinalIgnoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        Dictionary<string, AgentsMdBody> bodies = this.ReadAll(scope, comparer);
        Dictionary<string, string> current = this.Resolve(scope, heard, bodies, comparer);

        List<AgentsMdFile> behind = [.. scope.Where(file => bodies.TryGetValue(file.RelativePath, out AgentsMdBody? body) && !string.Equals(current.GetValueOrDefault(file.RelativePath, string.Empty), body.Hash, StringComparison.Ordinal))];
        HashSet<string> inScope = new(scope.Select(file => file.RelativePath), comparer);
        Dictionary<string, string> outOfScope = current.Where(pair => !inScope.Contains(pair.Key)).ToDictionary(static pair => pair.Key, static pair => pair.Value, comparer);
        if (behind.Count == 0 && outOfScope.Count == 0)
        {
            return new AgentsMdCollected(null, current);
        }

        List<(AgentsMdFile File, string Hash)> pending = [];
        foreach (AgentsMdFile file in behind)
        {
            AgentsMdObservation observation = ledger.Observe(file);
            if (observation.Pending)
            {
                pending.Add((file, observation.Hash));
            }
        }

        await this.WaitForSummariesAsync(pending, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested(); // WaitForHeadAsync swallows a cancel: stop before the fallback settles anything.
        foreach (AgentsMdFile file in behind)
        {
            _ = ledger.CommitComparison(file);
        }

        foreach ((AgentsMdFile file, _) in pending)
        {
            AgentsMdBody? fresh = this.TryRead(file); // The text may have changed during the wait: tell the text as it is now.
            if (fresh is not null)
            {
                bodies[file.RelativePath] = fresh;
            }
        }

        bool unreadableNew = scope.Any(file => !bodies.ContainsKey(file.RelativePath) && !(heard?.ContainsKey(file.RelativePath) ?? false));
        Dictionary<string, string> updated = new(current, comparer);
        List<AgentsMdBlockEntry> entries = this.EntriesFor(behind, bodies, current, updated, unreadableNew ? [] : outOfScope);
        string block = entries.Count == 0 ? string.Empty : renderer.Render(entries, readsFiles);
        return new AgentsMdCollected(block.Length == 0 ? null : block, updated);
    }

    /// <summary>
    /// Asks for a judgement of each pending edit, then waits for the head to reach it until one deadline: the latest start plus
    /// <c>NoticeWaitSeconds</c> (Spec §8.3, O-8). Only an edit whose judgement is running is waited for, and that is read from
    /// the ledger after the ask, never from the ask's answer.
    /// </summary>
    private async Task WaitForSummariesAsync(List<(AgentsMdFile File, string Hash)> pending, CancellationToken cancellationToken)
    {
        List<(string Path, string Hash)> running = [];
        DateTimeOffset? latestStart = null;
        foreach ((AgentsMdFile file, string hash) in pending)
        {
            _ = ledger.TryStartJudgement(file, JudgementReason.TurnAsked);
            DateTimeOffset? started = ledger.JudgementStarted(file.RelativePath);
            if (started is { } at)
            {
                running.Add((file.RelativePath, hash));
                latestStart = latestStart is { } latest && latest > at ? latest : at;
            }
        }

        if (latestStart is not { } start)
        {
            return;
        }

        DateTimeOffset deadline = start.AddSeconds(Math.Max(0, options.NoticeWaitSeconds));
        await Task.WhenAll(running.Select(item => ledger.WaitForHeadAsync(item.Path, item.Hash, deadline, cancellationToken))).ConfigureAwait(false);
    }

    /// <summary>The entries for the files whose heard hash differs from the current one, with <paramref name="updated"/> moved on for each.</summary>
    private List<AgentsMdBlockEntry> EntriesFor(List<AgentsMdFile> behind, Dictionary<string, AgentsMdBody> bodies, Dictionary<string, string> current, Dictionary<string, string> updated, Dictionary<string, string> outOfScope)
    {
        List<AgentsMdBlockEntry> entries = [];
        foreach (AgentsMdFile file in behind)
        {
            string heardHash = current.GetValueOrDefault(file.RelativePath, string.Empty);
            AgentsMdBody body = bodies[file.RelativePath];
            string display = "Teams/" + file.RelativePath;
            if (heardHash.Length == 0)
            {
                if (body.Hash.Length > 0 && !outOfScope.ContainsValue(body.Hash))
                {
                    entries.Add(new AgentsMdBlockEntry.NowFollow(display, file.FullPath, body.Text));
                }

                updated[file.RelativePath] = body.Hash;
                continue;
            }

            IReadOnlyList<AgentsMdRevision>? revisions = ledger.Since(file.RelativePath, heardHash);
            if (revisions is null)
            {
                entries.Add(new AgentsMdBlockEntry.Unknown(display, file.FullPath, body.Text));
                updated[file.RelativePath] = body.Hash;
            }
            else if (revisions.Count > 0)
            {
                List<AgentsMdRevision> told = [.. revisions.Where(static revision => revision.Notice is not AgentsMdNotice.Skipped)];
                if (told.Count > 0)
                {
                    entries.Add(new AgentsMdBlockEntry.Changed(display, file.FullPath, told, body.Text));
                }

                updated[file.RelativePath] = revisions[^1].ToHash;
            }
        }

        HashSet<string> followed = [.. bodies.Values.Select(static body => body.Hash)];
        foreach ((string path, string hash) in outOfScope)
        {
            if (hash.Length > 0 && !followed.Contains(hash))
            {
                entries.Add(new AgentsMdBlockEntry.NoLonger("Teams/" + path));
            }

            _ = updated.Remove(path);
        }

        return entries;
    }

    /// <summary>The heard map to compare with: the session's own, or each file's first sight when the session has none.</summary>
    private Dictionary<string, string> Resolve(IReadOnlyList<AgentsMdFile> scope, IReadOnlyDictionary<string, string>? heard, Dictionary<string, AgentsMdBody> bodies, StringComparer comparer)
    {
        if (heard is not null)
        {
            return new Dictionary<string, string>(heard, comparer);
        }

        Dictionary<string, string> resolved = new(comparer);
        foreach (AgentsMdFile file in scope)
        {
            string? firstSight = ledger.FirstSightHash(file.RelativePath);
            if (bodies.TryGetValue(file.RelativePath, out AgentsMdBody? body))
            {
                resolved[file.RelativePath] = firstSight ?? body.Hash;
            }
            else if (firstSight is not null)
            {
                resolved[file.RelativePath] = firstSight; // Unreadable now: keep what the ledger first saw, so the edit is told once it reads.
            }
        }

        return resolved;
    }

    /// <summary>Reads each file once; a file that cannot be read is left out.</summary>
    private Dictionary<string, AgentsMdBody> ReadAll(IReadOnlyList<AgentsMdFile> scope, StringComparer comparer)
    {
        Dictionary<string, AgentsMdBody> bodies = new(comparer);
        foreach (AgentsMdFile file in scope)
        {
            AgentsMdBody? body = this.TryRead(file);
            if (body is not null)
            {
                bodies[file.RelativePath] = body;
            }
        }

        return bodies;
    }

    /// <summary>Reads one file. A missing file has no text; null when the read fails, so the caller leaves it out.</summary>
    private AgentsMdBody? TryRead(AgentsMdFile file)
    {
        if (Directory.Exists(file.FullPath))
        {
            logger.LogWarning("AGENTS.md '{Path}' is a folder; leaving it out of the notice.", file.RelativePath);
            return null;
        }

        try
        {
            using FileStream stream = new(file.FullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using StreamReader reader = new(stream, Encoding.UTF8);
            return AgentsMdBody.Parse(reader.ReadToEnd());
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return AgentsMdBody.Absent;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not read AGENTS.md '{Path}'; leaving it out of the notice.", file.RelativePath);
            return null;
        }
    }
}
