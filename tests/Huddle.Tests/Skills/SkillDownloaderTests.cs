using System.IO.Compression;
using System.Net;
using Agency.Huddle.App.Skills;
using Agency.Huddle.Tests.Desktop;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.Skills;

/// <summary>Tests for <see cref="SkillDownloader"/>; no test touches the network.</summary>
public sealed class SkillDownloaderTests
{
    private const string Commit = "0123456789abcdef0123456789abcdef01234567";

    private const string Root = "repo-" + Commit + "/";

    /// <summary>Builds a zip in memory from entry names and contents.</summary>
    /// <param name="entries">The entries to add.</param>
    /// <param name="symlinkName">The name of an entry to mark as a symbolic link, if any.</param>
    /// <returns>The zip bytes.</returns>
    private static byte[] BuildZip(IEnumerable<(string Name, byte[] Content)> entries, string? symlinkName = null)
    {
        using MemoryStream stream = new();
        using (ZipArchive archive = new(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach ((string name, byte[] content) in entries)
            {
                ZipArchiveEntry entry = archive.CreateEntry(name);
                if (string.Equals(name, symlinkName, StringComparison.Ordinal))
                {
                    entry.ExternalAttributes = unchecked((int)0xA1FF0000);
                }

                using Stream entryStream = entry.Open();
                entryStream.Write(content);
            }
        }

        return stream.ToArray();
    }

    /// <summary>Encodes text as UTF-8.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The bytes.</returns>
    private static byte[] Text(string text) => System.Text.Encoding.UTF8.GetBytes(text);

    /// <summary>Creates a downloader whose handler answers with the given response factory.</summary>
    /// <param name="respond">Produces each response.</param>
    /// <returns>The downloader and its handler.</returns>
    private static (SkillDownloader Downloader, FakeHttpHandler Handler) Create(Func<HttpResponseMessage> respond)
    {
        FakeHttpHandler handler = new((_, _) => respond());
        SkillDownloader downloader = new(new HttpClient(handler), NullLogger<SkillDownloader>.Instance);
        return (downloader, handler);
    }

    /// <summary>Wraps zip bytes in a 200 response.</summary>
    /// <param name="zip">The body.</param>
    /// <returns>The response.</returns>
    private static HttpResponseMessage Ok(byte[] zip) => new(HttpStatusCode.OK) { Content = new ByteArrayContent(zip) };

    /// <summary>The folder at the commit is returned with paths relative to the skill folder.</summary>
    [Fact]
    public async Task DownloadAsync_ValidArchive_ReturnsFolderFilesRelativeToPath()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        byte[] zip = BuildZip(
        [
            (Root + "README.md", Text("readme")),
            (Root + "skills/", []),
            (Root + "skills/pdf/SKILL.md", Text("skill")),
            (Root + "skills/pdf/scripts/run.py", Text("print()")),
            (Root + "skills/pdf2/SKILL.md", Text("other")),
        ]);
        (SkillDownloader downloader, FakeHttpHandler handler) = Create(() => Ok(zip));

        DownloadResult result = await downloader.DownloadAsync("https://github.com/o/repo", "skills/pdf", Commit, ct);

        Assert.Null(result.Error);
        Assert.NotNull(result.Skill);
        Assert.Equal(["SKILL.md", "scripts/run.py"], result.Skill.Files.Keys.Order(StringComparer.Ordinal).ToArray());
        Assert.Equal("skill", System.Text.Encoding.UTF8.GetString(result.Skill.Files["SKILL.md"]));
        Assert.Equal("https://codeload.github.com/o/repo/zip/" + Commit, handler.Requests.Single().Uri?.ToString());
    }

    /// <summary>A path with no entries gives the plain not-found error.</summary>
    [Fact]
    public async Task DownloadAsync_PathMissing_ReturnsNotFound()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        byte[] zip = BuildZip([(Root + "a/SKILL.md", Text("x"))]);
        (SkillDownloader downloader, _) = Create(() => Ok(zip));

        DownloadResult result = await downloader.DownloadAsync("https://github.com/o/repo", "nope", Commit, ct);

