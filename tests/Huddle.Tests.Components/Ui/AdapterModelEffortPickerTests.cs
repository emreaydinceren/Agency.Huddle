using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.Tests.Acp.Fakes;
using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using static Agency.Huddle.Tests.Ui.TeammateCardTestSupport;

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
        List<(string? Adapter, string? Model, string? Effort)> raised,
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
            builder.AddAttribute(4, nameof(AdapterModelEffortPicker.SelectionChanged), EventCallback.Factory.Create<(string?, string?, string?)>(raised, raised.Add));
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

    /// <summary>Changing the Adapter clears Model and Effort, says so in a status note, raises the cleared selection and probes the new Adapter.</summary>
    [Fact]
    public async Task AdapterChange_ClearsModelAndEffort_WithANote_AndReprobes()
    {
        await using MudBunitContext ctx = new();
        FakeModelCatalog catalog = new() { Models = [Haiku], DefaultEffortLevels = [Low] };
        catalog.ModelsByAdapter["codex"] = [new AgentModelOption("gpt", "GPT", null)];
        List<(string? Adapter, string? Model, string? Effort)> raised = [];
        IRenderedComponent<ContainerFragment> cut = Render(ctx, catalog, [("claude", "Claude"), ("codex", "Codex")], raised, adapter: "claude", model: "haiku", effort: "low");
        cut.WaitForAssertion(() => Assert.False(IsSelectInert(cut, "Model")));

        await OpenSelectAsync(cut, "Adapter");
        await ClickSelectItemAsync(cut, "Codex");

        cut.WaitForAssertion(() => Assert.Contains(("codex", (string?)null, (string?)null), raised));
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
        List<(string? Adapter, string? Model, string? Effort)> raised = [];
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
        List<(string? Adapter, string? Model, string? Effort)> raised = [];
        IRenderedComponent<ContainerFragment> cut = Render(ctx, catalog, [("claude", "Claude")], raised, model: "haiku", effort: "high");
        cut.WaitForAssertion(() => Assert.False(IsSelectInert(cut, "Model")));
        cut.WaitForAssertion(() => Assert.False(IsSelectInert(cut, "Effort")));

        await OpenSelectAsync(cut, "Model");
        await ClickSelectItemAsync(cut, "Sonnet");

        cut.WaitForAssertion(() => Assert.Contains((null, "sonnet", (string?)null), raised));
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
        List<(string? Adapter, string? Model, string? Effort)> raised = [];
        IRenderedComponent<ContainerFragment> cut = Render(ctx, catalog, [("claude", "Claude")], raised, model: "haiku", effort: "high");
        cut.WaitForAssertion(() => Assert.False(IsSelectInert(cut, "Effort")));
        await OpenSelectAsync(cut, "Model");
        await ClickSelectItemAsync(cut, "Sonnet");
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[role='status']")));
        cut.WaitForAssertion(() => Assert.False(IsSelectInert(cut, "Effort")));

        await OpenSelectAsync(cut, "Effort");
        await ClickSelectItemAsync(cut, "Low");

        cut.WaitForAssertion(() => Assert.Contains((null, "sonnet", "low"), raised));
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
}
