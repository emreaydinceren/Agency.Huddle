using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.App.Teammates;
using static Agency.Huddle.Tests.Ui.TeammateCardTestSupport;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Pins U26's shared structure on the Teammate card (rows UX-022, UX-023, UX-080, UX-R13, UX-R18, UX-V04): the
/// dialog options are <c>HuddleDialogOptions.SmallForm</c> (MudBlazor's own close button, no backdrop dismissal),
/// a locked field is <c>Disabled</c> and never <c>ReadOnly</c>, Reset and Remove confirm through
/// <see cref="InlineConfirm"/> with the verb on the button and a title ending in "?", and the avatar is edited
/// in a Buffered <see cref="AvatarEditor"/>.
/// </summary>
public sealed class TeammateCardSharedStructureTests
{
    private const string LockedHelper = "This is written as a multi-line value in the Persona text below and can only be edited there.";

    /// <summary>UX-022 and UX-023: the card opens with the shared SmallForm options, so it has a close button and a backdrop click does not drop typed text.</summary>
    [Fact]
    public void Options_AreTheSharedSmallFormOptions()
    {
        Assert.Same(Agency.Huddle.App.Components.Shared.Dialogs.HuddleDialogOptions.SmallForm, TeammateCard.Options);
        Assert.True(TeammateCard.Options.CloseButton);
        Assert.False(TeammateCard.Options.BackdropClick);
    }

    /// <summary>UX-022: the title holds no hand-built close button with a native <c>title="Close"</c>; the dialog's own close button is the only one.</summary>
    [Fact]
    public async Task ViewCard_HasNoHandBuiltCloseButton()
    {
        await using var factory = new TeamWebApplicationFactory();
        await SeedPersonaAsync(factory, "coo", "x");
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");

        Assert.Empty(cut.FindAll("button[title='Close']"));
        Assert.Single(cut.FindAll(".mud-dialog-title button"));
    }

    /// <summary>UX-V04: a field whose value is a block scalar is locked as <c>disabled</c>, not <c>readonly</c>, and its helper line still says why.</summary>
    /// <param name="field">The frontmatter key and the box's label, lower and upper case.</param>
    /// <param name="label">The box's label.</param>
    [Theory]
    [InlineData("title", "Title")]
    [InlineData("alias", "Alias")]
    public async Task EditMode_LockedField_IsDisabledNotReadOnly(string field, string label)
    {
        await using var factory = new TeamWebApplicationFactory();
        string title = field == "title" ? "title: >\n  Legendary Assistant\n  and Chief of Staff" : "title: Assistant";
        string alias = field == "alias" ? "alias: >\n  coo\n  boss" : "alias: coo";
        await File.WriteAllTextAsync(
            EnsureTeamsDir(factory),
            $"---\nname: coo\n{title}\n{alias}\n---\nx",
            Xunit.TestContext.Current.CancellationToken);
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");
        await ClickButtonAsync(cut, "Edit");
        await SetTextValueAsync(cut, label, "Changed");

        IElement input = FindInputControl(cut, label).QuerySelector("input") ?? throw new InvalidOperationException($"No {label} input.");
        Assert.True(input.HasAttribute("disabled"));
        Assert.False(input.HasAttribute("readonly"));
        Assert.Equal(LockedHelper, FindInputControl(cut, label).QuerySelector(".mud-input-helper-text")?.TextContent.Trim());
    }

    /// <summary>UX-R13 and UX-080: Remove asks through an <see cref="InlineConfirm"/> titled "Remove coo?" whose confirm button says "Remove", with a Cancel.</summary>
    [Fact]
    public async Task Remove_AsksThroughInlineConfirmWithItsVerb()
    {
        await using var factory = new TeamWebApplicationFactory();
        await SeedPersonaAsync(factory, "coo", "x");
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");
        await ClickButtonAsync(cut, "Remove");

        IElement group = Assert.Single(cut.FindAll(".inline-confirm"));
        Assert.Equal("Remove coo?", group.GetAttribute("aria-label"));
        Assert.Equal("Remove", cut.Find(".inline-confirm-confirm").TextContent.Trim());
        Assert.Equal("Cancel", cut.Find(".inline-confirm-cancel").TextContent.Trim());
        Assert.False(HasButton(cut, "Confirm"));
    }

    /// <summary>UX-R13: confirming Remove through the inline confirm removes the Persona and closes the dialog.</summary>
    [Fact]
    public async Task Remove_ConfirmButton_RemovesThePersona()
    {
        await using var factory = new TeamWebApplicationFactory();
        await SeedPersonaAsync(factory, "coo", "x");
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");
        await ClickButtonAsync(cut, "Remove");
        await ClickAsync(cut, () => cut.Find(".inline-confirm-confirm"));

        Assert.Null(factory.Services.GetRequiredService<PersonaStore>().Get("coo"));
        Assert.Empty(cut.FindAll(".mud-dialog-container"));
    }

