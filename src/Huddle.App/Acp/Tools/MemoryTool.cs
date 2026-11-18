using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.FileChanges;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Memory;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.App.Acp.Tools;

/// <summary>
/// The <c>memory</c> App Tool (Instructions spec §5): lists, reads, writes, appends to, replaces in and deletes the Markdown
/// topic files of the caller's own Memory, or of a Team's or Project's shared Memory. It enforces in code what the Memory
/// Prompt can only ask: confinement to <c>memory/</c> folders, read before write, a description on every file, a size
/// cap, and that only a Team's manager writes the Team's Memory.
/// </summary>
/// <remarks>
/// Refusals are plain text, never exceptions, as in every App Tool. A mutating command does its read, version check and
/// write inside <see cref="MemoryFolderGate"/>, so two writers of one folder never interleave. The Agent's own <c>Write</c>
/// and the Human's Library edits do not take the gate, but the version token is a hash of what is on disk, so they are caught.
/// </remarks>
/// <param name="personaName">The calling Persona's Name.</param>
/// <param name="agentPaths">Locates the caller's Work Dir, and so its own memory folder.</param>
/// <param name="access">Decides which Team folders the caller reads and writes.</param>
/// <param name="gate">Serialises the writers of one folder.</param>
/// <param name="recycleBin">Receives a deleted file, as a Library delete does.</param>
/// <param name="prompts">Renders the tool's description and every refusal.</param>
/// <param name="options">Supplies the Teams root and the size cap.</param>
/// <param name="warnings">Warns once about a memory file with malformed frontmatter when the tool lists or describes it.</param>
/// <param name="logger">Records each mutation, and each refusal that points at a boundary.</param>
/// <param name="renames">Resolves a Team's old name in a <c>folder</c> argument to today's; optional.</param>
/// <param name="timeProvider">Gives the Human's local date that <c>reported.md</c> lines are stamped with; optional, the system clock when absent.</param>
/// <param name="callerAgentId">The Agent this tool instance is bound to, for the goldfish refusal; optional.</param>
/// <param name="goldfish">Tells whether the caller is a goldfish, which the tool refuses; optional.</param>
internal sealed class MemoryTool(
    string personaName,
    AgentPaths agentPaths,
    TeamMemoryAccess access,
    MemoryFolderGate gate,
    IRecycleBin recycleBin,
    IPromptSource prompts,
    IOptions<TeamOptions> options,
    MemoryWarnings warnings,
    ILogger<MemoryTool> logger,
    ITeamRenames? renames = null,
    TimeProvider? timeProvider = null,
    string? callerAgentId = null,
    Agency.Huddle.App.Goldfish.GoldfishGuard? goldfish = null) : IAppTool
{
    /// <summary>The tool's name, without any Adapter prefix.</summary>
    public const string ToolName = "memory";

    private const string AgentFolderPrefix = "agent:";

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;

    private int descriptionWarned;

    private readonly MemoryPaths paths = new(agentPaths, access, options);

    private readonly MemoryWriter writer = new(gate, options);

    /// <inheritdoc />
    public string Name => ToolName;

    /// <summary>Whether <paramref name="name"/> is this tool as offered: the bare name, or the name behind an Adapter's <c>mcp__{server}__</c> prefix.</summary>
    /// <param name="name">A tool name as offered or as an Adapter reported it.</param>
    /// <returns><see langword="true"/> for <c>memory</c> and for any name ending <c>__memory</c>.</returns>
    public static bool IsToolName(string name) => AppToolNames.Matches(name, ToolName);

    /// <inheritdoc />
    public string Description => prompts.Render("tool.memory.description");

    /// <inheritdoc />
    public JsonObject InputSchema => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["command"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray([.. MemoryRequest.Commands.Select(static command => (JsonNode?)JsonValue.Create(command))]) },
            ["folder"] = new JsonObject { ["type"] = "string" },
            ["file"] = new JsonObject { ["type"] = "string" },
            ["description"] = new JsonObject { ["type"] = "string" },
            ["text"] = new JsonObject { ["type"] = "string" },
            ["old"] = new JsonObject { ["type"] = "string" },
            ["new"] = new JsonObject { ["type"] = "string" },
            ["version"] = new JsonObject { ["type"] = "string" },
        },
        ["required"] = new JsonArray("command"),
    };

    /// <inheritdoc />
    public async Task<string> InvokeAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (callerAgentId is not null && goldfish?.Refusal(callerAgentId) is { } refusal)
        {
            return refusal;
        }

        MemoryRequest request = MemoryRequest.From(arguments);
        if (request.Problem() is { } problem)
        {
            return this.Say("systemPrompt.memoryBadCommand", ("problem", problem));
        }

        if (AgentFolderName(request.Folder) is { } agent)
        {
            return await this.RunAgentFolderAsync(request, agent, cancellationToken).ConfigureAwait(false);
        }

        MemoryFolder? folder = this.paths.ResolveFolder(personaName, request.Folder);
        string? note = null;
        if (folder is null && this.RenamedFolder(request.Folder) is { } renamed)
        {
            string sentence = string.Create(CultureInfo.InvariantCulture, $"{renamed.Kind} '{renamed.Old}' was renamed to '{renamed.New}'");
            if (request.Mutates)
            {
                return string.Create(CultureInfo.InvariantCulture, $"{sentence}. Use '{renamed.New}'.");
            }

            folder = this.paths.ResolveFolder(personaName, renamed.Folder);
            note = folder is null ? null : sentence + ".\n";
        }

        if (folder is null)
        {
            logger.LogWarning("Refused a memory call from '{Caller}': '{Folder}' is not a folder it may use.", personaName, request.Folder);
            return this.Say("systemPrompt.memoryBadFolder", ("folder", request.Folder ?? string.Empty), ("folders", string.Join(", ", this.UsableFolders())));
        }

        string result = request.Command == "list"
            ? this.List(folder)
            : await this.RunFileCommandAsync(request, folder, cancellationToken).ConfigureAwait(false);
        return note is null ? result : note + result;
    }

    /// <summary>Maps a <c>folder</c> argument that names a Team, and a Project, by an old name onto today's names.</summary>
    /// <param name="folder">The <c>folder</c> argument.</param>
    /// <returns>The old and new Team name and the folder to retry, or <see langword="null"/> when the first segment was never renamed.</returns>
    private (string Kind, string Old, string New, string Folder)? RenamedFolder(string? folder)
    {
        if (renames is null || string.IsNullOrWhiteSpace(folder))
        {
            return null;
        }

        string[] segments = folder.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return null;
        }

        if (segments.Take(2).Any(string.IsNullOrWhiteSpace))
        {
            return null;
        }

        string old = segments[0];
        string today = renames.Resolve(old);
        bool teamRenamed = !string.Equals(today, old, StringComparison.OrdinalIgnoreCase);
        string? oldProject = segments.Length > 1 ? segments[1] : null;
        string? newProject = oldProject is null ? null : renames.ResolveProject(old, oldProject);
        bool projectRenamed = oldProject is not null && !string.Equals(newProject, oldProject, StringComparison.OrdinalIgnoreCase);
        if (!teamRenamed && !projectRenamed)
        {
            return null;
        }

        segments[0] = today;
        if (newProject is not null)
        {
            segments[1] = newProject;
        }

        string retry = string.Join('/', segments);
        return teamRenamed
            ? ("Team", old, today, retry)
            : ("Project", $"{old}/{oldProject}", $"{today}/{newProject}", retry);
    }

    /// <summary>Runs a command that names a file, after the file name and the caller's right to change the folder are checked.</summary>
    /// <param name="request">The call.</param>
    /// <param name="folder">The folder it works in.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    private async Task<string> RunFileCommandAsync(MemoryRequest request, MemoryFolder folder, CancellationToken cancellationToken)
    {
        string file = request.File ?? string.Empty;
        string? path = MemoryPaths.FilePath(folder, file);
        if (path is null)
        {
            logger.LogWarning("Refused a memory call from '{Caller}': '{File}' is not a memory file name.", personaName, file);
            return this.Say("systemPrompt.memoryBadFile");
        }

        if (request.Mutates && this.WriteRefusal(folder) is { } refusal)
        {
            return refusal;
        }

        return request.Command switch
        {
            "read" => await this.ReadAsync(file, path, cancellationToken).ConfigureAwait(false),
            "write" => await this.WriteAsync(request, folder, cancellationToken).ConfigureAwait(false),
            "append" => await this.MutateAsync(folder, file, () => this.AppendAsync(request, folder, path, cancellationToken), cancellationToken).ConfigureAwait(false),
            "replace" => await this.MutateAsync(folder, file, () => this.ReplaceAsync(request, folder, path, cancellationToken), cancellationToken).ConfigureAwait(false),
            _ => await this.MutateAsync(folder, file, () => this.DeleteAsync(request, folder, path, cancellationToken), cancellationToken).ConfigureAwait(false),
        };
    }

    /// <summary>The Name after <c>agent:</c> in a <c>folder</c> argument, or <see langword="null"/> when the argument is not that form.</summary>
    /// <param name="folder">The <c>folder</c> argument.</param>
    private static string? AgentFolderName(string? folder)
    {
        string? trimmed = folder?.Trim();
        return trimmed is not null && trimmed.StartsWith(AgentFolderPrefix, StringComparison.OrdinalIgnoreCase)
            ? trimmed[AgentFolderPrefix.Length..].Trim()
            : null;
    }

    /// <summary>
    /// Answers a call whose <c>folder</c> is <c>agent:&lt;Name&gt;</c>: only the Chief of Staff, only <c>read</c> or <c>list</c>, only
    /// <c>reported.md</c> of a Team's manager. The value never becomes a <see cref="MemoryFolder"/>, so no write can be built on it.
    /// </summary>
    /// <param name="request">The call.</param>
    /// <param name="agent">The Name after <c>agent:</c>, as passed.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    private async Task<string> RunAgentFolderAsync(MemoryRequest request, string agent, CancellationToken cancellationToken)
    {
        if (!access.IsChiefOfStaff(personaName))
        {
            logger.LogWarning("Refused a memory call from '{Caller}': '{Folder}' is not a folder it may use.", personaName, request.Folder);
            return this.Say("systemPrompt.memoryBadFolder", ("folder", request.Folder ?? string.Empty), ("folders", string.Join(", ", this.UsableFolders())));
        }

        bool listing = request.Command == "list";
        if (!listing && !(request.Command == "read" && ReportedFile.IsName((request.File ?? string.Empty).Trim())))
        {
            return this.Say("systemPrompt.memoryAgentFolderRefusal");
        }

        ReportedLookup lookup = this.paths.ResolveReportedOf(personaName, agent, out string? manager, out string? path);
        if (lookup == ReportedLookup.NotManager)
        {
            string managers = access.Managers() is { Count: > 0 } today ? string.Join(", ", today) : "none";
            return this.Say("systemPrompt.memoryReportedNoManager", ("name", agent), ("managers", managers));
        }

        if (lookup != ReportedLookup.Found || path is null || !File.Exists(path))
        {
            return this.Say("systemPrompt.memoryReportedNone", ("name", manager ?? agent));
        }

        return listing
            ? $"{ReportedFile.FileName} - {MemoryIndex.SummaryFor(path, warnings)}"
            : await this.ReadAsync(ReportedFile.FileName, path, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The <c>folder</c> values the caller may use: its own, then each readable Team and each of that Team's Projects; for the Chief of Staff, each manager's reported file too.</summary>
    private List<string> UsableFolders()
    {
        List<string> folders = [MemoryPaths.OwnFolder, TeamNames.MemoryFolder];
        foreach (TeamSummary team in access.Readable(personaName))
        {
            folders.Add(team.Name);
            folders.AddRange(team.Projects.Select(project => $"{team.Name}/{project}"));
        }

        if (access.IsChiefOfStaff(personaName))
        {
            folders.AddRange(access.Managers().Select(static manager => $"{AgentFolderPrefix}{manager}"));
        }

        return folders;
    }

    /// <summary>The refusal for a change to a Team's Memory by anyone but its manager or the Chief of Staff, else <see langword="null"/>.</summary>
    /// <param name="folder">The resolved folder.</param>
    private string? WriteRefusal(MemoryFolder folder)
    {
        if (folder.HuddleWide)
        {
            if (access.IsChiefOfStaff(personaName))
            {
                return null;
            }

            logger.LogWarning("Refused a change to Huddle-wide Memory by '{Caller}': only the Chief of Staff writes it.", personaName);
            return this.Say("systemPrompt.huddleMemoryRefusal");
        }

        if (folder.Team is not { } team || access.CanWrite(personaName, team))
        {
            return null;
        }

        logger.LogWarning("Refused a change to the Team Memory of '{Team}' by '{Caller}': only its manager writes it.", team.Name, personaName);
        return TeamMemoryRefusals.ManagerOnly(prompts, team);
    }

    /// <summary>Lists every top-level <c>*.md</c> file with its index line, uncapped, then the folder path.</summary>
    /// <param name="folder">The resolved folder.</param>
    private string List(MemoryFolder folder)
    {
        List<string> files = Directory.Exists(folder.Path)
            ? [.. Directory.EnumerateFiles(folder.Path, "*.md", SearchOption.TopDirectoryOnly)
                .Where(static file => !MemoryFileRules.IsIndexFileName(Path.GetFileName(file)))]
            : [];
        files.Sort(StringComparer.Ordinal);

        string body = files.Count == 0
            ? prompts.Render("systemPrompt.memoryEmpty")
            : string.Join('\n', files.Select(file => $"{Path.GetFileName(file)} - {MemoryIndex.SummaryFor(file, warnings)}"));

        string result = $"{body}\nfolder: {folder.Path}";
        if (folder.Team is not null || folder.HuddleWide)
        {
            return result;
        }

        string folders = string.Join(", ", this.UsableFolders());
        result += $"\nfolders you may use: {folders}";
        return result;
    }

    /// <summary>Returns the whole file, then its version.</summary>
    /// <param name="file">The file name.</param>
    /// <param name="path">The file's full path.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    private async Task<string> ReadAsync(string file, string path, CancellationToken cancellationToken)
    {
        try
        {
            byte[]? bytes = await ReadIfExistsAsync(path, cancellationToken).ConfigureAwait(false);
            return bytes is null
                ? this.Say("systemPrompt.memoryNotFound", ("file", file))
                : WithVersion(Utf8.GetString(bytes), MemoryVersion.Of(bytes));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not read the memory file '{Path}'.", path);
            return this.Say("systemPrompt.memoryReadFailed", ("file", file));
        }
    }

    /// <summary>
    /// Creates a file, or replaces one given the version the Agent read, through <see cref="MemoryWriter"/>, which composes the
    /// frontmatter; then says what happened in the words this tool has always used.
    /// </summary>
    /// <param name="request">The call.</param>
    /// <param name="folder">The resolved folder.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    private async Task<string> WriteAsync(MemoryRequest request, MemoryFolder folder, CancellationToken cancellationToken)
    {
        string file = request.File ?? string.Empty;
        MemoryWriteResult result = await this.writer.WriteAsync(folder, file, request.Description, request.Text, request.Version, cancellationToken).ConfigureAwait(false);
        switch (result)
        {
            case MemoryWriteResult.Written written:
                this.LogCommitted(request, folder, written.Version);
                return $"written {file} (version {written.Version})";
            case MemoryWriteResult.OnlyFrontmatter:
                return this.Say("systemPrompt.memoryOnlyFrontmatter", ("file", file));
            case MemoryWriteResult.BadDescription bad:
                return this.Say("systemPrompt.memoryBadDescription", ("problem", bad.Problem));
            case MemoryWriteResult.TooLarge tooLarge:
                return this.SayTooLarge(file, tooLarge.Size, tooLarge.Max);
            case MemoryWriteResult.Missing:
                return this.Say("systemPrompt.memoryDeleted", ("file", file));
            case MemoryWriteResult.Exists:
                return this.Say("systemPrompt.memoryReadFirst", ("file", file));
            case MemoryWriteResult.Stale stale:
                return $"{this.Say("systemPrompt.memoryConflict", ("file", file))}\n\n{WithVersion(stale.CurrentText, stale.CurrentVersion)}";
            case MemoryWriteResult.Failed { Error: { } error }:
                logger.LogWarning(error, "A memory change to '{File}' in '{Folder}' failed.", file, folder.Label);
                return this.Say("systemPrompt.memoryWriteFailed", ("file", file));
            case MemoryWriteResult.Failed failed:
                logger.LogWarning("The memory file '{Path}' could not be replaced; it is held by another process.", failed.Path);
                return this.Say("systemPrompt.memoryWriteFailed", ("file", file));
            default:
                // BadName: the file name was checked before the call, so this is only reachable if the two checks ever disagree.
                return this.Say("systemPrompt.memoryBadFile");
        }
    }

    /// <summary>Adds <c>text</c> as a new line at the end of an existing file. No version: appending cannot lose an update.</summary>
    /// <param name="request">The call.</param>
    /// <param name="folder">The resolved folder.</param>
    /// <param name="path">The file's full path.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    private async Task<string> AppendAsync(MemoryRequest request, MemoryFolder folder, string path, CancellationToken cancellationToken)
    {
        string file = request.File ?? string.Empty;
        if (folder.Label == MemoryPaths.OwnFolder && folder.Team is null && !folder.HuddleWide && ReportedFile.IsName(file))
        {
            // The file's own spelling, so REPORTED.md on a case-sensitive file system appends to reported.md rather than starting a twin.
            string reported = MemoryPaths.FilePath(folder, ReportedFile.FileName) ?? path;
            return await this.AppendReportedAsync(request, folder, reported, cancellationToken).ConfigureAwait(false);
        }

        byte[]? current = await ReadIfExistsAsync(path, cancellationToken).ConfigureAwait(false);
        if (current is null)
        {
            return this.Say("systemPrompt.memoryNotFound", ("file", file));
        }

        string existing = Utf8.GetString(current);
        string separator = existing.Length == 0 || existing.EndsWith('\n') ? string.Empty : "\n";
        string text = request.Text ?? string.Empty;
        string result = existing + separator + text + (text.EndsWith('\n') ? string.Empty : "\n");
        byte[] bytes = Utf8.GetBytes(result);

        return (this.TooLarge(file, bytes.Length) ?? this.DescriptionLost(file, existing, result)) is { } refusal
            ? refusal
            : await this.CommitAsync(request, folder, path, bytes, "appended to", cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// <c>append</c> to <c>reported.md</c> in the caller's own folder (Memory Tidy spec §6.3): the tool sets the date and refuses several
    /// lines, creates the file when it is missing, drops lines older than <c>Team:Memory:Reported:KeepDays</c> and, at the size cap,
    /// the oldest dated lines, all in the one write.
    /// </summary>
    /// <param name="request">The call.</param>
    /// <param name="folder">The caller's own folder.</param>
    /// <param name="path">The file's full path.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    private async Task<string> AppendReportedAsync(MemoryRequest request, MemoryFolder folder, string path, CancellationToken cancellationToken)
    {
        string file = request.File ?? string.Empty;
        DateOnly today = DateOnly.FromDateTime(this.clock.GetLocalNow().DateTime);
        if (ReportedFile.BuildLine(request.Text, today, out string line) != ReportedFile.LineProblem.None)
        {
            return this.Say("systemPrompt.memoryReportedOneLine");
        }

        MemoryOptions memory = options.Value.Memory;
        int keepDays = memory.Reported.KeepDays;
        byte[]? current = await ReadIfExistsAsync(path, cancellationToken).ConfigureAwait(false);
        string suffix = string.Empty;
        string result;
        if (current is null)
        {
            result = MemoryFileRules.Compose(this.ReportedDescription(file), line + "\n");
            if (this.TooLarge(file, Utf8.GetByteCount(result)) is { } tooLargeAlone)
            {
                return tooLargeAlone;
            }
        }
        else
        {
            ReportedAppend appended = ReportedFile.Append(Utf8.GetString(current), line, today, keepDays, memory.MaxFileBytes);
            if (appended.Text is null)
            {
                return this.SayTooLarge(file, appended.Size, memory.MaxFileBytes);
            }

            result = appended.Text;
            suffix = DroppedSuffix(appended, keepDays);
        }

        return await this.CommitAsync(request, folder, path, Utf8.GetBytes(result), "appended to", cancellationToken).ConfigureAwait(false) + suffix;
    }

    /// <summary>The reply words for the lines an <c>append</c> to <c>reported.md</c> dropped: by age first, then to make room.</summary>
    /// <param name="appended">What the append did.</param>
    /// <param name="keepDays">The age limit in days.</param>
    private static string DroppedSuffix(ReportedAppend appended, int keepDays)
    {
        string suffix = string.Empty;
        if (appended.DroppedByAge > 0)
        {
            string noun = appended.DroppedByAge == 1 ? "line" : "lines";
            suffix += string.Create(CultureInfo.InvariantCulture, $"; dropped {appended.DroppedByAge} {noun} older than {keepDays} days");
        }

        if (appended.DroppedForRoom > 0)
        {
            suffix += string.Create(CultureInfo.InvariantCulture, $"; dropped {appended.DroppedForRoom} of the oldest lines to make room");
        }

        return suffix;
    }

    /// <summary>
    /// The description a new <c>reported.md</c> gets: the Prompt's text, or the catalog default when a Human's override breaks the
    /// description rule (a warning is logged once per tool instance), so the file can always be created.
    /// </summary>
    /// <param name="file">The file name.</param>
    private string ReportedDescription(string file)
    {
        const string Key = "systemPrompt.memoryReportedDescription";
        string rendered = prompts.Render(Key).Trim();
        if (MemoryFileRules.DescriptionProblem(rendered, file) is not { } problem)
        {
            return rendered;
        }

        if (Interlocked.Exchange(ref this.descriptionWarned, 1) == 0)
        {
            logger.LogWarning("The Prompt '{Key}' is not an acceptable description ({Problem}); using the default.", Key, problem);
        }

        return PromptCatalog.Get(Key).Default;
    }

    /// <summary>Swaps <c>old</c> for <c>new</c>, refused unless <c>old</c> occurs exactly once. No version: the exact match is the check.</summary>
    /// <param name="request">The call.</param>
    /// <param name="folder">The resolved folder.</param>
    /// <param name="path">The file's full path.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    private async Task<string> ReplaceAsync(MemoryRequest request, MemoryFolder folder, string path, CancellationToken cancellationToken)
    {
        string file = request.File ?? string.Empty;
        byte[]? current = await ReadIfExistsAsync(path, cancellationToken).ConfigureAwait(false);
        if (current is null)
        {
            return this.Say("systemPrompt.memoryNotFound", ("file", file));
        }

        string text = Utf8.GetString(current);
        (string Old, string New) match = ChooseSpelling(text, request.Old ?? string.Empty, request.New ?? string.Empty);
        int count = CountOccurrences(text, match.Old);
        if (count != 1)
        {
            return this.Say("systemPrompt.memoryOldNotUnique", ("file", file), ("count", count.ToString(CultureInfo.InvariantCulture)));
        }

        int at = text.IndexOf(match.Old, StringComparison.Ordinal);
        string result = string.Concat(text.AsSpan(0, at), match.New, text.AsSpan(at + match.Old.Length));
        byte[] bytes = Utf8.GetBytes(result);

        return (this.TooLarge(file, bytes.Length) ?? this.DescriptionLost(file, text, result)) is { } refusal
            ? refusal
            : await this.CommitAsync(request, folder, path, bytes, "replaced in", cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Sends an existing file to the Recycle Bin, given the version the Agent read.</summary>
    /// <param name="request">The call.</param>
    /// <param name="folder">The resolved folder.</param>
    /// <param name="path">The file's full path.</param>
    /// <param name="cancellationToken">Cancels the delete.</param>
    private async Task<string> DeleteAsync(MemoryRequest request, MemoryFolder folder, string path, CancellationToken cancellationToken)
    {
        string file = request.File ?? string.Empty;
        byte[]? current = await ReadIfExistsAsync(path, cancellationToken).ConfigureAwait(false);
        if (current is null)
        {
            return this.Say("systemPrompt.memoryNotFound", ("file", file));
        }

        if (this.VersionRefusal(file, request.Version, current, "systemPrompt.memoryDeleteNeedsVersion") is { } refusal)
        {
            return refusal;
        }

        if (!recycleBin.TrySend(path, out string? error))
        {
            logger.LogWarning("Could not send the memory file '{Path}' to the Recycle Bin: {Error}", path, error);
            return this.Say("systemPrompt.memoryWriteFailed", ("file", file));
        }

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Memory delete by '{Caller}' in '{Folder}' of '{File}', was version {Version}.", personaName, folder.Label, file, MemoryVersion.Of(current));
        }

        return $"deleted {file}";
    }

    /// <summary>Writes the bytes atomically and reports the new version, and for an append or replace the description that now stands.</summary>
    /// <param name="request">The call.</param>
    /// <param name="folder">The resolved folder.</param>
    /// <param name="path">The file's full path.</param>
    /// <param name="bytes">The file's new content.</param>
    /// <param name="verb">How the result begins: <c>written</c>, <c>appended to</c> or <c>replaced in</c>.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    private async Task<string> CommitAsync(MemoryRequest request, MemoryFolder folder, string path, byte[] bytes, string verb, CancellationToken cancellationToken)
    {
        string file = request.File ?? string.Empty;
        _ = Directory.CreateDirectory(folder.Path);
        if (!await AtomicFile.TryWriteAsync(path, bytes, cancellationToken).ConfigureAwait(false))
        {
            logger.LogWarning("The memory file '{Path}' could not be replaced; it is held by another process.", path);
            return this.Say("systemPrompt.memoryWriteFailed", ("file", file));
        }

        string version = MemoryVersion.Of(bytes);
        this.LogCommitted(request, folder, version);
        return $"{verb} {file} (version {version}); description: {MemoryIndex.SummaryFor(path, warnings)}";
    }

    /// <summary>Logs a committed change at Information, with the caller, folder, file, command and the new version.</summary>
    /// <param name="request">The call.</param>
    /// <param name="folder">The resolved folder.</param>
    /// <param name="version">The file's new version.</param>
    private void LogCommitted(MemoryRequest request, MemoryFolder folder, string version)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Memory {Command} by '{Caller}' in '{Folder}' on '{File}', now version {Version}.", request.Command, personaName, folder.Label, request.File ?? string.Empty, version);
        }
    }

    /// <summary>Runs a mutating command under the folder's gate, turning an IO failure into a refusal.</summary>
    /// <param name="folder">The resolved folder.</param>
    /// <param name="file">The file name.</param>
    /// <param name="action">The command, which reads, checks and writes.</param>
    /// <param name="cancellationToken">Cancels the wait for the gate.</param>
    private async Task<string> MutateAsync(MemoryFolder folder, string file, Func<Task<string>> action, CancellationToken cancellationToken)
    {
        try
        {
            return await gate.RunAsync(folder.Path, action, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "A memory change to '{File}' in '{Folder}' failed.", file, folder.Label);
            return this.Say("systemPrompt.memoryWriteFailed", ("file", file));
        }
    }

    /// <summary>
    /// The refusal for a write or delete whose version does not fit the file on disk: none given for a file that exists, or one that
    /// is out of date, in which case the current text and version follow so one more call can merge.
    /// </summary>
    /// <param name="file">The file name.</param>
    /// <param name="version">The version the Agent passed.</param>
    /// <param name="current">The file's bytes, or <see langword="null"/> when it is missing.</param>
    /// <param name="missingVersionKey">The Prompt that tells the Agent to read the file first: one for a write, one for a delete.</param>
    private string? VersionRefusal(string file, string? version, byte[]? current, string missingVersionKey)
    {
        if (current is null)
        {
            return null;
        }

        if (version is null)
        {
            return this.Say(missingVersionKey, ("file", file));
        }

        string token = MemoryVersion.Of(current);
        return string.Equals(version, token, StringComparison.Ordinal)
            ? null
            : $"{this.Say("systemPrompt.memoryConflict", ("file", file))}\n\n{WithVersion(Utf8.GetString(current), token)}";
    }

    /// <summary>The refusal for a file over <c>Team:Memory:MaxFileBytes</c>, else <see langword="null"/>.</summary>
    /// <param name="file">The file name.</param>
    /// <param name="size">The size the file would have.</param>
    private string? TooLarge(string file, int size)
    {
        int max = options.Value.Memory.MaxFileBytes;
        return max > 0 && size > max ? this.SayTooLarge(file, size, max) : null;
    }

    /// <summary>The refusal for a file of <paramref name="size"/> bytes over the cap of <paramref name="max"/>.</summary>
    /// <param name="file">The file name.</param>
    /// <param name="size">The size the file would have.</param>
    /// <param name="max">The cap.</param>
    private string SayTooLarge(string file, int size, int max) =>
        this.Say(
            "systemPrompt.memoryTooLarge",
            ("file", file),
            ("size", size.ToString(CultureInfo.InvariantCulture)),
            ("max", max.ToString(CultureInfo.InvariantCulture)));

    /// <summary>
    /// The refusal for an <c>append</c> or <c>replace</c> that would break a file's description: one the file had is removed or changed
    /// into one <c>write</c> would refuse. A file that had no description (an older one, indexed by its first line) is left alone.
    /// </summary>
    /// <param name="file">The file name.</param>
    /// <param name="before">The file's text now.</param>
    /// <param name="after">The file's text after the change.</param>
    private string? DescriptionLost(string file, string before, string after)
    {
        string? had = MemoryFileRules.DescriptionOf(before);
        string? has = MemoryFileRules.DescriptionOf(after);
        if (had is null || string.Equals(had, has, StringComparison.Ordinal))
        {
            return null;
        }

        return MemoryFileRules.DescriptionProblem(has, file) is { } problem
            ? this.Say("systemPrompt.memoryBadDescription", ("problem", problem))
            : null;
    }

    /// <summary>The text followed by a blank line and its version line.</summary>
    /// <param name="text">The file's text.</param>
    /// <param name="token">The file's version.</param>
    private static string WithVersion(string text, string token) =>
        text.EndsWith('\n') ? $"{text}\nversion: {token}" : $"{text}\n\nversion: {token}";

    /// <summary>Reads a file's bytes, or <see langword="null"/> when it does not exist.</summary>
    /// <param name="path">The file's full path.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    private static async Task<byte[]?> ReadIfExistsAsync(string path, CancellationToken cancellationToken)
    {
        return File.Exists(path) ? await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false) : null;
    }

    /// <summary>
    /// Picks the spelling of <c>old</c> and <c>new</c> to match with: as written, or with Windows line breaks when the file has them
    /// and the text as written is not in it, so a file the Human saved in an editor still matches.
    /// </summary>
    /// <param name="text">The file's text.</param>
    /// <param name="old">The text to find.</param>
    /// <param name="replacement">The text to put in its place.</param>
    private static (string Old, string New) ChooseSpelling(string text, string old, string replacement)
    {
        if (!text.Contains("\r\n", StringComparison.Ordinal) || text.Contains(old, StringComparison.Ordinal))
        {
            return (old, replacement);
        }

        static string Crlf(string value) => value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\n", "\r\n", StringComparison.Ordinal);
        return (Crlf(old), Crlf(replacement));
    }

    /// <summary>Counts the non-overlapping, ordinal occurrences of <paramref name="needle"/> in <paramref name="text"/>.</summary>
    /// <param name="text">The text searched.</param>
    /// <param name="needle">A non-empty piece of text.</param>
    private static int CountOccurrences(string text, string needle)
    {
        int count = 0;
        int at = text.IndexOf(needle, StringComparison.Ordinal);
        while (at >= 0)
        {
            count++;
            at = text.IndexOf(needle, at + needle.Length, StringComparison.Ordinal);
        }

        return count;
    }

    /// <summary>Renders a Prompt, naming each placeholder by its bare name.</summary>
    /// <param name="key">The Prompt's key.</param>
    /// <param name="values">The placeholder names (without braces) and their values.</param>
    private string Say(string key, params (string Name, string Value)[] values) => prompts.Say(key, values);
}
