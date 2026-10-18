using System.Text;
using System.Text.Json;
using Agency.Huddle.App;
using Agency.Huddle.App.SkillSources;
using Agency.Huddle.SkillPacks.Reading;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Agency.Huddle.Tests.SkillPacks.Stores;

/// <summary>Builds the small Skill Sources stores over a temporary data folder.</summary>
internal static class StoreFixture
{
    /// <summary>Settings over <paramref name="dir"/>, with an optional configured folder and computer name.</summary>
    public static SkillSourcesSettings Settings(TempDataDir dir, string? skillSourcesDir = null, string computer = "DESK-01")
    {
        IOptions<TeamOptions> options = Options.Create(new TeamOptions { DataDir = dir.Path, SkillSourcesDir = skillSourcesDir ?? string.Empty, ComputerName = computer });
        return new SkillSourcesSettings(options, NullLogger<SkillSourcesSettings>.Instance);
    }
}

/// <summary>Pins the four configuration keys of §8.7 and their defaults.</summary>
public sealed class TeamOptionsSkillSourcesTests
{
    /// <summary>All four keys default to empty, which means the documented default.</summary>
    [Fact]
    public void Defaults_AreEmpty()
    {
        TeamOptions options = new();

        Assert.Equal(string.Empty, options.SkillSourcesDir);
        Assert.Equal(string.Empty, options.ComputerName);
        Assert.Equal(string.Empty, options.SkillsRepositoryDir);
        Assert.Equal(string.Empty, options.HuddlePacksPath);
    }

    /// <summary>Empty keys give <c>{DataDir}/SkillSources</c> and the machine name.</summary>
    [Fact]
    public void EmptyKeys_GiveDefaults()
    {
        using TempDataDir dir = new();

        SkillSourcesSettings settings = StoreFixture.Settings(dir, computer: string.Empty);

        Assert.Equal(Path.Combine(dir.Path, "SkillSources"), settings.Dir);
        Assert.Equal(Environment.MachineName, settings.ComputerName);
        Assert.EndsWith("huddle-packs", Path.GetFileNameWithoutExtension(settings.HuddlePacksPath), StringComparison.Ordinal);
    }
}

/// <summary>Pins <c>skill-sources.json</c> (§7.2): <c>dir</c> wins over the key, <c>seen</c> writes keep <c>dir</c>, a missing
/// folder is refused, an unreadable file is absent.</summary>
public sealed class SkillSourcesSettingsTests
{
    /// <summary>A folder that does not exist is refused and nothing is written.</summary>
    [Fact]
    public void SetDir_MissingFolder_Refused()
    {
        using TempDataDir dir = new();
        SkillSourcesSettings settings = StoreFixture.Settings(dir);

        SetDirResult result = settings.SetDir(Path.Combine(dir.Path, "nowhere"));

        Assert.False(result.Changed);
        Assert.NotNull(result.Problem);
        Assert.False(File.Exists(settings.FilePath));
    }

    /// <summary>Writing <c>seen</c> keeps the <c>dir</c> already in the file.</summary>
    [Fact]
    public void MarkSeen_KeepsDirInSkillSourcesJson()
    {
        using TempDataDir dir = new();
        string shared = Directory.CreateDirectory(Path.Combine(dir.Path, "Shared")).FullName;
        SkillSourcesSettings settings = StoreFixture.Settings(dir);
        Assert.True(settings.SetDir(shared).Changed);

        settings.SetSeen(["skill:docx", "packs:X"]);

        SkillSourcesSettings reread = StoreFixture.Settings(dir);
        Assert.Equal(shared, reread.Dir);
        Assert.Equal(["packs:X", "skill:docx"], reread.Seen);
    }

    /// <summary>A <c>skill-sources.json</c> that is not JSON is treated as absent: no dir, no baseline.</summary>
    [Fact]
    public void SkillSourcesJson_Unreadable_TreatedAsAbsent()
    {
        using TempDataDir dir = new();
        File.WriteAllText(Path.Combine(dir.Path, SkillSourcesSettings.FileName), "{ nope");

        SkillSourcesSettings settings = StoreFixture.Settings(dir);

        Assert.Null(settings.FileDir);
        Assert.Null(settings.Seen);
        Assert.Equal(Path.Combine(dir.Path, "SkillSources"), settings.Dir);
    }

    /// <summary>With no file, <c>Team:SkillSourcesDir</c> is used, relative to the data folder.</summary>
    [Fact]
    public void Dir_ConfigurationKey_UsedWhenNoFile()
    {
        using TempDataDir dir = new();

        SkillSourcesSettings settings = StoreFixture.Settings(dir, skillSourcesDir: "Elsewhere");

        Assert.Equal(Path.Combine(dir.Path, "Elsewhere"), settings.Dir);
    }

    /// <summary>Setting a folder raises Changed once; setting it again changes nothing.</summary>
    [Fact]
    public void SetDir_RaisesChangedOnce()
    {
        using TempDataDir dir = new();
        string shared = Directory.CreateDirectory(Path.Combine(dir.Path, "Shared")).FullName;
        SkillSourcesSettings settings = StoreFixture.Settings(dir);
        int changes = 0;
        settings.Changed += () => changes++;

        settings.SetDir(shared);
        settings.SetDir(shared);

        Assert.Equal(1, changes);
    }
}

