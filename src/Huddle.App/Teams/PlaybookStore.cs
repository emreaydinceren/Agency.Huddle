using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Services;

namespace Agency.Huddle.App.Teams;

/// <summary>
/// Reads and writes a Team's Playbook files, <c>Teams/&lt;Team&gt;/_member-playbook.md</c> and <c>_manager-playbook.md</c> (Team Playbooks spec §3.3, §5.1, §5.2):
/// Customize, Keep my version and Reset, the state of one Playbook for one Team, and the startup pruning of unnamed bases.
/// </summary>
/// <param name="teamsRoot">The Teams root folder.</param>
/// <param name="bases">Keeps the text of the defaults a file was customized from.</param>
/// <param name="prompts">Supplies the effective default of each kind.</param>
/// <param name="options">The size limit a Playbook is judged against.</param>
/// <param name="logger">Receives the warning for a file that cannot be read; <see langword="null"/> logs nothing.</param>
internal sealed class PlaybookStore(string teamsRoot, PlaybookBaseStore bases, IPromptSource prompts, PlaybookOptions options, ILogger<PlaybookStore>? logger = null)
{
    private const string MemberKey = "systemPrompt.memberPlaybook";

    private const string ManagerKey = "systemPrompt.managerPlaybook";

    private const string HashPrefix = "sha256:";

    /// <summary>Raised after a Playbook file is created, changed or removed here.</summary>
    public event Action? Changed;

    private string seenDefaults = string.Empty;

    /// <summary>Raises <see cref="Changed"/> after a save of either Playbook Prompt (spec §7.3), so an open tab shows the new default. Saves of other Prompts raise nothing here.</summary>
    /// <param name="promptStore">The store whose saves are followed; the subscription lasts as long as the process.</param>
    public void FollowPromptSaves(PromptStore promptStore)
    {
        this.seenDefaults = this.Defaults();
        promptStore.PromptsChanged += this.OnPromptsChanged;
    }

    /// <summary>Works out the state of one Playbook for one Team.</summary>
    /// <param name="team">The Team's name.</param>
    /// <param name="kind">Which Playbook.</param>
    /// <returns>The state, with the text the tab shows.</returns>
    public PlaybookState GetState(string team, PlaybookKind kind)
    {
        string path = this.FilePath(team, kind);
        string effective = prompts.Raw(KeyOf(kind));
        if (!File.Exists(path))
        {
            return new PlaybookState(PlaybookStatus.Default, effective, path, null, 0);
        }

        PlaybookFile file;
        try
        {
            file = PlaybookFile.Parse(File.ReadAllText(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger?.LogWarning(ex, "The {Kind} Playbook of Team {Team} at {Path} cannot be read; its Agents get the default.", PlaybookBaseStore.KindName(kind), team, path);
            return new PlaybookState(PlaybookStatus.Unreadable, effective, path, null, 0);
        }

        int over = file.Body.Length > options.MaxChars ? file.Body.Length : 0;
        string? hash = HashOf(file.BasedOn);
        if (hash is null)
        {
            return new PlaybookState(PlaybookStatus.UnknownBase, file.Body, path, null, over);
        }

        if (string.Equals(hash, PlaybookBaseStore.Hash(effective), StringComparison.Ordinal))
        {
            return new PlaybookState(PlaybookStatus.Customized, file.Body, path, null, over);
        }

        return new PlaybookState(PlaybookStatus.DefaultUpdated, file.Body, path, this.DiffFor(kind, hash, effective), over);
    }

    /// <summary>Writes the effective default as the Team's own Playbook, stamped with the hash of that default. A file that exists is never overwritten.</summary>
    /// <param name="team">The Team's name.</param>
    /// <param name="kind">Which Playbook.</param>
    /// <param name="ct">Cancels the write.</param>
    /// <returns><see langword="true"/> when the file was written; <see langword="false"/> when one existed and was left alone.</returns>
    public async Task<bool> CustomizeAsync(string team, PlaybookKind kind, CancellationToken ct)
    {
        string path = this.FilePath(team, kind);
        string effective = prompts.Raw(KeyOf(kind));
        string hash = bases.Save(kind, effective);
        string text = new PlaybookFile(HashPrefix + hash, effective).Compose();
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path) ?? teamsRoot);
        try
        {
            FileStream stream = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            await using (stream.ConfigureAwait(false))
            {
                await stream.WriteAsync(new System.Text.UTF8Encoding(false).GetBytes(text), ct).ConfigureAwait(false);
            }
        }
        catch (IOException) when (File.Exists(path))
        {
            return false;
        }

        this.RaiseChanged();
        return true;
    }

