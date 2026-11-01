using Bunit;
using Bunit.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Avatars;
using static Agency.Huddle.Tests.Ui.AvatarLibraryPickSupport;
using static Agency.Huddle.Tests.Ui.AgentProfileCardTestSupport;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Round 3 of the visual audit for the Agent card and its avatar dialog (B8a to B8k, E5, B10): the title is the item's
/// name, sections are Group labels rather than boxes, the handle is written <c>@alias</c>, the removal sits under a
/// more-actions menu, the Create and Edit forms are grouped and use "e.g." examples, a list being read shows a progress bar
/// instead of a disabled-looking box, the swatches are a named radio group, and the library replaces the avatar editor's
/// content in place so the dialogs never stack three deep.
/// </summary>
public sealed class R3TeamsCardTests
{
    /// <summary>The View card's title is the Agent's name, not "Agent", and no second, larger name sits in the body (B8a, B10j).</summary>
    [Fact]
    public async Task ViewCard_TitleIsTheName_AndTheBodyHoldsNoSecondName()
    {
        await using TeamWebApplicationFactory factory = new();
        await SeedPersonaAsync(factory, "coo", "You keep the team honest.");
        await using MudBunitContext ctx = NewContext(factory);

        IRenderedComponent<ContainerFragment> cut = await OpenViewCardAsync(ctx, factory, "coo");

        Assert.Equal("coo", cut.Find(".mud-dialog-title h6").TextContent.Trim());
        Assert.Empty(cut.FindAll(".mud-dialog-content h5"));
        Assert.Empty(cut.FindAll(".agent-profile-card-name"));
    }

    /// <summary>The View card's sections are Group labels over their content and no outlined box is drawn (B8b).</summary>
    [Fact]
    public async Task ViewCard_SectionsAreGroupLabels_NotBoxes()
    {
        await using TeamWebApplicationFactory factory = new();
        await SeedPersonaAsync(factory, "coo", "You keep the team honest.");
        await using MudBunitContext ctx = NewContext(factory);

        IRenderedComponent<ContainerFragment> cut = await OpenViewCardAsync(ctx, factory, "coo");

        string[] labels = [.. cut.FindAll(".mud-dialog-content .agent-profile-card-section > .group-label").Select(static label => label.TextContent.Trim())];
        Assert.Equal(["Persona", "Model", "Effort", "Work mode", "Persona file"], labels);
        Assert.Empty(cut.FindAll(".mud-dialog-content .mud-paper-outlined"));
    }

    /// <summary>The Agent's handle is written <c>@alias</c>, not "Alias: alias" (B8f, design-system "Avatars and status").</summary>
    [Fact]
    public async Task ViewCard_AliasIsWrittenWithAnAtSign()
    {
        await using TeamWebApplicationFactory factory = new();
        await SeedPersonaAsync(factory, "coo", "x");
        await using MudBunitContext ctx = NewContext(factory);

        IRenderedComponent<ContainerFragment> cut = await OpenViewCardAsync(ctx, factory, "coo");

        Assert.Equal("@coo", cut.Find(".agent-profile-card-alias").TextContent.Trim());
        Assert.DoesNotContain("Alias:", cut.Find(".agent-profile-card-alias").ParentElement?.TextContent, StringComparison.Ordinal);
    }

    /// <summary>The destructive action is not a red icon among text buttons: the card has a more-actions menu named for its target and no "Remove" icon button (B8d, B10f).</summary>
    [Fact]
    public async Task ViewCard_RemovalSitsUnderANamedMenu_WithNoHoverTooltip()
    {
        await using TeamWebApplicationFactory factory = new();
        await SeedPersonaAsync(factory, "coo", "x");
        await using MudBunitContext ctx = NewContext(factory);

        IRenderedComponent<ContainerFragment> cut = await OpenViewCardAsync(ctx, factory, "coo");

        Assert.Empty(cut.FindAll("button[aria-label='Remove']"));
        Assert.Single(cut.FindAll("button[aria-label='Actions for coo']"));
        Assert.Empty(cut.FindAll(".mud-dialog-content .mud-tooltip-root"));
    }

