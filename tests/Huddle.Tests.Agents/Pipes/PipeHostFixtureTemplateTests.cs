using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.SkillSources.Move;

namespace Agency.Huddle.Tests.Pipes;

/// <summary>
/// Pins <see cref="SkillSourcesTemplate"/>: a <see cref="PipeHostFixture"/> whose Skill Sources folder was copied from the
/// process's template ends up with the same files as one whose host ran the full first-start move itself, so the shortcut
/// cannot drift from what the app writes on a first start.
/// </summary>
public sealed partial class PipeHostFixtureTemplateTests
{
    /// <summary>
    /// The whole data folder of a templated fixture, after its host has started, matches the folder of a fixture whose host
    /// ran the move: the same files with the same text, once each folder's own path and the moment each was written are
    /// masked. The templated host must also have skipped its move, or the comparison would prove nothing.
    /// </summary>
    [Fact]
    public async Task TemplatedFolder_MatchesAFullFirstStart()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        await using PipeHostFixture full = await PipeHostFixture.StartAsync(null, useSkillSourcesTemplate: false, ct);
        await using PipeHostFixture templated = await PipeHostFixture.StartAsync(null, useSkillSourcesTemplate: true, ct);

        Assert.Contains(full.LogEntries, static entry => IsMoveRun(entry));
        Assert.DoesNotContain(templated.LogEntries, static entry => IsMoveRun(entry));
        Assert.True(File.Exists(Path.Combine(DataDir(templated), "SkillSources", "SKILLS.md")), "The templated folder has no SKILLS.md.");

        SortedDictionary<string, string> expected = Normalised(DataDir(full));
        SortedDictionary<string, string> actual = Normalised(DataDir(templated));

        Assert.Equal(expected.Keys, actual.Keys);
        Assert.All(expected, pair => Assert.Equal(pair.Value, actual[pair.Key]));
    }

    /// <summary>A fixture's data folder, absolute.</summary>
    private static string DataDir(PipeHostFixture fixture) => Path.GetFullPath(fixture.Services.GetRequiredService<IOptions<TeamOptions>>().Value.DataDir);

    /// <summary>Whether a log line is the host's own move announcing that it is running.</summary>
    private static bool IsMoveRun(CapturedLogEntry entry) =>
        string.Equals(entry.Category, typeof(SkillSourcesMove).FullName, StringComparison.Ordinal)
        && entry.Message.StartsWith("Moving Packs and Skills into ", StringComparison.Ordinal);

    /// <summary>
    /// Every file under <paramref name="dataDir"/> by relative path, decoded byte for byte (a byte order mark survives as a
    /// character), with the folder's own path masked in each spelling (JSON-escaped, plain, forward slashes, any case) and
    /// every timestamp masked. The Skills panel's <c>seen</c> keys are upper-cased paths with <c>\</c> separators even on
    /// Linux (<c>ResolvedRegistry.RowKeyOf</c>), so the backslashed, JSON-escaped spelling is masked first. SQLite files are
    /// left out: the same code writes them in both hosts, and they are not text.
    /// </summary>
    private static SortedDictionary<string, string> Normalised(string dataDir)
    {
        string[] spellings =
        [
            dataDir.Replace('/', '\\').Replace("\\", "\\\\", StringComparison.Ordinal),
            dataDir.Replace("\\", "\\\\", StringComparison.Ordinal),
            dataDir,
            dataDir.Replace('\\', '/'),
        ];
        SortedDictionary<string, string> normalised = new(StringComparer.Ordinal);
        foreach (string file in Directory.GetFiles(dataDir, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(dataDir, file).Replace('\\', '/');
            if (relative.Contains(".db", StringComparison.Ordinal))
            {
                continue;
            }

            string text = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetString(File.ReadAllBytes(file));
            string masked = spellings.Aggregate(text, static (current, spelling) => current.Replace(spelling, "<data>", StringComparison.OrdinalIgnoreCase));
            normalised[relative] = Timestamp().Replace(masked, "<time>");
        }

        return normalised;
    }

    /// <summary>An ISO 8601 date and time, with or without fractions and offset.</summary>
    [GeneratedRegex(@"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(:\d{2}(\.\d+)?)?(Z|[+-]\d{2}:\d{2})?", RegexOptions.CultureInvariant)]
    private static partial Regex Timestamp();
}
