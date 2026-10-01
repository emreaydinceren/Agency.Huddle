using Microsoft.Extensions.Options;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App;
using Agency.Huddle.App.Library;

namespace Agency.Huddle.Tests.Library;

/// <summary>
/// Pins design §6.4 and §8.2 at the collector: which of a Turn's Library documents become Prompt
/// blocks. Only a path written in the Message that started the Turn is eligible, only a real raster
/// image or (for an Adapter with no file tools) text is ever a block, and every other path keeps today's line.
/// </summary>
public sealed class LibraryDocumentCollectorPromptBlockTests
{
    private static readonly PromptDelivery ImagesOn = new(ReadsFiles: true, Images: true, EmbeddedText: false, MaxImagesPerTurn: 4, MaxImageBytesPerTurn: 8 * 1024 * 1024);

    /// <summary>A path as a <c>file://</c> URL: the one form of an absolute path the collector recognises on every platform, so a test written on Windows also finds its files on Linux.</summary>
    private static string Link(string path) => new Uri(path).AbsoluteUri;

    private static (LibraryFileServiceFixture Fixture, LibraryDocumentCollector Collector, string Root) Build(Action<TeamOptions>? configure = null)
    {
        TeamOptions captured = new();
        LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build(options =>
        {
            configure?.Invoke(options);
            captured = options;
        });
        string root = fixture.CreatePinnedRoot("Docs");
        LibraryDocumentCollector collector = new(fixture.Resolver, fixture.CreateService(), Options.Create(captured));
        return (fixture, collector, root);
    }

    private static string WritePng(string root, string name, int width = 40, int height = 30, int padding = 50)
    {
        string path = Path.Combine(root, name);
        File.WriteAllBytes(path, TestImages.Png(width, height, padding));
        return path;
    }

    /// <summary>A PNG written in the Message that started the Turn becomes a block carrying the file's own bytes.</summary>
    [Fact]
    public async Task Collect_ImageInTriggerMessage_IsBlock()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (LibraryFileServiceFixture fixture, LibraryDocumentCollector collector, string root) = Build();
        using LibraryFileServiceFixture _ = fixture;
        string png = WritePng(root, "shot.png");

        LibraryDocumentsReport report = await collector.CollectAsync([$"what is wrong with {Link(png)}"], ImagesOn, ct);

