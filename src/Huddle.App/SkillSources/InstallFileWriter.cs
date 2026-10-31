using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Skills;
using Agency.Huddle.SkillPacks;
using Agency.Huddle.SkillPacks.Checking;
using Agency.Huddle.SkillPacks.Files;
using Agency.Huddle.SkillPacks.Reading;
using Agency.Huddle.SkillPacks.Resolving;

namespace Agency.Huddle.App.SkillSources;

/// <summary>
/// Keeps <c>{DataDir}/InstallSkillPack.md</c> and <c>{DataDir}/install-context.json</c> true to this Huddle (§6.7): written at
/// start-up, after the watcher's first read, and again when roles, installed Skills, the registry, the folder or the body
/// prompt change, at most once a second. It never reads or writes anything under the Skill Sources folder.
/// </summary>
internal sealed class InstallFileWriter : IHostedService, IInstallFileLocation, IDisposable
{
    /// <summary>The install file's name.</summary>
    public const string FileName = "InstallSkillPack.md";

    /// <summary>The cap on appendix A.</summary>
    public const int MaxRoles = 200;

    /// <summary>The cap on appendix B.</summary>
    public const int MaxInstalled = 300;

    /// <summary>The cap on each table of appendix C, and on appendix D.</summary>
    public const int MaxRows = 100;

    private const int MaxDescription = 160;
    private const int MaxRetries = 5;
    private static readonly TimeSpan MinInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    private readonly RoleLister roles;
    private readonly SkillStore skills;
    private readonly SkillRegistry registry;
    private readonly IPromptSource prompts;
    private readonly PromptStore? promptStore;
    private readonly SkillSourcesSettings settings;
    private readonly ILogger<InstallFileWriter> logger;
    private readonly TimeProvider clock;
    private readonly Lock gate = new();
    private ITimer? timer;
    private DateTimeOffset lastWriteStart = DateTimeOffset.MinValue;
    private string? lastHash;
    private int failures;
    private bool dirty;
    private bool started;
    private bool disposed;

    /// <summary>Wires the writer to the services it reads; it writes nothing until <see cref="StartAsync"/>.</summary>
    public InstallFileWriter(RoleLister roles, SkillStore skills, SkillRegistry registry, IPromptSource prompts, SkillSourcesSettings settings, ILogger<InstallFileWriter> logger, TimeProvider? timeProvider = null)
    {
        this.roles = roles;
        this.skills = skills;
        this.registry = registry;
        this.prompts = prompts;
        this.promptStore = prompts as PromptStore;
        this.settings = settings;
        this.logger = logger;
        this.clock = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Where <c>InstallSkillPack.md</c> is.</summary>
    public string InstallFilePath => Path.Combine(this.settings.DataDir, FileName);

    /// <summary>Where the context file is.</summary>
    public string ContextFilePath => Path.Combine(this.settings.DataDir, InstallContext.FileName);

    /// <summary>The command line <c>InstallSkillPack.md</c> gives for validating, without a path or options.</summary>
    public string ValidateCommand
    {
        get
        {
            string program = this.settings.HuddlePacksPath;
            string data = $"--data-dir \"{this.settings.DataDir}\"";
            return program.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                ? $"dotnet \"{program}\" validate {data}"
                : $"\"{program}\" validate {data}";
        }
    }

    /// <summary>Writes both files now, then starts listening for changes. Does not run the move.</summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        this.WriteNow();
        lock (this.gate)
        {
            this.started = true;
        }

        this.roles.Changed += this.RequestWrite;
        this.skills.SkillsChanged += this.RequestWrite;
        this.registry.Changed += this.OnRegistryChanged;
        this.settings.Changed += this.RequestWrite;
        if (this.promptStore is not null)
        {
            this.promptStore.PromptsChanged += this.RequestWrite;
        }

        return Task.CompletedTask;
    }

