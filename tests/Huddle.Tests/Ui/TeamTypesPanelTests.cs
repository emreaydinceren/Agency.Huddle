using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Components.Pages;
using Agency.Huddle.App.HiringPool;
using Agency.Huddle.App.TeamTypes;

namespace Agency.Huddle.Tests.Ui;

/// <summary>Tests for the Settings Team Types panel: the valid-type table, the Problems list, Reload and the <c>/settings/teamtypes</c> deep link (TT-4).</summary>
public sealed class TeamTypesPanelTests
{
    private const string Candidate = """
        ---
        title: Developer
        consult_when: code changes
        ---
        You build things.
        """;

    private static string Manifest(string id, string name, int order, string version = "1.0.0")
    {
        string json = """
            { "schemaVersion": 1, "id": "@ID", "version": "@VERSION", "displayName": "@NAME", "order": @ORDER,
              "workspace": { "kind": "none", "label": "None", "required": false },
              "processes": [{ "id": "flow", "file": "processes/flow.md", "default": true }],
              "roles": [
                { "id": "lead", "title": "Lead", "candidate": "candidates/lead.md", "manager": true, "required": true },
                { "id": "dev", "title": "Developer", "candidate": "candidates/lead.md", "maxPerTeam": 3 }] }
            """;
        return json
            .Replace("@ID", id, StringComparison.Ordinal)
            .Replace("@VERSION", version, StringComparison.Ordinal)
            .Replace("@NAME", name, StringComparison.Ordinal)
            .Replace("@ORDER", order.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    private static Dictionary<string, string> Builtins()
    {
        return new(StringComparer.Ordinal)
        {
            ["alpha/team-type.json"] = Manifest("alpha", "Alpha Team", 10, "2.1.0"),
            ["alpha/candidates/lead.md"] = Candidate,
            ["alpha/processes/flow.md"] = "# Flow",
            ["broken/team-type.json"] = "{ not json",
        };
    }

    private static TeamTypeCatalog Create(TempDataDir dir, Dictionary<string, string> builtins)
    {
        return new TeamTypeCatalog(dir.Options(), NullLogger<TeamTypeCatalog>.Instance, builtins);
    }

    private static void WriteUser(TempDataDir dir, string relativePath, string text)
    {
        string path = Path.Combine(dir.Path, "TeamTypes", relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? dir.Path);
        File.WriteAllText(path, text);
    }

    private static IRenderedComponent<ContainerFragment> RenderPanel(MudBunitContext ctx, TeamTypeCatalog catalog)
    {
        ctx.Services.AddSingleton(catalog);
        return ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<Agency.Huddle.App.Components.Settings.TeamTypesPanel>(0);
            builder.CloseComponent();
        });
    }

    /// <summary>A job the hiring pool rejected is listed with its reason beside the type problems, so a bad pool entry is not silent.</summary>
    [Fact]
    public async Task Panel_PoolProblem_IsListedWithItsReason()
    {
        using TempDataDir dir = new();
        Dictionary<string, string> poolFiles = new(StringComparer.Ordinal)
        {
            ["hiringpool.json"] = """{ "schemaVersion": 1, "jobs": [ { "id": "ghost", "title": "Ghost", "persona": "personas/missing.md" } ] }""",
        };
        HiringPoolCatalog pool = new(dir.Options(), NullLogger<HiringPoolCatalog>.Instance, poolFiles);
        TeamTypeCatalog catalog = new(dir.Options(), NullLogger<TeamTypeCatalog>.Instance, Builtins(), pool);
        await using MudBunitContext ctx = new();

        IRenderedComponent<ContainerFragment> cut = RenderPanel(ctx, catalog);

        IElement problem = cut.Find(".teamtypes-pool-problem");
        Assert.Equal("ghost", problem.QuerySelector(".teamtypes-problem-id")?.TextContent.Trim());
        Assert.Equal("The job has no persona file at 'personas/missing.md'.", problem.QuerySelector(".teamtypes-problem-message")?.TextContent.Trim());
    }

    /// <summary>Reload rereads the hiring pool, not only the types, so a job added to the user pool shows without a restart.</summary>
    [Fact]
    public async Task Reload_RereadsTheHiringPool()
    {
        using TempDataDir dir = new();
        HiringPoolCatalog pool = new(dir.Options(), NullLogger<HiringPoolCatalog>.Instance, new Dictionary<string, string>(StringComparer.Ordinal));
        TeamTypeCatalog catalog = new(dir.Options(), NullLogger<TeamTypeCatalog>.Instance, Builtins(), pool);
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = RenderPanel(ctx, catalog);
        string poolDir = Path.Combine(dir.Path, "HiringPool");
        Directory.CreateDirectory(Path.Combine(poolDir, "personas"));
        File.WriteAllText(Path.Combine(poolDir, "hiringpool.json"), """{ "schemaVersion": 1, "jobs": [ { "id": "tutor", "title": "Tutor" } ] }""");
        File.WriteAllText(Path.Combine(poolDir, "personas", "tutor.md"), "---\ntitle: 'Tutor'\n---\nYou teach.");
        Assert.Null(pool.FindJob("tutor"));

        await cut.InvokeAsync(() => cut.Find("button.teamtypes-reload").ClickAsync());

        Assert.Equal("Tutor", pool.FindJob("tutor")?.Title);
        Assert.Contains(catalog.Find("alpha")?.Roles ?? [], static role => role.Id == "tutor" && role.Pooled);
    }

