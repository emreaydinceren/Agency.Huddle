using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.TeamTypes;
using Agency.Huddle.App.Teams;
using static Agency.Huddle.Tests.Ui.AgentProfileCardTestSupport;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// The Complete Setup additions of <see cref="Agency.Huddle.App.Components.Shared.AgentProfileCard"/> for a
/// Agent hired from a team-type role (MS-1 to MS-5, NF-1, NF-6): the Role line, the Packs chip group,
/// the warnings and the lasting spend. An Agent without a <c>_role</c> shows none of them.
/// </summary>
public sealed class AgentProfileCardSetupTests
{
    private static readonly string Manifest = Factories.TeamTypeJson.Manifest("web", "Web", order: 1, version: "1.2.0", rolesJson: """
        [
            { "id": "lead", "title": "Lead", "candidate": "candidates/lead.md", "manager": true, "required": true },
            { "id": "dev", "title": "Developer", "candidate": "candidates/dev.md", "maxPerTeam": 2,
              "model": { "tier": "balanced", "differentVendorFrom": ["lead"] }, "packs": ["msbuild-authoring"] }
        ]
        """);

    private const string Lead = "---\nname: Boss\ntitle: Lead\nalias: boss\nteams: [Web Team]\nadapter: a1\n_role: 'web/lead@1.2.0'\n---\nYou lead.";

    private static readonly string[] PackSkills = ["target-authoring"];

    private static readonly VersionedRef[] PackRefs = [new VersionedRef("msbuild-authoring", "1.0.0")];

    /// <summary>Builds a Developer persona text with the given extra frontmatter lines.</summary>
    private static string Dev(string extra = "") =>
        $"---\nname: Dev\ntitle: Developer\nalias: dev\nteams: [Web Team]\nadapter: a1\n_role: 'web/dev@1.2.0'\n{extra}---\nYou build.";

    /// <summary>Registers the web team type, and a registry whose SKILLS.md lists the msbuild Pack (its Skill downloaded) and,
    /// when given, a catalog, into the context, as the real app's singletons would be.</summary>
    private static async Task<Agency.Huddle.Tests.SkillPacks.RegistryService.RegistryHost> AddRegistryAsync(MudBunitContext ctx, string? catalogJson = null)
    {
        Agency.Huddle.Tests.SkillPacks.RegistryService.RegistryHost host = new();
        host.Source();
        string skill = host.CloneSkill("target-authoring").Replace("\\", "\\\\", StringComparison.Ordinal);
        Agency.Huddle.Tests.SkillPacks.RegistryService.RegistryHost.Write(host.Dir, "packs/msbuild-authoring.skillpack.json", $$"""
            { "schemaVersion": 1, "id": "msbuild-authoring", "version": "1.0.0", "title": "MSBuild", "source": "local/test", "appliesTo": ["dev"],
              "consultWhenAdds": ".csproj changes", "skills": [{ "name": "target-authoring", "path": "{{skill}}", "origin": "https://github.com/o/r/tree/0123456789abcdef0123456789abcdef01234567/skills/target-authoring" }] }
            """);
        string[] catalogs = [];
        if (catalogJson is not null)
        {
            Agency.Huddle.Tests.SkillPacks.RegistryService.RegistryHost.Write(host.Dir, "catalogs/subs.skillcatalog.json", catalogJson);
            catalogs = ["catalogs/subs.skillcatalog.json"];
        }

        host.SkillsMd(["packs/msbuild-authoring.skillpack.json"], catalogs);
        await host.ReadAsync();
        ctx.Services.AddSingleton(host.Registry);
        return host;
    }

    /// <summary>Registers the web team type into the context, as the real app's singleton would be.</summary>
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

