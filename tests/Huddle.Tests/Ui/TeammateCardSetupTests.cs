using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Skills;
using Agency.Huddle.App.TeamTypes;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Teammates;
using static Agency.Huddle.Tests.Ui.TeammateCardTestSupport;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// The Complete Setup additions of <see cref="Agency.Huddle.App.Components.Shared.TeammateCard"/> for a
/// Teammate hired from a team-type role (MS-1 to MS-5, NF-1, NF-6): the Role line, the Packs chip group,
/// the warnings and the lasting spend. A Teammate without a <c>_role</c> shows none of them.
/// </summary>
public sealed class TeammateCardSetupTests
{
    private const string Manifest = """
        { "schemaVersion": 1, "id": "web", "version": "1.2.0", "displayName": "Web", "order": 1,
          "workspace": { "kind": "none", "label": "None", "required": false },
          "processes": [{ "id": "flow", "file": "processes/flow.md", "default": true }],
          "roles": [
            { "id": "lead", "title": "Lead", "candidate": "candidates/lead.md", "manager": true, "required": true },
            { "id": "dev", "title": "Developer", "candidate": "candidates/dev.md", "maxPerTeam": 2,
              "model": { "tier": "balanced", "differentVendorFrom": ["lead"] }, "packs": ["msbuild-authoring"] }
          ] }
        """;

    private const string Lead = "---\nname: Boss\ntitle: Lead\nalias: boss\nteams: [Web Team]\nadapter: a1\n_role: 'web/lead@1.2.0'\n---\nYou lead.";

    private static readonly string[] PackSkills = ["target-authoring"];

    private static readonly VersionedRef[] PackRefs = [new VersionedRef("msbuild-authoring", "1.0.0")];

    /// <summary>Builds a Developer persona text with the given extra frontmatter lines.</summary>
    private static string Dev(string extra = "") =>
        $"---\nname: Dev\ntitle: Developer\nalias: dev\nteams: [Web Team]\nadapter: a1\n_role: 'web/dev@1.2.0'\n{extra}---\nYou build.";

    /// <summary>Registers the web team type and the msbuild pack into the context, as the real app's singletons would be.</summary>
    private static void AddTeamTypes(MudBunitContext ctx, TempDataDir dir)
    {
        Dictionary<string, string> builtins = new(StringComparer.Ordinal)
        {
            ["web/team-type.json"] = Manifest,
            ["web/candidates/lead.md"] = "---\ntitle: Lead\nconsult_when: planning\n---\nYou lead.",
            ["web/candidates/dev.md"] = "---\ntitle: Developer\nconsult_when: code changes\n---\nYou build things.",
            ["web/processes/flow.md"] = "# Flow",
        };
        ctx.Services.AddSingleton(new TeamTypeCatalog(dir.Options(), NullLogger<TeamTypeCatalog>.Instance, builtins));
        ctx.Services.AddSingleton(RoleFixtures.Packs(dir));
    }

    /// <summary>Opens the View card of <paramref name="name"/> and switches it to Edit.</summary>
    private static async Task<IRenderedComponent<ContainerFragment>> OpenEditAsync(TeamWebApplicationFactory factory, MudBunitContext ctx, string name)
    {
        IRenderedComponent<ContainerFragment> cut = await OpenViewCardAsync(ctx, factory, name);
        await ClickButtonAsync(cut, "Edit");
        return cut;
    }

    /// <summary>The Persona text box's current value.</summary>
    private static string PersonaText(IRenderedComponent<ContainerFragment> cut)
    {
        IElement area = FindInputControl(cut, "Persona text").QuerySelector("textarea") ?? throw new InvalidOperationException("No textarea.");
        return area.GetAttribute("value") ?? area.TextContent;
    }

    /// <summary>A hired Teammate's card names its role as type/role@version.</summary>
    [Fact]
    public async Task RoleLine_ShowsTypeRoleAndVersion()
    {
        using TempDataDir dir = new();
        await using TeamWebApplicationFactory factory = new();
        await factory.WriteDefinitionAsync("Dev", Dev(), CancellationToken.None);
        await using MudBunitContext ctx = NewContext(factory);
        AddTeamTypes(ctx, dir);

        IRenderedComponent<ContainerFragment> cut = await OpenEditAsync(factory, ctx, "Dev");

        Assert.Equal("Role: web/dev@1.2.0", cut.Find(".teammate-card-role").TextContent.Trim());
    }

