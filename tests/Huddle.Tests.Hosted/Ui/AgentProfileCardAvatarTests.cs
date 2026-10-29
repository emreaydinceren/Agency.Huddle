using System.Text.RegularExpressions;
using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Utilities;
using Agency.Huddle.App.Avatars;
using static Agency.Huddle.Tests.Ui.AgentProfileCardTestSupport;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Avatar editing from <see cref="Agency.Huddle.App.Components.Shared.AgentProfileCard"/>: the card's avatar is a button that opens
/// <see cref="Agency.Huddle.App.Components.Shared.AvatarEditorDialog"/> in every mode, the card carries no inline avatar controls any more,
/// Create and Edit buffer what the editor applies until the card is saved, and View (which has no Save of its own) writes at once.
/// Split out of <see cref="AgentProfileCardTests"/> so xUnit can run the classes in parallel.
/// </summary>
public sealed class AgentProfileCardAvatarTests
{
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

    /// <summary>The text of the avatar inside the card's own avatar button, never the editor's preview.</summary>
    private static string CardAvatarText(IRenderedComponent<ContainerFragment> cut) =>
        cut.Find(AvatarButton + " .mud-avatar").TextContent.Trim();

    /// <summary>The text of the live preview avatar inside the editor dialog.</summary>
    private static string EditorPreviewText(IRenderedComponent<ContainerFragment> cut) =>
        cut.Find(EditorDialog + " .mud-avatar").TextContent.Trim();