    /// <summary>The adapter's commands are helper text under the Restart row, not part of the profile header (B8e).</summary>
    [Fact]
    public void CardSource_PutsTheAdaptersCommandsBesideRestart_NotInTheHeader()
    {
        string card = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "Components", "Shared", "AgentProfileCard.razor"));
        int restart = card.IndexOf("this.RestartAsync", StringComparison.Ordinal);
        int commands = card.IndexOf("this.offeredCommands", StringComparison.Ordinal);
        int firstSection = card.IndexOf("agent-profile-card-section", StringComparison.Ordinal);

        Assert.True(restart > 0 && commands > restart, "the commands must follow the Restart button");
        Assert.True(commands < firstSection, "the commands must come before the first section, with the action row");
    }

    /// <summary>Edit groups its fields under Identity, Persona and Model (B10 New agent).</summary>
    [Fact]
    public async Task EditCard_FieldsAreGroupedUnderGroupLabels()
    {
        await using TeamWebApplicationFactory factory = new();
        await SeedPersonaAsync(factory, "coo", "x");
        await using MudBunitContext ctx = NewContext(factory);

        IRenderedComponent<ContainerFragment> cut = await OpenViewCardAsync(ctx, factory, "coo");
        await ClickButtonAsync(cut, "Edit");

        string[] labels = [.. cut.FindAll(".mud-dialog-content .group-label").Select(static label => label.TextContent.Trim())];
        Assert.Equal(["Identity", "Persona", "Model"], labels);
    }

    /// <summary>The Create form's placeholders are "e.g." examples, never bare values, and the one text label reads "Persona text" (B10 New agent).</summary>
    [Fact]
    public async Task CreateCard_ExamplesAreMarkedAsExamples_AndThereIsOneTextLabel()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = NewContext(factory);

        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, factory);
        await ClickButtonAsync(cut, "New agent");

        string[] placeholders = [.. cut.FindAll(".mud-dialog-content input[placeholder], .mud-dialog-content textarea[placeholder]").Select(static field => field.GetAttribute("placeholder") ?? string.Empty)];
        Assert.NotEmpty(placeholders);
        Assert.All(placeholders, static placeholder => Assert.True(placeholder.StartsWith("e.g. ", StringComparison.Ordinal), placeholder));
        Assert.Equal(1, CountControlsLabelled(cut, "Persona text"));
        Assert.Equal(0, CountControlsLabelled(cut, "Persona body"));
    }

    /// <summary>UX-E15: the New agent form puts Title and Alias in one row, and Model and Effort in one row under a single Model label.</summary>
    [Fact]
    public async Task CreateCard_TitleAndAliasShareARow_AndModelEffortShareARow()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = NewContext(factory);

        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, factory);
        await ClickButtonAsync(cut, "New agent");

        AngleSharp.Dom.IElement identity = cut.Find(".mud-dialog-content .agent-profile-card-identity-row");
        Assert.Equal(2, identity.QuerySelectorAll("input").Length);
        Assert.Equal(1, identity.QuerySelectorAll("input[placeholder='e.g. coo']").Length);
        AngleSharp.Dom.IElement models = cut.Find(".mud-dialog-content .agent-profile-card-model-row");
        Assert.True(models.QuerySelectorAll(".mud-select").Length >= 2);
        Assert.NotNull(FindInputControl(cut, "Model").Closest(".agent-profile-card-model-row"));
        Assert.NotNull(FindInputControl(cut, "Effort").Closest(".agent-profile-card-model-row"));
        Assert.Equal(1, cut.FindAll(".mud-dialog-content .group-label").Count(static label => label.TextContent.Trim() == "Model"));
    }

    /// <summary>While the model list is being read the select refuses to open but is styled as busy rather than off: it carries aria-busy and the probing class, and a progress bar sits under it (E5).</summary>
    [Fact]
    public async Task EditCard_WhileTheModelsLoad_ShowsAProgressBarInsteadOfADisabledBox()
    {
        await using TeamWebApplicationFactory factory = new();
        factory.FakeModelCatalog.NeverCompletes = true;
        SeedPersonaWithModelAndEffort(factory, "coo", "x", model: "claude-sonnet-4", effort: null);
        await using MudBunitContext ctx = NewContext(factory);

        IRenderedComponent<ContainerFragment> cut = await OpenViewCardAsync(ctx, factory, "coo");
        await ClickButtonAsync(cut, "Edit");

        Assert.True(FindSelectCombobox(cut, "Model").HasAttribute("aria-busy"));
        Assert.Contains("agent-profile-card-probing", FindInputControl(cut, "Model").ClassList);
        Assert.NotEmpty(cut.FindAll(".agent-profile-card-probe[aria-label='Reading the models this agent offers']"));
    }

    /// <summary>The avatar dialog is titled with the item's name, "coo's avatar" (B10j).</summary>
    [Fact]
    public async Task AvatarDialog_TitleIsTheAgentsName()
    {
        await using TeamWebApplicationFactory factory = new();
        await SeedPersonaAsync(factory, "coo", "x");
        await using MudBunitContext ctx = NewContext(factory);

        IRenderedComponent<ContainerFragment> cut = await OpenViewCardAsync(ctx, factory, "coo");
        await OpenAvatarEditorAsync(cut);

        Assert.Equal("coo's avatar", cut.Find(EditorDialog + " .mud-dialog-title").TextContent.Trim());
    }

    /// <summary>The Background swatches are a radio group of named circles: one radio per palette colour, each with a colour name and none reachable only by pointer (B8h).</summary>
    [Fact]
    public async Task AvatarDialog_SwatchesAreANamedRadioGroup()
    {
        await using TeamWebApplicationFactory factory = new();
        await SeedPersonaAsync(factory, "coo", "x");
        await using MudBunitContext ctx = NewContext(factory);

        IRenderedComponent<ContainerFragment> cut = await OpenViewCardAsync(ctx, factory, "coo");
        await OpenAvatarEditorAsync(cut);
        await SelectAvatarChoiceAsync(cut, "Initials of the name");

        Assert.Single(cut.FindAll(BackgroundPicker + " [role='radiogroup'][aria-labelledby]"));
        var radios = cut.FindAll(Swatch);
        Assert.Equal(AvatarPalette.Colours.Count, radios.Count);
        Assert.All(radios, static radio => Assert.Equal("radio", radio.GetAttribute("type")));
        Assert.All(radios, static radio => Assert.Null(radio.GetAttribute("tabindex")));

        string[] names = [.. cut.FindAll(BackgroundPicker + " .avatar-swatch > .visually-hidden:not(input)").Select(static name => name.TextContent.Trim())];
        Assert.Equal(AvatarPalette.Swatches.Select(static swatch => swatch.Name), names);
    }

    /// <summary>Pressing a swatch checks that one radio and no other (B8h).</summary>
    [Fact]
    public async Task AvatarDialog_PressingASwatchChecksOnlyThatRadio()
    {
        await using TeamWebApplicationFactory factory = new();
        await SeedPersonaAsync(factory, "coo", "x");
        await using MudBunitContext ctx = NewContext(factory);

        IRenderedComponent<ContainerFragment> cut = await OpenViewCardAsync(ctx, factory, "coo");
        await OpenAvatarEditorAsync(cut);
        await SelectAvatarChoiceAsync(cut, "Initials of the name");
        await ClickAsync(cut, () => cut.FindAll(Swatch)[4]);

        List<bool> checkedFlags = [.. cut.FindAll(Swatch).Select(static radio => radio.HasAttribute("checked"))];
        Assert.Equal(1, checkedFlags.Count(static flag => flag));
        Assert.True(checkedFlags[4]);
    }

    /// <summary>
    /// Choosing from the library inside the avatar dialog does not open a third dialog: the picker replaces the editor's content
    /// with a Back button, the dialog reads "Choose an avatar" and hides its own Apply, and Back returns to the editor (B8k).
    /// </summary>
    [Fact]
    public async Task AvatarDialog_Library_ReplacesTheEditorInPlace_AndStaysTwoDialogsDeep()
    {
        await using TeamWebApplicationFactory factory = new();
        await SeedPersonaAsync(factory, "coo", "x");
        using TempDataDir library = CreateLibraryDir();
        await using MudBunitContext ctx = NewContext(factory);
        _ = Register(ctx.Services, library.Path, new AvatarLibraryImage(MinimalPng, ".png"));

        IRenderedComponent<ContainerFragment> cut = await OpenViewCardAsync(ctx, factory, "coo");
        await OpenAvatarEditorAsync(cut);
        await SelectAvatarChoiceAsync(cut, "Upload an image");
        await ClickEditorButtonAsync(cut, ButtonText);
        cut.WaitForAssertion(() => Assert.True(cut.FindAll(Thumb).Count > 3), TimeSpan.FromSeconds(5));

        Assert.Equal(2, cut.FindAll(".mud-dialog").Count);
        Assert.NotEmpty(cut.FindAll(EditorDialog + " .avatar-library-inline"));
        Assert.Equal("Choose an avatar", cut.Find(EditorDialog + " .mud-dialog-title").TextContent.Trim());
        Assert.True(HasEditorButton(cut, "Back"));
        Assert.False(HasEditorButton(cut, "Apply"));
        Assert.False(HasEditorButton(cut, "Save"));

        await ClickEditorButtonAsync(cut, "Back");

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(Thumb)), TimeSpan.FromSeconds(5));
        Assert.Equal(2, cut.FindAll(".mud-dialog").Count);
        Assert.Equal("coo's avatar", cut.Find(EditorDialog + " .mud-dialog-title").TextContent.Trim());
        Assert.True(HasEditorButton(cut, "Save"));
        Assert.True(HasEditorButton(cut, ButtonText));
    }

    /// <summary>Picking a thumbnail from the in-place library returns to the editor with that picture pending, still two dialogs deep (B8k).</summary>
    [Fact]
    public async Task AvatarDialog_Library_PickReturnsToTheEditorWithThePicture()
    {
        await using TeamWebApplicationFactory factory = new();
        await SeedPersonaAsync(factory, "coo", "x");
        using TempDataDir library = CreateLibraryDir();
        await using MudBunitContext ctx = NewContext(factory);
        PickLoader loader = Register(ctx.Services, library.Path, new AvatarLibraryImage(MinimalPng, ".png"));

        IRenderedComponent<ContainerFragment> cut = await OpenViewCardAsync(ctx, factory, "coo");
        await OpenAvatarEditorAsync(cut);
        await SelectAvatarChoiceAsync(cut, "Upload an image");
        await ClickEditorButtonAsync(cut, ButtonText);
        cut.WaitForAssertion(() => Assert.True(cut.FindAll(Thumb).Count > 3), TimeSpan.FromSeconds(5));
        await ClickAsync(cut, () => cut.FindAll(Thumb)[3]);

        cut.WaitForAssertion(() => Assert.Equal("New image selected (not yet saved)", EditorImageStatus(cut)), TimeSpan.FromSeconds(5));
        Assert.Equal("av-003.png", loader.LastFileName);
        Assert.Equal(2, cut.FindAll(".mud-dialog").Count);
        Assert.Empty(cut.FindAll(EditorDialog + " .avatar-library-inline"));
    }
}
