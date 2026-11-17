using System.Text.Json;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.DocHistory;
using Agency.Huddle.App.Helpers;
using Agency.Huddle.App.Helpers.MemoryTidy;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Memory;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Feed;
using Agency.Huddle.Tests.Teams;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.Tests.Helpers;

/// <summary>
/// One install for the Memory Tidy Helper's apply step (Memory Tidy spec §8.1): Nova's own memory folder in a temp data folder, the real
/// memory writer, History, Library resolver, day rows and journal, and a pass folder to copy into.
/// </summary>
internal sealed class MemoryTidyApplyHarness : IDisposable
{
    /// <summary>The key of Nova's own memory folder.</summary>
    internal const string Key = "agent:Nova";

    private readonly TempDataDir dir = new();
    private readonly MemoryFolderGate gate = new();
    private readonly ManualTimeProvider clock = new(new DateTimeOffset(2026, 11, 22, 12, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc);
    private readonly MemoryTidyInputBuilder builder;

    /// <summary>Wires the install.</summary>
    /// <param name="configure">Changes the options before anything is built, or <see langword="null"/>.</param>
    public MemoryTidyApplyHarness(Action<TeamOptions>? configure = null)
    {
        this.Options = this.dir.Options(team =>
        {
            team.Acp.Enabled = true;
            configure?.Invoke(team);
        });
        TeamMemoryAccess access = new(new FakeTeamCatalog(), static _ => false);
        AgentPaths agentPaths = new(this.Options);
        MemoryPaths paths = new(agentPaths, access, this.Options);
        this.Folder = paths.OwnFolderOf("Nova") ?? throw new InvalidOperationException("Nova's folder resolved to nothing.");
        _ = Directory.CreateDirectory(this.Folder.Path);

        DocHistoryStore historyStore = new(this.Options, NullLogger<DocHistoryStore>.Instance, this.Feed);
        this.History = historyStore;
        DocHistoryRecorder recorder = new(historyStore, this.Options, NullLogger<DocHistoryRecorder>.Instance);
        this.Resolver = new LibraryPathResolver(new LibraryRootStore(this.Options, agentPaths, NullLogger<LibraryRootStore>.Instance), this.Options, NullLogger<LibraryPathResolver>.Instance);
        this.SettingsStore = new HelperSettingsStore(this.Options);
        this.Store = new MemoryTidyStateStore(this.Options);
        this.builder = new MemoryTidyInputBuilder(this.Store, this.Options, this.clock);
        this.PassFolder = Path.Combine(this.dir.Path, "pass");
        _ = Directory.CreateDirectory(this.PassFolder);
        this.BackupFolder = Path.Combine(this.PassFolder, "Agents", "Nova");
        this.Helper = new MemoryTidyHelper(
            paths,
            new MemoryWriter(this.gate, this.Options),
            recorder,
            this.Resolver,
            this.Store,
            new HelperSettingsResolver(this.SettingsStore, this.Options, NullLogger<HelperSettingsResolver>.Instance),
            NullLogger<MemoryTidyHelper>.Instance);
    }

    /// <summary>The options of this install.</summary>
    public IOptions<TeamOptions> Options { get; }

    /// <summary>Nova's own memory folder.</summary>
    public MemoryFolder Folder { get; }

    /// <summary>Receives every Feed item.</summary>
    public RecordingFeedPublisher Feed { get; } = new();

    /// <summary>The documents' History.</summary>
    public DocHistoryStore History { get; }

    /// <summary>Finds a file in the Library.</summary>
    public LibraryPathResolver Resolver { get; }

    /// <summary>The Human's Helper settings.</summary>
    public HelperSettingsStore SettingsStore { get; }

    /// <summary>The day rows.</summary>
    public MemoryTidyStateStore Store { get; }

    /// <summary>The pass folder; the journal is <c>pass.jsonl</c> in it.</summary>
    public string PassFolder { get; }

    /// <summary>Where Nova's copies go.</summary>
    public string BackupFolder { get; }

    /// <summary>The Helper.</summary>
    public MemoryTidyHelper Helper { get; }

    /// <summary>Writes a topic file straight to disk, with a description, as the Human's editor would.</summary>
    /// <param name="file">The file name.</param>
    /// <param name="description">The description, or <see langword="null"/> for a file with no frontmatter.</param>
    /// <param name="body">The body.</param>
    /// <param name="written">When the file was written; 2026-11-10 when omitted.</param>
    /// <returns>The file's full path.</returns>
    public string Put(string file, string? description, string body, DateTime? written = null)
    {
        string path = Path.Combine(this.Folder.Path, file);
        File.WriteAllText(path, description is null ? body + "\n" : MemoryFileRules.Compose(description, body));
        File.SetLastWriteTimeUtc(path, written ?? new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc));
        return path;
    }