    private static string CellText(IElement row, string cellClass)
    {
        return row.QuerySelector("td." + cellClass)?.TextContent.Trim() ?? string.Empty;
    }

    /// <summary>A valid type renders one row with display name, id, version, source and role count.</summary>
    [Fact]
    public async Task Panel_ValidBuiltinType_RendersRow()
    {
        using TempDataDir dir = new();
        await using MudBunitContext ctx = new();

        IRenderedComponent<ContainerFragment> cut = RenderPanel(ctx, Create(dir, Builtins()));

        IElement row = Assert.Single(cut.FindAll("tr.teamtypes-row"));
        Assert.Equal("Alpha Team", CellText(row, "teamtypes-name"));
        Assert.Equal("alpha", CellText(row, "teamtypes-id"));
        Assert.Equal("2.1.0", CellText(row, "teamtypes-version"));
        Assert.Equal("Built-in", CellText(row, "teamtypes-source"));
        Assert.Equal("2", CellText(row, "teamtypes-roles"));
    }

    /// <summary>A type whose manifest comes from the user's TeamTypes folder is labelled Yours.</summary>
    [Fact]
    public async Task Panel_UserManifest_RendersSourceYours()
    {
        using TempDataDir dir = new();
        WriteUser(dir, "alpha/team-type.json", Manifest("alpha", "My Alpha", 5, "3.0.0"));
        await using MudBunitContext ctx = new();

        IRenderedComponent<ContainerFragment> cut = RenderPanel(ctx, Create(dir, Builtins()));

        IElement row = Assert.Single(cut.FindAll("tr.teamtypes-row"));
        Assert.Equal("My Alpha", CellText(row, "teamtypes-name"));
        Assert.Equal("Yours", CellText(row, "teamtypes-source"));
    }

    /// <summary>A rejected type renders in the Problems list with its id, file and reason.</summary>
    [Fact]
    public async Task Panel_Problem_RendersIdFileAndMessage()
    {
        using TempDataDir dir = new();
        await using MudBunitContext ctx = new();

        IRenderedComponent<ContainerFragment> cut = RenderPanel(ctx, Create(dir, Builtins()));

        IElement problem = Assert.Single(cut.FindAll(".teamtypes-problem"));
        Assert.Equal("broken", problem.QuerySelector(".teamtypes-problem-id")?.TextContent.Trim());
        Assert.Equal("builtin:broken/team-type.json", problem.QuerySelector(".teamtypes-problem-file")?.TextContent.Trim());
        Assert.Equal("The manifest is not valid team-type JSON.", problem.QuerySelector(".teamtypes-problem-message")?.TextContent.Trim());
        Assert.Empty(cut.FindAll(".teamtypes-no-problems"));
    }

    /// <summary>With no types and no problems both empty states show.</summary>
    [Fact]
    public async Task Panel_NothingLoaded_ShowsBothEmptyStates()
    {
        using TempDataDir dir = new();
        await using MudBunitContext ctx = new();

        IRenderedComponent<ContainerFragment> cut = RenderPanel(ctx, Create(dir, new Dictionary<string, string>(StringComparer.Ordinal)));

        Assert.NotEmpty(cut.FindAll(".mud-simple-table"));
        Assert.Empty(cut.FindAll(".mud-table-head"));
        Assert.NotEmpty(cut.FindAll("tbody tr .empty-state"));
        Assert.Equal("No team types found.", cut.Find(".empty-state [role=status]").TextContent.Trim());
        Assert.Equal("No problems.", cut.Find(".problem-list-empty").TextContent.Trim());
        Assert.Empty(cut.FindAll("tr.teamtypes-row"));
    }

    /// <summary>Reload rereads the folder: a type written after the first render appears once Reload is clicked.</summary>
    [Fact]
    public async Task Panel_Reload_RereadsTheTeamTypesFolder()
    {
        using TempDataDir dir = new();
        Dictionary<string, string> builtins = Builtins();
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = RenderPanel(ctx, Create(dir, builtins));
        Assert.Single(cut.FindAll("tr.teamtypes-row"));

        WriteUser(dir, "gamma/team-type.json", Manifest("gamma", "Gamma Team", 30));
        WriteUser(dir, "gamma/candidates/lead.md", Candidate);
        WriteUser(dir, "gamma/processes/flow.md", "# Flow");
        await cut.InvokeAsync(() => cut.Find("button.teamtypes-reload").ClickAsync());

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("tr.teamtypes-row").Count));
        Assert.Equal(["Alpha Team", "Gamma Team"], cut.FindAll("tr.teamtypes-row").Select(r => CellText(r, "teamtypes-name")));
    }

    /// <summary>The Settings page opens the Team Types panel at <c>/settings/teamtypes</c>.</summary>
    [Fact]
    public async Task SettingsPage_TeamTypesTab_RendersPanel()
    {
        using TempDataDir dir = new();
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = new();
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<Agency.Huddle.App.Prompts.PromptStore>());
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<Agency.Huddle.App.Appearance.AppearanceStore>());
        ctx.Services.AddSingleton(Create(dir, Builtins()));

        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<Settings>(0);
            builder.AddAttribute(1, nameof(Settings.Tab), "teamtypes");
            builder.CloseComponent();
        });

        Assert.Single(cut.FindAll("tr.teamtypes-row"));
        Assert.Equal("Team Types", cut.Find(".mud-tab.mud-tab-active").TextContent.Trim());
    }
}
