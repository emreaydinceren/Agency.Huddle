using Agency.Huddle.App.Library;
using Agency.Huddle.Tests.Library;
using Bunit;
using Bunit.Rendering;
using Microsoft.Extensions.DependencyInjection;
using static Agency.Huddle.Tests.Ui.Library.LibraryDocumentTestSupport;

namespace Agency.Huddle.Tests.Ui.Library;

/// <summary>Pins how <c>LibraryDocument</c> shows a PDF: the card with "Open in default app", never a text-decoding banner.</summary>
public sealed class LibraryDocumentPdfReadTests : IDisposable
{
    private readonly LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();

    /// <inheritdoc />
    public void Dispose() => this.fixture.Dispose();

    /// <summary>A binary PDF larger than the editing cap shows the card and no view-only banner.</summary>
    [Fact]
    public async Task Pdf_Binary_ShowsCardWithOpenDefault()
    {
        string root = this.fixture.CreatePinnedRoot("Notes");
        byte[] bytes = new byte[3 * 1024 * 1024];
        Array.Fill(bytes, (byte)0x80);
        "%PDF-1.4\n"u8.CopyTo(bytes);
        File.WriteAllBytes(Path.Combine(root, "a.pdf"), bytes);
        LibraryPath path = this.fixture.Resolve(root, "a.pdf");
        await using MudBunitContext ctx = new();
        ctx.Services.AddSingleton(this.fixture.Resolver);
        ctx.Services.AddSingleton(this.fixture.RootStore);
        ctx.Services.AddSingleton(this.fixture.CreateService());
        ctx.Services.AddSingleton(this.fixture.Index);
        ctx.Services.AddSingleton<ILibraryNoteResolver>(new FakeLibraryNoteResolver());
        ctx.Services.AddDocHistoryServices();
        ctx.Services.AddSingleton<Agency.Huddle.App.Tasks.ITaskReferenceResolver>(new FakeTaskReferenceResolver());

        IRenderedComponent<ContainerFragment> cut = RenderDoc(ctx, path);

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".library-document-other")));
        Assert.Equal("a.pdf", cut.Find(".library-document-other-name").TextContent.Trim());
        Assert.Equal("Open in default app", cut.Find(".library-document-open-default").TextContent.Trim());
        Assert.Empty(cut.FindAll(".library-document-banner"));
    }
}
