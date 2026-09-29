using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Acp;
using static Agency.Huddle.Tests.Ui.TeammateCardTestSupport;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// The Model, Effort and Adapter selects of <see cref="Agency.Huddle.App.Components.Shared.TeammateCard"/> - catalog probes, stored values, inert states and the reset notices - split out of <see cref="TeammateCardTests"/> so xUnit can run the two classes in parallel.
/// </summary>
public sealed class TeammateCardModelEffortTests
{
    /// <summary>
    /// A distinctive fragment of the Adapter-change reset notice - the Adapter counterpart of
    /// <see cref="EffortResetNotice"/>, asserted the same way (Spec §6.6, §8.3).
    /// </summary>
    private const string AdapterResetNotice = "Changing the adapter reset Model and Effort to their defaults.";

    /// <summary>
    /// Spec §6.6 (Internal flow) and §8.3: choosing a different Adapter discards whatever Model and
    /// Effort were chosen for the PREVIOUS adapter — a model id from one adapter's catalog means
    /// nothing against another's — and says so through a role="status" note (never role="alert": this
    /// is a consequence of what the human just did, not an interruption, the same precedent as
    /// <see cref="EffortResetNotice"/>). The probe that follows must run against the NEW adapter,
    /// proven through <see cref="Agency.Huddle.Tests.Acp.Fakes.FakeModelCatalog.AdaptersProbed"/>.
    /// </summary>
    [Fact]
    public async Task EditMode_ChangingTheAdapter_ResetsModelAndEffortWithANote()
    {
        await using var factory = new TeamWebApplicationFactory();
        factory.FakeModelCatalog.ModelsByAdapter["claude"] = [new("claude-opus-4", "Opus", null)];
        factory.FakeModelCatalog.ModelsByAdapter["agency"] = [new("gemma-4b", "Gemma", null)];
        SeedPersonaWithModelEffortAndAdapter(factory, "coo", "x", model: "claude-opus-4", effort: "high", adapter: "claude");
        await using var ctx = NewContext(factory);
        ctx.Services.AddSingleton(BuildAdapterCatalog(("claude", "Claude"), ("agency", "Agency")));

        var cut = await OpenViewCardAsync(ctx, factory, "coo");
        await ClickButtonAsync(cut, "Edit");

        await OpenSelectAsync(cut, "Adapter");
        await ClickSelectItemAsync(cut, "Agency");

        Assert.Equal(string.Empty, FindSelectInput(cut, "Model").GetAttribute("value"));
        Assert.Equal(string.Empty, FindSelectInput(cut, "Effort").GetAttribute("value"));

        var statusElements = cut.FindAll("[role='status']");
        Assert.Contains(statusElements, element => element.TextContent.Contains(AdapterResetNotice, StringComparison.Ordinal));
        Assert.DoesNotContain(cut.FindAll("[role='alert']"), element => element.TextContent.Contains(AdapterResetNotice, StringComparison.Ordinal));

        Assert.Contains("agency", factory.FakeModelCatalog.AdaptersProbed);
    }

