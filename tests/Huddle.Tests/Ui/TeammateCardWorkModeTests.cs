using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Acp;
using Bunit;
using Bunit.Rendering;
using Microsoft.Extensions.DependencyInjection;
using static Agency.Huddle.Tests.Ui.TeammateCardTestSupport;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// The Work mode select of <see cref="Agency.Huddle.App.Components.Shared.TeammateCard"/> (ADR-0033):
/// what it offers, what it shows for a stored value, when it is inert or absent, and what an Adapter
/// change does to it. A sibling of <see cref="TeammateCardModelEffortTests"/>, split out for the same
/// reason: each class is a unit xUnit can run in parallel.
/// </summary>
public sealed class TeammateCardWorkModeTests
{
    /// <summary>The note an Adapter change shows when it discards a Work Mode, whitespace collapsed.</summary>
    private const string AdapterResetNotice = "Changing the adapter reset Work mode to its default. Each adapter advertises its own modes, so a mode chosen for the previous adapter does not carry across — pick one again if you want a specific mode.";

    private static readonly IReadOnlyList<AgentModeOption> Modes =
    [
        new AgentModeOption("default", "Manual", "Always ask before making changes"),
        new AgentModeOption("acceptEdits", "Accept edits", "Automatically accept all file edits"),
        new AgentModeOption("plan", "Plan", "Create a plan before making changes"),
    ];

    /// <summary>Create offers every mode the Adapter advertises, and the blank choice meaning the Adapter's own mode.</summary>
    [Fact]
    public async Task CreateMode_OffersAWorkModeChoice()
    {
        await using var factory = new TeamWebApplicationFactory();
        factory.FakeModelCatalog.WorkModes = Modes;
        await using var ctx = NewContext(factory);

        var cut = RenderPage(ctx, factory);
        await ClickButtonAsync(cut, "New teammate");
        var options = await OpenSelectAndListOptionsAsync(cut, "Work mode");

        var texts = options.Select(option => option.TextContent.Trim()).ToList();
        Assert.Equal(["Use the agent's default", "Manual", "Accept edits", "Plan"], texts);
    }

    /// <summary>A fresh card has no Work Mode: the closed select shows the blank default.</summary>
    [Fact]
    public async Task CreateMode_DefaultsToTheAdaptersOwnMode()
    {
        await using var factory = new TeamWebApplicationFactory();
        factory.FakeModelCatalog.WorkModes = Modes;
        await using var ctx = NewContext(factory);

        var cut = RenderPage(ctx, factory);
        await ClickButtonAsync(cut, "New teammate");

        Assert.Equal(string.Empty, FindSelectInput(cut, "Work mode").GetAttribute("value"));
    }

    /// <summary>Opening Edit shows the Persona's stored Work Mode, read back through the real store.</summary>
    [Fact]
    public async Task EditMode_PreselectsTheStoredWorkMode()
    {
        await using var factory = new TeamWebApplicationFactory();
        factory.FakeModelCatalog.WorkModes = Modes;
        SeedPersonaWithWorkMode(factory, "coo", "x", workMode: "plan");
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");
        await ClickButtonAsync(cut, "Edit");

        Assert.Equal("Plan", FindSelectInput(cut, "Work mode").GetAttribute("value"));
    }

    /// <summary>
    /// A stored mode the Adapter no longer advertises (an upgrade, a failed probe) is still shown and kept,
    /// never silently reset to the default on the next save: a stale value must not be able to hide itself.
    /// </summary>
    [Fact]
    public async Task EditMode_StoredWorkModeNotInTheCatalog_IsStillOffered()
    {
        await using var factory = new TeamWebApplicationFactory();
        factory.FakeModelCatalog.WorkModes = [new AgentModeOption("plan", "Plan", null)];
        SeedPersonaWithWorkMode(factory, "coo", "x", workMode: "vintage");
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");
        await ClickButtonAsync(cut, "Edit");

        Assert.Equal("vintage", FindSelectInput(cut, "Work mode").GetAttribute("value"));
    }

