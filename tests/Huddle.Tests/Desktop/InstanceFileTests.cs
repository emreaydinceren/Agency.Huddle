using System.Text.Json;
using Agency.Huddle.App.Desktop;

namespace Agency.Huddle.Tests.Desktop;

/// <summary>Pins the persistence of <see cref="InstanceInfo"/> by <see cref="InstanceFile"/>, including corruption and torn writes.</summary>
public sealed class InstanceFileTests
{
    /// <summary>Writing a record and reading it back returns every field unchanged.</summary>
    [Fact]
    public void Write_ThenTryRead_RoundTrips()
    {
        using TempDataDir dir = new();
        InstanceInfo info = new("http://127.0.0.1:5123", "token-abc", 4242);

        InstanceFile.Write(dir.Path, info);
        InstanceInfo? read = InstanceFile.TryRead(dir.Path);

        Assert.NotNull(read);
        Assert.Equal("http://127.0.0.1:5123", read.Url);
        Assert.Equal("token-abc", read.StopToken);
        Assert.Equal(4242, read.Pid);
    }

    /// <summary>The file is camelCase JSON with exactly the keys <c>url</c>, <c>stopToken</c> and <c>pid</c>.</summary>
    [Fact]
    public void Write_ProducesCamelCaseJsonKeys()
    {
        using TempDataDir dir = new();

        InstanceFile.Write(dir.Path, new InstanceInfo("http://localhost:1", "t", 7));
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir.Path, "instance.json")));
        string[] keys = [.. document.RootElement.EnumerateObject().Select(static property => property.Name)];

        Assert.Equal(["url", "stopToken", "pid"], keys);
        Assert.Equal("http://localhost:1", document.RootElement.GetProperty("url").GetString());
        Assert.Equal("t", document.RootElement.GetProperty("stopToken").GetString());
        Assert.Equal(7, document.RootElement.GetProperty("pid").GetInt32());
    }

    /// <summary>Writing into a directory that does not exist creates it and leaves no temporary file behind.</summary>
    [Fact]
    public void Write_MissingDirectory_CreatesItAndLeavesNoTempFile()
    {
        using TempDataDir dir = new();
        string nested = Path.Combine(dir.Path, "nested", "data");

        InstanceFile.Write(nested, new InstanceInfo("http://localhost:1", "t", 7));

        Assert.True(File.Exists(Path.Combine(nested, "instance.json")));
        Assert.False(File.Exists(Path.Combine(nested, "instance.json.tmp")));
    }

    /// <summary>On Unix the published file is readable and writable by its owner only (0600) and a created data directory is 0700; Windows has no mode to check.</summary>
    [Fact]
    public void Write_OnUnix_FileIsOwnerOnlyAndCreatedDirectoryIsPrivate()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.True(OperatingSystem.IsWindows());
            return;
        }

        using TempDataDir dir = new();
        string nested = Path.Combine(dir.Path, "private-data");

        InstanceFile.Write(nested, new InstanceInfo("http://localhost:1", "t", 7));

        Assert.Equal(
            UnixFileMode.UserRead | UnixFileMode.UserWrite,
            File.GetUnixFileMode(Path.Combine(nested, "instance.json")));
        Assert.Equal(
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
            File.GetUnixFileMode(nested));
    }

    /// <summary>With no file in the directory, <see cref="InstanceFile.TryRead"/> returns null.</summary>
    [Fact]
    public void TryRead_NoFile_ReturnsNull()
    {
        using TempDataDir dir = new();

        Assert.Null(InstanceFile.TryRead(dir.Path));
    }

    /// <summary>Content that is empty, malformed, the wrong shape, or has a missing or invalid field reads as null.</summary>
    /// <param name="content">The raw file content.</param>
    [Theory]
    [InlineData("")]
    [InlineData("{")]
    [InlineData("[]")]
    [InlineData("""{"url":"x"}""")]
    [InlineData("""{"url":"x","stopToken":"y"}""")]
    [InlineData("""{"url":"x","stopToken":"y","pid":0}""")]
    [InlineData("""{"url":"","stopToken":"y","pid":5}""")]
    [InlineData("""{"url":"x","stopToken":"","pid":5}""")]
    [InlineData("""{"url":"  ","stopToken":"y","pid":5}""")]
    [InlineData("""{"url":"x","stopToken":"y","pid":-3}""")]
    [InlineData("""{"url":null,"stopToken":"y","pid":5}""")]
    public void TryRead_InvalidContent_ReturnsNull(string content)
    {
        using TempDataDir dir = new();
        File.WriteAllText(Path.Combine(dir.Path, "instance.json"), content);

        Assert.Null(InstanceFile.TryRead(dir.Path));
    }

    /// <summary>A complete record written by hand, without the writer, reads back.</summary>
    [Fact]
    public void TryRead_CompleteHandWrittenRecord_ReturnsIt()
    {
        using TempDataDir dir = new();
        File.WriteAllText(Path.Combine(dir.Path, "instance.json"), """{"url":"x","stopToken":"y","pid":5}""");

        InstanceInfo? read = InstanceFile.TryRead(dir.Path);

        Assert.Equal(new InstanceInfo("x", "y", 5), read);
    }

    /// <summary>Delete removes the record, and a second Delete (and one with no directory at all) does not throw.</summary>
    [Fact]
    public void Delete_RemovesFile_AndIsIdempotent()
    {
        using TempDataDir dir = new();
        InstanceFile.Write(dir.Path, new InstanceInfo("http://localhost:1", "t", 7));

        InstanceFile.Delete(dir.Path);
        Exception? second = Record.Exception(() => InstanceFile.Delete(dir.Path));
        Exception? noDirectory = Record.Exception(() => InstanceFile.Delete(Path.Combine(dir.Path, "missing")));

        Assert.False(File.Exists(Path.Combine(dir.Path, "instance.json")));
        Assert.Null(InstanceFile.TryRead(dir.Path));
        Assert.Null(second);
        Assert.Null(noDirectory);
    }

    /// <summary>A second Write replaces the previous record completely.</summary>
    [Fact]
    public void Write_Twice_ReplacesPreviousRecord()
    {
        using TempDataDir dir = new();
        InstanceFile.Write(dir.Path, new InstanceInfo("http://localhost:1", "first", 1));

        InstanceFile.Write(dir.Path, new InstanceInfo("http://localhost:2", "second", 2));
        InstanceInfo? read = InstanceFile.TryRead(dir.Path);

        Assert.Equal(new InstanceInfo("http://localhost:2", "second", 2), read);
    }

    /// <summary>
    /// 300 writes on one task while another task reads in a loop: the writer never throws, the reader never
    /// throws, and every read is either null or a complete record that was written (never a torn one).
    /// </summary>
    /// <returns>A task that completes when the writer and the reader have finished.</returns>
    [Fact]
    public async Task Write_WhileReading_NeverThrowsAndNeverYieldsATornRecord()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        string path = dir.Path;
        const int iterations = 300;

        Task writer = Task.Run(
            () =>
            {
                for (int i = 1; i <= iterations; i++)
                {
                    InstanceFile.Write(path, new InstanceInfo($"http://127.0.0.1:{i}", $"token-{i}", i));
                }
            },
            ct);

        Task<List<string>> reader = Task.Run(
            () =>
            {
                List<string> problems = [];
                while (!writer.IsCompleted)
                {
                    // A throw from TryRead faults this task, and awaiting the task below fails the test.
                    InstanceInfo? read = InstanceFile.TryRead(path);
                    if (read is not null && !IsComplete(read))
                    {
                        problems.Add($"torn record: {read}");
                    }
                }

                return problems;
            },
            ct);

        await writer;
        List<string> readerProblems = await reader;

        Assert.Empty(readerProblems);
        Assert.Equal(new InstanceInfo($"http://127.0.0.1:{iterations}", $"token-{iterations}", iterations), InstanceFile.TryRead(path));
    }

    /// <summary>Whether all three fields of <paramref name="info"/> came from the same write.</summary>
    /// <param name="info">The record to check.</param>
    /// <returns><see langword="true"/> when the url, token and pid carry the same iteration number.</returns>
    private static bool IsComplete(InstanceInfo info)
    {
        return string.Equals(info.Url, $"http://127.0.0.1:{info.Pid}", StringComparison.Ordinal)
            && string.Equals(info.StopToken, $"token-{info.Pid}", StringComparison.Ordinal);
    }
}
