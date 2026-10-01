using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Skills;
using Agency.Huddle.App.Teammates;
using static Agency.Huddle.Tests.Ui.TeammateCardTestSupport;

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

        // Remove is an icon-only MudIconButton (a destructive action gets an icon; its Confirm/Cancel
        // step below stays text, per the file's own comment), so this is matched by aria-label rather
        // than text content - see HasButton's remarks.
        Assert.True(HasButton(cut, "Remove"));

        // Viewing is not editing: the Persona text is shown, not offered for typing into.
        Assert.Empty(cut.FindAll("textarea"));
    }

    /// <summary>What a Teammate's Adapter has reported spending shows as one line under its status: the amount to three decimals in the current culture, then the currency.</summary>
    [Fact]
    public async Task SpendLine_ShowsThreeDecimalsAndTheCurrency()
    {
        await using var factory = new TeamWebApplicationFactory();
        await SeedPersonaAsync(factory, "coo", "x");
        factory.Services.GetRequiredService<PersonaSpend>().Add("coo", "s1", 0.0908m, "USD");
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");

        var line = Assert.Single(cut.FindAll(".teammate-card-spend"));
        Assert.Equal($"Spent since start: {0.091m.ToString("N3", System.Globalization.CultureInfo.CurrentCulture)} USD", line.TextContent.Trim());
    }

    /// <summary>A Teammate that reports no cost, such as a local model, has no Spend line at all, never a zero.</summary>
    [Fact]
    public async Task SpendLine_IsAbsent_WhenNothingWasReported()
    {
        await using var factory = new TeamWebApplicationFactory();
        await SeedPersonaAsync(factory, "coo", "x");
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");

        Assert.Empty(cut.FindAll(".teammate-card-spend"));
        Assert.DoesNotContain("Spent since start", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>An open card follows a new report without being reopened: a MudDialog freezes its parameters, so the card subscribes itself.</summary>
    [Fact]
    public async Task SpendLine_UpdatesOnSpendChanged()
    {
        await using var factory = new TeamWebApplicationFactory();
        await SeedPersonaAsync(factory, "coo", "x");
        await using var ctx = NewContext(factory);
        var cut = await OpenViewCardAsync(ctx, factory, "coo");
        Assert.Empty(cut.FindAll(".teammate-card-spend"));

        factory.Services.GetRequiredService<PersonaSpend>().Add("coo", "s1", 0.25m, "USD");

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".teammate-card-spend")));
    }

    /// <summary>A Teammate billed in two currencies shows one line for each, never a sum across them.</summary>
    [Fact]
    public async Task TwoCurrencies_ShowTwoLines()
    {
        await using var factory = new TeamWebApplicationFactory();
        await SeedPersonaAsync(factory, "coo", "x");
        var spend = factory.Services.GetRequiredService<PersonaSpend>();
        spend.Add("coo", "s1", 0.4m, "USD");
        spend.Add("coo", "s1", 1.25m, "EUR");
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");

        var culture = System.Globalization.CultureInfo.CurrentCulture;
        Assert.Equal(
            [
                $"Spent since start: {1.25m.ToString("N3", culture)} EUR",
                $"Spent since start: {0.4m.ToString("N3", culture)} USD",
            ],
            cut.FindAll(".teammate-card-spend").Select(line => line.TextContent.Trim()));
    }

    /// <summary>The card unsubscribes when it goes, because <see cref="PersonaSpend"/> outlives it and a leaked handler would keep every closed card alive.</summary>
    [Fact]
    public async Task Dispose_Unsubscribes()
    {
        await using var factory = new TeamWebApplicationFactory();
        await SeedPersonaAsync(factory, "coo", "x");
        var spend = factory.Services.GetRequiredService<PersonaSpend>();
        await using var ctx = NewContext(factory);
        _ = await OpenViewCardAsync(ctx, factory, "coo");
        Assert.Equal(1, spend.SubscriberCount);

        await ctx.DisposeComponentsAsync();

        Assert.Equal(0, spend.SubscriberCount);
    }

    /// <summary>Spend is shown when reading the card, not in Edit, where the card is a form for changing the Teammate.</summary>
    [Fact]
    public async Task SpendLine_IsNotShownInEditMode()
    {
        await using var factory = new TeamWebApplicationFactory();
        await SeedPersonaAsync(factory, "coo", "x");
        factory.Services.GetRequiredService<PersonaSpend>().Add("coo", "s1", 0.25m, "USD");
        await using var ctx = NewContext(factory);
        var cut = await OpenViewCardAsync(ctx, factory, "coo");
        Assert.NotEmpty(cut.FindAll(".teammate-card-spend"));

        await ClickButtonAsync(cut, "Edit");

        Assert.Empty(cut.FindAll(".teammate-card-spend"));
    }

    /// <summary>View mode is the one place Title, Alias and Teams are shown alongside the Name - Edit's top identity area stays as before (see <see cref="EditMode_TopIdentityAreaStillOmitsTitleAliasAndTeamsAsReadOnlyText"/>).</summary>
    [Fact]
    public async Task ViewMode_ShowsTitleAliasAndTeams()
    {
        await using var factory = new TeamWebApplicationFactory();
        await factory.WriteDefinitionAsync(
            "jarvis",
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

        // Scoped to the open dialog, not "the last .mud-avatar on the whole page": since D15, the
        // "Chief of Staff" row above seeds its own Persona of that exact Name, so
        // BuiltinTeammateSeeder's free-name search writes a second, real "Chief of Staff 2" - whose
        // own tile avatar can render after the dialog's in the DOM and would otherwise be "last".
        var dialogContainer = cut.Find(".mud-dialog-container");
        var avatars = dialogContainer.QuerySelectorAll(".mud-avatar");
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

    /// <summary>
    /// Edit now offers Name/Title/Alias boxes above the whole-file textarea, seeded from the Persona's
    /// frontmatter - unlike the retired behaviour this test used to pin, a rename is now possible from
    /// here (see traps.md's "Editing a Persona's name: renames the Teammate" and the rename warning
    /// this task adds). Two inputs share the "Chief of Staff" placeholder (Name and Title use the same
    /// wording as Create's own boxes), because this seeded Persona's Name, Title and Alias are all the
    /// same string.
    /// </summary>
    [Fact]
    public async Task EditMode_OffersNameTitleAliasAndText()
    {
        await using var factory = new TeamWebApplicationFactory();
        await SeedPersonaAsync(factory, "Chief of Staff", "You keep the team honest.");
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "Chief of Staff");
        await ClickButtonAsync(cut, "Edit");

        Assert.NotEmpty(cut.FindAll("textarea"));
        Assert.Contains("You keep the team honest.", cut.Markup, StringComparison.Ordinal);
        Assert.True(HasButton(cut, "Save"));
        Assert.Equal(2, cut.FindAll("input[placeholder='Chief of Staff']").Count);
        Assert.NotEmpty(cut.FindAll("input[placeholder='coo']"));
    }

    [Fact]
    public async Task EditMode_WarnsThatSavingClearsMemory()
    {
        await using var factory = new TeamWebApplicationFactory();
        await SeedPersonaAsync(factory, "coo", "x");
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");
        await ClickButtonAsync(cut, "Edit");

        Assert.Contains("clears what it remembers", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// Edit's ORIGINAL top identity area (the avatar, Name and status, beside the avatar) still shows
    /// no Title/Alias/Teams as read-only text - View mode is the only place those appear that way. This
    /// task's Title and Alias boxes live somewhere else entirely (above the raw text field, as editable
    /// inputs, not a duplicate of View's read-only lines), and Teams still has no box or line anywhere
    /// in Edit mode.
    /// </summary>
    [Fact]
    public async Task EditMode_TopIdentityAreaStillOmitsTitleAliasAndTeamsAsReadOnlyText()
    {
        await using var factory = new TeamWebApplicationFactory();
        await File.WriteAllTextAsync(
            EnsureTeamsDir(factory),
            "---\nName: coo\nTitle: Legendary Assistant\nAlias: coo\nTeams: Business\n---\nx",
            Xunit.TestContext.Current.CancellationToken);
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");
        await ClickButtonAsync(cut, "Edit");

        // Scoped to the open dialog's own avatar-and-identity stack, not the whole dialog: the dialog
        // now legitimately contains "Legendary Assistant" both inside the raw textarea and inside the
        // new Title box's value attribute, neither of which is the read-only duplication this test
        // guards against.
        var dialogContainer = cut.Find(".mud-dialog-container");
        var avatar = dialogContainer.QuerySelector(".mud-avatar") ?? throw new InvalidOperationException("No avatar in the open dialog.");
        var identityMarkup = avatar.ParentElement?.OuterHtml ?? string.Empty;

        Assert.DoesNotContain("Alias:", identityMarkup, StringComparison.Ordinal);
        Assert.DoesNotContain("Teams:", identityMarkup, StringComparison.Ordinal);

        // The new Title/Alias boxes ARE present elsewhere in Edit mode, but Teams still has no box.
        Assert.NotEmpty(dialogContainer.QuerySelectorAll("input[placeholder='coo']"));
        Assert.Empty(dialogContainer.QuerySelectorAll("input[placeholder='Business, Household']"));
    }

    /// <summary>Typing a new Title rewrites the frontmatter inside the raw text field beneath it - nothing is written to disk, since only Save does that.</summary>
    [Fact]
    public async Task EditMode_TypingATitle_UpdatesTheFrontmatterInTheRawText()
    {
        await using var factory = new TeamWebApplicationFactory();
        await SeedPersonaAsync(factory, "coo", "You are the Chief of Staff.");
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");
        await ClickButtonAsync(cut, "Edit");

        await SetTextValueAsync(cut, "Title", "Head of Everything");

        Assert.Contains("Head of Everything", cut.Find("textarea").TextContent, StringComparison.Ordinal);

        var stored = factory.Services.GetRequiredService<PersonaStore>().Get("coo");
        Assert.NotNull(stored);
        Assert.Contains("Title: coo", stored.Text, StringComparison.Ordinal);
    }

    /// <summary>Editing the raw Persona text re-seeds the Name/Title/Alias boxes from whatever <see cref="PersonaFrontmatter.TryReadIdentity"/> now parses - the reverse direction of typing into a box.</summary>
    [Fact]
    public async Task EditMode_EditingTheRawText_ReseedsTheBoxes()
    {
        await using var factory = new TeamWebApplicationFactory();
        await SeedPersonaAsync(factory, "coo", "You are the Chief of Staff.");
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");
        await ClickButtonAsync(cut, "Edit");

        await SetTextValueAsync(cut, "Persona text", "---\nName: coo\nTitle: Reseeded Title\nAlias: newalias\n---\nYou are the Chief of Staff.");

        var titleInput = FindInputControl(cut, "Title").QuerySelector("input") ?? throw new InvalidOperationException("No Title input.");
        var aliasInput = FindInputControl(cut, "Alias").QuerySelector("input") ?? throw new InvalidOperationException("No Alias input.");

        Assert.Equal("Reseeded Title", titleInput.GetAttribute("value"));
        Assert.Equal("newalias", aliasInput.GetAttribute("value"));
    }

    /// <summary>
    /// A Title written as a YAML block scalar cannot be rewritten by <see cref="PersonaFrontmatter.WriteScalarField"/>'s
    /// one-line rewrite, so typing into the Title box does not change the raw text - the box locks
    /// read-only instead, with a helper line pointing at the Persona text below.
    /// </summary>
    [Fact]
    public async Task EditMode_TitleWrittenAsABlockScalar_LocksTheTitleBoxReadOnly()
    {
        await using var factory = new TeamWebApplicationFactory();
        await File.WriteAllTextAsync(
            EnsureTeamsDir(factory),
            "---\nname: coo\ntitle: >\n  Legendary Assistant\n  and Chief of Staff\nalias: coo\n---\nx",
            Xunit.TestContext.Current.CancellationToken);
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");
        await ClickButtonAsync(cut, "Edit");

        await SetTextValueAsync(cut, "Title", "New Title");

        var titleInput = FindInputControl(cut, "Title").QuerySelector("input") ?? throw new InvalidOperationException("No Title input.");
        Assert.True(titleInput.HasAttribute("readonly"));
        Assert.Contains("can only be edited there", cut.Markup, StringComparison.Ordinal);

        // The refused edit never reached the raw text - the block scalar is still there, untouched.
        Assert.Contains("title: >", cut.Find("textarea").TextContent, StringComparison.Ordinal);
    }

    /// <summary>The rename warning appears only once the Name box's value differs from the frozen Name parameter the card opened with, and not before.</summary>
    [Fact]
    public async Task EditMode_RenameWarning_AppearsOnlyAfterTheNameChanges()
    {
        await using var factory = new TeamWebApplicationFactory();
        await SeedPersonaAsync(factory, "coo", "You are the Chief of Staff.");
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");
        await ClickButtonAsync(cut, "Edit");

        Assert.DoesNotContain("keep the old name", cut.Markup, StringComparison.Ordinal);

        await SetTextValueAsync(cut, "Name", "newcoo");

        Assert.Contains("keep the old name", cut.Markup, StringComparison.Ordinal);

        // The note must not claim the old, pre-ADR-0011 behaviour. A rename now carries the Agent,
        // its Rooms and their Transcripts with it, so this wording would be a lie rather than a
        // warning - and it is the kind of lie only a string assertion catches.
        Assert.DoesNotContain("stay behind under the old name", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// After renaming through the Name box and Saving, the rename moves the whole Teammate folder
    /// and renames the definition file, and the card lands on the NEW Persona - the displayed Name
    /// and file path both follow the move - rather than looking up the stale OLD name.
    /// </summary>
    [Fact]
    public async Task SaveAsync_AfterARename_LandsOnTheNewPersona()
    {
        await using var factory = new TeamWebApplicationFactory();
        await SeedPersonaAsync(factory, "coo", "You are the Chief of Staff.");
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");
        await ClickButtonAsync(cut, "Edit");

        await SetTextValueAsync(cut, "Name", "newcoo");
        await ClickButtonAsync(cut, "Save");

        Assert.Contains("newcoo", cut.Markup, StringComparison.Ordinal);
        Assert.Null(factory.Services.GetRequiredService<PersonaStore>().Get("coo"));
        Assert.NotNull(factory.Services.GetRequiredService<PersonaStore>().Get("newcoo"));

        // A rename moves the whole Teammate folder and renames the definition file. Completion is
        // signalled by TeammateFolderMoves.WhenSettledAsync; the card re-renders on PersonasChanged.
        TeammateFolderMoves folderMoves = factory.Services.GetRequiredService<TeammateFolderMoves>();
        await folderMoves.WhenSettledAsync("newcoo", Xunit.TestContext.Current.CancellationToken);

        string expectedPath = Path.Combine(factory.TeammatesDirPath, "newcoo", "newcoo.md");
        Assert.True(File.Exists(expectedPath));

        string oldFolderPath = Path.Combine(factory.TeammatesDirPath, "coo");
        Assert.False(Directory.Exists(oldFolderPath));

        cut.WaitForAssertion(() => Assert.Equal(expectedPath, cut.Find(".teammate-card-path").TextContent.Trim()));
    }

    [Fact]
    public async Task CreateMode_OffersNameTitleAliasTeamsAndText()
    {
        await using var factory = new TeamWebApplicationFactory();
        await using var ctx = NewContext(factory);

        var cut = RenderPage(ctx, factory);
        await ClickButtonAsync(cut, "New teammate");

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
        await ClickButtonAsync(cut, "New teammate");

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
        await ClickButtonAsync(cut, "New teammate");

        await SetTextValueAsync(cut, "Name", "bad/name");
        await SetTextValueAsync(cut, "Title", "Title");
        await SetTextValueAsync(cut, "Alias", "alias");
        await SetTextValueAsync(cut, "Persona body", "a draft worth keeping");
        await ClickButtonAsync(cut, "Add teammate");

        Assert.Contains("is not a valid Persona name.", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("a draft worth keeping", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>The inline confirm on Remove: clicking the icon-only Remove button (found by its aria-label - see <see cref="HasButton"/>) swaps it for text Confirm/Cancel buttons in place, never a nested dialog.</summary>
    [Fact]
    public async Task ConfirmingRemove_ReplacesRemoveWithAConfirmStep()
    {
        await using var factory = new TeamWebApplicationFactory();
        await SeedPersonaAsync(factory, "coo", "x");
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");
        await ClickButtonAsync(cut, "Remove");

        Assert.True(HasButton(cut, "Confirm"));
        Assert.False(HasButton(cut, "Remove"));
    }

    /// <summary>Confirming Remove actually removes the Persona and closes the dialog. Remove itself is found by aria-label, since it is now an icon-only button - see <see cref="HasButton"/>.</summary>
    [Fact]
    public async Task ConfirmRemove_RemovesThePersonaAndClosesTheDialog()
    {
        await using var factory = new TeamWebApplicationFactory();
        await SeedPersonaAsync(factory, "coo", "x");
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");
        await ClickButtonAsync(cut, "Remove");
        await ClickButtonAsync(cut, "Confirm");

        Assert.Null(factory.Services.GetRequiredService<PersonaStore>().Get("coo"));
        Assert.Empty(cut.FindAll(".mud-dialog-container"));
    }

    /// <summary>
    /// Spec §6.12: the Chief of Staff's card offers "Reset to default" in place of Remove, since it
    /// cannot be removed the way an ordinary Teammate can. Reuses the real seeded default -
    /// <see cref="TeamWebApplicationFactory"/> now runs <see cref="BuiltinTeammateSeeder"/> like any
    /// other host (D15) - rather than hand-writing a marker-carrying Persona file.
    /// </summary>
    [Fact]
    public async Task BuiltinTeammate_ShowsResetNotRemove()
    {
        await using var factory = new TeamWebApplicationFactory();
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "Chief of Staff");

        Assert.True(HasButton(cut, "Reset to default"));
        Assert.False(HasButton(cut, "Remove"));
    }

    /// <summary>
    /// Spec §6.12's Reset: renaming the seeded Chief of Staff to Alfred, editing its Title and Body
    /// away from the shipped default, and storing a Model and an Effort, all survive right up until
    /// Reset is confirmed - at which point the Title, Body, Teams, Skills, Model and Effort are all
    /// back to default, the <c>_builtin</c> marker is still present, and only the Name (Alfred) and
    /// Alias are kept, exactly as the confirm copy promises.
    /// </summary>
    [Fact]
    public async Task Reset_RestoresDefaultsKeepsNameAndAlias()
    {
        await using var factory = new TeamWebApplicationFactory();
        var personas = factory.Services.GetRequiredService<PersonaStore>();
        var seeded = personas.Entries.Single(
            entry => string.Equals(entry.Builtin, BuiltinTeammate.ChiefOfStaffMarker, StringComparison.Ordinal));

        // Rename to Alfred, edit Title and Body away from the shipped default, and store a Model
        // and an Effort - every field Reset promises to restore - while keeping the marker, so this
        // remains the same edited Chief of Staff rather than becoming an ordinary Persona.
        var editedText = PersonaFrontmatter.Compose(
            new PersonaIdentity(
                "Alfred", "Edited Title", seeded.Alias, Teams: [], Skills: ["team-building"], Builtin: BuiltinTeammate.ChiefOfStaffMarker),
            "This Body has been hand-edited and no longer matches the shipped default.");
        personas.Update(seeded.Name, editedText, model: "claude-opus-4", effort: "high", workMode: null);

        await using var ctx = NewContext(factory);
        var cut = await OpenViewCardAsync(ctx, factory, "Alfred");

        await ClickButtonAsync(cut, "Reset to default");

        Assert.Contains(
            "Restore the Chief of Staff's instructions, Title, Teams, Skills, Model and Effort to "
            + "their defaults? Its Name, Alias, Rooms and history are kept.",
            cut.Markup,
            StringComparison.Ordinal);

        await ClickButtonAsync(cut, "Confirm");

        var reset = personas.Get("Alfred");
        Assert.NotNull(reset);
        Assert.Null(reset.Model);
        Assert.Null(reset.Effort);

        var parsed = PersonaFrontmatter.TryReadIdentity(reset.Text, out PersonaIdentity? identity, out var error);
        Assert.True(parsed, error);
        Assert.NotNull(identity);
        Assert.Equal("Alfred", identity.Name);
        Assert.Equal(seeded.Alias, identity.Alias);
        Assert.Equal("Chief of Staff", identity.Title);
        Assert.Empty(identity.Teams);
        Assert.Equal(["team-building"], identity.Skills);
        Assert.Equal(BuiltinTeammate.ChiefOfStaffMarker, identity.Builtin);

        var (_, defaultBody) = PersonaFrontmatter.Parse(BuiltinTeammate.DefaultText);
        var (_, resetBody) = PersonaFrontmatter.Parse(reset.Text);
        Assert.Equal(defaultBody, resetBody);
    }

    /// <summary>An ordinary, non-built-in Teammate keeps Remove - only the Chief of Staff's card swaps it for Reset (see <see cref="BuiltinTeammate_ShowsResetNotRemove"/>).</summary>
    [Fact]
    public async Task NonBuiltin_StillShowsRemove()
    {
        await using var factory = new TeamWebApplicationFactory();
        await SeedPersonaAsync(factory, "coo", "x");
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");

        Assert.True(HasButton(cut, "Remove"));
        Assert.False(HasButton(cut, "Reset to default"));
    }

    /// <summary>Clicking Close asks the dialog to close - the <c>MudIconButton</c> replacement for the old <c>&amp;times;</c> button.</summary>
    [Fact]
    public async Task CloseButton_ClosesTheDialog()
    {
        await using var factory = new TeamWebApplicationFactory();
        await SeedPersonaAsync(factory, "coo", "x");
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");
        await ClickAsync(cut, () => cut.Find("button[aria-label='Close']"));

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
        await ClickButtonAsync(cut, "Open");

        Assert.Contains("Persona 'coo' does not exist.", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// Spec §6.6 / §4 (P6) / §12 (E-2): a dropdown offering a single option is a control nobody can
    /// use, so the Adapter select must not render at all when the catalog holds exactly one profile -
    /// the shape a stock installation (which configures no Adapters) always produces, and the exact
    /// case this test pins so a stock install keeps looking byte-for-byte like it did before this
    /// feature existed.
    /// </summary>
    [Fact]
    public async Task CreateMode_OneAdapterConfigured_DoesNotRenderTheAdapterSelect()
    {
        await using var factory = new TeamWebApplicationFactory();
        await using var ctx = NewContext(factory);
        ctx.Services.AddSingleton(BuildAdapterCatalog(("claude", "Claude")));

        var cut = RenderPage(ctx, factory);
        await ClickButtonAsync(cut, "New teammate");

        Assert.Equal(0, CountControlsLabelled(cut, "Adapter"));
    }

    /// <summary>Spec §6.6: two configured Adapters render exactly one Adapter select, above the Model select.</summary>
    [Fact]
    public async Task CreateMode_TwoAdaptersConfigured_RendersExactlyOneAdapterSelect()
    {
        await using var factory = new TeamWebApplicationFactory();
        await using var ctx = NewContext(factory);
        ctx.Services.AddSingleton(BuildAdapterCatalog(("claude", "Claude"), ("agency", "Agency")));

        var cut = RenderPage(ctx, factory);
        await ClickButtonAsync(cut, "New teammate");

        Assert.Equal(1, CountControlsLabelled(cut, "Adapter"));
    }

    /// <summary>
    /// Spec §6.7 (card row) and Spec §2 U7: picking "team-building" in the Skills select rewrites
    /// the frontmatter inside the raw text field through <see cref="PersonaFrontmatter.WriteListField"/>
    /// - the list-field counterpart of the <see cref="PersonaFrontmatter.WriteScalarField"/> mechanism
    /// the Adapter select above uses - so Save persists it the same way every other identity edit does.
    /// </summary>
    [Fact]
    public async Task SkillsSelect_PickTeamBuilding_TextGainsSkillsField()
    {
        await using var factory = new TeamWebApplicationFactory();
        await SeedPersonaAsync(factory, "coo", "You are the Chief of Staff.");
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");
        await ClickButtonAsync(cut, "Edit");

        await OpenSelectAsync(cut, "Skills");
        await ClickSkillOptionAsync(cut, "team-building");

        Assert.Contains("skills: ['team-building']", cut.Find("textarea").TextContent, StringComparison.Ordinal);
    }

    /// <summary>
    /// Spec §6.7: the Skills select's helper text warns that a Skill, like a Model, an Effort and an
    /// Adapter, is fixed for the life of a session - changing it is a restart, not a live edit.
    /// </summary>
    [Fact]
    public async Task SkillsSelect_HelperText_SaysChangingRestarts()
    {
        await using var factory = new TeamWebApplicationFactory();
        await SeedPersonaAsync(factory, "coo", "x");
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");
        await ClickButtonAsync(cut, "Edit");

        Assert.Contains("Changing Skills restarts this Teammate.", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// Spec §6.7: an assigned Skill the store no longer resolves - here, a frontmatter <c>skills</c>
    /// entry with no matching <see cref="Skill"/> in <see cref="SkillStore.All"/> - is listed with a
    /// warning chip rather than silently vanishing from the card, so the Human can see it and remove
    /// it (this is also the U14 Degraded case, seen here from the card rather than the status badge).
    /// </summary>
    [Fact]
    public async Task SkillsSelect_AssignedSkillMissing_ShowsWarningChip()
    {
        await using var factory = new TeamWebApplicationFactory();
        await File.WriteAllTextAsync(
            EnsureTeamsDir(factory),
            "---\nName: coo\nTitle: coo\nAlias: coo\nskills: ['nonexistent']\n---\nx",
            Xunit.TestContext.Current.CancellationToken);
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");
        await ClickButtonAsync(cut, "Edit");

        var warningChips = cut.FindAll(".mud-chip-color-warning");
        Assert.Contains(warningChips, chip => chip.TextContent.Contains("nonexistent", StringComparison.Ordinal));
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
    /// nothing for it to fix - but a NextSession Prompt edit can only reach a Teammate through a new
    /// session, and rules.md forbids a Prompt edit restarting one by itself, so hiding Restart here left
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

        // Scoped to the open dialog itself, not the whole page: since D15, the page also carries a
        // tile for the real, always-Offline "Chief of Staff" Agency.Huddle.App.Teammates.BuiltinTeammateSeeder
        // seeds by default (Spec §6.12), and that tile's own "Offline" status text would otherwise
        // make this assertion fail for a reason that has nothing to do with "coo"'s card.
        var dialogMarkup = cut.Find(".mud-dialog-container").OuterHtml;
        Assert.Contains("Starting", dialogMarkup, StringComparison.Ordinal);
        Assert.DoesNotContain("Offline", dialogMarkup, StringComparison.Ordinal);
        Assert.False(HasButton(cut, "Restart"));
    }
}