    /// <summary>The path of a file in Nova's folder.</summary>
    /// <param name="file">The file name.</param>
    public string PathOf(string file) => Path.Combine(this.Folder.Path, file);

    /// <summary>Builds the input as the pass would, over the journal of this install.</summary>
    /// <param name="backupFolder">Where the copies go; this install's own when omitted.</param>
    public MemoryTidyInput Input(string? backupFolder = null)
    {
        MemoryTidyPassContext context = new("2026-11-22", backupFolder ?? this.BackupFolder, new MemoryTidyJournal(this.PassFolder));
        return this.builder.Build(this.Folder, Key, "the Agent Nova", new DateOnly(2026, 11, 22), context)?.Input
            ?? throw new InvalidOperationException("The folder could not be read.");
    }

    /// <summary>Applies a Verdict of these actions.</summary>
    /// <param name="input">The input.</param>
    /// <param name="actions">The actions, in order.</param>
    public Task<HelperApplied> ApplyAsync(MemoryTidyInput input, params MemoryTidyAction[] actions) =>
        this.Helper.ApplyAsync(Key, input, new MemoryTidyVerdict(actions), TestContext.Current.CancellationToken);

    /// <summary>The journal's entries, oldest first; empty when there is no journal.</summary>
    public List<MemoryTidyEntry> Entries() => [.. this.JournalText().Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .Select(static line => JsonSerializer.Deserialize(line, MemoryTidyJsonContext.Default.MemoryTidyEntry) ?? throw new InvalidOperationException(line))];

    /// <summary>The journal file as text; empty when there is none.</summary>
    public string JournalText()
    {
        string path = Path.Combine(this.PassFolder, MemoryTidyJournal.FileName);
        return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
    }

    /// <summary>The History entries Memory Tidy wrote for a file.</summary>
    /// <param name="file">The file name.</param>
    public IReadOnlyList<DocHistoryEntry> HistoryOf(string file) =>
        this.Resolver.TryResolveAbsolute(this.PathOf(file), out LibraryPath? path) ? this.History.Read(path) : throw new InvalidOperationException($"{file} is in no Library root.");

    /// <summary>A merge action.</summary>
    /// <param name="into">The file kept.</param>
    /// <param name="from">The files folded in.</param>
    /// <param name="text">The new body.</param>
    /// <param name="description">The description, or <see langword="null"/>.</param>
    public static MemoryTidyAction Merge(string into, string[] from, string text, string? description = null) =>
        new("merge", null, into, from, description, text, "same topic");

    /// <summary>A rewrite action.</summary>
    /// <param name="file">The file.</param>
    /// <param name="text">The new body.</param>
    /// <param name="description">The description, or <see langword="null"/>.</param>
    public static MemoryTidyAction Rewrite(string file, string text, string? description = null) =>
        new("rewrite", file, null, null, description, text, "repeats itself");

    /// <summary>A describe action.</summary>
    /// <param name="file">The file.</param>
    /// <param name="description">The new description.</param>
    public static MemoryTidyAction Describe(string file, string description) =>
        new("describe", file, null, null, description, null, "no description");

    /// <summary>A drop action.</summary>
    /// <param name="file">The file.</param>
    public static MemoryTidyAction Drop(string file) =>
        new("drop", file, null, null, null, null, "out of date");

    /// <inheritdoc/>
    public void Dispose()
    {
        this.gate.Dispose();
        this.dir.Dispose();
    }
}
