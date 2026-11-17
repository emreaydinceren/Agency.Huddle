namespace Agency.Huddle.Tests.Memory;

using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Memory;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Elicitation;
using Agency.Huddle.Tests.Library;
using Agency.Huddle.Tests.Teams;

/// <summary>
/// Builds the <see cref="MemoryTool"/> for a caller over a temporary data folder, a hand-driven Team catalog and a recording
/// Recycle Bin, and runs its commands. One harness is one install: its tools share one <see cref="MemoryFolderGate"/>.
/// </summary>
internal sealed class MemoryToolHarness : IDisposable
{
    private readonly TempDataDir dir = new();
    private readonly IOptions<TeamOptions> options;
    private readonly AgentPaths paths;
    private readonly MemoryFolderGate gate = new();
    private readonly HashSet<string> chiefOfStaff = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Initializes a new instance of the <see cref="MemoryToolHarness"/> class.</summary>
    /// <param name="configure">Changes the options before the tools are built, or <see langword="null"/>.</param>
    public MemoryToolHarness(Action<TeamOptions>? configure = null)
    {
        this.options = this.dir.Options(configure);
        this.paths = new AgentPaths(this.options);
        this.Access = new TeamMemoryAccess(this.Catalog, name => this.chiefOfStaff.Contains(name));
        this.Renames = new Agency.Huddle.App.Teams.TeamRenameStore(this.options);
    }

    /// <summary>The rename journal the tool consults; empty until a test journals a rename.</summary>
    public Agency.Huddle.App.Teams.TeamRenameStore Renames { get; }

    /// <summary>The Teams the tool sees; set <see cref="FakeTeamCatalog.Teams"/> before a call.</summary>
    public FakeTeamCatalog Catalog { get; } = new();

    /// <summary>The clock every tool of this harness reads; fixed at 2026-11-16 noon UTC in a UTC zone until a test moves it.</summary>
    public ManualTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 11, 16, 12, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc);

    /// <summary>The Prompts every tool of this harness renders: catalog defaults until a test overrides one.</summary>
    public FakePromptSource Prompts { get; } = new();

    /// <summary>The Recycle Bin a <c>delete</c> sends to.</summary>
    public RecordingRecycleBin Bin { get; } = new();

    /// <summary>The logger every tool of this harness writes to.</summary>
    public RecordingLogger<MemoryTool> Logger { get; } = new();

    /// <summary>Who may read and write which Team folder.</summary>
    public TeamMemoryAccess Access { get; }

    /// <summary>The Teams root folder.</summary>
    public string TeamsRoot => Path.GetFullPath(Path.Combine(this.dir.Path, this.options.Value.Teams.Dir));

    /// <summary>Makes <paramref name="persona"/> the Chief of Staff.</summary>
    /// <param name="persona">The Persona's name.</param>
    public void MakeChiefOfStaff(string persona) => this.chiefOfStaff.Add(persona);

    /// <summary>The memory folder of <paramref name="persona"/>.</summary>
    /// <param name="persona">The Persona's name.</param>
    public string MemoryDir(string persona = "Nova") => Path.Combine(this.paths.WorkDir(persona), "memory");

    /// <summary>The memory folder of a Team, or of one of its Projects.</summary>
    /// <param name="team">The Team's folder name.</param>
    /// <param name="project">The Project's folder name, or <see langword="null"/> for the Team-wide folder.</param>
    public string TeamMemoryDir(string team, string? project = null) =>
        project is null ? Path.Combine(this.TeamsRoot, team, "memory") : Path.Combine(this.TeamsRoot, team, project, "memory");

    /// <summary>The path resolver over this harness's folders and access rules, the one the tool resolves with.</summary>
    public MemoryPaths Paths() => new(this.paths, this.Access, this.options);

    /// <summary>Builds the tool for a caller.</summary>
    /// <param name="persona">The calling Persona's name.</param>
    public MemoryTool ToolFor(string persona = "Nova") =>
        new(persona, this.paths, this.Access, this.gate, this.Bin, this.Prompts, this.options, new MemoryWarnings(NullLogger<MemoryWarnings>.Instance), this.Logger, this.Renames, this.Clock);

    /// <summary>Runs one command as a caller.</summary>
    /// <param name="persona">The calling Persona's name.</param>
    /// <param name="arguments">The tool arguments.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    public Task<string> RunAsAsync(string persona, JsonObject arguments, CancellationToken cancellationToken) =>
        this.ToolFor(persona).InvokeAsync(arguments, cancellationToken);

    /// <summary>Runs one command as Nova.</summary>
    /// <param name="arguments">The tool arguments.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    public Task<string> RunAsync(JsonObject arguments, CancellationToken cancellationToken) =>
        this.RunAsAsync("Nova", arguments, cancellationToken);

    /// <summary>Writes a memory file straight to disk, as the Human's editor would.</summary>
    /// <param name="folder">The memory folder.</param>
    /// <param name="file">The file name.</param>
    /// <param name="content">The whole content.</param>
    public static string Put(string folder, string file, string content)
    {
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, file);
        File.WriteAllText(path, content);
        return path;
    }

    /// <summary>Builds tool arguments from a command and name-value pairs.</summary>
    /// <param name="command">The command.</param>
    /// <param name="pairs">The other arguments.</param>
    public static JsonObject Args(string command, params (string Name, string Value)[] pairs)
    {
        JsonObject arguments = new() { ["command"] = command };
        foreach ((string name, string value) in pairs)
        {
            arguments[name] = value;
        }

        return arguments;
    }

    /// <summary>The version line a <c>read</c> ends with, taken from its result.</summary>
    /// <param name="readResult">The result of a <c>read</c>.</param>
    public static string VersionOf(string readResult)
    {
        ArgumentNullException.ThrowIfNull(readResult);
        const string Marker = "version: ";
        int at = readResult.LastIndexOf(Marker, StringComparison.Ordinal);
        Assert.True(at >= 0, "A read ends with its version.");
        return readResult[(at + Marker.Length)..].Trim();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        this.gate.Dispose();
        this.dir.Dispose();
    }
}