    /// <summary>
    /// Spec §12 (E-4) — a new defect class this feature introduces, and Spec §8.3's "Generation
    /// counters" paragraph. <c>LoadEffortsAsync</c> already guards a stale probe from overwriting a
    /// newer one with <c>effortProbeGeneration</c>; before Task 8.3, <c>LoadModelsAsync</c> has no
    /// equivalent, because nothing could previously re-enter it. Changing the Adapter twice quickly
    /// starts two model probes, and the slower one (here, the FIRST adapter chosen) must not land
    /// after the faster one (the SECOND) and overwrite it.
    /// <see cref="Agency.Huddle.Tests.Acp.Fakes.FakeModelCatalog.ModelsGate"/> captures whichever gate
    /// is set at the moment <c>GetAsync</c> is called, so clearing it between
    /// the two Adapter changes lets the second probe answer immediately while the first stays held
    /// open until explicitly released at the end.
    /// </summary>
    [Fact]
    public async Task EditMode_ChangingTheAdapterTwiceQuickly_TheFasterProbeWins()
    {
        await using var factory = new TeamWebApplicationFactory();
        factory.FakeModelCatalog.ModelsByAdapter["agency"] = [new("gemma-4b", "Gemma", null)];
        factory.FakeModelCatalog.ModelsByAdapter["mock"] = [new("mock-model", "Mock Model", null)];
        SeedPersonaWithModelEffortAndAdapter(factory, "coo", "x", model: null, effort: null, adapter: null);
        await using var ctx = NewContext(factory);
        ctx.Services.AddSingleton(BuildAdapterCatalog(("claude", "Claude"), ("agency", "Agency"), ("mock", "Mock")));

        var cut = await OpenViewCardAsync(ctx, factory, "coo");
        await ClickButtonAsync(cut, "Edit");

        TaskCompletionSource gateA = new(TaskCreationOptions.RunContinuationsAsynchronously);
        factory.FakeModelCatalog.ModelsGate = gateA;

        await OpenSelectAsync(cut, "Adapter");
        await ClickSelectItemAsync(cut, "Agency");

        // The Agency probe is now stuck on gateA. Clearing the gate before switching again lets the
        // SECOND probe (Mock) answer immediately - the ordering this test exists to prove wrong.
        factory.FakeModelCatalog.ModelsGate = null;

        await OpenSelectAsync(cut, "Adapter");
        await ClickSelectItemAsync(cut, "Mock");

        var optionsBeforeRelease = await OpenSelectAndListOptionsAsync(cut, "Model");
        Assert.Contains(optionsBeforeRelease, option => string.Equals(option.TextContent.Trim(), "Mock Model", StringComparison.Ordinal));
        Assert.DoesNotContain(optionsBeforeRelease, option => string.Equals(option.TextContent.Trim(), "Gemma", StringComparison.Ordinal));

        // Release the slower, now-stale Agency probe. Its answer must not overwrite Mock's.
        await cut.InvokeAsync(gateA.SetResult);

        cut.WaitForAssertion(() =>
        {
            var options = cut.FindAll("div.mud-list-item");
            Assert.DoesNotContain(options, option => string.Equals(option.TextContent.Trim(), "Gemma", StringComparison.Ordinal));
        });
    }