    /// <summary>UX-R13: Cancel on the Remove confirm puts the Remove button back and keeps the Persona.</summary>
    [Fact]
    public async Task Remove_Cancel_RestoresTheRemoveButton()
    {
        await using var factory = new TeamWebApplicationFactory();
        await SeedPersonaAsync(factory, "coo", "x");
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");
        await ClickButtonAsync(cut, "Remove");
        await ClickAsync(cut, () => cut.Find(".inline-confirm-cancel"));

        Assert.Empty(cut.FindAll(".inline-confirm"));
        Assert.True(HasButton(cut, "Remove"));
        Assert.NotNull(factory.Services.GetRequiredService<PersonaStore>().Get("coo"));
    }

    /// <summary>Escape inside the Remove confirm cancels only the confirm: the Teammate card stays open and the Persona is kept.</summary>
    [Fact]
    public async Task Remove_Escape_CancelsTheConfirmButNotTheCard()
    {
        await using var factory = new TeamWebApplicationFactory();
        await SeedPersonaAsync(factory, "coo", "x");
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");
        await ClickButtonAsync(cut, "Remove");
        await cut.InvokeAsync(() => cut.Find(".inline-confirm").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" }));

        Assert.Empty(cut.FindAll(".inline-confirm"));
        Assert.Single(cut.FindAll(".mud-dialog-container"));
        Assert.NotNull(factory.Services.GetRequiredService<PersonaStore>().Get("coo"));
    }

    /// <summary>Cancelling the Remove confirm hands focus back to the Remove button, so a keyboard user does not lose their place.</summary>
    [Fact]
    public async Task Remove_Cancel_ReturnsFocusToTheRemoveButton()
    {
        await using var factory = new TeamWebApplicationFactory();
        await SeedPersonaAsync(factory, "coo", "x");
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");
        await ClickButtonAsync(cut, "Remove");
        int before = FocusCalls(ctx);
        await ClickAsync(cut, () => cut.Find(".inline-confirm-cancel"));

        cut.WaitForAssertion(() => Assert.Equal(before + 1, FocusCalls(ctx)));
        Assert.True(HasButton(cut, "Remove"));
    }

    /// <summary>Cancelling the Chief of Staff's Reset confirm hands focus back to the Reset button.</summary>
    [Fact]
    public async Task Reset_Cancel_ReturnsFocusToTheResetButton()
    {
        await using var factory = new TeamWebApplicationFactory();
        var seeded = factory.Services.GetRequiredService<PersonaStore>().Entries.Single(
            entry => string.Equals(entry.Builtin, BuiltinTeammate.ChiefOfStaffMarker, StringComparison.Ordinal));
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, seeded.Name);
        await ClickButtonAsync(cut, "Reset to default");
        int before = FocusCalls(ctx);
        await ClickAsync(cut, () => cut.Find(".inline-confirm-cancel"));

        cut.WaitForAssertion(() => Assert.Equal(before + 1, FocusCalls(ctx)));
        Assert.True(HasButton(cut, "Reset to default"));
    }

    /// <summary>How many times the page asked the browser to move focus.</summary>
    /// <param name="ctx">The test context whose JS interop records the calls.</param>
    private static int FocusCalls(MudBunitContext ctx) =>
        ctx.JSInterop.Invocations.Count(static call => string.Equals(call.Identifier, "Blazor._internal.domWrapper.focus", StringComparison.Ordinal));

    /// <summary>UX-R13 and UX-080: the Chief of Staff's Reset asks through an <see cref="InlineConfirm"/> titled "Reset to default?" whose confirm button says "Reset".</summary>
    [Fact]
    public async Task Reset_AsksThroughInlineConfirmWithItsVerb()
    {
        await using var factory = new TeamWebApplicationFactory();
        var personas = factory.Services.GetRequiredService<PersonaStore>();
        var seeded = personas.Entries.Single(
            entry => string.Equals(entry.Builtin, BuiltinTeammate.ChiefOfStaffMarker, StringComparison.Ordinal));
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, seeded.Name);
        await ClickButtonAsync(cut, "Reset to default");

        IElement group = Assert.Single(cut.FindAll(".inline-confirm"));
        Assert.Equal("Reset to default?", group.GetAttribute("aria-label"));
        Assert.Equal("Reset", cut.Find(".inline-confirm-confirm").TextContent.Trim());
        Assert.False(HasButton(cut, "Confirm"));
    }

    /// <summary>UX-R18: pressing the avatar opens an <see cref="AvatarEditor"/> in Buffered mode, so the card (not the editor) decides when an avatar is written.</summary>
    [Fact]
    public async Task AvatarDialog_HostsABufferedAvatarEditor()
    {
        await using var factory = new TeamWebApplicationFactory();
        await SeedPersonaAsync(factory, "coo", "x");
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");
        await OpenAvatarEditorAsync(cut);

        var editor = Assert.Single(cut.FindComponents<AvatarEditor>());
        Assert.Equal(AvatarSaveMode.Buffered, editor.Instance.SaveMode);
    }
}
