using Agency.Huddle.App.TeamTypes;
using Agency.Huddle.App.Teams;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.Teams;

/// <summary>Tests for <see cref="CardResolver"/>: every default on a hire card resolves, or the card is Needs setup (HR-6, HR-7).</summary>
public sealed class CardResolverTests
{
    private static readonly string[] NoNames = [];

    private static readonly string[] NoAvoid = [];

    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<ModelTier, string>> NoTiers =
        new Dictionary<string, IReadOnlyDictionary<ModelTier, string>>(StringComparer.Ordinal);

    private static readonly IReadOnlyList<AdapterAvailability> OneAdapter = [new("claude", ["sonnet"])];

    /// <summary>A connector status that reports exactly the listed connectors as connected.</summary>
    /// <param name="connected">The connector ids that are connected.</param>
    private sealed class FakeConnectors(params string[] connected) : IConnectorStatus
    {
        /// <inheritdoc />
        public bool IsConnected(string connector) => connected.Contains(connector, StringComparer.Ordinal);
    }

    private static RoleSpec Role(IReadOnlyList<string>? requires = null, IReadOnlyList<SkillRef>? skills = null, IReadOnlyList<string>? packs = null)
    {
        return new RoleSpec("developer", "Developer", "candidates/developer.md", Skills: skills, Packs: packs, Requires: requires, SuggestedNames: ["Jane", "Priya"]);
    }

    private static CardState Resolve(RoleSpec role, IReadOnlyCollection<string>? namesInUse = null, IReadOnlyList<AdapterAvailability>? answered = null,
        ISkillAvailability? skills = null, IConnectorStatus? connectors = null, string? chosenName = null)
    {
        return CardResolver.Resolve(role, namesInUse ?? CardResolverTests.NoNames, answered ?? CardResolverTests.OneAdapter, CardResolverTests.NoTiers,
            CardResolverTests.NoAvoid, skills ?? new FakeSkillAvailability(), connectors ?? new FakeConnectors(), chosenName);
    }

    /// <summary>A role with defaults for everything is Ready with the first suggested name and a pick.</summary>
    [Fact]
    public void Resolve_AllDefaultsExist_IsReady()
    {
        CardState state = CardResolverTests.Resolve(CardResolverTests.Role());

        Assert.Equal(CardReadiness.Ready, state.Readiness);
        Assert.Empty(state.Reasons);
        Assert.Equal("Jane", state.Name);
        Assert.Equal("developer", state.RoleId);
        Assert.Equal(new ModelPick("claude", null), state.Pick);
    }

    /// <summary>A suggested name already in use (compared without case) is skipped for the next one.</summary>
    [Fact]
    public void Resolve_FirstNameInUse_UsesNextSuggestion()
    {
        CardState state = CardResolverTests.Resolve(CardResolverTests.Role(), namesInUse: ["jane"]);

        Assert.Equal("Priya", state.Name);
        Assert.Equal(CardReadiness.Ready, state.Readiness);
    }

    /// <summary>When every suggested name is used the card is Needs setup with a name conflict.</summary>
    [Fact]
    public void Resolve_AllNamesInUse_NeedsSetupNameConflict()
    {
        CardState state = CardResolverTests.Resolve(CardResolverTests.Role(), namesInUse: ["Jane", "PRIYA"]);

        Assert.Equal(CardReadiness.NeedsSetup, state.Readiness);
        Assert.Equal(["name conflict"], state.Reasons);
    }

    /// <summary>With no adapter that answered, the card is Needs setup naming the tier.</summary>
    [Fact]
    public void Resolve_NoAdapter_NeedsSetupWithTier()
    {
        CardState state = CardResolverTests.Resolve(CardResolverTests.Role(), answered: []);

        Assert.Null(state.Pick);
        Assert.Equal(CardReadiness.NeedsSetup, state.Readiness);
        Assert.Equal(["no logged-in adapter for tier Balanced"], state.Reasons);
    }

