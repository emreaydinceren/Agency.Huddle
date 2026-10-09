using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Avatars;
using static Agency.Huddle.Tests.Ui.AvatarLibraryPickSupport;
using static Agency.Huddle.Tests.Ui.TeammateCardTestSupport;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// The "Choose from library" button of the avatar editor in <see cref="Agency.Huddle.App.Components.Shared.TeammateCard"/>
/// (Avatar Library Spec §6.6 and §6.7): it shows only for a configured library under the Image choice, and a pick
/// feeds the same pending-image state an upload does, so preview, Save and Cancel behave as for a file.
/// </summary>
public sealed class TeammateCardLibraryTests
{
    private const string PendingText = "New image selected (not yet saved)";

    private const string UnreadableText = "That image could not be read.";

    /// <summary>Seeds the Persona "coo", opens its card, switches to Edit and (optionally) chooses the Image avatar option.</summary>
    private static async Task<IRenderedComponent<ContainerFragment>> OpenEditAsync(MudBunitContext ctx, TeamWebApplicationFactory factory, bool chooseImage)
    {
        IRenderedComponent<ContainerFragment> cut = await OpenViewCardAsync(ctx, factory, "coo");
        await ClickButtonAsync(cut, "Edit");
        if (chooseImage)
        {
            await SelectAvatarChoiceAsync(cut, "An image");
        }

        return cut;
    }

    /// <summary>Clicks the library button and clicks the fourth thumbnail (av-003.png) once the picker has painted.</summary>
    private static async Task PickFourthAsync(IRenderedComponent<ContainerFragment> cut)
    {
        await ClickButtonAsync(cut, ButtonText);
        cut.WaitForAssertion(() => Assert.True(cut.FindAll(Thumb).Count > 3), TimeSpan.FromSeconds(5));
        await ClickAsync(cut, () => cut.FindAll(Thumb)[3]);
    }

    /// <summary>With no library configured the Image choice offers no library button.</summary>
    [Fact]
    public async Task LibraryButton_Hidden_WhenNotConfigured()
    {
        await using TeamWebApplicationFactory factory = new();
        await SeedPersonaAsync(factory, "coo", "x");
        await using MudBunitContext ctx = NewContext(factory);

        IRenderedComponent<ContainerFragment> cut = await OpenEditAsync(ctx, factory, chooseImage: true);

        Assert.True(HasButton(cut, "Choose an image"));
        Assert.False(HasButton(cut, ButtonText));
    }

    /// <summary>With a configured library the Image choice offers the library button next to "Choose an image".</summary>
    [Fact]
    public async Task LibraryButton_Shown_WhenConfigured()
    {
        await using TeamWebApplicationFactory factory = new();
        await SeedPersonaAsync(factory, "coo", "x");
        using TempDataDir library = CreateLibraryDir();
        await using MudBunitContext ctx = NewContext(factory);
        _ = Register(ctx.Services, library.Path, null);

        IRenderedComponent<ContainerFragment> cut = await OpenEditAsync(ctx, factory, chooseImage: true);

        Assert.True(HasButton(cut, ButtonText));
        Assert.True(HasButton(cut, "Choose an image"));
    }

    /// <summary>The button exists only under the Image choice, not under Initials or Label.</summary>
    [Fact]
    public async Task LibraryButton_OnlyUnderImageChoice()
    {
        await using TeamWebApplicationFactory factory = new();
        await SeedPersonaAsync(factory, "coo", "x");
        using TempDataDir library = CreateLibraryDir();
        await using MudBunitContext ctx = NewContext(factory);
        _ = Register(ctx.Services, library.Path, null);

        IRenderedComponent<ContainerFragment> cut = await OpenEditAsync(ctx, factory, chooseImage: false);
        Assert.False(HasButton(cut, ButtonText));

        await SelectAvatarChoiceAsync(cut, "A short label");
        Assert.False(HasButton(cut, ButtonText));

        await SelectAvatarChoiceAsync(cut, "An image");
        Assert.True(HasButton(cut, ButtonText));
    }

