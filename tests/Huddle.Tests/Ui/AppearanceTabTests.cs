using Bunit;
using Bunit.Rendering;
using Microsoft.Extensions.DependencyInjection;
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
    }

    /// <summary>A context with the one store the Appearance tab injects.</summary>
    /// <param name="factory">The factory whose store backs it.</param>
    private static MudBunitContext NewContext(TeamWebApplicationFactory factory)
    {
        MudBunitContext ctx = new();
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<AppearanceStore>());
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
