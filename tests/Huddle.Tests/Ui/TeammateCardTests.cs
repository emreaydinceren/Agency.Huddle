using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Avatars;
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

        // Remove is an icon-only MudIconButton (a destructive action gets an icon; its Confirm/Cancel
        // step below stays text, per the file's own comment), so this is matched by aria-label rather
        // than text content - see HasButton's remarks.
        Assert.True(HasButton(cut, "Remove"));

        // Viewing is not editing: the Persona text is shown, not offered for typing into.
        Assert.Empty(cut.FindAll("textarea"));
    }

    /// <summary>View mode is the one place Title, Alias and Teams are shown alongside the Name - Edit's top identity area stays as before (see <see cref="EditMode_TopIdentityAreaStillOmitsTitleAliasAndTeamsAsReadOnlyText"/>).</summary>
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
        FindButton(cut, "Edit").Click();

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
        FindButton(cut, "Edit").Click();

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
        FindButton(cut, "Edit").Click();

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
        FindButton(cut, "Edit").Click();

        SetTextValue(cut, "Title", "Head of Everything");

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
        FindButton(cut, "Edit").Click();

        SetTextValue(cut, "Persona text", "---\nName: coo\nTitle: Reseeded Title\nAlias: newalias\n---\nYou are the Chief of Staff.");

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
        FindButton(cut, "Edit").Click();

        SetTextValue(cut, "Title", "New Title");

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
        FindButton(cut, "Edit").Click();

        Assert.DoesNotContain("keep the old name", cut.Markup, StringComparison.Ordinal);

        SetTextValue(cut, "Name", "newcoo");

        Assert.Contains("keep the old name", cut.Markup, StringComparison.Ordinal);

        // The note must not claim the old, pre-ADR-0011 behaviour. A rename now carries the Agent,
        // its Rooms and their Transcripts with it, so this wording would be a lie rather than a
        // warning - and it is the kind of lie only a string assertion catches.
        Assert.DoesNotContain("stay behind under the old name", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// Step 4's fix: after renaming through the Name box and Saving, the card lands on the NEW
    /// Persona - the displayed Name and file path both follow the rename - rather than looking up the
    /// stale OLD name <see cref="PersonaStore.Update"/> was originally called with, which no longer
    /// exists once the rename has taken effect.
    /// </summary>
    [Fact]
    public async Task SaveAsync_AfterARename_LandsOnTheNewPersona()
    {
        await using var factory = new TeamWebApplicationFactory();
        await SeedPersonaAsync(factory, "coo", "You are the Chief of Staff.");
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");
        FindButton(cut, "Edit").Click();

        SetTextValue(cut, "Name", "newcoo");
        FindButton(cut, "Save").Click();

        Assert.Contains("newcoo", cut.Markup, StringComparison.Ordinal);
        Assert.Null(factory.Services.GetRequiredService<PersonaStore>().Get("coo"));
        Assert.NotNull(factory.Services.GetRequiredService<PersonaStore>().Get("newcoo"));

        // PersonaStore.Update rewrites the SAME file in place - identity is frontmatter, never the
        // filename (rules.md) - so the file path a rename lands on is still the original "coo.md".
        var expectedPath = Path.Combine(factory.TeamsDirPath, "coo.md");
        Assert.True(File.Exists(expectedPath));
        Assert.Contains(expectedPath, cut.Markup, StringComparison.Ordinal);
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

    /// <summary>The inline confirm on Remove: clicking the icon-only Remove button (found by its aria-label - see <see cref="HasButton"/>) swaps it for text Confirm/Cancel buttons in place, never a nested dialog.</summary>
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

    /// <summary>Confirming Remove actually removes the Persona and closes the dialog. Remove itself is found by aria-label, since it is now an icon-only button - see <see cref="HasButton"/>.</summary>
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
        FindButton(cut, "New teammate").Click();

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
        FindButton(cut, "New teammate").Click();

        Assert.Equal(1, CountControlsLabelled(cut, "Adapter"));
    }

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
        FindButton(cut, "Edit").Click();

        await OpenSelectAsync(cut, "Adapter");
        (await FindSelectItemAsync(cut, "Agency")).Click();

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
        FindButton(cut, "Edit").Click();

        TaskCompletionSource gateA = new(TaskCreationOptions.RunContinuationsAsynchronously);
        factory.FakeModelCatalog.ModelsGate = gateA;

        await OpenSelectAsync(cut, "Adapter");
        (await FindSelectItemAsync(cut, "Agency")).Click();

        // The Agency probe is now stuck on gateA. Clearing the gate before switching again lets the
        // SECOND probe (Mock) answer immediately - the ordering this test exists to prove wrong.
        factory.FakeModelCatalog.ModelsGate = null;

        await OpenSelectAsync(cut, "Adapter");
        (await FindSelectItemAsync(cut, "Mock")).Click();

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
        FindButton(cut, "Edit").Click();

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
        FindButton(cut, "Edit").Click();

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
        FindButton(cut, "Edit").Click();

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
        FindButton(cut, "Edit").Click();

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
        FindButton(cut, "Edit").Click();

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
        FindButton(cut, "Edit").Click();

        await OpenSelectAsync(cut, "Model");
        (await FindSelectItemAsync(cut, "Sonnet")).Click();

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
        FindButton(cut, "Edit").Click();

        await OpenSelectAsync(cut, "Model");
        (await FindSelectItemAsync(cut, "Sonnet")).Click();

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
        FindButton(cut, "Edit").Click();

        await OpenSelectAsync(cut, "Model");
        (await FindSelectItemAsync(cut, "Sonnet")).Click();
        Assert.Contains(EffortResetNotice, cut.Markup, StringComparison.Ordinal);

        await OpenSelectAsync(cut, "Effort");
        (await FindSelectItemAsync(cut, "Medium")).Click();

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
        FindButton(cut, "Edit").Click();

        await OpenSelectAsync(cut, "Model");
        (await FindSelectItemAsync(cut, "Sonnet")).Click();
        Assert.Contains(EffortResetNotice, cut.Markup, StringComparison.Ordinal);

        await OpenSelectAsync(cut, "Model");
        (await FindSelectItemAsync(cut, "Haiku")).Click();

        Assert.DoesNotContain(EffortResetNotice, cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// A minimal but genuine PNG - the 8-byte signature plus a 13-byte IHDR chunk - the same bytes
    /// <see cref="AvatarEndpointTests"/> uses, so an upload test is honest about what a real PNG
    /// header looks like rather than an arbitrary byte string that happens to start right.
    /// </summary>
    private static readonly byte[] MinimalPng =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
        0x08, 0x00, 0x00, 0x00, 0x00, 0x3A, 0x7E, 0x9B,
        0x55,
    ];

    /// <summary>Create mode offers the three-way avatar choice - initials, a short label, or an uploaded image - alongside the rest of the Create form.</summary>
    [Fact]
    public async Task CreateMode_ShowsTheThreeWayAvatarChoice()
    {
        await using var factory = new TeamWebApplicationFactory();
        await using var ctx = NewContext(factory);

        var cut = RenderPage(ctx, factory);
        FindButton(cut, "New teammate").Click();

        Assert.Contains("Initials of the name", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("A short label", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("An image", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>View mode renders the avatar itself but none of its editing controls - the avatar counterpart of Model and Effort rendering as plain text there, never their selects.</summary>
    [Fact]
    public async Task ViewMode_ShowsNoAvatarEditingControls()
    {
        await using var factory = new TeamWebApplicationFactory();
        await SeedPersonaAsync(factory, "coo", "x");
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");

        Assert.DoesNotContain("Initials of the name", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("A short label", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Choose an image", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// The frozen-parameter proof, and the most valuable test in this file: MudBlazor freezes a
    /// dialog's own <c>[Parameter]</c>s at the moment it opens (see the file-level comment on
    /// <c>TeammateCard.razor</c>), so choosing "A short label" and typing can only reach the preview
    /// <c>TeammateAvatar</c> renders because that preview is an ordinary CHILD render reading a LOCAL
    /// field (<c>CurrentAvatar</c>), never a re-pushed parameter, inside this SAME already-open dialog.
    /// </summary>
    [Fact]
    public async Task EditMode_ChoosingLabelAndTyping_UpdatesThePreviewAvatarInsideTheSameOpenDialog()
    {
        await using var factory = new TeamWebApplicationFactory();
        await SeedPersonaAsync(factory, "coo", "x");
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");
        FindButton(cut, "Edit").Click();

        SelectAvatarChoice(cut, "A short label");
        SetImmediateTextValue(cut, "Label", "AB");

        Assert.Equal("AB", cut.Find(".mud-avatar").TextContent.Trim());
    }

    /// <summary>Choosing a file but Cancelling leaves <c>{DataDir}/avatars</c> untouched - the buffer-until-save proof: nothing reaches disk before Save (Part C's own contract).</summary>
    [Fact]
    public async Task CreateMode_ChooseAFileThenCancel_LeavesTheAvatarsDirectoryEmpty()
    {
        await using var factory = new TeamWebApplicationFactory();
        await using var ctx = NewContext(factory);

        var cut = RenderPage(ctx, factory);
        FindButton(cut, "New teammate").Click();
        SelectAvatarChoice(cut, "An image");

        var inputFile = cut.FindComponent<InputFile>();
        inputFile.UploadFiles(InputFileContent.CreateFromBinary(MinimalPng, "avatar.png", contentType: "image/png"));

        FindButton(cut, "Cancel").Click();

        // Program.cs creates {DataDir}/avatars unconditionally at startup so the static-file
        // middleware always has a directory to point at (see AvatarEndpointTests) - so its mere
        // existence proves nothing here. Emptiness is the actual proof: nothing was ever WRITTEN
        // into it, because Cancel never reached SaveAsync, the only place that calls WriteImage.
        var avatarsDir = Path.Combine(DataDirOf(factory), "avatars");
        Assert.Empty(Directory.EnumerateFileSystemEntries(avatarsDir));
    }

    /// <summary>An oversized upload sets <c>avatarError</c> and keeps the card open, rather than throwing or silently accepting it.</summary>
    [Fact]
    public async Task CreateMode_OversizedImage_SetsAnErrorAndKeepsTheCardOpen()
    {
        await using var factory = new TeamWebApplicationFactory();
        await using var ctx = NewContext(factory);

        var cut = RenderPage(ctx, factory);
        FindButton(cut, "New teammate").Click();
        SelectAvatarChoice(cut, "An image");

        var oversized = new byte[AvatarImage.MaxBytes + 1];
        var inputFile = cut.FindComponent<InputFile>();
        inputFile.UploadFiles(InputFileContent.CreateFromBinary(oversized, "big.png", contentType: "image/png"));

        Assert.Contains("larger than", cut.Markup, StringComparison.Ordinal);
        Assert.NotEmpty(cut.FindAll(".mud-dialog-container"));
    }

    /// <summary>An upload whose bytes are not a PNG, JPEG or WebP signature sets <c>avatarError</c> with the exact wording <c>AvatarImage.SniffExtension</c>'s caller promises.</summary>
    [Fact]
    public async Task CreateMode_UploadedFileIsNotARecognisedImage_SetsAnError()
    {
        await using var factory = new TeamWebApplicationFactory();
        await using var ctx = NewContext(factory);

        var cut = RenderPage(ctx, factory);
        FindButton(cut, "New teammate").Click();
        SelectAvatarChoice(cut, "An image");

        var inputFile = cut.FindComponent<InputFile>();
        inputFile.UploadFiles(InputFileContent.CreateFromBinary([1, 2, 3, 4], "fake.png", contentType: "image/png"));

        Assert.Contains("That file is not a PNG, JPEG or WebP image.", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// The isolated <see cref="TeamOptions.DataDir"/> <paramref name="factory"/> composed its app with -
    /// resolved from the running host, the same way <c>AvatarEndpointTests.DataDirOf</c> does, since
    /// <see cref="TeamWebApplicationFactory"/> exposes its temp directory only indirectly.
    /// </summary>
    /// <param name="factory">The factory whose composed <see cref="TeamOptions"/> to read.</param>
    private static string DataDirOf(TeamWebApplicationFactory factory)
    {
        return factory.Services.GetRequiredService<IOptions<TeamOptions>>().Value.DataDir;
    }

    /// <summary>
    /// Clicks the <c>MudRadio</c> whose own text is <paramref name="choiceLabel"/>, inside the avatar
    /// editor's <c>MudRadioGroup</c>. Clicks the inner <c>input.mud-radio-input</c>, not the outer
    /// <c>label.mud-radio</c> - verified against the rendered markup rather than assumed: the outer
    /// label carries only <c>@onkeydown</c> and <c>@onclick:stoppropagation</c>, and MudRadio binds its
    /// actual <c>@onclick</c> to the native radio input itself.
    /// </summary>
    /// <param name="cut">The rendered page holding the card.</param>
    /// <param name="choiceLabel">The radio's visible text - "Initials of the name", "A short label" or "An image".</param>
    private static void SelectAvatarChoice(IRenderedComponent<ContainerFragment> cut, string choiceLabel)
    {
        var radio = cut.FindAll(".mud-radio").First(element => element.TextContent.Contains(choiceLabel, StringComparison.Ordinal));
        (radio.QuerySelector("input.mud-radio-input") ?? throw new InvalidOperationException($"No radio input under '{choiceLabel}'.")).Click();
    }

    /// <summary>
    /// The <c>Immediate="true"</c> counterpart of <see cref="SetTextValue"/>: raises <c>@oninput</c>
    /// rather than <c>@onchange</c>, matching the Label box's own binding (see the markup comment on
    /// why it is <c>Immediate</c>, unlike every blur-only box in this file).
    /// </summary>
    /// <param name="cut">The rendered page holding the card.</param>
    /// <param name="label">The control's label text.</param>
    /// <param name="value">The text to type.</param>
    private static void SetImmediateTextValue(IRenderedComponent<ContainerFragment> cut, string label, string value)
    {
        var control = FindInputControl(cut, label);
        var input = control.QuerySelector("input") ?? throw new InvalidOperationException($"No input under '{label}'.");
        input.Input(value);
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
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<AdapterCatalog>());
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<AvatarStore>());
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

    /// <summary>
    /// The <see cref="SeedPersonaWithModelAndEffort"/> sibling that also seeds an Adapter through
    /// <see cref="PersonaIdentity.Adapter"/> — the only way to seed one, since it travels with the
    /// frontmatter rather than through a separate store (Spec §7.1, §7.2).
    /// </summary>
    /// <param name="factory">The factory whose <see cref="PersonaStore"/> to add the Persona through.</param>
    /// <param name="name">The Persona's Name, Title and Alias alike.</param>
    /// <param name="body">The Persona's system-prompt body.</param>
    /// <param name="model">The Model to store, or <see langword="null"/> for the agent's default.</param>
    /// <param name="effort">The Effort to store, or <see langword="null"/> for the model's default.</param>
    /// <param name="adapter">The Adapter id to store, or <see langword="null"/> for the installation's default.</param>
    private static void SeedPersonaWithModelEffortAndAdapter(TeamWebApplicationFactory factory, string name, string body, string? model, string? effort, string? adapter)
    {
        factory.Services.GetRequiredService<PersonaStore>().Add(new PersonaIdentity(name, name, name, [], adapter), body, model, effort);
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

    /// <summary>
    /// Whether any rendered button's trimmed text, OR its <c>aria-label</c>, equals <paramref name="text"/>.
    /// The Remove button became an icon-only <c>MudIconButton</c> (see <see cref="ViewMode_ShowsDetailsAndActions"/>
    /// and its neighbours) and carries no text content at all - <c>aria-label</c> is the reliable, accessible
    /// handle for it, and this helper is deliberately not scoped to just that one button so every other
    /// text-button caller keeps working unchanged.
    /// </summary>
    private static bool HasButton(IRenderedComponent<ContainerFragment> cut, string text) =>
        cut.FindAll("button").Any(button => MatchesButtonLabel(button, text));

    /// <summary>The first rendered button whose trimmed text, OR its <c>aria-label</c>, equals <paramref name="text"/> - see <see cref="HasButton"/>.</summary>
    private static IElement FindButton(IRenderedComponent<ContainerFragment> cut, string text) =>
        cut.FindAll("button").First(button => MatchesButtonLabel(button, text));

    private static bool MatchesButtonLabel(IElement button, string text) =>
        string.Equals(button.TextContent.Trim(), text, StringComparison.Ordinal)
        || string.Equals(button.GetAttribute("aria-label"), text, StringComparison.Ordinal);

    private static IElement FindInputControl(IRenderedComponent<ContainerFragment> cut, string label) =>
        cut.FindAll("div.mud-input-control")
            .First(control => control.QuerySelectorAll("label").Any(l => l.TextContent.Contains(label, StringComparison.Ordinal)));

    /// <summary>
    /// The number of rendered <c>.mud-input-control</c>s whose own label contains <paramref name="label"/> -
    /// used to prove the Adapter select is absent (0) or renders exactly once (1), since a hidden
    /// <c>@if</c> block leaves no element at all for <see cref="FindInputControl"/>'s single-match
    /// lookup to find, and <c>Assert.DoesNotContain</c> on markup text would be fooled by "Adapter"
    /// appearing in this file's own doc comments once compiled into nothing of the sort - counting
    /// actual rendered controls is the only honest oracle for absence.
    /// </summary>
    private static int CountControlsLabelled(IRenderedComponent<ContainerFragment> cut, string label) =>
        cut.FindAll("div.mud-input-control").Count(control => control.QuerySelectorAll("label").Any(l => l.TextContent.Contains(label, StringComparison.Ordinal)));

    /// <summary>
    /// Builds a real <see cref="AdapterCatalog"/> over in-memory <see cref="TeamOptions"/> - a pure
    /// configuration object, not a fake (Spec §6.6) - holding one <see cref="AdapterProfileOptions"/>
    /// entry per <paramref name="profiles"/> pair, in order.
    /// </summary>
    /// <param name="profiles">Each configured Adapter's (Id, DisplayName) pair.</param>
    private static AdapterCatalog BuildAdapterCatalog(params (string Id, string DisplayName)[] profiles)
    {
        var acp = new AcpOptions
        {
            Adapters = profiles
                .Select(profile => new AdapterProfileOptions { Id = profile.Id, DisplayName = profile.DisplayName, Command = "node" })
                .ToList(),
        };

        return new AdapterCatalog(Options.Create(new TeamOptions { Acp = acp }));
    }

    /// <summary>The <c>&lt;input&gt;</c> under a <c>MudSelect</c> labelled <paramref name="label"/> - its closed-state value is the currently selected item's own display text, which is how a "preselected" assertion can be made without opening the popover at all.</summary>
    private static IElement FindSelectInput(IRenderedComponent<ContainerFragment> cut, string label) =>
        FindInputControl(cut, label).QuerySelector("input") ?? throw new InvalidOperationException($"No input under the '{label}' select.");

    private static async Task OpenSelectAsync(IRenderedComponent<ContainerFragment> cut, string label)
    {
        await FindInputControl(cut, label).MouseDownAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
    }

    /// <summary>
    /// Whether the <c>MudSelect</c> labelled <paramref name="label"/> is refusing interaction, which is
    /// how the card says "this list is still being probed" (issue #39). Asserted off the rendered
    /// <c>disabled</c> attribute, not the component's parameter: what matters is what the control does,
    /// not what it was passed. Note <c>readonly</c> would be useless here - MudSelect puts it on the
    /// input unconditionally, because a select's text box is never typeable.
    /// </summary>
    /// <param name="cut">The rendered page holding the card.</param>
    /// <param name="label">The select's label text.</param>
    /// <returns><see langword="true"/> when the select's input is disabled.</returns>
    private static bool IsSelectInert(IRenderedComponent<ContainerFragment> cut, string label) =>
        FindSelectInput(cut, label).HasAttribute("disabled");

    /// <summary>
    /// Clicks the <c>MudSelect</c> labelled <paramref name="label"/> and returns the options that
    /// actually appeared. Empty means the popover refused to open - the behavioural half of
    /// <see cref="IsSelectInert"/>, and the one a user would notice. <c>MudSelect</c> renders no
    /// <c>&lt;option&gt;</c> elements, so <c>.mud-list-item</c> is the only honest oracle here.
    /// </summary>
    /// <param name="cut">The rendered page holding the card.</param>
    /// <param name="label">The select's label text.</param>
    /// <returns>The rendered option elements, empty when the select did not open.</returns>
    private static async Task<IReadOnlyList<IElement>> OpenSelectAndListOptionsAsync(IRenderedComponent<ContainerFragment> cut, string label)
    {
        await OpenSelectAsync(cut, label);
        await Task.Yield();
        return cut.FindAll("div.mud-list-item");
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
