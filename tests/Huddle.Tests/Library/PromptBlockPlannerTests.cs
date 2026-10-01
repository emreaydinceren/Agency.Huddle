using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Library;

namespace Agency.Huddle.Tests.Library;

/// <summary>
/// Tests for <see cref="PromptBlockPlanner"/> (design §6.4): per Library document, how it reaches the
/// model: as today's <c>Path</c> line, as a <c>Block</c>, or <c>Unavailable</c>. Pure: the bytes come
/// from an injected reader, so the delivery table is exercised without a filesystem.
/// </summary>
public sealed class PromptBlockPlannerTests
{
    private static readonly LibraryRoot Root = new("docs", "Docs", @"E:\Docs", LibraryRootKind.Pinned);

    private static PromptDelivery Delivery(
        bool readsFiles = true,
        bool images = true,
        bool embeddedText = false,
        int maxImages = 4,
        long maxBytes = 8 * 1024 * 1024) => new(readsFiles, images, embeddedText, maxImages, maxBytes);

    private static LibraryPath PathOf(string name) => new(Root, name, @"E:\Docs\" + name, LibraryNodeRole.File);

    private static PlanInput Input(string name, LibraryFileKind kind = LibraryFileKind.Image, bool fromTrigger = true, string? text = null)
        => new(PathOf(name), kind, fromTrigger, text);

    private static LibraryImageRead Png(int bytes = 100) => new("image/png", new byte[bytes], 10, 10);

    private static Task<IReadOnlyList<PlannedDocument>> PlanAsync(
        IReadOnlyList<PlanInput> inputs,
        PromptDelivery delivery,
        Func<LibraryPath, LibraryImageResult>? read = null,
        List<string>? reads = null)
    {
        return PromptBlockPlanner.PlanAsync(
            inputs,
            delivery,
            (path, _) =>
            {
                reads?.Add(path.RelativePath);
                return Task.FromResult(read is null ? Png() : read(path));
            },
            TestContext.Current.CancellationToken);
    }

    /// <summary>An image the Adapter takes, from the trigger Message and within every cap, is a block carrying the bytes read.</summary>
    [Fact]
    public async Task Plan_ImageEligible_IsBlock()
    {
        IReadOnlyList<PlannedDocument> plan = await PlanAsync([Input("a.png")], Delivery());

        PlannedDocument document = Assert.Single(plan);
        Assert.Equal(LibraryDocumentDelivery.Block, document.Delivery);
        AgentImageBlock block = Assert.IsType<AgentImageBlock>(document.Block);
        Assert.Equal("image/png", block.MimeType);
        Assert.Equal(100, block.Data.Length);
        Assert.Equal(PromptBlockWithheld.None, document.Withheld);
    }

    /// <summary>The Adapter takes no images and reads files: today's path line, and the file is never read.</summary>
    [Fact]
    public async Task Plan_ImagesOffReadsFiles_IsPathWithoutReading()
    {
        List<string> reads = [];

        IReadOnlyList<PlannedDocument> plan = await PlanAsync([Input("a.png")], Delivery(images: false), reads: reads);

        Assert.Equal(LibraryDocumentDelivery.Path, Assert.Single(plan).Delivery);
        Assert.Empty(reads);
    }

    /// <summary>The Adapter takes no images and has no file tools: the model is told it cannot see the image.</summary>
    [Fact]
    public async Task Plan_ImagesOffNoFileTools_IsUnavailable()
    {
        IReadOnlyList<PlannedDocument> plan = await PlanAsync([Input("a.png")], Delivery(readsFiles: false, images: false));

        PlannedDocument document = Assert.Single(plan);
        Assert.Equal(LibraryDocumentDelivery.Unavailable, document.Delivery);
        Assert.Null(document.Block);
    }

    /// <summary>An Adapter with no file tools that does take images gets a block.</summary>
    [Fact]
    public async Task Plan_NoFileToolsImagesOn_IsBlock()
    {
        IReadOnlyList<PlannedDocument> plan = await PlanAsync([Input("a.png")], Delivery(readsFiles: false, images: true));

        Assert.Equal(LibraryDocumentDelivery.Block, Assert.Single(plan).Delivery);
    }

    /// <summary>An image first written in catch-up, not the Message that started the Turn, is a path line and is never read (D-2).</summary>
    [Fact]
    public async Task Plan_ImageNotFromTrigger_IsPathWithoutReading()
    {
        List<string> reads = [];

        IReadOnlyList<PlannedDocument> plan = await PlanAsync([Input("a.png", fromTrigger: false)], Delivery(), reads: reads);

        Assert.Equal(LibraryDocumentDelivery.Path, Assert.Single(plan).Delivery);
        Assert.Empty(reads);
    }

    /// <summary>An image not from the trigger, for an Adapter with no file tools, is Unavailable, not a block.</summary>
    [Fact]
    public async Task Plan_ImageNotFromTriggerNoFileTools_IsUnavailable()
    {
        IReadOnlyList<PlannedDocument> plan = await PlanAsync([Input("a.png", fromTrigger: false)], Delivery(readsFiles: false));

        Assert.Equal(LibraryDocumentDelivery.Unavailable, Assert.Single(plan).Delivery);
    }

    /// <summary>SVG, Other, and (with file tools) Markdown and Text are path lines, never blocks, and never read as images.</summary>
    /// <param name="kind">The document kind.</param>
    [Theory]
    [InlineData(LibraryFileKind.Svg)]
    [InlineData(LibraryFileKind.Other)]
    [InlineData(LibraryFileKind.Markdown)]
    [InlineData(LibraryFileKind.Text)]
    public async Task Plan_NonImageKinds_AreAlwaysPath(LibraryFileKind kind)
    {
        List<string> reads = [];

        IReadOnlyList<PlannedDocument> plan = await PlanAsync([Input("x.bin", kind, text: "hello")], Delivery(embeddedText: false), reads: reads);

        Assert.Equal(LibraryDocumentDelivery.Path, Assert.Single(plan).Delivery);
        Assert.Empty(reads);
    }

    /// <summary>SVG and Other for an Adapter with no file tools stay path lines: only an image kind can be Unavailable.</summary>
    /// <param name="kind">The document kind.</param>
    [Theory]
    [InlineData(LibraryFileKind.Svg)]
    [InlineData(LibraryFileKind.Other)]
    public async Task Plan_SvgAndOtherNoFileTools_ArePath(LibraryFileKind kind)
    {
        IReadOnlyList<PlannedDocument> plan = await PlanAsync([Input("x.bin", kind)], Delivery(readsFiles: false, embeddedText: true));

        Assert.Equal(LibraryDocumentDelivery.Path, Assert.Single(plan).Delivery);
    }

    /// <summary>Images are admitted in first-seen order up to the count cap; the fifth, over a cap of four, is a path line.</summary>
    [Fact]
    public async Task Plan_FiveImagesCapFour_FirstFourAreBlocks()
    {
        PlanInput[] inputs = [.. Enumerable.Range(1, 5).Select(i => Input($"{i}.png"))];

        IReadOnlyList<PlannedDocument> plan = await PlanAsync(inputs, Delivery(maxImages: 4));

        Assert.Equal(
            [LibraryDocumentDelivery.Block, LibraryDocumentDelivery.Block, LibraryDocumentDelivery.Block, LibraryDocumentDelivery.Block, LibraryDocumentDelivery.Path],
            plan.Select(document => document.Delivery));
        Assert.Equal(PromptBlockWithheld.ImageCountCap, plan[4].Withheld);
    }

    /// <summary>Exactly four images with a cap of four are all blocks: the cap is inclusive.</summary>
    [Fact]
    public async Task Plan_FourImagesCapFour_AllBlocks()
    {
        PlanInput[] inputs = [.. Enumerable.Range(1, 4).Select(i => Input($"{i}.png"))];

        IReadOnlyList<PlannedDocument> plan = await PlanAsync(inputs, Delivery(maxImages: 4));

        Assert.All(plan, document => Assert.Equal(LibraryDocumentDelivery.Block, document.Delivery));
    }

    /// <summary>The fifth image, for an Adapter with no file tools, is Unavailable rather than a path line.</summary>
    [Fact]
    public async Task Plan_OverCountCapNoFileTools_IsUnavailable()
    {
        PlanInput[] inputs = [.. Enumerable.Range(1, 3).Select(i => Input($"{i}.png"))];

        IReadOnlyList<PlannedDocument> plan = await PlanAsync(inputs, Delivery(readsFiles: false, maxImages: 2));

        Assert.Equal(LibraryDocumentDelivery.Unavailable, plan[2].Delivery);
    }

    /// <summary>An image that would push the Turn past the byte cap is withheld, and is not read past it.</summary>
    [Fact]
    public async Task Plan_PerTurnBytesCap_WithholdsTheImageThatWouldExceedIt()
    {
        PlanInput[] inputs = [Input("a.png"), Input("b.png"), Input("c.png")];

        IReadOnlyList<PlannedDocument> plan = await PlanAsync(inputs, Delivery(maxBytes: 250), read: _ => Png(100));

        Assert.Equal(
            [LibraryDocumentDelivery.Block, LibraryDocumentDelivery.Block, LibraryDocumentDelivery.Path],
            plan.Select(document => document.Delivery));
        Assert.Equal(PromptBlockWithheld.TurnBytesCap, plan[2].Withheld);
    }

    /// <summary>A refused image does not use up the budget: a later, smaller image still fits.</summary>
    [Fact]
    public async Task Plan_RefusedImage_DoesNotConsumeBudget()
    {
        PlanInput[] inputs = [Input("bad.png"), Input("good.png")];

        IReadOnlyList<PlannedDocument> plan = await PlanAsync(
            inputs,
            Delivery(maxImages: 1),
            read: path => path.RelativePath == "bad.png" ? new LibraryImageRefused(LibraryImageRefusal.TooLarge) : Png());

        Assert.Equal([LibraryDocumentDelivery.Path, LibraryDocumentDelivery.Block], plan.Select(document => document.Delivery));
        Assert.Equal(PromptBlockWithheld.TooLarge, plan[0].Withheld);
    }

    /// <summary>An over-the-byte-budget image does not use up the count: a later, smaller image still fits.</summary>
    [Fact]
    public async Task Plan_OverBudgetImage_DoesNotConsumeCount()
    {
        PlanInput[] inputs = [Input("huge.png"), Input("small.png")];

        IReadOnlyList<PlannedDocument> plan = await PlanAsync(
            inputs,
            Delivery(maxImages: 1, maxBytes: 200),
            read: path => path.RelativePath == "huge.png" ? Png(500) : Png(50));

        Assert.Equal([LibraryDocumentDelivery.Path, LibraryDocumentDelivery.Block], plan.Select(document => document.Delivery));
    }

    /// <summary>Every refusal reason is carried through as the reason the image was withheld.</summary>
    /// <param name="refusal">The reader's refusal.</param>
    /// <param name="expected">The withheld reason the planner reports.</param>
    [Theory]
    [InlineData(LibraryImageRefusal.NotAnImage, PromptBlockWithheld.NotAnImage)]
    [InlineData(LibraryImageRefusal.TooLarge, PromptBlockWithheld.TooLarge)]
    [InlineData(LibraryImageRefusal.TooManyPixels, PromptBlockWithheld.TooManyPixels)]
    [InlineData(LibraryImageRefusal.Unreadable, PromptBlockWithheld.Unreadable)]
    public async Task Plan_Refusal_IsCarriedAsWithheldReason(LibraryImageRefusal refusal, PromptBlockWithheld expected)
    {
        IReadOnlyList<PlannedDocument> plan = await PlanAsync([Input("a.png")], Delivery(), read: _ => new LibraryImageRefused(refusal));

        PlannedDocument document = Assert.Single(plan);
        Assert.Equal(LibraryDocumentDelivery.Path, document.Delivery);
        Assert.Equal(expected, document.Withheld);
    }

    /// <summary>A reader that throws an <see cref="IOException"/> costs that one image, never the Turn: it is a path line, withheld as unreadable.</summary>
    [Fact]
    public async Task Plan_ReaderThrowsIOException_IsPathUnreadable()
    {
        IReadOnlyList<PlannedDocument> plan = await PromptBlockPlanner.PlanAsync(
            [Input("a.png"), Input("b.png")],
            Delivery(),
            (path, _) => path.RelativePath == "a.png"
                ? throw new IOException("locked")
                : Task.FromResult<LibraryImageResult>(Png()),
            TestContext.Current.CancellationToken);

        Assert.Equal([LibraryDocumentDelivery.Path, LibraryDocumentDelivery.Block], plan.Select(document => document.Delivery));
        Assert.Equal(PromptBlockWithheld.Unreadable, plan[0].Withheld);
    }

    /// <summary>A refused image for an Adapter with no file tools is Unavailable.</summary>
    [Fact]
    public async Task Plan_RefusalNoFileTools_IsUnavailable()
    {
        IReadOnlyList<PlannedDocument> plan = await PlanAsync(
            [Input("a.png")],
            Delivery(readsFiles: false),
            read: _ => new LibraryImageRefused(LibraryImageRefusal.TooLarge));

        Assert.Equal(LibraryDocumentDelivery.Unavailable, Assert.Single(plan).Delivery);
    }

    /// <summary>A cap of zero or less sends no images, and does not read one: the safe direction.</summary>
    /// <param name="maxImages">The count cap.</param>
    /// <param name="maxBytes">The byte cap.</param>
    [Theory]
    [InlineData(0, 1000)]
    [InlineData(-1, 1000)]
    [InlineData(4, 0)]
    [InlineData(4, -1)]
    public async Task Plan_NonPositiveCap_SendsNone(int maxImages, long maxBytes)
    {
        List<string> reads = [];

        IReadOnlyList<PlannedDocument> plan = await PlanAsync([Input("a.png")], Delivery(maxImages: maxImages, maxBytes: maxBytes), reads: reads);

        Assert.Equal(LibraryDocumentDelivery.Path, Assert.Single(plan).Delivery);
        Assert.Empty(reads);
    }

    /// <summary>Withheld is reported only for an image that was eligible and then refused, never for one the design excludes.</summary>
    [Fact]
    public async Task Plan_NotFromTrigger_ReportsNothingWithheld()
    {
        IReadOnlyList<PlannedDocument> plan = await PlanAsync([Input("a.png", fromTrigger: false)], Delivery());

        Assert.Equal(PromptBlockWithheld.None, Assert.Single(plan).Withheld);
    }

    /// <summary>Output order matches input order, mixing kinds.</summary>
    [Fact]
    public async Task Plan_MixedKinds_KeepInputOrder()
    {
        PlanInput[] inputs = [Input("a.md", LibraryFileKind.Markdown, text: "x"), Input("b.png"), Input("c.svg", LibraryFileKind.Svg)];

        IReadOnlyList<PlannedDocument> plan = await PlanAsync(inputs, Delivery());

        Assert.Equal(
            [LibraryDocumentDelivery.Path, LibraryDocumentDelivery.Block, LibraryDocumentDelivery.Path],
            plan.Select(document => document.Delivery));
    }

    /// <summary>A cancelled token stops planning with <see cref="OperationCanceledException"/>.</summary>
    [Fact]
    public async Task Plan_Cancelled_Throws()
    {
        using CancellationTokenSource cancelled = new();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => PromptBlockPlanner.PlanAsync(
                [Input("a.png")],
                Delivery(),
                (_, token) => Task.FromResult<LibraryImageResult>(Png()).WaitAsync(token),
                cancelled.Token));
    }

