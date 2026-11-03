using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.Tests.Waiting;

namespace Agency.Huddle.Tests.Prompts;

/// <summary>
/// Pins SB-R17 (Spec 7.5): <see cref="PromptStore"/> reads and writes one <c>&lt;key&gt;.md</c> file per
/// override under <c>{DataDir}/Prompts</c>. Rows are deterministic by constructing a NEW store to observe
/// the folder, because the watcher's debounce is a fixed real 500 ms timer; exactly one row waits for it.
/// </summary>
public sealed class PromptFolderStoreTests
{
    // The key and file used throughout: a catalog key (PromptCatalog) whose override file is "<key>.md".
    private const string Key = "getHelp.intro";
    private const string OtherKey = "getHelp.budget";

    /// <summary>A file named <c>&lt;key&gt;.md</c> overrides that key and leaves the others at their defaults.</summary>
    [Fact]
    public void Constructor_FileNamedAfterKey_OverridesThatKey()
    {
        using TempDataDir dataDir = new();
        WriteFile(dataDir.Path, Key + ".md", "From the folder.");

        PromptStore store = NewStore(dataDir);

        Assert.Equal("From the folder.", store.Raw(Key));
        Assert.Equal(PromptCatalog.Get(OtherKey).Default, store.Raw(OtherKey));
    }

    /// <summary><see cref="PromptStore.Save"/> writes exactly the text (UTF-8, no BOM, no added newline) to <c>&lt;key&gt;.md</c>, and a new store reads it back.</summary>
    [Fact]
    public void Save_WritesExactlyTheTextToTheKeyFile()
    {
        using TempDataDir dataDir = new();
        PromptStore store = NewStore(dataDir);
        const string text = "Line one\nLine two {{roomName}}"; // no trailing whitespace: reads trim it

        store.Save(Key, text);

        byte[] bytes = File.ReadAllBytes(Path.Combine(PromptsDir(dataDir.Path), Key + ".md"));
        Assert.Equal(new UTF8Encoding(false).GetBytes(text), bytes);
        Assert.Equal(text, store.Raw(Key));
        Assert.Equal(text, NewStore(dataDir).Raw(Key));
    }

    /// <summary>Saving the catalog's default text deletes the override file instead of storing a copy.</summary>
    [Fact]
    public void Save_DefaultText_DeletesTheFile()
    {
        using TempDataDir dataDir = new();
        PromptStore store = NewStore(dataDir);
        store.Save(Key, "An override.");
        string file = Path.Combine(PromptsDir(dataDir.Path), Key + ".md");
        Assert.True(File.Exists(file));

        store.Save(Key, PromptCatalog.Get(Key).Default);

        Assert.False(File.Exists(file));
        Assert.Equal(PromptCatalog.Get(Key).Default, NewStore(dataDir).Raw(Key));
    }

    /// <summary><see cref="PromptStore.Reset"/> deletes the key's file, so a new store resolves the default.</summary>
    [Fact]
    public void Reset_DeletesTheFile()
    {
        using TempDataDir dataDir = new();
        PromptStore store = NewStore(dataDir);
        store.Save(Key, "An override.");

        store.Reset(Key);

        Assert.False(File.Exists(Path.Combine(PromptsDir(dataDir.Path), Key + ".md")));
        Assert.Equal(PromptCatalog.Get(Key).Default, NewStore(dataDir).Raw(Key));
    }

    /// <summary>A file deleted behind the store's back means the default for a store constructed afterwards.</summary>
    [Fact]
    public void Constructor_FileDeletedAfterSave_ResolvesDefault()
    {
        using TempDataDir dataDir = new();
        NewStore(dataDir).Save(Key, "An override.");

        File.Delete(Path.Combine(PromptsDir(dataDir.Path), Key + ".md"));

        Assert.Equal(PromptCatalog.Get(Key).Default, NewStore(dataDir).Raw(Key));
    }

    /// <summary><see cref="PromptStore.SaveMany"/> writes one file per key and raises <see cref="PromptStore.PromptsChanged"/> once.</summary>
    [Fact]
    public void SaveMany_SeveralKeys_WritesEachFileAndRaisesOnce()
    {
        using TempDataDir dataDir = new();
        PromptStore store = NewStore(dataDir);
        int raised = 0;
        store.PromptsChanged += () => raised++;

        store.SaveMany(new Dictionary<string, string>
        {
            [Key] = "First override.",
            [OtherKey] = "Second override.",
        });

        Assert.Equal(1, raised);
        Assert.Equal("First override.", File.ReadAllText(Path.Combine(PromptsDir(dataDir.Path), Key + ".md")));
        Assert.Equal("Second override.", File.ReadAllText(Path.Combine(PromptsDir(dataDir.Path), OtherKey + ".md")));
        Assert.Equal("Second override.", store.Raw(OtherKey));
    }

