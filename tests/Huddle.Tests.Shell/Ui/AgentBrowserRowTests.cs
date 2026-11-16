using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.Tests.Factories;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Renders <see cref="AgentBrowserRow"/> (row P6b of the Agent card, #551) on its own: on by default, off at once, no confirm in
/// either direction, and the states that show a sentence instead of a switch. No browser and no process is involved.
/// </summary>
public sealed class AgentBrowserRowTests
{
    private const string Switch = ".agent-browser-switch input";
    private const string Helper = ".agent-browser-helper";
    private const string Note = ".agent-browser-note";

    private static readonly BrowserLaunchFacts Available = new(true, "cli.js", "msedge", false, false, 30);

    private static void Configure(TeamOptions options)
    {
        options.Acp.HiddenModes = ["plan"];
        options.Acp.Adapters =
        [
            new AdapterProfileOptions { Id = "claude", DisplayName = "Claude", Command = "node", Args = ["tools/claude-agent-acp/index.js"], IsolateUserSettings = true },
            new AdapterProfileOptions { Id = "claude-bare", DisplayName = "Claude bare", Command = "node", Args = ["tools/claude-agent-acp/index.js"], IsolateUserSettings = false },
            new AdapterProfileOptions { Id = "other", DisplayName = "Other", Command = "other-acp" },
        ];
    }

    private static IRenderedComponent<AgentBrowserRow> Render(
        MudBunitContext ctx,
        TempDataDir dir,
        out PersonaStore store,
        string adapter = "claude",
        string? workMode = null,
        BrowserLaunchFacts? facts = null)
    {
        IOptions<TeamOptions> options = dir.Options(Configure);
        store = options.CreatePersonaStore();
        _ = store.Add(TestPersonas.Identity("Nova"), "You are Nova.", workMode: workMode);
        ctx.Services.AddSingleton(store);
        ctx.Services.AddSingleton(new AdapterCatalog(options));
        ctx.Services.AddSingleton(new WorkModePolicy(options));
        ctx.Services.AddSingleton(new BrowserLaunchFactsSource(facts ?? Available));

        return ctx.Render<AgentBrowserRow>(parameters => parameters
            .Add(p => p.Name, "Nova")
            .Add(p => p.Adapter, adapter));
    }

    /// <summary>A new Agent shows the switch on, with the On helper line.</summary>
    [Fact]
    public async Task Renders_On_ByDefault_WithTheOnHelper()
    {
        await using MudBunitContext ctx = new();
        using TempDataDir dir = new();

        IRenderedComponent<AgentBrowserRow> cut = Render(ctx, dir, out PersonaStore store);
        using PersonaStore owned = store;

        Assert.Equal(
            "On. Nova has its own browser window on this computer. Reading pages runs freely; every click, typing or form step asks you first. Sites signed in to in that window stay signed in for Nova only.",
            cut.Find(Helper).TextContent.Trim());
        Assert.Equal("On", cut.Find("section").GetAttribute("data-browser-state"));
        Assert.Equal("true", cut.Find(Switch).GetAttribute("aria-checked"));
    }

    /// <summary>An Agent the Human switched off shows the switch off and the Off line.</summary>
    [Fact]
    public async Task Renders_Off_WhenSwitchedOff()
    {
        await using MudBunitContext ctx = new();
        using TempDataDir dir = new();
        IRenderedComponent<AgentBrowserRow> cut = Render(ctx, dir, out PersonaStore store);
        using PersonaStore owned = store;
        store.SetBrowser("Nova", false);

        cut.Render();

        Assert.Equal("Off. Nova has no browser.", cut.Find(Helper).TextContent.Trim());
        Assert.Equal("Off", cut.Find("section").GetAttribute("data-browser-state"));
        Assert.Equal("false", cut.Find(Switch).GetAttribute("aria-checked"));
    }

    /// <summary>While the host restarts, the applying line replaces the helper.</summary>
    [Fact]
    public async Task Restarting_ShowsTheApplyingLine()
    {
        await using MudBunitContext ctx = new();
        using TempDataDir dir = new();
        IRenderedComponent<AgentBrowserRow> cut = Render(ctx, dir, out PersonaStore store);
        using PersonaStore owned = store;

        cut.Render(parameters => parameters.Add(p => p.Restarting, true));

        Assert.Equal("Restarting Nova to apply this.", cut.Find(Helper).TextContent.Trim());
        Assert.Equal("Restarting", cut.Find("section").GetAttribute("data-browser-state"));
    }

    /// <summary>A Work Mode that never asks keeps the browser off: the switch is shown disabled, unchecked, with the reason.</summary>
    /// <param name="mode">The Work Mode.</param>
    [Theory]
    [InlineData("bypassPermissions")]
    [InlineData("auto")]
    public async Task ModeSkipsApproval_ShowsDisabledSwitchAndTheReason(string mode)
    {
        await using MudBunitContext ctx = new();
        using TempDataDir dir = new();

        IRenderedComponent<AgentBrowserRow> cut = Render(ctx, dir, out PersonaStore store, workMode: mode);
        using PersonaStore owned = store;

        Assert.True(cut.Find(Switch).HasAttribute("disabled"));
        Assert.Equal("false", cut.Find(Switch).GetAttribute("aria-checked"));
        Assert.Equal(
            "This Agent's Work Mode never asks before a tool runs, so the browser stays off. Choose another Work Mode first.",
            cut.Find(".agent-browser-mode-note").TextContent.Trim());
        Assert.Equal("ModeSkips", cut.Find("section").GetAttribute("data-browser-state"));
    }

    /// <summary>An Adapter that does not run Claude gets a sentence naming it and no switch.</summary>
    [Fact]
    public async Task NotClaude_ShowsTheSentence_AndNoSwitch()
    {
        await using MudBunitContext ctx = new();
        using TempDataDir dir = new();

        IRenderedComponent<AgentBrowserRow> cut = Render(ctx, dir, out PersonaStore store, "other");
        using PersonaStore owned = store;

        Assert.Empty(cut.FindAll(Switch));
        Assert.Equal("Huddle can't give Other a browser.", cut.Find(Note).TextContent.Trim());
        Assert.Equal("NotClaude", cut.Find("section").GetAttribute("data-browser-state"));
    }

    /// <summary>A Claude Adapter without isolation gets the isolation sentence and no switch.</summary>
    [Fact]
    public async Task ClaudeWithoutIsolation_ShowsTheSentence_AndNoSwitch()
    {
        await using MudBunitContext ctx = new();
        using TempDataDir dir = new();

        IRenderedComponent<AgentBrowserRow> cut = Render(ctx, dir, out PersonaStore store, "claude-bare");
        using PersonaStore owned = store;

        Assert.Empty(cut.FindAll(Switch));
        Assert.Equal("Huddle gives a browser only to an Adapter with isolation on.", cut.Find(Note).TextContent.Trim());
        Assert.Equal("NoIsolation", cut.Find("section").GetAttribute("data-browser-state"));
    }

    /// <summary>Without Edge or Chrome, Playwright MCP, or with the kill switch off, the row says why and shows no switch.</summary>
    /// <param name="enabled">The kill switch.</param>
    /// <param name="cli">The CLI path, or null.</param>
    /// <param name="channel">The channel, or null.</param>
    /// <param name="reason">The reason the row must show.</param>
    [Theory]
    [InlineData(true, "cli.js", null, "no Edge or Chrome was found")]
    [InlineData(true, null, "msedge", "Playwright MCP is not installed")]
    [InlineData(false, "cli.js", "msedge", "the browser is turned off for this install")]
    public async Task Unavailable_ShowsTheReason_AndNoSwitch(bool enabled, string? cli, string? channel, string reason)
    {
        await using MudBunitContext ctx = new();
        using TempDataDir dir = new();

        IRenderedComponent<AgentBrowserRow> cut = Render(ctx, dir, out PersonaStore store, facts: new BrowserLaunchFacts(enabled, cli, channel, false, false, 30));
        using PersonaStore owned = store;

        Assert.Empty(cut.FindAll(Switch));
        Assert.Equal($"No browser is available: {reason}.", cut.Find(Note).TextContent.Trim());
        Assert.Equal("Unavailable", cut.Find("section").GetAttribute("data-browser-state"));
    }

    /// <summary>With no display, the On line says the browser runs without a window instead of promising one, and keeps the asking sentence.</summary>
    [Fact]
    public async Task NoDisplay_On_SaysItRunsWithoutAWindow()
    {
        await using MudBunitContext ctx = new();
        using TempDataDir dir = new();

        IRenderedComponent<AgentBrowserRow> cut = Render(ctx, dir, out PersonaStore store, facts: Available with { NoDisplay = true, Headless = true });
        using PersonaStore owned = store;

        Assert.Equal(
            "On. This computer has no display, so Nova's browser runs without a window and you can't sign in to sites for it. Reading pages runs freely; every click, typing or form step asks you first.",
            cut.Find(Helper).TextContent.Trim());
        Assert.Equal("NoDisplay", cut.Find("section").GetAttribute("data-browser-state"));
    }

    /// <summary>Turning off calls the one writer at once, and the store then reads it off.</summary>
    [Fact]
    public async Task Toggle_Off_CallsSetBrowser()
    {
        await using MudBunitContext ctx = new();
        using TempDataDir dir = new();
        IRenderedComponent<AgentBrowserRow> cut = Render(ctx, dir, out PersonaStore store);
        using PersonaStore owned = store;

        await cut.InvokeAsync(() => cut.Find(Switch).Change(false));

        Assert.True(store.Get("Nova")?.BrowserOff);
        Assert.Equal("Off. Nova has no browser.", cut.Find(Helper).TextContent.Trim());
    }

    /// <summary>Turning back on calls the one writer, and the store then reads it on.</summary>
    [Fact]
    public async Task Toggle_On_CallsSetBrowser()
    {
        await using MudBunitContext ctx = new();
        using TempDataDir dir = new();
        IRenderedComponent<AgentBrowserRow> cut = Render(ctx, dir, out PersonaStore store);
        using PersonaStore owned = store;
        store.SetBrowser("Nova", false);
        cut.Render();

        await cut.InvokeAsync(() => cut.Find(Switch).Change(true));

        Assert.False(store.Get("Nova")?.BrowserOff);
        Assert.Equal("On", cut.Find("section").GetAttribute("data-browser-state"));
    }

    /// <summary>Neither direction shows a confirm: the change is written the moment the switch moves.</summary>
    [Fact]
    public async Task NoConfirm_EitherDirection()
    {
        await using MudBunitContext ctx = new();
        using TempDataDir dir = new();
        IRenderedComponent<AgentBrowserRow> cut = Render(ctx, dir, out PersonaStore store);
        using PersonaStore owned = store;

        await cut.InvokeAsync(() => cut.Find(Switch).Change(false));
        Assert.Empty(cut.FindAll(".inline-confirm-prompt"));
        Assert.True(store.Get("Nova")?.BrowserOff);

        await cut.InvokeAsync(() => cut.Find(Switch).Change(true));
        Assert.Empty(cut.FindAll(".inline-confirm-prompt"));
        Assert.False(store.Get("Nova")?.BrowserOff);
    }
}