    /// <summary>Stamps the Playbook file with the hash of the current default, leaving its body and every other line as they were.</summary>
    /// <param name="team">The Team's name.</param>
    /// <param name="kind">Which Playbook.</param>
    /// <param name="ct">Cancels the write.</param>
    /// <returns>A task that completes when the file is written; nothing happens when there is no file.</returns>
    public async Task KeepMyVersionAsync(string team, PlaybookKind kind, CancellationToken ct)
    {
        string path = this.FilePath(team, kind);
        if (!File.Exists(path))
        {
            return;
        }

        PlaybookFile file = PlaybookFile.Parse(await File.ReadAllTextAsync(path, ct).ConfigureAwait(false));
        string hash = bases.Save(kind, prompts.Raw(KeyOf(kind)));
        PlaybookFile stamped = file with { BasedOn = HashPrefix + hash };
        AtomicFile.WriteText(path, stamped.Compose());
        this.RaiseChanged();
    }

    /// <summary>Deletes the Team's own Playbook, so the Team reads the default again.</summary>
    /// <param name="team">The Team's name.</param>
    /// <param name="kind">Which Playbook.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>A task that completes when the file is gone.</returns>
    public Task ResetAsync(string team, PlaybookKind kind, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        File.Delete(this.FilePath(team, kind));
        this.RaiseChanged();
        return Task.CompletedTask;
    }

    /// <summary>Deletes every base that no Team's Playbook file names, for the kind that names it. Run once at startup.</summary>
    public void PruneBases()
    {
        string folder = bases.Folder();
        if (!Directory.Exists(folder))
        {
            return;
        }

        HashSet<string> named = NamedBases();
        foreach (string path in Directory.EnumerateFiles(folder, "*.md"))
        {
            if (!named.Contains(Path.GetFileName(path)))
            {
                try
                {
                    File.Delete(path);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    logger?.LogWarning(ex, "The unused Playbook default {Path} could not be deleted.", path);
                }
            }
        }
    }

    /// <summary>The key of the Prompt that holds a kind's default.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The Prompt key.</returns>
    private static string KeyOf(PlaybookKind kind) => kind == PlaybookKind.Manager ? ManagerKey : MemberKey;

    /// <summary>The hash a <c>basedOn</c> value names.</summary>
    /// <param name="basedOn">The value, or <see langword="null"/>.</param>
    /// <returns>The 16 hex characters, or <see langword="null"/> when the value is not a stamp.</returns>
    private static string? HashOf(string? basedOn)
    {
        if (basedOn is null || !basedOn.StartsWith(HashPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        string hash = basedOn[HashPrefix.Length..];
        return hash.Length == 16 && hash.All(static c => char.IsAsciiHexDigitLower(c)) ? hash : null;
    }

    /// <summary>The file names of the bases some Playbook file names.</summary>
    /// <returns>The base file names.</returns>
    private HashSet<string> NamedBases()
    {
        HashSet<string> named = new(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(teamsRoot))
        {
            return named;
        }

        foreach (string team in Directory.EnumerateDirectories(teamsRoot))
        {
            foreach (PlaybookKind kind in new[] { PlaybookKind.Member, PlaybookKind.Manager })
            {
                string file = Path.Combine(team, PlaybookFile.FileName(kind == PlaybookKind.Manager));
                if (TryRead(file) is { } text && HashOf(PlaybookFile.Parse(text).BasedOn) is { } hash)
                {
                    _ = named.Add(Path.GetFileName(bases.PathFor(kind, hash)));
                }
            }
        }

        return named;
    }

    /// <summary>Reads a file that may be missing or locked.</summary>
    /// <param name="path">The file.</param>
    /// <returns>The text, or <see langword="null"/>.</returns>
    private static string? TryRead(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>The diff from the base a file started from to the current default.</summary>
    /// <param name="kind">Which Playbook.</param>
    /// <param name="hash">The hash the file names.</param>
    /// <param name="effective">The current default.</param>
    /// <returns>The unified diff, or <see langword="null"/> when Huddle no longer has the base.</returns>
    private string? DiffFor(PlaybookKind kind, string hash, string effective)
    {
        string? previous = bases.Read(kind, hash);
        if (previous is null)
        {
            return null;
        }

        return PlaybookDiff.Lines(PlaybookFile.FileName(kind == PlaybookKind.Manager), previous, effective);
    }

    /// <summary>The full path of a Team's Playbook file.</summary>
    /// <param name="team">The Team's name.</param>
    /// <param name="kind">Which Playbook.</param>
    /// <returns>The path.</returns>
    private string FilePath(string team, PlaybookKind kind) =>
        Path.Combine(teamsRoot, team, PlaybookFile.FileName(kind == PlaybookKind.Manager));

    /// <summary>Both defaults as one string, to tell a save of a Playbook Prompt from a save of any other.</summary>
    private string Defaults() => prompts.Raw(MemberKey) + "\0" + prompts.Raw(ManagerKey);

    private void OnPromptsChanged()
    {
        string now = this.Defaults();
        if (!string.Equals(Interlocked.Exchange(ref this.seenDefaults, now), now, StringComparison.Ordinal))
        {
            this.RaiseChanged();
        }
    }

    /// <summary>Raises <see cref="Changed"/>, so a throwing subscriber neither skips the others nor fails the write.</summary>
    private void RaiseChanged() =>
        EventRaise.Isolated(this.Changed, ex => logger?.LogWarning(ex, "A subscriber to the Playbook change threw."));
}