/// <summary>Pins <c>hidden.json</c> (§6.12.4): hide and unhide round-trip, a malformed file is not overwritten, the write is
/// through a temporary file.</summary>
public sealed class HiddenStoreTests
{
    /// <summary>Hiding then unhiding returns the file to nothing hidden.</summary>
    [Fact]
    public void Hide_Unhide_RoundTrip()
    {
        using TempDataDir dir = new();
        HiddenStore store = new(StoreFixture.Settings(dir));

        Assert.Equal(HiddenWriteResult.Written, store.Hide(HiddenKind.Skill, "qa-kit"));
        Assert.Equal(HiddenWriteResult.Written, store.Hide(HiddenKind.Pack, "document-skills"));
        Assert.Contains("qa-kit", store.Read().Skills);
        Assert.Contains("DOCUMENT-SKILLS", store.Read().Packs);

        Assert.Equal(HiddenWriteResult.Written, store.Unhide(HiddenKind.Skill, "qa-kit"));
        Assert.Equal(HiddenWriteResult.Unchanged, store.Unhide(HiddenKind.Skill, "qa-kit"));
        Assert.Empty(store.Read().Skills);
        Assert.Equal("""{"schemaVersion":1,"packs":["document-skills"],"skills":[]}""", File.ReadAllText(store.FilePath).Trim());
    }

    /// <summary>A malformed file reads as nothing hidden and is never overwritten.</summary>
    [Fact]
    public void MalformedFile_NotOverwritten()
    {
        using TempDataDir dir = new();
        HiddenStore store = new(StoreFixture.Settings(dir));
        Directory.CreateDirectory(Path.GetDirectoryName(store.FilePath) ?? dir.Path);
        File.WriteAllText(store.FilePath, "[ broken");

        HiddenWriteResult result = store.Hide(HiddenKind.Skill, "qa-kit");

        Assert.Equal(HiddenWriteResult.Refused, result);
        Assert.True(store.Read().Malformed);
        Assert.Equal("[ broken", File.ReadAllText(store.FilePath));
    }

    /// <summary>The write leaves no temporary file behind.</summary>
    [Fact]
    public void Write_IsThroughTemporaryFile()
    {
        using TempDataDir dir = new();
        HiddenStore store = new(StoreFixture.Settings(dir));

        store.Hide(HiddenKind.Skill, "qa-kit");

        Assert.Equal([HiddenStore.FileName], Directory.GetFiles(Path.GetDirectoryName(store.FilePath) ?? dir.Path).Select(Path.GetFileName));
    }
}

/// <summary>Pins <c>log.jsonl</c> (§7.2): one line per call, never rewritten, the fixed fields, no folder created.</summary>
public sealed class SourceLogTests
{
    /// <summary>A log over a Skill Sources folder that has the Source's folder.</summary>
    private static (SourceLog Log, string File, SkillSourcesSettings Settings) Make(TempDataDir dir)
    {
        SkillSourcesSettings settings = StoreFixture.Settings(dir);
        string folder = Directory.CreateDirectory(Path.Combine(settings.Dir, "sources", "github.com", "o", "r")).FullName;
        FakeTimeProvider clock = new(new DateTimeOffset(2026, 10, 17, 16, 41, 2, TimeSpan.Zero));
        return (new SourceLog(settings, clock), Path.Combine(folder, SourceLog.FileName), settings);
    }

    /// <summary>Each call adds one line and keeps the earlier ones as they were.</summary>
    [Fact]
    public void Append_NeverRewrites_OneLinePerCall()
    {
        using TempDataDir dir = new();
        (SourceLog log, string file, _) = Make(dir);

        log.Append("github.com/o/r", new LogLine("row-appeared", "pack:a"));
        string first = File.ReadAllText(file);
        log.Append("github.com/o/r", new LogLine("row-broken", "pack:a", Code: "P005"));

        string[] lines = File.ReadAllLines(file);
        Assert.Equal(2, lines.Length);
        Assert.StartsWith(first, File.ReadAllText(file), StringComparison.Ordinal);
    }

    /// <summary>Concurrent appends give whole lines, each valid JSON.</summary>
    [Fact]
    public void Append_Concurrent_NoInterleave()
    {
        using TempDataDir dir = new();
        (SourceLog log, string file, _) = Make(dir);

        Parallel.For(0, 50, i => log.Append("github.com/o/r", new LogLine("copied", $"skill:s{i}", Detail: new string('x', 500))));

        string[] lines = File.ReadAllLines(file);
        Assert.Equal(50, lines.Length);
        Assert.All(lines, static line => Assert.Equal(JsonValueKind.Object, JsonDocument.Parse(line).RootElement.ValueKind));
    }