    /// <summary>Only the connector that is not connected is listed, and a connector alone never blocks: the card is Ready with no reasons.</summary>
    [Fact]
    public void Resolve_OneConnectorUnmet_IsReadyAndListsOnlyThatConnector()
    {
        CardState state = CardResolverTests.Resolve(CardResolverTests.Role(requires: ["email:read", "calendar:read"]), connectors: new FakeConnectors("email:read"));

        Assert.Equal(CardReadiness.Ready, state.Readiness);
        Assert.Empty(state.Reasons);
        Assert.Equal(["calendar:read"], state.Unconnected);
    }

    /// <summary>A missing Skill still blocks when a connector is also unconnected; the connector is only listed, never a reason.</summary>
    [Fact]
    public void Resolve_SkillMissingAndConnectorUnmet_NeedsSetupForTheSkillOnly()
    {
        FakeSkillAvailability skills = new FakeSkillAvailability().WithSkill("tdd", SkillAvailability.NotDownloaded);

        CardState state = CardResolverTests.Resolve(CardResolverTests.Role(requires: ["email:read"], skills: [new SkillRef("tdd", "builtin", null)]), skills: skills);

        Assert.Equal(CardReadiness.NeedsSetup, state.Readiness);
        Assert.NotEmpty(state.SkillSetup);
        Assert.DoesNotContain(state.Reasons, reason => reason.Contains("email:read", StringComparison.Ordinal));
        Assert.Equal(["email:read"], state.Unconnected);
    }

    /// <summary>Pack skills merge into the role skills, de-duplicated by name, and the packs are listed at their version.</summary>
    [Fact]
    public void Resolve_PacksMerged_SkillsDeduplicatedAndPacksPinned()
    {
        SkillRef own = new("tdd", "builtin", null);
        FakeSkillAvailability skills = new FakeSkillAvailability()
            .WithPack("msbuild-authoring", "1.2.0", "MSBuild", ["tdd", "msbuild"])
            .WithPack("unrelated", "1.0.0", "Other", ["other-skill"]);

        CardState state = CardResolverTests.Resolve(CardResolverTests.Role(skills: [own], packs: ["msbuild-authoring", "missing-pack"]), skills: skills);

        Assert.Equal(["tdd", "msbuild"], state.Skills.Select(static s => s.Name).ToArray());
        Assert.Equal([new VersionedRef("msbuild-authoring", "1.2.0")], state.Packs);
        Assert.Equal(CardReadiness.Ready, state.Readiness);
        Assert.Empty(state.SkillSetup);
    }

    /// <summary><see cref="CardResolver.SkillSetupOf"/> gives the same items a resolved card carries, so the Candidate check and a hire card agree.</summary>
    [Fact]
    public void SkillSetupOf_RoleSkillMissing_MatchesResolvedCard()
    {
        FakeSkillAvailability skills = new FakeSkillAvailability().WithSkill("tdd", SkillAvailability.Unknown);
        RoleSpec role = CardResolverTests.Role(skills: [new SkillRef("tdd", "builtin", null)]);

        IReadOnlyList<SkillSetupItem> setup = CardResolver.SkillSetupOf(role, skills);

        Assert.Equal(CardResolverTests.Resolve(role, skills: skills).SkillSetup, setup);
        Assert.Single(setup);
    }

    /// <summary>A known Pack whose Skills are not downloaded makes the card Needs setup, with one Pack item and the reason
    /// naming the Pack's title (Skill Packs spec §6.10.2).</summary>
    [Fact]
    public void Resolve_PackNotDownloaded_NeedsSetupWithTheReason()
    {
        FakeSkillAvailability skills = new FakeSkillAvailability()
            .WithPack("msbuild-authoring", "1.0.0", "MSBuild Authoring", ["msbuild", "targets"])
            .WithSkill("msbuild", SkillAvailability.NotDownloaded)
            .WithSkill("targets", SkillAvailability.NotDownloaded);

        CardState state = CardResolverTests.Resolve(CardResolverTests.Role(packs: ["msbuild-authoring"]), skills: skills);

        Assert.Equal(CardReadiness.NeedsSetup, state.Readiness);
        Assert.Equal(["Pack \"MSBuild Authoring\" not downloaded"], state.Reasons);
        SkillSetupItem item = Assert.Single(state.SkillSetup);
        Assert.Equal(new SkillSetupItem(SkillSetupKind.Pack, "msbuild-authoring", SkillAvailability.NotDownloaded, "MSBuild Authoring"), item);
    }