    /// <summary>A hired Agent's card names its role as type/role@version.</summary>
    [Fact]
    public async Task RoleLine_ShowsTypeRoleAndVersion()
    {
        using TempDataDir dir = new();
        await using TeamWebApplicationFactory factory = new();
        await factory.WriteDefinitionAsync("Dev", Dev(), CancellationToken.None);
        await using MudBunitContext ctx = NewContext(factory);
        AddTeamTypes(ctx, dir);

        IRenderedComponent<ContainerFragment> cut = await OpenEditAsync(factory, ctx, "Dev");

        Assert.Equal("Role: web/dev@1.2.0", cut.Find(".agent-profile-card-role").TextContent.Trim());
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
        using Agency.Huddle.Tests.SkillPacks.RegistryService.RegistryHost host = await AddRegistryAsync(ctx);
        IRenderedComponent<ContainerFragment> cut = await OpenEditAsync(factory, ctx, "Dev");

        await ClickAsync(cut, () => cut.FindAll(".agent-profile-card-pack").First(chip => chip.TextContent.Contains("MSBuild", StringComparison.Ordinal)));

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
        using Agency.Huddle.Tests.SkillPacks.RegistryService.RegistryHost host = await AddRegistryAsync(ctx);
        IRenderedComponent<ContainerFragment> cut = await OpenEditAsync(factory, ctx, "Dev");
        await ClickAsync(cut, () => cut.FindAll(".agent-profile-card-pack")[0]);

        await ClickAsync(cut, () => cut.FindAll(".agent-profile-card-pack")[0]);

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

        Assert.Equal(["Developer Dev runs on the same vendor as Lead Boss."], cut.FindAll(".agent-profile-card-warning").Select(alert => alert.TextContent.Trim()).ToList());
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

        Assert.Equal(["Dev has 11 skills; more than 10 makes skill choice harder."], cut.FindAll(".agent-profile-card-warning").Select(alert => alert.TextContent.Trim()).ToList());
    }

    /// <summary>Two skills a registry catalog files under one Sub Category produce the shared-Sub-Category warning.</summary>
    [Fact]
    public async Task SharedSubCategory_ShowsTheCatalogWarning()
    {
        using TempDataDir dir = new();
        await using TeamWebApplicationFactory factory = new();
        await factory.WriteDefinitionAsync("Dev", Dev("skills: [alpha, beta]\n"), CancellationToken.None);
        await using MudBunitContext ctx = NewContext(factory);
        AddTeamTypes(ctx, dir);
        using Agency.Huddle.Tests.SkillPacks.RegistryService.RegistryHost host = await AddRegistryAsync(ctx, """
            { "schemaVersion": 1, "id": "subs", "title": "Subs", "source": "local/test", "skills": [
              { "name": "alpha", "description": "A.", "origin": "https://github.com/a/b/tree/0123456789abcdef0123456789abcdef01234567/alpha", "license": "MIT", "category": "Cat", "subCategory": "Sub" },
              { "name": "beta", "description": "B.", "origin": "https://github.com/a/b/tree/0123456789abcdef0123456789abcdef01234567/beta", "license": "MIT", "category": "Cat", "subCategory": "Sub" } ] }
            """);

        IRenderedComponent<ContainerFragment> cut = await OpenEditAsync(factory, ctx, "Dev");

        Assert.Equal(["alpha and beta are both in Sub."], cut.FindAll(".agent-profile-card-warning").Select(alert => alert.TextContent.Trim()).ToList());
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

        Assert.Equal("Total spent: 0.500 USD", cut.Find(".agent-profile-card-spend-total").TextContent.Trim());
    }

    /// <summary>An Agent without a role shows no role line, packs or warnings, but still shows its lasting spend (NF-6).</summary>
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
        Assert.Empty(view.FindAll(".agent-profile-card-role"));
        Assert.Equal("Total spent: 0.500 USD", view.Find(".agent-profile-card-spend-total").TextContent.Trim());

        await ClickButtonAsync(view, "Edit");
        Assert.Empty(view.FindAll(".agent-profile-card-pack"));
        Assert.Empty(view.FindAll(".agent-profile-card-warning"));
    }

    /// <summary>An Agent without a role and with nothing stored shows no lasting spend line.</summary>
    [Fact]
    public async Task NoRole_NothingStored_ShowsNoSpendTotal()
    {
        using TempDataDir dir = new();
        await using TeamWebApplicationFactory factory = new();
        await SeedPersonaAsync(factory, "quiet", "You help.");
        await using MudBunitContext ctx = NewContext(factory);
        AddTeamTypes(ctx, dir);

        IRenderedComponent<ContainerFragment> view = await OpenViewCardAsync(ctx, factory, "quiet");

        Assert.Empty(view.FindAll(".agent-profile-card-spend-total"));
    }
}