    // ---- Phase 5: a document's text as an embedded resource ----

    /// <summary>For an Adapter with no file tools that takes embedded context, Markdown from the trigger is a text-resource block with a file URI.</summary>
    [Fact]
    public async Task Plan_MarkdownEmbeddedText_IsResourceBlock()
    {
        IReadOnlyList<PlannedDocument> plan = await PlanAsync(
            [Input("notes.md", LibraryFileKind.Markdown, text: "# t")],
            Delivery(readsFiles: false, images: false, embeddedText: true));

        PlannedDocument document = Assert.Single(plan);
        Assert.Equal(LibraryDocumentDelivery.Block, document.Delivery);
        AgentTextResourceBlock block = Assert.IsType<AgentTextResourceBlock>(document.Block);
        Assert.Equal(new Uri(@"E:\Docs\notes.md").AbsoluteUri, block.Uri);
        Assert.Equal("text/markdown", block.MimeType);
        Assert.Equal("# t", block.Text);
    }

    /// <summary>A plain text file is a <c>text/plain</c> resource.</summary>
    [Fact]
    public async Task Plan_TextEmbeddedText_IsPlainTextResource()
    {
        IReadOnlyList<PlannedDocument> plan = await PlanAsync(
            [Input("n.txt", LibraryFileKind.Text, text: "hi")],
            Delivery(readsFiles: false, images: false, embeddedText: true));

        Assert.Equal("text/plain", Assert.IsType<AgentTextResourceBlock>(Assert.Single(plan).Block).MimeType);
    }