    [Fact]
    public async Task CreateMode_OffersAModelChoice()
    {
        await using var factory = new TeamWebApplicationFactory();
        factory.FakeModelCatalog.Models = [new("claude-opus-4", "Opus", null), new("claude-sonnet-4", "Sonnet", null)];
        await using var ctx = NewContext(factory);

        var cut = RenderPage(ctx, factory);
        await ClickButtonAsync(cut, "New teammate");
        await OpenSelectAsync(cut, "Model");

        Assert.Contains("Opus", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Sonnet", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Use the agent's default", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>No option carries a literal "selected" other than the blank default: the closed select's own input shows the empty label, precisely because Model is null on a fresh Create card.</summary>
    [Fact]
    public async Task CreateMode_DefaultsToTheAgentsOwnModel()
    {
        await using var factory = new TeamWebApplicationFactory();
        factory.FakeModelCatalog.Models = [new("claude-opus-4", "Opus", null)];
        await using var ctx = NewContext(factory);

        var cut = RenderPage(ctx, factory);
        await ClickButtonAsync(cut, "New teammate");

        var modelInput = FindSelectInput(cut, "Model");
        Assert.Equal(string.Empty, modelInput.GetAttribute("value"));
    }

    /// <summary>Opening Edit shows the Persona's already-stored Model preselected in the closed select, read back through the real <see cref="PersonaStore"/>.</summary>
    [Fact]
    public async Task EditMode_PreselectsTheStoredModel()
    {
        await using var factory = new TeamWebApplicationFactory();
        factory.FakeModelCatalog.Models = [new("claude-opus-4", "Opus", null), new("claude-sonnet-4", "Sonnet", null)];
        SeedPersonaWithModelAndEffort(factory, "coo", "x", model: "claude-opus-4", effort: null);
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");
        await ClickButtonAsync(cut, "Edit");

        // MudSelect<string> shows the raw Value as its closed-state text by default (no
        // ToStringFunc maps a value to its MudSelectItem's child content), so the id itself - not
        // "Opus" - is what proves the stored choice survived into the select.
        var modelInput = FindSelectInput(cut, "Model");
        Assert.Equal("claude-opus-4", modelInput.GetAttribute("value"));
    }

    /// <summary>
    /// rules.md is binding here: "A Model the agent does not advertise is a warning, never a
    /// failure... A stale stored Model must not be able to brick a Persona." If the probe failed, or
    /// the adapter's own catalog changed since this Persona was saved, its stored Model is no longer
    /// (or never was) in the catalog - opening Edit must still show and keep that stored choice
    /// rather than silently resetting it to default the moment the card is saved again.
    /// </summary>
    [Fact]
    public async Task EditMode_StoredModelNotInTheCatalog_IsStillOffered()
    {
        await using var factory = new TeamWebApplicationFactory();
        factory.FakeModelCatalog.Models = [new("claude-opus-4", "Opus", null)];
        SeedPersonaWithModelAndEffort(factory, "coo", "x", model: "claude-vintage-1", effort: null);
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");
        await ClickButtonAsync(cut, "Edit");

        var modelInput = FindSelectInput(cut, "Model");
        Assert.Equal("claude-vintage-1", modelInput.GetAttribute("value"));
    }

    [Fact]
    public async Task CreateMode_NoModelsAdvertised_StillRenders()
    {
        await using var factory = new TeamWebApplicationFactory();
        await using var ctx = NewContext(factory);

        var cut = RenderPage(ctx, factory);
        await ClickButtonAsync(cut, "New teammate");

        Assert.NotEmpty(cut.FindAll(".mud-select"));
        Assert.Contains("advertises no models", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateMode_WhileTheCatalogLoads_SaysSo()
    {
        await using var factory = new TeamWebApplicationFactory();
        factory.FakeModelCatalog.NeverCompletes = true;
        await using var ctx = NewContext(factory);

        var cut = RenderPage(ctx, factory);
        await ClickButtonAsync(cut, "New teammate");

        Assert.Contains("Reading the models this agent offers", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ViewMode_WithNoModel_SaysAgentDefault()
    {
        await using var factory = new TeamWebApplicationFactory();
        await SeedPersonaAsync(factory, "coo", "x");
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");

        Assert.Contains("Agent default", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// View mode itself never probes the model catalog (rules.md: "only when a New/Edit form
    /// opens"), so the one real path to seeing a friendly name instead of a raw id in View is the
    /// one <c>TeammateCard.SaveAsync</c> takes deliberately: landing on the saved Teammate's
    /// details right after Create, while this card's own catalog from that Create form is still
    /// held. Proves the display-name lookup and the "land on View, don't just close" behaviour at
    /// once, driven by real clicks: fill the Create form, pick "Opus" from a real popover, save.
    /// </summary>
    [Fact]
    public async Task ViewMode_ShowsTheChosenModel()
    {
        await using var factory = new TeamWebApplicationFactory();
        factory.FakeModelCatalog.Models = [new("claude-opus-4", "Opus", null)];
        await using var ctx = NewContext(factory);

        var cut = RenderPage(ctx, factory);
        await ClickButtonAsync(cut, "New teammate");
        await SetTextValueAsync(cut, "Name", "coo");
        await SetTextValueAsync(cut, "Title", "Chief of Staff");
        await SetTextValueAsync(cut, "Alias", "coo");
        await SetTextValueAsync(cut, "Persona body", "You are the Chief of Staff.");
        await OpenSelectAsync(cut, "Model");
        await ClickSelectItemAsync(cut, "Opus");
        await ClickButtonAsync(cut, "Add teammate");

        // The display name, not the raw wire id.
        Assert.Contains("Opus", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("claude-opus-4", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ViewMode_WithNoEffort_SaysModelDefault()
    {
        await using var factory = new TeamWebApplicationFactory();
        await SeedPersonaAsync(factory, "coo", "x");
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");

        Assert.Contains("Model default", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>The Effort-catalog sibling of <see cref="ViewMode_ShowsTheChosenModel"/> - see its remarks for why this is the one real path to a friendly name rather than a raw id in View mode.</summary>
    [Fact]
    public async Task ViewMode_ShowsTheChosenEffort()
    {
        await using var factory = new TeamWebApplicationFactory();
        factory.FakeModelCatalog.DefaultEffortLevels = [new("effort-high", "High", null)];
        await using var ctx = NewContext(factory);

        var cut = RenderPage(ctx, factory);
        await ClickButtonAsync(cut, "New teammate");
        await SetTextValueAsync(cut, "Name", "coo");
        await SetTextValueAsync(cut, "Title", "Chief of Staff");
        await SetTextValueAsync(cut, "Alias", "coo");
        await SetTextValueAsync(cut, "Persona body", "You are the Chief of Staff.");
        await OpenSelectAsync(cut, "Effort");
        await ClickSelectItemAsync(cut, "High");
        await ClickButtonAsync(cut, "Add teammate");

        // The display name, not the raw wire id.
        Assert.Contains("High", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("effort-high", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateMode_OffersAnEffortChoice()
    {
        await using var factory = new TeamWebApplicationFactory();
        factory.FakeModelCatalog.DefaultEffortLevels = [new("high", "High", null), new("low", "Low", null)];
        await using var ctx = NewContext(factory);

        var cut = RenderPage(ctx, factory);
        await ClickButtonAsync(cut, "New teammate");
        await OpenSelectAsync(cut, "Effort");

        Assert.Contains("High", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Low", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Use the agent's default", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>Opening Edit shows the Persona's already-stored Effort preselected in the closed select, read back through the real <see cref="PersonaStore"/>.</summary>
    [Fact]
    public async Task EditMode_PreselectsTheStoredEffort()
    {
        await using var factory = new TeamWebApplicationFactory();
        factory.FakeModelCatalog.DefaultEffortLevels = [new("high", "High", null), new("low", "Low", null)];
        SeedPersonaWithModelAndEffort(factory, "coo", "x", model: null, effort: "high");
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");
        await ClickButtonAsync(cut, "Edit");

        // MudSelect<string> shows the raw Value as its closed-state text by default - see the same
        // note on EditMode_PreselectsTheStoredModel.
        var effortInput = FindSelectInput(cut, "Effort");
        Assert.Equal("high", effortInput.GetAttribute("value"));
    }

    /// <summary>
    /// The Effort-catalog sibling of <see cref="EditMode_StoredModelNotInTheCatalog_IsStillOffered"/>:
    /// a stale stored Effort must be visible and kept, never silently reset to default on the next
    /// save.
    /// </summary>
    [Fact]
    public async Task EditMode_StoredEffortNotInTheCatalog_IsStillOffered()
    {
        await using var factory = new TeamWebApplicationFactory();
        factory.FakeModelCatalog.DefaultEffortLevels = [new("high", "High", null)];
        SeedPersonaWithModelAndEffort(factory, "coo", "x", model: null, effort: "vintage");
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");
        await ClickButtonAsync(cut, "Edit");

        var effortInput = FindSelectInput(cut, "Effort");
        Assert.Equal("vintage", effortInput.GetAttribute("value"));
    }

    [Fact]
    public async Task CreateMode_NoEffortLevelsAdvertised_StillRenders()
    {
        await using var factory = new TeamWebApplicationFactory();
        await using var ctx = NewContext(factory);

        var cut = RenderPage(ctx, factory);
        await ClickButtonAsync(cut, "New teammate");

        Assert.Contains("offers no effort choice", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateMode_WhileTheEffortCatalogLoads_SaysSo()
    {
        await using var factory = new TeamWebApplicationFactory();
        factory.FakeModelCatalog.EffortsNeverComplete = true;
        await using var ctx = NewContext(factory);

        var cut = RenderPage(ctx, factory);
        await ClickButtonAsync(cut, "New teammate");

        Assert.Contains("Reading the effort levels this model offers", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// Issue #39: an unread model catalog is an EMPTY one, so ModelChoices renders just the blank
    /// default plus the synthesised entry for the stored id. An interactive select would offer that
    /// two-item list as though it were the agent's whole answer, which is what the issue measured
    /// ("two entries at 265 ms, six at 8.5 s"). While the probe is out the control must refuse to open.
    /// </summary>
    [Fact]
    public async Task EditMode_WhileTheCatalogLoads_TheModelSelectIsInert()
    {
        await using var factory = new TeamWebApplicationFactory();
        factory.FakeModelCatalog.NeverCompletes = true;
        SeedPersonaWithModelAndEffort(factory, "coo", "x", model: "claude-sonnet-4", effort: null);
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");
        await ClickButtonAsync(cut, "Edit");

        Assert.True(IsSelectInert(cut, "Model"));
        Assert.Empty(await OpenSelectAndListOptionsAsync(cut, "Model"));
    }

    /// <summary>The Effort select's half of <see cref="EditMode_WhileTheCatalogLoads_TheModelSelectIsInert"/> - the same empty-catalog-looks-complete trap, on the ladder rather than the models.</summary>
    [Fact]
    public async Task EditMode_WhileTheEffortCatalogLoads_TheEffortSelectIsInert()
    {
        await using var factory = new TeamWebApplicationFactory();
        factory.FakeModelCatalog.EffortsNeverComplete = true;
        SeedPersonaWithModelAndEffort(factory, "coo", "x", model: null, effort: "high");
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");
        await ClickButtonAsync(cut, "Edit");

        Assert.True(IsSelectInert(cut, "Effort"));
        Assert.Empty(await OpenSelectAndListOptionsAsync(cut, "Effort"));
    }

    /// <summary>
    /// MODELEFFORT-11's Pass condition in automated form, and the guard that going inert did not cost
    /// it: while the catalog is still out the closed select must already show the STORED value - as the
    /// raw id, since no label is known yet - and not snap to "Use the agent's default". A read-only
    /// MudSelect still displays its selection, which is the whole reason ReadOnly was chosen over
    /// emptying or replacing the option list.
    /// </summary>
    [Fact]
    public async Task EditMode_WhileTheCatalogLoads_StillShowsTheStoredModel()
    {
        await using var factory = new TeamWebApplicationFactory();
        factory.FakeModelCatalog.NeverCompletes = true;
        SeedPersonaWithModelAndEffort(factory, "coo", "x", model: "claude-sonnet-4", effort: null);
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");
        await ClickButtonAsync(cut, "Edit");

        Assert.Equal("claude-sonnet-4", FindSelectInput(cut, "Model").GetAttribute("value"));
    }

    /// <summary>Going inert is for the probe window only: once the catalog lands the select opens and offers the real list.</summary>
    [Fact]
    public async Task EditMode_AfterTheCatalogLands_TheModelSelectIsInteractiveAgain()
    {
        await using var factory = new TeamWebApplicationFactory();
        factory.FakeModelCatalog.Models = [new("claude-opus-4", "Opus", null), new("claude-sonnet-4", "Sonnet", null)];
        SeedPersonaWithModelAndEffort(factory, "coo", "x", model: "claude-sonnet-4", effort: null);
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");
        await ClickButtonAsync(cut, "Edit");

        Assert.False(IsSelectInert(cut, "Model"));

        var options = await OpenSelectAndListOptionsAsync(cut, "Model");
        Assert.Contains(options, option => string.Equals(option.TextContent.Trim(), "Opus", StringComparison.Ordinal));
    }

    /// <summary>
    /// The render-gap half of issue #39. ComponentBase renders a handler at its first yield and its
    /// completion and nowhere between, so BeginEditAsync's two back-to-back probes used to repaint only
    /// once BOTH adapter spawns had answered - the Model select sat on its stale two-item list waiting
    /// for the EFFORT catalog. Here the effort probe never answers at all, so the only thing that can
    /// paint the real model list is the explicit StateHasChanged between the two awaits.
    /// </summary>
    [Fact]
    public async Task EditMode_ModelCatalogLandsFirst_RepaintsWithoutWaitingOnTheEffortProbe()
    {
        await using var factory = new TeamWebApplicationFactory();
        TaskCompletionSource gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        factory.FakeModelCatalog.Models = [new("claude-opus-4", "Opus", null), new("claude-sonnet-4", "Sonnet", null)];
        factory.FakeModelCatalog.ModelsGate = gate;
        factory.FakeModelCatalog.EffortsNeverComplete = true;
        SeedPersonaWithModelAndEffort(factory, "coo", "x", model: "claude-sonnet-4", effort: null);
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");
        await ClickButtonAsync(cut, "Edit");

        Assert.True(IsSelectInert(cut, "Model"));

        await cut.InvokeAsync(gate.SetResult);

        // A render carrying the landed catalog has happened - and the ONLY thing that can have caused it
        // is the explicit StateHasChanged, because the handler it sits in has not returned and cannot
        // until the effort probe answers, which it never will.
        cut.WaitForAssertion(() => Assert.DoesNotContain("Reading the models this agent offers", cut.Markup, StringComparison.Ordinal));

        Assert.False(IsSelectInert(cut, "Model"));
        Assert.True(IsSelectInert(cut, "Effort"));
        Assert.Contains("Reading the effort levels this model offers", cut.Markup, StringComparison.Ordinal);

        var options = await OpenSelectAndListOptionsAsync(cut, "Model");
        Assert.Contains(options, option => string.Equals(option.TextContent.Trim(), "Opus", StringComparison.Ordinal));
    }

    /// <summary>
    /// Preserves the "changing the Model re-probes the Effort catalog" behaviour rules.md and
    /// testing.md both call out - previously provable only by the manual checklist (steps 14-19),
    /// because it needs a real interaction with a live <c>&lt;select&gt;</c>, which the retired
    /// <c>HtmlRenderer</c> could never dispatch. Now that this card can receive a real click, this
    /// proves it directly: opening Create already probes the effort catalog once (for the agent's
    /// default model, key <see langword="null"/>), and picking a different model in the Model select
    /// probes it again, for the new model specifically.
    /// </summary>
    [Fact]
    public async Task ChangingTheModel_ReProbesTheEffortCatalog()
    {
        await using var factory = new TeamWebApplicationFactory();
        factory.FakeModelCatalog.Models = [new("claude-opus-4", "Opus", null), new("claude-sonnet-4", "Sonnet", null)];
        await using var ctx = NewContext(factory);

        var cut = RenderPage(ctx, factory);
        await ClickButtonAsync(cut, "New teammate");

        Assert.Equal(1, factory.FakeModelCatalog.EffortProbeCount);
        Assert.Contains(null, factory.FakeModelCatalog.EffortProbedModels);

        await OpenSelectAsync(cut, "Model");
        await ClickSelectItemAsync(cut, "Sonnet");

        Assert.Equal(2, factory.FakeModelCatalog.EffortProbeCount);
        Assert.Contains("claude-sonnet-4", factory.FakeModelCatalog.EffortProbedModels);
    }

    /// <summary>
    /// A distinctive fragment of issue #45's notice text - one literal the four tests below assert
    /// against, rather than four copies of the full sentence (with its em dash) scattered across them.
    /// </summary>
    private const string EffortResetNotice = "Changing the model reset Effort to the default.";

    /// <summary>
    /// The reset itself is deliberate and stays pinned by manual test PERSONALIFECYCLE-29 - what issue
    /// #45 asks for is that it stop happening silently. Proves the notice text appears, that it is
    /// carried by an element with <c>role="status"</c> rather than merely present somewhere in the
    /// markup (MudAlert renders no role by default - rules.md), and that the Effort select's own reset
    /// still actually happens underneath the new notice.
    /// </summary>
    [Fact]
    public async Task ChangingTheModel_WithAnEffortChosen_SaysTheEffortWasReset()
    {
        await using var factory = new TeamWebApplicationFactory();
        factory.FakeModelCatalog.Models = [new("claude-opus-4", "Opus", null), new("claude-sonnet-4", "Sonnet", null)];
        factory.FakeModelCatalog.DefaultEffortLevels = [new("high", "High", null), new("low", "Low", null)];
        SeedPersonaWithModelAndEffort(factory, "coo", "x", model: "claude-opus-4", effort: "high");
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");
        await ClickButtonAsync(cut, "Edit");

        await OpenSelectAsync(cut, "Model");
        await ClickSelectItemAsync(cut, "Sonnet");

        Assert.Contains(EffortResetNotice, cut.Markup, StringComparison.Ordinal);
        var statusElements = cut.FindAll("[role='status']");
        Assert.Contains(statusElements, element => element.TextContent.Contains(EffortResetNotice, StringComparison.Ordinal));

        var effortInput = FindSelectInput(cut, "Effort");
        Assert.Equal(string.Empty, effortInput.GetAttribute("value"));
    }

    /// <summary>Nothing was discarded when no Effort was chosen in the first place, so a Model change here has nothing to announce - the notice exists to report a loss, not to narrate every Model change.</summary>
    [Fact]
    public async Task ChangingTheModel_WithNoEffortChosen_SaysNothing()
    {
        await using var factory = new TeamWebApplicationFactory();
        factory.FakeModelCatalog.Models = [new("claude-opus-4", "Opus", null), new("claude-sonnet-4", "Sonnet", null)];
        factory.FakeModelCatalog.DefaultEffortLevels = [new("high", "High", null), new("low", "Low", null)];
        SeedPersonaWithModelAndEffort(factory, "coo", "x", model: "claude-opus-4", effort: null);
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");
        await ClickButtonAsync(cut, "Edit");

        await OpenSelectAsync(cut, "Model");
        await ClickSelectItemAsync(cut, "Sonnet");

        Assert.DoesNotContain(EffortResetNotice, cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>Making a fresh Effort choice is the notice's own resolution: the level just picked replaces whatever the Model change discarded, so there is nothing left for the notice to say.</summary>
    [Fact]
    public async Task ChoosingAnEffortAfterAModelChange_ClearsTheNotice()
    {
        await using var factory = new TeamWebApplicationFactory();
        factory.FakeModelCatalog.Models = [new("claude-opus-4", "Opus", null), new("claude-sonnet-4", "Sonnet", null)];
        factory.FakeModelCatalog.DefaultEffortLevels = [new("high", "High", null), new("low", "Low", null)];
        factory.FakeModelCatalog.EffortLevelsByModel["claude-sonnet-4"] = [new("effort-medium", "Medium", null)];
        SeedPersonaWithModelAndEffort(factory, "coo", "x", model: "claude-opus-4", effort: "high");
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");
        await ClickButtonAsync(cut, "Edit");

        await OpenSelectAsync(cut, "Model");
        await ClickSelectItemAsync(cut, "Sonnet");
        Assert.Contains(EffortResetNotice, cut.Markup, StringComparison.Ordinal);

        await OpenSelectAsync(cut, "Effort");
        await ClickSelectItemAsync(cut, "Medium");

        Assert.DoesNotContain(EffortResetNotice, cut.Markup, StringComparison.Ordinal);
        var effortInput = FindSelectInput(cut, "Effort");
        Assert.Equal("effort-medium", effortInput.GetAttribute("value"));
    }

    /// <summary>
    /// A second Model change made while no Effort is chosen discards nothing, so the notice left over
    /// from the FIRST change must not go on standing over a reset that did not happen this time - the
    /// defect this guards, distinct from the notice simply never appearing (see
    /// <see cref="ChangingTheModel_WithNoEffortChosen_SaysNothing"/>). The first change's notice is
    /// asserted too, so this cannot pass vacuously by the notice never having appeared at all.
    /// </summary>
    [Fact]
    public async Task ChangingTheModelAgain_WithTheEffortAlreadyGone_ClearsTheNotice()
    {
        await using var factory = new TeamWebApplicationFactory();
        factory.FakeModelCatalog.Models =
        [
            new("claude-opus-4", "Opus", null),
            new("claude-sonnet-4", "Sonnet", null),
            new("claude-haiku-4", "Haiku", null),
        ];
        factory.FakeModelCatalog.DefaultEffortLevels = [new("high", "High", null), new("low", "Low", null)];
        SeedPersonaWithModelAndEffort(factory, "coo", "x", model: "claude-opus-4", effort: "high");
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");
        await ClickButtonAsync(cut, "Edit");

        await OpenSelectAsync(cut, "Model");
        await ClickSelectItemAsync(cut, "Sonnet");
        Assert.Contains(EffortResetNotice, cut.Markup, StringComparison.Ordinal);

        await OpenSelectAsync(cut, "Model");
        await ClickSelectItemAsync(cut, "Haiku");

        Assert.DoesNotContain(EffortResetNotice, cut.Markup, StringComparison.Ordinal);
    }
}
