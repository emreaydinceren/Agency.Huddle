using Agency.Huddle.App.Skills;
using Agency.Huddle.App.Teams;
using Agency.Huddle.SkillPacks;
using Agency.Huddle.SkillPacks.Checking;
using Agency.Huddle.SkillPacks.Folders;
using Agency.Huddle.SkillPacks.Resolving;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace Agency.Huddle.App.SkillSources;

/// <summary>
/// Puts a chosen Skill's files into <c>Skills/&lt;name&gt;/</c> safely and records where they came from (§6.6, §8.6): wait for
/// quiet, hash, copy into <c>.staging</c>, hash both again, check the copy, write <c>.source.json</c>, then swap through
/// <c>.trash</c>. It writes only inside <c>Skills/</c> plus one line in the Source's log, and never changes a Teammate file.
/// </summary>
internal sealed partial class SkillCopier : ISkillCopier
{
    /// <summary>How long the source must be quiet before a copy.</summary>
    public static readonly TimeSpan Quiet = TimeSpan.FromSeconds(2);

    /// <summary>The longest wait for quiet.</summary>
    public static readonly TimeSpan MaxQuietWait = TimeSpan.FromSeconds(30);

    private const int Attempts = 3;

    private readonly SkillRegistry registry;
    private readonly SkillSourcesSettings settings;
    private readonly SourceLog log;
    private readonly ILogger<SkillCopier> logger;
    private readonly TimeProvider clock;
    private readonly IFileReader source;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> gates = new(StringComparer.Ordinal);

    /// <summary>Creates the copier. <paramref name="sourceReader"/> reads the pointer folders; the disk by default.</summary>
    public SkillCopier(SkillRegistry registry, SkillSourcesSettings settings, SourceLog log, ILogger<SkillCopier> logger, TimeProvider? timeProvider = null, IFileReader? sourceReader = null)
    {
        this.registry = registry;
        this.settings = settings;
        this.log = log;
        this.logger = logger;
        this.clock = timeProvider ?? TimeProvider.System;
        this.source = sourceReader ?? PhysicalFileReader.Instance;
    }

