using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Memory;

namespace Agency.Huddle.Tests.Memory;

/// <summary>
/// Pins each result of <see cref="MemoryWriter.WriteAsync"/>, the one write path the <c>memory</c> tool's <c>write</c> and the Team
/// Memory Helper share (Team Memory Helper spec §6.6). The tool's own texts for these results are pinned by <c>MemoryToolTests</c>.
/// </summary>
public sealed class MemoryWriterTests : IDisposable
{
    private const string Description = "Interview recording rules; read before scheduling an interview";

    private readonly TempDataDir dir = new();
    private readonly MemoryFolderGate gate = new();
    private readonly IOptions<TeamOptions> options;
    private readonly MemoryWriter writer;
    private readonly MemoryFolder folder;

    /// <summary>Builds a writer over a temp folder.</summary>
    public MemoryWriterTests()
    {
        this.options = this.dir.Options(static team => team.Memory.MaxFileBytes = 400);
        this.writer = new MemoryWriter(this.gate, this.options);
        this.folder = new MemoryFolder(Path.Combine(this.dir.Path, "memory"), "me", null);
    }

    /// <summary>A new file is written with its frontmatter, and the result names the path, the version and that it was created.</summary>
    [Fact]
    public async Task WriteAsync_NewFileWithoutVersion_CreatesIt()
    {
        MemoryWriteResult result = await this.writer.WriteAsync(this.folder, "consent.md", Description, "Written consent only.", null, TestContext.Current.CancellationToken);

        MemoryWriteResult.Written written = Assert.IsType<MemoryWriteResult.Written>(result);
        string path = Path.Combine(this.folder.Path, "consent.md");
        Assert.Equal(path, written.Path);
        Assert.True(written.Created);
        byte[] bytes = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);
        Assert.Equal(MemoryVersion.Of(bytes), written.Version);
        Assert.Equal(MemoryFileRules.Compose(Description, "Written consent only."), await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
    }

    /// <summary>An existing file with the version the writer read is replaced, and the result says it was not created.</summary>
    [Fact]
    public async Task WriteAsync_ExistingFileWithItsVersion_ReplacesIt()
    {
        MemoryWriteResult.Written first = Assert.IsType<MemoryWriteResult.Written>(
            await this.writer.WriteAsync(this.folder, "consent.md", Description, "Written consent only.", null, TestContext.Current.CancellationToken));

        MemoryWriteResult result = await this.writer.WriteAsync(this.folder, "consent.md", Description, "Recorded verbal consent is enough.", first.Version, TestContext.Current.CancellationToken);

        Assert.False(Assert.IsType<MemoryWriteResult.Written>(result).Created);
        Assert.Equal(MemoryFileRules.Compose(Description, "Recorded verbal consent is enough."), await File.ReadAllTextAsync(first.Path, TestContext.Current.CancellationToken));
    }

    /// <summary>No version for a file that exists is <see cref="MemoryWriteResult.Exists"/>, and the file is left as it was.</summary>
    [Fact]
    public async Task WriteAsync_ExistingFileWithoutVersion_IsExistsAndWritesNothing()
    {
        _ = await this.writer.WriteAsync(this.folder, "consent.md", Description, "Original.", null, TestContext.Current.CancellationToken);

        MemoryWriteResult result = await this.writer.WriteAsync(this.folder, "consent.md", Description, "Overwrite.", null, TestContext.Current.CancellationToken);

        Assert.IsType<MemoryWriteResult.Exists>(result);
        Assert.Equal(MemoryFileRules.Compose(Description, "Original."), await File.ReadAllTextAsync(Path.Combine(this.folder.Path, "consent.md"), TestContext.Current.CancellationToken));
    }

    /// <summary>A version that no longer matches is <see cref="MemoryWriteResult.Stale"/>, carrying the current text and version.</summary>
    [Fact]
    public async Task WriteAsync_OutOfDateVersion_IsStaleWithTheCurrentText()
    {
        MemoryWriteResult.Written first = Assert.IsType<MemoryWriteResult.Written>(
            await this.writer.WriteAsync(this.folder, "consent.md", Description, "Original.", null, TestContext.Current.CancellationToken));
        await File.WriteAllTextAsync(first.Path, "Changed by the Human.\n", TestContext.Current.CancellationToken);

        MemoryWriteResult result = await this.writer.WriteAsync(this.folder, "consent.md", Description, "Mine.", first.Version, TestContext.Current.CancellationToken);

        MemoryWriteResult.Stale stale = Assert.IsType<MemoryWriteResult.Stale>(result);
        Assert.Equal("Changed by the Human.\n", stale.CurrentText);
        Assert.Equal(MemoryVersion.Of("Changed by the Human.\n"u8.ToArray()), stale.CurrentVersion);
    }

    /// <summary>A version for a file that is gone is <see cref="MemoryWriteResult.Missing"/>, and nothing is created.</summary>
    [Fact]
    public async Task WriteAsync_VersionForMissingFile_IsMissing()
    {
        MemoryWriteResult result = await this.writer.WriteAsync(this.folder, "gone.md", Description, "Text.", "abcdef012345", TestContext.Current.CancellationToken);

        Assert.IsType<MemoryWriteResult.Missing>(result);
        Assert.False(File.Exists(Path.Combine(this.folder.Path, "gone.md")));
    }

    /// <summary>A name that is not one memory file name, the reserved index name included, is <see cref="MemoryWriteResult.BadName"/>.</summary>
    /// <param name="file">The name.</param>
    [Theory]
    [InlineData("../escape.md")]
    [InlineData("notes.txt")]
    [InlineData("MEMORY.md")]
    public async Task WriteAsync_BadName_IsBadName(string file)
    {
        MemoryWriteResult result = await this.writer.WriteAsync(this.folder, file, Description, "Text.", null, TestContext.Current.CancellationToken);

        Assert.IsType<MemoryWriteResult.BadName>(result);
    }

    /// <summary>A description that breaks a rule is <see cref="MemoryWriteResult.BadDescription"/> with the rule's problem.</summary>
    [Fact]
    public async Task WriteAsync_ShortDescription_IsBadDescription()
    {
        MemoryWriteResult result = await this.writer.WriteAsync(this.folder, "consent.md", "Too short", "Text.", null, TestContext.Current.CancellationToken);

        Assert.Equal("it is shorter than 20 characters.", Assert.IsType<MemoryWriteResult.BadDescription>(result).Problem);
    }

    /// <summary>A file over the cap is <see cref="MemoryWriteResult.TooLarge"/> with its size and the cap, and is not written.</summary>
    [Fact]
    public async Task WriteAsync_OverTheCap_IsTooLarge()
    {
        MemoryWriteResult result = await this.writer.WriteAsync(this.folder, "big.md", Description, new string('x', 500), null, TestContext.Current.CancellationToken);

        MemoryWriteResult.TooLarge tooLarge = Assert.IsType<MemoryWriteResult.TooLarge>(result);
        Assert.Equal(400, tooLarge.Max);
        Assert.True(tooLarge.Size > 400);
        Assert.False(File.Exists(Path.Combine(this.folder.Path, "big.md")));
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.gate.Dispose();
        this.dir.Dispose();
    }
}
