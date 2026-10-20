using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Components.Settings.Helpers;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Helpers;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Helpers;
using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using static Agency.Huddle.Tests.Ui.TeammateCardTestSupport;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// <see cref="HelperCard"/> and <see cref="HelpersPanel"/>: the state, usage, last run and last error of a Helper, Continue,
/// Save and Reset to defaults, and the read-only state when Adapters are off (Helpers Spec §6.7, plan HL-16, HL-17).
/// </summary>
public sealed class HelperCardTests : IDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    private readonly TempDataDir dir = new();
    private readonly ScriptedHelper helper = new("NeedsHuman", new HelperDefaults(true, "claude", "haiku", "low", 50));
    private readonly ManualTimeProvider clock = new(Noon, TimeZoneInfo.Utc);
    private readonly HelperSettingsStore settingsStore;
    private readonly HelperStatusStore statusStore;
    private readonly HelperBudget budget;

    /// <summary>Opens the three stores the card reads and writes over the temp directory, so a test can seed and inspect them.</summary>
    public HelperCardTests()
    {
        IOptions<TeamOptions> options = this.dir.Options();
        this.settingsStore = new HelperSettingsStore(options);
        this.statusStore = new HelperStatusStore(options);
        this.budget = new HelperBudget(new HelperUsageStore(options), this.clock);
    }

    /// <summary>Removes the temp data directory.</summary>
    public void Dispose() => this.dir.Dispose();

    /// <summary>Registers everything the card injects, over real stores on the temp directory.</summary>
    /// <param name="ctx">The bUnit context.</param>
    /// <param name="acpEnabled">Whether Adapters are on.</param>
    private void Register(MudBunitContext ctx, bool acpEnabled = true)
    {
        IOptions<TeamOptions> options = this.dir.Options(team =>
        {
            team.Acp.Enabled = acpEnabled;
            team.Acp.Adapters = [new AdapterProfileOptions { Id = "claude", Command = "node", Args = ["a.js"], UsesToolNamePrefix = false }];
        });
        HelperSettingsResolver resolver = new(this.settingsStore, options, NullLogger<HelperSettingsResolver>.Instance);
        HelperDispatcher dispatcher = new([], resolver, this.statusStore, options, NullLogger<HelperDispatcher>.Instance, this.clock);

        ctx.Services.AddSingleton(options);
        ctx.Services.AddSingleton<TimeProvider>(this.clock);
        ctx.Services.AddSingleton(this.settingsStore);
        ctx.Services.AddSingleton(this.statusStore);
        ctx.Services.AddSingleton(this.budget);
        ctx.Services.AddSingleton(resolver);
        ctx.Services.AddSingleton(dispatcher);
        ctx.Services.AddSingleton(new HelperRegistry([this.helper]));
        ctx.Services.AddSingleton<IModelCatalog>(new FakeModelCatalog());
        ctx.Services.AddSingleton(BuildAdapterCatalog(("claude", "Claude")));
    }

    /// <summary>Renders the card for the scripted Helper.</summary>
    /// <param name="ctx">The bUnit context.</param>
    /// <param name="adaptersOn">Whether Adapters are on.</param>
    private static IRenderedComponent<ContainerFragment> RenderCard(MudBunitContext ctx, bool adaptersOn = true) =>
        ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<HelperCard>(0);
            builder.AddAttribute(1, nameof(HelperCard.HelperId), "NeedsHuman");
            builder.AddAttribute(2, nameof(HelperCard.AdaptersOn), adaptersOn);
            builder.CloseComponent();
        });

    /// <summary>The text of the card's chip.</summary>
    /// <param name="cut">The rendered card.</param>
    private static string Chip(IRenderedComponent<ContainerFragment> cut) => cut.Find(".helper-card-chip").TextContent.Trim();

    /// <summary>A running Helper shows its name, description, an On chip, today's use and the prompt link.</summary>
    [Fact]
    public async Task RunningHelper_ShowsNameChipUsageAndPromptLink()
    {
        await using MudBunitContext ctx = new();
        this.Register(ctx);

        IRenderedComponent<ContainerFragment> cut = RenderCard(ctx);

        Assert.Equal("Scripted", cut.Find("h2").TextContent.Trim());
        Assert.Contains("A scripted Helper.", cut.Markup, StringComparison.Ordinal); // contains-ok: the description is one paragraph among the card's other text, with no selector of its own.
        Assert.Equal("On", Chip(cut));
        Assert.Equal("0 of 50 calls today", cut.Find(".helper-card-usage").TextContent.Trim());
        Assert.Equal("/settings/prompts?highlight=test.system", cut.Find("a[href^='/settings/prompts']").GetAttribute("href"));
        Assert.Equal("Edit the prompt", cut.Find("a[href^='/settings/prompts']").TextContent.Trim());
    }

    /// <summary>The daily cap field carries its helper text and refuses a cap below 1 by keeping 1.</summary>
    [Fact]
    public async Task DailyCap_HasHelperText_AndRefusesZero()
    {
        await using MudBunitContext ctx = new();
        this.Register(ctx);
        IRenderedComponent<ContainerFragment> cut = RenderCard(ctx);
        Assert.Contains("Calls a day before it pauses and asks you.", cut.Find(".helper-card-cap").TextContent, StringComparison.Ordinal); // contains-ok: the field's text content is its label and its helper text together.

        await cut.InvokeAsync(() => cut.Find(".helper-card-cap input").ChangeAsync(new ChangeEventArgs { Value = "0" }));

        cut.WaitForAssertion(() => Assert.Equal("1", cut.Find(".helper-card-cap input").GetAttribute("value")));
    }

    /// <summary>With Adapters off every control is inert, and the panel says why.</summary>
    [Fact]
    public async Task AdaptersOff_CardIsReadOnly_AndPanelSaysWhy()
    {
        await using MudBunitContext ctx = new();
        this.Register(ctx, acpEnabled: false);

        IRenderedComponent<HelpersPanel> panel = ctx.Render<HelpersPanel>();

        Assert.Equal("Helpers run on an Adapter, and Adapters are off in this installation.", panel.Find("[role='status']").TextContent.Trim());
        Assert.True(panel.Find(".helper-card-save").HasAttribute("disabled"));
        Assert.True(panel.Find(".helper-card-reset").HasAttribute("disabled"));
        Assert.True(panel.Find(".mud-switch input").HasAttribute("disabled"));
        Assert.Equal("Helpers are small background agents that make one judgement and let Huddle act on it. They don't chat, keep memory or use tools.", panel.Find(".settings-intro").TextContent.Trim());
    }

    /// <summary>The panel lists a card per registered Helper and where the choices are stored.</summary>
    [Fact]
    public async Task Panel_ListsEachHelper_AndTheStorePath()
    {
        await using MudBunitContext ctx = new();
        this.Register(ctx);

        IRenderedComponent<HelpersPanel> panel = ctx.Render<HelpersPanel>();

        Assert.Single(panel.FindAll(".helper-card"));
        Assert.Equal(Path.Combine(this.dir.Path, "team.db"), panel.Find(".settings-stored-at code").TextContent);
        Assert.Empty(panel.FindAll("[role='status']"));
    }

    /// <summary>At its cap the card shows Paused and Continue; pressing Continue adds the grant for today and resumes it.</summary>
    [Fact]
    public async Task PausedHelper_ShowsContinue_AndContinueResumesIt()
    {
        await using MudBunitContext ctx = new();
        this.Register(ctx);
        HelperId id = this.helper.Id;
        for (int i = 0; i < 50; i++)
        {
            Assert.True(this.budget.TryReserve(id, 50));
        }

        Assert.False(this.budget.TryReserve(id, 50));
        IRenderedComponent<ContainerFragment> cut = RenderCard(ctx);

        Assert.Equal("Paused", Chip(cut));
        Assert.Contains("Paused — daily cap reached", cut.Find(".helper-card-paused").TextContent, StringComparison.Ordinal); // contains-ok: the alert holds the sentence and the Continue button's text.
        Assert.Equal("1 skipped while paused", cut.Find(".helper-card-usage").TextContent.Split(" · ")[1]);
        Assert.Equal("Continue (+10 today)", cut.Find(".helper-card-continue").TextContent.Trim());

        await cut.InvokeAsync(() => cut.Find(".helper-card-continue").ClickAsync());

        cut.WaitForAssertion(() => Assert.Equal("On", Chip(cut)));
        Assert.Empty(cut.FindAll(".helper-card-paused"));
        Assert.Equal("50 of 60 calls today · 1 skipped while paused", cut.Find(".helper-card-usage").TextContent.Trim());
    }

    /// <summary>The last run shows its local time and result.</summary>
    [Fact]
    public async Task LastRun_ShowsTimeAndResult()
    {
        await using MudBunitContext ctx = new();
        this.Register(ctx);
        this.statusStore.RecordSuccess(this.helper.Id, new DateTimeOffset(2026, 10, 20, 10, 42, 0, TimeSpan.Zero), "Turned the gate on for RSH-0208");

        IRenderedComponent<ContainerFragment> cut = RenderCard(ctx);

        Assert.Equal("Last run 10:42 · Turned the gate on for RSH-0208", cut.Find(".helper-card-last-run").TextContent.Trim());
        Assert.Empty(cut.FindAll(".helper-card-error"));
    }

    /// <summary>A failure after the last good run shows as a status alert with the time and message.</summary>
    [Fact]
    public async Task LastError_ShownWhenTheLatestRunFailed()
    {
        await using MudBunitContext ctx = new();
        this.Register(ctx);
        this.statusStore.RecordSuccess(this.helper.Id, new DateTimeOffset(2026, 10, 20, 9, 0, 0, TimeSpan.Zero), "No change");
        this.statusStore.RecordFailure(this.helper.Id, new DateTimeOffset(2026, 10, 20, 10, 40, 0, TimeSpan.Zero), "Timed out after 60 s");

        IRenderedComponent<ContainerFragment> cut = RenderCard(ctx);

        IElement error = cut.Find(".helper-card-error");
        Assert.Equal("status", error.GetAttribute("role"));
        Assert.Equal("Last error 10:40 · Timed out after 60 s", error.TextContent.Trim());
    }

    /// <summary>A good run after the failure hides the error: it is no longer the latest word.</summary>
    [Fact]
    public async Task LastError_HiddenOnceALaterRunSucceeded()
    {
        await using MudBunitContext ctx = new();
        this.Register(ctx);
        this.statusStore.RecordFailure(this.helper.Id, new DateTimeOffset(2026, 10, 20, 10, 40, 0, TimeSpan.Zero), "Timed out after 60 s");
        this.statusStore.RecordSuccess(this.helper.Id, new DateTimeOffset(2026, 10, 20, 11, 0, 0, TimeSpan.Zero), "No change");

        IRenderedComponent<ContainerFragment> cut = RenderCard(ctx);

        Assert.Empty(cut.FindAll(".helper-card-error"));
    }

    /// <summary>Turning the switch off and pressing Save stores the Human's row and shows Off; Reset to defaults deletes it and shows On again.</summary>
    [Fact]
    public async Task Save_StoresTheRow_AndResetDeletesIt()
    {
        await using MudBunitContext ctx = new();
        this.Register(ctx);
        IRenderedComponent<ContainerFragment> cut = RenderCard(ctx);
        Assert.True(cut.Find(".helper-card-save").HasAttribute("disabled"));

        await cut.InvokeAsync(() => cut.Find(".mud-switch input").ChangeAsync(new ChangeEventArgs { Value = false }));
        cut.WaitForAssertion(() => Assert.False(cut.Find(".helper-card-save").HasAttribute("disabled")));
        await cut.InvokeAsync(() => cut.Find(".helper-card-save").ClickAsync());

        HelperSettingsRow? row = this.settingsStore.Get(this.helper.Id);
        Assert.NotNull(row);
        Assert.False(row.Enabled);
        cut.WaitForAssertion(() => Assert.Equal("Off", Chip(cut)));

        await cut.InvokeAsync(() => cut.Find(".helper-card-reset").ClickAsync());

        Assert.Null(this.settingsStore.Get(this.helper.Id));
        cut.WaitForAssertion(() => Assert.Equal("On", Chip(cut)));
    }
}
