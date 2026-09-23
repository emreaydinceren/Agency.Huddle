using Microsoft.Extensions.Configuration;
using Agency.Huddle.App;
using Agency.Huddle.App.FileChanges;

namespace Agency.Huddle.Tests.FileChanges;

/// <summary>Pins <see cref="FileChangesOptions"/>'s defaults and its binding behaviour, per FC §6.14.</summary>
public sealed class FileChangesOptionsTests
{
    /// <summary>The defaults match FC §6.14 exactly.</summary>
    [Fact]
    public void Defaults_AreThoseInTheSpec()
    {
        FileChangesOptions options = new();

        Assert.True(options.Enabled);
        Assert.Equal([".git", "node_modules", "bin", "obj"], options.EffectiveIgnore);
        Assert.Equal(5000, options.MaxFilesPerFolder);
        Assert.Equal(50, options.MaxListed);
        Assert.Equal(100, options.MaxMemoryEntries);
    }

    /// <summary>Binding a configured <c>Ignore</c> list replaces the default rather than appending to it.</summary>
    [Fact]
    public void Bind_IgnoreFromConfiguration_ReplacesTheDefaultRatherThanAppending()
    {
        Dictionary<string, string?> settings = new()
        {
            ["Team:FileChanges:Ignore:0"] = "dist",
        };

        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        TeamOptions teamOptions = new();
        configuration.GetSection(TeamOptions.SectionName).Bind(teamOptions);

        Assert.Equal(["dist"], teamOptions.FileChanges.EffectiveIgnore);
    }
}
