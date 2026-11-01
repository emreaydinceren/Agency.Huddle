using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Teams;
using Agency.Huddle.App.TeamTypes;
using Agency.Huddle.SkillPacks.Reading;
using Agency.Huddle.SkillPacks.Resolving;

namespace Agency.Huddle.App.SkillSources;

/// <summary>The remove and hide flows of §6.12. Huddle deletes only inside the Skill Sources folder and only what each flow
/// names; a clone, a file outside the folder and a <c>Skills/</c> copy are never deleted. Order is safest first, Agents,
/// then the row, then the file, then notes, so a failure leaves something re-runnable. Every flow logs to the Source's
/// <c>log.jsonl</c> when it has one. Callers rebuild the registry afterwards.</summary>
internal sealed partial class SkillSourcesRemover(
    SkillRegistry registry,
    SkillSourcesSettings settings,
    SkillsMdWriter writer,
    HiddenStore hidden,
    SourceLog log,
    PersonaStore personas,
    TeamTypeCatalog types,
    ILogger<SkillSourcesRemover> logger)
{
    /// <summary>The refusal for a Source id that resolves outside <c>sources/</c>.</summary>
    public const string OutsideSourcesRefusal = "That Source is not in the Skill Sources folder, so Huddle did not touch it.";

    /// <summary>The outcome when SKILLS.md cannot be changed.</summary>
    public const string RowNotRemoved = "SKILLS.md could not be changed, so the row is still there. Try again.";

    /// <summary>The start of the outcome when the Source's notes folder could not be deleted; the system's reason follows.</summary>
    public const string NotesNotDeleted = "The rows are gone, but the Source's notes could not be deleted: ";

    /// <summary>Whether <paramref name="path"/>, made full, lies inside <paramref name="folder"/> (§6.12.1 rule 2).</summary>
    public static bool IsInside(string path, string folder) => PathContainment.IsInside(path, folder, includeRoot: false, PathContainment.Comparison);

    /// <summary>Removes a Pack: its holders first (as <paramref name="choice"/> says), then its SKILLS.md row, then its file
    /// when it is inside the folder, then its <c>hidden.json</c> entry, then the log line.</summary>
    public RemovalOutcome RemovePack(PackEntry pack, HolderChoice choice)
    {
        ArgumentNullException.ThrowIfNull(pack);
        IReadOnlyList<string> holders = registry.HolderNames(pack);
        if (holders.Count > 0 && choice != HolderChoice.None && this.ChangeHolders(pack, holders, choice) is { } failure)
        {
            return failure;
        }

        if (pack.FilePath is null || !this.TryRemoveRow(RowKind.Pack, pack.FilePath))
        {
            return new RemovalOutcome(false, RowNotRemoved);
        }

        (bool deleted, RemovalOutcome? fileFailure) = this.DeleteIfInside(pack.FilePath);
        _ = hidden.Unhide(HiddenKind.Pack, pack.Id);
        string agents = choice == HolderChoice.RemoveToo ? "removed" : "kept";
        if (holders.Count == 0)
        {
            agents = "none";
        }

        log.Append(pack.Source, new LogLine("removed", $"pack:{pack.Id}", Data: new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["row"] = this.Relative(pack.FilePath),
            ["fileDeleted"] = deleted,
            ["agents"] = agents,
            ["holders"] = holders,
        }));
        return fileFailure ?? new RemovalOutcome(true, $"{pack.Title} removed.");
    }

    /// <summary>Takes a withdrawn Pack off every Agent that holds it (step 1 of Remove too only); its row and file stay.</summary>
    public RemovalOutcome UnholdPack(PackEntry pack)
    {
        ArgumentNullException.ThrowIfNull(pack);
        IReadOnlyList<string> holders = registry.HolderNames(pack);
        if (this.ChangeHolders(pack, holders, HolderChoice.RemoveToo) is { } failure)
        {
            return failure;
        }

        log.Append(pack.Source, new LogLine("unheld", $"pack:{pack.Id}", Data: new Dictionary<string, object?>(StringComparer.Ordinal) { ["holders"] = holders }));
        string count = Agency.Huddle.App.Components.Shared.Plural.Of(holders.Count, "Agent", "Agents");
        return new RemovalOutcome(true, $"{pack.Title} was taken off {count}.");
    }

    /// <summary>Removes a catalog: its SKILLS.md row, its file when inside the folder, and the log line. <c>hidden.json</c>
    /// is not touched.</summary>
    public RemovalOutcome RemoveCatalog(ResolvedCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        IReadOnlyList<string> broken = [.. registry.Current.Resolved.PacksUsingCatalog(catalog.Id).Select(static pack => pack.Id)];
        if (catalog.FilePath is null || !this.TryRemoveRow(RowKind.Catalog, catalog.FilePath))
        {
            return new RemovalOutcome(false, RowNotRemoved);
        }

        (bool deleted, RemovalOutcome? fileFailure) = this.DeleteIfInside(catalog.FilePath);
        log.Append(catalog.Source, new LogLine("removed", $"catalog:{catalog.Id}", Data: new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["row"] = this.Relative(catalog.FilePath),
            ["fileDeleted"] = deleted,
            ["skills"] = catalog.SkillNames.Count,
            ["packsBroken"] = broken,
        }));
        return fileFailure ?? new RemovalOutcome(true, $"Catalog {catalog.Title} removed.");
    }

    /// <summary>Removes a Source: each row that names it, in SKILLS.md order, and their files inside the folder; then the
    /// Source's folder (a reparse point inside is unlinked, never followed) and its empty parents up to <c>sources/</c>.</summary>
    public RemovalOutcome RemoveSource(string sourceId)
    {
        ArgumentNullException.ThrowIfNull(sourceId);
        string sources = Path.Combine(settings.Dir, "sources");
        string? folder = SourceId.IsValid(sourceId) ? SourceId.FolderOf(settings.Dir, sourceId) : null;
        if (folder is null || !IsInside(folder, sources))
        {
            return new RemovalOutcome(false, OutsideSourcesRefusal);
        }

        ResolvedRegistry resolved = registry.Current.Resolved;
        List<(RowKind Kind, string? File)> rows =
        [
            .. resolved.Packs.Where(pack => string.Equals(pack.Source, sourceId, StringComparison.OrdinalIgnoreCase)).Select(static pack => (RowKind.Pack, pack.FilePath)),
            .. resolved.Catalogs.Where(catalog => string.Equals(catalog.Source, sourceId, StringComparison.OrdinalIgnoreCase)).Select(static catalog => (RowKind.Catalog, catalog.FilePath)),
        ];
        foreach ((RowKind kind, string? file) in rows)
        {
            if (file is not null && !this.TryRemoveRow(kind, file))
            {
                return new RemovalOutcome(false, "SKILLS.md could not be changed, so the Source and its notes are still there. Try again.");
            }
        }

        foreach ((_, string? file) in rows)
        {
            if (file is not null)
            {
                _ = this.DeleteIfInside(file);
            }
        }

        try
        {
            if (Directory.Exists(folder))
            {
                DeleteTree(new DirectoryInfo(folder));
            }

            DeleteEmptyParents(folder, sources);
        }
        catch (IOException ex)
        {
            return new RemovalOutcome(false, NotesNotDeleted + ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return new RemovalOutcome(false, NotesNotDeleted + ex.Message);
        }

        LogSourceRemoved(logger, sourceId, rows.Count);
        string removed = Agency.Huddle.App.Components.Shared.Plural.Of(rows.Count, "row", "rows");
        return new RemovalOutcome(true, $"Source removed. {removed} and their files are gone; the clone and Agents' copies are untouched.");
    }

    /// <summary>Hides or unhides a Pack or Skill in <c>hidden.json</c> and logs it in its Source's log (§6.12.4).</summary>
    /// <returns>The written result; <see cref="HiddenWriteResult.Refused"/> leaves a malformed file as it is.</returns>
    public HiddenWriteResult SetHidden(HiddenKind kind, string name, string? sourceId, bool hide)
    {
        ArgumentNullException.ThrowIfNull(name);
        HiddenWriteResult result = hide ? hidden.Hide(kind, name) : hidden.Unhide(kind, name);
        if (result == HiddenWriteResult.Written && sourceId is not null)
        {
            log.Append(sourceId, new LogLine(hide ? "hidden" : "unhidden", kind == HiddenKind.Pack ? $"pack:{name}" : $"skill:{name}"));
        }

        return result;
    }

    /// <summary>Deletes a directory tree, unlinking a reparse point instead of following it.</summary>
    private static void DeleteTree(DirectoryInfo directory)
    {
        if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            directory.Delete(recursive: false);
            return;
        }

        foreach (FileSystemInfo entry in directory.EnumerateFileSystemInfos())
        {
            if (entry is DirectoryInfo child)
            {
                DeleteTree(child);
            }
            else
            {
                entry.Attributes = FileAttributes.Normal;
                entry.Delete();
            }
        }

        directory.Delete(recursive: false);
    }

    /// <summary>Deletes the empty folders above <paramref name="folder"/>, stopping at <paramref name="stop"/>.</summary>
    private static void DeleteEmptyParents(string folder, string stop)
    {
        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(stop));
        DirectoryInfo? parent = new DirectoryInfo(folder).Parent;
        while (parent is not null && IsInside(parent.FullName, root) && parent.Exists && !parent.EnumerateFileSystemInfos().Any())
        {
            parent.Delete();
            parent = parent.Parent;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Source {SourceId} removed with {Rows} rows")]
    private static partial void LogSourceRemoved(ILogger logger, string sourceId, int rows);

    /// <summary>Step 1 for each holder; the first failure stops before SKILLS.md is touched.</summary>
    private RemovalOutcome? ChangeHolders(PackEntry pack, IReadOnlyList<string> holders, HolderChoice choice)
    {
        PackContents contents = PackContents.From(pack);
        foreach (string name in holders)
        {
            if (personas.Get(name) is not { } persona)
            {
                continue;
            }

            string text = choice == HolderChoice.Keep
                ? PackMembership.DropRecord(persona.Text, pack.Id)
                : PackMembership.Remove(persona.Text, contents, this.RoleSkills(persona.Text), this.OtherPacks(persona.Text, pack.Id));
            try
            {
                if (!string.Equals(text, persona.Text, StringComparison.Ordinal))
                {
                    _ = personas.Update(persona.Name, text, persona.Model, persona.Effort, persona.WorkMode);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Agency.Huddle.App.Services.ChatException)
            {
                return new RemovalOutcome(false, $"Could not change {name}: {ex.Message} The Pack and its row are still there.");
            }
        }

        return null;
    }

    /// <summary>The Skills the Agent's role carries.</summary>
    private IReadOnlyList<string> RoleSkills(string text)
    {
        if (!PersonaFrontmatter.TryReadIdentity(text, out PersonaIdentity? identity, out _) || identity.Role is not { } role)
        {
            return [];
        }

        RoleSpec? spec = types.Find(role.TypeId)?.Roles.FirstOrDefault(candidate => string.Equals(candidate.Id, role.RoleId, StringComparison.Ordinal));
        return [.. spec?.Skills.Select(static skill => skill.Name) ?? []];
    }

    /// <summary>The Agent's other Packs, as the registry has them.</summary>
    private IReadOnlyList<PackContents> OtherPacks(string text, string removing)
    {
        if (!PersonaFrontmatter.TryReadIdentity(text, out PersonaIdentity? identity, out _))
        {
            return [];
        }

        return [.. (identity.Packs ?? [])
            .Where(entry => !string.Equals(entry.Id, removing, StringComparison.OrdinalIgnoreCase))
            .Select(entry => registry.FindPack(entry.Id))
            .OfType<PackEntry>()
            .Select(PackContents.From)];
    }

    /// <summary>Removes a row; false when SKILLS.md kept changing or could not be written. A row already gone counts as removed.</summary>
    private bool TryRemoveRow(RowKind kind, string file)
    {
        try
        {
            return writer.RemoveRow(kind, file) != RemoveRowResult.KeptChanging;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Deletes a pack or catalog file when it is inside the folder.</summary>
    private (bool Deleted, RemovalOutcome? Failure) DeleteIfInside(string file)
    {
        if (!IsInside(file, settings.Dir) || !File.Exists(file))
        {
            return (false, null);
        }

        try
        {
            File.Delete(file);
            return (true, null);
        }
        catch (IOException)
        {
            return (false, new RemovalOutcome(false, $"Removed the row, but could not delete {file}. Delete it yourself."));
        }
        catch (UnauthorizedAccessException)
        {
            return (false, new RemovalOutcome(false, $"Removed the row, but could not delete {file}. Delete it yourself."));
        }
    }

    /// <summary>The file's path relative to the folder, with <c>/</c>, or the full path when it is outside.</summary>
    private string Relative(string file)
    {
        return IsInside(file, settings.Dir) ? PathContainment.RelativeSlashPath(settings.Dir, file) : file;
    }
}
