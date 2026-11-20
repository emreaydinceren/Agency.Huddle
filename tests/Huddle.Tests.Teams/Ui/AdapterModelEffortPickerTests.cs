using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Components;
using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.Tests.Acp.Fakes;
using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using static Agency.Huddle.Tests.Ui.AgentProfileCardTestSupport;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// <see cref="AdapterModelEffortPicker"/>: the Adapter, Model and Effort selects with the cascade rules of ADR-0015 and
/// rules.md in one place (Helpers Spec §6.7, plan HL-15).
/// </summary>
public sealed class AdapterModelEffortPickerTests
{
    private static readonly AgentModelOption Haiku = new("haiku", "Haiku", null);
    private static readonly AgentModelOption Sonnet = new("sonnet", "Sonnet", null);
    private static readonly AgentEffortOption Low = new("low", "Low", null);
    private static readonly AgentEffortOption High = new("high", "High", null);

    /// <summary>Renders the picker over a fake catalog, recording every selection it raises.</summary>
    /// <param name="ctx">The bUnit context.</param>
    /// <param name="catalog">The fake model catalog.</param>
    /// <param name="adapters">The configured Adapters.</param>
    /// <param name="raised">Receives each raised selection.</param>
    /// <param name="adapter">The starting Adapter.</param>
    /// <param name="model">The starting Model.</param>
    /// <param name="effort">The starting Effort.</param>
    private static IRenderedComponent<ContainerFragment> Render(
        MudBunitContext ctx,
        FakeModelCatalog catalog,
        (string Id, string DisplayName)[] adapters,
        List<AdapterModelEffortChoice> raised,
        string? adapter = null,
        string? model = null,
        string? effort = null)
    {
        ctx.Services.AddSingleton<IModelCatalog>(catalog);
        ctx.Services.AddSingleton(BuildAdapterCatalog(adapters));
        return ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<AdapterModelEffortPicker>(0);
            builder.AddAttribute(1, nameof(AdapterModelEffortPicker.Adapter), adapter);
            builder.AddAttribute(2, nameof(AdapterModelEffortPicker.Model), model);
            builder.AddAttribute(3, nameof(AdapterModelEffortPicker.Effort), effort);
            builder.AddAttribute(4, nameof(AdapterModelEffortPicker.SelectionChanged), EventCallback.Factory.Create<AdapterModelEffortChoice>(raised, raised.Add));
            builder.CloseComponent();
        });
    }

    /// <summary>With one configured Adapter there is nothing to choose, so only Model and Effort show.</summary>
    [Fact]
    public async Task OneAdapter_ShowsNoAdapterSelect()
    {
        await using MudBunitContext ctx = new();
        FakeModelCatalog catalog = new() { Models = [Haiku] };

        IRenderedComponent<ContainerFragment> cut = Render(ctx, catalog, [("claude", "Claude")], []);

        Assert.Equal(0, CountControlsLabelled(cut, "Adapter"));
        Assert.Equal(1, CountControlsLabelled(cut, "Model"));
        Assert.Equal(1, CountControlsLabelled(cut, "Effort"));
    }

    /// <summary>
    /// A stored Adapter that is not configured says that nothing runs until another is picked. It used to promise it would run on
    /// the default Adapter, but <c>HelperRunner</c> fails such a call ("The Adapter '…' is not configured"), and the promise led a
    /// manual run to record FEED-16 as blocked.
    /// </summary>
    [Fact]
    public async Task StoredAdapterNotConfigured_SaysNothingRunsUntilAnotherIsPicked()
    {
        await using MudBunitContext ctx = new();
        FakeModelCatalog catalog = new() { Models = [Haiku] };

        IRenderedComponent<ContainerFragment> cut = Render(ctx, catalog, [("claude", "Claude")], [], adapter: "nope");

        string[] helperTexts = [.. cut.FindAll(".mud-input-helper-text").Select(element => element.TextContent.Trim())];
        Assert.Contains("The adapter 'nope' is not configured, so nothing runs until you pick another.", helperTexts);
        Assert.DoesNotContain(helperTexts, text => text.Contains("will run on", StringComparison.Ordinal)); // contains-ok: no helper text may promise a fallback
    }

    /// <summary>Opening reads the Models and Efforts, and shows the stored Model by name.</summary>
    [Fact]
    public async Task Open_ReadsTheCatalogsAndPreselectsTheStoredModel()
    {
        await using MudBunitContext ctx = new();
        FakeModelCatalog catalog = new() { Models = [Haiku, Sonnet], DefaultEffortLevels = [Low, High] };

        IRenderedComponent<ContainerFragment> cut = Render(ctx, catalog, [("claude", "Claude")], [], model: "haiku", effort: "low");

        // A closed MudSelect shows its Value until the popover registers the items, so the stored ids are what shows here.
        cut.WaitForAssertion(() => Assert.Equal("haiku", FindSelectInput(cut, "Model").GetAttribute("value")));
        cut.WaitForAssertion(() => Assert.Equal("low", FindSelectInput(cut, "Effort").GetAttribute("value")));
        Assert.Equal(1, catalog.ProbeCount);
        Assert.Equal(1, catalog.EffortProbeCount);
    }

    /// <summary>A stored Model the probe does not list is still shown, rather than silently reset.</summary>
    [Fact]
    public async Task StoredModelNotListed_IsKeptAsAnEntry()
    {
        await using MudBunitContext ctx = new();
        FakeModelCatalog catalog = new() { Models = [Sonnet] };

        IRenderedComponent<ContainerFragment> cut = Render(ctx, catalog, [("claude", "Claude")], [], model: "retired-model");

        cut.WaitForAssertion(() => Assert.Equal("retired-model", FindSelectInput(cut, "Model").GetAttribute("value")));
    }

    /// <summary>While the Model list is being read its select is inert and busy, and the Effort list waits only on its own probe.</summary>
    [Fact]
    public async Task WhileTheModelProbeIsOut_ModelSelectIsInertAndBusy()
    {
        await using MudBunitContext ctx = new();
        TaskCompletionSource gate = new();
        FakeModelCatalog catalog = new() { Models = [Haiku], ModelsGate = gate };

        IRenderedComponent<ContainerFragment> cut = Render(ctx, catalog, [("claude", "Claude")], []);

        cut.WaitForAssertion(() => Assert.True(IsSelectInert(cut, "Model")));
        Assert.NotNull(cut.Find("[aria-label='Reading the models this adapter offers']"));
        gate.SetResult();
        cut.WaitForAssertion(() => Assert.False(IsSelectInert(cut, "Model")));
    }

    /// <summary>While the Model list is being read a stored Model shows its known display name (#735), not the raw id and not "Loading…".</summary>
    [Fact]
    public async Task WhileTheModelProbeIsOut_StoredModelShowsItsKnownName()
    {
        await using MudBunitContext ctx = new();
        TaskCompletionSource gate = new();
        FakeModelCatalog catalog = new() { Models = [Haiku], ModelsGate = gate, Runtime = new RuntimeNames("Haiku", null) };

        IRenderedComponent<ContainerFragment> cut = Render(ctx, catalog, [("claude", "Claude")], [], model: "haiku");

        cut.WaitForAssertion(() => Assert.True(IsSelectInert(cut, "Model")));
        Assert.Equal("Haiku", FindSelectInput(cut, "Model").GetAttribute("value"));
    }

    /// <summary>While the Model list is being read a stored Model with no known name shows exactly as stored (#566), not "Loading…".</summary>
    [Fact]
    public async Task WhileTheModelProbeIsOut_StoredModelWithNoKnownNameShowsAsStored()
    {
        await using MudBunitContext ctx = new();
        TaskCompletionSource gate = new();
        FakeModelCatalog catalog = new() { Models = [Haiku], ModelsGate = gate };

        IRenderedComponent<ContainerFragment> cut = Render(ctx, catalog, [("claude", "Claude")], [], model: "haiku");

        cut.WaitForAssertion(() => Assert.True(IsSelectInert(cut, "Model")));
        Assert.Equal("haiku", FindSelectInput(cut, "Model").GetAttribute("value"));
    }

    /// <summary>While the Model list is being read and nothing is stored the select shows "Loading…".</summary>
    [Fact]
    public async Task WhileTheModelProbeIsOut_NothingStored_ShowsLoading()
    {
        await using MudBunitContext ctx = new();
        TaskCompletionSource gate = new();
        FakeModelCatalog catalog = new() { Models = [Haiku], ModelsGate = gate };

        IRenderedComponent<ContainerFragment> cut = Render(ctx, catalog, [("claude", "Claude")], []);

        cut.WaitForAssertion(() => Assert.Equal("Loading…", FindSelectInput(cut, "Model").GetAttribute("value")));
    }

    /// <summary>Changing the Adapter clears Model and Effort, says so in a status note, raises the cleared selection and probes the new Adapter.</summary>
    [Fact]
    public async Task AdapterChange_ClearsModelAndEffort_WithANote_AndReprobes()
    {
        await using MudBunitContext ctx = new();
        FakeModelCatalog catalog = new() { Models = [Haiku], DefaultEffortLevels = [Low] };
        catalog.ModelsByAdapter["codex"] = [new AgentModelOption("gpt", "GPT", null)];
        List<AdapterModelEffortChoice> raised = [];
        IRenderedComponent<ContainerFragment> cut = Render(ctx, catalog, [("claude", "Claude"), ("codex", "Codex")], raised, adapter: "claude", model: "haiku", effort: "low");
        cut.WaitForAssertion(() => Assert.False(IsSelectInert(cut, "Model")));

        await OpenSelectAsync(cut, "Adapter");
        await ClickSelectItemAsync(cut, "Codex");

        cut.WaitForAssertion(() => Assert.Contains(new AdapterModelEffortChoice("codex", null, null, null), raised));
        cut.WaitForAssertion(() => Assert.Equal(
            "Changing the adapter reset Model and Effort to their defaults. Each adapter advertises its own models, so choices made for the previous adapter do not carry across — pick them again if you want specific ones.",
            cut.Find("[role='status']").TextContent.Trim()));
        Assert.Contains("codex", catalog.AdaptersProbed);
    }

    /// <summary>Changing the Adapter with nothing chosen yet shows no reset note, because nothing was reset.</summary>
    [Fact]
    public async Task AdapterChange_WithNothingChosen_ShowsNoNote()
    {
        await using MudBunitContext ctx = new();
        FakeModelCatalog catalog = new() { Models = [Haiku] };
        List<AdapterModelEffortChoice> raised = [];
        IRenderedComponent<ContainerFragment> cut = Render(ctx, catalog, [("claude", "Claude"), ("codex", "Codex")], raised);
        cut.WaitForAssertion(() => Assert.False(IsSelectInert(cut, "Model")));

        await OpenSelectAsync(cut, "Adapter");
        await ClickSelectItemAsync(cut, "Codex");

        cut.WaitForAssertion(() => Assert.NotEmpty(raised));
        Assert.Empty(cut.FindAll("[role='status']"));
    }

    /// <summary>Changing the Model clears Effort with a note, raises the new Model and re-reads the Efforts for it.</summary>
    [Fact]
    public async Task ModelChange_ClearsEffort_WithANote_AndReprobesEfforts()
    {
        await using MudBunitContext ctx = new();
        FakeModelCatalog catalog = new() { Models = [Haiku, Sonnet], DefaultEffortLevels = [Low, High] };
        List<AdapterModelEffortChoice> raised = [];
        IRenderedComponent<ContainerFragment> cut = Render(ctx, catalog, [("claude", "Claude")], raised, model: "haiku", effort: "high");
        cut.WaitForAssertion(() => Assert.False(IsSelectInert(cut, "Model")));
        cut.WaitForAssertion(() => Assert.False(IsSelectInert(cut, "Effort")));

        await OpenSelectAsync(cut, "Model");
        await ClickSelectItemAsync(cut, "Sonnet");

        cut.WaitForAssertion(() => Assert.Contains(new AdapterModelEffortChoice(null, "sonnet", null, null), raised));
        cut.WaitForAssertion(() => Assert.Equal(
            "Changing the model reset Effort to the default. Each model advertises its own effort levels, so a level chosen for the previous model does not carry across — pick one again if you want a specific level.",
            cut.Find("[role='status']").TextContent.Trim()));
        Assert.Contains("sonnet", catalog.EffortProbedModels);
    }

    /// <summary>Choosing an Effort raises the selection and clears any earlier reset note.</summary>
    [Fact]
    public async Task EffortChange_RaisesTheSelection_AndClearsTheNote()
    {
        await using MudBunitContext ctx = new();
        FakeModelCatalog catalog = new() { Models = [Haiku, Sonnet], DefaultEffortLevels = [Low, High] };
        List<AdapterModelEffortChoice> raised = [];
        IRenderedComponent<ContainerFragment> cut = Render(ctx, catalog, [("claude", "Claude")], raised, model: "haiku", effort: "high");
        cut.WaitForAssertion(() => Assert.False(IsSelectInert(cut, "Effort")));
        await OpenSelectAsync(cut, "Model");
        await ClickSelectItemAsync(cut, "Sonnet");
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[role='status']")));
        cut.WaitForAssertion(() => Assert.False(IsSelectInert(cut, "Effort")));

        await OpenSelectAsync(cut, "Effort");
        await ClickSelectItemAsync(cut, "Low");

        cut.WaitForAssertion(() => Assert.Contains(new AdapterModelEffortChoice(null, "sonnet", "low", null), raised));
        Assert.Empty(cut.FindAll("[role='status']"));
    }

    /// <summary>A Disabled picker keeps all its selects inert.</summary>
    [Fact]
    public async Task Disabled_KeepsEverySelectInert()
    {
        await using MudBunitContext ctx = new();
        FakeModelCatalog catalog = new() { Models = [Haiku] };
        ctx.Services.AddSingleton<IModelCatalog>(catalog);
        ctx.Services.AddSingleton(BuildAdapterCatalog(("claude", "Claude")));

        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<AdapterModelEffortPicker>(0);
            builder.AddAttribute(1, nameof(AdapterModelEffortPicker.Disabled), true);
            builder.CloseComponent();
        });

        cut.WaitForAssertion(() => Assert.True(IsSelectInert(cut, "Model")));
        Assert.True(IsSelectInert(cut, "Effort"));
    }

    /// <summary>Without <c>OfferWorkMode</c> (the Helper card) there is no Work mode select and no Work mode probe, even when the Adapter advertises modes.</summary>
    [Fact]
    public async Task WithoutWorkMode_ShowsNoWorkModeSelect_AndReadsNoModes()
    {
        await using MudBunitContext ctx = new();
        FakeModelCatalog catalog = new() { Models = [Haiku], WorkModes = [new AgentModeOption("plan", "Plan", null)] };

        IRenderedComponent<ContainerFragment> cut = Render(ctx, catalog, [("claude", "Claude")], []);

        cut.WaitForAssertion(() => Assert.False(IsSelectInert(cut, "Effort")));
        Assert.Equal(0, CountControlsLabelled(cut, "Work mode"));
        Assert.Equal(0, catalog.WorkModeProbeCount);
    }

    /// <summary>
    /// With <c>OfferWorkMode</c> the advertised modes show in a third select, an Adapter change clears a stored mode with its own
    /// note and raises it cleared, and the Model and Effort selects carry no <c>title</c> tooltip (Q-A4-2).
    /// </summary>
    [Fact]
    public async Task WithWorkMode_AdapterChange_ClearsTheMode_WithItsOwnNote()
    {
        await using MudBunitContext ctx = new();
        FakeModelCatalog catalog = new() { Models = [Haiku], WorkModes = [new AgentModeOption("plan", "Plan", null)] };
        ctx.Services.AddSingleton<IModelCatalog>(catalog);
        ctx.Services.AddSingleton(BuildAdapterCatalog(("claude", "Claude"), ("codex", "Codex")));
        List<AdapterModelEffortChoice> raised = [];

        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<AdapterModelEffortPicker>(0);
            builder.AddAttribute(1, nameof(AdapterModelEffortPicker.Adapter), "claude");
            builder.AddAttribute(2, nameof(AdapterModelEffortPicker.OfferWorkMode), true);
            builder.AddAttribute(3, nameof(AdapterModelEffortPicker.WorkMode), "plan");
            builder.AddAttribute(4, nameof(AdapterModelEffortPicker.SelectionChanged), EventCallback.Factory.Create<AdapterModelEffortChoice>(raised, raised.Add));
            builder.CloseComponent();
        });
        cut.WaitForAssertion(() => Assert.False(IsSelectInert(cut, "Work mode")));
        Assert.Null(FindInputControl(cut, "Model").QuerySelector("[title]"));
        Assert.Null(FindInputControl(cut, "Effort").QuerySelector("[title]"));
        Assert.NotNull(FindInputControl(cut, "Work mode").QuerySelector("[title]"));

        await OpenSelectAsync(cut, "Adapter");
        await ClickSelectItemAsync(cut, "Codex");

        cut.WaitForAssertion(() => Assert.Contains(new AdapterModelEffortChoice("codex", null, null, null), raised));
        cut.WaitForAssertion(() => Assert.Contains(
            "Changing the adapter reset Work mode to its default.",
            cut.Find("[role='status']").TextContent,
            StringComparison.Ordinal)); // contains-ok: the note's first sentence names what was reset
    }

    /// <summary>Each probe that is still current hands its lists to <c>OptionsRead</c>, so a caller can show a stored id by its name.</summary>
    [Fact]
    public async Task OptionsRead_CarriesTheProbedLists()
    {
        await using MudBunitContext ctx = new();
        FakeModelCatalog catalog = new() { Models = [Haiku, Sonnet], DefaultEffortLevels = [Low] };
        ctx.Services.AddSingleton<IModelCatalog>(catalog);
        ctx.Services.AddSingleton(BuildAdapterCatalog(("claude", "Claude")));
        List<AdapterModelEffortOptions> read = [];

        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<AdapterModelEffortPicker>(0);
            builder.AddAttribute(1, nameof(AdapterModelEffortPicker.OptionsRead), EventCallback.Factory.Create<AdapterModelEffortOptions>(read, read.Add));
            builder.CloseComponent();
        });

        cut.WaitForAssertion(() => Assert.Contains(read, options => options.Efforts.Count > 0));
        AdapterModelEffortOptions last = read[^1];
        Assert.Equal([Haiku, Sonnet], last.Models);
        Assert.Equal([Low], last.Efforts);
    }
}
