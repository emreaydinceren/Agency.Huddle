using System.Net;
using System.Reflection;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.Tests.Ui;
using Microsoft.Extensions.DependencyInjection;

namespace Agency.Huddle.Tests.Avatars;

/// <summary>
/// Exercises the <c>/avatar-library</c> static-file mount and the Avatar Library DI registrations in
/// <c>Program.cs</c>, through a real <see cref="HttpClient"/> hosted by <see cref="TeamWebApplicationFactory"/>,
/// the same shape <c>AvatarEndpointTests</c> uses for <c>/teammate-avatars</c>.
/// </summary>
public sealed class AvatarLibraryMountTests : IDisposable
{
    private static readonly byte[] MinimalPng =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
        0x08, 0x00, 0x00, 0x00, 0x00, 0x3A, 0x7E, 0x9B,
        0x55,
    ];

    private readonly string libraryDir = Path.Combine(Path.GetTempPath(), "avatar-library-mount-" + Guid.NewGuid().ToString("N"));

    /// <summary>Deletes the temporary library folder.</summary>
    public void Dispose()
    {
        if (Directory.Exists(this.libraryDir))
        {
            Directory.Delete(this.libraryDir, recursive: true);
        }
    }

    /// <summary>A PNG in the configured library folder is served as image/png with a one-hour, non-immutable cache and nosniff.</summary>
    [Fact]
    public async Task Mount_ServesPng()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string png = this.Seed("a.png", MinimalPng);

        await using WebApplicationFactoryHandle handle = Host(this.libraryDir);
        using HttpClient client = handle.Factory.CreateClient();
        using HttpResponseMessage response = await client.GetAsync("/avatar-library/a.png", ct);

        Assert.True(File.Exists(png));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
        string cache = response.Headers.CacheControl?.ToString() ?? string.Empty;
        Assert.Contains("max-age=3600", cache, StringComparison.Ordinal);
        Assert.DoesNotContain("immutable", cache, StringComparison.Ordinal);
        Assert.True(response.Headers.TryGetValues("X-Content-Type-Options", out IEnumerable<string>? nosniff));
        Assert.Equal("nosniff", nosniff.First());
    }

    /// <summary>master.json is never served through the mount, even though it exists in the folder.</summary>
    [Fact]
    public async Task Mount_MasterJson_Returns404_WhileFileExists()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string master = this.Seed("master.json", "[]"u8.ToArray());

        await using WebApplicationFactoryHandle handle = Host(this.libraryDir);
        using HttpClient client = handle.Factory.CreateClient();
        using HttpResponseMessage response = await client.GetAsync("/avatar-library/master.json", ct);

        Assert.True(File.Exists(master));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>An svg planted in the library folder is never served through the mount.</summary>
    [Fact]
    public async Task Mount_Svg_Returns404_WhileFileExists()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string svg = this.Seed("evil.svg", "<svg onload=\"alert(1)\"></svg>"u8.ToArray());

        await using WebApplicationFactoryHandle handle = Host(this.libraryDir);
        using HttpClient client = handle.Factory.CreateClient();
        using HttpResponseMessage response = await client.GetAsync("/avatar-library/evil.svg", ct);

        Assert.True(File.Exists(svg));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>With no library folder configured the app still starts and the URL space answers 404.</summary>
    [Fact]
    public async Task Mount_NotAdded_WhenDirUnset()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        await using WebApplicationFactoryHandle handle = Host(null);
        using HttpClient client = handle.Factory.CreateClient();
        using HttpResponseMessage response = await client.GetAsync("/avatar-library/x.png", ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>A configured folder that does not exist does not stop the app starting, and the mount is absent.</summary>
    [Fact]
    public async Task Mount_NotAdded_WhenDirMissing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Assert.False(Directory.Exists(this.libraryDir));

        await using WebApplicationFactoryHandle handle = Host(this.libraryDir);
        using HttpClient client = handle.Factory.CreateClient();
        using HttpResponseMessage response = await client.GetAsync("/avatar-library/x.png", ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.False(Directory.Exists(this.libraryDir));
    }

    /// <summary>The locator and loader resolve, and the loader is one stateless singleton holding no catalogue.</summary>
    [Fact]
    public async Task Services_Resolve()
    {
        await using WebApplicationFactoryHandle handle = Host(this.libraryDir);
        using HttpClient client = handle.Factory.CreateClient();

        IAvatarLibraryLoader first = handle.Factory.Services.GetRequiredService<IAvatarLibraryLoader>();
        IAvatarLibraryLoader second = handle.Factory.Services.GetRequiredService<IAvatarLibraryLoader>();
        AvatarLibraryLocator locator = handle.Factory.Services.GetRequiredService<AvatarLibraryLocator>();

        Assert.NotNull(locator);
        Assert.Same(first, second);
        Assert.IsType<AvatarLibraryLoader>(first);
        FieldInfo[] fields = typeof(AvatarLibraryLoader).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.DoesNotContain(
            fields,
            static field => field.FieldType == typeof(AvatarCatalog)
                || (field.FieldType != typeof(string) && typeof(System.Collections.IEnumerable).IsAssignableFrom(field.FieldType)));
    }

    private string Seed(string name, byte[] bytes)
    {
        Directory.CreateDirectory(this.libraryDir);
        string path = Path.Combine(this.libraryDir, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private static WebApplicationFactoryHandle Host(string? dir)
    {
        TeamWebApplicationFactory root = new();
        var configured = dir is null
            ? root
            : root.WithWebHostBuilder(builder => builder.UseSetting("Team:AvatarLibrary:Dir", dir));
        return new WebApplicationFactoryHandle(root, configured);
    }

    private sealed class WebApplicationFactoryHandle(TeamWebApplicationFactory root, Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> configured) : IAsyncDisposable
    {
        public Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> Factory { get; } = configured;

        public async ValueTask DisposeAsync()
        {
            await this.Factory.DisposeAsync();
            await root.DisposeAsync();
        }
    }
}
