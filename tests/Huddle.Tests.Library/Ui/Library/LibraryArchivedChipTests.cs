using Agency.Huddle.App.Library;
using Agency.Huddle.Tests.Library;
using Bunit;
using Bunit.Rendering;
using Microsoft.Extensions.DependencyInjection;
using static Agency.Huddle.Tests.Ui.Library.LibraryDocumentTestSupport;

namespace Agency.Huddle.Tests.Ui.Library;

/// <summary>Pins SB-R20's viewer row (Spec §8.5): a document reached through the archive fallback shows an "Archived" chip.</summary>
public sealed class LibraryArchivedChipTests : IDisposable
{
    private readonly LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();

    /// <inheritdoc />
    public void Dispose() => this.fixture.Dispose();

    private MudBunitContext NewContext()
    {
        MudBunitContext ctx = new();
        ctx.Services.AddSingleton(this.fixture.Resolver);
        ctx.Services.AddSingleton(this.fixture.RootStore);
        ctx.Services.AddSingleton(this.fixture.CreateService());
        ctx.Services.AddSingleton(this.fixture.Index);
        ctx.Services.AddSingleton<ILibraryNoteResolver>(new FakeLibraryNoteResolver());
        ctx.Services.AddDocHistoryServices();
        ctx.Services.AddSingleton<Agency.Huddle.App.Tasks.ITaskReferenceResolver>(new FakeTaskReferenceResolver());
        return ctx;
    }

    /// <summary>A path resolved for reading through the archive fallback renders the Archived chip; a live path renders none.</summary>
    [Fact]
    public async Task Viewer_ArchivedFallbackPath_ShowsArchivedChip()
    {
        string archived = Path.Combine(this.fixture.DataDir, "Archive", "Home", "outputs");
        Directory.CreateDirectory(archived);
        File.WriteAllText(Path.Combine(archived, "plan.md"), "# Plan");
        string live = Path.Combine(this.fixture.DataDir, "Teams", "Live");
        Directory.CreateDirectory(live);
        File.WriteAllText(Path.Combine(live, "a.md"), "# Live");
        Assert.True(this.fixture.Resolver.TryResolveForRead("teams", "Home/outputs/plan.md", out LibraryPath? archivedPath, out _));
        Assert.True(this.fixture.Resolver.TryResolveForRead("teams", "Live/a.md", out LibraryPath? livePath, out _));
        Assert.NotNull(archivedPath);
        Assert.NotNull(livePath);
        await using MudBunitContext ctx = this.NewContext();

        IRenderedComponent<ContainerFragment> cut = RenderDoc(ctx, archivedPath);
        await using MudBunitContext liveCtx = this.NewContext();
        IRenderedComponent<ContainerFragment> liveCut = RenderDoc(liveCtx, livePath);

        cut.WaitForAssertion(() => Assert.Equal("Archived", cut.Find(".library-document-archived").TextContent.Trim()));
        liveCut.WaitForAssertion(() => Assert.NotEmpty(liveCut.FindAll(".library-document-copy")));
        Assert.Empty(liveCut.FindAll(".library-document-archived"));
    }
}
