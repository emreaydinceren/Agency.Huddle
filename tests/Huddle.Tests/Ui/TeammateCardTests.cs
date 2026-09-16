using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Pipes;
using Agency.Huddle.App.Services;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Exercises <see cref="Agency.Huddle.App.Components.Shared.TeammateCard"/> through the real
/// <see cref="Agency.Huddle.App.Components.Pages.Teammates"/> page and the real
/// <c>IDialogService</c>, via <see cref="MudBunitContext.RenderWithPopovers"/> - not, as the retired
/// <c>HtmlRenderer</c> version did, by constructing <c>TeammateCard</c> in isolation from a bare
/// parameter dictionary.
///
/// This is not a stylistic choice: <c>MudDialog</c>'s own content only renders once it is registered
/// with a real <c>MudDialogInstance</c>, and that registration goes through
/// <c>IMudDialogInstanceInternal</c> - a type <c>internal</c> to MudBlazor's own assembly, which no
/// external test project can construct or substitute. A hand-written fake
/// <see cref="MudBlazor.IMudDialogInstance"/>, supplied directly to a standalone-rendered
/// <c>TeammateCard</c>, satisfies its public <c>[CascadingParameter]</c> but leaves that internal one
/// unset, so <c>MudDialog</c> reports itself "inline" with nothing telling it to be visible, and
/// renders nothing at all. The only way to see this card's content in a test is the only way a user
/// ever does: open it through <c>IDialogService.ShowAsync</c>, with a real <c>MudDialogProvider</c>
/// present. <see cref="MudBunitContext.RenderWithPopovers"/> already provides one (see its own remarks
/// for why it now provides both a popover and a dialog provider), so this file uses it, together with
/// <see cref="TeamWebApplicationFactory"/> for the real, fully-wired <see cref="PersonaStore"/> and
/// friends the page and card both inject.
///
/// Several of these tests dispatch a real click - Remove/Confirm, Close, and a Model change that
/// re-probes the Effort catalog - which the retired <c>HtmlRenderer</c> version had no supported way
/// to do at all.
/// </summary>
public sealed class TeammateCardTests
{
    [Fact]
    public async Task ViewMode_ShowsDetailsAndActions()
    {
        await using var factory = new TeamWebApplicationFactory();
        await SeedPersonaAsync(factory, "Chief of Staff", "You keep the team honest.");
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "Chief of Staff");

