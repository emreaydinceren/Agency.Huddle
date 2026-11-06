using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.Tests.Factories;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Renders <see cref="AgentConnectorsRow"/> (row P6a of the Agent card) on its own: off by default, the confirm before turning on,
/// off at once, and the three Adapter states (docs/Huddle.AgentConnectors-Specifications.md T-14, T-15).
/// </summary>
public sealed class AgentConnectorsRowTests
{
    private const string Switch = ".agent-connectors-switch input";

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

    private static IRenderedComponent<AgentConnectorsRow> Render(MudBunitContext ctx, TempDataDir dir, string adapter, out PersonaStore store, string? workMode = null)
    {
        IOptions<TeamOptions> options = dir.Options(Configure);
        store = options.CreatePersonaStore();
        _ = store.Add(TestPersonas.Identity("Nova"), "You are Nova.", workMode: workMode);
        ctx.Services.AddSingleton(store);
        ctx.Services.AddSingleton(new AdapterCatalog(options));
        ctx.Services.AddSingleton(new WorkModePolicy(options));

        return ctx.Render<AgentConnectorsRow>(parameters => parameters
            .Add(p => p.Name, "Nova")
            .Add(p => p.Adapter, adapter));
    }

    /// <summary>
    /// A Work Mode that never asks before a tool runs (bypassPermissions, or auto, where the model decides) cannot be combined with
    /// connectors, because the approval prompt would be skipped: the switch refuses, says why, and writes nothing.
    /// </summary>
    /// <param name="mode">The Work Mode.</param>
    [Theory]
    [InlineData("bypassPermissions")]
    [InlineData("auto")]
    public async Task TurnOn_WorkModeThatSkipsThePrompt_Refuses_AndSaysWhy(string mode)
    {
        await using MudBunitContext ctx = new();
        using TempDataDir dir = new();
        IRenderedComponent<AgentConnectorsRow> cut = Render(ctx, dir, "claude", out PersonaStore store, mode);
        using PersonaStore owned = store;

        await cut.InvokeAsync(() => cut.Find(Switch).Change(true));

        Assert.Empty(cut.FindAll(".agent-connectors-confirm"));
        Assert.False(store.Get("Nova")?.Connectors);
        Assert.Equal(
            "This Agent's Work Mode never asks before a tool runs, so account connectors stay off. Choose another Work Mode first.",
            cut.Find(".agent-connectors-mode-note").TextContent.Trim());
    }

    /// <summary>A Work Mode that does ask (acceptEdits only auto-accepts edits) still lets the switch turn on.</summary>
    [Fact]
    public async Task TurnOn_AcceptEdits_StillAsksForTheConfirm()
    {
        await using MudBunitContext ctx = new();
        using TempDataDir dir = new();
        IRenderedComponent<AgentConnectorsRow> cut = Render(ctx, dir, "claude", out PersonaStore store, "acceptEdits");
        using PersonaStore owned = store;

        await cut.InvokeAsync(() => cut.Find(Switch).Change(true));

        Assert.Single(cut.FindAll(".agent-connectors-confirm"));
    }

    /// <summary>A new Agent shows the switch off, with the helper line that says so.</summary>
    [Fact]
    public async Task Renders_OffByDefault_WithTheOffHelper()
    {
        await using MudBunitContext ctx = new();
        using TempDataDir dir = new();

        IRenderedComponent<AgentConnectorsRow> cut = Render(ctx, dir, "claude", out PersonaStore store);
        using PersonaStore owned = store;

        Assert.Equal("Off. Nova cannot use your connected accounts.", cut.Find(".agent-connectors-helper").TextContent.Trim());
        Assert.Empty(cut.FindAll(".agent-connectors-confirm"));
        Assert.False(store.Get("Nova")?.Connectors);
    }

    /// <summary>Turning on asks first with the warning, and writes nothing until Give access is pressed.</summary>
    [Fact]
    public async Task TurnOn_ShowsTheConfirmWithTheWarning_AndWritesNothingYet()
    {
        await using MudBunitContext ctx = new();
        using TempDataDir dir = new();
        IRenderedComponent<AgentConnectorsRow> cut = Render(ctx, dir, "claude", out PersonaStore store);
        using PersonaStore owned = store;

        cut.Find(Switch).Change(true);

        Assert.Equal("Give Nova access to all your connected accounts?", cut.Find(".agent-connectors-confirm .inline-confirm-prompt").TextContent.Trim());
        Assert.Equal("Nova will be able to use every account connector on this computer's claude.ai sign-in, for example mail, calendar and files. Each use will ask you first.", cut.Find(".agent-connectors-warning").TextContent.Trim());
        Assert.False(store.Get("Nova")?.Connectors);
    }

    /// <summary>Cancel leaves the Agent off and removes the confirm.</summary>
    [Fact]
    public async Task Cancel_LeavesItOff()
    {
        await using MudBunitContext ctx = new();
        using TempDataDir dir = new();
        IRenderedComponent<AgentConnectorsRow> cut = Render(ctx, dir, "claude", out PersonaStore store);
        using PersonaStore owned = store;
        cut.Find(Switch).Change(true);

        cut.Find("button.inline-confirm-cancel").Click();

        Assert.Empty(cut.FindAll(".agent-connectors-confirm"));
        Assert.False(store.Get("Nova")?.Connectors);
        Assert.Equal("Off. Nova cannot use your connected accounts.", cut.Find(".agent-connectors-helper").TextContent.Trim());
    }

