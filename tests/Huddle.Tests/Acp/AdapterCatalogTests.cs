using Microsoft.Extensions.Configuration;
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

    /// <summary>A configured profile's <c>EnvironmentOverrides</c> entries all project onto <see cref="AdapterProfile.EnvironmentOverrides"/>.</summary>
    [Fact]
    public void Profiles_ConfiguredWithEnvironmentOverrides_ProjectsAllEntries()
    {
        var acp = new AcpOptions
        {
            Adapters =
            [
                new AdapterProfileOptions
                {
                    Id = "agency",
                    DisplayName = "Agency",
                    Command = "agency-acp",
                    EnvironmentOverrides = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["Agent__DefaultModel"] = "sonnet",
                        ["Agent__ApiKeyEnv"] = "AGENCY_API_KEY",
                    },
                },
            ],
        };
        var options = Options.Create(new TeamOptions { Acp = acp });

        var catalog = new AdapterCatalog(options);

        AdapterProfile profile = Assert.Single(catalog.Profiles);
        Assert.NotNull(profile.EnvironmentOverrides);
        Assert.Equal(2, profile.EnvironmentOverrides.Count);
        Assert.Equal("sonnet", profile.EnvironmentOverrides["Agent__DefaultModel"]);
        Assert.Equal("AGENCY_API_KEY", profile.EnvironmentOverrides["Agent__ApiKeyEnv"]);
    }

    /// <summary>
    /// The synthesised legacy profile (no <c>Team:Acp:Adapters</c> configured) carries a null
    /// <c>EnvironmentOverrides</c> — Spec §4 P6: a stock installation must behave exactly as before.
    /// </summary>
    [Fact]
    public void Profiles_AdaptersNull_SynthesisedProfileHasNullEnvironmentOverrides()
    {
        var acp = new AcpOptions { Command = "node", AdapterPath = "index.js", Args = ["a", "b"] };
        var options = Options.Create(new TeamOptions { Acp = acp });

        var catalog = new AdapterCatalog(options);

        AdapterProfile profile = Assert.Single(catalog.Profiles);
        Assert.Null(profile.EnvironmentOverrides);
    }

    /// <summary>
    /// Binding from real configuration populates <c>EnvironmentOverrides</c> — proof the chosen
    /// options-class property type actually binds through <c>ConfigurationBinder</c>, not just
    /// that the projection compiles.
    /// </summary>
    [Fact]
    public void Profiles_BoundFromConfiguration_PopulatesEnvironmentOverrides()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Team:Acp:Adapters:0:Id"] = "agency",
                ["Team:Acp:Adapters:0:Command"] = "agency-acp",
                ["Team:Acp:Adapters:0:EnvironmentOverrides:Agent__DefaultModel"] = "sonnet",
            })
            .Build();
        var teamOptions = new TeamOptions();
        configuration.GetSection(TeamOptions.SectionName).Bind(teamOptions);
        var options = Options.Create(teamOptions);

        var catalog = new AdapterCatalog(options);

        AdapterProfile profile = Assert.Single(catalog.Profiles);
        Assert.NotNull(profile.EnvironmentOverrides);
        Assert.Equal("sonnet", profile.EnvironmentOverrides["Agent__DefaultModel"]);
    }

    /// <summary>
    /// The catalog is frozen at construction and <see cref="AdapterProfile"/> is handed to a Razor
    /// <c>[Parameter]</c>, so it must copy <see cref="AdapterProfileOptions.EnvironmentOverrides"/>
    /// rather than alias it: mutating the source dictionary after the catalog is built must not
    /// change the profile.
    /// </summary>
    [Fact]
    public void Profiles_SourceEnvironmentOverridesMutatedAfterConstruction_ProfileUnaffected()
    {
        var source = new Dictionary<string, string>(StringComparer.Ordinal) { ["Agent__DefaultModel"] = "sonnet" };
        var acp = new AcpOptions
        {
            Adapters =
            [
                new AdapterProfileOptions { Id = "agency", DisplayName = "Agency", Command = "agency-acp", EnvironmentOverrides = source },
            ],
        };
        var options = Options.Create(new TeamOptions { Acp = acp });

        var catalog = new AdapterCatalog(options);
        source["Agent__DefaultModel"] = "opus";
        source["Agent__New"] = "added-after-construction";

        AdapterProfile profile = Assert.Single(catalog.Profiles);
        Assert.NotNull(profile.EnvironmentOverrides);
        Assert.Equal("sonnet", profile.EnvironmentOverrides["Agent__DefaultModel"]);
        Assert.False(profile.EnvironmentOverrides.ContainsKey("Agent__New"));
    }

    /// <summary>
    /// An empty <c>EnvironmentOverrides</c> dictionary normalises to null, so
    /// <c>AgentProcessLauncher</c> skips its environment loop entirely and a stock install launches
    /// byte-identically.
    /// </summary>
    [Fact]
    public void Profiles_ConfiguredWithEmptyEnvironmentOverrides_NormalisesToNull()
    {
        var acp = new AcpOptions
        {
            Adapters =
            [
                new AdapterProfileOptions
                {
                    Id = "agency",
                    DisplayName = "Agency",
                    Command = "agency-acp",
                    EnvironmentOverrides = new Dictionary<string, string>(StringComparer.Ordinal),
                },
            ],
        };
        var options = Options.Create(new TeamOptions { Acp = acp });

        var catalog = new AdapterCatalog(options);

        AdapterProfile profile = Assert.Single(catalog.Profiles);
        Assert.Null(profile.EnvironmentOverrides);
    }

    /// <summary>The synthesised legacy profile (no <c>Team:Acp:Adapters</c> configured) carries <c>ReadsFiles</c> true — FC §6.11, Spec §4 P6: a stock installation reads its own files.</summary>
    [Fact]
    public void Legacy_ReadsFilesTrue()
    {
        var acp = new AcpOptions { Command = "node", AdapterPath = "index.js", Args = ["a", "b"] };
        var options = Options.Create(new TeamOptions { Acp = acp });

        var catalog = new AdapterCatalog(options);

        AdapterProfile profile = Assert.Single(catalog.Profiles);
        Assert.True(profile.ReadsFiles);
    }

    /// <summary>A configured Adapter entry with no <c>ReadsFiles</c> key defaults to true — FC §6.11.</summary>
    [Fact]
    public void Configured_ReadsFilesDefaultsTrue()
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

        AdapterProfile profile = Assert.Single(catalog.Profiles);
        Assert.True(profile.ReadsFiles);
    }

    /// <summary><c>Team:Acp:Adapters:0:ReadsFiles = false</c> binds through to the projected profile — FC §6.11, for an Adapter with no file tools such as <c>agency-acp</c>.</summary>
    [Fact]
    public void Configured_ReadsFilesFalse_Bound()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Team:Acp:Adapters:0:Id"] = "agency",
                ["Team:Acp:Adapters:0:Command"] = "agency-acp",
                ["Team:Acp:Adapters:0:ReadsFiles"] = "false",
            })
            .Build();
        var teamOptions = new TeamOptions();
        configuration.GetSection(TeamOptions.SectionName).Bind(teamOptions);
        var options = Options.Create(teamOptions);

        var catalog = new AdapterCatalog(options);

        AdapterProfile profile = Assert.Single(catalog.Profiles);
        Assert.False(profile.ReadsFiles);
    }

    /// <summary>The synthesised legacy profile (no <c>Team:Acp:Adapters</c> configured) carries <c>IsolateUserSettings</c> true — RS §6.10, finding P-11: a stock installation gets isolation.</summary>
    [Fact]
    public void Legacy_IsolateUserSettingsTrue()
    {
        var acp = new AcpOptions { Command = "node", AdapterPath = "index.js", Args = ["a", "b"] };
        var options = Options.Create(new TeamOptions { Acp = acp });

        var catalog = new AdapterCatalog(options);

        AdapterProfile profile = Assert.Single(catalog.Profiles);
        Assert.True(profile.IsolateUserSettings);
    }

    /// <summary>A configured Adapter entry with no <c>IsolateUserSettings</c> key defaults to false — finding P-11: an explicit <c>Adapters</c> list opts in.</summary>
    [Fact]
    public void Configured_IsolateUserSettingsDefaultsFalse()
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

        AdapterProfile profile = Assert.Single(catalog.Profiles);
        Assert.False(profile.IsolateUserSettings);
    }

    /// <summary>
    /// D28: the synthesised legacy profile (no <c>Team:Acp:Adapters</c> configured) carries
    /// <c>SessionPerRoom</c> true - finding P-9's default flip, now that Room Sessions' dependencies
    /// (D22-D27) all exist. Renamed from <c>Legacy_SessionPerRoomFalse_UntilD28</c>.
    /// </summary>
    [Fact]
    public void Legacy_SessionPerRoomTrue()
    {
        var acp = new AcpOptions { Command = "node", AdapterPath = "index.js", Args = ["a", "b"] };
        var options = Options.Create(new TeamOptions { Acp = acp });

        var catalog = new AdapterCatalog(options);

        AdapterProfile profile = Assert.Single(catalog.Profiles);
        Assert.True(profile.SessionPerRoom);
    }

    /// <summary>
    /// D28: a configured Adapter entry that does not set <c>SessionPerRoom</c> at all defaults to
    /// true, matching <see cref="AdapterProfileOptions.SessionPerRoom"/>'s own default. Renamed from
    /// <c>Configured_SessionPerRoomBound</c>.
    /// </summary>
    [Fact]
    public void Configured_SessionPerRoomDefaultsTrue()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Team:Acp:Adapters:0:Id"] = "claude",
                ["Team:Acp:Adapters:0:Command"] = "node",
            })
            .Build();
        var teamOptions = new TeamOptions();
        configuration.GetSection(TeamOptions.SectionName).Bind(teamOptions);
        var options = Options.Create(teamOptions);

        var catalog = new AdapterCatalog(options);

        AdapterProfile profile = Assert.Single(catalog.Profiles);
        Assert.True(profile.SessionPerRoom);
    }

    /// <summary>RS §6.12: a configured <c>agency-acp</c> entry must stay shared until V-5, so <c>SessionPerRoom: false</c> still binds through explicitly.</summary>
    [Fact]
    public void Configured_SessionPerRoomFalse_Bound()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Team:Acp:Adapters:0:Id"] = "agency",
                ["Team:Acp:Adapters:0:Command"] = "agency-acp",
                ["Team:Acp:Adapters:0:SessionPerRoom"] = "false",
            })
            .Build();
        var teamOptions = new TeamOptions();
        configuration.GetSection(TeamOptions.SectionName).Bind(teamOptions);
        var options = Options.Create(teamOptions);

        var catalog = new AdapterCatalog(options);

        AdapterProfile profile = Assert.Single(catalog.Profiles);
        Assert.False(profile.SessionPerRoom);
    }

    /// <summary>The synthesised legacy profile sends Prompt blocks to an Adapter that advertises it can take them.</summary>
    [Fact]
    public void Profiles_AdaptersNull_SynthesisedProfileAllowsPromptBlocks()
    {
        var options = Options.Create(new TeamOptions { Acp = new AcpOptions { Command = "node" } });

        var catalog = new AdapterCatalog(options);

        Assert.True(Assert.Single(catalog.Profiles).PromptBlocks);
    }

    /// <summary>A configured profile that does not mention <c>PromptBlocks</c> allows them: the Adapter's own advertisement is the gate.</summary>
    [Fact]
    public void Profiles_ConfiguredWithoutPromptBlocks_DefaultsTrue()
    {
        var acp = new AcpOptions { Adapters = [new AdapterProfileOptions { Id = "claude", Command = "node" }] };

        var catalog = new AdapterCatalog(Options.Create(new TeamOptions { Acp = acp }));

        Assert.True(Assert.Single(catalog.Profiles).PromptBlocks);
    }

    /// <summary><c>PromptBlocks: false</c> binds from <c>Team:Acp:Adapters:*:PromptBlocks</c> and is carried into the profile.</summary>
    [Fact]
    public void Profiles_PromptBlocksFalse_BoundFromConfigurationAndCarried()
    {
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Team:Acp:Adapters:0:Id"] = "agency",
                ["Team:Acp:Adapters:0:Command"] = "agency-acp",
                ["Team:Acp:Adapters:0:PromptBlocks"] = "false",
            })
            .Build();
        TeamOptions teamOptions = new();
        configuration.GetSection(TeamOptions.SectionName).Bind(teamOptions);

        var catalog = new AdapterCatalog(Options.Create(teamOptions));

        Assert.False(Assert.Single(catalog.Profiles).PromptBlocks);
    }

    /// <summary>The synthesised legacy profile allows the <c>compact</c> Adapter command and nothing else.</summary>
    [Fact]
    public void Profiles_AdaptersNull_SynthesisedProfileAllowsCompact()
    {
        var acp = new AcpOptions { Command = "node", AdapterPath = "index.js", Args = ["a", "b"] };
        var options = Options.Create(new TeamOptions { Acp = acp });

        var catalog = new AdapterCatalog(options);

        AdapterProfile profile = Assert.Single(catalog.Profiles);
        Assert.Equal(["compact"], profile.Commands);
    }

    /// <summary>A configured profile that lists no <c>Commands</c> allows none: absent means none.</summary>
    [Fact]
    public void Profiles_ConfiguredWithoutCommands_AllowsNone()
    {
        var acp = new AcpOptions
        {
            Adapters = [new AdapterProfileOptions { Id = "agency", Command = "agency-acp" }],
        };
        var options = Options.Create(new TeamOptions { Acp = acp });

        var catalog = new AdapterCatalog(options);

        AdapterProfile profile = Assert.Single(catalog.Profiles);
        Assert.Null(profile.Commands);
    }

    /// <summary>A configured profile copies its <c>Commands</c>, dropping blank entries.</summary>
    [Fact]
    public void Profiles_ConfiguredWithCommands_CopiesAndDropsBlankEntries()
    {
        var acp = new AcpOptions
        {
            Adapters = [new AdapterProfileOptions { Id = "claude", Command = "node", Commands = ["compact", " ", "", "init"] }],
        };
        var options = Options.Create(new TeamOptions { Acp = acp });

        var catalog = new AdapterCatalog(options);

        AdapterProfile profile = Assert.Single(catalog.Profiles);
        Assert.Equal(["compact", "init"], profile.Commands);
    }

    /// <summary>A profile never aliases the options collection: changing it afterwards does not change the profile.</summary>
    [Fact]
    public void Profiles_CommandsChangedAfterConstruction_ProfileIsUnchanged()
    {
        string[] configured = ["compact"];
        var entry = new AdapterProfileOptions { Id = "claude", Command = "node", Commands = configured };
        var options = Options.Create(new TeamOptions { Acp = new AcpOptions { Adapters = [entry] } });
        var catalog = new AdapterCatalog(options);

        configured[0] = "init";

        AdapterProfile profile = Assert.Single(catalog.Profiles);
        Assert.Equal(["compact"], profile.Commands);
    }

    /// <summary>Binding from real configuration populates <c>Commands</c> once, not doubled.</summary>
    [Fact]
    public void Profiles_BoundFromConfiguration_PopulatesCommands()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Team:Acp:Adapters:0:Id"] = "claude",
                ["Team:Acp:Adapters:0:Command"] = "node",
                ["Team:Acp:Adapters:0:Commands:0"] = "compact",
            })
            .Build();
        var teamOptions = new TeamOptions();
        configuration.GetSection(TeamOptions.SectionName).Bind(teamOptions);
        var options = Options.Create(teamOptions);

        var catalog = new AdapterCatalog(options);

        AdapterProfile profile = Assert.Single(catalog.Profiles);
        Assert.Equal(["compact"], profile.Commands);
    }
}
