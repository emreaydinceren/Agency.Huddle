using System.Text.Json.Nodes;

namespace Agency.Huddle.App.Memory;

/// <summary>
/// The arguments of one <c>memory</c> call, read out of the tool's JSON (Instructions spec §5.1). A value that is absent or
/// not a string is <see langword="null"/>; whether a command needs it is decided by <see cref="Problem"/>.
/// </summary>
/// <param name="Command">One of <c>list</c>, <c>read</c>, <c>write</c>, <c>append</c>, <c>replace</c>, <c>delete</c>.</param>
/// <param name="Folder"><c>me</c>, a Team, or <c>Team/Project</c>; <see langword="null"/> means <c>me</c>.</param>
/// <param name="File">A bare file name ending in <c>.md</c>.</param>
/// <param name="Description">The one-line description of a file being written.</param>
/// <param name="Text">The body to write or append.</param>
/// <param name="Old">The text a <c>replace</c> swaps out.</param>
/// <param name="New">The text a <c>replace</c> swaps in.</param>
/// <param name="Version">The token a <c>read</c> returned.</param>
internal sealed record MemoryRequest(
    string? Command,
    string? Folder,
    string? File,
    string? Description,
    string? Text,
    string? Old,
    string? New,
    string? Version)
{
    /// <summary>The commands the tool understands, in the order its description lists them.</summary>
    public static readonly IReadOnlyList<string> Commands = ["list", "read", "write", "append", "replace", "delete"];

    /// <summary>Whether the command changes a file.</summary>
    public bool Mutates => this.Command is "write" or "append" or "replace" or "delete";

    /// <summary>Reads a call's arguments.</summary>
    /// <param name="arguments">The tool arguments.</param>
    /// <returns>The request.</returns>
    public static MemoryRequest From(JsonObject arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        return new MemoryRequest(
            Read(arguments, "command"),
            Read(arguments, "folder"),
            Read(arguments, "file"),
            Read(arguments, "description"),
            Read(arguments, "text"),
            Read(arguments, "old"),
            Read(arguments, "new"),
            Read(arguments, "version"));
    }

    /// <summary>Checks that the command is known and that it has the arguments it cannot do without.</summary>
    /// <returns>What is wrong, or <see langword="null"/> when the call can be tried.</returns>
    public string? Problem()
    {
        if (string.IsNullOrWhiteSpace(this.Command))
        {
            return "A memory call needs a 'command'.";
        }

        if (!Commands.Contains(this.Command, StringComparer.Ordinal))
        {
            return $"Unknown command '{this.Command}'.";
        }

        string? missing = this.MissingArgument();
        return missing is null ? null : $"The {this.Command} command needs {missing}.";
    }

    /// <summary>The first argument the command cannot do without and was not given, quoted, or <see langword="null"/> when it has them all.</summary>
    private string? MissingArgument()
    {
        if (this.Command is "list")
        {
            return null;
        }

        if (this.File is null)
        {
            return "'file'";
        }

        return this.Command switch
        {
            "write" or "append" => this.Text is null ? "'text'" : null,
            "replace" => this.MissingReplacement(),
            _ => null,
        };
    }

    /// <summary>The missing argument of a <c>replace</c>, which needs a non-empty <c>old</c> and a <c>new</c> (possibly empty).</summary>
    private string? MissingReplacement()
    {
        if (string.IsNullOrEmpty(this.Old))
        {
            return "'old'";
        }

        return this.New is null ? "'new'" : null;
    }

    /// <summary>Reads one string argument; a missing or non-string one is <see langword="null"/>.</summary>
    /// <param name="arguments">The tool arguments.</param>
    /// <param name="name">The argument's name.</param>
    private static string? Read(JsonObject arguments, string name) =>
        arguments[name] is JsonValue value && value.TryGetValue(out string? text) ? text : null;
}
