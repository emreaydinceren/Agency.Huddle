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
internal sealed class MemoryTool(
    string personaName,
    AgentPaths agentPaths,
    TeamMemoryAccess access,
    MemoryFolderGate gate,
    IRecycleBin recycleBin,
    IPromptSource prompts,
    IOptions<TeamOptions> options,
    MemoryWarnings warnings,
    ILogger<MemoryTool> logger) : IAppTool
{
    /// <summary>The tool's name, without any Adapter prefix.</summary>
    public const string ToolName = "memory";

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    private readonly MemoryPaths paths = new(agentPaths, access, options);

    private readonly MemoryWriter writer = new(gate, options);

    /// <inheritdoc />
    public string Name => ToolName;

    /// <summary>Whether <paramref name="name"/> is this tool as offered: the bare name, or the name behind an Adapter's <c>mcp__{server}__</c> prefix.</summary>
    /// <param name="name">A tool name as offered or as an Adapter reported it.</param>
    /// <returns><see langword="true"/> for <c>memory</c> and for any name ending <c>__memory</c>.</returns>
    public static bool IsToolName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        return string.Equals(name, ToolName, StringComparison.Ordinal)
            || name.EndsWith("__" + ToolName, StringComparison.Ordinal);
    }

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

        MemoryRequest request = MemoryRequest.From(arguments);
        if (request.Problem() is { } problem)
        {
            return this.Say("systemPrompt.memoryBadCommand", ("problem", problem));
        }

        MemoryFolder? folder = this.paths.ResolveFolder(personaName, request.Folder);
        if (folder is null)
        {
            logger.LogWarning("Refused a memory call from '{Caller}': '{Folder}' is not a folder it may use.", personaName, request.Folder);
            return this.Say("systemPrompt.memoryBadFolder", ("folder", request.Folder ?? string.Empty), ("folders", string.Join(", ", this.UsableFolders())));
        }

        if (request.Command == "list")
        {
            return this.List(folder);
        }

        return await this.RunFileCommandAsync(request, folder, cancellationToken).ConfigureAwait(false);
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

    /// <summary>The <c>folder</c> values the caller may use: its own, then each readable Team and each of that Team's Projects.</summary>
    private List<string> UsableFolders()
    {
        List<string> folders = [MemoryPaths.OwnFolder];
        foreach (TeamSummary team in access.Readable(personaName))
        {
            folders.Add(team.Name);
            folders.AddRange(team.Projects.Select(project => $"{team.Name}/{project}"));
        }

        return folders;
    }

    /// <summary>The refusal for a change to a Team's Memory by anyone but its manager or the Chief of Staff, else <see langword="null"/>.</summary>
    /// <param name="folder">The resolved folder.</param>
    private string? WriteRefusal(MemoryFolder folder)
    {
        if (folder.Team is not { } team || access.CanWrite(personaName, team))
        {
            return null;
        }

        logger.LogWarning("Refused a change to the Team Memory of '{Team}' by '{Caller}': only its manager writes it.", team.Name, personaName);
        return team.Manager is { } manager
            ? this.Say("systemPrompt.memoryManagerOnly", ("team", team.Name), ("manager", manager))
            : this.Say("systemPrompt.memoryNoManager", ("team", team.Name));
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
        if (folder.Team is not null)
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
            return this.Say("systemPrompt.memoryWriteFailed", ("file", file));
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
        byte[]? current = await ReadIfExistsAsync(path, cancellationToken).ConfigureAwait(false);
        if (current is null)
        {
            return this.Say("systemPrompt.memoryNotFound", ("file", file));
        }

        string existing = Utf8.GetString(current);
        string separator = existing.Length == 0 || existing.EndsWith('\n') ? string.Empty : "\n";
        string text = request.Text ?? string.Empty;
        byte[] bytes = Utf8.GetBytes(existing + separator + text + (text.EndsWith('\n') ? string.Empty : "\n"));

        return this.TooLarge(file, bytes.Length) is { } tooLarge
            ? tooLarge
            : await this.CommitAsync(request, folder, path, bytes, "appended to", cancellationToken).ConfigureAwait(false);
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
        byte[] bytes = Utf8.GetBytes(string.Concat(text.AsSpan(0, at), match.New, text.AsSpan(at + match.Old.Length)));

        return this.TooLarge(file, bytes.Length) is { } tooLarge
            ? tooLarge
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
    private string Say(string key, params (string Name, string Value)[] values)
    {
        Dictionary<string, string> map = new(StringComparer.Ordinal);
        foreach ((string name, string value) in values)
        {
            map["{{" + name + "}}"] = value;
        }

        return prompts.Render(key, map);
    }
}
