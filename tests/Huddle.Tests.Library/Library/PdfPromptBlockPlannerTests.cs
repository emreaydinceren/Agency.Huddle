using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Library;

namespace Agency.Huddle.Tests.Library;

/// <summary>
/// Tests for the PDF rules of <see cref="PromptBlockPlanner"/> (Adapter Upgrades Spec §3): a PDF the Human
/// named goes as an embedded blob resource under its own count and byte caps, and every refusal falls
/// back to the path line. The bytes come from an injected reader, so no filesystem is involved.
/// </summary>
public sealed class PdfPromptBlockPlannerTests
{
    private static readonly LibraryRoot Root = new("docs", "Docs", @"E:\Docs", LibraryRootKind.Pinned);

    private static PromptDelivery Delivery(
        bool readsFiles = false,
        bool documents = true,
        int maxDocuments = 2,
        long maxDocumentBytes = 10485760,
        int maxImages = 4)
        => new(readsFiles, true, documents, maxImages, 8 * 1024 * 1024, documents, maxDocuments, maxDocumentBytes);

    private static PlanInput PdfInput(string name, bool fromTrigger = true)
        => new(new LibraryPath(Root, name, @"E:\Docs\" + name, LibraryNodeRole.File), LibraryFileKind.Pdf, fromTrigger, null);

    private static PlanInput PngInput(string name)
        => new(new LibraryPath(Root, name, @"E:\Docs\" + name, LibraryNodeRole.File), LibraryFileKind.Image, true, null);

    private static LibraryDocumentRead PdfBytes(int length = 100) => new(new byte[length]);

    private static Task<IReadOnlyList<PlannedDocument>> PlanAsync(
        IReadOnlyList<PlanInput> inputs,
        PromptDelivery delivery,
        Func<LibraryPath, long, LibraryDocumentResult>? readDocument = null,
        List<(string Name, long MaxBytes)>? documentReads = null)
    {
        return PromptBlockPlanner.PlanAsync(
            inputs,
            delivery,
            static (_, _) => Task.FromResult<LibraryImageResult>(new LibraryImageRead("image/png", new byte[100], 10, 10)),
            (path, maxBytes, _) =>
            {
                documentReads?.Add((path.RelativePath, maxBytes));
                LibraryDocumentRead defaultRead = PdfBytes();
                LibraryDocumentResult defaultResult = defaultRead.Bytes.Length > maxBytes
                    ? new LibraryDocumentRefused(LibraryDocumentRefusal.TooLarge)
                    : defaultRead;
                return Task.FromResult(readDocument is null ? defaultResult : readDocument(path, maxBytes));
            },
            TestContext.Current.CancellationToken);
    }

    /// <summary>Row 1: an eligible PDF is a blob block with the PDF media type, the file URI and the bytes read; the reader gets the whole byte allowance.</summary>
    [Fact]
    public async Task Plan_PdfEligible_IsBlobResourceBlock()
    {
        List<(string Name, long MaxBytes)> reads = [];
        byte[] bytes = [0x25, 0x50, 0x44, 0x46, 0x2D];

        IReadOnlyList<PlannedDocument> plan = await PlanAsync([PdfInput("a.pdf")], Delivery(), (_, _) => new LibraryDocumentRead(bytes), reads);

        PlannedDocument document = Assert.Single(plan);
        Assert.Equal(LibraryDocumentDelivery.Block, document.Delivery);
        AgentBlobResourceBlock block = Assert.IsType<AgentBlobResourceBlock>(document.Block);
        Assert.Equal("application/pdf", block.MimeType);
        Assert.Equal(new Uri(@"E:\Docs\a.pdf").AbsoluteUri, block.Uri);
        Assert.Equal(bytes, block.Data.ToArray());
        Assert.Equal(PromptBlockWithheld.None, document.Withheld);
        Assert.Equal([("a.pdf", 10485760L)], reads);
    }

    /// <summary>Row 2: with Documents off the PDF is a path line whether or not the Adapter has file tools (never Unavailable), nothing is withheld, and the reader is not called.</summary>
    /// <param name="readsFiles">Whether the Adapter has file tools.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Plan_PdfDocumentsOff_IsPathWithoutReading(bool readsFiles)
    {
        List<(string Name, long MaxBytes)> reads = [];

        IReadOnlyList<PlannedDocument> plan = await PlanAsync([PdfInput("a.pdf")], Delivery(readsFiles: readsFiles, documents: false), documentReads: reads);

        PlannedDocument document = Assert.Single(plan);
        Assert.Equal(LibraryDocumentDelivery.Path, document.Delivery);
        Assert.Null(document.Block);
        Assert.Equal(PromptBlockWithheld.None, document.Withheld);
        Assert.Empty(reads);
    }

    /// <summary>Row 3: a PDF not named in the triggering Message is a path line with nothing withheld, and the reader is not called.</summary>
    [Fact]
    public async Task Plan_PdfNotFromTrigger_IsPathWithoutReading()
    {
        List<(string Name, long MaxBytes)> reads = [];

        IReadOnlyList<PlannedDocument> plan = await PlanAsync([PdfInput("a.pdf", fromTrigger: false)], Delivery(), documentReads: reads);

        PlannedDocument document = Assert.Single(plan);
        Assert.Equal(LibraryDocumentDelivery.Path, document.Delivery);
        Assert.Equal(PromptBlockWithheld.None, document.Withheld);
        Assert.Empty(reads);
    }

    /// <summary>Row 4: the third PDF under a cap of two is a path line withheld as DocumentCountCap, and is not read.</summary>
    [Fact]
    public async Task Plan_ThirdPdfCapTwo_IsPathWithDocumentCountCap()
    {
        List<(string Name, long MaxBytes)> reads = [];

        IReadOnlyList<PlannedDocument> plan = await PlanAsync(
            [PdfInput("a.pdf"), PdfInput("b.pdf"), PdfInput("c.pdf")],
            Delivery(maxDocuments: 2),
            documentReads: reads);

        Assert.Equal(
            [LibraryDocumentDelivery.Block, LibraryDocumentDelivery.Block, LibraryDocumentDelivery.Path],
            plan.Select(document => document.Delivery));
        Assert.Equal(PromptBlockWithheld.DocumentCountCap, plan[2].Withheld);
        Assert.Equal(["a.pdf", "b.pdf"], reads.Select(read => read.Name));
    }

    /// <summary>Row 5: the reader is given the bytes still allowed, and a PDF that would pass the Turn total is a path line withheld as TurnBytesCap.</summary>
    [Fact]
    public async Task Plan_PdfsOverTheByteCap_PassRemainingBudgetAndWithholdTurnBytesCap()
    {
        List<(string Name, long MaxBytes)> reads = [];

        IReadOnlyList<PlannedDocument> plan = await PlanAsync(
            [PdfInput("a.pdf"), PdfInput("b.pdf"), PdfInput("c.pdf")],
            Delivery(maxDocuments: 3, maxDocumentBytes: 250),
            documentReads: reads);

        Assert.Equal([("a.pdf", 250L), ("b.pdf", 150L), ("c.pdf", 50L)], reads);
        Assert.Equal(
            [LibraryDocumentDelivery.Block, LibraryDocumentDelivery.Block, LibraryDocumentDelivery.Path],
            plan.Select(document => document.Delivery));
        Assert.Equal(PromptBlockWithheld.TurnBytesCap, plan[2].Withheld);
    }

    /// <summary>Row 6: a reader that cannot read the file makes that PDF a path line withheld as Unreadable.</summary>
    [Fact]
    public async Task Plan_PdfUnreadable_IsPathWithUnreadable()
    {
        IReadOnlyList<PlannedDocument> plan = await PlanAsync(
            [PdfInput("a.pdf")],
            Delivery(),
            (_, _) => new LibraryDocumentRefused(LibraryDocumentRefusal.Unreadable));

        PlannedDocument document = Assert.Single(plan);
        Assert.Equal(LibraryDocumentDelivery.Path, document.Delivery);
        Assert.Equal(PromptBlockWithheld.Unreadable, document.Withheld);
    }

    /// <summary>A reader that says the file is over the allowed bytes (which are the Turn's remaining budget) makes that PDF a path line withheld as TurnBytesCap.</summary>
    [Fact]
    public async Task Plan_PdfTooLarge_IsPathWithTurnBytesCap()
    {
        IReadOnlyList<PlannedDocument> plan = await PlanAsync(
            [PdfInput("a.pdf")],
            Delivery(),
            (_, _) => new LibraryDocumentRefused(LibraryDocumentRefusal.TooLarge));

        PlannedDocument document = Assert.Single(plan);
        Assert.Equal(LibraryDocumentDelivery.Path, document.Delivery);
        Assert.Equal(PromptBlockWithheld.TurnBytesCap, document.Withheld);
    }

    /// <summary>A reader that throws an <see cref="IOException"/> costs that one PDF, never the Turn: a path line withheld as Unreadable, and the next PDF still goes.</summary>
    [Fact]
    public async Task Plan_PdfReaderThrowsIOException_IsPathUnreadable()
    {
        IReadOnlyList<PlannedDocument> plan = await PlanAsync(
            [PdfInput("a.pdf"), PdfInput("b.pdf")],
            Delivery(),
            (path, _) => path.RelativePath == "a.pdf" ? throw new IOException("locked") : PdfBytes());

        Assert.Equal([LibraryDocumentDelivery.Path, LibraryDocumentDelivery.Block], plan.Select(document => document.Delivery));
        Assert.Equal(PromptBlockWithheld.Unreadable, plan[0].Withheld);
    }

    /// <summary>Row 7: PDFs do not consume the image caps; an image after two PDFs under an image cap of one still goes.</summary>
    [Fact]
    public async Task Plan_PdfsDoNotConsumeImageCaps()
    {
        IReadOnlyList<PlannedDocument> plan = await PlanAsync(
            [PdfInput("a.pdf"), PdfInput("b.pdf"), PngInput("c.png")],
            Delivery(maxImages: 1));

        Assert.Equal(
            [LibraryDocumentDelivery.Block, LibraryDocumentDelivery.Block, LibraryDocumentDelivery.Block],
            plan.Select(document => document.Delivery));
        Assert.IsType<AgentImageBlock>(plan[2].Block);
    }

    /// <summary>Images do not consume the document caps either: a PDF after an image under a document cap of one still goes.</summary>
    [Fact]
    public async Task Plan_ImagesDoNotConsumeDocumentCaps()
    {
        IReadOnlyList<PlannedDocument> plan = await PlanAsync(
            [PngInput("a.png"), PdfInput("b.pdf")],
            Delivery(maxDocuments: 1));

        Assert.IsType<AgentBlobResourceBlock>(plan[1].Block);
    }

    /// <summary>A PDF the reader refused does not use up the count or the bytes, so a later one still fits.</summary>
    [Fact]
    public async Task Plan_RefusedPdf_DoesNotConsumeBudget()
    {
        IReadOnlyList<PlannedDocument> plan = await PlanAsync(
            [PdfInput("bad.pdf"), PdfInput("good.pdf")],
            Delivery(maxDocuments: 1),
            (path, _) => path.RelativePath == "bad.pdf" ? new LibraryDocumentRefused(LibraryDocumentRefusal.TooLarge) : PdfBytes());

        Assert.Equal([LibraryDocumentDelivery.Path, LibraryDocumentDelivery.Block], plan.Select(document => document.Delivery));
    }

    /// <summary>A document cap of zero or less sends no PDFs and does not read one: the safe direction.</summary>
    /// <param name="maxDocuments">The count cap.</param>
    /// <param name="maxDocumentBytes">The byte cap.</param>
    [Theory]
    [InlineData(0, 10485760L)]
    [InlineData(-1, 10485760L)]
    [InlineData(2, 0L)]
    [InlineData(2, -1L)]
    public async Task Plan_NonPositiveDocumentCap_SendsNone(int maxDocuments, long maxDocumentBytes)
    {
        List<(string Name, long MaxBytes)> reads = [];

        IReadOnlyList<PlannedDocument> plan = await PlanAsync([PdfInput("a.pdf")], Delivery(maxDocuments: maxDocuments, maxDocumentBytes: maxDocumentBytes), documentReads: reads);

        Assert.Equal(LibraryDocumentDelivery.Path, Assert.Single(plan).Delivery);
        Assert.Empty(reads);
    }
}
