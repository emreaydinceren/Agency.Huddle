using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;

namespace Agency.Huddle.Tests.Acp;

/// <summary>Tests for <see cref="AdapterCatalog"/>.</summary>
public sealed class AdapterCatalogTests
{
    /// <summary>With no <c>Adapters</c> configured, the catalog synthesises one profile from the legacy keys.</summary>
    [Fact]
    public void Profiles_AdaptersNull_SynthesisesOneClaudeProfile()
    {
        var acp = new AcpOptions { Command = "node", AdapterPath = "index.js", Args = ["a", "b"] };
        var options = Options.Create(new TeamOptions { Acp = acp });

        var catalog = new AdapterCatalog(options);

        AdapterProfile profile = Assert.Single(catalog.Profiles);
        Assert.Equal("claude", profile.Id);
        Assert.Equal("Claude", profile.DisplayName);
        Assert.True(profile.UsesToolNamePrefix);
        Assert.Equal(acp.Command, profile.Command);
        Assert.Equal(acp.Args, profile.Args);
        Assert.Equal(acp.AdapterPath, profile.AdapterPath);
    }

    /// <summary>An empty <c>Adapters</c> list is treated the same as a null one.</summary>
    [Fact]
    public void Profiles_AdaptersEmpty_SynthesisesOneClaudeProfile()
    {
        var acp = new AcpOptions { Command = "node", AdapterPath = "index.js", Args = ["a", "b"], Adapters = [] };
        var options = Options.Create(new TeamOptions { Acp = acp });

        var catalog = new AdapterCatalog(options);

        AdapterProfile profile = Assert.Single(catalog.Profiles);
        Assert.Equal("claude", profile.Id);
        Assert.Equal("Claude", profile.DisplayName);
        Assert.True(profile.UsesToolNamePrefix);
        Assert.Equal(acp.Command, profile.Command);
        Assert.Equal(acp.Args, profile.Args);
        Assert.Equal(acp.AdapterPath, profile.AdapterPath);
    }

    /// <summary>Two configured profiles preserve configuration order, and the first one is the default.</summary>
    [Fact]
    public void Profiles_TwoConfigured_PreservesOrderAndDefaultsToFirst()
    {
        var acp = new AcpOptions
        {
            Adapters =
            [
                new AdapterProfileOptions { Id = "agency", DisplayName = "Agency", Command = "agency-acp" },
                new AdapterProfileOptions { Id = "claude", DisplayName = "Claude", Command = "node" },
            ],
        };
        var options = Options.Create(new TeamOptions { Acp = acp });

        var catalog = new AdapterCatalog(options);

        Assert.Equal(2, catalog.Profiles.Count);
        Assert.Equal("agency", catalog.Profiles[0].Id);
        Assert.Equal("claude", catalog.Profiles[1].Id);
        Assert.Equal("agency", catalog.Default.Id);
    }

    /// <summary><see cref="AdapterCatalog.Find"/> matches ordinal, case-insensitively, and misses cleanly.</summary>
    [Fact]
    public void Find_MatchesOrdinalIgnoreCase_AndReturnsNullForUnknownId()
    {
        var acp = new AcpOptions
        {
            Adapters =
            [
                new AdapterProfileOptions { Id = "agency", DisplayName = "Agency", Command = "agency-acp" },
            ],
        };
        var options = Options.Create(new TeamOptions { Acp = acp });

        var catalog = new AdapterCatalog(options);

        Assert.NotNull(catalog.Find("AGENCY"));
        Assert.Equal("agency", catalog.Find("AGENCY")!.Id);
        Assert.Null(catalog.Find("unknown"));
    }

    /// <summary>A configured profile with a blank <c>Command</c> is a startup error naming the profile's id.</summary>
    [Fact]
    public void Constructor_BlankCommand_ThrowsNamingTheProfileId()
    {
        var acp = new AcpOptions
        {
            Adapters =
            [
                new AdapterProfileOptions { Id = "agency", DisplayName = "Agency", Command = "   " },
            ],
        };
        var options = Options.Create(new TeamOptions { Acp = acp });

        var exception = Assert.Throws<InvalidOperationException>(() => new AdapterCatalog(options));

        Assert.Contains("agency", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Two configured profiles sharing an id, differing only in case, are a startup error.</summary>
    [Fact]
    public void Constructor_DuplicateId_ThrowsNamingTheProfileId()
    {
        var acp = new AcpOptions
        {
            Adapters =
            [
                new AdapterProfileOptions { Id = "agency", DisplayName = "Agency", Command = "agency-acp" },
                new AdapterProfileOptions { Id = "AGENCY", DisplayName = "Agency again", Command = "agency-acp" },
            ],
        };
        var options = Options.Create(new TeamOptions { Acp = acp });

        var exception = Assert.Throws<InvalidOperationException>(() => new AdapterCatalog(options));

        Assert.Contains("agency", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
