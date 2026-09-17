using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;

namespace Agency.Huddle.Tests.Acp;

/// <summary>Tests for <see cref="AdapterProfileResolver"/>.</summary>
public sealed class AdapterProfileResolverTests
{
    /// <summary>A <see langword="null"/> Adapter id resolves to the default profile with no warning.</summary>
    [Fact]
    public void Resolve_NullId_ReturnsDefaultProfileWithNoWarning()
    {
        AdapterProfileResolver resolver = AdapterProfileResolverTests.CreateResolver(out AdapterCatalog catalog);

        (AdapterProfile profile, string? warning) = resolver.Resolve(null);

        Assert.Same(catalog.Default, profile);
        Assert.Null(warning);
    }

    /// <summary>A whitespace Adapter id resolves to the default profile with no warning.</summary>
    [Fact]
    public void Resolve_WhitespaceId_ReturnsDefaultProfileWithNoWarning()
    {
        AdapterProfileResolver resolver = AdapterProfileResolverTests.CreateResolver(out AdapterCatalog catalog);

        (AdapterProfile profile, string? warning) = resolver.Resolve("   ");

        Assert.Same(catalog.Default, profile);
        Assert.Null(warning);
    }

    /// <summary>An id matching a profile only by case resolves to that profile with no warning.</summary>
    [Fact]
    public void Resolve_IdDiffersOnlyByCase_ReturnsMatchingProfileWithNoWarning()
    {
        AdapterProfileResolver resolver = AdapterProfileResolverTests.CreateResolver(out AdapterCatalog catalog);

        (AdapterProfile profile, string? warning) = resolver.Resolve("AGENCY");

        Assert.Same(catalog.Find("agency"), profile);
        Assert.Null(warning);
    }

    /// <summary>An unknown id resolves to the default profile, with a warning naming both ids (P4).</summary>
    [Fact]
    public void Resolve_UnknownId_ReturnsDefaultProfileWithWarningNamingBothIds()
    {
        AdapterProfileResolver resolver = AdapterProfileResolverTests.CreateResolver(out AdapterCatalog catalog);

        (AdapterProfile profile, string? warning) = resolver.Resolve("ghost");

        Assert.Same(catalog.Default, profile);
        Assert.NotNull(warning);
        Assert.Contains("ghost", warning, StringComparison.Ordinal);
        Assert.Contains(catalog.Default.Id, warning, StringComparison.Ordinal);
    }

    /// <summary>Builds a resolver over a catalog with two configured profiles, "agency" first (the default).</summary>
    /// <param name="catalog">Receives the catalog the resolver was built over, for assertions.</param>
    private static AdapterProfileResolver CreateResolver(out AdapterCatalog catalog)
    {
        AcpOptions acp = new()
        {
            Adapters =
            [
                new AdapterProfileOptions { Id = "agency", DisplayName = "Agency", Command = "agency-acp" },
                new AdapterProfileOptions { Id = "claude", DisplayName = "Claude", Command = "node" },
            ],
        };
        IOptions<TeamOptions> options = Options.Create(new TeamOptions { Acp = acp });

        catalog = new AdapterCatalog(options);
        return new AdapterProfileResolver(catalog);
    }
}
