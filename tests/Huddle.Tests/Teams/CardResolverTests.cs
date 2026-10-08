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
        IReadOnlyList<SkillPack>? packs = null, IConnectorStatus? connectors = null, string? chosenName = null)
    {
        return CardResolver.Resolve(role, namesInUse ?? CardResolverTests.NoNames, answered ?? CardResolverTests.OneAdapter, CardResolverTests.NoTiers,
            CardResolverTests.NoAvoid, packs ?? [], connectors ?? new FakeConnectors(), chosenName);
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

    /// <summary>Only the connector that is not connected is reported.</summary>
    [Fact]
    public void Resolve_OneConnectorUnmet_NeedsSetupNamingIt()
    {
        CardState state = CardResolverTests.Resolve(CardResolverTests.Role(requires: ["email:read", "calendar:read"]), connectors: new FakeConnectors("email:read"));

        Assert.Equal(CardReadiness.NeedsSetup, state.Readiness);
        Assert.Equal(["connector calendar:read not connected"], state.Reasons);
    }

    /// <summary>Pack skills merge into the role skills, de-duplicated by name, and the packs are listed at their version.</summary>
    [Fact]
    public void Resolve_PacksMerged_SkillsDeduplicatedAndPacksPinned()
    {
        SkillRef own = new("tdd", "builtin", null);
        SkillPack pack = new("msbuild-authoring", "1.2.0", "MSBuild", [], "", [new SkillRef("tdd", "builtin", null), new SkillRef("msbuild", "builtin", null)]);
        SkillPack other = new("unrelated", "1.0.0", "Other", [], "", [new SkillRef("other-skill", "builtin", null)]);

        CardState state = CardResolverTests.Resolve(CardResolverTests.Role(skills: [own], packs: ["msbuild-authoring", "missing-pack"]), packs: [pack, other]);

        Assert.Equal(["tdd", "msbuild"], state.Skills.Select(static s => s.Name).ToArray());
        Assert.Equal([new VersionedRef("msbuild-authoring", "1.2.0")], state.Packs);
        Assert.Equal(CardReadiness.Ready, state.Readiness);
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

    /// <summary>Several problems are all reported, in name, adapter, connector order.</summary>
    [Fact]
    public void Resolve_SeveralProblems_ReportsAll()
    {
        CardState state = CardResolverTests.Resolve(CardResolverTests.Role(requires: ["email:read"]), namesInUse: ["Jane", "Priya"], answered: []);

        Assert.Equal(["name conflict", "no logged-in adapter for tier Balanced", "connector email:read not connected"], state.Reasons);
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
        SkillPack pack = new("msbuild-authoring", "1.0.0", "MSBuild", [], "", [new SkillRef("msbuild", "builtin", null)]);

        CardState state = CardResolverTests.Resolve(role, packs: [pack]);

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
