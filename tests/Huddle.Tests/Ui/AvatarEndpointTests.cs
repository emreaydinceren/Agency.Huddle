using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Exercises the <c>/teammate-avatars</c> static-file middleware registered in <c>Program.cs</c>,
/// through a real <see cref="System.Net.Http.HttpClient"/> hosted by <see cref="TeamWebApplicationFactory"/> -
/// the same shape <see cref="AppHostTests"/> uses for <c>/health</c>, so these prove the real pipeline
/// rather than a hand-built <c>StaticFileOptions</c> in isolation.
/// </summary>
public sealed class AvatarEndpointTests
{
    // A minimal but genuine PNG: the 8-byte signature plus a 13-byte IHDR chunk (length, type,
    // 1x1/8-bit-greyscale data, and a CRC). The middleware under test never decodes the bytes - it
    // only needs a file that exists - but a real signature keeps this test honest about what a
    // browser would actually be handed.
    private static readonly byte[] MinimalPng =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
        0x08, 0x00, 0x00, 0x00, 0x00, 0x3A, 0x7E, 0x9B,
        0x55,
    ];

    /// <summary>A real PNG written into <c>{DataDir}/avatars/</c> is served with the restricted content type and both immutable-cache headers.</summary>
    [Fact]
    public async Task Get_RealPng_ServesWithImagePngAndImmutableCacheHeaders()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        var avatarsDirectory = Path.Combine(DataDirOf(factory), "avatars");
        Directory.CreateDirectory(avatarsDirectory);
        await File.WriteAllBytesAsync(Path.Combine(avatarsDirectory, "real.png"), MinimalPng, ct);

        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/teammate-avatars/real.png", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("immutable", response.Headers.CacheControl?.ToString() ?? string.Empty, StringComparison.Ordinal);
        Assert.Equal("nosniff", GetHeaderValue(response, "X-Content-Type-Options"));
    }

    /// <summary>
    /// A planted <c>.svg</c> file is refused with 404, proving the restricted <see cref="Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider"/>
    /// is doing its job - the default provider knows <c>.svg</c> and would otherwise serve it despite
    /// <c>ServeUnknownFileTypes=false</c>, because that flag alone only blocks a genuinely unknown type.
    /// </summary>
    [Fact]
    public async Task Get_PlantedSvg_IsNotServed()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        var avatarsDirectory = Path.Combine(DataDirOf(factory), "avatars");
        Directory.CreateDirectory(avatarsDirectory);
        const string svgMarker = "<svg onload=\"alert(1)\"></svg>";
        var svgPath = Path.Combine(avatarsDirectory, "evil.svg");
        await File.WriteAllTextAsync(svgPath, svgMarker, ct);

        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/teammate-avatars/evil.svg", ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(ct);
        Assert.DoesNotContain(svgMarker, body, StringComparison.Ordinal);

        // Proves the 404 above is the middleware refusing a servable-looking file, not merely a
        // typo in this test never having written one.
        Assert.True(File.Exists(svgPath));
    }

    /// <summary>A path-traversal attempt against a file outside the avatars directory is refused, and the refusal is not the file's contents.</summary>
    [Fact]
    public async Task Get_PathTraversalOutsideAvatarsDirectory_IsRefused()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/teammate-avatars/../appsettings.json", ct);

        Assert.False(response.IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(ct);
        Assert.DoesNotContain("AllowedHosts", body, StringComparison.Ordinal);
    }

    /// <summary>A file name that does not exist in the avatars directory is a 404, not a 500.</summary>
    [Fact]
    public async Task Get_MissingFile_Is404()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/teammate-avatars/does-not-exist.png", ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>Reads one response header's first value, or <see langword="null"/> when it is absent - <see cref="HttpResponseMessage.Headers"/> does not expose an indexer.</summary>
    /// <param name="response">The response to read from.</param>
    /// <param name="name">The header name.</param>
    private static string? GetHeaderValue(HttpResponseMessage response, string name)
    {
        return response.Headers.TryGetValues(name, out var values) ? values.FirstOrDefault() : null;
    }

    /// <summary>
    /// The isolated <see cref="TeamOptions.DataDir"/> <paramref name="factory"/> composed its app
    /// with - resolved from the running host rather than duplicated, since <see cref="TeamWebApplicationFactory"/>
    /// exposes its temp directory only indirectly (via <see cref="TeamWebApplicationFactory.TeamsDirPath"/>
    /// and friends), never as a raw path of its own.
    /// </summary>
    /// <param name="factory">The factory whose composed <see cref="TeamOptions"/> to read.</param>
    private static string DataDirOf(TeamWebApplicationFactory factory)
    {
        return factory.Services.GetRequiredService<IOptions<TeamOptions>>().Value.DataDir;
    }
}