    /// <summary>The selected mode's own description, which the Adapter supplies, is the helper text.</summary>
    [Fact]
    public async Task EditMode_ShowsTheSelectedModesDescription()
    {
        await using var factory = new TeamWebApplicationFactory();
        factory.FakeModelCatalog.WorkModes = Modes;
        SeedPersonaWithWorkMode(factory, "coo", "x", workMode: "plan");
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");
        await ClickButtonAsync(cut, "Edit");

        Assert.Equal(
            "Create a plan before making changes. Changing it restarts the teammate, which clears what it remembers.",
            HelperTextOf(cut, "Work mode"));
    }

    /// <summary>An Adapter that advertises no mode, with nothing stored, gets no picker at all.</summary>
    [Fact]
    public async Task CreateMode_NoModesAdvertisedAndNothingStored_ShowsNoPicker()
    {
        await using var factory = new TeamWebApplicationFactory();
        await using var ctx = NewContext(factory);

        var cut = RenderPage(ctx, factory);
        await ClickButtonAsync(cut, "New teammate");

        Assert.Equal(0, CountControlsLabelled(cut, "Work mode"));
    }

    /// <summary>A stored mode keeps the picker even when the Adapter now advertises none, so it can still be seen and cleared.</summary>
    [Fact]
    public async Task EditMode_NoModesAdvertisedButOneStored_StillShowsThePicker()
    {
        await using var factory = new TeamWebApplicationFactory();
        SeedPersonaWithWorkMode(factory, "coo", "x", workMode: "plan");
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");
        await ClickButtonAsync(cut, "Edit");

        Assert.Equal(1, CountControlsLabelled(cut, "Work mode"));
        Assert.Equal("plan", FindSelectInput(cut, "Work mode").GetAttribute("value"));
    }

    /// <summary>
    /// While the probe is out the catalog is unread, which looks like an empty one: the select must refuse
    /// to open rather than offer that as the Adapter's whole answer (the same trap as issue #39). It is
    /// Disabled, not ReadOnly, because only the disabled state is styled.
    /// </summary>
    [Fact]
    public async Task EditMode_WhileTheCatalogLoads_TheWorkModeSelectIsInert()
    {
        await using var factory = new TeamWebApplicationFactory();
        factory.FakeModelCatalog.WorkModesNeverComplete = true;
        SeedPersonaWithWorkMode(factory, "coo", "x", workMode: "plan");
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");
        await ClickButtonAsync(cut, "Edit");

        Assert.True(IsSelectInert(cut, "Work mode"));
        Assert.Empty(await OpenSelectAndListOptionsAsync(cut, "Work mode"));
    }

    /// <summary>While the probe is out the helper text says the modes are being read, rather than claiming the Adapter offers none.</summary>
    [Fact]
    public async Task EditMode_WhileTheCatalogLoads_SaysSo()
    {
        await using var factory = new TeamWebApplicationFactory();
        factory.FakeModelCatalog.WorkModesNeverComplete = true;
        SeedPersonaWithWorkMode(factory, "coo", "x", workMode: "plan");
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");
        await ClickButtonAsync(cut, "Edit");

        Assert.Equal("Reading the work modes this agent offers…", HelperTextOf(cut, "Work mode"));
    }

    /// <summary>Going inert must not cost the stored value: while the catalog is out the closed select still shows it, as the raw id.</summary>
    [Fact]
    public async Task EditMode_WhileTheCatalogLoads_StillShowsTheStoredWorkMode()
    {
        await using var factory = new TeamWebApplicationFactory();
        factory.FakeModelCatalog.WorkModesNeverComplete = true;
        SeedPersonaWithWorkMode(factory, "coo", "x", workMode: "plan");
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");
        await ClickButtonAsync(cut, "Edit");

        Assert.Equal("plan", FindSelectInput(cut, "Work mode").GetAttribute("value"));
    }

