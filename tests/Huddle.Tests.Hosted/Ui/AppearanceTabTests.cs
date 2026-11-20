using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MudBlazor;
using Agency.Huddle.App.Appearance;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// The Appearance tab after the Human's avatar moved to the Profile tab: the theme picker still saves a
/// choice, and the tab no longer carries an avatar editor of its own, only a link to Profile.
/// </summary>
public sealed class AppearanceTabTests
{
    /// <summary>The theme picker still renders and still saves a choice.</summary>
    [Fact]
    public async Task ThemePicker_SavesAChoice()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = NewContext(factory);
        AppearanceStore appearance = factory.Services.GetRequiredService<AppearanceStore>();

        IRenderedComponent<ContainerFragment> cut = RenderTab(ctx);
        await cut.InvokeAsync(() => cut.FindAll(".theme-picker .mud-list-item")
            .First(item => item.TextContent.Contains("Nordic Dark", StringComparison.Ordinal))
            .ClickAsync());

        Assert.Equal("nordic-dark", appearance.Current.DarkThemeId);
    }

    /// <summary>The avatar editor is gone from Appearance; the tab points to the Profile tab instead.</summary>
    [Fact]
    public async Task AvatarEditor_IsNotHere_AndTheTabLinksToProfile()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = NewContext(factory);

        IRenderedComponent<ContainerFragment> cut = RenderTab(ctx);

        Assert.DoesNotContain("Initials of the name", cut.Markup, StringComparison.Ordinal);
        Assert.Single(cut.FindAll("a[href='/settings/profile']"));
        Assert.Single(cut.FindAll(".settings-intro a[href='/settings/profile']"));
    }

    /// <summary>The chosen Theme of each list carries a check as a second cue besides the tint, and no other row does (#736).</summary>
    [Fact]
    public async Task ThemePicker_MarksTheChosenRowOfEachListWithACheck()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = NewContext(factory);

        IRenderedComponent<ContainerFragment> cut = RenderTab(ctx);

        Assert.Single(cut.FindAll("[aria-label='Light theme'] .theme-row-check"));
        Assert.Single(cut.FindAll("[aria-label='Dark theme'] .theme-row-check"));
    }

    /// <summary>The Accent field says "Theme default" while no accent is set (#736).</summary>
    [Fact]
    public async Task AccentField_ShowsThemeDefaultAsItsPlaceholder()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = NewContext(factory);

        IRenderedComponent<ContainerFragment> cut = RenderTab(ctx);

        Assert.Equal("Theme default", cut.Find(".theme-picker-accent-colour input").GetAttribute("placeholder"));
    }

    /// <summary>The Light and Dark theme headings are <c>h3</c> sub-sections under the tab's <c>h2</c> title (#473), and the accent field is outlined, not underlined (#480).</summary>
    [Fact]
    public async Task Tab_SubSectionsAreH3_AndTheAccentFieldIsOutlined()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = NewContext(factory);

        IRenderedComponent<ContainerFragment> cut = RenderTab(ctx);

        Assert.Equal(["Light theme", "Dark theme"], cut.FindAll("h3").Select(heading => heading.TextContent.Trim()));
        Assert.Empty(cut.FindAll("h2"));
        Assert.NotEmpty(cut.FindAll(".theme-picker-accent-colour .mud-input-outlined"));
        Assert.Empty(cut.FindAll(".theme-picker-accent-colour .mud-input-underline"));
    }

    /// <summary>The picker lists four light and four dark Themes, and the retired Custom Accent Themes are gone from the tab (P5-D4).</summary>
    [Fact]
    public async Task ThemePicker_ListsFourLightAndFourDarkThemes_AndNoCustomAccent()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = NewContext(factory);

        IRenderedComponent<ContainerFragment> cut = RenderTab(ctx);

        Assert.Equal(4, cut.FindAll("[aria-label='Light theme'] .mud-list-item").Count);
        Assert.Equal(4, cut.FindAll("[aria-label='Dark theme'] .mud-list-item").Count);
        Assert.DoesNotContain("Custom Accent", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Huddle Light", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>The accent control is always there and is labelled for the shown Theme (P5-D2), not shown only for a special Theme.</summary>
    [Fact]
    public async Task AccentControl_IsLabelledForTheShownTheme()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = NewContext(factory);
        AppearanceStore appearance = factory.Services.GetRequiredService<AppearanceStore>();

        IRenderedComponent<ContainerFragment> cut = RenderTab(ctx);
        Assert.Contains("Accent for Lumen Light", cut.Markup, StringComparison.Ordinal);

        appearance.Save("nordic-light");
        cut.WaitForAssertion(() => Assert.Contains("Accent for Nordic Light", cut.Markup, StringComparison.Ordinal));
    }

    /// <summary>Picking a colour writes <c>customizations[shown id].palette.primary</c> and the old <c>accentColor</c> key is never written.</summary>
    [Fact]
    public async Task AccentControl_WritesThePrimaryUnderTheShownThemesCustomization()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = NewContext(factory);
        AppearanceStore appearance = factory.Services.GetRequiredService<AppearanceStore>();
        appearance.Save("nordic-dark");
        IRenderedComponent<ContainerFragment> cut = RenderTab(ctx);

        IRenderedComponent<MudColorPicker> picker = cut.FindComponent<MudColorPicker>();
        await cut.InvokeAsync(() => picker.Instance.TextChanged.InvokeAsync("#00b294"));

        Assert.Equal("#00b294", appearance.Current.CustomizationFor("nordic-dark")?.Palette["primary"]);
        Assert.Null(appearance.Current.CustomizationFor("lumen-light"));
        Assert.DoesNotContain("accentColor", File.ReadAllText(appearance.FilePath), StringComparison.Ordinal);
    }

    /// <summary>The picker shows the stored colour for the shown Theme, and blanking it clears the colour.</summary>
    [Fact]
    public async Task AccentControl_ShowsTheStoredColour_AndBlankClearsIt()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = NewContext(factory);
        AppearanceStore appearance = factory.Services.GetRequiredService<AppearanceStore>();
        appearance.SaveAccentColor("lumen-light", "#00b294");
        IRenderedComponent<ContainerFragment> cut = RenderTab(ctx);
        IRenderedComponent<MudColorPicker> picker = cut.FindComponent<MudColorPicker>();
        Assert.Equal("#00b294", cut.Find(".theme-picker-accent-colour input").GetAttribute("value"));

        await cut.InvokeAsync(() => picker.Instance.TextChanged.InvokeAsync(" "));

        Assert.Null(appearance.Current.CustomizationFor("lumen-light"));
    }

    /// <summary>Reset appears only when the shown Theme has a customization, and removes its whole entry (P5-D2).</summary>
    [Fact]
    public async Task AccentReset_AppearsOnlyWithACustomization_AndRemovesTheEntry()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = NewContext(factory);
        AppearanceStore appearance = factory.Services.GetRequiredService<AppearanceStore>();
        IRenderedComponent<ContainerFragment> cut = RenderTab(ctx);
        Assert.Empty(cut.FindAll(".theme-accent-reset"));

        appearance.SaveAccentColor("lumen-light", "#00b294");
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".theme-accent-reset")));
        await cut.InvokeAsync(() => cut.Find(".theme-accent-reset").ClickAsync());

        Assert.Null(appearance.Current.CustomizationFor("lumen-light"));
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".theme-accent-reset")));
    }

    /// <summary>Reset removes the customization of the shown Theme only.</summary>
    [Fact]
    public async Task AccentReset_LeavesOtherThemesCustomizationsAlone()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = NewContext(factory);
        AppearanceStore appearance = factory.Services.GetRequiredService<AppearanceStore>();
        appearance.SaveAccentColor("lumen-light", "#00b294");
        appearance.SaveAccentColor("nordic-light", "#b20094");
        IRenderedComponent<ContainerFragment> cut = RenderTab(ctx);

        await cut.InvokeAsync(() => cut.Find(".theme-accent-reset").ClickAsync());

        Assert.Null(appearance.Current.CustomizationFor("lumen-light"));
        Assert.Equal("#b20094", appearance.Current.CustomizationFor("nordic-light")?.Palette["primary"]);
    }

    /// <summary>A Theme's swatch shows the Accent it will actually render with, customization included.</summary>
    [Fact]
    public async Task ThemeSwatch_ShowsTheCustomizedAccent()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = NewContext(factory);
        AppearanceStore appearance = factory.Services.GetRequiredService<AppearanceStore>();
        IRenderedComponent<ContainerFragment> cut = RenderTab(ctx);
        string plain = cut.FindAll("[aria-label='Light theme'] .mud-list-item")[0].InnerHtml;

        appearance.SaveAccentColor("lumen-light", "#00b294");

        cut.WaitForAssertion(() => Assert.NotEqual(plain, cut.FindAll("[aria-label='Light theme'] .mud-list-item")[0].InnerHtml));
    }

    /// <summary>In Development the tab links to the Theme lab.</summary>
    [Fact]
    public async Task ThemeLabLink_ShownInDevelopment()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = NewContext(factory, Environments.Development);

        IRenderedComponent<ContainerFragment> cut = RenderTab(ctx);

        AngleSharp.Dom.IElement link = cut.Find("a.theme-lab-link");
        Assert.Equal("/settings/theme-lab", link.GetAttribute("href"));
        Assert.Equal("Open the Theme lab", link.TextContent.Trim());
    }

    /// <summary>Outside Development the tab does not offer the Theme lab, a development tool.</summary>
    [Fact]
    public async Task ThemeLabLink_HiddenOutsideDevelopment()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = NewContext(factory, Environments.Production);

        IRenderedComponent<ContainerFragment> cut = RenderTab(ctx);

        Assert.Empty(cut.FindAll("a.theme-lab-link"));
    }

    /// <summary>A context with the store and the hosting environment the Appearance tab injects.</summary>
    /// <param name="factory">The factory whose store backs it.</param>
    /// <param name="environmentName">The hosting environment the tab sees; Development unless a test says otherwise.</param>
    private static MudBunitContext NewContext(TeamWebApplicationFactory factory, string environmentName = "Development")
    {
        MudBunitContext ctx = new();
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<AppearanceStore>());
        IWebHostEnvironment environment = factory.Services.GetRequiredService<IWebHostEnvironment>();
        environment.EnvironmentName = environmentName;
        ctx.Services.AddSingleton(environment);
        return ctx;
    }

    /// <summary>Renders the Appearance tab inside the popover providers its colour picker needs.</summary>
    /// <param name="ctx">The context.</param>
    private static IRenderedComponent<ContainerFragment> RenderTab(MudBunitContext ctx) =>
        ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<Agency.Huddle.App.Components.Settings.Appearance>(0);
            builder.CloseComponent();
        });
}
