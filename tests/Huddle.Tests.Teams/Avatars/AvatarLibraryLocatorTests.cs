using Agency.Huddle.App;
using Agency.Huddle.App.Avatars;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.Tests.Avatars;

/// <summary>
/// Pins <see cref="AvatarLibraryLocator"/>: the library is configured only when a folder is named and
/// that folder holds a <c>master.json</c>. Each test works in its own temporary folder, removed on dispose.
/// </summary>
public sealed class AvatarLibraryLocatorTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "huddle-avatar-locator-" + Guid.NewGuid().ToString("N"));

    /// <summary>Creates the per-test temporary root that every case below builds its folders under.</summary>
    public AvatarLibraryLocatorTests()
    {
        Directory.CreateDirectory(this.root);
    }

    /// <summary>Removes the per-test temporary root and everything the test placed under it.</summary>
    public void Dispose()
    {
        if (Directory.Exists(this.root))
        {
            Directory.Delete(this.root, recursive: true);
        }
    }

    /// <summary>With no <c>Dir</c> configured, the library is not configured and has no folder.</summary>
    [Fact]
    public void IsConfigured_DirUnset_IsFalseAndDirIsNull()
    {
        AvatarLibraryLocator locator = Create(null);

        Assert.False(locator.IsConfigured);
        Assert.Null(locator.Dir);
    }

    /// <summary>A <c>Dir</c> that is only whitespace counts as unset.</summary>
    [Fact]
    public void IsConfigured_DirBlank_IsFalseAndDirIsNull()
    {
        AvatarLibraryLocator locator = Create("   ");

        Assert.False(locator.IsConfigured);
        Assert.Null(locator.Dir);
    }

    /// <summary>A surrounding-whitespace <c>Dir</c> is exposed trimmed, so the path used is the path named.</summary>
    [Fact]
    public void Dir_PaddedPath_IsTrimmed()
    {
        string library = Path.Combine(this.root, "library");
        Directory.CreateDirectory(library);
        AvatarLibraryLocator locator = Create("  " + library + "  ");

        Assert.Equal(library, locator.Dir);
    }

    /// <summary>A configured <c>Dir</c> whose folder does not exist is not a library.</summary>
    [Fact]
    public void IsConfigured_FolderMissing_IsFalse()
    {
        string library = Path.Combine(this.root, "missing");
        AvatarLibraryLocator locator = Create(library);

        Assert.False(locator.IsConfigured);
    }

    /// <summary>A configured folder with no <c>master.json</c> in it is not a library.</summary>
    [Fact]
    public void IsConfigured_FolderWithoutMasterJson_IsFalse()
    {
        string library = Path.Combine(this.root, "empty");
        Directory.CreateDirectory(library);
        AvatarLibraryLocator locator = Create(library);

        Assert.False(locator.IsConfigured);
    }

    /// <summary>A configured folder holding a <c>master.json</c> is a library, and is detected without parsing it.</summary>
    [Fact]
    public void IsConfigured_FolderWithMasterJson_IsTrue()
    {
        string library = Path.Combine(this.root, "library");
        Directory.CreateDirectory(library);
        File.WriteAllText(Path.Combine(library, "master.json"), "{}");
        AvatarLibraryLocator locator = Create(library);

        Assert.True(locator.IsConfigured);
        Assert.Equal(library, locator.Dir);
    }

    /// <summary>The bundled library is the folder named <c>AvatarLibrary</c> beside the app, where the build copies it.</summary>
    [Fact]
    public void BundledDir_IsTheAvatarLibraryFolderBesideTheApp()
    {
        Assert.Equal(Path.Combine(AppContext.BaseDirectory, "AvatarLibrary"), AvatarLibraryLocator.BundledDir);
    }

    /// <summary>With no <c>Dir</c> configured, the bundled folder is the library.</summary>
    [Fact]
    public void Dir_Unset_UsesTheBundledFolder()
    {
        string bundled = this.MakeLibrary("bundled");
        AvatarLibraryLocator locator = Create(null, bundled);

        Assert.Equal(bundled, locator.Dir);
        Assert.True(locator.IsConfigured);
    }

    /// <summary>A blank <c>Dir</c> counts as unset, so the bundled folder is the library.</summary>
    [Fact]
    public void Dir_Blank_UsesTheBundledFolder()
    {
        string bundled = this.MakeLibrary("bundled");
        AvatarLibraryLocator locator = Create("   ", bundled);

        Assert.Equal(bundled, locator.Dir);
    }

    /// <summary>A configured <c>Dir</c> wins over the bundled folder: the operator's library replaces the shipped one.</summary>
    [Fact]
    public void Dir_Configured_WinsOverTheBundledFolder()
    {
        string bundled = this.MakeLibrary("bundled");
        string custom = this.MakeLibrary("custom");
        AvatarLibraryLocator locator = Create(custom, bundled);

        Assert.Equal(custom, locator.Dir);
    }

    /// <summary>
    /// A configured <c>Dir</c> that is not a library turns the picker off, and the bundled folder is not a fallback for it.
    /// This is how an operator who does not want the shipped library switches it off.
    /// </summary>
    [Fact]
    public void IsConfigured_ConfiguredFolderMissing_DoesNotFallBackToTheBundledFolder()
    {
        string bundled = this.MakeLibrary("bundled");
        AvatarLibraryLocator locator = Create(Path.Combine(this.root, "no-such-folder"), bundled);

        Assert.False(locator.IsConfigured);
    }

    /// <summary>With neither a <c>Dir</c> nor a bundled folder there is no library.</summary>
    [Fact]
    public void Dir_UnsetAndNoBundledFolder_IsNull()
    {
        AvatarLibraryLocator locator = Create(null, null);

        Assert.Null(locator.Dir);
        Assert.False(locator.IsConfigured);
    }

    /// <summary>Makes a folder under the per-test root holding a <c>master.json</c>, and returns its path.</summary>
    private string MakeLibrary(string name)
    {
        string library = Path.Combine(this.root, name);
        Directory.CreateDirectory(library);
        File.WriteAllText(Path.Combine(library, "master.json"), "{}");
        return library;
    }

    private static AvatarLibraryLocator Create(string? dir, string? bundledDir = null)
    {
        TeamOptions options = new()
        {
            AvatarLibrary = new AvatarLibraryOptions { Dir = dir },
        };
        return new AvatarLibraryLocator(Options.Create(options), bundledDir);
    }
}