    /// <summary>Inert is for the probe window only: once the catalog lands the select opens and offers the real list.</summary>
    [Fact]
    public async Task EditMode_AfterTheCatalogLands_TheWorkModeSelectIsInteractive()
    {
        await using var factory = new TeamWebApplicationFactory();
        factory.FakeModelCatalog.WorkModes = Modes;
        SeedPersonaWithWorkMode(factory, "coo", "x", workMode: "plan");
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");
        await ClickButtonAsync(cut, "Edit");

        Assert.False(IsSelectInert(cut, "Work mode"));
        Assert.NotEmpty(await OpenSelectAndListOptionsAsync(cut, "Work mode"));
    }

    /// <summary>
    /// Changing the Adapter discards the Work Mode, as it does Model and Effort: a mode chosen for one
    /// Adapter means nothing against another's list. It says so with a role="status" note, never
    /// role="alert", because it is a consequence of what the Human just did and not an interruption.
    /// </summary>
    [Fact]
    public async Task EditMode_ChangingTheAdapter_ClearsTheWorkModeWithANote()
    {
        await using var factory = new TeamWebApplicationFactory();
        factory.FakeModelCatalog.WorkModes = Modes;
        factory.FakeModelCatalog.ModelsByAdapter["agency"] = [new("gemma-4b", "Gemma", null)];
        SeedPersonaWithWorkMode(factory, "coo", "x", workMode: "plan", adapter: "claude");
        await using var ctx = NewContext(factory);
        ctx.Services.AddSingleton(BuildAdapterCatalog(("claude", "Claude"), ("agency", "Agency")));

        var cut = await OpenViewCardAsync(ctx, factory, "coo");
        await ClickButtonAsync(cut, "Edit");

        await OpenSelectAsync(cut, "Adapter");
        await ClickSelectItemAsync(cut, "Agency");

        Assert.Equal(string.Empty, FindSelectInput(cut, "Work mode").GetAttribute("value"));
        Assert.Contains(AdapterResetNotice, NotesWithRole(cut, "status"));
        Assert.DoesNotContain(AdapterResetNotice, NotesWithRole(cut, "alert"));
        Assert.Contains(factory.FakeModelCatalog.WorkModesProbed, probe => probe.AdapterId == "agency");
    }

    /// <summary>With no Work Mode chosen there is nothing to discard, so an Adapter change shows no Work mode note.</summary>
    [Fact]
    public async Task EditMode_ChangingTheAdapter_WithNoWorkMode_ShowsNoNote()
    {
        await using var factory = new TeamWebApplicationFactory();
        factory.FakeModelCatalog.WorkModes = Modes;
        SeedPersonaWithWorkMode(factory, "coo", "x", workMode: null, adapter: "claude");
        await using var ctx = NewContext(factory);
        ctx.Services.AddSingleton(BuildAdapterCatalog(("claude", "Claude"), ("agency", "Agency")));

        var cut = await OpenViewCardAsync(ctx, factory, "coo");
        await ClickButtonAsync(cut, "Edit");

        await OpenSelectAsync(cut, "Adapter");
        await ClickSelectItemAsync(cut, "Agency");

        Assert.DoesNotContain(AdapterResetNotice, NotesWithRole(cut, "status"));
    }

    /// <summary>A Model change keeps the Work Mode: modes do not depend on the model the way an effort ladder does.</summary>
    [Fact]
    public async Task EditMode_ChangingTheModel_KeepsTheWorkMode()
    {
        await using var factory = new TeamWebApplicationFactory();
        factory.FakeModelCatalog.WorkModes = Modes;
        factory.FakeModelCatalog.Models = [new("claude-opus-4", "Opus", null), new("claude-sonnet-4", "Sonnet", null)];
        SeedPersonaWithWorkMode(factory, "coo", "x", workMode: "plan");
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");
        await ClickButtonAsync(cut, "Edit");
        await OpenSelectAsync(cut, "Model");
        await ClickSelectItemAsync(cut, "Sonnet");

        Assert.Equal("Plan", FindSelectInput(cut, "Work mode").GetAttribute("value"));
    }

