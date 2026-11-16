using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Components.Shared;
using static Agency.Huddle.Tests.Ui.AvatarLibraryPickSupport;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Renders <see cref="AvatarEditor"/> on its own, in both save modes. The component is a radiogroup of choice cards (initials, a short
/// label, the library, an upload) with the label field and its counter, and the background swatches; its
/// <see cref="AvatarEditor.SaveMode"/> decides whether each change is written to the <see cref="AvatarStore"/> under
/// <see cref="AvatarEditor.Name"/> (Immediate, what Settings uses) or only reported through
/// <see cref="AvatarEditor.ValueChanged"/> and <see cref="AvatarEditor.PendingImageChanged"/> (Buffered, what the
/// Agent card will use).
/// </summary>
public sealed class AvatarEditorTests
{
    /// <summary>The CSS selector of one swatch in the Background palette while it is offered; under an image card the palette is rendered but hidden, and matches nothing here.</summary>
    private const string Swatch = ".avatar-editor-background:not(.avatar-editor-background-hidden) .avatar-swatch-input";

    /// <summary>What the editor reported, and the parameters a parent would pass on its next render.</summary>
    private sealed class Probe(AvatarSaveMode mode)
    {
        /// <summary>The save mode the editor is rendered in.</summary>
        public AvatarSaveMode Mode { get; } = mode;

        /// <summary>The avatar the parent passes down.</summary>
        public Avatar Value { get; set; } = Avatar.None;

        /// <summary>Whether the parent offers the library card.</summary>
        public bool AllowLibrary { get; init; } = true;

        /// <summary>Whether the parent offers any picture card.</summary>
        public bool AllowImages { get; init; } = true;

        /// <summary>The outline the parent asks the previews to be drawn in.</summary>
        public AvatarShape Shape { get; init; } = AvatarShape.Circle;

        /// <summary>Every avatar the editor raised through <see cref="AvatarEditor.ValueChanged"/>, in order.</summary>
        public List<Avatar> Values { get; } = [];

        /// <summary>Every pair the editor raised through <see cref="AvatarEditor.PendingImageChanged"/>, in order.</summary>
        public List<(byte[]? Bytes, string? Extension)> Pending { get; } = [];
    }

    /// <summary>Registers the factory's real avatar store and the unconfigured library defaults into a fresh context.</summary>
    private static MudBunitContext NewContext(TeamWebApplicationFactory factory)
    {
        MudBunitContext ctx = new();
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<AvatarStore>());
        RegisterUnconfigured(ctx.Services);
        return ctx;
    }

    /// <summary>A parent of the editor that re-reads <see cref="Probe"/> on every render, as a real parent re-reads its store, and can be told to render again.</summary>
    private sealed class Host : ComponentBase
    {
        /// <summary>What to pass the editor and where to record what it reports.</summary>
        [Parameter]
        public Probe? Source { get; set; }

        /// <summary>Renders this host again, so the editor receives the probe's current <see cref="Probe.Value"/>.</summary>
        public void Refresh() => this.StateHasChanged();

        /// <inheritdoc />
        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
            Probe probe = this.Source ?? throw new InvalidOperationException("No probe.");
            builder.OpenComponent<AvatarEditor>(0);
            builder.AddAttribute(1, nameof(AvatarEditor.Name), "You");
            builder.AddAttribute(2, nameof(AvatarEditor.Value), probe.Value);
            builder.AddAttribute(3, nameof(AvatarEditor.SaveMode), probe.Mode);
            builder.AddAttribute(4, nameof(AvatarEditor.ValueChanged), EventCallback.Factory.Create<Avatar>(probe, probe.Values.Add));
            builder.AddAttribute(5, nameof(AvatarEditor.PendingImageChanged), EventCallback.Factory.Create<(byte[]? Bytes, string? Extension)>(probe, probe.Pending.Add));
            builder.AddAttribute(6, nameof(AvatarEditor.AllowLibrary), probe.AllowLibrary);
            builder.AddAttribute(7, nameof(AvatarEditor.Shape), probe.Shape);
            builder.AddAttribute(8, nameof(AvatarEditor.AllowImages), probe.AllowImages);
            builder.CloseComponent();
        }
    }

    /// <summary>Renders the editor for the Human's Name ("You", the factory's configured one) inside a <see cref="Host"/>, with popover and dialog providers.</summary>
    private static IRenderedComponent<ContainerFragment> RenderEditor(MudBunitContext ctx, Probe probe) =>
        ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<Host>(0);
            builder.AddAttribute(1, nameof(Host.Source), probe);
            builder.CloseComponent();
        });

    /// <summary>Makes the parent render again with the probe's current value.</summary>
    private static Task RefreshParentAsync(IRenderedComponent<ContainerFragment> cut) =>
        cut.InvokeAsync(cut.FindComponent<Host>().Instance.Refresh);

    /// <summary>The framework call <c>ElementReference.FocusAsync</c> makes, which <c>MudTextField.FocusAsync</c> ends in.</summary>
    private const string FocusCall = "Blazor._internal.domWrapper.focus";

    /// <summary>The choice cards, in page order.</summary>
    private const string Cards = ".avatar-choice-card";

    /// <summary>The visible name of the upload card.</summary>
    private const string UploadCard = "Upload an image";

    /// <summary>The visible name of the library card.</summary>
    private const string LibraryCard = "Image from the library";

    /// <summary>The choice card whose text contains <paramref name="choiceLabel"/>.</summary>
    private static IElement Card(IRenderedComponent<ContainerFragment> cut, string choiceLabel) =>
        cut.FindAll(Cards).First(element => element.TextContent.Contains(choiceLabel, StringComparison.Ordinal));

    /// <summary>The radio input inside the card named <paramref name="choiceLabel"/>.</summary>
    private static IElement Radio(IRenderedComponent<ContainerFragment> cut, string choiceLabel) =>
        Card(cut, choiceLabel).QuerySelector("input[type=radio]") ?? throw new InvalidOperationException($"No radio input under '{choiceLabel}'.");

    /// <summary>Whether the card named <paramref name="choiceLabel"/> is the chosen one (carries the selected class).</summary>
    private static bool IsSelected(IRenderedComponent<ContainerFragment> cut, string choiceLabel) =>
        Card(cut, choiceLabel).ClassList.Contains("avatar-choice-card-selected");

    /// <summary>Clicks the radio input of the card named <paramref name="choiceLabel"/>; the click bubbles to the card as in a browser.</summary>
    private static Task SelectChoiceAsync(IRenderedComponent<ContainerFragment> cut, string choiceLabel) =>
        cut.InvokeAsync(() => Radio(cut, choiceLabel).ClickAsync());

    /// <summary>Types <paramref name="value"/> into the Label field by raising its <c>oninput</c>.</summary>
    private static Task TypeLabelAsync(IRenderedComponent<ContainerFragment> cut, string value) =>
        AgentProfileCardTestSupport.SetAvatarLabelAsync(cut, value);

    /// <summary>Clicks the first button whose trimmed text equals <paramref name="text"/>.</summary>
    private static Task ClickButtonAsync(IRenderedComponent<ContainerFragment> cut, string text) =>
        cut.InvokeAsync(() => cut.FindAll("button")
            .First(button => string.Equals(button.TextContent.Trim(), text, StringComparison.Ordinal))
            .ClickAsync());

    /// <summary>Whether some element with no children has exactly the trimmed text <paramref name="text"/>.</summary>
    private static bool HasText(IRenderedComponent<ContainerFragment> cut, string text) =>
        cut.FindAll("*").Any(element => element.Children.Length == 0 && string.Equals(element.TextContent.Trim(), text, StringComparison.Ordinal));

    /// <summary>Whether some input on the page holds exactly <paramref name="value"/>.</summary>
    private static bool HasInputValue(IRenderedComponent<ContainerFragment> cut, string value) =>
        cut.FindAll("input").OfType<AngleSharp.Html.Dom.IHtmlInputElement>().Any(input => string.Equals(input.Value, value, StringComparison.Ordinal));

    /// <summary>Whether a button with the trimmed text <paramref name="text"/> is on the page.</summary>
    private static bool HasButton(IRenderedComponent<ContainerFragment> cut, string text) =>
        cut.FindAll("button").Any(button => string.Equals(button.TextContent.Trim(), text, StringComparison.Ordinal));

    /// <summary>Uploads <paramref name="bytes"/> through the editor's file input.</summary>
    private static void Upload(IRenderedComponent<ContainerFragment> cut, byte[] bytes, string fileName) =>
        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromBinary(bytes, fileName, contentType: "image/png"));

    /// <summary>Picks the fourth thumbnail in the library dialog, after opening it from the editor's library button.</summary>
    private static async Task PickFromLibraryAsync(IRenderedComponent<ContainerFragment> cut)
    {
        // ClickAsync() is not awaited here: the handler awaits the dialog's result, so awaiting it would wait for the pick that comes next.
        await cut.InvokeAsync(() => { _ = cut.FindAll("button").First(button => string.Equals(button.TextContent.Trim(), ButtonText, StringComparison.Ordinal)).ClickAsync(); });
        cut.WaitForAssertion(() => Assert.True(cut.FindAll(Thumb).Count > 3), TimeSpan.FromSeconds(5));
        await cut.InvokeAsync(() => cut.FindAll(Thumb)[3].ClickAsync());
    }

    /// <summary>With nothing stored and no library the editor offers three cards in order, starts on the upload card (the avatar falls back to initials) and always renders the Label field.</summary>
    [Fact]
    public async Task Renders_ThreeCards_StartsOnUpload_AndAlwaysHasTheLabelField()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = NewContext(factory);

        IRenderedComponent<ContainerFragment> cut = RenderEditor(ctx, new Probe(AvatarSaveMode.Immediate));

        Assert.Equal(["Initials of the name", "A short label", UploadCard], cut.FindAll(".avatar-choice-title").Select(title => title.TextContent.Trim()));
        Assert.Equal("radiogroup", cut.Find(".avatar-choices").GetAttribute("role"));
        Assert.True(IsSelected(cut, UploadCard));
        Assert.True(HasButton(cut, "Upload…"));
        AssertLabelFieldIsNamedByAriaOnly(cut);

        await SelectChoiceAsync(cut, "Initials of the name");

        Assert.True(IsSelected(cut, "Initials of the name"));
        Assert.False(IsSelected(cut, UploadCard));
        Assert.True(HasButton(cut, "Upload…"));
        AssertLabelFieldIsNamedByAriaOnly(cut);
        Assert.Equal(AvatarPalette.Colours.Count, cut.FindAll(Swatch).Count);
    }

    /// <summary>A configured library adds the library card between the label and upload cards.</summary>
    [Fact]
    public async Task Renders_FourCards_WhenALibraryIsConfigured()
    {
        await using TeamWebApplicationFactory factory = new();
        using TempDataDir library = CreateLibraryDir();
        await using MudBunitContext ctx = NewContext(factory);
        _ = Register(ctx.Services, library.Path, null);

        IRenderedComponent<ContainerFragment> cut = RenderEditor(ctx, new Probe(AvatarSaveMode.Immediate));

        Assert.Equal(["Initials of the name", "A short label", LibraryCard, UploadCard], cut.FindAll(".avatar-choice-title").Select(title => title.TextContent.Trim()));
    }

    /// <summary>
    /// A Project's editor (ADR-0083) passes AllowImages false: even with a library configured it offers only Initials and A short label,
    /// starts on Initials with the swatches showing, and draws its previews as sharp squares.
    /// </summary>
    [Fact]
    public async Task AllowImagesFalse_OffersOnlyInitialsAndLabel_StartsOnInitials()
    {
        await using TeamWebApplicationFactory factory = new();
        using TempDataDir library = CreateLibraryDir();
        await using MudBunitContext ctx = NewContext(factory);
        _ = Register(ctx.Services, library.Path, null);

        IRenderedComponent<ContainerFragment> cut = RenderEditor(ctx, new Probe(AvatarSaveMode.Buffered) { AllowImages = false, Shape = AvatarShape.Square });

        Assert.Equal(["Initials of the name", "A short label"], cut.FindAll(".avatar-choice-title").Select(title => title.TextContent.Trim()));
        Assert.True(IsSelected(cut, "Initials of the name"));
        Assert.False(HasButton(cut, "Upload…"));
        Assert.False(HasButton(cut, ButtonText));
        Assert.Equal(AvatarPalette.Colours.Count, cut.FindAll(Swatch).Count);
        Assert.Equal(3, cut.FindAll(".mud-avatar.avatar-square.avatar-sharp").Count);
    }

    /// <summary>With AllowImages false a stored image is not offered or handed back: the editor opens on Initials, and the next change reports an avatar with no image.</summary>
    [Fact]
    public async Task AllowImagesFalse_StoredImage_OpensOnInitials_AndIsNeverReportedBack()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = NewContext(factory);
        Probe probe = new(AvatarSaveMode.Buffered) { AllowImages = false, Value = new Avatar(Label: null, Image: "face.png", Background: null) };

        IRenderedComponent<ContainerFragment> cut = RenderEditor(ctx, probe);
        Assert.True(IsSelected(cut, "Initials of the name"));
        Assert.False(HasText(cut, "face.png"));

        await TypeLabelAsync(cut, "🚀");

        Avatar reported = Assert.Single(probe.Values);
        Assert.Equal("🚀", reported.Label);
        Assert.Null(reported.Image);
    }

    /// <summary>A stored label starts the editor on the label card with the text in the field and a counter sized from the label limit.</summary>
    [Fact]
    public async Task Value_WithLabel_SeedsTheLabelFieldAndCounter()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = NewContext(factory);
        Probe probe = new(AvatarSaveMode.Immediate) { Value = new Avatar(Label: "AB", Image: null, Background: null) };

        IRenderedComponent<ContainerFragment> cut = RenderEditor(ctx, probe);

        Assert.True(IsSelected(cut, "A short label"));
        Assert.True(HasInputValue(cut, "AB"));
        Assert.True(HasText(cut, "2 / " + Avatar.MaxLabelTextElements));
    }

    /// <summary>A stored image reopens on the upload card, with the file name and a way to remove it, and no background swatches (the image covers the colour).</summary>
    [Fact]
    public async Task Value_WithImage_SeedsTheUploadCardAndHidesTheSwatches()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = NewContext(factory);
        Probe probe = new(AvatarSaveMode.Immediate) { Value = new Avatar(Label: null, Image: "face.png", Background: null) };

        IRenderedComponent<ContainerFragment> cut = RenderEditor(ctx, probe);

        Assert.True(IsSelected(cut, UploadCard));
        Assert.True(HasButton(cut, "Upload…"));
        Assert.True(HasButton(cut, "Remove image"));
        Assert.True(HasText(cut, "face.png"));
        Assert.Empty(cut.FindAll(Swatch));
    }

    /// <summary>Typing in the Label field chooses the label card without pressing it first.</summary>
    [Fact]
    public async Task TypingALabel_ChoosesTheLabelCard()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = NewContext(factory);

        IRenderedComponent<ContainerFragment> cut = RenderEditor(ctx, new Probe(AvatarSaveMode.Immediate));
        Assert.False(IsSelected(cut, "A short label"));
        await TypeLabelAsync(cut, "AB");

        Assert.True(IsSelected(cut, "A short label"));
        Assert.False(IsSelected(cut, UploadCard));
        Assert.Equal("AB", cut.Find(".mud-avatar").TextContent.Trim());
    }

    /// <summary>The upload card can be chosen with no image, and "Remove image" keeps it chosen.</summary>
    [Fact]
    public async Task UploadCard_StaysChosenAfterRemoveImage()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = NewContext(factory);
        Probe probe = new(AvatarSaveMode.Immediate) { Value = new Avatar(Label: null, Image: "face.png", Background: null) };

        IRenderedComponent<ContainerFragment> cut = RenderEditor(ctx, probe);
        await ClickButtonAsync(cut, "Remove image");

        Assert.True(IsSelected(cut, UploadCard));
        Assert.False(HasButton(cut, "Remove image"));
        Assert.Empty(cut.FindAll(Swatch));
    }

    /// <summary>A click on the empty part of the upload card's body, beside its button, chooses the card: only the buttons stop the click from reaching the card.</summary>
    [Fact]
    public async Task UploadCard_ClickBesideTheButton_ChoosesTheCard()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = NewContext(factory);
        Probe probe = new(AvatarSaveMode.Immediate) { Value = new Avatar(Label: "AB", Image: null, Background: null) };

        IRenderedComponent<ContainerFragment> cut = RenderEditor(ctx, probe);
        Assert.False(IsSelected(cut, UploadCard));
        await cut.InvokeAsync(() => (Card(cut, UploadCard).QuerySelector(".avatar-choice-body") ?? throw new InvalidOperationException("No card body.")).ClickAsync());

        Assert.True(IsSelected(cut, UploadCard));
    }

    /// <summary>The library card's radio is disabled until it holds a picture, a press on the card itself does nothing, and only its "Choose from library" button opens the library.</summary>
    [Fact]
    public async Task LibraryCard_WithNoPicture_IgnoresACardPress_AndOnlyTheButtonOpensTheLibrary()
    {
        await using TeamWebApplicationFactory factory = new();
        using TempDataDir library = CreateLibraryDir();
        await using MudBunitContext ctx = NewContext(factory);
        _ = Register(ctx.Services, library.Path, new AvatarLibraryImage(MinimalPng, ".png"));

        IRenderedComponent<ContainerFragment> cut = RenderEditor(ctx, new Probe(AvatarSaveMode.Immediate));
        Assert.True(Radio(cut, LibraryCard).HasAttribute("disabled"));

        await cut.InvokeAsync(() => { _ = Card(cut, LibraryCard).ClickAsync(); });

        Assert.Empty(cut.FindAll(Thumb));
        Assert.True(IsSelected(cut, UploadCard));

        await cut.InvokeAsync(() => { _ = cut.FindAll("button").First(button => string.Equals(button.TextContent.Trim(), ButtonText, StringComparison.Ordinal)).ClickAsync(); });

        cut.WaitForAssertion(() => Assert.True(cut.FindAll(Thumb).Count > 3), TimeSpan.FromSeconds(5));
    }

    /// <summary>After a library pick the library card is chosen with its picture and "Remove image"; removing returns the choice to Initials, and the Background is hidden while it is chosen.</summary>
    [Fact]
    public async Task LibraryPick_ChoosesTheLibraryCard_AndRemoveReturnsToInitials()
    {
        await using TeamWebApplicationFactory factory = new();
        using TempDataDir library = CreateLibraryDir();
        await using MudBunitContext ctx = NewContext(factory);
        _ = Register(ctx.Services, library.Path, new AvatarLibraryImage(MinimalPng, ".png"));
        AvatarStore avatars = factory.Services.GetRequiredService<AvatarStore>();

        IRenderedComponent<ContainerFragment> cut = RenderEditor(ctx, new Probe(AvatarSaveMode.Immediate));
        await PickFromLibraryAsync(cut);

        cut.WaitForAssertion(() => Assert.True(IsSelected(cut, LibraryCard)), TimeSpan.FromSeconds(5));
        Assert.False(Radio(cut, LibraryCard).HasAttribute("disabled"));
        Assert.True(HasButton(cut, "Remove image"));
        Assert.Empty(cut.FindAll(Swatch));
        Assert.NotNull(avatars.Get("You").Image);

        await ClickButtonAsync(cut, "Remove image");

        Assert.True(IsSelected(cut, "Initials of the name"));
        Assert.True(Radio(cut, LibraryCard).HasAttribute("disabled"));
        Assert.Null(avatars.Get("You").Image);
        Assert.NotEmpty(cut.FindAll(Swatch));
    }

    /// <summary>Asserts the label field is present with the accessible name "Avatar label" and that no visible <c>&lt;label&gt;</c> reads "Label".</summary>
    private static void AssertLabelFieldIsNamedByAriaOnly(IRenderedComponent<ContainerFragment> cut)
    {
        Assert.Equal("Avatar label", cut.Find(AgentProfileCardTestSupport.AvatarLabelInput).GetAttribute("aria-label"));
        Assert.DoesNotContain(cut.FindAll("label"), l => string.Equals(l.TextContent.Trim(), "Label", StringComparison.Ordinal));
    }

    /// <summary>The Background palette offers every swatch of <see cref="AvatarPalette"/> and no swatch is chosen until one is pressed.</summary>
    [Fact]
    public async Task Background_OffersEverySwatch_AndWritesNothingOnFirstRender()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = NewContext(factory);
        AvatarStore avatars = factory.Services.GetRequiredService<AvatarStore>();
        Probe probe = new(AvatarSaveMode.Immediate);

        IRenderedComponent<ContainerFragment> cut = RenderEditor(ctx, probe);

        Assert.Equal(Avatar.None, avatars.Get("You"));
        Assert.Empty(probe.Values);

        await SelectChoiceAsync(cut, "Initials of the name");

        Assert.Equal(AvatarPalette.Colours.Count, cut.FindAll(Swatch).Count);
    }

    /// <summary>Immediate: choosing "A short label" and typing writes the label under the Name with no Save, and reports it.</summary>
    [Fact]
    public async Task Immediate_TypingALabel_SavesUnderTheName()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = NewContext(factory);
        AvatarStore avatars = factory.Services.GetRequiredService<AvatarStore>();
        Probe probe = new(AvatarSaveMode.Immediate);

        IRenderedComponent<ContainerFragment> cut = RenderEditor(ctx, probe);
        await SelectChoiceAsync(cut, "A short label");
        await TypeLabelAsync(cut, "AB");

        Assert.Equal("AB", avatars.Get("You").Label);
        Assert.Equal("AB", probe.Values[^1].Label);
    }

    /// <summary>Immediate: the label is cut to the label limit by <see cref="Avatar.TrimLabel(string?)"/> before it is saved, and the preview shows it without a reload.</summary>
    [Fact]
    public async Task Immediate_TypedLabel_IsLimitedAndShownInThePreview()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = NewContext(factory);
        AvatarStore avatars = factory.Services.GetRequiredService<AvatarStore>();

        IRenderedComponent<ContainerFragment> cut = RenderEditor(ctx, new Probe(AvatarSaveMode.Immediate));
        await SelectChoiceAsync(cut, "A short label");
        await TypeLabelAsync(cut, "ABCDEFGH");

        Assert.Equal("ABCDEF", avatars.Get("You").Label);
        Assert.Equal("ABCDEF", cut.Find(".mud-avatar").TextContent.Trim());
    }

    /// <summary>
    /// Immediate: choosing "A short label" before typing saves nothing yet, and the parent re-passing the stored (empty) avatar
    /// on its next render must not snap the choice back to initials - the echo trap the Appearance tab already guards.
    /// </summary>
    [Fact]
    public async Task Immediate_ParentEchoOfAnEmptyAvatar_KeepsTheChosenLabelField()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = NewContext(factory);
        Probe probe = new(AvatarSaveMode.Immediate);

        IRenderedComponent<ContainerFragment> cut = RenderEditor(ctx, probe);
        await SelectChoiceAsync(cut, "A short label");
        probe.Value = Avatar.None;
        await RefreshParentAsync(cut);

        Assert.True(IsSelected(cut, "A short label"));
    }

    /// <summary>Immediate: a genuinely different <see cref="AvatarEditor.Value"/> from the parent (an outside edit) re-seeds the editor.</summary>
    [Fact]
    public async Task Immediate_ADifferentValueFromTheParent_ReseedsTheEditor()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = NewContext(factory);
        Probe probe = new(AvatarSaveMode.Immediate);

        IRenderedComponent<ContainerFragment> cut = RenderEditor(ctx, probe);
        probe.Value = new Avatar(Label: "ZZ", Image: null, Background: null);
        await RefreshParentAsync(cut);

        Assert.True(HasInputValue(cut, "ZZ"));
    }

    /// <summary>Immediate: switching back to initials clears the stored label.</summary>
    [Fact]
    public async Task Immediate_ChoosingInitials_ClearsTheStoredLabel()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = NewContext(factory);
        AvatarStore avatars = factory.Services.GetRequiredService<AvatarStore>();
        avatars.Save("You", new Avatar(Label: "AB", Image: null, Background: null));
        Probe probe = new(AvatarSaveMode.Immediate) { Value = avatars.Get("You") };

        IRenderedComponent<ContainerFragment> cut = RenderEditor(ctx, probe);
        await SelectChoiceAsync(cut, "Initials of the name");

        Assert.Null(avatars.Get("You").Label);
        Assert.Null(probe.Values[^1].Label);
    }

    /// <summary>Immediate: pressing a swatch writes that colour under the Name and keeps the label.</summary>
    [Fact]
    public async Task Immediate_PressingASwatch_SavesThatColour()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = NewContext(factory);
        AvatarStore avatars = factory.Services.GetRequiredService<AvatarStore>();
        avatars.Save("You", new Avatar(Label: "AB", Image: null, Background: null));
        Probe probe = new(AvatarSaveMode.Immediate) { Value = avatars.Get("You") };

        IRenderedComponent<ContainerFragment> cut = RenderEditor(ctx, probe);
        await cut.InvokeAsync(() => cut.FindAll(Swatch)[3].ClickAsync());

        Avatar saved = avatars.Get("You");
        Assert.Equal("AB", saved.Label);
        Assert.Equal(AvatarPalette.Colours[3].ToString(MudBlazor.Utilities.MudColorOutputFormats.Hex), saved.Background, ignoreCase: true);
    }

    /// <summary>"Use the theme colour" is offered only while a colour is set, and pressing it clears the colour back to the theme's own.</summary>
    [Fact]
    public async Task Immediate_UseTheThemeColour_ClearsAStoredColour()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext none = NewContext(factory);
        await using MudBunitContext ctx = NewContext(factory);
        AvatarStore avatars = factory.Services.GetRequiredService<AvatarStore>();
        Assert.False(HasButton(RenderEditor(none, new Probe(AvatarSaveMode.Immediate)), "Use the theme colour"));
        avatars.Save("You", new Avatar(Label: "AB", Image: null, Background: "#336699"));
        Probe probe = new(AvatarSaveMode.Immediate) { Value = avatars.Get("You") };

        IRenderedComponent<ContainerFragment> cut = RenderEditor(ctx, probe);
        await ClickButtonAsync(cut, "Use the theme colour");

        Assert.Null(avatars.Get("You").Background);
        Assert.Equal("AB", avatars.Get("You").Label);
    }

    /// <summary>Immediate: an uploaded image is written through the store at once and named in the saved avatar.</summary>
    [Fact]
    public async Task Immediate_Upload_WritesTheImageAndSaves()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = NewContext(factory);
        AvatarStore avatars = factory.Services.GetRequiredService<AvatarStore>();
        Probe probe = new(AvatarSaveMode.Immediate);

        IRenderedComponent<ContainerFragment> cut = RenderEditor(ctx, probe);
        await SelectChoiceAsync(cut, UploadCard);
        Upload(cut, MinimalPng, "me.png");

        cut.WaitForAssertion(() => Assert.NotNull(avatars.Get("You").Image), TimeSpan.FromSeconds(5));
        string stored = avatars.Get("You").Image ?? string.Empty;
        Assert.Equal(".png", Path.GetExtension(stored));
        Assert.True(File.Exists(Path.Combine(avatars.ImageDirectory, stored)));
        Assert.Empty(probe.Pending);
    }

    /// <summary>Immediate: "Remove image" clears the stored image so the avatar falls back to initials.</summary>
    [Fact]
    public async Task Immediate_RemoveImage_ClearsTheStoredImage()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = NewContext(factory);
        AvatarStore avatars = factory.Services.GetRequiredService<AvatarStore>();
        string stored = avatars.WriteImage(MinimalPng, ".png");
        avatars.Save("You", new Avatar(Label: null, Image: stored, Background: null));
        Probe probe = new(AvatarSaveMode.Immediate) { Value = avatars.Get("You") };

        IRenderedComponent<ContainerFragment> cut = RenderEditor(ctx, probe);
        await ClickButtonAsync(cut, "Remove image");

        Assert.Null(avatars.Get("You").Image);
    }

    /// <summary>An upload over <see cref="AvatarImage.MaxBytes"/> shows an alert with the size message and saves nothing.</summary>
    [Fact]
    public async Task Upload_TooLarge_ShowsAnAlertAndSavesNothing()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = NewContext(factory);
        AvatarStore avatars = factory.Services.GetRequiredService<AvatarStore>();

        IRenderedComponent<ContainerFragment> cut = RenderEditor(ctx, new Probe(AvatarSaveMode.Immediate));
        await SelectChoiceAsync(cut, UploadCard);
        Upload(cut, new byte[AvatarImage.MaxBytes + 1], "big.png");

        cut.WaitForAssertion(() => Assert.Equal("alert", cut.Find(".mud-alert").GetAttribute("role")), TimeSpan.FromSeconds(5));
        Assert.Equal(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"That file is larger than {AvatarImage.MaxBytes / 1000} KB."), cut.Find(".mud-alert").TextContent.Trim());
        Assert.Null(avatars.Get("You").Image);
    }

    /// <summary>An upload that is not a PNG, JPEG or WebP shows the type message and saves nothing.</summary>
    [Fact]
    public async Task Upload_NotAnImage_ShowsTheTypeMessage()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = NewContext(factory);
        AvatarStore avatars = factory.Services.GetRequiredService<AvatarStore>();

        IRenderedComponent<ContainerFragment> cut = RenderEditor(ctx, new Probe(AvatarSaveMode.Immediate));
        await SelectChoiceAsync(cut, UploadCard);
        Upload(cut, [1, 2, 3, 4], "notes.png");

        cut.WaitForAssertion(() => Assert.Equal("That file is not a PNG, JPEG or WebP image.", cut.Find(".mud-alert").TextContent.Trim()), TimeSpan.FromSeconds(5));
        Assert.Null(avatars.Get("You").Image);
    }

    /// <summary>Immediate: a library pick is read, written and saved as an upload is.</summary>
    [Fact]
    public async Task Immediate_LibraryPick_WritesTheImage()
    {
        await using TeamWebApplicationFactory factory = new();
        using TempDataDir library = CreateLibraryDir();
        await using MudBunitContext ctx = NewContext(factory);
        PickLoader loader = Register(ctx.Services, library.Path, new AvatarLibraryImage(MinimalPng, ".png"));
        AvatarStore avatars = factory.Services.GetRequiredService<AvatarStore>();

        IRenderedComponent<ContainerFragment> cut = RenderEditor(ctx, new Probe(AvatarSaveMode.Immediate));
        await PickFromLibraryAsync(cut);

        cut.WaitForAssertion(() => Assert.NotNull(avatars.Get("You").Image), TimeSpan.FromSeconds(5));
        Assert.Equal("av-003.png", loader.LastFileName);
    }

    /// <summary>An unreadable library pick shows the alert and saves nothing.</summary>
    [Fact]
    public async Task LibraryPick_Unreadable_ShowsTheAlert()
    {
        await using TeamWebApplicationFactory factory = new();
        using TempDataDir library = CreateLibraryDir();
        await using MudBunitContext ctx = NewContext(factory);
        _ = Register(ctx.Services, library.Path, null);
        AvatarStore avatars = factory.Services.GetRequiredService<AvatarStore>();

        IRenderedComponent<ContainerFragment> cut = RenderEditor(ctx, new Probe(AvatarSaveMode.Immediate));
        await PickFromLibraryAsync(cut);

        cut.WaitForAssertion(() => Assert.Equal("That image could not be read.", cut.Find(".mud-alert").TextContent.Trim()), TimeSpan.FromSeconds(5));
        Assert.Null(avatars.Get("You").Image);
    }

    /// <summary>The library button is offered only when a library is configured.</summary>
    [Fact]
    public async Task LibraryButton_OnlyWhenConfigured()
    {
        await using TeamWebApplicationFactory factory = new();
        using TempDataDir library = CreateLibraryDir();
        await using MudBunitContext unconfigured = NewContext(factory);
        await using MudBunitContext configured = NewContext(factory);
        _ = Register(configured.Services, library.Path, null);
        Probe image = new(AvatarSaveMode.Immediate) { Value = new Avatar(Label: null, Image: "face.png", Background: null) };

        Assert.False(HasButton(RenderEditor(unconfigured, image), ButtonText));
        Assert.True(HasButton(RenderEditor(configured, image), ButtonText));
    }

    /// <summary>Buffered: typing a label reports it and writes nothing to the store.</summary>
    [Fact]
    public async Task Buffered_TypingALabel_ReportsItAndWritesNothing()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = NewContext(factory);
        AvatarStore avatars = factory.Services.GetRequiredService<AvatarStore>();
        Probe probe = new(AvatarSaveMode.Buffered);

        IRenderedComponent<ContainerFragment> cut = RenderEditor(ctx, probe);
        await SelectChoiceAsync(cut, "A short label");
        await TypeLabelAsync(cut, "AB");

        Assert.Equal("AB", probe.Values[^1].Label);
        Assert.Equal(Avatar.None, avatars.Get("You"));
    }

    /// <summary>Buffered: pressing a swatch reports the colour and writes nothing.</summary>
    [Fact]
    public async Task Buffered_PressingASwatch_ReportsTheColourAndWritesNothing()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = NewContext(factory);
        AvatarStore avatars = factory.Services.GetRequiredService<AvatarStore>();
        Probe probe = new(AvatarSaveMode.Buffered);

        IRenderedComponent<ContainerFragment> cut = RenderEditor(ctx, probe);
        await SelectChoiceAsync(cut, "Initials of the name");
        await cut.InvokeAsync(() => cut.FindAll(Swatch)[3].ClickAsync());

        Assert.Equal(AvatarPalette.Colours[3].ToString(MudBlazor.Utilities.MudColorOutputFormats.Hex), probe.Values[^1].Background, ignoreCase: true);
        Assert.Equal(Avatar.None, avatars.Get("You"));
    }

    /// <summary>Buffered: an upload is held as pending bytes and extension, says so, and nothing is written.</summary>
    [Fact]
    public async Task Buffered_Upload_IsHeldAsPendingAndWritesNothing()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = NewContext(factory);
        AvatarStore avatars = factory.Services.GetRequiredService<AvatarStore>();
        Probe probe = new(AvatarSaveMode.Buffered);

        IRenderedComponent<ContainerFragment> cut = RenderEditor(ctx, probe);
        await SelectChoiceAsync(cut, UploadCard);
        Upload(cut, MinimalPng, "me.png");

        cut.WaitForAssertion(() => Assert.NotEmpty(probe.Pending), TimeSpan.FromSeconds(5));
        Assert.Equal(MinimalPng, probe.Pending[^1].Bytes);
        Assert.Equal(".png", probe.Pending[^1].Extension);
        Assert.True(HasText(cut, "New image selected (not yet saved)"));
        Assert.Null(avatars.Get("You").Image);
        Assert.Null(probe.Values[^1].Image);
    }

    /// <summary>Buffered: a library pick is held as pending bytes too, and nothing is written.</summary>
    [Fact]
    public async Task Buffered_LibraryPick_IsHeldAsPending()
    {
        await using TeamWebApplicationFactory factory = new();
        using TempDataDir library = CreateLibraryDir();
        await using MudBunitContext ctx = NewContext(factory);
        _ = Register(ctx.Services, library.Path, new AvatarLibraryImage(MinimalPng, ".png"));
        AvatarStore avatars = factory.Services.GetRequiredService<AvatarStore>();
        Probe probe = new(AvatarSaveMode.Buffered);

        IRenderedComponent<ContainerFragment> cut = RenderEditor(ctx, probe);
        await PickFromLibraryAsync(cut);

        cut.WaitForAssertion(() => Assert.NotEmpty(probe.Pending), TimeSpan.FromSeconds(5));
        Assert.Equal(MinimalPng, probe.Pending[^1].Bytes);
        Assert.Null(avatars.Get("You").Image);
    }

    /// <summary>Buffered: "Remove image" drops a pending image (both pending values null) without touching the store.</summary>
    [Fact]
    public async Task Buffered_RemoveImage_DropsThePendingImage()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = NewContext(factory);
        Probe probe = new(AvatarSaveMode.Buffered);

        IRenderedComponent<ContainerFragment> cut = RenderEditor(ctx, probe);
        await SelectChoiceAsync(cut, UploadCard);
        Upload(cut, MinimalPng, "me.png");
        cut.WaitForAssertion(() => Assert.True(HasButton(cut, "Remove image")), TimeSpan.FromSeconds(5));
        await ClickButtonAsync(cut, "Remove image");

        Assert.Null(probe.Pending[^1].Bytes);
        Assert.Null(probe.Pending[^1].Extension);
    }

    /// <summary>Buffered: an earlier pending image passed in starts the editor on the upload card with the not-yet-saved note.</summary>
    [Fact]
    public async Task Buffered_PendingImageParameters_SeedTheImageChoice()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = NewContext(factory);

        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<AvatarEditor>(0);
            builder.AddAttribute(1, nameof(AvatarEditor.Name), "You");
            builder.AddAttribute(2, nameof(AvatarEditor.SaveMode), AvatarSaveMode.Buffered);
            builder.AddAttribute(3, nameof(AvatarEditor.PendingImage), MinimalPng);
            builder.AddAttribute(4, nameof(AvatarEditor.PendingImageExtension), ".png");
            builder.CloseComponent();
        });

        Assert.True(HasText(cut, "New image selected (not yet saved)"));
        Assert.True(HasButton(cut, "Remove image"));
    }

    /// <summary>With <c>AllowLibrary</c> off the library card is not offered even though a library is configured, which is how a Team's editor hides it.</summary>
    [Fact]
    public async Task AllowLibraryFalse_HidesTheLibraryCard_EvenWhenALibraryIsConfigured()
    {
        await using TeamWebApplicationFactory factory = new();
        using TempDataDir library = CreateLibraryDir();
        await using MudBunitContext ctx = NewContext(factory);
        _ = Register(ctx.Services, library.Path, null);

        IRenderedComponent<ContainerFragment> cut = RenderEditor(ctx, new Probe(AvatarSaveMode.Immediate) { AllowLibrary = false });

        Assert.Equal(["Initials of the name", "A short label", UploadCard], cut.FindAll(".avatar-choice-title").Select(title => title.TextContent.Trim()));
        Assert.DoesNotContain(cut.FindAll("button"), button => button.TextContent.Contains("Choose from library", StringComparison.Ordinal));
    }

    /// <summary>The default editor draws circles: none of its previews is a rounded square.</summary>
    [Fact]
    public async Task Shape_Default_DrawsCircles()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = NewContext(factory);

        IRenderedComponent<ContainerFragment> cut = RenderEditor(ctx, new Probe(AvatarSaveMode.Immediate));

        Assert.True(cut.FindAll(".mud-avatar").Count >= 4);
        Assert.Empty(cut.FindAll(".mud-avatar.avatar-square"));
    }

    /// <summary>A rounded-square editor draws every preview, the large one and each card's thumbnail, as a rounded square.</summary>
    [Fact]
    public async Task Shape_RoundedSquare_DrawsEveryPreviewAsASquare()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = NewContext(factory);

        IRenderedComponent<ContainerFragment> cut = RenderEditor(ctx, new Probe(AvatarSaveMode.Immediate) { Shape = AvatarShape.RoundedSquare });

        int previews = cut.FindAll(".mud-avatar").Count;
        Assert.True(previews >= 4);
        Assert.Equal(previews, cut.FindAll(".mud-avatar.avatar-square").Count);
    }

    /// <summary>The Label card's field carries a smiley button that names itself for a screen reader.</summary>
    [Fact]
    public async Task EmojiButton_IsOnTheLabelField_WithAnAriaLabel()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = NewContext(factory);

        IRenderedComponent<ContainerFragment> cut = RenderEditor(ctx, new Probe(AvatarSaveMode.Immediate));

        IElement button = cut.Find("button[aria-label='Choose an emoji']");
        Assert.NotNull(button.Closest("div.mud-input-control"));
        Assert.Empty(cut.FindAll("emoji-picker"));
    }

    /// <summary>
    /// The browser's callback replaces the label with the emoji and chooses the Label card, as typing does, and the field shows it.
    /// The text goes through <c>Avatar.TrimLabel</c>, so a control character is stripped and a seventh text element is cut.
    /// </summary>
    [Fact]
    public async Task OnEmojiPicked_ReplacesTheLabel_ChoosesTheLabelCard_AndTrims()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = NewContext(factory);
        Probe probe = new(AvatarSaveMode.Immediate) { Value = new Avatar(Label: "AB", Image: null, Background: null) };
        IRenderedComponent<ContainerFragment> cut = RenderEditor(ctx, probe);
        await SelectChoiceAsync(cut, UploadCard);
        AvatarEditor editor = cut.FindComponent<AvatarEditor>().Instance;

        await cut.InvokeAsync(() => editor.OnEmojiPickedAsync("\u0007\U0001F600"));

        Assert.True(IsSelected(cut, "A short label"));
        Assert.True(HasInputValue(cut, "\U0001F600"));
        Assert.Equal(new Avatar("\U0001F600", null, null), probe.Values[^1]);

        await cut.InvokeAsync(() => editor.OnEmojiPickedAsync("1234567"));

        Assert.Equal(new Avatar("123456", null, null), probe.Values[^1]);
    }

    /// <summary>A ZWJ sequence or a skin-toned emoji counts as one text element, so the whole emoji becomes the label.</summary>
    [Fact]
    public async Task OnEmojiPicked_ZwjSequence_IsOneLabelCharacter()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = NewContext(factory);
        Probe probe = new(AvatarSaveMode.Immediate);
        IRenderedComponent<ContainerFragment> cut = RenderEditor(ctx, probe);
        AvatarEditor editor = cut.FindComponent<AvatarEditor>().Instance;
        string technologist = "\U0001F469‍\U0001F4BB";

        await cut.InvokeAsync(() => editor.OnEmojiPickedAsync(technologist));

        Assert.Equal(new Avatar(technologist, null, null), probe.Values[^1]);
        Assert.True(HasText(cut, "1 / " + Avatar.MaxLabelTextElements));
    }

    /// <summary>The counter counts text elements, the unit the limit is in: a rocket is 1 / 6 (two UTF-16 units), and plain typing still counts one per letter.</summary>
    [Fact]
    public async Task Counter_CountsTextElementsNotUtf16Units()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = NewContext(factory);
        IRenderedComponent<ContainerFragment> cut = RenderEditor(ctx, new Probe(AvatarSaveMode.Immediate));
        AvatarEditor editor = cut.FindComponent<AvatarEditor>().Instance;
        Assert.True(HasText(cut, "0 / " + Avatar.MaxLabelTextElements));

        await cut.InvokeAsync(() => editor.OnEmojiPickedAsync("\U0001F680"));

        Assert.True(HasText(cut, "1 / " + Avatar.MaxLabelTextElements));
        Assert.Single(cut.FindAll(".mud-input-control-helper-container .ms-auto"));

        await cut.InvokeAsync(() => editor.OnEmojiPickedAsync("AB\U0001F469\u200D\U0001F4BB"));

        Assert.True(HasText(cut, "3 / " + Avatar.MaxLabelTextElements));
    }

    /// <summary>Pressing the button opens the popover with the picker pointed at the local data file, attaches the browser side, and a pick closes the popover and detaches it.</summary>
    [Fact]
    public async Task EmojiButton_OpensThePicker_AndAPickClosesIt()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = NewContext(factory);
        BunitJSModuleInterop module = ctx.JSInterop.SetupModule("./emoji-picker.js");
        BunitJSModuleInterop handle = module.SetupModule("attach", _ => true);
        handle.SetupVoid("dispose").SetVoidResult();
        Probe probe = new(AvatarSaveMode.Immediate);
        IRenderedComponent<ContainerFragment> cut = RenderEditor(ctx, probe);

        await cut.InvokeAsync(() => cut.Find("button[aria-label='Choose an emoji']").ClickAsync());

        IElement picker = cut.WaitForElement("emoji-picker");
        Assert.Equal("/lib/emoji-picker/data.json", picker.GetAttribute("data-source"));
        cut.WaitForAssertion(() => Assert.Equal("avatar-emoji-picker", Assert.Single(module.Invocations, call => call.Identifier == "attach").Arguments[0]));

        await cut.InvokeAsync(() => cut.FindComponent<AvatarEditor>().Instance.OnEmojiPickedAsync("\U0001F680"));

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("emoji-picker")));
        Assert.Equal(new Avatar("\U0001F680", null, null), probe.Values[^1]);
        handle.VerifyInvoke("dispose");
        ctx.JSInterop.VerifyInvoke(FocusCall);
    }

    /// <summary>Escape inside the picker closes the popover and leaves the label alone.</summary>
    [Fact]
    public async Task OnEmojiClosed_ClosesThePickerWithoutChangingTheLabel()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = NewContext(factory);
        ctx.JSInterop.SetupModule("./emoji-picker.js").SetupModule("attach", _ => true).SetupVoid("dispose").SetVoidResult();
        Probe probe = new(AvatarSaveMode.Immediate) { Value = new Avatar(Label: "AB", Image: null, Background: null) };
        IRenderedComponent<ContainerFragment> cut = RenderEditor(ctx, probe);
        await cut.InvokeAsync(() => cut.Find("button[aria-label='Choose an emoji']").ClickAsync());
        cut.WaitForElement("emoji-picker");

        await cut.InvokeAsync(() => cut.FindComponent<AvatarEditor>().Instance.OnEmojiClosedAsync());

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("emoji-picker")));
        Assert.True(HasInputValue(cut, "AB"));
        ctx.JSInterop.VerifyInvoke(FocusCall);
    }

    /// <summary>Disposing the editor while the picker is open releases the browser handle and the module.</summary>
    [Fact]
    public async Task Dispose_WhileThePickerIsOpen_ReleasesTheHandleAndTheModule()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = NewContext(factory);
        BunitJSModuleInterop module = ctx.JSInterop.SetupModule("./emoji-picker.js");
        BunitJSModuleInterop handle = module.SetupModule("attach", _ => true);
        handle.SetupVoid("dispose").SetVoidResult();
        IRenderedComponent<ContainerFragment> cut = RenderEditor(ctx, new Probe(AvatarSaveMode.Immediate));
        await cut.InvokeAsync(() => cut.Find("button[aria-label='Choose an emoji']").ClickAsync());
        cut.WaitForAssertion(() => Assert.Single(module.Invocations, call => call.Identifier == "attach"));

        await cut.InvokeAsync(() => cut.FindComponent<AvatarEditor>().Instance.DisposeAsync().AsTask());

        handle.VerifyInvoke("dispose");
    }

    /// <summary>Closing a picker that was never opened does not move focus: only a close that took focus from the field gives it back.</summary>
    [Fact]
    public async Task OnEmojiClosed_WhenThePickerIsNotOpen_DoesNotTouchFocus()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = NewContext(factory);
        IRenderedComponent<ContainerFragment> cut = RenderEditor(ctx, new Probe(AvatarSaveMode.Immediate));

        await cut.InvokeAsync(() => cut.FindComponent<AvatarEditor>().Instance.OnEmojiClosedAsync());

        Assert.DoesNotContain(ctx.JSInterop.Invocations, call => call.Identifier == FocusCall);
    }

    /// <summary>The popover is told to flip to the side with room, so it can sit above or below the field inside a short window.</summary>
    [Fact]
    public async Task EmojiPopover_FlipsToTheSideWithRoom()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = NewContext(factory);
        IRenderedComponent<ContainerFragment> cut = RenderEditor(ctx, new Probe(AvatarSaveMode.Immediate));

        MudBlazor.MudPopover popover = cut.FindComponent<MudBlazor.MudPopover>().Instance;

        Assert.Equal(MudBlazor.OverflowBehavior.FlipAlways, popover.OverflowBehavior);
    }
}
