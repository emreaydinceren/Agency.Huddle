using Agency.Huddle.App.Components.Library;
using Agency.Huddle.App.Gateway;
using Agency.Huddle.App.Library;
using Agency.Huddle.Tests.Gateway;
using Agency.Huddle.Tests.Library;
using Bunit;
using Bunit.Rendering;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace Agency.Huddle.Tests.Ui.Library;

/// <summary>
/// Remote access spec 6.15 (REMOTE-11): the Library's "Open in default app" starts a process on the host, so it goes through
/// <see cref="HostActions"/> like the Agent card's Open: not drawn while remote access is configured, and refused when reached anyway.
/// </summary>
public sealed class LibraryRemoteAccessTests : IDisposable
{
    private readonly LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();

    /// <inheritdoc />
    public void Dispose() => this.fixture.Dispose();

    private MudBunitContext NewContext(bool configured)
    {
        MudBunitContext ctx = new();
        ctx.Services.AddSingleton(this.fixture.Resolver);
        ctx.Services.AddSingleton(this.fixture.RootStore);
        ctx.Services.AddSingleton(this.fixture.CreateService());
        ctx.Services.AddSingleton(this.fixture.Index);
        ctx.Services.AddSingleton<ILibraryNoteResolver>(new FakeLibraryNoteResolver());
        ctx.Services.AddDocHistoryServices();
        ctx.Services.AddSingleton<Agency.Huddle.App.Tasks.ITaskReferenceResolver>(new FakeTaskReferenceResolver());
        ((FakeRemoteAccessState)ctx.Services.GetRequiredService<IRemoteAccessState>()).IsConfigured = configured;
        return ctx;
    }

    private LibraryPath BinaryFile()
    {
        string root = this.fixture.CreatePinnedRoot("Notes");
        File.WriteAllBytes(Path.Combine(root, "a.bin"), new byte[1024]);
        return this.fixture.Resolve(root, "a.bin");
    }

    /// <summary>While remote access is configured the document card for an unrecognised file has no "Open in default app" button.</summary>
    [Fact]
    public async Task OpenInDefaultApp_RemoteAccessConfigured_IsNotRendered()
    {
        LibraryPath path = this.BinaryFile();
        await using MudBunitContext ctx = this.NewContext(configured: true);

        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<LibraryDocument>(0);
            builder.AddAttribute(1, nameof(LibraryDocument.Path), path);
            builder.CloseComponent();
        });

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".library-document-other")));
        Assert.Empty(cut.FindAll(".library-document-open-default"));
    }

    /// <summary>With remote access off the button is still drawn.</summary>
    [Fact]
    public async Task OpenInDefaultApp_RemoteAccessOff_IsRendered()
    {
        LibraryPath path = this.BinaryFile();
        await using MudBunitContext ctx = this.NewContext(configured: false);

        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<LibraryDocument>(0);
            builder.AddAttribute(1, nameof(LibraryDocument.Path), path);
            builder.CloseComponent();
        });

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("button.library-document-open-default")));
    }

    /// <summary>The handler refuses with the refusal text, shown as a snackbar, and throws nothing, when the action arrives while remote access is configured.</summary>
    [Fact]
    public async Task HandleAsync_OpenInDefaultApp_RemoteAccessConfigured_RefusesWithoutThrowing()
    {
        LibraryPath path = this.BinaryFile();
        await using MudBunitContext ctx = this.NewContext(configured: true);
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<LibraryFileOps>(0);
            builder.CloseComponent();
        });
        LibraryFileOps ops = cut.FindComponent<LibraryFileOps>().Instance;

        await cut.InvokeAsync(() => ops.HandleAsync(new LibraryTreeAction(LibraryTreeActionKind.OpenInDefaultApp, path, null)));

        ISnackbar snackbar = ctx.Services.GetRequiredService<ISnackbar>();
        Assert.Equal(HostActions.RefusedMessage, Assert.Single(snackbar.ShownSnackbars).Message);
    }
}