    /// <summary>Choosing a mode and saving stores it, read back through the real <see cref="PersonaStore"/>.</summary>
    [Fact]
    public async Task EditMode_ChoosingAWorkModeAndSaving_StoresIt()
    {
        await using var factory = new TeamWebApplicationFactory();
        factory.FakeModelCatalog.WorkModes = Modes;
        SeedPersonaWithWorkMode(factory, "coo", "x", workMode: null);
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");
        await ClickButtonAsync(cut, "Edit");
        await OpenSelectAsync(cut, "Work mode");
        await ClickSelectItemAsync(cut, "Accept edits");
        await ClickButtonAsync(cut, "Save");

        Assert.Equal("acceptEdits", StoredWorkMode(factory, "coo"));
    }

    /// <summary>Choosing the blank default and saving clears a stored mode.</summary>
    [Fact]
    public async Task EditMode_ChoosingTheBlankDefaultAndSaving_ClearsIt()
    {
        await using var factory = new TeamWebApplicationFactory();
        factory.FakeModelCatalog.WorkModes = Modes;
        SeedPersonaWithWorkMode(factory, "coo", "x", workMode: "plan");
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");
        await ClickButtonAsync(cut, "Edit");
        await OpenSelectAsync(cut, "Work mode");
        await ClickSelectItemAsync(cut, "Use the agent's default");
        await ClickButtonAsync(cut, "Save");

        Assert.Null(StoredWorkMode(factory, "coo"));
    }

    /// <summary>Saving without touching the Work Mode keeps it: the card passes the stored value back rather than defaulting it.</summary>
    [Fact]
    public async Task EditMode_SavingWithoutTouchingTheWorkMode_KeepsIt()
    {
        await using var factory = new TeamWebApplicationFactory();
        factory.FakeModelCatalog.WorkModes = Modes;
        SeedPersonaWithWorkMode(factory, "coo", "x", workMode: "plan");
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");
        await ClickButtonAsync(cut, "Edit");
        await ClickButtonAsync(cut, "Save");

        Assert.Equal("plan", StoredWorkMode(factory, "coo"));
    }

    /// <summary>A new teammate created with a Work Mode stores it.</summary>
    [Fact]
    public async Task CreateMode_ChoosingAWorkMode_StoresItOnAdd()
    {
        await using var factory = new TeamWebApplicationFactory();
        factory.FakeModelCatalog.WorkModes = Modes;
        await using var ctx = NewContext(factory);

        var cut = RenderPage(ctx, factory);
        await ClickButtonAsync(cut, "New teammate");
        await SetTextValueAsync(cut, "Name", "coo");
        await SetTextValueAsync(cut, "Title", "Chief of Staff");
        await SetTextValueAsync(cut, "Alias", "coo");
        await SetTextValueAsync(cut, "Persona body", "You are the Chief of Staff.");
        await OpenSelectAsync(cut, "Work mode");
        await ClickSelectItemAsync(cut, "Plan");
        await ClickButtonAsync(cut, "Add teammate");

        Assert.Equal("plan", StoredWorkMode(factory, "coo"));
    }

    /// <summary>View mode with no Work Mode says the Adapter's default is used.</summary>
    [Fact]
    public async Task ViewMode_WithNoWorkMode_SaysAgentDefault()
    {
        await using var factory = new TeamWebApplicationFactory();
        await SeedPersonaAsync(factory, "coo", "x");
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");

        Assert.Equal("Agent default", ViewPaperText(cut, "Work mode"));
    }