    /// <summary>The URI percent-encodes a space and a non-ASCII character.</summary>
    [Fact]
    public async Task Plan_ResourceUri_EncodesSpaceAndNonAscii()
    {
        PlanInput input = new(new LibraryPath(Root, "notes v2 é.md", @"E:\Docs\notes v2 é.md", LibraryNodeRole.File), LibraryFileKind.Markdown, true, "x");

        IReadOnlyList<PlannedDocument> plan = await PlanAsync([input], Delivery(readsFiles: false, images: false, embeddedText: true));

        string uri = Assert.IsType<AgentTextResourceBlock>(Assert.Single(plan).Block).Uri;
        Assert.Equal("file:///E:/Docs/notes%20v2%20%C3%A9.md", uri);
    }

    /// <summary>Without embedded context the text stays as today: a path line carrying the inline text.</summary>
    [Fact]
    public async Task Plan_MarkdownNoEmbeddedText_IsPath()
    {
        IReadOnlyList<PlannedDocument> plan = await PlanAsync(
            [Input("notes.md", LibraryFileKind.Markdown, text: "# t")],
            Delivery(readsFiles: false, images: false, embeddedText: false));

        Assert.Equal(LibraryDocumentDelivery.Path, Assert.Single(plan).Delivery);
    }

    /// <summary>Text from catch-up, not the trigger Message, is not a resource block: it stays inline.</summary>
    [Fact]
    public async Task Plan_MarkdownNotFromTrigger_IsPath()
    {
        IReadOnlyList<PlannedDocument> plan = await PlanAsync(
            [Input("notes.md", LibraryFileKind.Markdown, fromTrigger: false, text: "# t")],
            Delivery(readsFiles: false, images: false, embeddedText: true));

        Assert.Equal(LibraryDocumentDelivery.Path, Assert.Single(plan).Delivery);
    }

    /// <summary>A document whose text was not read (too large) has no text to embed and stays a path line.</summary>
    [Fact]
    public async Task Plan_MarkdownWithoutText_IsPath()
    {
        IReadOnlyList<PlannedDocument> plan = await PlanAsync(
            [Input("big.md", LibraryFileKind.Markdown, text: null)],
            Delivery(readsFiles: false, images: false, embeddedText: true));

        Assert.Equal(LibraryDocumentDelivery.Path, Assert.Single(plan).Delivery);
    }

    /// <summary>An Adapter that reads files never gets text as a resource, whatever <c>EmbeddedText</c> says.</summary>
    [Fact]
    public async Task Plan_MarkdownReadsFiles_IsNeverResource()
    {
        IReadOnlyList<PlannedDocument> plan = await PlanAsync(
            [Input("notes.md", LibraryFileKind.Markdown, text: "# t")],
            Delivery(readsFiles: true, images: false, embeddedText: true));

        Assert.Equal(LibraryDocumentDelivery.Path, Assert.Single(plan).Delivery);
    }
}