    /// <inheritdoc/>
    public async Task<CopyResult> CopyAsync(IReadOnlyList<SkillEntry> skills, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(skills);
        List<CopiedSkill> copied = [];
        List<CopyFailure> failed = [];
        foreach (SkillEntry skill in skills)
        {
            SemaphoreSlim gate = this.gates.GetOrAdd(skill.Name, static _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                (CopiedSkill? done, CopyFailure? failure) = await this.CopyOneAsync(skill, ct).ConfigureAwait(false);
                if (done is not null)
                {
                    copied.Add(done);
                }

                if (failure is not null)
                {
                    failed.Add(failure);
                }
            }
            finally
            {
                gate.Release();
            }
        }

        return new CopyResult(copied, failed);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SkillSetupItem>> EnsureAvailableAsync(IReadOnlyList<string> skillNames, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(skillNames);
        List<SkillSetupItem> problems = [];
        List<SkillEntry> copyable = [];
        foreach (string name in skillNames.Distinct(StringComparer.Ordinal))
        {
            SkillAvailability availability = this.registry.Of(name);
            if (availability == SkillAvailability.Present)
            {
                continue;
            }

            SkillEntry? entry = this.registry.Current.Resolved.FindSkill(name);
            if (availability == SkillAvailability.Copyable && entry is not null)
            {
                copyable.Add(entry);
                continue;
            }

            problems.Add(new SkillSetupItem(SkillSetupKind.Skill, name, availability, this.DetailOf(entry)));
        }

        CopyResult result = await this.CopyAsync(copyable, ct).ConfigureAwait(false);
        problems.AddRange(result.Failed.Select(static failure => new SkillSetupItem(SkillSetupKind.Skill, failure.Name, SkillAvailability.Broken, failure.Message)));
        return problems;
    }

    /// <summary>The Source's reason for a withdrawn Skill, else empty.</summary>
    private string DetailOf(SkillEntry? entry)
    {
        return entry?.Source is { } sourceId ? this.registry.Current.Resolved.FindSource(sourceId)?.File?.StatusReason ?? string.Empty : string.Empty;
    }

    /// <summary>Copies one Skill (§6.6 "Per Skill").</summary>
    private async Task<(CopiedSkill? Done, CopyFailure? Failure)> CopyOneAsync(SkillEntry requested, CancellationToken ct)
    {
        string name = requested.Name;
        SkillEntry skill = this.registry.Current.Resolved.FindSkill(name) ?? requested;
        string target = Path.Combine(this.settings.SkillsDir, name);
        bool exists = Directory.Exists(target);
        ItemState state = this.registry.DisplayState(skill);
        bool usable = state is ItemState.Available or ItemState.UpdateAvailable or ItemState.Deprecated or ItemState.InUse or ItemState.New
            || (state == ItemState.Withdrawn && exists);
        if (!usable || skill.FolderPath is not { } pointer || skill.PathState != SkillPathState.Present)
        {
            return (null, new CopyFailure(name, CopyFailureKind.NotUsable, $"{name} is not ready to use ({Words(state)}). Fix it on Settings › Skills first."));
        }

        if (exists)
        {
            (CopiedSkill? Done, CopyFailure? Failure) early = this.CheckExisting(name, skill, pointer, target);
            if (early.Done is not null || early.Failure is not null)
            {
                return early;
            }
        }

        string staging = Path.Combine(this.settings.SkillsDir, SkillCopyFolders.Staging, $"{name}-{Guid.NewGuid():N}");
        try
        {
            string? hash = await this.CopyUntilStableAsync(pointer, staging, ct).ConfigureAwait(false);

            if (hash is null)
            {
                return (null, new CopyFailure(name, CopyFailureKind.Unstable, $"The files for {name} kept changing while Huddle copied them. Try again when your AI tool has finished."));
            }

            CheckResult checks = Checks.RunSkillFolder(staging, name, this.registry.BuildCheckContext(), new CheckOptions(), PhysicalFileReader.Instance, ct);
            if (checks.Findings.FirstOrDefault(static finding => finding.Severity == Severity.Error) is { } error)
            {
                return (null, new CopyFailure(name, CopyFailureKind.CheckFailed, $"{name} failed {error.Code} after copying: {error.Message}"));
            }

            this.Provenance(skill, pointer, hash).Write(staging);
            this.Swap(name, staging, target);
            this.log.Append(skill.Source ?? string.Empty, new LogLine("copied", $"skill:{name}", Data: new Dictionary<string, object?> { ["sha256"] = hash, ["outcome"] = exists ? "Replaced" : "Created" }));
            return (new CopiedSkill(name, hash, exists ? CopyOutcome.Replaced : CopyOutcome.Created), null);
        }
        catch (IOException exception)
        {
            return (null, new CopyFailure(name, CopyFailureKind.Io, $"Huddle could not write {name} to Skills/: {exception.Message}"));
        }
        catch (UnauthorizedAccessException exception)
        {
            return (null, new CopyFailure(name, CopyFailureKind.Io, $"Huddle could not write {name} to Skills/: {exception.Message}"));
        }
        finally
        {
            this.TryDeleteFolder(staging);
        }
    }

    /// <summary>Checks a Skill folder that already exists in Skills/ before it is replaced (§6.6 "Per Skill").</summary>
    /// <param name="name">The requested Skill name.</param>
    /// <param name="skill">The Skill being copied.</param>
    /// <param name="pointer">The folder the Skill is copied from.</param>
    /// <param name="target">The existing folder in Skills/.</param>
    /// <returns>A failure when the folder belongs to a different Skill, an already-current result when nothing needs copying, else neither.</returns>
    private (CopiedSkill? Done, CopyFailure? Failure) CheckExisting(string name, SkillEntry skill, string pointer, string target)
    {
        if (!SkillProvenance.TryRead(target, out SkillProvenance? existing) || existing is null || !OriginIdentity.Same(existing.EffectiveOrigin, skill.Origin))
        {
            return (null, new CopyFailure(name, CopyFailureKind.NameTaken, $"A different Skill called {name} is already in Skills/, so Huddle did not replace it."));
        }

        string pointerHash = SkillFolderHash.ComputeFolder(this.source, pointer);
        if (string.Equals(pointerHash, existing.Sha256, StringComparison.Ordinal)
            && string.Equals(SkillFolderHash.ComputeFolder(target), existing.Sha256, StringComparison.Ordinal))
        {
            return (new CopiedSkill(name, existing.Sha256, CopyOutcome.AlreadyCurrent), null);
        }

        return (null, null);
    }

    /// <summary>Copies the source folder into staging until the source, the source again and the copy agree (§8.6).</summary>
    /// <param name="pointer">The folder the Skill is copied from.</param>
    /// <param name="staging">The staging folder to copy into.</param>
    /// <param name="ct">Cancels the wait for the source to go quiet.</param>
    /// <returns>The folder hash, or <see langword="null"/> when the files kept changing.</returns>
    private async Task<string?> CopyUntilStableAsync(string pointer, string staging, CancellationToken ct)
    {
        string? hash = null;
        for (int attempt = 0; attempt < Attempts && hash is null; attempt++)
        {
            await this.WaitForQuietAsync(pointer, ct).ConfigureAwait(false);
            string before = SkillFolderHash.ComputeFolder(this.source, pointer);
            DeleteFolder(staging);
            CopyFolder(pointer, staging);
            string after = SkillFolderHash.ComputeFolder(this.source, pointer);
            string copy = SkillFolderHash.ComputeFolder(staging);
            if (string.Equals(before, after, StringComparison.Ordinal) && string.Equals(after, copy, StringComparison.Ordinal))
            {
                hash = copy;
            }
        }

        return hash;
    }

    /// <summary>Waits until no file in the source was written in the last two seconds, up to thirty seconds (§8.6 step 1).</summary>
    private async Task WaitForQuietAsync(string folder, CancellationToken ct)
    {
        TimeSpan waited = TimeSpan.Zero;
        while (waited < MaxQuietWait)
        {
            IReadOnlyList<string> files = this.source.EnumerateFiles(folder, recursive: true);
            DateTimeOffset newest = files.Count == 0 ? DateTimeOffset.MinValue : files.Max(this.source.LastWriteTime);
            TimeSpan age = this.clock.GetUtcNow() - newest;
            if (age >= Quiet)
            {
                return;
            }

            TimeSpan wait = Quiet - age;
            await Task.Delay(wait, this.clock, ct).ConfigureAwait(false);
            waited += wait;
        }
    }

    /// <summary>The <c>.source.json</c> record for a copy (§6.6, the mapping table).</summary>
    private SkillProvenance Provenance(SkillEntry skill, string pointer, string hash)
    {
        Match tree = TreeUrl().Match(skill.Origin);
        Agency.Huddle.SkillPacks.Files.SourceFile? sourceFile = skill.Source is null ? null : this.registry.Current.Resolved.FindSource(skill.Source)?.File;
        string repo = tree.Success ? tree.Groups["repo"].Value : sourceFile?.Url ?? $"local:{skill.Source}";
        string path = tree.Success ? tree.Groups["path"].Value : skill.Name;
        string commit = tree.Success ? tree.Groups["commit"].Value : sourceFile?.Commit ?? "unpinned";
        string publisher = skill.Publisher ?? OwnerOf(repo) ?? "unknown";
        return new SkillProvenance(repo, path, commit, hash, publisher, skill.License ?? "unknown", this.clock.GetLocalNow(), 1, skill.Source, skill.Origin, pointer);
    }

    /// <summary>The owner part of a GitHub-style repository URL.</summary>
    private static string? OwnerOf(string repo)
    {
        Match match = OwnerUrl().Match(repo);
        return match.Success ? match.Groups["owner"].Value : null;
    }

    /// <summary>The swap (§8.6 step 7): the old folder to <c>.trash</c>, the staging folder into place, then the trash deleted.
    /// A failure between the two renames puts the old folder back.</summary>
    private void Swap(string name, string staging, string target)
    {
        string? trashed = null;
        if (Directory.Exists(target))
        {
            string trashDir = Path.Combine(this.settings.SkillsDir, SkillCopyFolders.Trash);
            Directory.CreateDirectory(trashDir);
            trashed = Path.Combine(trashDir, $"{name}-{Guid.NewGuid():N}");
            Directory.Move(target, trashed);
        }

        try
        {
            Directory.Move(staging, target);
        }
        catch (IOException)
        {
            if (trashed is not null && !Directory.Exists(target))
            {
                Directory.Move(trashed, target);
            }

            throw;
        }

        if (trashed is not null)
        {
            this.TryDeleteFolder(trashed);
        }
    }

    /// <summary>Copies every file except <c>.git</c> and <c>.source.json</c>, never following a link or junction.</summary>
    private static void CopyFolder(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (string file in Directory.GetFiles(from))
        {
            string name = Path.GetFileName(file);
            if (string.Equals(name, RegistryResolver.ProvenanceFileName, StringComparison.Ordinal) || (File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
            {
                continue;
            }

            File.Copy(file, Path.Combine(to, name));
        }

        foreach (string folder in Directory.GetDirectories(from))
        {
            string name = Path.GetFileName(folder);
            if (string.Equals(name, ".git", StringComparison.Ordinal) || (File.GetAttributes(folder) & FileAttributes.ReparsePoint) != 0)
            {
                continue;
            }

            CopyFolder(folder, Path.Combine(to, name));
        }
    }

    /// <summary>Deletes a folder, unlinking rather than following a link.</summary>
    private static void DeleteFolder(string folder)
    {
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>Deletes a folder; a failure is logged and left for the next start-up sweep.</summary>
    private void TryDeleteFolder(string folder)
    {
        try
        {
            DeleteFolder(folder);
        }
        catch (IOException exception)
        {
            this.logger.LogWarning(exception, "Could not delete {Folder}; the next start empties it.", folder);
        }
        catch (UnauthorizedAccessException exception)
        {
            this.logger.LogWarning(exception, "Could not delete {Folder}; the next start empties it.", folder);
        }
    }

    /// <summary>A state in words, for a message.</summary>
    private static string Words(ItemState state)
    {
        return state switch
        {
            ItemState.NotDownloaded => "not downloaded",
            ItemState.Broken => "broken",
            ItemState.Withdrawn => "withdrawn",
            ItemState.UpdateAvailable => "update available",
            ItemState.InUse => "in use",
#pragma warning disable HAA0102 // Enum.ToString returns the runtime's cached name, so a lookup table would save nothing.
            _ => state.ToString().ToUpperInvariant() is { } upper ? new string([.. upper.Select(char.ToLowerInvariant)]) : string.Empty,
#pragma warning restore HAA0102
        };
    }

    [GeneratedRegex(@"\A(?<repo>https?://[^/]+/[^/]+/[^/]+)/(?:tree|blob)/(?<commit>[^/]+)/(?<path>.+)\z", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex TreeUrl();

    [GeneratedRegex(@"\Ahttps?://[^/]+/(?<owner>[^/]+)/", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex OwnerUrl();
}
