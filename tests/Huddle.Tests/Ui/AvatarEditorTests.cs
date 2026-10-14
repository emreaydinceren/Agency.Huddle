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
/// Renders <see cref="AvatarEditor"/> on its own, in both save modes. The component is the radio group (initials, a short
/// label, an image), the label field with its counter, the upload and library pick, and the background swatches; its
/// <see cref="AvatarEditor.SaveMode"/> decides whether each change is written to the <see cref="AvatarStore"/> under
/// <see cref="AvatarEditor.Name"/> (Immediate, what Settings uses) or only reported through
/// <see cref="AvatarEditor.ValueChanged"/> and <see cref="AvatarEditor.PendingImageChanged"/> (Buffered, what the
/// Teammate card will use).
/// </summary>
public sealed class AvatarEditorTests
{
    /// <summary>The CSS selector of one swatch in the Background palette.</summary>
    private const string Swatch = ".avatar-editor-background .mud-picker-color-dot";

    /// <summary>What the editor reported, and the parameters a parent would pass on its next render.</summary>
    private sealed class Probe(AvatarSaveMode mode)
    {
        /// <summary>The save mode the editor is rendered in.</summary>
        public AvatarSaveMode Mode { get; } = mode;

        /// <summary>The avatar the parent passes down.</summary>
        public Avatar Value { get; set; } = Avatar.None;

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

    /// <summary>Clicks the radio whose own text is <paramref name="choiceLabel"/>, on its inner input as the Teammate card tests do.</summary>
    private static Task SelectChoiceAsync(IRenderedComponent<ContainerFragment> cut, string choiceLabel) =>
        cut.InvokeAsync(() =>
        {
            IElement radio = cut.FindAll(".mud-radio").First(element => element.TextContent.Contains(choiceLabel, StringComparison.Ordinal));
            return (radio.QuerySelector("input.mud-radio-input") ?? throw new InvalidOperationException($"No radio input under '{choiceLabel}'.")).ClickAsync();
        });

    /// <summary>Types <paramref name="value"/> into the Label field by raising its <c>oninput</c>.</summary>
    private static Task TypeLabelAsync(IRenderedComponent<ContainerFragment> cut, string value) =>
        cut.InvokeAsync(() =>
        {
            IElement control = cut.FindAll("div.mud-input-control")
                .First(c => c.QuerySelectorAll("label").Any(l => l.TextContent.Contains("Label", StringComparison.Ordinal)));
            return (control.QuerySelector("input") ?? throw new InvalidOperationException("No Label input.")).InputAsync(value);
        });

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
        // Click(), not ClickAsync(): the handler awaits the dialog's result, so awaiting it here would wait for the pick that comes next.
        await cut.InvokeAsync(() => cut.FindAll("button").First(button => string.Equals(button.TextContent.Trim(), ButtonText, StringComparison.Ordinal)).Click());
        cut.WaitForAssertion(() => Assert.True(cut.FindAll(Thumb).Count > 3), TimeSpan.FromSeconds(5));
        await cut.InvokeAsync(() => cut.FindAll(Thumb)[3].Click());
    }

    /// <summary>With nothing stored the editor offers the three choices, starts on "An image" with its controls and no Label field, and shows no fields for the initials.</summary>
    [Fact]
    public async Task Renders_ThreeChoices_AndNoFieldsForInitials()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = NewContext(factory);

        IRenderedComponent<ContainerFragment> cut = RenderEditor(ctx, new Probe(AvatarSaveMode.Immediate));

        Assert.Equal(["Initials of the name", "A short label", "An image"], cut.FindAll(".mud-radio").Select(radio => radio.TextContent.Trim()));
        Assert.False(HasText(cut, "Shown instead of initials."));
        Assert.True(HasButton(cut, "Choose an image"));

        await SelectChoiceAsync(cut, "Initials of the name");

        Assert.False(HasButton(cut, "Choose an image"));
    }

    /// <summary>A stored label starts the editor on "A short label" with the text in the field and a counter sized from the label limit.</summary>
    [Fact]
    public async Task Value_WithLabel_SeedsTheLabelFieldAndCounter()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = NewContext(factory);
        Probe probe = new(AvatarSaveMode.Immediate) { Value = new Avatar(Label: "AB", Image: null, Background: null) };

        IRenderedComponent<ContainerFragment> cut = RenderEditor(ctx, probe);

        Assert.True(HasText(cut, "Shown instead of initials."));
        Assert.True(HasInputValue(cut, "AB"));
        Assert.True(HasText(cut, "2 / " + Avatar.MaxLabelTextElements));
    }

    /// <summary>A stored image starts the editor on "An image", with the file name and a way to remove it, and no background swatches (the image covers the colour).</summary>
    [Fact]
    public async Task Value_WithImage_SeedsTheImageControlsAndHidesTheSwatches()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = NewContext(factory);
        Probe probe = new(AvatarSaveMode.Immediate) { Value = new Avatar(Label: null, Image: "face.png", Background: null) };

        IRenderedComponent<ContainerFragment> cut = RenderEditor(ctx, probe);

        Assert.True(HasButton(cut, "Choose an image"));
        Assert.True(HasButton(cut, "Remove image"));
        Assert.True(HasText(cut, "face.png"));
        Assert.Empty(cut.FindAll(Swatch));
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
        await TypeLabelAsync(cut, "ABCDE");

        Assert.Equal("ABC", avatars.Get("You").Label);
        Assert.Equal("ABC", cut.Find(".mud-avatar").TextContent.Trim());
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

        Assert.True(HasText(cut, "Shown instead of initials."));
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
        await SelectChoiceAsync(cut, "An image");
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
        await SelectChoiceAsync(cut, "An image");
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
        await SelectChoiceAsync(cut, "An image");
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
        await SelectChoiceAsync(cut, "An image");
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
        await SelectChoiceAsync(cut, "An image");
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
        await SelectChoiceAsync(cut, "An image");
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
        await SelectChoiceAsync(cut, "An image");
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
        await SelectChoiceAsync(cut, "An image");
        Upload(cut, MinimalPng, "me.png");
        cut.WaitForAssertion(() => Assert.True(HasButton(cut, "Remove image")), TimeSpan.FromSeconds(5));
        await ClickButtonAsync(cut, "Remove image");

        Assert.Null(probe.Pending[^1].Bytes);
        Assert.Null(probe.Pending[^1].Extension);
    }

    /// <summary>Buffered: an earlier pending image passed in starts the editor on "An image" with the not-yet-saved note.</summary>
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
}
