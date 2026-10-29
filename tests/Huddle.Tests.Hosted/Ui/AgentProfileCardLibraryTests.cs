using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Avatars;
using static Agency.Huddle.Tests.Ui.AvatarLibraryPickSupport;
using static Agency.Huddle.Tests.Ui.AgentProfileCardTestSupport;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// The "Choose from library" button of <see cref="Agency.Huddle.App.Components.Shared.AvatarEditorDialog"/>, opened from
/// <see cref="Agency.Huddle.App.Components.Shared.AgentProfileCard"/> (Avatar Library Spec §6.6 and §6.7): it shows only for a configured library
/// under the Image choice, and a pick feeds the same pending-image state an upload does, so preview, Apply, Save and Cancel behave as for a file.
/// </summary>
public sealed class AgentProfileCardLibraryTests
{
    private const string PendingText = "New image selected (not yet saved)";

    private const string UnreadableText = "That image could not be read.";

    /// <summary>Seeds the Persona "coo", opens its card, optionally switches to Edit, opens the avatar editor and (optionally) chooses the Image option.</summary>
    private static async Task<IRenderedComponent<ContainerFragment>> OpenEditorAsync(MudBunitContext ctx, TeamWebApplicationFactory factory, bool edit, bool chooseImage)
    {
        IRenderedComponent<ContainerFragment> cut = await OpenViewCardAsync(ctx, factory, "coo");
        if (edit)
        {
            await ClickButtonAsync(cut, "Edit");
        }

        await OpenAvatarEditorAsync(cut);
        if (chooseImage)
        {
            await SelectAvatarChoiceAsync(cut, "An image");
        }

        return cut;
    }

    /// <summary>Clicks the library button and clicks the fourth thumbnail (av-003.png) once the picker has painted.</summary>
    private static async Task PickFourthAsync(IRenderedComponent<ContainerFragment> cut)
    {
        await ClickEditorButtonAsync(cut, ButtonText);
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

        IRenderedComponent<ContainerFragment> cut = await OpenEditorAsync(ctx, factory, edit: true, chooseImage: true);

        Assert.True(HasEditorButton(cut, "Choose an image"));
        Assert.False(HasEditorButton(cut, ButtonText));
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

        IRenderedComponent<ContainerFragment> cut = await OpenEditorAsync(ctx, factory, edit: true, chooseImage: true);

        Assert.True(HasEditorButton(cut, ButtonText));
        Assert.True(HasEditorButton(cut, "Choose an image"));
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

        IRenderedComponent<ContainerFragment> cut = await OpenEditorAsync(ctx, factory, edit: true, chooseImage: false);
        Assert.True(HasEditorButton(cut, ButtonText));

        await SelectAvatarChoiceAsync(cut, "A short label");
        Assert.False(HasEditorButton(cut, ButtonText));

        await SelectAvatarChoiceAsync(cut, "An image");
        Assert.True(HasEditorButton(cut, ButtonText));
    }