    /// <summary>Picking a thumbnail reads that file and buffers it as an upload does: pending text shows and nothing reaches disk before Save.</summary>
    [Fact]
    public async Task LibraryPick_SetsPendingBytes_AsUploadDoes()
    {
        await using TeamWebApplicationFactory factory = new();
        await SeedPersonaAsync(factory, "coo", "x");
        using TempDataDir library = CreateLibraryDir();
        await using MudBunitContext ctx = NewContext(factory);
        PickLoader loader = Register(ctx.Services, library.Path, new AvatarLibraryImage(MinimalPng, ".png"));

        IRenderedComponent<ContainerFragment> cut = await OpenEditAsync(ctx, factory, chooseImage: true);
        await PickFourthAsync(cut);

        cut.WaitForAssertion(() => Assert.Contains(PendingText, cut.Markup, StringComparison.Ordinal), TimeSpan.FromSeconds(5));
        Assert.Equal("av-003.png", loader.LastFileName);
        Assert.DoesNotContain(UnreadableText, cut.Markup, StringComparison.Ordinal);
        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(DataDirOf(factory), "avatars")));
    }

    /// <summary>Dismissing the picker changes nothing: no pending image, no error, no file read, nothing on disk.</summary>
    [Fact]
    public async Task LibraryPick_Cancel_WritesNothing()
    {
        await using TeamWebApplicationFactory factory = new();
        await SeedPersonaAsync(factory, "coo", "x");
        using TempDataDir library = CreateLibraryDir();
        await using MudBunitContext ctx = NewContext(factory);
        PickLoader loader = Register(ctx.Services, library.Path, new AvatarLibraryImage(MinimalPng, ".png"));

        IRenderedComponent<ContainerFragment> cut = await OpenEditAsync(ctx, factory, chooseImage: true);
        await ClickButtonAsync(cut, ButtonText);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(Thumb)), TimeSpan.FromSeconds(5));

        await cut.InvokeAsync(() => cut.Find(".avatar-library-search input").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" }));

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(Thumb)), TimeSpan.FromSeconds(5));
        Assert.DoesNotContain(PendingText, cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain(UnreadableText, cut.Markup, StringComparison.Ordinal);
        Assert.Null(loader.LastFileName);
        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(DataDirOf(factory), "avatars")));
    }

    /// <summary>A pick whose file cannot be read shows the avatar error line and buffers nothing.</summary>
    [Fact]
    public async Task LibraryPick_Unreadable_ShowsAvatarError()
    {
        await using TeamWebApplicationFactory factory = new();
        await SeedPersonaAsync(factory, "coo", "x");
        using TempDataDir library = CreateLibraryDir();
        await using MudBunitContext ctx = NewContext(factory);
        _ = Register(ctx.Services, library.Path, null);

        IRenderedComponent<ContainerFragment> cut = await OpenEditAsync(ctx, factory, chooseImage: true);
        await PickFourthAsync(cut);

        cut.WaitForAssertion(() => Assert.Equal(UnreadableText, cut.Find(".mud-alert").TextContent.Trim()), TimeSpan.FromSeconds(5));
        Assert.DoesNotContain(PendingText, cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>After a library pick, Save writes the bytes through the store and the Persona's Avatar names the generated image.</summary>
    [Fact]
    public async Task LibraryPick_SaveWritesImageThroughStore()
    {
        await using TeamWebApplicationFactory factory = new();
        await SeedPersonaAsync(factory, "coo", "x");
        using TempDataDir library = CreateLibraryDir();
        await using MudBunitContext ctx = NewContext(factory);
        _ = Register(ctx.Services, library.Path, new AvatarLibraryImage(MinimalPng, ".png"));
        AvatarStore avatars = factory.Services.GetRequiredService<AvatarStore>();

        IRenderedComponent<ContainerFragment> cut = await OpenEditAsync(ctx, factory, chooseImage: true);
        await PickFourthAsync(cut);
        cut.WaitForAssertion(() => Assert.Contains(PendingText, cut.Markup, StringComparison.Ordinal), TimeSpan.FromSeconds(5));
        await ClickButtonAsync(cut, "Save");

        string? image = avatars.Get("coo").Image;
        Assert.NotNull(image);
        Assert.EndsWith(".png", image, StringComparison.Ordinal);
        Assert.Single(Directory.EnumerateFileSystemEntries(Path.Combine(DataDirOf(factory), "avatars")));
    }
}
