using System.Text;
using Agency.Huddle.App.Library;

namespace Agency.Huddle.Tests.Library;

/// <summary>Tests for the synchronous, throwing overloads of <see cref="AtomicFile"/>: <c>WriteText</c> and <c>WriteBytes</c>.</summary>
public sealed class AtomicFileTests
{
    /// <summary>The text lands in the file exactly, as UTF-8 with no byte-order mark, the same bytes <see cref="File.WriteAllText(string, string)"/> would write.</summary>
    [Fact]
    public void WriteText_NewFile_WritesUtf8WithoutBom()
    {
        using TempDataDir dir = new();
        string path = Path.Combine(dir.Path, "doc.json");
        const string Text = "café ✓ line\r\nnext";

        AtomicFile.WriteText(path, Text);

        byte[] expected = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(Text);
        Assert.Equal(expected, File.ReadAllBytes(path));
        Assert.NotEqual(0xEF, File.ReadAllBytes(path)[0]);
    }

    /// <summary>An existing file is replaced wholesale, and the folder holds nothing but the target afterwards.</summary>
    [Fact]
    public void WriteText_ExistingFile_OverwritesAndLeavesNoTemp()
    {
        using TempDataDir dir = new();
        string path = Path.Combine(dir.Path, "doc.json");
        File.WriteAllText(path, "old and much longer content");

        AtomicFile.WriteText(path, "new");

        Assert.Equal("new", File.ReadAllText(path));
        Assert.Equal([path], Directory.GetFileSystemEntries(dir.Path));
    }

    /// <summary>When the move is refused the call throws an IO error and deletes its temp file instead of leaving a <c>.tmp</c> behind.</summary>
    [Fact]
    public void WriteText_MoveRefused_ThrowsAndLeavesNoTemp()
    {
        using TempDataDir dir = new();
        string target = Path.Combine(dir.Path, "doc.json");
        Directory.CreateDirectory(target);

        Exception? thrown = Record.Exception(() => AtomicFile.WriteText(target, "content"));

        Assert.True(thrown is IOException or UnauthorizedAccessException, $"Unexpected: {thrown?.GetType().Name ?? "no exception"}");
        Assert.Empty(Directory.GetFiles(dir.Path));
        Assert.True(Directory.Exists(target));
    }

    /// <summary>
    /// A target another process holds open for a moment (an indexer, antivirus, an editor reading it) no longer
    /// refuses the save: the move is retried, so a sharing violation released after 50 ms still writes the file.
    /// </summary>
    [Fact(Timeout = 10000)]
    public async Task TryWriteAsync_TargetHeldOpenBriefly_RetriesAndWrites()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        string path = Path.Combine(dir.Path, "doc.md");
        File.WriteAllText(path, "old");

        using FileStream reader = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        ReleaseShortly(reader);

        bool written = await AtomicFile.TryWriteAsync(path, Encoding.UTF8.GetBytes("new"), ct);

        Assert.True(written);
        Assert.Equal("new", File.ReadAllText(path));
        Assert.Equal([path], Directory.GetFileSystemEntries(dir.Path));
    }

    /// <summary>The synchronous overload retries a briefly held target the same way.</summary>
    [Fact(Timeout = 10000)]
    public async Task WriteBytes_TargetHeldOpenBriefly_RetriesAndWrites()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        string path = Path.Combine(dir.Path, "doc.json");
        File.WriteAllText(path, "old");

        using FileStream reader = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        ReleaseShortly(reader);

        await Task.Run(() => AtomicFile.WriteBytes(path, "new"u8.ToArray()), ct);

        Assert.Equal("new", File.ReadAllText(path));
        Assert.Equal([path], Directory.GetFileSystemEntries(dir.Path));
    }

    /// <summary>A target held open for the whole call is still refused, after the bounded retries, and leaves no temp file.</summary>
    [Fact(Timeout = 10000)]
    public async Task TryWriteAsync_TargetHeldOpenThroughout_RefusedAndLeavesNoTemp()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Only Windows refuses a replace over a file held open without FileShare.Delete.");
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        string path = Path.Combine(dir.Path, "doc.md");
        File.WriteAllText(path, "old");

        using FileStream reader = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        bool written = await AtomicFile.TryWriteAsync(path, Encoding.UTF8.GetBytes("new"), ct);

        Assert.False(written);
        Assert.Equal([path], Directory.GetFileSystemEntries(dir.Path));
    }

    /// <summary>The bytes overload writes its input verbatim (a byte-order mark is not stripped or added) over an existing file.</summary>
    [Fact]
    public void WriteBytes_ExistingFile_OverwritesWithExactBytes()
    {
        using TempDataDir dir = new();
        string path = Path.Combine(dir.Path, "blob.bin");
        byte[] bytes = [0xEF, 0xBB, 0xBF, 0x00, 0xFF];
        File.WriteAllText(path, "old");

        AtomicFile.WriteBytes(path, bytes);

        Assert.Equal(bytes, File.ReadAllBytes(path));
        Assert.Equal([path], Directory.GetFileSystemEntries(dir.Path));
    }

    /// <summary>
    /// Lets go of the file after 50 ms on a thread of its own. An <c>await</c> in the test would resume through the test framework's
    /// limited scheduler, and under load that outlasted the writer's whole retry budget, so a healthy retry looked refused.
    /// </summary>
    /// <param name="reader">The stream holding the file open.</param>
    private static void ReleaseShortly(FileStream reader)
    {
        Thread release = new(() =>
        {
            Thread.Sleep(50);
            reader.Dispose();
        })
        {
            IsBackground = true,
        };
        release.Start();
    }
}