    /// <summary>Clicking a pack adds its skill and its consult-when clause to the persona text, and records the pack.</summary>
    [Fact]
    public async Task PackToggle_AddsSkillClauseAndPack()
    {
        using TempDataDir dir = new();
        await using TeamWebApplicationFactory factory = new();
        await factory.WriteDefinitionAsync("Dev", Dev("consult_when: code changes\n"), CancellationToken.None);
        await using MudBunitContext ctx = NewContext(factory);
        AddTeamTypes(ctx, dir);
        IRenderedComponent<ContainerFragment> cut = await OpenEditAsync(factory, ctx, "Dev");

        await ClickAsync(cut, () => cut.FindAll(".teammate-card-pack").First(chip => chip.TextContent.Contains("MSBuild", StringComparison.Ordinal)));

        string text = PersonaText(cut);
        Assert.True(PersonaFrontmatter.TryReadIdentity(text, out PersonaIdentity? identity, out _));
        Assert.Equal(PackSkills, identity.Skills);
        Assert.Equal(PackRefs, identity.Packs);
        PersonaFrontmatterField consult = PersonaFrontmatter.Parse(text).Fields.First(field => string.Equals(field.Key, "consult_when", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("code changes; .csproj changes", consult.Value);
    }

    /// <summary>Clicking an active pack again removes its skill and its clause.</summary>
    [Fact]
    public async Task PackToggle_Twice_RemovesSkillAndClause()
    {
        using TempDataDir dir = new();
        await using TeamWebApplicationFactory factory = new();
        await factory.WriteDefinitionAsync("Dev", Dev("consult_when: code changes\n"), CancellationToken.None);
        await using MudBunitContext ctx = NewContext(factory);
        AddTeamTypes(ctx, dir);
        IRenderedComponent<ContainerFragment> cut = await OpenEditAsync(factory, ctx, "Dev");
        await ClickAsync(cut, () => cut.FindAll(".teammate-card-pack")[0]);

        await ClickAsync(cut, () => cut.FindAll(".teammate-card-pack")[0]);

        string text = PersonaText(cut);
        Assert.True(PersonaFrontmatter.TryReadIdentity(text, out PersonaIdentity? identity, out _));
        Assert.Empty(identity.Skills ?? []);
        Assert.Empty(identity.Packs ?? []);
        PersonaFrontmatterField consult = PersonaFrontmatter.Parse(text).Fields.First(field => string.Equals(field.Key, "consult_when", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("code changes", consult.Value);
    }

    /// <summary>Two members on one adapter whose role asks for a different vendor produce the vendor warning.</summary>
    [Fact]
    public async Task VendorWarning_ShownWhenSharingAnAdapterWithADifferentVendorRole()
    {
        using TempDataDir dir = new();
        await using TeamWebApplicationFactory factory = new();
        await factory.WriteDefinitionAsync("Boss", Lead, CancellationToken.None);
        await factory.WriteDefinitionAsync("Dev", Dev(), CancellationToken.None);
        await using MudBunitContext ctx = NewContext(factory);
        AddTeamTypes(ctx, dir);

        IRenderedComponent<ContainerFragment> cut = await OpenEditAsync(factory, ctx, "Dev");

        Assert.Equal(["Developer Dev runs on the same vendor as Lead Boss."], cut.FindAll(".teammate-card-warning").Select(alert => alert.TextContent.Trim()).ToList());
    }

    /// <summary>More than ten skills produce the skill-count warning.</summary>
    [Fact]
    public async Task ElevenSkills_ShowTheSkillCountWarning()
    {
        using TempDataDir dir = new();
        await using TeamWebApplicationFactory factory = new();
        string skills = string.Join(", ", Enumerable.Range(1, 11).Select(static i => $"s{i}"));
        await factory.WriteDefinitionAsync("Dev", Dev($"skills: [{skills}]\n"), CancellationToken.None);
        await using MudBunitContext ctx = NewContext(factory);
        AddTeamTypes(ctx, dir);

        IRenderedComponent<ContainerFragment> cut = await OpenEditAsync(factory, ctx, "Dev");

        Assert.Equal(["Dev has 11 skills; more than 10 makes skill choice harder."], cut.FindAll(".teammate-card-warning").Select(alert => alert.TextContent.Trim()).ToList());
    }

    /// <summary>Two skills the catalog files under one Sub Category produce the shared-Sub-Category warning.</summary>
    [Fact]
    public async Task SharedSubCategory_ShowsTheCatalogWarning()
    {
        using TempDataDir dir = new();
        await File.WriteAllTextAsync(Path.Combine(dir.Path, TeamPaths.SkillsCatalogFile), """
            [
              { "id": "a/b/alpha", "name": "alpha", "description": "A.", "publisher": "P", "repo": "https://github.com/a/b",
                "path": "alpha", "link": "l", "commit": "c", "license": "MIT", "category": "Cat", "subCategory": "Sub", "requires": [] },
              { "id": "a/b/beta", "name": "beta", "description": "B.", "publisher": "P", "repo": "https://github.com/a/b",
                "path": "beta", "link": "l", "commit": "c", "license": "MIT", "category": "Cat", "subCategory": "Sub", "requires": [] }
            ]
            """, Xunit.TestContext.Current.CancellationToken);
        await using TeamWebApplicationFactory factory = new();
        await factory.WriteDefinitionAsync("Dev", Dev("skills: [alpha, beta]\n"), CancellationToken.None);
        await using MudBunitContext ctx = NewContext(factory);
        AddTeamTypes(ctx, dir);
        ctx.Services.AddSingleton(new SkillsCatalogIndex(dir.Options(), NullLogger<SkillsCatalogIndex>.Instance));

        IRenderedComponent<ContainerFragment> cut = await OpenEditAsync(factory, ctx, "Dev");

        Assert.Equal(["alpha and beta are both in Sub."], cut.FindAll(".teammate-card-warning").Select(alert => alert.TextContent.Trim()).ToList());
    }

    /// <summary>The View card shows the lasting spend next to the since-start spend (NF-6).</summary>
    [Fact]
    public async Task ViewCard_ShowsPersistedSpend()
    {
        using TempDataDir dir = new();
        await using TeamWebApplicationFactory factory = new();
        await factory.WriteDefinitionAsync("Dev", Dev(), CancellationToken.None);
        factory.Services.GetRequiredService<PersonaSpend>().Add("Dev", "s1", 0.5m, "USD");
        await using MudBunitContext ctx = NewContext(factory);
        AddTeamTypes(ctx, dir);

        IRenderedComponent<ContainerFragment> cut = await OpenViewCardAsync(ctx, factory, "Dev");

        Assert.Equal("Total spent: 0.500 USD", cut.Find(".teammate-card-spend-total").TextContent.Trim());
    }

    /// <summary>A Teammate without a role shows no role line, packs, warnings or lasting spend.</summary>
    [Fact]
    public async Task NoRole_ShowsNoSetupAdditions()
    {
        using TempDataDir dir = new();
        await using TeamWebApplicationFactory factory = new();
        await SeedPersonaAsync(factory, "plain", "You help.");
        factory.Services.GetRequiredService<PersonaSpend>().Add("plain", "s1", 0.5m, "USD");
        await using MudBunitContext ctx = NewContext(factory);
        AddTeamTypes(ctx, dir);

        IRenderedComponent<ContainerFragment> view = await OpenViewCardAsync(ctx, factory, "plain");
        Assert.Empty(view.FindAll(".teammate-card-role"));
        Assert.Empty(view.FindAll(".teammate-card-spend-total"));

        await ClickButtonAsync(view, "Edit");
        Assert.Empty(view.FindAll(".teammate-card-pack"));
        Assert.Empty(view.FindAll(".teammate-card-warning"));
    }
}