        Assert.Contains("Chief of Staff", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("You keep the team honest.", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Offline", cut.Markup, StringComparison.Ordinal);
        Assert.True(HasButton(cut, "Edit"));
        Assert.True(HasButton(cut, "Open"));
        Assert.True(HasButton(cut, "Remove"));

        // Viewing is not editing: the Persona text is shown, not offered for typing into.
        Assert.Empty(cut.FindAll("textarea"));
    }

    /// <summary>View mode is the one place Title, Alias and Teams are shown alongside the Name - Edit's identity area stays exactly as before (see <see cref="EditMode_DoesNotShowTitleAliasOrTeams"/>).</summary>
    [Fact]
    public async Task ViewMode_ShowsTitleAliasAndTeams()
    {
        await using var factory = new TeamWebApplicationFactory();
        Directory.CreateDirectory(factory.TeamsDirPath);
        await File.WriteAllTextAsync(
            Path.Combine(factory.TeamsDirPath, "jarvis.md"),
            "---\nName: Jarvis\nTitle: Chief of Staff\nAlias: jar\nTeams: Business, Household\n---\nYou are Jarvis.",
            Xunit.TestContext.Current.CancellationToken);
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "Jarvis");

        Assert.Contains("Chief of Staff", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("jar", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Business, Household", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>An empty Teams field shows no "Teams:" line at all rather than an empty one.</summary>
    [Fact]
    public async Task ViewMode_WithNoTeams_ShowsNoTeamsLine()
    {
        await using var factory = new TeamWebApplicationFactory();
        await SeedPersonaAsync(factory, "echo", "x");
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "echo");

        Assert.DoesNotContain("Teams:", cut.Markup, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Emily Lee", "EL")]
    [InlineData("Chief of Staff", "CS")]
    [InlineData("echo", "E")]
    public async Task ViewMode_ShowsMonogramFromFirstAndLastWord(string name, string expectedMonogram)
    {
        await using var factory = new TeamWebApplicationFactory();
        await SeedPersonaAsync(factory, name, "x");
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, name);

        var avatars = cut.FindAll(".mud-avatar");
        var avatar = avatars[^1];
        Assert.Equal(expectedMonogram, avatar.TextContent.Trim());
    }

    [Fact]
    public async Task ViewMode_OffersMessageOnlyOnceARoomExists()
    {
        await using var factory = new TeamWebApplicationFactory();
        await SeedPersonaAsync(factory, "echo", "x");
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "echo");

        // No Agent has ever connected in this test, so no Room exists yet for the card to offer.
        Assert.DoesNotContain("Message", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EditMode_OffersTheTextButNotTheName()
    {
        await using var factory = new TeamWebApplicationFactory();
        await SeedPersonaAsync(factory, "Chief of Staff", "You keep the team honest.");
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "Chief of Staff");
        FindButton(cut, "Edit").Click();

        Assert.NotEmpty(cut.FindAll("textarea"));
        Assert.Contains("You keep the team honest.", cut.Markup, StringComparison.Ordinal);
        Assert.True(HasButton(cut, "Save"));

        // Renaming a Persona would mean renaming its file and re-registering its Agent under a new
        // identity, so the Name is deliberately not editable here: no Name text box exists in Edit
        // mode, only the Persona textarea and the Model/Effort selects.
        Assert.Empty(cut.FindAll("input[placeholder='Chief of Staff']"));
    }

    [Fact]
    public async Task EditMode_WarnsThatSavingClearsMemory()
    {
        await using var factory = new TeamWebApplicationFactory();
        await SeedPersonaAsync(factory, "coo", "x");
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");
        FindButton(cut, "Edit").Click();

        Assert.Contains("clears what it remembers", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>Edit's identity area is unchanged by this phase: only the Name and status show, never Title/Alias/Teams - the textarea is still a raw-file editor over the whole file, front matter included, so those fields would be redundant with it.</summary>
    [Fact]
    public async Task EditMode_DoesNotShowTitleAliasOrTeams()
    {
        await using var factory = new TeamWebApplicationFactory();
        await File.WriteAllTextAsync(
            EnsureTeamsDir(factory),
            "---\nName: coo\nTitle: Legendary Assistant\nAlias: coo\nTeams: Business\n---\nx",
            Xunit.TestContext.Current.CancellationToken);
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");
        FindButton(cut, "Edit").Click();

        // Scoped to the dialog itself: the page's own tile behind it legitimately shows "Legendary
        // Assistant" as the tile's role text, which a whole-page search would trip over. Edit mode's
        // textarea is a raw-file editor over the WHOLE file, front matter included, so
        // "Title: Legendary Assistant" is expected to appear there verbatim too - what must not happen
        // is Title/Alias/Teams showing again as SEPARATE identity fields outside it, the way View mode
        // shows them. Excise the textarea's own content before searching, so the raw file text itself
        // does not produce a false failure.
        var dialogMarkup = cut.Find(".mud-dialog-container").InnerHtml;
        var rawFileText = cut.Find("textarea").TextContent;
        var dialogMarkupOutsideTextarea = dialogMarkup.Replace(rawFileText, string.Empty, StringComparison.Ordinal);

        Assert.DoesNotContain("Legendary Assistant", dialogMarkupOutsideTextarea, StringComparison.Ordinal);
        Assert.DoesNotContain("Alias:", dialogMarkupOutsideTextarea, StringComparison.Ordinal);
        Assert.DoesNotContain("Teams:", dialogMarkupOutsideTextarea, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateMode_OffersNameTitleAliasTeamsAndText()
    {
        await using var factory = new TeamWebApplicationFactory();
        await using var ctx = NewContext(factory);

        var cut = RenderPage(ctx, factory);
        FindButton(cut, "New teammate").Click();

        Assert.NotEmpty(cut.FindAll("input[placeholder='Chief of Staff']"));
        Assert.NotEmpty(cut.FindAll("textarea"));
        Assert.Contains("New teammate", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Title", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Alias", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Teams", cut.Markup, StringComparison.Ordinal);

        // The card is where a user learns that a Name may hold spaces at all.
        Assert.Contains("Spaces are fine.", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>The Create textarea is the Persona's BODY only, not the whole file - its label and hint must say so, distinct from Edit's "whole file" wording.</summary>
    [Fact]
    public async Task CreateMode_TextareaIsExplicitlyTheBodyOnly()
    {
        await using var factory = new TeamWebApplicationFactory();
        await using var ctx = NewContext(factory);

        var cut = RenderPage(ctx, factory);
        FindButton(cut, "New teammate").Click();

        Assert.Contains("Persona body", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("no front matter needed here", cut.Markup, StringComparison.Ordinal);

        // "Persona text" (Edit's label) and "front matter included" (Edit's hint) must not leak into
        // Create's copy - the two modes describe two different things in the same textarea.
        Assert.DoesNotContain("Persona text", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("front matter included", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>A rejected Save (an invalid Name) shows the error on the still-open card rather than losing the draft.</summary>
    [Fact]
    public async Task Error_IsShownOnTheCardRatherThanLosingTheDraft()
    {
        await using var factory = new TeamWebApplicationFactory();
        await using var ctx = NewContext(factory);

        var cut = RenderPage(ctx, factory);
        FindButton(cut, "New teammate").Click();

        SetTextValue(cut, "Name", "bad/name");
        SetTextValue(cut, "Title", "Title");
        SetTextValue(cut, "Alias", "alias");
        SetTextValue(cut, "Persona body", "a draft worth keeping");
        FindButton(cut, "Add teammate").Click();

        Assert.Contains("is not a valid Persona name.", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("a draft worth keeping", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>The inline confirm on Remove: clicking Remove swaps it for Confirm/Cancel in place, never a nested dialog.</summary>
    [Fact]
    public async Task ConfirmingRemove_ReplacesRemoveWithAConfirmStep()
    {
        await using var factory = new TeamWebApplicationFactory();
        await SeedPersonaAsync(factory, "coo", "x");
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");
        FindButton(cut, "Remove").Click();

        Assert.True(HasButton(cut, "Confirm"));
        Assert.False(HasButton(cut, "Remove"));
    }

    /// <summary>Confirming Remove actually removes the Persona and closes the dialog.</summary>
    [Fact]
    public async Task ConfirmRemove_RemovesThePersonaAndClosesTheDialog()
    {
        await using var factory = new TeamWebApplicationFactory();
        await SeedPersonaAsync(factory, "coo", "x");
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");
        FindButton(cut, "Remove").Click();
        FindButton(cut, "Confirm").Click();

        Assert.Null(factory.Services.GetRequiredService<PersonaStore>().Get("coo"));
        Assert.Empty(cut.FindAll(".mud-dialog-container"));
    }

    /// <summary>Clicking Close asks the dialog to close - the <c>MudIconButton</c> replacement for the old <c>&amp;times;</c> button.</summary>
    [Fact]
    public async Task CloseButton_ClosesTheDialog()
    {
        await using var factory = new TeamWebApplicationFactory();
        await SeedPersonaAsync(factory, "coo", "x");
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");
        cut.Find("button[aria-label='Close']").Click();

        Assert.Empty(cut.FindAll(".mud-dialog-container"));
    }

    /// <summary>
    /// <c>OpenInEditor</c>'s null-return branch is deliberately not covered here, and this note
    /// records why so nobody reads the gap as an oversight. <c>Process.Start</c> is not injectable in
    /// this codebase, so the case the null-check exists for - <c>Process.Start</c> returning
    /// <see langword="null"/> under <c>UseShellExecute</c> when no application is registered for
    /// <c>.md</c> - cannot be driven from a test without a seam that exists only for the test. It
    /// cannot be reached indirectly either: clicking "Open" on a Persona whose file still exists
    /// invokes the real <c>Process.Start</c> and launches a real OS process, which is an
    /// environment-dependent side effect rather than a deterministic assertion.
    ///
    /// What this test does pin is the sibling guard the fix must not regress: a Persona removed out
    /// from under an already-open card still makes <c>PersonaStore.PathFor</c> throw
    /// <see cref="ChatException"/> before <c>Process.Start</c> is ever reached, and the existing catch
    /// block still reports it via <c>error</c> - "Could not open" is not the only message this method
    /// can show.
    /// </summary>
    [Fact]
    public async Task OpenInEditor_PersonaRemovedFirst_ReportsWithoutLaunchingAProcess()
    {
        await using var factory = new TeamWebApplicationFactory();
        await SeedPersonaAsync(factory, "coo", "x");
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");
        factory.Services.GetRequiredService<PersonaStore>().Remove("coo");
        FindButton(cut, "Open").Click();

        Assert.Contains("Persona 'coo' does not exist.", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateMode_OffersAModelChoice()
    {
        await using var factory = new TeamWebApplicationFactory();
        factory.FakeModelCatalog.Models = [new("claude-opus-4", "Opus", null), new("claude-sonnet-4", "Sonnet", null)];
        await using var ctx = NewContext(factory);

        var cut = RenderPage(ctx, factory);
        FindButton(cut, "New teammate").Click();
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
        FindButton(cut, "New teammate").Click();

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
        FindButton(cut, "Edit").Click();

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
        FindButton(cut, "Edit").Click();

        var modelInput = FindSelectInput(cut, "Model");
        Assert.Equal("claude-vintage-1", modelInput.GetAttribute("value"));
    }

    [Fact]
    public async Task CreateMode_NoModelsAdvertised_StillRenders()
    {
        await using var factory = new TeamWebApplicationFactory();
        await using var ctx = NewContext(factory);

        var cut = RenderPage(ctx, factory);
        FindButton(cut, "New teammate").Click();

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
        FindButton(cut, "New teammate").Click();

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
        FindButton(cut, "New teammate").Click();
        SetTextValue(cut, "Name", "coo");
        SetTextValue(cut, "Title", "Chief of Staff");
        SetTextValue(cut, "Alias", "coo");
        SetTextValue(cut, "Persona body", "You are the Chief of Staff.");
        await OpenSelectAsync(cut, "Model");
        (await FindSelectItemAsync(cut, "Opus")).Click();
        FindButton(cut, "Add teammate").Click();

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
        FindButton(cut, "New teammate").Click();
        SetTextValue(cut, "Name", "coo");
        SetTextValue(cut, "Title", "Chief of Staff");
        SetTextValue(cut, "Alias", "coo");
        SetTextValue(cut, "Persona body", "You are the Chief of Staff.");
        await OpenSelectAsync(cut, "Effort");
        (await FindSelectItemAsync(cut, "High")).Click();
        FindButton(cut, "Add teammate").Click();

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
        FindButton(cut, "New teammate").Click();
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
        FindButton(cut, "Edit").Click();

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
        FindButton(cut, "Edit").Click();

        var effortInput = FindSelectInput(cut, "Effort");
        Assert.Equal("vintage", effortInput.GetAttribute("value"));
    }

    [Fact]
    public async Task CreateMode_NoEffortLevelsAdvertised_StillRenders()
    {
        await using var factory = new TeamWebApplicationFactory();
        await using var ctx = NewContext(factory);

        var cut = RenderPage(ctx, factory);
        FindButton(cut, "New teammate").Click();

        Assert.Contains("offers no effort choice", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateMode_WhileTheEffortCatalogLoads_SaysSo()
    {
        await using var factory = new TeamWebApplicationFactory();
        factory.FakeModelCatalog.EffortsNeverComplete = true;
        await using var ctx = NewContext(factory);

        var cut = RenderPage(ctx, factory);
        FindButton(cut, "New teammate").Click();

        Assert.Contains("Reading the effort levels this model offers", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>An Offline status's reason renders as its own line under the status, not just the badge, and Restart is offered - the one status besides Degraded that has something for it to fix.</summary>
    [Fact]
    public async Task TeammateCard_ShowsTheReasonWhenOffline_AndOffersRestart()
    {
        await using var factory = new TeamWebApplicationFactory();
        await SeedPersonaAsync(factory, "coo", "x");
        factory.Services.GetRequiredService<PersonaHealth>().Report("coo", PersonaState.Offline, "The Adapter needs authentication.");
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");

        Assert.Contains("Offline", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("The Adapter needs authentication.", cut.Markup, StringComparison.Ordinal);
        Assert.True(HasButton(cut, "Restart"));
    }

    /// <summary>A Degraded status renders its own label and reason, distinct from Offline, and offers Restart.</summary>
    [Fact]
    public async Task TeammateCard_ShowsDegradedAndOffersRestart()
    {
        await using var factory = new TeamWebApplicationFactory();
        await SeedPersonaAsync(factory, "coo", "x");
        await MakeOnlineAsync(factory, "coo");
        factory.Services.GetRequiredService<PersonaHealth>().Report("coo", PersonaState.Degraded, "The Reply Gate has not answered in a while.");
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");

        Assert.Contains("Degraded", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("The Reply Gate has not answered in a while.", cut.Markup, StringComparison.Ordinal);
        Assert.True(HasButton(cut, "Restart"));
    }

    /// <summary>
    /// An Online, healthy teammate offers Restart too. It once did not, on the grounds that there was
    /// nothing for it to fix - but a NextSession Hook edit can only reach a Teammate through a new
    /// session, and rules.md forbids a Hook edit restarting one by itself, so hiding Restart here left
    /// editing the Persona text as the only way to apply one.
    /// </summary>
    [Fact]
    public async Task TeammateCard_Online_OffersRestart()
    {
        await using var factory = new TeamWebApplicationFactory();
        await SeedPersonaAsync(factory, "coo", "x");
        await MakeOnlineAsync(factory, "coo");
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");

        Assert.Contains("Online", cut.Markup, StringComparison.Ordinal);
        Assert.True(HasButton(cut, "Restart"));
    }

    /// <summary>
    /// The open card follows live health/presence, exactly like the tile behind it: rules.md's
    /// "Health outranks pipe liveness" applies to every surface rendering this badge. A card that kept
    /// showing its opening snapshot would still be claiming Online for an Agent that just went
    /// offline, and the reason line under the badge would never appear. Drives the same transition
    /// <see cref="TeammatesPageTests.TeammatesPage_RepaintsWhenAnAgentGoesOffline"/> does for the
    /// tile, but on an already-open card.
    /// </summary>
    [Fact]
    public async Task ViewMode_RepaintsAndOffersRestart_WhenTheAgentGoesOffline()
    {
        await using var factory = new TeamWebApplicationFactory();
        await SeedPersonaAsync(factory, "coo", "x");
        await MakeOnlineAsync(factory, "coo");
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");
        Assert.Contains("Online", cut.Markup, StringComparison.Ordinal);

        var agent = await factory.Services.GetRequiredService<ITeamDirectory>().FindUserByNameAsync("coo", Xunit.TestContext.Current.CancellationToken);
        Assert.NotNull(agent);
        factory.FakeAgentGateway.SetOffline(agent.Id);

        while (!cut.Markup.Contains("Offline", StringComparison.Ordinal))
        {
            await Task.Delay(20, Xunit.TestContext.Current.CancellationToken);
        }

        Assert.True(HasButton(cut, "Restart"));
    }

    /// <summary>A Starting status renders "Starting", never "Offline" - a Persona launching for the first time must not read as a fault - and offers no Restart either.</summary>
    [Fact]
    public async Task TeammateCard_ShowsStartingRatherThanOffline_AndOffersNoRestart()
    {
        await using var factory = new TeamWebApplicationFactory();
        await SeedPersonaAsync(factory, "coo", "x");

        // PersonaStatusResolver's rule 2 forces Offline whenever the pipe is not connected,
        // regardless of what health reports - so Starting is only observable once the Agent is
        // connected too (see MakeOnlineAsync).
        await MakeOnlineAsync(factory, "coo");
        factory.Services.GetRequiredService<PersonaHealth>().Report("coo", PersonaState.Starting, null);
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");

        Assert.Contains("Starting", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Offline", cut.Markup, StringComparison.Ordinal);
        Assert.False(HasButton(cut, "Restart"));
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
        FindButton(cut, "New teammate").Click();

        Assert.Equal(1, factory.FakeModelCatalog.EffortProbeCount);
        Assert.Contains(null, factory.FakeModelCatalog.EffortProbedModels);

        await OpenSelectAsync(cut, "Model");
        (await FindSelectItemAsync(cut, "Sonnet")).Click();

        Assert.Equal(2, factory.FakeModelCatalog.EffortProbeCount);
        Assert.Contains("claude-sonnet-4", factory.FakeModelCatalog.EffortProbedModels);
    }

    /// <summary>Registers this factory's real services (<see cref="PersonaStore"/> and friends) into a fresh <see cref="MudBunitContext"/>, the same pattern <see cref="TeammatesPageTests"/> uses.</summary>
    private static MudBunitContext NewContext(TeamWebApplicationFactory factory)
    {
        MudBunitContext ctx = new();
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<PersonaStore>());
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<ITeamDirectory>());
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<IAgentGateway>());
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<PersonaHealth>());
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<PersonaSupervisor>());
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<RoomEvents>());
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<IModelCatalog>());
        return ctx;
    }

    /// <summary>Renders the real <see cref="Agency.Huddle.App.Components.Pages.Teammates"/> page, with the popover and dialog providers <see cref="MudBunitContext.RenderWithPopovers"/> supplies so an opened card actually renders (see the type-level remarks) and a <c>MudSelect</c> inside it can too.</summary>
    private static IRenderedComponent<ContainerFragment> RenderPage(MudBunitContext ctx, TeamWebApplicationFactory factory)
    {
        _ = factory;
        return ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<Agency.Huddle.App.Components.Pages.Teammates>(0);
            builder.CloseComponent();
        });
    }

    /// <summary>Renders the page and clicks the tile matching <paramref name="personaName"/>, opening its View card.</summary>
    private static async Task<IRenderedComponent<ContainerFragment>> OpenViewCardAsync(MudBunitContext ctx, TeamWebApplicationFactory factory, string personaName)
    {
        await Task.Yield();
        var cut = RenderPage(ctx, factory);
        cut.FindAll("button.teammate-tile").First(tile => tile.TextContent.Contains(personaName, StringComparison.Ordinal)).Click();
        return cut;
    }

    /// <summary>Writes a minimally-valid Persona file (Name, Title and Alias all <paramref name="name"/>) and ensures the factory's Teams directory exists.</summary>
    private static async Task SeedPersonaAsync(TeamWebApplicationFactory factory, string name, string body)
    {
        Directory.CreateDirectory(factory.TeamsDirPath);
        await File.WriteAllTextAsync(Path.Combine(factory.TeamsDirPath, $"{SanitizeFileName(name)}.md"), $"---\nName: {name}\nTitle: {name}\nAlias: {name}\n---\n{body}");
    }

    private static string EnsureTeamsDir(TeamWebApplicationFactory factory)
    {
        Directory.CreateDirectory(factory.TeamsDirPath);
        return Path.Combine(factory.TeamsDirPath, "coo.md");
    }

    private static string SanitizeFileName(string name) => name.Replace(' ', '_');

    /// <summary>
    /// Adds a Persona directly through the factory's real <see cref="PersonaStore"/>, with a stored
    /// Model and/or Effort - the only way to seed those, since they live in the SQLite-backed
    /// <c>persona_models</c>/<c>persona_efforts</c> tables, not the Persona file itself.
    /// </summary>
    /// <param name="factory">The factory whose <see cref="PersonaStore"/> to add the Persona through.</param>
    /// <param name="name">The Persona's Name, Title and Alias alike.</param>
    /// <param name="body">The Persona's system-prompt body.</param>
    /// <param name="model">The Model to store, or <see langword="null"/> for the agent's default.</param>
    /// <param name="effort">The Effort to store, or <see langword="null"/> for the model's default.</param>
    private static void SeedPersonaWithModelAndEffort(TeamWebApplicationFactory factory, string name, string body, string? model, string? effort)
    {
        factory.Services.GetRequiredService<PersonaStore>().Add(new PersonaIdentity(name, name, name, []), body, model, effort);
    }

    /// <summary>Registers <paramref name="name"/>'s Agent with the real <see cref="ITeamDirectory"/> and marks it connected in <see cref="TeamWebApplicationFactory.FakeAgentGateway"/>, so <see cref="PersonaStatusResolver"/> resolves it Online.</summary>
    private static async Task MakeOnlineAsync(TeamWebApplicationFactory factory, string name)
    {
        var directory = factory.Services.GetRequiredService<ITeamDirectory>();
        await directory.InitializeAsync("You");
        var agent = await directory.UpsertAgentUserAsync(name, null);
        Assert.NotNull(agent);
        factory.FakeAgentGateway.SetOnline(agent.Id);
    }

    private static bool HasButton(IRenderedComponent<ContainerFragment> cut, string text) =>
        cut.FindAll("button").Any(button => button.TextContent.Trim() == text);

    private static IElement FindButton(IRenderedComponent<ContainerFragment> cut, string text) =>
        cut.FindAll("button").First(button => button.TextContent.Trim() == text);

    private static IElement FindInputControl(IRenderedComponent<ContainerFragment> cut, string label) =>
        cut.FindAll("div.mud-input-control")
            .First(control => control.QuerySelectorAll("label").Any(l => l.TextContent.Contains(label, StringComparison.Ordinal)));

    /// <summary>The <c>&lt;input&gt;</c> under a <c>MudSelect</c> labelled <paramref name="label"/> - its closed-state value is the currently selected item's own display text, which is how a "preselected" assertion can be made without opening the popover at all.</summary>
    private static IElement FindSelectInput(IRenderedComponent<ContainerFragment> cut, string label) =>
        FindInputControl(cut, label).QuerySelector("input") ?? throw new InvalidOperationException($"No input under the '{label}' select.");

    private static async Task OpenSelectAsync(IRenderedComponent<ContainerFragment> cut, string label)
    {
        await FindInputControl(cut, label).MouseDownAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
    }

    private static async Task<IElement> FindSelectItemAsync(IRenderedComponent<ContainerFragment> cut, string text)
    {
        await Task.Yield();
        return cut.FindAll("div.mud-list-item").First(item => item.TextContent.Trim() == text);
    }

    private static void SetTextValue(IRenderedComponent<ContainerFragment> cut, string label, string value)
    {
        var control = FindInputControl(cut, label);
        var input = control.QuerySelector("input") ?? control.QuerySelector("textarea") ?? throw new InvalidOperationException($"No input/textarea under '{label}'.");
        input.Change(value);
    }
}