    /// <summary>A line starts with at, computer, huddle, event and subject; computer is Team:ComputerName.</summary>
    [Fact]
    public void Line_HasAtComputerHuddleEventSubject()
    {
        using TempDataDir dir = new();
        (SourceLog log, string file, _) = Make(dir);

        log.Append("github.com/o/r", new LogLine("removed", "pack:a", Data: new Dictionary<string, object?> { ["fileDeleted"] = true }));

        using JsonDocument line = JsonDocument.Parse(File.ReadAllText(file));
        Assert.Equal(["at", "computer", "huddle", "event", "subject", "data"], line.RootElement.EnumerateObject().Select(static property => property.Name));
        Assert.Equal("DESK-01", line.RootElement.GetProperty("computer").GetString());
        Assert.Equal("2026-10-17T16:41:02+00:00", line.RootElement.GetProperty("at").GetString());
        Assert.True(line.RootElement.GetProperty("data").GetProperty("fileDeleted").GetBoolean());
    }

    /// <summary>No line and no folder when the Source's folder does not exist.</summary>
    [Fact]
    public void Append_NoSourceFolder_WritesNothing()
    {
        using TempDataDir dir = new();
        (SourceLog log, _, SkillSourcesSettings settings) = Make(dir);

        bool written = log.Append("local/absent", new LogLine("hidden", "skill:x"));

        Assert.False(written);
        Assert.False(Directory.Exists(Path.Combine(settings.Dir, "sources", "local")));
    }
}

/// <summary>Pins removing a row from SKILLS.md (§8.8): every other byte unchanged, CRLF kept, a retry when the file changed.</summary>
public sealed class SkillsMdWriterTests
{
    private const string Text = "# Skills registry\r\n\r\n## Packs\r\n\r\n| Local path | Notes |\r\n| --- | --- |\r\n| packs/a.skillpack.json | first |\r\n| packs/b.skillpack.json | second |\r\n\r\n## Catalogs\r\n\r\n| Local path |\r\n| --- |\r\n| catalogs/c.skillcatalog.json |\r\n";

    /// <summary>Removing a row deletes exactly its line and leaves every other byte, CRLF included.</summary>
    [Fact]
    public void RemoveRow_WritesEveryOtherByteUnchanged_CrlfKept()
    {
        string dir = InMemoryFileReader.At("SS");

        byte[]? edited = SkillsMdWriter.Remove(Encoding.UTF8.GetBytes(Text), RowKind.Pack, Path.Combine(dir, "packs", "a.skillpack.json"), dir, "DESK-01");

        Assert.NotNull(edited);
        Assert.Equal(Text.Replace("| packs/a.skillpack.json | first |\r\n", string.Empty, StringComparison.Ordinal), Encoding.UTF8.GetString(edited));
        Assert.Null(SkillsMdWriter.Remove(Encoding.UTF8.GetBytes(Text), RowKind.Catalog, Path.Combine(dir, "packs", "a.skillpack.json"), dir, "DESK-01"));
    }

    /// <summary>On disk: the row goes and the file is written whole.</summary>
    [Fact]
    public void RemoveRow_OnDisk_RemovesOnceThenNotFound()
    {
        using TempDataDir dir = new();
        SkillSourcesSettings settings = StoreFixture.Settings(dir);
        Directory.CreateDirectory(settings.Dir);
        SkillsMdWriter writer = new(settings);
        File.WriteAllText(writer.FilePath, Text);

        RemoveRowResult result = writer.RemoveRow(RowKind.Catalog, Path.Combine(settings.Dir, "catalogs", "c.skillcatalog.json"));

        Assert.Equal(RemoveRowResult.Removed, result);
        Assert.DoesNotContain("c.skillcatalog.json", File.ReadAllText(writer.FilePath), StringComparison.Ordinal);
        Assert.Equal(RemoveRowResult.NotFound, writer.RemoveRow(RowKind.Catalog, Path.Combine(settings.Dir, "catalogs", "c.skillcatalog.json")));
    }

    /// <summary>The template the move writes reads back with its rows, and empty cells are one space wide.</summary>
    [Fact]
    public void Template_RoundTripsThroughReader()
    {
        string text = SkillsMdTemplate.Build(
            [new TemplateRow(@"DESK-01: E:\Repos\skills\packs\a.skillpack.json", string.Empty, "2026-10-17", "Shipped with Huddle")],
            [new TemplateRow("catalogs/c.skillcatalog.json", "https://github.com/o/r", "2026-10-17", "From SkillsCatalog.json; 3 Skills")]);

        Registry registry = RegistryReader.Read(text, InMemoryFileReader.At("SS"), "DESK-01");

        Assert.Empty(registry.Findings);
        Assert.Equal(@"E:\Repos\skills\packs\a.skillpack.json", Assert.Single(registry.Packs).LocalPath.Resolve("DESK-01"));
        Assert.Contains(@"| DESK-01: E:\Repos\skills\packs\a.skillpack.json | | 2026-10-17 | Shipped with Huddle |", text, StringComparison.Ordinal);
        Assert.EndsWith("|\n", text, StringComparison.Ordinal);
    }
}