    /// <summary>A file whose name differs from the key only by case, and an upper-case <c>.MD</c> extension, still map to the key.</summary>
    [Fact]
    public void Constructor_FileNameInOtherCase_MapsToTheKey()
    {
        using TempDataDir dataDir = new();
        WriteFile(dataDir.Path, Key.ToUpperInvariant() + ".md", "Upper-case name.");
        WriteFile(dataDir.Path, OtherKey + ".MD", "Upper-case extension.");

        PromptStore store = NewStore(dataDir);

        Assert.Equal("Upper-case name.", store.Raw(Key));
        Assert.Equal("Upper-case extension.", store.Raw(OtherKey));
    }

    /// <summary>A non-<c>.md</c> file and an <c>AtomicFile</c> temp (<c>x.md.&lt;guid&gt;.tmp</c>) are ignored.</summary>
    [Fact]
    public void Constructor_NonMarkdownAndTempFiles_AreIgnored()
    {
        using TempDataDir dataDir = new();
        WriteFile(dataDir.Path, Key + ".txt", "Not markdown.");
        WriteFile(dataDir.Path, OtherKey + ".md." + Guid.NewGuid().ToString("N") + ".tmp", "Half written.");

        PromptStore store = NewStore(dataDir);

        Assert.Equal(PromptCatalog.Get(Key).Default, store.Raw(Key));
        Assert.Equal(PromptCatalog.Get(OtherKey).Default, store.Raw(OtherKey));
    }

    /// <summary>Trailing whitespace in a file is trimmed on read (Spec 7.5).</summary>
    [Fact]
    public void Raw_FileWithTrailingWhitespace_ReturnsTrimmedText()
    {
        using TempDataDir dataDir = new();
        WriteFile(dataDir.Path, Key + ".md", "Body text.\n\n   \n");

        PromptStore store = NewStore(dataDir);

        Assert.Equal("Body text.", store.Raw(Key));
    }

    /// <summary>A file with a UTF-8 BOM and CRLF line endings reads as LF without the BOM.</summary>
    [Fact]
    public void Raw_FileWithBomAndCrlf_ReadsAsLfWithoutBom()
    {
        using TempDataDir dataDir = new();
        Directory.CreateDirectory(PromptsDir(dataDir.Path));
        File.WriteAllText(Path.Combine(PromptsDir(dataDir.Path), Key + ".md"), "Line one\r\nLine two", new UTF8Encoding(true));

        PromptStore store = NewStore(dataDir);

        Assert.Equal("Line one\nLine two", store.Raw(Key));
    }

    /// <summary>A <c>prompts.json</c> in the data directory is no longer read (Spec 7.5; no migration).</summary>
    [Fact]
    public void Constructor_PromptsJsonInDataDir_IsIgnored()
    {
        using TempDataDir dataDir = new();
        File.WriteAllText(Path.Combine(dataDir.Path, "prompts.json"), """{ "getHelp.intro": "From the retired file." }""");

        PromptStore store = NewStore(dataDir);

        Assert.Equal(PromptCatalog.Get(Key).Default, store.Raw(Key));
    }

    /// <summary>The one watcher row: a file written externally into the folder is picked up.</summary>
    [Fact]
    public async Task Raw_FileWrittenExternally_IsPickedUp()
    {
        using TempDataDir dataDir = new();
        using PromptStore store = NewStore(dataDir);

        WriteFile(dataDir.Path, Key + ".md", "Written by hand.");

        await Eventually.UntilAsync(() => string.Equals(store.Raw(Key), "Written by hand.", StringComparison.Ordinal), TestContext.Current.CancellationToken, timeout: TestBudgets.Slow, interval: TimeSpan.FromMilliseconds(50));
        Assert.Equal("Written by hand.", store.Raw(Key));
    }

    /// <summary>Builds a store over <paramref name="dataDir"/> with the default Prompts folder.</summary>
    /// <param name="dataDir">The temp data directory.</param>
    private static PromptStore NewStore(TempDataDir dataDir) => new(dataDir.Options(), NullLogger<PromptStore>.Instance);

    /// <summary>The Prompts folder under <paramref name="dataDir"/>.</summary>
    /// <param name="dataDir">The temp data directory path.</param>
    private static string PromptsDir(string dataDir) => Path.Combine(dataDir, "Prompts");

    /// <summary>Writes <paramref name="text"/> to <paramref name="fileName"/> in the Prompts folder, creating the folder.</summary>
    /// <param name="dataDir">The temp data directory path.</param>
    /// <param name="fileName">The file name inside the folder.</param>
    /// <param name="text">The file content.</param>
    private static void WriteFile(string dataDir, string fileName, string text)
    {
        Directory.CreateDirectory(PromptsDir(dataDir));
        File.WriteAllText(Path.Combine(PromptsDir(dataDir), fileName), text);
    }
}
