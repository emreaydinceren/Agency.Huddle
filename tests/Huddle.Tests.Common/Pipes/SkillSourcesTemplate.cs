using Agency.Huddle.App;
using Agency.Huddle.App.SkillSources;
using Agency.Huddle.App.SkillSources.Move;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.Tests.Pipes;

/// <summary>
/// The files a first start's Skill Sources move writes into an empty data folder, made once per test process and copied into
/// each <see cref="PipeHostFixture"/>'s own folder before its host starts, so the host's <see cref="SkillSourcesMove"/> finds
/// <c>SKILLS.md</c> and skips. Every fixture otherwise repeats the same first-run move: about 100 ms of a warm start in a
/// CPU-loaded Linux process. The first fixture of a process still pays for the move once, here. The app's start-up is
/// unchanged; only the folder the test hands it is prepared differently, and <c>PipeHostFixtureTemplateTests</c> pins that
/// the folder matches a full first start.
/// </summary>
/// <remarks>
/// The files are copied, never hard-linked: a test may write to <c>SKILLS.md</c> or a catalog, and a link would carry that
/// write into every other fixture of the process. The template is made by the real <see cref="SkillSourcesMove"/> with default
/// <see cref="TeamOptions"/>, so a fixture uses it only when its host's options leave every input of the move at that default
/// (<see cref="Applies"/>).
/// </remarks>
internal static class SkillSourcesTemplate
{
    private static readonly Lazy<Task<string>> Folder = new(BuildAsync, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>UTF-8 that neither adds nor strips a byte order mark, so an untouched file is copied byte for byte.</summary>
    private static readonly System.Text.UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// Whether the template is what the move would write for this host: the data folder is the fixture's own, the Skill Sources
    /// folder is its default, there is no skills repository to add Pack rows from, and <c>Skills/</c> is the default folder.
    /// The fixture's folder is new and empty, so there is no <c>SkillsCatalog.json</c>, old Pack or imported Skill to move.
    /// </summary>
    /// <param name="options">The host's bound options.</param>
    /// <param name="settings">The host's Skill Sources settings.</param>
    /// <param name="dataDir">The fixture's data folder.</param>
    public static bool Applies(TeamOptions options, SkillSourcesSettings settings, string dataDir)
    {
        string full = Path.GetFullPath(dataDir);
        TeamOptions defaults = new() { DataDir = full };
        return string.Equals(settings.DataDir, full, StringComparison.Ordinal)
            && string.Equals(settings.Dir, Path.Combine(full, "SkillSources"), StringComparison.Ordinal)
            && string.IsNullOrWhiteSpace(options.SkillsRepositoryDir)
            && string.Equals(options.Acp.SkillsDir, defaults.Acp.SkillsDir, StringComparison.Ordinal)
            && !Directory.EnumerateFileSystemEntries(full).Any();
    }

    /// <summary>
    /// Copies the template's files into <paramref name="dataDir"/>, making the template first if this process has none. The move
    /// writes the data folder's own path into two files (the my-packs source's <c>localPath</c> and its <c>NOTES.md</c>), so a
    /// file naming the template's folder is rewritten to name <paramref name="dataDir"/>, in each spelling the move uses.
    /// </summary>
    /// <param name="dataDir">The fixture's empty data folder.</param>
    /// <param name="ct">Bounds the wait for the template; making it is not cancelled, since other fixtures share it.</param>
    public static async Task CopyIntoAsync(string dataDir, CancellationToken ct)
    {
        string template = await Folder.Value.WaitAsync(ct);
        string target = Path.GetFullPath(dataDir);
        (string From, string To)[] spellings =
        [
            (JsonEscaped(template), JsonEscaped(target)),
            (template, target),
        ];
        foreach (string file in Directory.EnumerateFiles(template, "*", SearchOption.AllDirectories))
        {
            string copy = Path.Combine(target, Path.GetRelativePath(template, file));
            Directory.CreateDirectory(Path.GetDirectoryName(copy) ?? target);
            byte[] bytes = await File.ReadAllBytesAsync(file, ct);
            string text = Utf8.GetString(bytes);
            string rewritten = spellings.Aggregate(text, static (current, spelling) => current.Replace(spelling.From, spelling.To, StringComparison.Ordinal));
            await File.WriteAllBytesAsync(copy, string.Equals(rewritten, text, StringComparison.Ordinal) ? bytes : Utf8.GetBytes(rewritten), ct);
        }

        foreach (string folder in Directory.EnumerateDirectories(template, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(target, Path.GetRelativePath(template, folder)));
        }
    }

    /// <summary>A path as a JSON string spells it: each backslash doubled.</summary>
    private static string JsonEscaped(string path) => path.Replace("\\", "\\\\", StringComparison.Ordinal);

    /// <summary>Runs the real move once into a new folder that lives until the process exits.</summary>
    private static async Task<string> BuildAsync()
    {
        TempDataDir dir = new();
        AppDomain.CurrentDomain.ProcessExit += (_, _) => dir.Dispose();
        IOptions<TeamOptions> options = Microsoft.Extensions.Options.Options.Create(new TeamOptions { DataDir = dir.Path });
        SkillSourcesSettings settings = new(options, NullLogger<SkillSourcesSettings>.Instance);
        SkillSourcesMove move = new(options, settings, new PhysicalMoveFiles(), new MoveReportStore(settings), new SourceLog(settings, TimeProvider.System), NullLogger<SkillSourcesMove>.Instance);
        MoveReport? report = await move.RunAsync(CancellationToken.None);
        if (report is null || !File.Exists(Path.Combine(settings.Dir, "SKILLS.md")))
        {
            throw new InvalidOperationException($"The Skill Sources move wrote no SKILLS.md into the template folder {dir.Path}.");
        }

        return dir.Path;
    }
}
