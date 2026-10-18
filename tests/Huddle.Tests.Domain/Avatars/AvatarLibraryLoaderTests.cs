using Agency.Huddle.App;
using Agency.Huddle.App.Avatars;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.Tests.Avatars;

/// <summary>
/// Pins <see cref="AvatarLibraryLoader"/>: what it reads from <c>master.json</c>, which records it skips and
/// why, and how it hands one image back. Each test works in its own temporary library folder, removed on dispose.
/// </summary>
public sealed class AvatarLibraryLoaderTests : IDisposable
{
    private static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4];

    private static readonly byte[] JpegBytes = [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3, 4];

    private static readonly byte[] WebpBytes = [0x52, 0x49, 0x46, 0x46, 0, 0, 0, 0, 0x57, 0x45, 0x42, 0x50, 1, 2];

    private readonly string root = Path.Combine(Path.GetTempPath(), "huddle-avatar-loader-" + Guid.NewGuid().ToString("N"));

    private readonly CapturingLogger logger = new();

    /// <summary>Creates the per-test library folder.</summary>
    public AvatarLibraryLoaderTests()
    {
        Directory.CreateDirectory(this.root);
    }

    /// <summary>Removes the per-test folder and everything under it.</summary>
    public void Dispose()
    {
        if (Directory.Exists(this.root))
        {
            Directory.Delete(this.root, recursive: true);
        }
    }

    /// <summary>With no library folder configured the load is unreadable, with no catalogue.</summary>
    [Fact]
    public async Task Load_NotConfigured_ReturnsUnreadable()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        AvatarLibraryLoader loader = this.CreateLoader(null);

        AvatarLibraryLoadResult result = await loader.LoadAsync(ct);

        Assert.Null(result.Catalog);
        Assert.True(result.Unreadable);
        Assert.Equal(0, result.Skipped);
    }

    /// <summary>Only the five declared fields reach the record; unknown fields are ignored without error.</summary>
    [Fact]
    public async Task Load_ReadsOnlyDeclaredFields()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        File.WriteAllBytes(Path.Combine(this.root, "a.png"), PngBytes);
        File.WriteAllText(Path.Combine(this.root, "master.json"), """
            {
              "version": 3,
              "avatars": [
                {
                  "file": "a.png", "profession": "Podcaster", "skin_tone": "fair", "gender_presentation": "woman",
                  "tags": ["mic", "calm"], "grid": {"x": 1, "y": 2}, "hair": "red", "notes": null
                }
              ]
            }
            """);
        AvatarLibraryLoader loader = this.CreateLoader(this.root);

        AvatarLibraryLoadResult result = await loader.LoadAsync(ct);

        Assert.False(result.Unreadable);
        Assert.Equal(0, result.Skipped);
        Assert.NotNull(result.Catalog);
        LibraryAvatar avatar = Assert.Single(result.Catalog.All);
        Assert.Equal("a.png", avatar.File);
        Assert.Equal("Podcaster", avatar.Profession);
        Assert.Equal("fair", avatar.SkinTone);
        Assert.Equal("woman", avatar.Gender);
        Assert.Equal(["mic", "calm"], avatar.Tags);
    }

    /// <summary>Missing skin tone, gender and tags become empty values, not a skipped record.</summary>
    [Fact]
    public async Task Load_MissingOptionalFields_BecomeEmpty()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        File.WriteAllBytes(Path.Combine(this.root, "a.png"), PngBytes);
        this.WriteMaster("""{"avatars":[{"file":"a.png","profession":"Chef"}]}""");
        AvatarLibraryLoader loader = this.CreateLoader(this.root);

        AvatarLibraryLoadResult result = await loader.LoadAsync(ct);

        Assert.NotNull(result.Catalog);
        LibraryAvatar avatar = Assert.Single(result.Catalog.All);
        Assert.Equal(string.Empty, avatar.SkinTone);
        Assert.Equal(string.Empty, avatar.Gender);
        Assert.Empty(avatar.Tags);
    }

    /// <summary>A record with a blank profession is skipped and counted.</summary>
    [Fact]
    public async Task Load_BlankProfession_IsSkipped()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        File.WriteAllBytes(Path.Combine(this.root, "a.png"), PngBytes);
        this.WriteMaster("""{"avatars":[{"file":"a.png","profession":"  "}]}""");
        AvatarLibraryLoader loader = this.CreateLoader(this.root);

        AvatarLibraryLoadResult result = await loader.LoadAsync(ct);

        Assert.NotNull(result.Catalog);
        Assert.Empty(result.Catalog.All);
        Assert.Equal(1, result.Skipped);
    }

    /// <summary>Malformed JSON is unreadable, logs one warning and never throws.</summary>
    [Fact]
    public async Task Load_MalformedJson_ReturnsUnreadable()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        this.WriteMaster("{ this is not json");
        AvatarLibraryLoader loader = this.CreateLoader(this.root);

        AvatarLibraryLoadResult result = await loader.LoadAsync(ct);

        Assert.Null(result.Catalog);
        Assert.True(result.Unreadable);
        Assert.Single(this.logger.Entries, e => e.Level == LogLevel.Warning);
    }

    /// <summary>A document with no <c>avatars</c> array is unreadable.</summary>
    [Fact]
    public async Task Load_NoAvatarsArray_ReturnsUnreadable()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        this.WriteMaster("""{"version":1}""");
        AvatarLibraryLoader loader = this.CreateLoader(this.root);

        AvatarLibraryLoadResult result = await loader.LoadAsync(ct);

        Assert.Null(result.Catalog);
        Assert.True(result.Unreadable);
    }

    /// <summary>A <c>master.json</c> over four megabytes is refused as unreadable.</summary>
    [Fact]
    public async Task Load_OverFourMegabytes_ReturnsUnreadable()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string head = """{"avatars":[],"pad":" """.TrimEnd();
        string text = head + new string(' ', (4 * 1024 * 1024) + 10) + "\"}";
        this.WriteMaster(text);
        AvatarLibraryLoader loader = this.CreateLoader(this.root);

        AvatarLibraryLoadResult result = await loader.LoadAsync(ct);

        Assert.Null(result.Catalog);
        Assert.True(result.Unreadable);
    }

    /// <summary>A record whose file name carries a separator or <c>..</c> is skipped, never read.</summary>
    [Fact]
    public async Task Load_SkipsTraversalFileName()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string library = Path.Combine(this.root, "lib");
        Directory.CreateDirectory(library);
        File.WriteAllBytes(Path.Combine(this.root, "outside.png"), PngBytes);
        File.WriteAllBytes(Path.Combine(library, "sub.png"), PngBytes);
        File.WriteAllText(Path.Combine(library, "master.json"), """
            {"avatars":[
              {"file":"../outside.png","profession":"A"},
              {"file":"..\\outside.png","profession":"B"},
              {"file":"sub/x.png","profession":"C"}
            ]}
            """);
        AvatarLibraryLoader loader = this.CreateLoader(library);

        AvatarLibraryLoadResult result = await loader.LoadAsync(ct);

        Assert.NotNull(result.Catalog);
        Assert.Empty(result.Catalog.All);
        Assert.Equal(3, result.Skipped);
    }

    /// <summary>A record naming a file that is not in the folder is skipped and counted.</summary>
    [Fact]
    public async Task Load_SkipsMissingFile()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        File.WriteAllBytes(Path.Combine(this.root, "here.png"), PngBytes);
        this.WriteMaster("""{"avatars":[{"file":"here.png","profession":"A"},{"file":"gone.png","profession":"B"}]}""");
        AvatarLibraryLoader loader = this.CreateLoader(this.root);

        AvatarLibraryLoadResult result = await loader.LoadAsync(ct);

        Assert.NotNull(result.Catalog);
        Assert.Equal("here.png", Assert.Single(result.Catalog.All).File);
        Assert.Equal(1, result.Skipped);
    }

    /// <summary>SVG files and files whose bytes are not an image are skipped.</summary>
    [Fact]
    public async Task Load_SkipsSvgAndNonImageBytes()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        File.WriteAllText(Path.Combine(this.root, "pic.svg"), "<svg/>");
        File.WriteAllText(Path.Combine(this.root, "fake.png"), "this is text, not a png");
        File.WriteAllBytes(Path.Combine(this.root, "real.webp"), WebpBytes);
        this.WriteMaster("""
            {"avatars":[
              {"file":"pic.svg","profession":"A"},
              {"file":"fake.png","profession":"B"},
              {"file":"real.webp","profession":"C"}
            ]}
            """);
        AvatarLibraryLoader loader = this.CreateLoader(this.root);

        AvatarLibraryLoadResult result = await loader.LoadAsync(ct);

        Assert.NotNull(result.Catalog);
        Assert.Equal("real.webp", Assert.Single(result.Catalog.All).File);
        Assert.Equal(2, result.Skipped);
    }

    /// <summary>An image over <see cref="AvatarImage.MaxBytes"/> is skipped.</summary>
    [Fact]
    public async Task Load_SkipsOversizeImage()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        byte[] big = new byte[AvatarImage.MaxBytes + 1];
        PngBytes.CopyTo(big, 0);
        File.WriteAllBytes(Path.Combine(this.root, "big.png"), big);
        this.WriteMaster("""{"avatars":[{"file":"big.png","profession":"A"}]}""");
        AvatarLibraryLoader loader = this.CreateLoader(this.root);

        AvatarLibraryLoadResult result = await loader.LoadAsync(ct);

        Assert.NotNull(result.Catalog);
        Assert.Empty(result.Catalog.All);
        Assert.Equal(1, result.Skipped);
    }

    /// <summary>When two records name the same file, the first wins and the rest are skipped and counted.</summary>
    [Fact]
    public async Task Load_DuplicateFile_FirstWins()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        File.WriteAllBytes(Path.Combine(this.root, "a.png"), PngBytes);
        this.WriteMaster("""
            {"avatars":[
              {"file":"a.png","profession":"First"},
              {"file":"A.PNG","profession":"Second"}
            ]}
            """);
        AvatarLibraryLoader loader = this.CreateLoader(this.root);

        AvatarLibraryLoadResult result = await loader.LoadAsync(ct);

        Assert.NotNull(result.Catalog);
        Assert.Equal("First", Assert.Single(result.Catalog.All).Profession);
        Assert.Equal(1, result.Skipped);
    }

    /// <summary>Many skipped records produce exactly one warning that names at most five examples.</summary>
    [Fact]
    public async Task Load_Skipped_LogsOneWarning()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string records = string.Join(",", Enumerable.Range(0, 8).Select(i => $$"""{"file":"missing{{i}}.png","profession":"P"}"""));
        this.WriteMaster("{\"avatars\":[" + records + "]}");
        AvatarLibraryLoader loader = this.CreateLoader(this.root);

        AvatarLibraryLoadResult result = await loader.LoadAsync(ct);

        Assert.Equal(8, result.Skipped);
        CapturedLog warning = Assert.Single(this.logger.Entries, e => e.Level == LogLevel.Warning);
        Assert.Contains("missing0.png", warning.Message, StringComparison.Ordinal);
        Assert.Contains("missing4.png", warning.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("missing5.png", warning.Message, StringComparison.Ordinal);
    }

    /// <summary>A real image comes back as its bytes and the extension its bytes prove.</summary>
    [Fact]
    public async Task ReadImage_ReturnsBytesAndExtension()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        File.WriteAllBytes(Path.Combine(this.root, "p.png"), PngBytes);
        File.WriteAllBytes(Path.Combine(this.root, "j.jpeg"), JpegBytes);
        AvatarLibraryLoader loader = this.CreateLoader(this.root);

        AvatarLibraryImage? png = await loader.ReadImageAsync("p.png", ct);
        AvatarLibraryImage? jpeg = await loader.ReadImageAsync("j.jpeg", ct);

        Assert.NotNull(png);
        Assert.Equal(PngBytes, png.Bytes);
        Assert.Equal(".png", png.Extension);
        Assert.NotNull(jpeg);
        Assert.Equal(".jpg", jpeg.Extension);
    }

    /// <summary>Blank, traversing, disallowed-extension, missing and oversize names all return null.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("../outside.png")]
    [InlineData("..\\outside.png")]
    [InlineData("sub/p.png")]
    [InlineData("pic.svg")]
    [InlineData("nope.png")]
    [InlineData("big.png")]
    public async Task ReadImage_UnsafeName_ReturnsNull(string? name)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        byte[] big = new byte[AvatarImage.MaxBytes + 1];
        PngBytes.CopyTo(big, 0);
        File.WriteAllBytes(Path.Combine(this.root, "big.png"), big);
        File.WriteAllText(Path.Combine(this.root, "pic.svg"), "<svg/>");
        AvatarLibraryLoader loader = this.CreateLoader(this.root);

        AvatarLibraryImage? image = await loader.ReadImageAsync(name!, ct);

        Assert.Null(image);
    }

    /// <summary>A text file renamed to <c>.png</c> returns null: the bytes decide, not the name.</summary>
    [Fact]
    public async Task ReadImage_RenamedTextFile_ReturnsNull()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        File.WriteAllText(Path.Combine(this.root, "fake.png"), "plain text");
        AvatarLibraryLoader loader = this.CreateLoader(this.root);

        AvatarLibraryImage? image = await loader.ReadImageAsync("fake.png", ct);

        Assert.Null(image);
    }

    private void WriteMaster(string json)
    {
        File.WriteAllText(Path.Combine(this.root, "master.json"), json);
    }

    private AvatarLibraryLoader CreateLoader(string? dir)
    {
        TeamOptions options = new()
        {
            AvatarLibrary = new AvatarLibraryOptions { Dir = dir },
        };
        return new AvatarLibraryLoader(new AvatarLibraryLocator(Options.Create(options)), this.logger);
    }

    /// <summary>One captured log call.</summary>
    /// <param name="Level">The level logged at.</param>
    /// <param name="Message">The formatted message.</param>
    private sealed record CapturedLog(LogLevel Level, string Message);

    /// <summary>A logger that records every call, so a test can count and read the warnings.</summary>
    private sealed class CapturingLogger : ILogger<AvatarLibraryLoader>
    {
        /// <summary>Every call made so far.</summary>
        internal List<CapturedLog> Entries { get; } = [];

        /// <inheritdoc />
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        /// <inheritdoc />
        public bool IsEnabled(LogLevel logLevel) => true;

        /// <inheritdoc />
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            this.Entries.Add(new CapturedLog(logLevel, formatter(state, exception)));
        }
    }
}