    /// <summary>A role Skill in each not-ready state gives the matching item and reason; Present and Copyable give none.</summary>
    [Theory]
    [InlineData("Present", null)]
    [InlineData("Copyable", null)]
    [InlineData("NotDownloaded", "Skill \"review\" not downloaded")]
    [InlineData("Unknown", "Skill \"review\" is not in any catalog")]
    [InlineData("Broken", "Skill \"review\" is broken: S002 no SKILL.md")]
    [InlineData("Withdrawn", "Skill \"review\" was withdrawn by its Source: licence changed")]
    public void Resolve_RoleSkillState_GivesTheMatchingItem(string stateName, string? reason)
    {
        SkillAvailability state = Enum.Parse<SkillAvailability>(stateName);
        string detail = state switch
        {
            SkillAvailability.Broken => "S002 no SKILL.md",
            SkillAvailability.Withdrawn => "licence changed",
            _ => string.Empty,
        };
        FakeSkillAvailability skills = new FakeSkillAvailability().WithSkill("review", state, detail);

        CardState card = CardResolverTests.Resolve(CardResolverTests.Role(skills: [new SkillRef("review", "builtin", null)]), skills: skills);

        if (reason is null)
        {
            Assert.Equal(CardReadiness.Ready, card.Readiness);
            Assert.Empty(card.SkillSetup);
        }
        else
        {
            Assert.Equal(CardReadiness.NeedsSetup, card.Readiness);
            Assert.Equal([reason], card.Reasons);
            Assert.Equal(new SkillSetupItem(SkillSetupKind.Skill, "review", state, detail), Assert.Single(card.SkillSetup));
        }
    }

    /// <summary>A withdrawn role Skill blocks a new hire: the card is Needs setup (Skill Packs spec §6.10.8).</summary>
    [Fact]
    public void CardResolver_WithdrawnSkill_NeedsSetup()
    {
        FakeSkillAvailability skills = new FakeSkillAvailability().WithSkill("review", SkillAvailability.Withdrawn, "pulled");

        CardState card = CardResolverTests.Resolve(CardResolverTests.Role(skills: [new SkillRef("review", "builtin", null)]), skills: skills);

        Assert.Equal(CardReadiness.NeedsSetup, card.Readiness);
        Assert.Equal(SkillAvailability.Withdrawn, Assert.Single(card.SkillSetup).Problem);
    }

    /// <summary>A Pack id no row provides is skipped without a setup item, so it neither blocks the hire nor appears.</summary>
    [Fact]
    public void Resolve_PackNoRowProvides_SkippedWithoutSetup()
    {
        CardState card = CardResolverTests.Resolve(CardResolverTests.Role(packs: ["gone"]), skills: new FakeSkillAvailability { Default = SkillAvailability.Unknown });

        Assert.Equal(CardReadiness.Ready, card.Readiness);
        Assert.Empty(card.Packs);
        Assert.Empty(card.SkillSetup);
    }

    /// <summary>A Skill ref's source and path are ignored: two refs with the same name and a different source and path resolve
    /// the same, by name, to a registry ref.</summary>
    [Fact]
    public void Resolve_SkillRefSourceAndPath_AreIgnored()
    {
        CardState github = CardResolverTests.Resolve(CardResolverTests.Role(skills: [new SkillRef("review", "acme/skills", "skills/review")]));
        CardState local = CardResolverTests.Resolve(CardResolverTests.Role(skills: [new SkillRef("review", "builtin", null)]));

        Assert.Equal(github.Skills, local.Skills);
        Assert.Equal(new SkillRef("review", CardResolver.RegistrySource, null), Assert.Single(github.Skills));
    }

    /// <summary>An explicit chosen name that is already in use is Needs setup, not silently replaced.</summary>
    [Fact]
    public void Resolve_ChosenNameInUse_NeedsSetup()
    {
        CardState state = CardResolverTests.Resolve(CardResolverTests.Role(), namesInUse: ["Ravi"], chosenName: "ravi");

        Assert.Equal("ravi", state.Name);
        Assert.Equal(CardReadiness.NeedsSetup, state.Readiness);
        Assert.Equal(["name conflict"], state.Reasons);
    }

