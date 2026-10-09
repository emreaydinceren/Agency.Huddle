using Bunit;
using Bunit.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Appearance;
using Agency.Huddle.App.Avatars;
using static Agency.Huddle.Tests.Ui.AvatarLibraryPickSupport;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// The "Choose from library" button on the Appearance tab, which edits the Human's own Avatar and saves at once
/// (Avatar Library Spec §6.6 and §6.7).
/// </summary>
public sealed class AppearanceLibraryTests
{
    private static MudBunitContext NewContext(TeamWebApplicationFactory factory)
    {
        MudBunitContext ctx = new();
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<AppearanceStore>());
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<AvatarStore>());
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<IOptions<TeamOptions>>());
        RegisterUnconfigured(ctx.Services);
        return ctx;
    }

    private static IRenderedComponent<ContainerFragment> RenderPage(MudBunitContext ctx) =>
        ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<Agency.Huddle.App.Components.Settings.Appearance>(0);
            builder.CloseComponent();
        });

    private static Task ChooseImageAsync(IRenderedComponent<ContainerFragment> cut) =>
        cut.InvokeAsync(() =>
        {
            AngleSharp.Dom.IElement radio = cut.FindAll(".mud-radio").First(element => element.TextContent.Contains("An image", StringComparison.Ordinal));
            return (radio.QuerySelector("input.mud-radio-input") ?? throw new InvalidOperationException("No radio input.")).ClickAsync();
        });

    private static AngleSharp.Dom.IElement? FindLibraryButton(IRenderedComponent<ContainerFragment> cut) =>
        cut.FindAll("button").FirstOrDefault(button => string.Equals(button.TextContent.Trim(), ButtonText, StringComparison.Ordinal));

    /// <summary>Picking a thumbnail writes the image through the store and saves the Human's Avatar with no further click.</summary>
    [Fact]
    public async Task Appearance_LibraryPick_WritesImmediately()
    {
        await using TeamWebApplicationFactory factory = new();
        using TempDataDir library = CreateLibraryDir();
        await using MudBunitContext ctx = NewContext(factory);
        PickLoader loader = Register(ctx.Services, library.Path, new AvatarLibraryImage(MinimalPng, ".png"));
        AvatarStore avatars = factory.Services.GetRequiredService<AvatarStore>();

        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx);
        await ChooseImageAsync(cut);
        await cut.InvokeAsync(() => (FindLibraryButton(cut) ?? throw new InvalidOperationException("No library button.")).Click());
        cut.WaitForAssertion(() => Assert.True(cut.FindAll(Thumb).Count > 3), TimeSpan.FromSeconds(5));
        await cut.InvokeAsync(() => cut.FindAll(Thumb)[3].Click());

        cut.WaitForAssertion(() => Assert.NotNull(avatars.Get("You").Image), TimeSpan.FromSeconds(5));
        Assert.Equal("av-003.png", loader.LastFileName);
        Assert.EndsWith(".png", avatars.Get("You").Image, StringComparison.Ordinal);
    }

    /// <summary>A pick whose file cannot be read shows the page's avatar error line and saves no image.</summary>
    [Fact]
    public async Task Appearance_LibraryPick_Unreadable_ShowsAvatarError()
    {
        await using TeamWebApplicationFactory factory = new();
        using TempDataDir library = CreateLibraryDir();
        await using MudBunitContext ctx = NewContext(factory);
        _ = Register(ctx.Services, library.Path, null);
        AvatarStore avatars = factory.Services.GetRequiredService<AvatarStore>();

        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx);
        await ChooseImageAsync(cut);
        await cut.InvokeAsync(() => (FindLibraryButton(cut) ?? throw new InvalidOperationException("No library button.")).Click());
        cut.WaitForAssertion(() => Assert.True(cut.FindAll(Thumb).Count > 3), TimeSpan.FromSeconds(5));
        await cut.InvokeAsync(() => cut.FindAll(Thumb)[3].Click());

        cut.WaitForAssertion(() => Assert.Equal("That image could not be read.", cut.Find(".mud-alert").TextContent.Trim()), TimeSpan.FromSeconds(5));
        Assert.Null(avatars.Get("You").Image);
    }

    /// <summary>With no library configured the Image choice offers no library button.</summary>
    [Fact]
    public async Task Appearance_LibraryButton_Hidden_WhenNotConfigured()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = NewContext(factory);

        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx);
        await ChooseImageAsync(cut);

        Assert.Null(FindLibraryButton(cut));
        Assert.Contains("Choose an image", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>With a configured library the button shows under the Image choice and not under Initials.</summary>
    [Fact]
    public async Task Appearance_LibraryButton_Shown_OnlyUnderImageChoice_WhenConfigured()
    {
        await using TeamWebApplicationFactory factory = new();
        using TempDataDir library = CreateLibraryDir();
        await using MudBunitContext ctx = NewContext(factory);
        _ = Register(ctx.Services, library.Path, null);

        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx);
        Assert.Null(FindLibraryButton(cut));

        await ChooseImageAsync(cut);
        Assert.NotNull(FindLibraryButton(cut));
    }
}
