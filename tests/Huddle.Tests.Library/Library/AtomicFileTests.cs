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
}