    /// <summary>
    /// After Cancel the switch itself reads off, not only the helper line: MudSwitch keeps its own value, so a bound value that never
    /// changed (still false) does not reset a switch the Human has flipped (AGCONN-02: drawn on while the line said Off).
    /// </summary>
    [Fact]
    public async Task Cancel_ResetsTheSwitchValueToOff()
    {
        await using MudBunitContext ctx = new();
        using TempDataDir dir = new();
        IRenderedComponent<AgentConnectorsRow> cut = Render(ctx, dir, "claude", out PersonaStore store);
        using PersonaStore owned = store;
        cut.Find(Switch).Change(true);

        cut.Find("button.inline-confirm-cancel").Click();

        Assert.DoesNotContain("mud-checked", cut.Find(".agent-connectors-switch").ClassList);
        Assert.Equal("false", cut.Find(Switch).GetAttribute("aria-checked"));
    }

    /// <summary>A refused turn-on (a Work Mode that skips the prompt) leaves the switch reading off too.</summary>
    [Fact]
    public async Task TurnOn_RefusedByWorkMode_ResetsTheSwitchValueToOff()
    {
        await using MudBunitContext ctx = new();
        using TempDataDir dir = new();
        IRenderedComponent<AgentConnectorsRow> cut = Render(ctx, dir, "claude", out PersonaStore store, "bypassPermissions");
        using PersonaStore owned = store;

        await cut.InvokeAsync(() => cut.Find(Switch).Change(true));

        Assert.DoesNotContain("mud-checked", cut.Find(".agent-connectors-switch").ClassList);
    }

    /// <summary>Give access calls the one writer with on, and the helper then says since when.</summary>
    [Fact]
    public async Task GiveAccess_TurnsItOn_AndShowsSince()
    {
        await using MudBunitContext ctx = new();
        using TempDataDir dir = new();
        IRenderedComponent<AgentConnectorsRow> cut = Render(ctx, dir, "claude", out PersonaStore store);
        using PersonaStore owned = store;
        cut.Find(Switch).Change(true);

        cut.Find("button.inline-confirm-confirm").Click();

        Assert.True(store.Get("Nova")?.Connectors);
        string helper = cut.Find(".agent-connectors-helper").TextContent;
        Assert.Contains("On since", helper, StringComparison.Ordinal); // contains-ok: pinning one load-bearing phrase of the copy
        Assert.Contains("all of your connected accounts", helper, StringComparison.Ordinal); // contains-ok: pinning one load-bearing phrase of the copy
    }

    /// <summary>Turning off applies at once, with no confirm.</summary>
    [Fact]
    public async Task TurnOff_AppliesAtOnce()
    {
        await using MudBunitContext ctx = new();
        using TempDataDir dir = new();
        IRenderedComponent<AgentConnectorsRow> cut = Render(ctx, dir, "claude", out PersonaStore store);
        using PersonaStore owned = store;
        store.SetConnectors("Nova", true);
        cut.Render();

        cut.Find(Switch).Change(false);

        Assert.False(store.Get("Nova")?.Connectors);
        Assert.Empty(cut.FindAll(".agent-connectors-confirm"));
    }

    /// <summary>Restarting shows the applying line in place of the helper.</summary>
    [Fact]
    public async Task Restarting_ShowsTheApplyingLine()
    {
        await using MudBunitContext ctx = new();
        using TempDataDir dir = new();
        IRenderedComponent<AgentConnectorsRow> cut = Render(ctx, dir, "claude", out PersonaStore store);
        using PersonaStore owned = store;

        cut.Render(parameters => parameters.Add(p => p.Restarting, true));

        Assert.Equal("Restarting Nova to apply this.", cut.Find(".agent-connectors-helper").TextContent.Trim());
    }

    /// <summary>A Claude Adapter without isolation gets the per-profile text and no switch (spec 7.3, second row).</summary>
    [Fact]
    public async Task ClaudeWithoutIsolation_ShowsThePerProfileText_AndNoSwitch()
    {
        await using MudBunitContext ctx = new();
        using TempDataDir dir = new();

        IRenderedComponent<AgentConnectorsRow> cut = Render(ctx, dir, "claude-bare", out PersonaStore store);
        using PersonaStore owned = store;

        Assert.Empty(cut.FindAll(Switch));
        string note = cut.Find(".agent-connectors-note").TextContent;
        Assert.Contains("cannot switch this Adapter's account connectors per Agent", note, StringComparison.Ordinal); // contains-ok: pinning one load-bearing phrase of the copy
        Assert.Contains("ENABLE_CLAUDEAI_MCP_SERVERS=false", note, StringComparison.Ordinal); // contains-ok: pinning one load-bearing phrase of the copy
    }

    /// <summary>
    /// Any other Adapter gets a sentence that claims nothing: it names no protection and does not say the Adapter "does not load"
    /// the connectors, which is unverified for every shipped preset (spec 7.3, third row).
    /// </summary>
    [Fact]
    public async Task OtherAdapter_ShowsTheClaimNothingText_AndNoSwitch()
    {
        await using MudBunitContext ctx = new();
        using TempDataDir dir = new();

        IRenderedComponent<AgentConnectorsRow> cut = Render(ctx, dir, "other", out PersonaStore store);
        using PersonaStore owned = store;

        Assert.Empty(cut.FindAll(Switch));
        Assert.Equal("Huddle can't control Other's account connectors.", cut.Find(".agent-connectors-note").TextContent.Trim());
        Assert.DoesNotContain("does not load", cut.Markup, StringComparison.Ordinal);
    }
}