    /// <summary>
    /// View mode never probes, so the one real path to a friendly name rather than a raw id is the one
    /// Save takes deliberately: landing on the saved Teammate's details right after Create, while the card's
    /// own catalog is still held. The sibling of the Model and Effort tests that prove the same lookup.
    /// </summary>
    [Fact]
    public async Task ViewMode_ShowsTheChosenWorkModesName()
    {
        await using var factory = new TeamWebApplicationFactory();
        factory.FakeModelCatalog.WorkModes = [new AgentModeOption("work-mode-x", "Careful", null)];
        await using var ctx = NewContext(factory);

        var cut = RenderPage(ctx, factory);
        await ClickButtonAsync(cut, "New teammate");
        await SetTextValueAsync(cut, "Name", "coo");
        await SetTextValueAsync(cut, "Title", "Chief of Staff");
        await SetTextValueAsync(cut, "Alias", "coo");
        await SetTextValueAsync(cut, "Persona body", "You are the Chief of Staff.");
        await OpenSelectAsync(cut, "Work mode");
        await ClickSelectItemAsync(cut, "Careful");
        await ClickButtonAsync(cut, "Add teammate");

        Assert.Equal("Careful", ViewPaperText(cut, "Work mode"));
    }

    /// <summary>The page passes a stored Work Mode through to the View card, so it is not silently dropped on the way in.</summary>
    [Fact]
    public async Task ViewMode_ShowsTheStoredWorkMode()
    {
        await using var factory = new TeamWebApplicationFactory();
        SeedPersonaWithWorkMode(factory, "coo", "x", workMode: "stored-mode-id");
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");

        Assert.Equal("stored-mode-id", ViewPaperText(cut, "Work mode"));
    }

    /// <summary>A Work Mode set to the Adapter's own default is never an error, whichever way the Human arrived.</summary>
    [Fact]
    public async Task EditMode_ThePickerIsNeverARoleAlert()
    {
        await using var factory = new TeamWebApplicationFactory();
        factory.FakeModelCatalog.WorkModes = Modes;
        SeedPersonaWithWorkMode(factory, "coo", "x", workMode: "plan");
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");
        await ClickButtonAsync(cut, "Edit");

        Assert.Empty(NotesWithRole(cut, "alert"));
    }

    /// <summary>The stored Work Mode of <paramref name="name"/>, read back through the real <see cref="PersonaStore"/>, failing the test if the Persona is gone.</summary>
    private static string? StoredWorkMode(TeamWebApplicationFactory factory, string name)
    {
        Persona? persona = factory.Services.GetRequiredService<PersonaStore>().Get(name);
        Assert.NotNull(persona);
        return persona.WorkMode;
    }

    /// <summary>The helper text under the select labelled <paramref name="label"/>, trimmed.</summary>
    private static string HelperTextOf(IRenderedComponent<ContainerFragment> cut, string label)
    {
        var helper = FindInputControl(cut, label).QuerySelector(".mud-input-helper-text");
        Assert.NotNull(helper);
        return helper.TextContent.Trim();
    }

    /// <summary>The body text of the View card's outlined paper whose overline reads <paramref name="overline"/>, trimmed.</summary>
    private static string ViewPaperText(IRenderedComponent<ContainerFragment> cut, string overline)
    {
        var paper = cut.FindAll(".mud-paper")
            .First(candidate => string.Equals(candidate.QuerySelector(".mud-typography-overline")?.TextContent.Trim(), overline, StringComparison.Ordinal));
        var body = paper.QuerySelector(".mud-typography-body1");
        Assert.NotNull(body);
        return body.TextContent.Trim();
    }

    /// <summary>The text of every element with <c>role="<paramref name="role"/>"</c>, whitespace collapsed to single spaces.</summary>
    private static List<string> NotesWithRole(IRenderedComponent<ContainerFragment> cut, string role)
    {
        return [.. cut.FindAll($"[role='{role}']").Select(element => string.Join(' ', element.TextContent.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)))];
    }
}
