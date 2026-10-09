using Bunit;
using Bunit.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Appearance;
using Agency.Huddle.App.Avatars;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// The file watcher raises <see cref="AvatarStore.AvatarsChanged"/> a second time for the Appearance tab's own write.
/// That echo must not re-derive the avatar choice from a store that holds nothing yet, while a genuinely different
/// external edit must still re-seed the editor.
/// </summary>
public sealed class AppearanceAvatarEchoTests
{
    private static MudBunitContext NewContext(TeamWebApplicationFactory factory)
    {
        MudBunitContext ctx = new();
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<AppearanceStore>());
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<AvatarStore>());
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<IOptions<TeamOptions>>());
        AvatarLibraryPickSupport.RegisterUnconfigured(ctx.Services);
        return ctx;
    }

    private static IRenderedComponent<ContainerFragment> RenderPage(MudBunitContext ctx) =>
        ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<Agency.Huddle.App.Components.Settings.Appearance>(0);
            builder.CloseComponent();
        });

    private static Task ChooseAsync(IRenderedComponent<ContainerFragment> cut, string choiceLabel) =>
        cut.InvokeAsync(() =>
        {
            AngleSharp.Dom.IElement radio = cut.FindAll(".mud-radio").First(element => element.TextContent.Contains(choiceLabel, StringComparison.Ordinal));
            return (radio.QuerySelector("input.mud-radio-input") ?? throw new InvalidOperationException("No radio input.")).ClickAsync();
        });

    /// <summary>After "An image" is chosen with nothing stored behind it, an equal echo from the store keeps the Image choice and its buttons.</summary>
    [Fact]
    public async Task Echo_AfterChoosingImageWithNothingStored_KeepsImageChoice()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = NewContext(factory);
        AvatarStore avatars = factory.Services.GetRequiredService<AvatarStore>();

        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx);
        await ChooseAsync(cut, "An image");
        avatars.Save("You", Avatar.None);
        await cut.InvokeAsync(() => Task.CompletedTask);
        await Task.Delay(300, Xunit.TestContext.Current.CancellationToken);

        Assert.Contains(cut.FindAll("button"), b => string.Equals(b.TextContent.Trim(), "Choose an image", StringComparison.Ordinal));
    }

    /// <summary>After "A short label" is chosen with nothing stored behind it, an equal echo keeps the Label box.</summary>
    [Fact]
    public async Task Echo_AfterChoosingLabelWithNothingStored_KeepsLabelChoice()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = NewContext(factory);
        AvatarStore avatars = factory.Services.GetRequiredService<AvatarStore>();

        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx);
        await ChooseAsync(cut, "A short label");
        avatars.Save("You", Avatar.None);
        await cut.InvokeAsync(() => Task.CompletedTask);
        await Task.Delay(300, Xunit.TestContext.Current.CancellationToken);

        Assert.Contains(cut.FindAll("div.mud-input-control"), c => c.QuerySelectorAll("label").Any(l => l.TextContent.Contains("Label", StringComparison.Ordinal)));
    }

    /// <summary>An outside edit that differs from the editor still re-seeds it: the choice flips to Label showing the stored text.</summary>
    [Fact]
    public async Task External_DifferentValue_StillReseeds()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = NewContext(factory);
        AvatarStore avatars = factory.Services.GetRequiredService<AvatarStore>();

        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx);
        avatars.Save("You", new Avatar("AB", null, null));

        cut.WaitForAssertion(
            () =>
            {
                AngleSharp.Dom.IElement control = cut.FindAll("div.mud-input-control")
                    .First(c => c.QuerySelectorAll("label").Any(l => l.TextContent.Contains("Label", StringComparison.Ordinal)));
                Assert.Equal("AB", control.QuerySelector("input")?.GetAttribute("value"));
            },
            TimeSpan.FromSeconds(5));
    }
}