    /// <summary>An explicit chosen name that is free is used as given.</summary>
    [Fact]
    public void Resolve_ChosenNameFree_IsUsed()
    {
        CardState state = CardResolverTests.Resolve(CardResolverTests.Role(), chosenName: "Zed");

        Assert.Equal("Zed", state.Name);
        Assert.Equal(CardReadiness.Ready, state.Readiness);
    }

    /// <summary>Several problems are all reported, in name, adapter order; the unconnected connector is listed apart, not as a reason.</summary>
    [Fact]
    public void Resolve_SeveralProblems_ReportsAll()
    {
        CardState state = CardResolverTests.Resolve(CardResolverTests.Role(requires: ["email:read"]), namesInUse: ["Jane", "Priya"], answered: []);

        Assert.Equal(CardReadiness.NeedsSetup, state.Readiness);
        Assert.Equal(["name conflict", "no logged-in adapter for tier Balanced"], state.Reasons);
        Assert.Equal(["email:read"], state.Unconnected);
    }

    /// <summary>A stock install (no catalog, every name unknown, no Pack provided) still resolves the built-in developer's
    /// card: its Pack is skipped without a setup item, and only its pooled Skills, which are not on this computer yet, make it
    /// Needs setup (Skill Packs spec §6.10.2, principle 6).</summary>
    [Fact]
    public void Resolve_RealDevelopmentDeveloper_StockInstall_PackSkippedSkillsNeedSetup()
    {
        RoleSpec role = RealDeveloper(out TempDataDir dir);
        using TempDataDir owned = dir;

        CardState state = CardResolverTests.Resolve(role, skills: new FakeSkillAvailability { Default = SkillAvailability.Unknown });

        Assert.Empty(state.Packs);
        Assert.Equal(CardReadiness.NeedsSetup, state.Readiness);
        Assert.All(state.SkillSetup, static item => Assert.Equal(SkillSetupKind.Skill, item.Kind));
        Assert.Equal(role.Skills.Select(static skill => skill.Name), state.SkillSetup.Select(static item => item.Name));
    }

    /// <summary>A stock install whose developer Skills are in Skills/ resolves the built-in developer to Ready, without its
    /// Pack.</summary>
    [Fact]
    public void Resolve_RealDevelopmentDeveloper_SkillsPresentNoPack_IsReady()
    {
        RoleSpec role = RealDeveloper(out TempDataDir dir);
        using TempDataDir owned = dir;

        CardState state = CardResolverTests.Resolve(role, skills: new FakeSkillAvailability());

        Assert.Equal(CardReadiness.Ready, state.Readiness);
        Assert.Empty(state.Packs);
    }

    /// <summary>The built-in development type's developer role, from the real catalog over a fresh data folder.</summary>
    private static RoleSpec RealDeveloper(out TempDataDir dir)
    {
        dir = new TempDataDir();
        TeamTypeCatalog catalog = new(dir.Options(), NullLogger<TeamTypeCatalog>.Instance);
        return Assert.IsType<TeamTypeManifest>(catalog.Find("development")).Roles.Single(static r => r.Id == "developer");
    }

    /// <summary>The built-in developer role resolves Ready from the real catalog, with its msbuild pack.</summary>
    [Fact]
    public void Resolve_RealDevelopmentDeveloper_IsReady()
    {
        using TempDataDir dir = new();
        TeamTypeCatalog catalog = new(dir.Options(), NullLogger<TeamTypeCatalog>.Instance);
        TeamTypeManifest? type = catalog.Find("development");
        Assert.NotNull(type);
        RoleSpec role = type.Roles.Single(static r => r.Id == "developer");
        FakeSkillAvailability skills = new FakeSkillAvailability().WithPack("msbuild-authoring", "1.0.0", "MSBuild", ["msbuild"]);

        CardState state = CardResolverTests.Resolve(role, skills: skills);

        Assert.Equal(CardReadiness.Ready, state.Readiness);
        Assert.Equal("Jane", state.Name);
        Assert.Single(state.Packs);
    }

    /// <summary>The empty connector status reports nothing connected.</summary>
    [Fact]
    public void EmptyConnectorStatus_IsConnected_AlwaysFalse()
    {
        EmptyConnectorStatus status = new();

        Assert.False(status.IsConnected("email:read"));
    }
}
