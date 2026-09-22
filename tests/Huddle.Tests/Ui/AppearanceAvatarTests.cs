using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Appearance;
using Agency.Huddle.App.Avatars;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Tests for Task 5.2's second section of the Appearance tab: the Human's own Avatar editor.
/// Renders <see cref="Agency.Huddle.App.Components.Settings.Appearance"/> directly through
/// <see cref="MudBunitContext"/>, the same pattern <c>TeammateCardTests</c> uses for
/// <see cref="Agency.Huddle.App.Components.Shared.TeammateCard"/> -
/// this component's editor needs a real click and a real keystroke to prove the preview and the save
/// both work, which a plain HTTP GET (as <c>AppearanceRenderingTests</c> and most of
/// <c>SettingsPageTests</c>'s Appearance-tab tests use) cannot exercise. <c>RenderWithPopovers</c> is
/// used rather than the plain generic <c>Render</c>, matching <c>TeammateCardTests</c>' own reasoning
/// for a component holding a <c>MudColorPicker</c>.
/// </summary>
public sealed class AppearanceAvatarTests
{
    /// <summary>Registers this factory's real stores into a fresh <see cref="MudBunitContext"/>, the same pattern <c>SettingsPageTests.NewContext</c> and <c>TeammateCardTests.NewContext</c> use.</summary>
    private static MudBunitContext NewContext(TeamWebApplicationFactory factory)
    {
        MudBunitContext ctx = new();
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<AppearanceStore>());
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<AvatarStore>());
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<IOptions<TeamOptions>>());
        return ctx;
    }

    private static IRenderedComponent<ContainerFragment> RenderPage(MudBunitContext ctx)
    {
        return ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<Agency.Huddle.App.Components.Settings.Appearance>(0);
            builder.CloseComponent();
        });
    }

    /// <summary>The Appearance tab renders the avatar section's three-way choice alongside the existing Theme picker.</summary>
    [Fact]
    public async Task AppearanceTab_Renders_AvatarControls()
    {
        await using var factory = new TeamWebApplicationFactory();
        await using var ctx = NewContext(factory);

        var cut = RenderPage(ctx);

        Assert.Contains("Initials of the name", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("A short label", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("An image", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Theme\"", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// Choosing "A short label" and typing writes the Human's key ("You", the factory's configured
    /// <c>Team:HumanName</c>) into <c>avatars.json</c> with no Save button clicked - the whole point
    /// of this tab's "no Save button, so every change saves immediately" design.
    /// </summary>
    [Fact]
    public async Task AppearanceTab_ChoosingLabelAndTyping_SavesUnderTheHumanNameKey()
    {
        await using var factory = new TeamWebApplicationFactory();
        await using var ctx = NewContext(factory);
        var avatars = factory.Services.GetRequiredService<AvatarStore>();

        var cut = RenderPage(ctx);
        SelectAvatarChoice(cut, "A short label");
        SetImmediateTextValue(cut, "Label", "AB");

        var saved = avatars.Get("You");
        Assert.Equal("AB", saved.Label);

        var json = await File.ReadAllTextAsync(avatars.FilePath, Xunit.TestContext.Current.CancellationToken);
        Assert.Contains("\"You\"", json, StringComparison.Ordinal);
        Assert.Contains("\"AB\"", json, StringComparison.Ordinal);
    }

    /// <summary>The typed label reflects in the live preview <see cref="Agency.Huddle.App.Components.Shared.TeammateAvatar"/> renders, with no reload - the same "same open surface, local field" proof <c>TeammateCardTests</c> gives its own preview.</summary>
    [Fact]
    public async Task AppearanceTab_TypedLabel_ReflectsInThePreviewWithoutReload()
    {
        await using var factory = new TeamWebApplicationFactory();
        await using var ctx = NewContext(factory);

        var cut = RenderPage(ctx);
        SelectAvatarChoice(cut, "A short label");
        SetImmediateTextValue(cut, "Label", "AB");

        Assert.Equal("AB", cut.Find(".mud-avatar").TextContent.Trim());
    }

    /// <summary>The existing Theme picker still renders and still saves a choice - proof that adding the avatar section did not disturb it.</summary>
    [Fact]
    public async Task AppearanceTab_ThemePicker_StillSavesAChoice()
    {
        await using var factory = new TeamWebApplicationFactory();
        await using var ctx = NewContext(factory);
        var appearance = factory.Services.GetRequiredService<AppearanceStore>();

        var cut = RenderPage(ctx);
        cut.FindAll(".theme-picker .mud-list-item")
            .First(item => item.TextContent.Contains("Huddle Dark", StringComparison.Ordinal))
            .Click();

        Assert.Equal("huddle-dark", appearance.Current.ThemeId);
    }

    /// <summary>An upload too large for <see cref="AvatarImage.MaxBytes"/> sets an error and never reaches <see cref="AvatarStore.WriteImage(ReadOnlySpan{byte}, string)"/>.</summary>
    [Fact]
    public async Task AppearanceTab_OversizedImage_SetsAnErrorAndDoesNotSave()
    {
        await using var factory = new TeamWebApplicationFactory();
        await using var ctx = NewContext(factory);
        var avatars = factory.Services.GetRequiredService<AvatarStore>();

        var cut = RenderPage(ctx);
        SelectAvatarChoice(cut, "An image");

        var oversized = new byte[AvatarImage.MaxBytes + 1];
        var inputFile = cut.FindComponent<InputFile>();
        inputFile.UploadFiles(InputFileContent.CreateFromBinary(oversized, "big.png", contentType: "image/png"));

        Assert.Contains("larger than", cut.Markup, StringComparison.Ordinal);
        Assert.Null(avatars.Get("You").Image);
    }

    /// <summary>
    /// Clicks the <c>MudRadio</c> whose own text is <paramref name="choiceLabel"/>, inside the avatar
    /// editor's <c>MudRadioGroup</c> - mirrors <c>TeammateCardTests.SelectAvatarChoice</c> exactly,
    /// including clicking the inner <c>input.mud-radio-input</c> rather than the outer label.
    /// </summary>
    /// <param name="cut">The rendered page holding the Appearance tab.</param>
    /// <param name="choiceLabel">The radio's visible text - "Initials of the name", "A short label" or "An image".</param>
    private static void SelectAvatarChoice(IRenderedComponent<ContainerFragment> cut, string choiceLabel)
    {
        var radio = cut.FindAll(".mud-radio").First(element => element.TextContent.Contains(choiceLabel, StringComparison.Ordinal));
        (radio.QuerySelector("input.mud-radio-input") ?? throw new InvalidOperationException($"No radio input under '{choiceLabel}'.")).Click();
    }

    /// <summary>Sets an <c>Immediate="true"</c> text field's value by raising <c>@oninput</c> - mirrors <c>TeammateCardTests.SetImmediateTextValue</c>.</summary>
    /// <param name="cut">The rendered page holding the Appearance tab.</param>
    /// <param name="label">The control's label text.</param>
    /// <param name="value">The text to type.</param>
    private static void SetImmediateTextValue(IRenderedComponent<ContainerFragment> cut, string label, string value)
    {
        var control = cut.FindAll("div.mud-input-control")
            .First(c => c.QuerySelectorAll("label").Any(l => l.TextContent.Contains(label, StringComparison.Ordinal)));
        var input = control.QuerySelector("input") ?? throw new InvalidOperationException($"No input under '{label}'.");
        input.Input(value);
    }
}
