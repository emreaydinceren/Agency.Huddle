using Microsoft.Extensions.Configuration;
using Agency.Huddle.App;
using Agency.Huddle.App.Library;

namespace Agency.Huddle.Tests.Library;

/// <summary>Tests for <see cref="LibraryOptions"/> and <see cref="TeamsOptions"/> binding and defaults.</summary>
public sealed class LibraryOptionsTests
{
    /// <summary>Default values match the Spec §7 defaults.</summary>
    [Fact]
    public void Defaults_MatchSpec()
    {
        TeamOptions options = new();

        Assert.Null(options.Library.Roots);
        Assert.True(options.Library.Enabled);
        Assert.Equal(2097152, options.Library.MaxEditableBytes);
        Assert.Equal(5000, options.Library.MaxIndexedFiles);
        Assert.Equal(10, options.Library.MaxReferencedDocuments);
        Assert.Equal(16384, options.Library.MaxInlineBytes);
        Assert.Equal("Teams", options.Teams.Dir);
    }

    /// <summary>Configuration binding reads Library and Teams options.</summary>
    [Fact]
    public void Bind_FromConfiguration_ReadsTeamLibraryAndTeams()
    {
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Team:Library:Roots:0:Name"] = "Docs",
                ["Team:Library:Roots:0:Path"] = "C:/docs",
                ["Team:Library:MaxInlineBytes"] = "100",
                ["Team:Teams:Dir"] = "T",
            })
            .Build();

        TeamOptions options = new();
        configuration.GetSection(TeamOptions.SectionName).Bind(options);

        Assert.NotNull(options.Library.Roots);
        PinnedRootOption root = Assert.Single(options.Library.Roots);
        Assert.Equal("Docs", root.Name);
        Assert.Equal("C:/docs", root.Path);
        Assert.Equal(100, options.Library.MaxInlineBytes);
        Assert.Equal("T", options.Teams.Dir);
    }

    /// <summary>The Prompt-block limits default to the design's §6.7 values.</summary>
    [Fact]
    public void ImageLimits_Defaults_MatchDesign()
    {
        LibraryOptions options = new TeamOptions().Library;

        Assert.Equal(3145728, options.MaxImageBytes);
        Assert.Equal(4, options.MaxImagesPerTurn);
        Assert.Equal(8388608, options.MaxImageBytesPerTurn);
        Assert.Equal(8000, options.MaxImageEdgePixels);
    }

    /// <summary>The Prompt-block limits bind from <c>Team:Library</c>.</summary>
    [Fact]
    public void ImageLimits_Bind_FromConfiguration()
    {
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Team:Library:MaxImageBytes"] = "1000",
                ["Team:Library:MaxImagesPerTurn"] = "2",
                ["Team:Library:MaxImageBytesPerTurn"] = "3000",
                ["Team:Library:MaxImageEdgePixels"] = "512",
            })
            .Build();

        TeamOptions options = new();
        configuration.GetSection(TeamOptions.SectionName).Bind(options);

        Assert.Equal((1000, 2, 3000, 512), (options.Library.MaxImageBytes, options.Library.MaxImagesPerTurn, options.Library.MaxImageBytesPerTurn, options.Library.MaxImageEdgePixels));
    }

    /// <summary>Default MaxMemoryEntries is 50.</summary>
    [Fact]
    public void MaxMemoryEntries_Default_Is50()
    {
        TeamOptions options = new();

        Assert.Equal(50, options.Teams.MaxMemoryEntries);
    }

    /// <summary>MaxMemoryEntries binds from Team:Teams configuration section.</summary>
    [Fact]
    public void MaxMemoryEntries_Binds_FromTeamTeamsSection()
    {
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Team:Teams:MaxMemoryEntries"] = "7",
            })
            .Build();

        TeamOptions options = new();
        configuration.GetSection(TeamOptions.SectionName).Bind(options);

        Assert.Equal(7, options.Teams.MaxMemoryEntries);
    }

    /// <summary>MaxMemoryEntries of zero is allowed.</summary>
    [Fact]
    public void MaxMemoryEntries_Zero_IsAllowed()
    {
        TeamOptions options = new();
        options.Teams.MaxMemoryEntries = 0;

        Assert.Equal(0, options.Teams.MaxMemoryEntries);
    }
}
