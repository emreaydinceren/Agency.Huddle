using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Library;

namespace Agency.Huddle.Tests.Library;

/// <summary>Pins <see cref="PropertyTypes"/>: the types file is read through the path resolver, cached per root and dropped when it changes (D-2 step 1, D-10, D-12).</summary>
public sealed class PropertyTypesTests
{
    private static PropertyTypes Create(LibraryFileServiceFixture fixture, string? file = null)
    {
        TeamOptions options = new();
        if (file is not null)
        {
            options.Library.PropertyTypesFile = file;
        }

        return new PropertyTypes(fixture.Resolver, Options.Create(options));
    }

    private static string NewRoot(LibraryFileServiceFixture fixture)
    {
        string root = fixture.CreatePinnedRoot("root");
        File.WriteAllText(Path.Combine(root, "a.md"), "# A\n");
        return root;
    }

    private static string TypesPath(string root) => Path.Combine(root, LibraryOptions.DefaultPropertyTypesFile.Replace('/', Path.DirectorySeparatorChar));

    private static void WriteTypes(string root, string json, string relative = LibraryOptions.DefaultPropertyTypesFile)
    {
        string path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path) ?? root);
        File.WriteAllText(path, json);
    }

    /// <summary>FP-T5: the default path is the reference app's, held in one constant.</summary>
    [Fact]
    public void DefaultPath_IsTheConstant()
    {
        Assert.Equal(LibraryOptions.DefaultPropertyTypesFile, new LibraryOptions().PropertyTypesFile);
    }

    /// <summary>D-2 step 1: the file's names map to Huddle's types, an unknown name is ignored, and keys match case-insensitively.</summary>
    [Fact]
    public void For_ReadsAndMapsTheFile()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string root = NewRoot(fixture);
        WriteTypes(root, """{ "types": { "pages": "number", "due": "date", "stamp": "datetime", "multi": "multitext", "done": "checkbox", "plain": "text", "al": "aliases", "tg": "tags", "odd": "bogus" } }""");
        fixture.Reload();

        IReadOnlyDictionary<string, NotePropertyKind> types = Create(fixture).For(fixture.Resolve(root, "a.md"));

        Assert.Equal(NotePropertyKind.Number, types["PAGES"]);
        Assert.Equal(NotePropertyKind.Date, types["due"]);
        Assert.Equal(NotePropertyKind.DateTime, types["stamp"]);
        Assert.Equal(NotePropertyKind.List, types["multi"]);
        Assert.Equal(NotePropertyKind.Checkbox, types["done"]);
        Assert.Equal(NotePropertyKind.Text, types["plain"]);
        Assert.Equal(NotePropertyKind.Aliases, types["al"]);
        Assert.Equal(NotePropertyKind.Tags, types["tg"]);
        Assert.False(types.ContainsKey("odd"));
    }

    /// <summary>FP-T5 and B9: a root with no file, an invalid file, a file of the wrong shape and an oversized file each give no assigned types and no exception.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("not json {")]
    [InlineData("[1, 2]")]
    [InlineData("""{ "types": [1] }""")]
    [InlineData("""{ "types": { "k": 5 } }""")]
    [InlineData("")]
    public void For_MissingOrInvalidFile_GivesNoTypes(string? json)
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string root = NewRoot(fixture);
        if (json is not null)
        {
            WriteTypes(root, json);
        }

        fixture.Reload();

        Assert.Empty(Create(fixture).For(fixture.Resolve(root, "a.md")));
    }

    /// <summary>FP-T5: a file over 1 MB is not read.</summary>
    [Fact]
    public void For_OversizedFile_GivesNoTypes()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string root = NewRoot(fixture);
        WriteTypes(root, "{ \"types\": { \"pages\": \"number\" }, \"pad\": \"" + new string('x', (1024 * 1024) + 1) + "\" }");
        fixture.Reload();

        Assert.Empty(Create(fixture).For(fixture.Resolve(root, "a.md")));
    }

    /// <summary>FP-T5: a path that is a folder, not a file, gives no types and no exception.</summary>
    [Fact]
    public void For_PathIsAFolder_GivesNoTypes()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string root = NewRoot(fixture);
        _ = Directory.CreateDirectory(TypesPath(root));
        fixture.Reload();

        Assert.Empty(Create(fixture).For(fixture.Resolve(root, "a.md")));
    }

    /// <summary>FP-T5: a file that cannot be opened gives no types and no exception.</summary>
    [Fact]
    public void For_LockedFile_GivesNoTypes()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "An exclusive lock is only enforced across handles on Windows.");
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string root = NewRoot(fixture);
        WriteTypes(root, """{ "types": { "pages": "number" } }""");
        fixture.Reload();
        using FileStream held = new(TypesPath(root), FileMode.Open, FileAccess.Read, FileShare.None);

        Assert.Empty(Create(fixture).For(fixture.Resolve(root, "a.md")));
    }

    /// <summary>An empty setting turns the types file off, and another path is read from there.</summary>
    [Fact]
    public void For_SettingEmptyIsOff_AndACustomPathIsRead()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string root = NewRoot(fixture);
        WriteTypes(root, """{ "types": { "pages": "number" } }""");
        WriteTypes(root, """{ "types": { "pages": "text" } }""", "meta/types.json");
        fixture.Reload();
        LibraryPath note = fixture.Resolve(root, "a.md");

        Assert.Empty(Create(fixture, string.Empty).For(note));
        Assert.Equal(NotePropertyKind.Text, Create(fixture, "meta/types.json").For(note)["pages"]);
    }

    /// <summary>FP-T18: changing the types file makes the next read use the new types.</summary>
    [Fact]
    public void For_FileChanged_NextReadUsesTheNewTypes()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string root = NewRoot(fixture);
        WriteTypes(root, """{ "types": { "pages": "date" } }""");
        fixture.Reload();
        PropertyTypes types = Create(fixture);
        LibraryPath note = fixture.Resolve(root, "a.md");
        Assert.Equal(NotePropertyKind.Date, types.For(note)["pages"]);

        // Same length on purpose: only the last-write time tells the two files apart.
        WriteTypes(root, """{ "types": { "pages": "text" } }""");
        File.SetLastWriteTimeUtc(TypesPath(root), DateTime.UtcNow.AddMinutes(5));

        Assert.Equal(NotePropertyKind.Text, types.For(note)["pages"]);
    }

    /// <summary>FP-T18: deleting the types file drops its types at the next read.</summary>
    [Fact]
    public void For_FileDeleted_NextReadHasNoTypes()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string root = NewRoot(fixture);
        WriteTypes(root, """{ "types": { "pages": "number" } }""");
        fixture.Reload();
        PropertyTypes types = Create(fixture);
        LibraryPath note = fixture.Resolve(root, "a.md");
        Assert.NotEmpty(types.For(note));

        File.Delete(TypesPath(root));

        Assert.Empty(types.For(note));
    }

    /// <summary>FP-T17: the settings folder stays out of the Library tree and out of the link index, while its types file is still read.</summary>
    [Fact]
    public async Task SettingsFolder_StaysHidden_ButTheTypesFileIsRead()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string root = NewRoot(fixture);
        WriteTypes(root, """{ "types": { "pages": "number" } }""");
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(TypesPath(root)) ?? root, "other.md"), "# Other\n");
        fixture.Reload();
        LibraryPath rootPath = fixture.Resolve(root, string.Empty);

        IReadOnlyList<LibraryEntry> listing = await fixture.CreateService().ListAsync(rootPath, ct);
        IReadOnlyList<string> indexed = fixture.Index.FilesUnder(rootPath.Root.Id, string.Empty);

        Assert.DoesNotContain(listing, static entry => entry.Path.RelativePath.StartsWith(LibraryOptions.DefaultPropertyTypesFile.Split('/')[0], StringComparison.Ordinal));
        Assert.Equal(["a.md"], indexed);
        Assert.Equal(NotePropertyKind.Number, Create(fixture).For(fixture.Resolve(root, "a.md"))["pages"]);
    }

    /// <summary>Fix 4: a read that fails is not cached, so the next draw retries and the types then apply.</summary>
    [Fact]
    public void For_FailedRead_IsNotCached_NextReadRetries()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "An exclusive lock is only enforced across handles on Windows.");
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string root = NewRoot(fixture);
        WriteTypes(root, """{ "types": { "pages": "number" } }""");
        fixture.Reload();
        PropertyTypes types = Create(fixture);
        LibraryPath note = fixture.Resolve(root, "a.md");

        using (FileStream held = new(TypesPath(root), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Assert.Empty(types.For(note));
            Assert.True(held.CanRead);
        }

        Assert.Equal(NotePropertyKind.Number, types.For(note)["pages"]);
    }
}