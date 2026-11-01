using System.Reflection;
using Agency.Huddle.App;
using Agency.Huddle.App.HiringPool;

namespace Agency.Huddle.Tests.TeamTypes;

/// <summary>Tests for <see cref="EmbeddedResources"/> against the real resources embedded in the App assembly.</summary>
public sealed class EmbeddedResourcesTests
{
    private const string Prefix = "HiringPool/";

    private static readonly Assembly AppAssembly = typeof(HiringPoolCatalog).Assembly;

    /// <summary>Keys are the part after the prefix, separated by forward slashes, never backslashes.</summary>
    [Fact]
    public void ReadFolder_RealPrefix_KeysAreSlashSeparatedRemainders()
    {
        Dictionary<string, string> files = EmbeddedResources.ReadFolder(AppAssembly, Prefix);

        Assert.NotEmpty(files);
        Assert.All(files.Keys, key =>
        {
            Assert.DoesNotContain('\\', key);
            Assert.False(key.StartsWith(Prefix, StringComparison.Ordinal));
        });
        Assert.Contains(files.Keys, static key => key.Contains('/'));
    }

    /// <summary>Folder text comes back with LF line endings only.</summary>
    [Fact]
    public void ReadFolder_RealPrefix_TextHasNoCarriageReturns()
    {
        Dictionary<string, string> files = EmbeddedResources.ReadFolder(AppAssembly, Prefix);

        Assert.NotEmpty(files);
        Assert.All(files.Values, static text => Assert.DoesNotContain('\r', text));
    }

    /// <summary>The include predicate sees the remainder and drops entries it rejects.</summary>
    [Fact]
    public void ReadFolder_IncludePredicate_FiltersOnRemainder()
    {
        Dictionary<string, string> all = EmbeddedResources.ReadFolder(AppAssembly, Prefix);
        Dictionary<string, string> json = EmbeddedResources.ReadFolder(AppAssembly, Prefix, static rest => rest.EndsWith(".json", StringComparison.Ordinal));

        Assert.NotEmpty(json);
        Assert.True(json.Count < all.Count);
        Assert.All(json.Keys, static key => Assert.EndsWith(".json", key, StringComparison.Ordinal));
    }

    /// <summary>A prefix that matches nothing gives an empty map.</summary>
    [Fact]
    public void ReadFolder_UnknownPrefix_ReturnsEmpty()
    {
        Assert.Empty(EmbeddedResources.ReadFolder(AppAssembly, "No/Such/Folder/"));
    }

    /// <summary>A missing resource throws an InvalidOperationException that names it.</summary>
    [Fact]
    public void ReadText_MissingName_ThrowsNamingTheResource()
    {
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => EmbeddedResources.ReadText(AppAssembly, "No/Such/File.txt"));

        Assert.Contains("No/Such/File.txt", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>Open on a missing resource throws, naming it.</summary>
    [Fact]
    public void Open_MissingName_ThrowsNamingTheResource()
    {
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => EmbeddedResources.Open(AppAssembly, "No/Such/File.txt"));

        Assert.Contains("No/Such/File.txt", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>A real resource reads as non-empty LF-only text.</summary>
    [Fact]
    public void ReadText_RealResource_ReturnsLfText()
    {
        string text = EmbeddedResources.ReadText(AppAssembly, "Skills/SkillsCatalog.json");

        Assert.NotEmpty(text);
        Assert.DoesNotContain('\r', text);
    }
}
