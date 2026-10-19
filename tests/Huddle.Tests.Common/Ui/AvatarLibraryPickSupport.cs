using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.Tests.Avatars;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Shared setup for the tests that pick an Avatar from the library through <see cref="Agency.Huddle.App.Components.Shared.TeammateCard"/>
/// and <see cref="Agency.Huddle.App.Components.Settings.Appearance"/>: a fake loader whose picked image is a chosen value, and the
/// registration of a locator that is configured (over a temp folder holding <c>master.json</c>) or not.
/// </summary>
internal static class AvatarLibraryPickSupport
{
    /// <summary>A minimal but genuine PNG: the 8-byte signature plus a 13-byte IHDR chunk, so <c>AvatarImage.SniffExtension</c> accepts it.</summary>
    internal static readonly byte[] MinimalPng =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
        0x08, 0x00, 0x00, 0x00, 0x00, 0x3A, 0x7E, 0x9B,
        0x55,
    ];

    /// <summary>The CSS selector of one thumbnail button in the picker's grid.</summary>
    internal const string Thumb = "button.avatar-library-thumb";

    /// <summary>The visible text of the button that opens the picker.</summary>
    internal const string ButtonText = "Choose from library";

    /// <summary>Creates a library folder under a fresh temp directory and writes an empty-ish <c>master.json</c> into it, so the file-based locator reports configured.</summary>
    /// <returns>The temp directory, which the caller disposes.</returns>
    internal static TempDataDir CreateLibraryDir()
    {
        TempDataDir dir = new();
        File.WriteAllText(Path.Combine(dir.Path, "master.json"), "[]");
        return dir;
    }

    /// <summary>
    /// Registers a locator and a loader into <paramref name="services"/>, replacing whatever was registered before
    /// (the last registration wins).
    /// </summary>
    /// <param name="services">The test context's service collection.</param>
    /// <param name="libraryDir">The folder the locator points at, or null for an unconfigured library.</param>
    /// <param name="image">What <see cref="IAvatarLibraryLoader.ReadImageAsync"/> returns for any pick.</param>
    /// <returns>The loader, so a test can read which file name was requested.</returns>
    internal static PickLoader Register(IServiceCollection services, string? libraryDir, AvatarLibraryImage? image)
    {
        TeamOptions options = new();
        options.AvatarLibrary.Dir = libraryDir;
        services.AddSingleton(new AvatarLibraryLocator(Options.Create(options)));
        PickLoader loader = new(image);
        services.AddSingleton<IAvatarLibraryLoader>(loader);
        return loader;
    }

    /// <summary>Registers the unconfigured defaults a context needs just so a card or page that injects the locator renders.</summary>
    /// <param name="services">The test context's service collection.</param>
    internal static void RegisterUnconfigured(IServiceCollection services) => Register(services, null, null);

    /// <summary>A loader serving the 120-record fixture catalogue whose <see cref="ReadImageAsync"/> returns a fixed image or null.</summary>
    /// <param name="image">The image every read returns, or null for an unreadable pick.</param>
    internal sealed class PickLoader(AvatarLibraryImage? image) : IAvatarLibraryLoader
    {
        private readonly FakeAvatarLibraryLoader inner = new();

        /// <summary>The file name most recently passed to <see cref="ReadImageAsync"/>, or null before the first call.</summary>
        public string? LastFileName { get; private set; }

        /// <inheritdoc />
        public Task<AvatarLibraryLoadResult> LoadAsync(CancellationToken cancellationToken) => this.inner.LoadAsync(cancellationToken);

        /// <inheritdoc />
        public Task<AvatarLibraryImage?> ReadImageAsync(string fileName, CancellationToken cancellationToken)
        {
            this.LastFileName = fileName;
            return Task.FromResult(image);
        }
    }
}