    /// <summary>Stops listening and drops a pending write.</summary>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        this.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>Asks for a write; coalesced to at most one a second, never blocks. Also called by Reload.</summary>
    public void RequestWrite()
    {
        lock (this.gate)
        {
            if (this.disposed || !this.started)
            {
                return;
            }

            this.dirty = true;
            if (this.timer is not null)
            {
                return;
            }

            TimeSpan due = this.lastWriteStart + MinInterval - this.clock.GetUtcNow();
            this.timer = this.clock.CreateTimer(_ => this.OnTimer(), null, due < TimeSpan.Zero ? TimeSpan.Zero : due, Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>Stops listening.</summary>
    public void Dispose()
    {
        lock (this.gate)
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
            this.timer?.Dispose();
            this.timer = null;
        }

        this.roles.Changed -= this.RequestWrite;
        this.skills.SkillsChanged -= this.RequestWrite;
        this.registry.Changed -= this.OnRegistryChanged;
        this.settings.Changed -= this.RequestWrite;
        if (this.promptStore is not null)
        {
            this.promptStore.PromptsChanged -= this.RequestWrite;
        }
    }

    /// <summary>Composes both files without writing them: the document and the context JSON.</summary>
    internal (string Document, string Context) Compose()
    {
        CheckContext context = this.registry.BuildCheckContext();
        RegistrySnapshot snapshot = this.registry.Current;
        string written = context.Written.ToString(InstallContext.TimeFormat, CultureInfo.InvariantCulture);
        InstallContext file = new(
            InstallContext.KnownSchemaVersion,
            context.HuddleVersion,
            context.ComputerName,
            context.DataDir,
            context.SkillSourcesDir,
            context.SkillsDir,
            context.Roles,
            [.. context.InstalledSkills.OrderBy(static skill => skill.Name, StringComparer.Ordinal)],
            context.AppToolNames,
            context.Written);

        string registryPath = Path.Combine(context.SkillSourcesDir, RegistryReader.FileName);
        Dictionary<string, string> values = new(StringComparer.Ordinal)
        {
            ["{{computer}}"] = context.ComputerName,
            ["{{skillSourcesDir}}"] = context.SkillSourcesDir,
            ["{{registryPath}}"] = registryPath,
            ["{{validateCommand}}"] = this.ValidateCommand,
            ["{{checkHints}}"] = CheckHintsTable(),
        };
        string body = this.prompts.Render("installSkillPack.body", values).ReplaceLineEndings("\n").Trim();

        StringBuilder document = new();
        document.Append("---\n");
        AppendYaml(document, "huddle", context.HuddleVersion);
        AppendYaml(document, "schemaVersion", PackFile.KnownSchemaVersion.ToString(CultureInfo.InvariantCulture));
        AppendYaml(document, "dataDir", context.DataDir);
        AppendYaml(document, "skillSourcesDir", context.SkillSourcesDir);
        AppendYaml(document, "registry", registryPath);
        AppendYaml(document, "context", this.ContextFilePath);
        AppendYaml(document, "validate", this.ValidateCommand);
        AppendYaml(document, "computer", context.ComputerName);
        AppendYaml(document, "written", written);
        document.Append("---\n\n# InstallSkillPack.md\n\n").Append(body).Append("\n\n");
        this.AppendRoles(document, context.Roles);
        this.AppendInstalled(document);
        this.AppendRegistry(document, snapshot);
        AppendCatalogs(document, snapshot.Resolved);
        return (document.ToString().TrimEnd() + "\n", file.ToJson());
    }

    /// <summary>The <c>{{checkHints}}</c> table, generated from <see cref="CheckCatalog.All"/>: the one source of fix hints.</summary>
    internal static string CheckHintsTable()
    {
        StringBuilder table = new("| Code | Severity | Check | How to fix |\n| --- | --- | --- | --- |\n");
        foreach (CheckInfo info in CheckCatalog.All)
        {
            string severity = info.Severity == Severity.Error ? "error" : "warning";
            table.Append("| `").Append(info.Code).Append("` | ").Append(severity).Append(" | ").Append(Cell(info.Title, int.MaxValue)).Append(" | ").Append(Cell(info.FixHint, int.MaxValue)).Append(" |\n");
        }

        return table.ToString().TrimEnd();
    }

    /// <summary>A table cell: <c>|</c> escaped, white space one space, trimmed, cut at <paramref name="max"/> with an ellipsis.</summary>
    internal static string Cell(string? text, int max = MaxDescription)
    {
        string flat = string.Join(' ', (text ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).Replace("|", "\\|", StringComparison.Ordinal);
        return flat.Length <= max ? flat : flat[..(max - 1)].TrimEnd() + "…";
    }

    /// <summary>An id, name or file name in backticks, with any backtick inside dropped.</summary>
    internal static string Code(string text)
    {
        return $"`{Cell(text, int.MaxValue).Replace("`", string.Empty, StringComparison.Ordinal)}`";
    }

    /// <summary>A YAML scalar: bare, or single-quoted with <c>'</c> doubled when it needs quotes (§6.7, "The front matter").</summary>
    internal static string YamlScalar(string value)
    {
        const string indicators = "'\"&*!|>%@`[]{}?-#";
        bool quote = value.Length == 0
            || value.Contains(": ", StringComparison.Ordinal)
            || value.Contains(" #", StringComparison.Ordinal)
            || indicators.Contains(value[0], StringComparison.Ordinal)
            || char.IsWhiteSpace(value[0])
            || char.IsWhiteSpace(value[^1])
            || value.Contains('"', StringComparison.Ordinal);
        return quote ? $"'{value.Replace("'", "''", StringComparison.Ordinal)}'" : value;
    }

    /// <summary>One front matter line.</summary>
    private static void AppendYaml(StringBuilder text, string key, string value)
    {
        text.Append(key).Append(": ").Append(YamlScalar(value)).Append('\n');
    }

    /// <summary>A capped table's "N more" line, in italics.</summary>
    private static void AppendMore(StringBuilder text, int dropped, string hint)
    {
        if (dropped > 0)
        {
            text.Append('\n').Append(string.Create(CultureInfo.InvariantCulture, $"*{dropped} more; {hint}.*")).Append('\n');
        }
    }

    /// <summary>Appendix A, Roles.</summary>
    private void AppendRoles(StringBuilder text, IReadOnlyList<RoleInfo> list)
    {
        _ = this;
        text.Append("## Appendix A. Roles\n\n| Id | Title | Description |\n| --- | --- | --- |\n");
        foreach (RoleInfo role in list.Take(MaxRoles))
        {
            text.Append("| ").Append(Code(role.Id)).Append(" | ").Append(Cell(role.Title)).Append(" | ").Append(Cell(role.Description)).Append(" |\n");
        }

        AppendMore(text, list.Count - MaxRoles, "run huddle-packs roles");
        text.Append('\n');
    }

    /// <summary>Appendix B, Installed Skills.</summary>
    private void AppendInstalled(StringBuilder text)
    {
        List<Skill> all = [.. this.skills.All.OrderBy(static skill => skill.Name, StringComparer.Ordinal)];
        text.Append("## Appendix B. Installed Skills\n\n| Name | Origin | Built in |\n| --- | --- | --- |\n");
        foreach (Skill skill in all.Take(MaxInstalled))
        {
            bool builtIn = skill.Source is SkillSource.Default or SkillSource.Overridden;
            string origin = builtIn ? "built in" : skill.Provenance?.EffectiveOrigin ?? "yours";
            text.Append("| ").Append(Code(skill.Name)).Append(" | ").Append(Cell(origin, int.MaxValue)).Append(" | ").Append(builtIn ? "yes" : "no").Append(" |\n");
        }

        AppendMore(text, all.Count - MaxInstalled, "run huddle-packs installed");
        text.Append('\n');
    }

    /// <summary>Appendix C, SKILLS.md today: one row per SKILLS.md row, then one per Source.</summary>
    private void AppendRegistry(StringBuilder text, RegistrySnapshot snapshot)
    {
        ResolvedRegistry resolved = snapshot.Resolved;
        List<RegistryRow> rows = [.. resolved.Registry.AllRows];
        text.Append("## Appendix C. SKILLS.md today\n\n| Table | Local path | State | Id | Source |\n| --- | --- | --- | --- | --- |\n");
        foreach (RegistryRow row in rows.Take(MaxRows))
        {
            (string? path, string id, string source, string state) = row.Kind == RowKind.Pack
                ? this.PackCells(resolved.Packs[row.Index], snapshot)
                : this.CatalogCells(resolved.Catalogs[row.Index], snapshot);
            string shown = path ?? $"(no entry for {this.settings.ComputerName})";
            text.Append("| ").Append(row.TableName).Append(" | ").Append(Cell(shown, int.MaxValue)).Append(" | ").Append(state).Append(" | ").Append(id).Append(" | ").Append(Cell(source)).Append(" |\n");
        }

        AppendMore(text, rows.Count - MaxRows, "open SKILLS.md");
        text.Append("\n| Source | Kind | Status | Commit | Clone |\n| --- | --- | --- | --- | --- |\n");
        foreach (ResolvedSource source in resolved.Sources.Take(MaxRows))
        {
            SourceFile? file = source.File;
            string status = StatusWords(file);
            string commit = file?.Commit is { } full ? full[..Math.Min(7, full.Length)] : string.Empty;
            string clone = file?.LocalPath?.Resolve(this.settings.ComputerName) ?? string.Empty;
            text.Append("| ").Append(Code(source.Id)).Append(" | ").Append(file is null ? string.Empty : SourceFile.Lower(file.Kind)).Append(" | ").Append(Cell(status)).Append(" | ").Append(commit).Append(" | ").Append(Cell(clone, int.MaxValue)).Append(" |\n");
        }

        AppendMore(text, resolved.Sources.Count - MaxRows, "open SKILLS.md");
        text.Append('\n');
    }

    /// <summary>A Source's status, with its reason in brackets when it has one; empty with no valid <c>source.json</c>.</summary>
    private static string StatusWords(SourceFile? file)
    {
        if (file is null)
        {
            return string.Empty;
        }

        string status = SourceFile.Lower(file.Status);
        return file.StatusReason is { Length: > 0 } reason ? $"{status} ({reason})" : status;
    }

    /// <summary>The appendix C cells of a Pack row.</summary>
    private (string? Path, string Id, string Source, string State) PackCells(PackEntry pack, RegistrySnapshot snapshot)
    {
        string state = this.StateWords(this.registry.DisplayState(pack), pack.StateKey, snapshot, this.registry.HolderNames(pack).Count);
        return (pack.FilePath, pack.File is null ? "-" : Code(pack.Id), pack.Source, state);
    }

    /// <summary>The appendix C cells of a Catalogs row.</summary>
    private (string? Path, string Id, string Source, string State) CatalogCells(ResolvedCatalog catalog, RegistrySnapshot snapshot)
    {
        ItemCheckState state = snapshot.Checked.StateOf(catalog.StateKey) ?? ItemCheckState.Broken;
        ItemState shown = state switch
        {
            ItemCheckState.Broken => ItemState.Broken,
            ItemCheckState.NotDownloaded => ItemState.NotDownloaded,
            _ => ItemState.Available,
        };
        return (catalog.FilePath, catalog.File is null ? "-" : Code(catalog.Id), catalog.Source, this.StateWords(shown, catalog.StateKey, snapshot, 0));
    }

    /// <summary>A state in the words of appendix C; Broken carries its first error code.</summary>
    private string StateWords(ItemState state, string key, RegistrySnapshot snapshot, int holders)
    {
        _ = this;
        return state switch
        {
            ItemState.Broken => $"Broken ({FirstErrorCode(key, snapshot) ?? "?"})",
            ItemState.InUse => string.Create(CultureInfo.InvariantCulture, $"In use by {holders}"),
            ItemState.UpdateAvailable => "Update available",
            ItemState.NotDownloaded => "Not downloaded",
            ItemState.New or ItemState.Available => "Available",
#pragma warning disable HAA0102 // Enum.ToString returns the runtime's cached name, so a lookup table would save nothing.
            _ => state.ToString(),
#pragma warning restore HAA0102
        };
    }

    /// <summary>The first error code filed under a row's file.</summary>
    private static string? FirstErrorCode(string key, RegistrySnapshot snapshot)
    {
        string? file = snapshot.Resolved.Packs.FirstOrDefault(pack => pack.StateKey == key)?.FilePath
            ?? snapshot.Resolved.Catalogs.FirstOrDefault(catalog => catalog.StateKey == key)?.FilePath;
        return snapshot.Checked.Findings.FirstOrDefault(finding => finding.Severity == Severity.Error && (file is null || finding.File.StartsWith(file, StringComparison.OrdinalIgnoreCase) || finding.File.EndsWith(RegistryReader.FileName, StringComparison.Ordinal)))?.Code;
    }

    /// <summary>Appendix D, Where to look: one row per catalog.</summary>
    private static void AppendCatalogs(StringBuilder text, ResolvedRegistry resolved)
    {
        text.Append("## Appendix D. Where to look\n\n| Catalog | Source | Skills | Downloaded | File |\n| --- | --- | --- | --- | --- |\n");
        foreach (ResolvedCatalog catalog in resolved.Catalogs.Take(MaxRows))
        {
            int count = catalog.File?.Skills.Count ?? 0;
            text.Append("| ").Append(catalog.File is null ? "-" : Code(catalog.Id)).Append(" | ").Append(Cell(catalog.Source)).Append(" | ")
                .Append(count.ToString(CultureInfo.InvariantCulture)).Append(" | ").Append(catalog.DownloadedCount.ToString(CultureInfo.InvariantCulture)).Append(" | ")
                .Append(Cell(catalog.FilePath ?? catalog.Row.LocalPath.FirstText, int.MaxValue)).Append(" |\n");
        }

        AppendMore(text, resolved.Catalogs.Count - MaxRows, "run huddle-packs skills");
    }

    /// <summary>A registry change: write again.</summary>
    private void OnRegistryChanged(RegistryChange change)
    {
        this.RequestWrite();
    }

    /// <summary>The coalescing timer fired.</summary>
    private void OnTimer()
    {
        lock (this.gate)
        {
            this.timer?.Dispose();
            this.timer = null;
            if (this.disposed || !this.dirty)
            {
                return;
            }
        }

        this.WriteNow();
    }

    /// <summary>The write: clear dirty first, compose, compare, write the context file then the document. An I/O failure is
    /// logged and retried after 5 seconds, up to 5 times in a row.</summary>
    private void WriteNow()
    {
        lock (this.gate)
        {
            this.dirty = false;
            this.lastWriteStart = this.clock.GetUtcNow();
        }

        try
        {
            (string document, string context) = this.Compose();
            string hash = Hash(document, context);
            if (string.Equals(hash, this.lastHash, StringComparison.Ordinal) && File.Exists(this.InstallFilePath) && File.Exists(this.ContextFilePath))
            {
                return;
            }

            Directory.CreateDirectory(this.settings.DataDir);
            AtomicFile.WriteText(this.ContextFilePath, context);
            AtomicFile.WriteText(this.InstallFilePath, document);
            this.lastHash = hash;
            this.failures = 0;
        }
        catch (IOException exception)
        {
            this.Retry(exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            this.Retry(exception);
        }
    }

    /// <summary>Logs a failed write and schedules a retry.</summary>
    private void Retry(Exception exception)
    {
        this.logger.LogWarning("InstallSkillPack.md could not be written: {Message}. Retrying in 5 seconds.", exception.Message);
        lock (this.gate)
        {
            this.failures++;
            if (this.disposed || this.failures > MaxRetries)
            {
                return;
            }

            this.dirty = true;
            this.timer?.Dispose();
            this.timer = this.clock.CreateTimer(_ => this.OnTimer(), null, RetryDelay, Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>The hash of both files with their <c>written</c> value removed.</summary>
    private static string Hash(string document, string context)
    {
        static string WithoutWritten(string text)
        {
            return string.Join('\n', text.Split('\n').Where(static line => !line.TrimStart().StartsWith("written", StringComparison.Ordinal) && !line.TrimStart().StartsWith("\"written\"", StringComparison.Ordinal)));
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(WithoutWritten(document) + "\0" + WithoutWritten(context))));
    }
}
