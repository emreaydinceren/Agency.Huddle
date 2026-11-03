using Microsoft.Extensions.Configuration;
using Agency.Huddle.App;
using Agency.Huddle.App.Library;

namespace Agency.Huddle.Tests.Library;

/// <summary>Pins the Second Brain configuration keys and their defaults, per Spec §7.4.</summary>
public sealed class SecondBrainOptionsTests
{
    /// <summary>The Raw root folder defaults to <c>Raw</c> under DataDir.</summary>
    [Fact]
    public void Raw_Dir_DefaultsToRaw()
    {
        Assert.Equal("Raw", new LibraryOptions().Raw.Dir);
    }

    /// <summary>The Archive root folder defaults to <c>Archive</c>.</summary>
    [Fact]
    public void Archive_Dir_DefaultsToArchive()
    {
        Assert.Equal("Archive", new LibraryOptions().Archive.Dir);
    }

    /// <summary>The Prompt override folder defaults to <c>Prompts</c>.</summary>
    [Fact]
    public void Prompts_Dir_DefaultsToPrompts()
    {
        Assert.Equal("Prompts", new LibraryOptions().Prompts.Dir);
    }

    /// <summary>The per-file AGENTS.md cut defaults to 8000 characters.</summary>
    [Fact]
    public void AgentsMd_MaxCharsPerFile_DefaultsTo8000()
    {
        Assert.Equal(8000, new TeamOptions().AgentsMd.MaxCharsPerFile);
    }

    /// <summary>The whole AGENTS.md block budget defaults to 32000 characters.</summary>
    [Fact]
    public void AgentsMd_MaxTotalChars_DefaultsTo32000()
    {
        Assert.Equal(32000, new TeamOptions().AgentsMd.MaxTotalChars);
    }

    /// <summary>The Huddle-wide Memory index cap defaults to 30 entries.</summary>
    [Fact]
    public void Teams_MaxHuddleMemoryEntries_DefaultsTo30()
    {
        Assert.Equal(30, new TeamsOptions().MaxHuddleMemoryEntries);
    }

    /// <summary>The Outputs index cap defaults to 40 entries.</summary>
    [Fact]
    public void Outputs_MaxIndexEntries_DefaultsTo40()
    {
        Assert.Equal(40, new TeamOptions().Outputs.MaxIndexEntries);
    }

    /// <summary>The brain-check quiet threshold defaults to 60 days.</summary>
    [Fact]
    public void SecondBrain_QuietDays_DefaultsTo60()
    {
        Assert.Equal(60, new TeamOptions().SecondBrain.QuietDays);
    }

    /// <summary>The Raw, Archive and Prompts root kinds exist.</summary>
    [Fact]
    public void LibraryRootKind_HasRawArchivePrompts()
    {
        string[] names = Enum.GetNames<LibraryRootKind>();

        Assert.Contains("Raw", names);
        Assert.Contains("Archive", names);
        Assert.Contains("Prompts", names);
    }

    /// <summary>The Uploads root kind is retired in favour of Raw.</summary>
    [Fact]
    public void LibraryRootKind_NoLongerHasUploads()
    {
        string[] names = Enum.GetNames<LibraryRootKind>();

        Assert.DoesNotContain("Uploads", names);
    }

    /// <summary>The Raw folder binds from <c>Team:Library:Raw:Dir</c>.</summary>
    [Fact]
    public void Raw_Dir_BindsFromConfiguration()
    {
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Team:Library:Raw:Dir"] = "In",
            })
            .Build();

        TeamOptions options = new();
        configuration.GetSection(TeamOptions.SectionName).Bind(options);

        Assert.Equal("In", options.Library.Raw.Dir);
    }
}