    /// <summary>Picking a thumbnail reads that file and buffers it as an upload does: pending text shows in the editor and nothing reaches disk.</summary>
    [Fact]
    public async Task LibraryPick_SetsPendingBytes_AsUploadDoes()
    {
        await using TeamWebApplicationFactory factory = new();
        await SeedPersonaAsync(factory, "coo", "x");
        using TempDataDir library = CreateLibraryDir();
        await using MudBunitContext ctx = NewContext(factory);
        PickLoader loader = Register(ctx.Services, library.Path, new AvatarLibraryImage(MinimalPng, ".png"));

        IRenderedComponent<ContainerFragment> cut = await OpenEditorAsync(ctx, factory, edit: true, chooseImage: true);
        await PickFourthAsync(cut);

        cut.WaitForAssertion(() => Assert.Equal(PendingText, EditorImageStatus(cut)), TimeSpan.FromSeconds(5));
        Assert.Equal("av-003.png", loader.LastFileName);
        Assert.DoesNotContain(UnreadableText, cut.Markup, StringComparison.Ordinal);
        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(DataDirOf(factory), "avatars")));
    }

    /// <summary>Dismissing the picker changes nothing and leaves the editor open: no pending image, no error, no file read, nothing on disk.</summary>
    [Fact]
    public async Task LibraryPick_Cancel_WritesNothing_AndLeavesTheEditorOpen()
    {
        await using TeamWebApplicationFactory factory = new();
        await SeedPersonaAsync(factory, "coo", "x");
        using TempDataDir library = CreateLibraryDir();
        await using MudBunitContext ctx = NewContext(factory);
        PickLoader loader = Register(ctx.Services, library.Path, new AvatarLibraryImage(MinimalPng, ".png"));

        IRenderedComponent<ContainerFragment> cut = await OpenEditorAsync(ctx, factory, edit: true, chooseImage: true);
        await ClickEditorButtonAsync(cut, ButtonText);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(Thumb)), TimeSpan.FromSeconds(5));

        await cut.InvokeAsync(() => cut.Find(".avatar-library-search input").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" }));

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(Thumb)), TimeSpan.FromSeconds(5));
        Assert.NotEmpty(cut.FindAll(EditorDialog));
        Assert.DoesNotContain(PendingText, cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain(UnreadableText, cut.Markup, StringComparison.Ordinal);
        Assert.Null(loader.LastFileName);
        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(DataDirOf(factory), "avatars")));
    }

    /// <summary>A pick whose file cannot be read shows the error line inside the editor and buffers nothing.</summary>
    [Fact]
    public async Task LibraryPick_Unreadable_ShowsTheErrorInTheEditor()
    {
        await using TeamWebApplicationFactory factory = new();
        await SeedPersonaAsync(factory, "coo", "x");
        using TempDataDir library = CreateLibraryDir();
        await using MudBunitContext ctx = NewContext(factory);
        _ = Register(ctx.Services, library.Path, null);

        IRenderedComponent<ContainerFragment> cut = await OpenEditorAsync(ctx, factory, edit: true, chooseImage: true);
        await PickFourthAsync(cut);

        cut.WaitForAssertion(() => Assert.Equal(UnreadableText, cut.Find(EditorDialog + " .mud-alert").TextContent.Trim()), TimeSpan.FromSeconds(5));
        Assert.DoesNotContain(PendingText, cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>After a library pick, Apply and the card's Save write the bytes through the store, and the Persona's Avatar names the generated image.</summary>
    [Fact]
    public async Task LibraryPick_ApplyThenSave_WritesImageThroughStore()
    {
        await using TeamWebApplicationFactory factory = new();
        await SeedPersonaAsync(factory, "coo", "x");
        using TempDataDir library = CreateLibraryDir();
        await using MudBunitContext ctx = NewContext(factory);
        _ = Register(ctx.Services, library.Path, new AvatarLibraryImage(MinimalPng, ".png"));
        AvatarStore avatars = factory.Services.GetRequiredService<AvatarStore>();

        IRenderedComponent<ContainerFragment> cut = await OpenEditorAsync(ctx, factory, edit: true, chooseImage: true);
        await PickFourthAsync(cut);
        cut.WaitForAssertion(() => Assert.Equal(PendingText, EditorImageStatus(cut)), TimeSpan.FromSeconds(5));
        await ClickEditorButtonAsync(cut, "Apply");
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(EditorDialog)), TimeSpan.FromSeconds(5));
        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(DataDirOf(factory), "avatars")));
        await ClickButtonAsync(cut, "Save");

        string? image = avatars.Get("coo").Image;
        Assert.NotNull(image);
        Assert.Equal(".png", Path.GetExtension(image));
        Assert.Single(Directory.EnumerateFileSystemEntries(Path.Combine(DataDirOf(factory), "avatars")));
    }

    /// <summary>In View the editor's Save writes a library pick at once: the image is filed and the Persona's Avatar names it, with no card Save.</summary>
    [Fact]
    public async Task LibraryPick_InView_EditorSaveWritesImmediately()
    {
        await using TeamWebApplicationFactory factory = new();
        await SeedPersonaAsync(factory, "coo", "x");
        using TempDataDir library = CreateLibraryDir();
        await using MudBunitContext ctx = NewContext(factory);
        _ = Register(ctx.Services, library.Path, new AvatarLibraryImage(MinimalPng, ".png"));
        AvatarStore avatars = factory.Services.GetRequiredService<AvatarStore>();

        IRenderedComponent<ContainerFragment> cut = await OpenEditorAsync(ctx, factory, edit: false, chooseImage: true);
        await PickFourthAsync(cut);
        cut.WaitForAssertion(() => Assert.Equal(PendingText, EditorImageStatus(cut)), TimeSpan.FromSeconds(5));
        await ClickEditorButtonAsync(cut, "Save");
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(EditorDialog)), TimeSpan.FromSeconds(5));

        string? image = avatars.Get("coo").Image;
        Assert.NotNull(image);
        Assert.Single(Directory.EnumerateFileSystemEntries(Path.Combine(DataDirOf(factory), "avatars")));
    }
}
