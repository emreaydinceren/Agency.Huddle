using Agency.Huddle.App;
using Agency.Huddle.App.Search;
using Microsoft.Extensions.Configuration;

namespace Agency.Huddle.Tests.Search;

/// <summary>Pins <see cref="SearchOptions"/>' defaults and binding behaviour, per spec §10 and GS-T3.</summary>
public sealed class SearchOptionsTests
{
    /// <summary>The defaults match spec §10 exactly, the visit budget (OP-2, assumed) included.</summary>
    [Fact]
    public void Defaults_AreThoseInTheSpec()
    {
        SearchOptions options = new();

        Assert.Equal(4, options.LibraryParallelism);
        Assert.Equal(1048576L, options.MaxContentFileBytes);
        Assert.Equal(5000, options.MaxVisitedEntries);
        Assert.Equal(
            [".md", ".markdown", ".txt", ".json", ".yaml", ".yml", ".csv", ".log", ".xml", ".cs", ".js", ".ts", ".css", ".ps1"],
            options.EffectiveTextExtensions);
    }

    /// <summary>Binding a configured <c>TextExtensions</c> list replaces the default rather than appending to it.</summary>
    [Fact]
    public void Bind_TextExtensionsFromConfiguration_ReplacesTheDefaultRatherThanAppending()
    {
        Dictionary<string, string?> settings = new()
        {
            ["Team:Search:TextExtensions:0"] = ".rst",
        };

        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        TeamOptions teamOptions = new();
        configuration.GetSection(TeamOptions.SectionName).Bind(teamOptions);

        Assert.Equal([".rst"], teamOptions.Search.EffectiveTextExtensions);
    }

    /// <summary>An empty configured extension list falls back to the default rather than matching nothing.</summary>
    [Fact]
    public void EffectiveTextExtensions_EmptyConfigured_FallsBackToTheDefault()
    {
        SearchOptions options = new() { TextExtensions = [] };

        Assert.Equal(new SearchOptions().EffectiveTextExtensions, options.EffectiveTextExtensions);
    }

    /// <summary>A parallelism below one is raised to one, so the Library is always searched.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void EffectiveLibraryParallelism_BelowOne_IsRaisedToOne(int configured)
    {
        SearchOptions options = new() { LibraryParallelism = configured };

        Assert.Equal(1, options.EffectiveLibraryParallelism);
    }

    /// <summary>Scalar keys under <c>Team:Search</c> are read into their properties.</summary>
    [Fact]
    public void Bind_ScalarsFromConfiguration_AreRead()
    {
        Dictionary<string, string?> settings = new()
        {
            ["Team:Search:LibraryParallelism"] = "2",
            ["Team:Search:MaxContentFileBytes"] = "10",
            ["Team:Search:MaxVisitedEntries"] = "7",
        };

        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        TeamOptions teamOptions = new();
        configuration.GetSection(TeamOptions.SectionName).Bind(teamOptions);

        Assert.Equal(2, teamOptions.Search.LibraryParallelism);
        Assert.Equal(10L, teamOptions.Search.MaxContentFileBytes);
        Assert.Equal(7, teamOptions.Search.MaxVisitedEntries);
    }
}