        Assert.Null(result.Skill);
        Assert.Equal("Path not found at that commit.", result.Error);
    }

    /// <summary>An entry that climbs out of the folder with dot-dot is rejected.</summary>
    [Fact]
    public async Task DownloadAsync_ZipSlipEntry_IsRejected()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        byte[] zip = BuildZip([(Root + "a/SKILL.md", Text("x")), (Root + "a/../../evil.txt", Text("x"))]);
        (SkillDownloader downloader, _) = Create(() => Ok(zip));

        DownloadResult result = await downloader.DownloadAsync("https://github.com/o/repo", "a", Commit, ct);

        Assert.Null(result.Skill);
        Assert.Equal("The archive contains an unsafe path.", result.Error);
    }

    /// <summary>A rooted entry name is rejected.</summary>
    [Fact]
    public async Task DownloadAsync_RootedEntry_IsRejected()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        byte[] zip = BuildZip([(Root + "a/SKILL.md", Text("x")), ("/etc/passwd", Text("x"))]);
        (SkillDownloader downloader, _) = Create(() => Ok(zip));

        DownloadResult result = await downloader.DownloadAsync("https://github.com/o/repo", "a", Commit, ct);

        Assert.Null(result.Skill);
        Assert.Equal("The archive contains an unsafe path.", result.Error);
    }

    /// <summary>A symlink entry in the selection is rejected.</summary>
    [Fact]
    public async Task DownloadAsync_SymlinkEntry_IsRejected()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        byte[] zip = BuildZip([(Root + "a/SKILL.md", Text("x")), (Root + "a/link", Text("/etc/passwd"))], Root + "a/link");
        (SkillDownloader downloader, _) = Create(() => Ok(zip));

        DownloadResult result = await downloader.DownloadAsync("https://github.com/o/repo", "a", Commit, ct);

        Assert.Null(result.Skill);
        Assert.Equal("The archive contains a symbolic link.", result.Error);
    }

    /// <summary>More than 5 MB of content is an error.</summary>
    [Fact]
    public async Task DownloadAsync_OverSize_IsRejected()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        byte[] big = new byte[3 * 1024 * 1024];
        byte[] zip = BuildZip([(Root + "a/one.bin", big), (Root + "a/two.bin", big)]);
        (SkillDownloader downloader, _) = Create(() => Ok(zip));

        DownloadResult result = await downloader.DownloadAsync("https://github.com/o/repo", "a", Commit, ct);

        Assert.Null(result.Skill);
        Assert.Equal("The skill folder is larger than 5 MB.", result.Error);
    }

    /// <summary>More than 200 files is an error.</summary>
    [Fact]
    public async Task DownloadAsync_TooManyFiles_IsRejected()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        byte[] zip = BuildZip(Enumerable.Range(0, 201).Select(static i => (Root + "a/f" + i + ".txt", Text("x"))));
        (SkillDownloader downloader, _) = Create(() => Ok(zip));

        DownloadResult result = await downloader.DownloadAsync("https://github.com/o/repo", "a", Commit, ct);

        Assert.Null(result.Skill);
        Assert.Equal("The skill folder has more than 200 files.", result.Error);
    }

    /// <summary>A URL that is not a plain GitHub repository is refused without any request.</summary>
    /// <param name="url">The repository URL.</param>
    [Theory]
    [InlineData("https://gitlab.com/o/repo")]
    [InlineData("http://github.com/o/repo")]
    [InlineData("https://github.com/o")]
    [InlineData("https://evil.example/github.com/o/repo")]
    [InlineData("not a url")]
    public async Task DownloadAsync_NonGitHubUrl_IsRejectedWithoutRequest(string url)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (SkillDownloader downloader, FakeHttpHandler handler) = Create(static () => new HttpResponseMessage(HttpStatusCode.OK));

        DownloadResult result = await downloader.DownloadAsync(url, "a", Commit, ct);

        Assert.Null(result.Skill);
        Assert.Equal("Only https://github.com/<owner>/<repo> URLs are supported.", result.Error);
        Assert.Empty(handler.Requests);
    }

    /// <summary>A commit that is not 40 hex characters is refused without any request.</summary>
    /// <param name="commit">The commit string.</param>
    [Theory]
    [InlineData("main")]
    [InlineData("0123456789abcdef")]
    [InlineData("zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz")]
    public async Task DownloadAsync_BadCommit_IsRejectedWithoutRequest(string commit)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (SkillDownloader downloader, FakeHttpHandler handler) = Create(static () => new HttpResponseMessage(HttpStatusCode.OK));

        DownloadResult result = await downloader.DownloadAsync("https://github.com/o/repo", "a", commit, ct);

        Assert.Null(result.Skill);
        Assert.Equal("The commit must be 40 hexadecimal characters.", result.Error);
        Assert.Empty(handler.Requests);
    }

    /// <summary>A dot-dot path is refused without any request.</summary>
    [Fact]
    public async Task DownloadAsync_PathWithDotDot_IsRejectedWithoutRequest()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (SkillDownloader downloader, FakeHttpHandler handler) = Create(static () => new HttpResponseMessage(HttpStatusCode.OK));

        DownloadResult result = await downloader.DownloadAsync("https://github.com/o/repo", "a/../b", Commit, ct);

        Assert.Null(result.Skill);
        Assert.Equal("The path is not valid.", result.Error);
        Assert.Empty(handler.Requests);
    }

    /// <summary>An HTTP 404 gives a plain error with the status code.</summary>
    [Fact]
    public async Task DownloadAsync_Http404_ReturnsPlainError()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (SkillDownloader downloader, _) = Create(static () => new HttpResponseMessage(HttpStatusCode.NotFound));

        DownloadResult result = await downloader.DownloadAsync("https://github.com/o/repo", "a", Commit, ct);

        Assert.Null(result.Skill);
        Assert.Equal("The download failed with HTTP status 404.", result.Error);
    }

    /// <summary>A transport failure is reported as an error, not thrown.</summary>
    [Fact]
    public async Task DownloadAsync_TransportFailure_ReturnsError()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeHttpHandler handler = FakeHttpHandler.Throwing(new HttpRequestException("boom"));
        SkillDownloader downloader = new(new HttpClient(handler), NullLogger<SkillDownloader>.Instance);

        DownloadResult result = await downloader.DownloadAsync("https://github.com/o/repo", "a", Commit, ct);

        Assert.Null(result.Skill);
        Assert.Equal("The download failed.", result.Error);
    }

    /// <summary>A body that is not a zip is reported as an error.</summary>
    [Fact]
    public async Task DownloadAsync_NotAZip_ReturnsError()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (SkillDownloader downloader, _) = Create(() => Ok(Text("this is not a zip")));

        DownloadResult result = await downloader.DownloadAsync("https://github.com/o/repo", "a", Commit, ct);

        Assert.Null(result.Skill);
        Assert.Equal("The download is not a valid zip archive.", result.Error);
    }

    /// <summary>An archive whose declared length is over the configured limit is refused before it is read.</summary>
    [Fact]
    public async Task DownloadAsync_DeclaredLengthOverLimit_IsRejected()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        byte[] zip = BuildZip([(Root + "a/one.bin", System.Security.Cryptography.RandomNumberGenerator.GetBytes(2 * 1024 * 1024))]);
        FakeHttpHandler handler = new((_, _) => Ok(zip));
        SkillDownloader downloader = new(new HttpClient(handler), NullLogger<SkillDownloader>.Instance, maxDownloadBytes: 1024 * 1024);

        DownloadResult result = await downloader.DownloadAsync("https://github.com/o/repo", "a", Commit, ct);

        Assert.Null(result.Skill);
        Assert.Equal("The download is larger than 1 MB.", result.Error);
    }

    /// <summary>A body with no declared length is cut off once it passes the configured limit.</summary>
    [Fact]
    public async Task DownloadAsync_UnknownLengthOverLimit_IsRejected()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        byte[] zip = BuildZip([(Root + "a/one.bin", System.Security.Cryptography.RandomNumberGenerator.GetBytes(2 * 1024 * 1024))]);
        FakeHttpHandler handler = new((_, _) => new HttpResponseMessage(HttpStatusCode.OK) { Content = new UnknownLengthContent(zip) });
        SkillDownloader downloader = new(new HttpClient(handler), NullLogger<SkillDownloader>.Instance, maxDownloadBytes: 1024 * 1024);

        DownloadResult result = await downloader.DownloadAsync("https://github.com/o/repo", "a", Commit, ct);

        Assert.Null(result.Skill);
        Assert.Equal("The download is larger than 1 MB.", result.Error);
    }

    /// <summary>HTTP content that streams its bytes without declaring a length.</summary>
    /// <param name="bytes">The body.</param>
    private sealed class UnknownLengthContent(byte[] bytes) : HttpContent
    {
        /// <inheritdoc />
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            return stream.WriteAsync(bytes, 0, bytes.Length);
        }

        /// <inheritdoc />
        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }

    /// <summary>An entry name Windows cannot store (reserved device name, forbidden character, trailing dot or space) is rejected.</summary>
    /// <param name="entryName">The entry below the selected folder.</param>
    [Theory]
    [InlineData("CON.txt")]
    [InlineData("nul")]
    [InlineData("bad<name.txt")]
    [InlineData("q?.md")]
    [InlineData("trail.")]
    [InlineData("space ")]
    public async Task DownloadAsync_WindowsUnsafeName_ReturnsError(string entryName)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        byte[] zip = BuildZip([(Root + "a/SKILL.md", Text("x")), (Root + "a/" + entryName, Text("x"))]);
        (SkillDownloader downloader, _) = Create(() => Ok(zip));

        DownloadResult result = await downloader.DownloadAsync("https://github.com/o/repo", "a", Commit, ct);

        Assert.Null(result.Skill);
        Assert.Equal("The archive contains a name that cannot be stored on Windows.", result.Error);
    }

    /// <summary>Two entries whose names differ only in case are rejected, because they would collide on a case-insensitive disk.</summary>
    [Fact]
    public async Task DownloadAsync_CaseInsensitiveDuplicate_ReturnsError()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        byte[] zip = BuildZip([(Root + "a/SKILL.md", Text("x")), (Root + "a/skill.md", Text("y"))]);
        (SkillDownloader downloader, _) = Create(() => Ok(zip));

        DownloadResult result = await downloader.DownloadAsync("https://github.com/o/repo", "a", Commit, ct);

        Assert.Null(result.Skill);
        Assert.Equal("The archive contains two files whose names differ only in case.", result.Error);
    }
}