    /// <summary>Waits until the editor dialog has gone from the DOM, which is how a close shows.</summary>
    private static void WaitForEditorClosed(IRenderedComponent<ContainerFragment> cut) =>
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(EditorDialog)), TimeSpan.FromSeconds(30));

    /// <summary>Chooses "A short label" in the open editor and types <paramref name="label"/>.</summary>
    private static async Task TypeLabelAsync(IRenderedComponent<ContainerFragment> cut, string label)
    {
        await SelectAvatarChoiceAsync(cut, "A short label");
        await SetImmediateTextValueAsync(cut, "Label", label);
    }

    /// <summary>Uploads <paramref name="bytes"/> through the open editor's file input.</summary>
    private static void UploadFile(IRenderedComponent<ContainerFragment> cut, byte[] bytes, string fileName) =>
        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromBinary(bytes, fileName, contentType: "image/png"));

    /// <summary>Create mode shows no avatar controls inline; clicking the avatar opens the editor with its choice cards.</summary>
    [Fact]
    public async Task CreateMode_ClickingTheAvatar_OpensTheEditorWithTheChoiceCards()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = NewContext(factory);

        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, factory);
        await ClickButtonAsync(cut, "New agent");

        Assert.DoesNotContain("Initials of the name", cut.Markup, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll(EditorDialog));

        await OpenAvatarEditorAsync(cut);

        string[] choices = [.. cut.FindAll(EditorDialog + " .avatar-choice-title").Select(title => title.TextContent.Trim())];
        Assert.Equal(["Initials of the name", "A short label", "Upload an image"], choices);
        Assert.EndsWith("'s avatar", cut.Find(EditorDialog + " .mud-dialog-title").TextContent.Trim(), StringComparison.Ordinal);
    }

    /// <summary>Edit mode shows no avatar controls inline; clicking the avatar opens the editor, and its commit button reads Apply.</summary>
    [Fact]
    public async Task EditMode_ClickingTheAvatar_OpensTheEditorWithApply()
    {
        await using TeamWebApplicationFactory factory = new();
        await SeedPersonaAsync(factory, "coo", "x");
        await using MudBunitContext ctx = NewContext(factory);

        IRenderedComponent<ContainerFragment> cut = await OpenViewCardAsync(ctx, factory, "coo");
        await ClickButtonAsync(cut, "Edit");
        Assert.DoesNotContain("Initials of the name", cut.Markup, StringComparison.Ordinal);

        await OpenAvatarEditorAsync(cut);

        Assert.True(HasEditorButton(cut, "Apply"));
        Assert.False(HasEditorButton(cut, "Save"));
        Assert.True(HasEditorButton(cut, "Cancel"));
    }

    /// <summary>View mode renders the avatar button but none of the editing controls; clicking it opens the editor, and its commit button reads Save.</summary>
    [Fact]
    public async Task ViewMode_ClickingTheAvatar_OpensTheEditorWithSave()
    {
        await using TeamWebApplicationFactory factory = new();
        await SeedPersonaAsync(factory, "coo", "x");
        await using MudBunitContext ctx = NewContext(factory);

        IRenderedComponent<ContainerFragment> cut = await OpenViewCardAsync(ctx, factory, "coo");

        Assert.NotNull(cut.Find(AvatarButton));
        Assert.DoesNotContain("Initials of the name", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("A short label", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Upload an image", cut.Markup, StringComparison.Ordinal);

        await OpenAvatarEditorAsync(cut);

        Assert.True(HasEditorButton(cut, "Save"));
        Assert.False(HasEditorButton(cut, "Apply"));
    }

    /// <summary>The avatar button names itself for assistive technology, in every mode.</summary>
    [Fact]
    public async Task AvatarButton_HasAnAccessibleName()
    {
        await using TeamWebApplicationFactory factory = new();
        await SeedPersonaAsync(factory, "coo", "x");
        await using MudBunitContext ctx = NewContext(factory);

        IRenderedComponent<ContainerFragment> cut = await OpenViewCardAsync(ctx, factory, "coo");

        Assert.Equal("Change avatar", cut.Find(AvatarButton).GetAttribute("aria-label"));
    }

    /// <summary>
    /// The frozen-parameter proof, and the most valuable test in this file: MudBlazor freezes a
    /// dialog's own <c>[Parameter]</c>s at the moment it opens (see the file-level comment on
    /// <c>AgentProfileCard.razor</c>), so choosing "A short label" and typing can only reach the preview
    /// the editor renders because that preview is an ordinary CHILD render reading a LOCAL field inside
    /// this SAME already-open dialog. The card's own avatar must not change until Apply.
    /// </summary>
    [Fact]
    public async Task EditMode_ChoosingLabelAndTyping_UpdatesThePreviewInsideTheEditor_AndTheCardOnlyAfterApply()
    {
        await using TeamWebApplicationFactory factory = new();
        await SeedPersonaAsync(factory, "coo", "x");
        await using MudBunitContext ctx = NewContext(factory);
        AvatarStore avatars = factory.Services.GetRequiredService<AvatarStore>();

        IRenderedComponent<ContainerFragment> cut = await OpenViewCardAsync(ctx, factory, "coo");
        await ClickButtonAsync(cut, "Edit");
        await OpenAvatarEditorAsync(cut);
        await TypeLabelAsync(cut, "AB");

        Assert.Equal("AB", EditorPreviewText(cut));
        Assert.Equal("CO",CardAvatarText(cut));

        await ClickEditorButtonAsync(cut, "Apply");
        WaitForEditorClosed(cut);

        Assert.Equal("AB", CardAvatarText(cut));
        Assert.True(avatars.Get("coo").IsDefault);
    }

    /// <summary>In Edit, what the editor applied is written when the card is saved.</summary>
    [Fact]
    public async Task EditMode_ApplyThenSave_PersistsTheAvatar()
    {
        await using TeamWebApplicationFactory factory = new();
        await SeedPersonaAsync(factory, "coo", "x");
        await using MudBunitContext ctx = NewContext(factory);
        AvatarStore avatars = factory.Services.GetRequiredService<AvatarStore>();

        IRenderedComponent<ContainerFragment> cut = await OpenViewCardAsync(ctx, factory, "coo");
        await ClickButtonAsync(cut, "Edit");
        await OpenAvatarEditorAsync(cut);
        await TypeLabelAsync(cut, "AB");
        await ClickEditorButtonAsync(cut, "Apply");
        WaitForEditorClosed(cut);
        await ClickButtonAsync(cut, "Save");

        Assert.Equal("AB", avatars.Get("coo").Label);
    }

    /// <summary>In Edit, applying and then cancelling the card writes nothing: Apply buffers, it does not save.</summary>
    [Fact]
    public async Task EditMode_ApplyThenCancelTheCard_PersistsNothing()
    {
        await using TeamWebApplicationFactory factory = new();
        await SeedPersonaAsync(factory, "coo", "x");
        await using MudBunitContext ctx = NewContext(factory);
        AvatarStore avatars = factory.Services.GetRequiredService<AvatarStore>();

        IRenderedComponent<ContainerFragment> cut = await OpenViewCardAsync(ctx, factory, "coo");
        await ClickButtonAsync(cut, "Edit");
        await OpenAvatarEditorAsync(cut);
        await TypeLabelAsync(cut, "AB");
        await ClickEditorButtonAsync(cut, "Apply");
        WaitForEditorClosed(cut);
        await ClickButtonAsync(cut, "Cancel");

        Assert.True(avatars.Get("coo").IsDefault);
    }

    /// <summary>Cancelling the editor discards what was typed in it: the card's avatar is unchanged and the editor reopens as it was.</summary>
    [Fact]
    public async Task Editor_Cancel_DiscardsTheWorkingState()
    {
        await using TeamWebApplicationFactory factory = new();
        await SeedPersonaAsync(factory, "coo", "x");
        await using MudBunitContext ctx = NewContext(factory);

        IRenderedComponent<ContainerFragment> cut = await OpenViewCardAsync(ctx, factory, "coo");
        await ClickButtonAsync(cut, "Edit");
        await OpenAvatarEditorAsync(cut);
        await TypeLabelAsync(cut, "AB");
        await ClickEditorButtonAsync(cut, "Cancel");
        WaitForEditorClosed(cut);

        Assert.Equal("CO",CardAvatarText(cut));

        await OpenAvatarEditorAsync(cut);

        Assert.Equal("CO",EditorPreviewText(cut));
        Assert.Equal("Upload an image", cut.Find(EditorDialog + " .avatar-choice-card-selected .avatar-choice-title").TextContent.Trim());
    }

    /// <summary>Reopening the editor after an Apply starts from what was applied, not from what is on disk.</summary>
    [Fact]
    public async Task Editor_Reopened_ShowsWhatWasApplied()
    {
        await using TeamWebApplicationFactory factory = new();
        await SeedPersonaAsync(factory, "coo", "x");
        await using MudBunitContext ctx = NewContext(factory);

        IRenderedComponent<ContainerFragment> cut = await OpenViewCardAsync(ctx, factory, "coo");
        await ClickButtonAsync(cut, "Edit");
        await OpenAvatarEditorAsync(cut);
        await TypeLabelAsync(cut, "AB");
        await ClickEditorButtonAsync(cut, "Apply");
        WaitForEditorClosed(cut);
        await OpenAvatarEditorAsync(cut);

        IElement input = FindInputControl(cut, "Label").QuerySelector("input") ?? throw new InvalidOperationException("No input under 'Label'.");
        Assert.Equal("AB", input.GetAttribute("value"));
        Assert.Equal("AB", EditorPreviewText(cut));
    }

    /// <summary>View mode has no Save of its own, so the editor's Save writes the avatar at once and the card stays in View.</summary>
    [Fact]
    public async Task ViewMode_EditorSave_WritesImmediately_AndTheCardStaysInView()
    {
        await using TeamWebApplicationFactory factory = new();
        await SeedPersonaAsync(factory, "coo", "x");
        await using MudBunitContext ctx = NewContext(factory);
        AvatarStore avatars = factory.Services.GetRequiredService<AvatarStore>();

        IRenderedComponent<ContainerFragment> cut = await OpenViewCardAsync(ctx, factory, "coo");
        await OpenAvatarEditorAsync(cut);
        await TypeLabelAsync(cut, "AB");
        await ClickEditorButtonAsync(cut, "Save");
        WaitForEditorClosed(cut);

        Assert.Equal("AB", avatars.Get("coo").Label);
        Assert.Equal("AB", CardAvatarText(cut));
        Assert.True(HasButton(cut, "Edit"));
    }

    /// <summary>View mode's Save keeps the Background the editor was not asked to change: a label change must not wipe a stored colour.</summary>
    [Fact]
    public async Task ViewMode_EditorSave_KeepsTheStoredBackground()
    {
        await using TeamWebApplicationFactory factory = new();
        await SeedPersonaAsync(factory, "coo", "x");
        await using MudBunitContext ctx = NewContext(factory);
        AvatarStore avatars = factory.Services.GetRequiredService<AvatarStore>();
        avatars.Save("coo", new Avatar(Label: null, Image: null, Background: "#336699"));

        IRenderedComponent<ContainerFragment> cut = await OpenViewCardAsync(ctx, factory, "coo");
        await OpenAvatarEditorAsync(cut);
        await TypeLabelAsync(cut, "AB");
        await ClickEditorButtonAsync(cut, "Save");
        WaitForEditorClosed(cut);

        Avatar saved = avatars.Get("coo");
        Assert.Equal("AB", saved.Label);
        Assert.Equal("#336699", saved.Background);
    }

    /// <summary>A stored colour is replaced by pressing another swatch; the label beside it is kept.</summary>
    [Fact]
    public async Task ViewMode_EditorPressingASwatch_ReplacesAStoredColour()
    {
        await using TeamWebApplicationFactory factory = new();
        await SeedPersonaAsync(factory, "coo", "x");
        await using MudBunitContext ctx = NewContext(factory);
        AvatarStore avatars = factory.Services.GetRequiredService<AvatarStore>();
        avatars.Save("coo", new Avatar(Label: "AB", Image: null, Background: "#336699"));

        IRenderedComponent<ContainerFragment> cut = await OpenViewCardAsync(ctx, factory, "coo");
        await OpenAvatarEditorAsync(cut);
        await ClickAsync(cut, () => cut.FindAll(Swatch)[5]);
        await ClickEditorButtonAsync(cut, "Save");
        WaitForEditorClosed(cut);

        Avatar saved = avatars.Get("coo");
        Assert.Equal("AB", saved.Label);
        Assert.Equal(AvatarPalette.Colours[5].ToString(MudColorOutputFormats.Hex), saved.Background, ignoreCase: true);
    }

    /// <summary>The Background control is the palette: one swatch for every colour in <see cref="AvatarPalette.Colours"/>, in order, not a dropdown field.</summary>
    [Fact]
    public async Task Editor_Background_OffersEverySwatchOfThePalette()
    {
        await using TeamWebApplicationFactory factory = new();
        await SeedPersonaAsync(factory, "coo", "x");
        await using MudBunitContext ctx = NewContext(factory);

        IRenderedComponent<ContainerFragment> cut = await OpenViewCardAsync(ctx, factory, "coo");
        await OpenAvatarEditorAsync(cut);
        await SelectAvatarChoiceAsync(cut, "Initials of the name");

        Assert.Equal(AvatarPalette.Colours.Count, cut.FindAll(Swatch).Count);
        Assert.Single(cut.FindAll(BackgroundPicker + " [role='radiogroup']"));
        Assert.Empty(cut.FindAll(BackgroundPicker + " .mud-select"));
    }

    /// <summary>Pressing a swatch chooses that colour, and Save writes it as the six-digit hex the palette holds.</summary>
    [Fact]
    public async Task ViewMode_EditorPressingASwatch_SavesThatColour()
    {
        await using TeamWebApplicationFactory factory = new();
        await SeedPersonaAsync(factory, "coo", "x");
        await using MudBunitContext ctx = NewContext(factory);
        AvatarStore avatars = factory.Services.GetRequiredService<AvatarStore>();

        IRenderedComponent<ContainerFragment> cut = await OpenViewCardAsync(ctx, factory, "coo");
        await OpenAvatarEditorAsync(cut);
        await SelectAvatarChoiceAsync(cut, "Initials of the name");
        await ClickAsync(cut, () => cut.FindAll(Swatch)[3]);
        await ClickEditorButtonAsync(cut, "Save");
        WaitForEditorClosed(cut);

        Assert.Equal(AvatarPalette.Colours[3].ToString(MudColorOutputFormats.Hex), avatars.Get("coo").Background, ignoreCase: true);
    }

    /// <summary>
    /// Opening the editor and saving a label must not invent a colour: a picker that reports its default value on first render would
    /// write one, and an avatar that had no background would gain one the Human never chose.
    /// </summary>
    [Fact]
    public async Task ViewMode_EditorSaveWithoutTouchingTheBackground_LeavesNoColour()
    {
        await using TeamWebApplicationFactory factory = new();
        await SeedPersonaAsync(factory, "coo", "x");
        await using MudBunitContext ctx = NewContext(factory);
        AvatarStore avatars = factory.Services.GetRequiredService<AvatarStore>();

        IRenderedComponent<ContainerFragment> cut = await OpenViewCardAsync(ctx, factory, "coo");
        await OpenAvatarEditorAsync(cut);
        await TypeLabelAsync(cut, "AB");
        await ClickEditorButtonAsync(cut, "Save");
        WaitForEditorClosed(cut);

        Avatar saved = avatars.Get("coo");
        Assert.Equal("AB", saved.Label);
        Assert.Null(saved.Background);
    }

    /// <summary>A new Agent's card starts with a random colour from the palette behind its initials, drawn when the card opens.</summary>
    [Fact]
    public async Task CreateMode_StartsWithARandomBackgroundFromThePalette()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = NewContext(factory);

        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, factory);
        await ClickButtonAsync(cut, "New agent");

        Assert.Contains(CardAvatarBackground(cut), PaletteHex());
    }

    /// <summary>The colour a new Agent's card started with is the one written when it is added.</summary>
    [Fact]
    public async Task CreateMode_AddAgent_SavesTheRandomBackground()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = NewContext(factory);
        AvatarStore avatars = factory.Services.GetRequiredService<AvatarStore>();

        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, factory);
        await ClickButtonAsync(cut, "New agent");
        string shown = CardAvatarBackground(cut);
        await SetTextValueAsync(cut, "Name", "zzcolour");
        await SetTextValueAsync(cut, "Title", "Probe");
        await SetTextValueAsync(cut, "Alias", "zzc");
        await SetTextValueAsync(cut, "Persona text", "a body");
        await ClickButtonAsync(cut, "Add agent");

        Assert.Equal(shown, avatars.Get("zzcolour").Background, ignoreCase: true);
    }

    /// <summary>Only creation draws a colour: an existing Agent with no avatar opens, in View and in Edit, with no background at all.</summary>
    [Fact]
    public async Task ExistingAgent_WithNoAvatar_GetsNoRandomBackground()
    {
        await using TeamWebApplicationFactory factory = new();
        await SeedPersonaAsync(factory, "coo", "x");
        await using MudBunitContext ctx = NewContext(factory);

        IRenderedComponent<ContainerFragment> cut = await OpenViewCardAsync(ctx, factory, "coo");

        Assert.Equal(string.Empty, CardAvatarStyle(cut));

        await ClickButtonAsync(cut, "Edit");

        Assert.Equal(string.Empty, CardAvatarStyle(cut));
    }

    /// <summary>The hex of every palette colour, compared case-insensitively against what the page and the store hold.</summary>
    private static HashSet<string> PaletteHex() =>
        new(AvatarPalette.Colours.Select(static colour => colour.ToString(MudColorOutputFormats.Hex)), StringComparer.OrdinalIgnoreCase);

    /// <summary>The inline <c>style</c> of the card's own avatar, empty when it has none.</summary>
    private static string CardAvatarStyle(IRenderedComponent<ContainerFragment> cut) =>
        cut.Find(AvatarButton + " .mud-avatar").GetAttribute("style") ?? string.Empty;

    /// <summary>The six-digit hex in the inline <c>background</c> of the card's own avatar.</summary>
    private static string CardAvatarBackground(IRenderedComponent<ContainerFragment> cut)
    {
        string style = CardAvatarStyle(cut);
        Match match = Regex.Match(style, @"(?:(?:background|--mud-palette-primary):\s*)(#[0-9a-fA-F]{6})", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));
        Assert.True(match.Success, $"The card's avatar has no background in its style: '{style}'.");
        return match.Groups[1].Value;
    }

    /// <summary>
    /// A background colour only matters behind initials or a label: the palette shows for those two choices and is hidden under
    /// the upload card - still rendered and inert, so the dialog keeps its height (B8i). The palette is the whole Background
    /// control: there is no button to clear it back to the theme's colour.
    /// </summary>
    [Fact]
    public async Task Editor_BackgroundPalette_ShowsForInitialsAndLabel_AndIsHiddenForAnImage()
    {
        await using TeamWebApplicationFactory factory = new();
        await SeedPersonaAsync(factory, "coo", "x");
        await using MudBunitContext ctx = NewContext(factory);

        IRenderedComponent<ContainerFragment> cut = await OpenViewCardAsync(ctx, factory, "coo");
        await OpenAvatarEditorAsync(cut);

        Assert.Empty(cut.FindAll(BackgroundPicker));

        await SelectAvatarChoiceAsync(cut, "Initials of the name");

        Assert.Single(cut.FindAll(BackgroundPicker));
        Assert.Empty(cut.FindAll(BackgroundPicker + " button"));
        Assert.False(HasEditorButton(cut, "Use the theme colour"));

        await SelectAvatarChoiceAsync(cut, "A short label");

        Assert.Single(cut.FindAll(BackgroundPicker));
        Assert.False(HasEditorButton(cut, "Use the theme colour"));

        await SelectAvatarChoiceAsync(cut, "Upload an image");

        Assert.Empty(cut.FindAll(BackgroundPicker));
        Assert.Single(cut.FindAll(HiddenBackgroundPicker));

        await SelectAvatarChoiceAsync(cut, "Initials of the name");

        Assert.Single(cut.FindAll(BackgroundPicker));
    }

    /// <summary>Hiding the Background controls under "An image" does not erase a stored colour: saving an image leaves it as it was, ready for a return to initials or a label.</summary>
    [Fact]
    public async Task ViewMode_EditorSaveWithAnImage_LeavesTheStoredBackgroundAlone()
    {
        await using TeamWebApplicationFactory factory = new();
        await SeedPersonaAsync(factory, "coo", "x");
        await using MudBunitContext ctx = NewContext(factory);
        AvatarStore avatars = factory.Services.GetRequiredService<AvatarStore>();
        avatars.Save("coo", new Avatar(Label: null, Image: null, Background: "#336699"));

        IRenderedComponent<ContainerFragment> cut = await OpenViewCardAsync(ctx, factory, "coo");
        await OpenAvatarEditorAsync(cut);
        await SelectAvatarChoiceAsync(cut, "Upload an image");
        UploadFile(cut, MinimalPng, "avatar.png");
        await ClickEditorButtonAsync(cut, "Save");
        WaitForEditorClosed(cut);

        Avatar saved = avatars.Get("coo");
        Assert.NotNull(saved.Image);
        Assert.Equal("#336699", saved.Background);
    }

    /// <summary>A stored image opens the editor on "An image", names the file, and "Remove image" followed by Save clears it.</summary>
    [Fact]
    public async Task ViewMode_EditorRemoveImage_ThenSave_ClearsTheStoredImage()
    {
        await using TeamWebApplicationFactory factory = new();
        await SeedPersonaAsync(factory, "coo", "x");
        await using MudBunitContext ctx = NewContext(factory);
        AvatarStore avatars = factory.Services.GetRequiredService<AvatarStore>();
        string file = avatars.WriteImage(MinimalPng, ".png");
        avatars.Save("coo", new Avatar(Label: null, Image: file, Background: null));

        IRenderedComponent<ContainerFragment> cut = await OpenViewCardAsync(ctx, factory, "coo");
        await OpenAvatarEditorAsync(cut);

        Assert.Equal(file, EditorImageStatus(cut));

        await ClickEditorButtonAsync(cut, "Remove image");
        await ClickEditorButtonAsync(cut, "Save");
        WaitForEditorClosed(cut);

        Assert.Null(avatars.Get("coo").Image);
    }

    /// <summary>Uploading an image and then choosing a label instead means the image is no longer wanted: applying and adding the agent files no orphan image.</summary>
    [Fact]
    public async Task CreateMode_UploadThenChooseLabel_FilesNoImage()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = NewContext(factory);
        AvatarStore avatars = factory.Services.GetRequiredService<AvatarStore>();

        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, factory);
        await ClickButtonAsync(cut, "New agent");
        await SetTextValueAsync(cut, "Name", "zzlabel");
        await SetTextValueAsync(cut, "Title", "Probe");
        await SetTextValueAsync(cut, "Alias", "zzl");
        await SetTextValueAsync(cut, "Persona text", "a body");
        await OpenAvatarEditorAsync(cut);
        await SelectAvatarChoiceAsync(cut, "Upload an image");
        UploadFile(cut, MinimalPng, "avatar.png");
        await TypeLabelAsync(cut, "AB");
        await ClickEditorButtonAsync(cut, "Apply");
        WaitForEditorClosed(cut);

        Assert.Empty(cut.FindAll(AvatarButton + " img"));
        Assert.Equal("AB", CardAvatarText(cut));

        await ClickButtonAsync(cut, "Add agent");

        Avatar saved = avatars.Get("zzlabel");
        Assert.Equal("AB", saved.Label);
        Assert.Null(saved.Image);
        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(DataDirOf(factory), "avatars")));
    }

    /// <summary>Choosing a file, applying it and then cancelling the card leaves <c>{DataDir}/avatars</c> untouched - the buffer-until-save proof: nothing reaches disk before the card is saved.</summary>
    [Fact]
    public async Task CreateMode_UploadApplyThenCancelTheCard_LeavesTheAvatarsDirectoryEmpty()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = NewContext(factory);

        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, factory);
        await ClickButtonAsync(cut, "New agent");
        await OpenAvatarEditorAsync(cut);
        await SelectAvatarChoiceAsync(cut, "Upload an image");
        UploadFile(cut, MinimalPng, "avatar.png");
        await ClickEditorButtonAsync(cut, "Apply");
        WaitForEditorClosed(cut);
        await ClickButtonAsync(cut, "Cancel");

        // Program.cs creates {DataDir}/avatars unconditionally at startup so the static-file
        // middleware always has a directory to point at (see AvatarEndpointTests) - so its mere
        // existence proves nothing here. Emptiness is the actual proof: nothing was ever WRITTEN
        // into it, because the card's Cancel never reached SaveAsync, the only place that calls WriteImage.
        string avatarsDir = Path.Combine(DataDirOf(factory), "avatars");
        Assert.Empty(Directory.EnumerateFileSystemEntries(avatarsDir));
    }

    /// <summary>An applied upload shows on the card's own avatar as an inline image before any save, and adding the agent files it under the new Name.</summary>
    [Fact]
    public async Task CreateMode_UploadApplyThenAdd_ShowsAPreviewAndWritesTheImageUnderTheNewName()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = NewContext(factory);
        AvatarStore avatars = factory.Services.GetRequiredService<AvatarStore>();

        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, factory);
        await ClickButtonAsync(cut, "New agent");
        await SetTextValueAsync(cut, "Name", "zzavatar");
        await SetTextValueAsync(cut, "Title", "Probe");
        await SetTextValueAsync(cut, "Alias", "zza");
        await SetTextValueAsync(cut, "Persona text", "a body");
        await OpenAvatarEditorAsync(cut);
        await SelectAvatarChoiceAsync(cut, "Upload an image");
        UploadFile(cut, MinimalPng, "avatar.png");
        await ClickEditorButtonAsync(cut, "Apply");
        WaitForEditorClosed(cut);

        string? source = cut.Find(AvatarButton + " img").GetAttribute("src");
        Assert.NotNull(source);
        Assert.Equal("data:image/png;base64," + Convert.ToBase64String(MinimalPng), source);
        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(DataDirOf(factory), "avatars")));

        await ClickButtonAsync(cut, "Add agent");

        string? image = avatars.Get("zzavatar").Image;
        Assert.NotNull(image);
        Assert.Equal(".png", Path.GetExtension(image));
        Assert.Single(Directory.EnumerateFileSystemEntries(Path.Combine(DataDirOf(factory), "avatars")));
    }

    /// <summary>An oversized upload shows an error inside the editor and keeps both dialogs open, rather than throwing or silently accepting it.</summary>
    [Fact]
    public async Task Editor_OversizedImage_ShowsAnErrorAndKeepsBothDialogsOpen()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = NewContext(factory);

        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, factory);
        await ClickButtonAsync(cut, "New agent");
        await OpenAvatarEditorAsync(cut);
        await SelectAvatarChoiceAsync(cut, "Upload an image");
        UploadFile(cut, new byte[AvatarImage.MaxBytes + 1], "big.png");

        Assert.Equal("That file is larger than 512 KB.", cut.Find(EditorDialog + " .mud-alert").TextContent.Trim());
        Assert.Equal(2, cut.FindAll(".mud-dialog").Count);
    }

    /// <summary>An upload whose bytes are not a PNG, JPEG or WebP signature shows the exact wording the editor promises.</summary>
    [Fact]
    public async Task Editor_UploadedFileIsNotARecognisedImage_ShowsAnError()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = NewContext(factory);

        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, factory);
        await ClickButtonAsync(cut, "New agent");
        await OpenAvatarEditorAsync(cut);
        await SelectAvatarChoiceAsync(cut, "Upload an image");
        UploadFile(cut, [1, 2, 3, 4], "fake.png");

        Assert.Equal("That file is not a PNG, JPEG or WebP image.", cut.Find(EditorDialog + " .mud-alert").TextContent.Trim());
    }
}