        LibraryDocumentItem item = Assert.Single(report.Items);
        Assert.Equal(LibraryDocumentDelivery.Block, item.Delivery);
        AgentImageBlock block = Assert.IsType<AgentImageBlock>(item.Block);
        Assert.Equal("image/png", block.MimeType);
        Assert.Equal(File.ReadAllBytes(png), block.Data.ToArray());
        Assert.Equal(png, item.FullPath, StringComparer.OrdinalIgnoreCase);
        Assert.Equal("pinned root \"Docs\"", item.Location);
    }

    /// <summary>A PNG mentioned only in catch-up, not the Message that started the Turn, stays a path line (D-2).</summary>
    [Fact]
    public async Task Collect_ImageOnlyInCatchUp_IsPath()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (LibraryFileServiceFixture fixture, LibraryDocumentCollector collector, string root) = Build();
        using LibraryFileServiceFixture _ = fixture;
        string png = WritePng(root, "old.png");

        LibraryDocumentsReport report = await collector.CollectAsync(["no paths here", $"earlier: {Link(png)}"], ImagesOn, ct);

        LibraryDocumentItem item = Assert.Single(report.Items);
        Assert.Equal(LibraryDocumentDelivery.Path, item.Delivery);
        Assert.Null(item.Block);
    }

    /// <summary>The same PNG in catch-up and in the trigger Message is eligible: first-seen order must not hide that it was also written in the Message.</summary>
    [Fact]
    public async Task Collect_ImageInCatchUpAndTrigger_IsBlock()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (LibraryFileServiceFixture fixture, LibraryDocumentCollector collector, string root) = Build();
        using LibraryFileServiceFixture _ = fixture;
        string png = WritePng(root, "both.png");

        LibraryDocumentsReport report = await collector.CollectAsync([$"look {Link(png)}", $"earlier: {Link(png)}"], ImagesOn, ct);

        Assert.Equal(LibraryDocumentDelivery.Block, Assert.Single(report.Items).Delivery);
    }

    /// <summary>The trigger Message's image is listed after one first written in catch-up only when that is the order of the texts: the trigger text is scanned first.</summary>
    [Fact]
    public async Task Collect_TriggerImageAndCatchUpImage_OnlyTriggerIsBlock()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (LibraryFileServiceFixture fixture, LibraryDocumentCollector collector, string root) = Build();
        using LibraryFileServiceFixture _ = fixture;
        string current = WritePng(root, "current.png");
        string old = WritePng(root, "old.png");

        LibraryDocumentsReport report = await collector.CollectAsync([$"see {Link(current)}", $"before: {Link(old)}"], ImagesOn, ct);

        Assert.Equal(
            [LibraryDocumentDelivery.Block, LibraryDocumentDelivery.Path],
            report.Items.Select(item => item.Delivery));
    }

    /// <summary>The same path written twice in one Message is one block (P12).</summary>
    [Fact]
    public async Task Collect_SameImageTwice_IsOneBlock()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (LibraryFileServiceFixture fixture, LibraryDocumentCollector collector, string root) = Build();
        using LibraryFileServiceFixture _ = fixture;
        string png = WritePng(root, "twice.png");

        LibraryDocumentsReport report = await collector.CollectAsync([$"{Link(png)} and again {Link(png)}"], ImagesOn, ct);

        Assert.Single(report.Items);
        Assert.Single(report.Items, item => item.Block is not null);
    }

    /// <summary>An SVG, a PDF and a file of unknown kind named in the Message are never blocks.</summary>
    [Fact]
    public async Task Collect_SvgPdfAndOther_AreNeverBlocks()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (LibraryFileServiceFixture fixture, LibraryDocumentCollector collector, string root) = Build();
        using LibraryFileServiceFixture _ = fixture;
        string svg = Path.Combine(root, "mock.svg");
        string pdf = Path.Combine(root, "report.pdf");
        string other = Path.Combine(root, "data.bin");
        File.WriteAllText(svg, "<svg width=\"10\" height=\"10\"></svg>");
        File.WriteAllBytes(pdf, [0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34, 0x00]);
        File.WriteAllBytes(other, [0x00, 0x01, 0x02, 0x03]);

        LibraryDocumentsReport report = await collector.CollectAsync([$"`{Link(svg)}`\n`{Link(pdf)}`\n`{Link(other)}`"], ImagesOn, ct);

        Assert.Equal(3, report.Items.Count);
        Assert.All(report.Items, item => Assert.Equal(LibraryDocumentDelivery.Path, item.Delivery));
        Assert.All(report.Items, item => Assert.Null(item.Block));
    }

    /// <summary>A file named <c>.png</c> that holds HTML is classified Other by the Library, so it is a path line, never a block (P10).</summary>
    [Fact]
    public async Task Collect_TextNamedPng_IsPath()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (LibraryFileServiceFixture fixture, LibraryDocumentCollector collector, string root) = Build();
        using LibraryFileServiceFixture _ = fixture;
        string fake = Path.Combine(root, "shot.png");
        File.WriteAllText(fake, "<html>hi</html>");

        LibraryDocumentsReport report = await collector.CollectAsync([$"{Link(fake)}"], ImagesOn, ct);

        LibraryDocumentItem item = Assert.Single(report.Items);
        Assert.Equal(LibraryDocumentDelivery.Path, item.Delivery);
    }

    /// <summary>A path outside every Library Root adds nothing, image or not.</summary>
    [Fact]
    public async Task Collect_ImageOutsideEveryRoot_YieldsNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (LibraryFileServiceFixture fixture, LibraryDocumentCollector collector, string root) = Build();
        using LibraryFileServiceFixture _ = fixture;
        using TempDataDir outside = new();
        string png = WritePng(outside.Path, "secret.png");

        LibraryDocumentsReport report = await collector.CollectAsync([$"look at {Link(png)}"], ImagesOn, ct);

        Assert.NotEqual(root, Path.GetDirectoryName(png));
        Assert.True(report.IsEmpty);
    }

    /// <summary>With no file tools and images on, the image is a block; with images off it is Unavailable.</summary>
    /// <param name="images">Whether the Adapter takes images.</param>
    /// <param name="expected">The expected delivery.</param>
    [Theory]
    [InlineData(true, LibraryDocumentDelivery.Block)]
    [InlineData(false, LibraryDocumentDelivery.Unavailable)]
    public async Task Collect_NoFileTools_ImageDelivery(bool images, LibraryDocumentDelivery expected)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (LibraryFileServiceFixture fixture, LibraryDocumentCollector collector, string root) = Build();
        using LibraryFileServiceFixture _ = fixture;
        string png = WritePng(root, "shot.png");
        PromptDelivery delivery = new(ReadsFiles: false, Images: images, EmbeddedText: false, 4, 8 * 1024 * 1024);

        LibraryDocumentsReport report = await collector.CollectAsync([$"{Link(png)}"], delivery, ct);

        Assert.Equal(expected, Assert.Single(report.Items).Delivery);
    }

    /// <summary>An image over <see cref="LibraryOptions.MaxImageBytes"/> is not a block, and the item says why it was withheld.</summary>
    [Fact]
    public async Task Collect_ImageOverMaxImageBytes_IsPathWithReason()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (LibraryFileServiceFixture fixture, LibraryDocumentCollector collector, string root) = Build(options => options.Library.MaxImageBytes = 100);
        using LibraryFileServiceFixture _ = fixture;
        string png = WritePng(root, "big.png", padding: 500);

        LibraryDocumentsReport report = await collector.CollectAsync([$"{Link(png)}"], ImagesOn, ct);

        LibraryDocumentItem item = Assert.Single(report.Items);
        Assert.Equal(LibraryDocumentDelivery.Path, item.Delivery);
        Assert.Equal(PromptBlockWithheld.TooLarge, item.Withheld);
    }

    /// <summary>An image over <see cref="LibraryOptions.MaxImageEdgePixels"/> is not a block.</summary>
    [Fact]
    public async Task Collect_ImageOverEdgeCap_IsPathWithReason()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (LibraryFileServiceFixture fixture, LibraryDocumentCollector collector, string root) = Build(options => options.Library.MaxImageEdgePixels = 100);
        using LibraryFileServiceFixture _ = fixture;
        string png = WritePng(root, "wide.png", width: 101, height: 10);

        LibraryDocumentsReport report = await collector.CollectAsync([$"{Link(png)}"], ImagesOn, ct);

        Assert.Equal(PromptBlockWithheld.TooManyPixels, Assert.Single(report.Items).Withheld);
    }

    /// <summary>Six images with a cap of four: the first four are blocks and the other two are path lines.</summary>
    [Fact]
    public async Task Collect_SixImagesCapFour_FourBlocks()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (LibraryFileServiceFixture fixture, LibraryDocumentCollector collector, string root) = Build();
        using LibraryFileServiceFixture _ = fixture;
        string[] paths = [.. Enumerable.Range(1, 6).Select(i => WritePng(root, $"{i}.png"))];

        LibraryDocumentsReport report = await collector.CollectAsync([string.Join(' ', paths.Select(Link))], ImagesOn with { MaxImagesPerTurn = 4 }, ct);

        Assert.Equal(6, report.Items.Count);
        Assert.Equal(4, report.Items.Count(item => item.Block is not null));
        Assert.Equal(
            [PromptBlockWithheld.ImageCountCap, PromptBlockWithheld.ImageCountCap],
            report.Items.Skip(4).Select(item => item.Withheld));
    }

    /// <summary>The existing overload takes no part in Prompt blocks: an image for an Adapter with no file tools is still today's path line, not Unavailable.</summary>
    [Fact]
    public async Task Collect_LegacyOverload_NeverPlansBlocks()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (LibraryFileServiceFixture fixture, LibraryDocumentCollector collector, string root) = Build();
        using LibraryFileServiceFixture _ = fixture;
        string png = WritePng(root, "shot.png");

        LibraryDocumentsReport report = await collector.CollectAsync([$"{Link(png)}"], readsFiles: false, ct);

        LibraryDocumentItem item = Assert.Single(report.Items);
        Assert.Equal(LibraryDocumentDelivery.Path, item.Delivery);
        Assert.Null(item.Block);
    }

    // ---- Phase 5 ----

    /// <summary>For an Adapter with no file tools that takes embedded context, Markdown from the Message becomes a text-resource block and is not also inlined.</summary>
    [Fact]
    public async Task Collect_MarkdownEmbeddedText_IsResourceBlockWithoutInlineText()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (LibraryFileServiceFixture fixture, LibraryDocumentCollector collector, string root) = Build();
        using LibraryFileServiceFixture _ = fixture;
        string notes = Path.Combine(root, "notes v2.md");
        File.WriteAllText(notes, "# Notes\nhello");
        PromptDelivery delivery = new(ReadsFiles: false, Images: false, EmbeddedText: true, 4, 8 * 1024 * 1024);

        LibraryDocumentsReport report = await collector.CollectAsync([$"`{Link(notes)}`"], delivery, ct);

        LibraryDocumentItem item = Assert.Single(report.Items);
        Assert.Equal(LibraryDocumentDelivery.Block, item.Delivery);
        AgentTextResourceBlock block = Assert.IsType<AgentTextResourceBlock>(item.Block);
        Assert.Equal(new Uri(notes).AbsoluteUri, block.Uri);
        Assert.Equal("text/markdown", block.MimeType);
        Assert.Equal("# Notes\nhello", block.Text);
        Assert.Null(item.Text);
        Assert.False(item.Truncated);
    }

    /// <summary>A long document is cut at <see cref="LibraryOptions.MaxInlineBytes"/> exactly as inline text is, and the cut is recorded for the note that follows its line.</summary>
    [Fact]
    public async Task Collect_LongMarkdownEmbeddedText_IsCutAndMarkedTruncated()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (LibraryFileServiceFixture fixture, LibraryDocumentCollector collector, string root) = Build(options => options.Library.MaxInlineBytes = 10);
        using LibraryFileServiceFixture _ = fixture;
        string notes = Path.Combine(root, "long.md");
        File.WriteAllText(notes, new string('x', 40));
        PromptDelivery delivery = new(ReadsFiles: false, Images: false, EmbeddedText: true, 4, 8 * 1024 * 1024);

        LibraryDocumentsReport report = await collector.CollectAsync([$"{Link(notes)}"], delivery, ct);

        LibraryDocumentItem item = Assert.Single(report.Items);
        Assert.Equal(new string('x', 10), Assert.IsType<AgentTextResourceBlock>(item.Block).Text);
        Assert.True(item.Truncated);
    }

    /// <summary>Without embedded context the text is inlined as today.</summary>
    [Fact]
    public async Task Collect_MarkdownNoEmbeddedText_IsInlinedAsToday()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (LibraryFileServiceFixture fixture, LibraryDocumentCollector collector, string root) = Build();
        using LibraryFileServiceFixture _ = fixture;
        string notes = Path.Combine(root, "notes.md");
        File.WriteAllText(notes, "hello");
        PromptDelivery delivery = new(ReadsFiles: false, Images: false, EmbeddedText: false, 4, 8 * 1024 * 1024);

        LibraryDocumentsReport report = await collector.CollectAsync([$"{Link(notes)}"], delivery, ct);

        LibraryDocumentItem item = Assert.Single(report.Items);
        Assert.Equal(LibraryDocumentDelivery.Path, item.Delivery);
        Assert.Equal("hello", item.Text);
    }

    /// <summary>An Adapter that reads files is never sent a document's text, as a block or inline.</summary>
    [Fact]
    public async Task Collect_MarkdownReadsFiles_HasNoTextAndNoBlock()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (LibraryFileServiceFixture fixture, LibraryDocumentCollector collector, string root) = Build();
        using LibraryFileServiceFixture _ = fixture;
        string notes = Path.Combine(root, "notes.md");
        File.WriteAllText(notes, "hello");
        PromptDelivery delivery = new(ReadsFiles: true, Images: true, EmbeddedText: true, 4, 8 * 1024 * 1024);

        LibraryDocumentsReport report = await collector.CollectAsync([$"{Link(notes)}"], delivery, ct);

        LibraryDocumentItem item = Assert.Single(report.Items);
        Assert.Null(item.Block);
        Assert.Null(item.Text);
    }
}
