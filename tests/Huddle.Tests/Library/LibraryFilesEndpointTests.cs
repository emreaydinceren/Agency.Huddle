using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.Tests.Ui;

namespace Agency.Huddle.Tests.Library;

/// <summary>
/// Exercises the <c>/library-files/{rootId}/{**path}</c> endpoint (Task 12.8) over a real
/// <see cref="System.Net.Http.HttpClient"/> hosted by <see cref="TeamWebApplicationFactory"/>,
/// the same shape <see cref="Agency.Huddle.Tests.Ui.AvatarEndpointTests"/> uses for
/// <c>/teammate-avatars</c>. This endpoint is a stated exception to <c>UseStaticFiles</c> (rules.md):
/// pinned roots can live anywhere on disk, so it re-runs <see cref="Agency.Huddle.App.Library.LibraryPathResolver.TryResolve"/>
/// on every request instead (Spec §9 item 4).
/// </summary>
public sealed class LibraryFilesEndpointTests
{
    // A minimal but genuine PNG: the 8-byte signature plus a 13-byte IHDR chunk.
    private static readonly byte[] MinimalPng =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
        0x08, 0x00, 0x00, 0x00, 0x00, 0x3A, 0x7E, 0x9B,
        0x55,
    ];

    // A minimal JPEG: the SOI + APP0/JFIF marker bytes the sniffer keys on (0xFF 0xD8 0xFF).
    private static readonly byte[] MinimalJpeg =
    [
        0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46,
        0x49, 0x46, 0x00, 0x01, 0x01, 0x00, 0x00, 0x01,
    ];

    // GIF89a header bytes, the minimum LibraryFileKinds.ImageContentType reads.
    private static readonly byte[] MinimalGif =
    [
        0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 0x01, 0x00, 0x01, 0x00,
    ];

    // RIFF....WEBP: the 12 bytes LibraryFileKinds.ImageContentType reads for WebP.
    private static readonly byte[] MinimalWebp =
    [
        0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50,
    ];

    private static readonly byte[] TextBytesNamedPng = "not really a png"u8.ToArray();

    /// <summary>A real PNG in the Teams root is served with the exact <c>image/png</c> content type.</summary>
    [Fact]
    public async Task Get_PngInTeamsRoot_200WithImagePng()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        var teamsRoot = TeamsRootOf(factory);
        Directory.CreateDirectory(teamsRoot);
        await File.WriteAllBytesAsync(Path.Combine(teamsRoot, "shot.png"), MinimalPng, ct);

        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/library-files/teams/shot.png", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
    }

    /// <summary>A real PNG in the Teams root carries <c>X-Content-Type-Options: nosniff</c> and the exact content-type header (corrections-B6 item 29).</summary>
    [Fact]
    public async Task Get_Png_HasNosniffHeaderAndExactContentType()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        var teamsRoot = TeamsRootOf(factory);
        Directory.CreateDirectory(teamsRoot);
        await File.WriteAllBytesAsync(Path.Combine(teamsRoot, "shot.png"), MinimalPng, ct);

        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/library-files/teams/shot.png", ct);

        Assert.Equal("nosniff", GetHeaderValue(response, "X-Content-Type-Options"));
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
    }

    /// <summary>A JPEG is served as <c>image/jpeg</c>, identified by its magic bytes.</summary>
    [Fact]
    public async Task Get_Jpeg_200WithImageJpeg()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        var teamsRoot = TeamsRootOf(factory);
        Directory.CreateDirectory(teamsRoot);
        await File.WriteAllBytesAsync(Path.Combine(teamsRoot, "shot.jpg"), MinimalJpeg, ct);

        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/library-files/teams/shot.jpg", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/jpeg", response.Content.Headers.ContentType?.MediaType);
    }

    /// <summary>A GIF is served as <c>image/gif</c>, identified by its magic bytes.</summary>
    [Fact]
    public async Task Get_Gif_200WithImageGif()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        var teamsRoot = TeamsRootOf(factory);
        Directory.CreateDirectory(teamsRoot);
        await File.WriteAllBytesAsync(Path.Combine(teamsRoot, "shot.gif"), MinimalGif, ct);

        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/library-files/teams/shot.gif", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/gif", response.Content.Headers.ContentType?.MediaType);
    }

    /// <summary>A WebP is served as <c>image/webp</c>, identified by its RIFF/WEBP magic bytes.</summary>
    [Fact]
    public async Task Get_Webp_200WithImageWebp()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        var teamsRoot = TeamsRootOf(factory);
        Directory.CreateDirectory(teamsRoot);
        await File.WriteAllBytesAsync(Path.Combine(teamsRoot, "shot.webp"), MinimalWebp, ct);

        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/library-files/teams/shot.webp", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/webp", response.Content.Headers.ContentType?.MediaType);
    }

    /// <summary>An SVG is never served, even though it is a Spec §6.11 "known" extension (rules.md, "SVG is never accepted").</summary>
    [Fact]
    public async Task Get_Svg_404()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        var teamsRoot = TeamsRootOf(factory);
        Directory.CreateDirectory(teamsRoot);
        const string svgMarker = "<svg onload=\"alert(1)\"></svg>";
        var svgPath = Path.Combine(teamsRoot, "evil.svg");
        await File.WriteAllTextAsync(svgPath, svgMarker, ct);

        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/library-files/teams/evil.svg", ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(ct);
        Assert.DoesNotContain(svgMarker, body, StringComparison.Ordinal);
        Assert.True(File.Exists(svgPath));
    }

    /// <summary>A Markdown file is refused: this endpoint serves images only, whatever its extension.</summary>
    [Fact]
    public async Task Get_Markdown_404()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        var teamsRoot = TeamsRootOf(factory);
        Directory.CreateDirectory(teamsRoot);
        var mdPath = Path.Combine(teamsRoot, "note.md");
        await File.WriteAllTextAsync(mdPath, "# Nova\n", ct);

        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/library-files/teams/note.md", ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.True(File.Exists(mdPath));
    }

    /// <summary>A file named <c>.png</c> whose content is plain text is refused: magic bytes decide, not the extension.</summary>
    [Fact]
    public async Task Get_PngNamedFileWithTextBytes_404()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        var teamsRoot = TeamsRootOf(factory);
        Directory.CreateDirectory(teamsRoot);
        var fakePath = Path.Combine(teamsRoot, "fake.png");
        await File.WriteAllBytesAsync(fakePath, TextBytesNamedPng, ct);

        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/library-files/teams/fake.png", ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.True(File.Exists(fakePath));
    }

    /// <summary>A raw <c>..</c> segment aimed at a real file outside the requested root is refused (the plan's literal example).</summary>
    [Fact]
    public async Task Get_TraversalRawDotDot_404()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        await factory.WriteDefinitionAsync("nova", "---\nname: Nova\n---\nHi\n", ct);
        var targetPath = Path.Combine(factory.TeammatesDirPath, "nova", "nova.md");
        Assert.True(File.Exists(targetPath));

        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/library-files/teams/../Teammates/nova/nova.md", ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.True(File.Exists(targetPath));
    }

    /// <summary>A <c>%2e%2e</c>-encoded traversal segment aimed at a real file outside the root is refused.</summary>
    [Fact]
    public async Task Get_TraversalPercentEncodedDots_404()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        await factory.WriteDefinitionAsync("nova", "---\nname: Nova\n---\nHi\n", ct);
        var targetPath = Path.Combine(factory.TeammatesDirPath, "nova", "nova.md");
        Assert.True(File.Exists(targetPath));

        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/library-files/teams/%2e%2e/Teammates/nova/nova.md", ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.True(File.Exists(targetPath));
    }

    /// <summary>A <c>%2f</c>-encoded separator smuggling a traversal past naive segmentation, aimed at a real file, is refused.</summary>
    [Fact]
    public async Task Get_TraversalPercentEncodedSlash_404()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        var contentRoot = FindAppContentRoot();
        var targetPath = Path.Combine(contentRoot, "appsettings.json");
        Assert.True(File.Exists(targetPath));

        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/library-files/teams/..%2f..%2fappsettings.json", ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.True(File.Exists(targetPath));
    }

    /// <summary>An underscore-prefixed folder under the Teams root is refused (Spec §6.16, the Teams root only).</summary>
    [Fact]
    public async Task Get_UnderscoreFolder_404()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        var teamsRoot = TeamsRootOf(factory);
        var hiddenFolder = Path.Combine(teamsRoot, "_tasks");
        Directory.CreateDirectory(hiddenFolder);
        var hiddenPath = Path.Combine(hiddenFolder, "shot.png");
        await File.WriteAllBytesAsync(hiddenPath, MinimalPng, ct);

        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/library-files/teams/_tasks/shot.png", ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.True(File.Exists(hiddenPath));
    }

    /// <summary>An unknown Library root id is refused.</summary>
    [Fact]
    public async Task Get_UnknownRoot_404()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/library-files/nope/shot.png", ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>A real PNG under a pinned root outside the data directory is served.</summary>
    [Fact]
    public async Task Get_PinnedRootPng_200()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        using var pinnedDir = new TempDataDir();
        await File.WriteAllBytesAsync(Path.Combine(pinnedDir.Path, "shot.png"), MinimalPng, ct);

        await using var factory = new TeamWebApplicationFactory();
        using var pinned = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Team:Library:Roots:0:Name", "Pinned");
            builder.UseSetting("Team:Library:Roots:0:Path", pinnedDir.Path);
        });
        using var client = pinned.CreateClient();

        using var response = await client.GetAsync("/library-files/pinned/shot.png", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
    }

    /// <summary>The allowed neighbour of the two hidden-folder rows below: a normal subfolder under the same pinned root is served.</summary>
    [Fact]
    public async Task Get_PinnedRootPngInNormalSubfolder_200()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        using var pinnedDir = new TempDataDir();
        var photosFolder = Path.Combine(pinnedDir.Path, "Photos");
        Directory.CreateDirectory(photosFolder);
        await File.WriteAllBytesAsync(Path.Combine(photosFolder, "shot.png"), MinimalPng, ct);

        await using var factory = new TeamWebApplicationFactory();
        using var pinned = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Team:Library:Roots:0:Name", "Pinned");
            builder.UseSetting("Team:Library:Roots:0:Path", pinnedDir.Path);
        });
        using var client = pinned.CreateClient();

        using var response = await client.GetAsync("/library-files/pinned/Photos/shot.png", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
    }

    /// <summary>A PNG inside a <c>.git</c> folder under a pinned root is refused, even though <see cref="Agency.Huddle.App.Library.LibraryPathResolver.TryResolve"/> itself still accepts it (corrections-B6 item 29).</summary>
    [Fact]
    public async Task Get_PngInsideDotGitUnderPinnedRoot_404()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        using var pinnedDir = new TempDataDir();
        var gitFolder = Path.Combine(pinnedDir.Path, ".git");
        Directory.CreateDirectory(gitFolder);
        var hiddenPath = Path.Combine(gitFolder, "shot.png");
        await File.WriteAllBytesAsync(hiddenPath, MinimalPng, ct);

        await using var factory = new TeamWebApplicationFactory();
        using var pinned = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Team:Library:Roots:0:Name", "Pinned");
            builder.UseSetting("Team:Library:Roots:0:Path", pinnedDir.Path);
        });
        using var client = pinned.CreateClient();

        using var response = await client.GetAsync("/library-files/pinned/.git/shot.png", ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.True(File.Exists(hiddenPath));
    }

    /// <summary>A PNG inside a <c>node_modules</c> folder under a pinned root is refused, even though <see cref="Agency.Huddle.App.Library.LibraryPathResolver.TryResolve"/> itself still accepts it (corrections-B6 item 29).</summary>
    [Fact]
    public async Task Get_PngInNodeModulesUnderPinnedRoot_404()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        using var pinnedDir = new TempDataDir();
        var nodeModulesFolder = Path.Combine(pinnedDir.Path, "node_modules");
        Directory.CreateDirectory(nodeModulesFolder);
        var hiddenPath = Path.Combine(nodeModulesFolder, "shot.png");
        await File.WriteAllBytesAsync(hiddenPath, MinimalPng, ct);

        await using var factory = new TeamWebApplicationFactory();
        using var pinned = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Team:Library:Roots:0:Name", "Pinned");
            builder.UseSetting("Team:Library:Roots:0:Path", pinnedDir.Path);
        });
        using var client = pinned.CreateClient();

        using var response = await client.GetAsync("/library-files/pinned/node_modules/shot.png", ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.True(File.Exists(hiddenPath));
    }

    /// <summary>When <c>Library:Enabled</c> is <see langword="false"/>, even a valid image request is refused (corrections-B6 item 29).</summary>
    [Fact]
    public async Task Get_WhenLibraryDisabled_404()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        var teamsRoot = TeamsRootOf(factory);
        Directory.CreateDirectory(teamsRoot);
        await File.WriteAllBytesAsync(Path.Combine(teamsRoot, "shot.png"), MinimalPng, ct);

        using var disabled = factory.WithWebHostBuilder(builder =>
            builder.UseSetting("Team:Library:Enabled", "false"));
        using var client = disabled.CreateClient();

        using var response = await client.GetAsync("/library-files/teams/shot.png", ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>A path with a space and a <c>#</c>, each segment URL-encoded, resolves to a real PNG.</summary>
    [Fact]
    public async Task Get_PathWithSpaceAndHash_200()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        var teamsRoot = TeamsRootOf(factory);
        var folder = Path.Combine(teamsRoot, "Launch Q4");
        Directory.CreateDirectory(folder);
        var filePath = Path.Combine(folder, "#1 shot.png");
        await File.WriteAllBytesAsync(filePath, MinimalPng, ct);

        using var client = factory.CreateClient();
        var url = "/library-files/teams/"
            + Uri.EscapeDataString("Launch Q4") + "/"
            + Uri.EscapeDataString("#1 shot.png");
        using var response = await client.GetAsync(url, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
    }

    /// <summary>An image inside a Teammate's own folder in the Teammates root is served: Spec §6.11 lists Image as previewed through this endpoint for every Library root, and §6.12 shows a Teammate's own files inside the Library tree.</summary>
    [Fact]
    public async Task Get_ImageInTeammatesRoot_200()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        await factory.WriteDefinitionAsync("nova", "---\nname: Nova\n---\nHi\n", ct);
        var novaFolder = Path.Combine(factory.TeammatesDirPath, "nova");
        await File.WriteAllBytesAsync(Path.Combine(novaFolder, "avatar.png"), MinimalPng, ct);

        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/library-files/teammates/nova/avatar.png", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
    }

    /// <summary>Reads one response header's first value, or <see langword="null"/> when it is absent.</summary>
    private static string? GetHeaderValue(HttpResponseMessage response, string name)
    {
        return response.Headers.TryGetValues(name, out var values) ? values.FirstOrDefault() : null;
    }

    /// <summary>The Teams root this factory's data dir resolves to (<c>{DataDir}/Teams</c>, the default <see cref="Agency.Huddle.App.TeamsOptions.Dir"/>).</summary>
    private static string TeamsRootOf(TeamWebApplicationFactory factory)
    {
        return Path.Combine(DataDirOf(factory), "Teams");
    }

    /// <summary>The isolated <see cref="TeamOptions.DataDir"/> <paramref name="factory"/> composed its app with.</summary>
    private static string DataDirOf(TeamWebApplicationFactory factory)
    {
        return factory.Services.GetRequiredService<IOptions<TeamOptions>>().Value.DataDir;
    }

    /// <summary>Walks up from the test binary to find <c>src/Huddle.App</c>, the same way <see cref="TeamWebApplicationFactory"/> locates its content root, so <see cref="Get_TraversalPercentEncodedSlash_404"/> can assert against a file that really exists.</summary>
    private static string FindAppContentRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Huddle.slnx")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException(
                $"Could not locate Huddle.slnx by walking up from '{AppContext.BaseDirectory}'.");
        }

        return Path.Combine(directory.FullName, "src", "Huddle.App");
    }
}
