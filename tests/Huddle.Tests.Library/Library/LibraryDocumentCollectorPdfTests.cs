using Microsoft.Extensions.Options;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App;
using Agency.Huddle.App.Library;

namespace Agency.Huddle.Tests.Library;

/// <summary>
/// Pins Spec §3 end to end at the collector: a PDF written in the Message that started the Turn becomes
/// a blob block for an Adapter with no file tools that takes documents, read through the Library file
/// service under the document cap, not the image cap.
/// </summary>
public sealed class LibraryDocumentCollectorPdfTests
{
    private static readonly PromptDelivery DocumentsOn = new(ReadsFiles: false, Images: false, EmbeddedText: false, MaxImagesPerTurn: 4, MaxImageBytesPerTurn: 8 * 1024 * 1024, Documents: true, MaxDocumentsPerTurn: 2, MaxDocumentBytesPerTurn: 10485760);

    private const string WindowsPathOnly = "Plain text finds only a drive, UNC or file:// path mid-line; a POSIX absolute path is found only in a code span.";

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

    /// <summary>A PDF named in the trigger Message becomes one blob block carrying the file's own bytes.</summary>
    [Fact]
    public async Task Collect_PdfInTrigger_IsABlobBlock()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (LibraryFileServiceFixture fixture, LibraryDocumentCollector collector, string root) = Build();
        using LibraryFileServiceFixture _ = fixture;
        string pdf = Path.Combine(root, "report.pdf");
        File.WriteAllBytes(pdf, "%PDF-1.4\n%fake\n"u8.ToArray());

        LibraryDocumentsReport report = await collector.CollectAsync([$"summarise {Link(pdf)}"], DocumentsOn, ct);

        LibraryDocumentItem item = Assert.Single(report.Items);
        Assert.Equal(LibraryDocumentDelivery.Block, item.Delivery);
        AgentBlobResourceBlock block = Assert.IsType<AgentBlobResourceBlock>(item.Block);
        Assert.Equal(File.ReadAllBytes(pdf), block.Data.ToArray());
    }

    /// <summary>A PDF named mid-line, followed by more words, is found and becomes a blob block.</summary>
    [Fact]
    public async Task Collect_PdfNamedMidLine_IsABlobBlock()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), WindowsPathOnly);
        CancellationToken ct = TestContext.Current.CancellationToken;
        (LibraryFileServiceFixture fixture, LibraryDocumentCollector collector, string root) = Build();
        using LibraryFileServiceFixture _ = fixture;
        string pdf = Path.Combine(root, "report.pdf");
        File.WriteAllBytes(pdf, "%PDF-1.4\n%fake\n"u8.ToArray());

        LibraryDocumentsReport report = await collector.CollectAsync([$"read {pdf} please"], DocumentsOn, ct);

        LibraryDocumentItem item = Assert.Single(report.Items);
        Assert.Equal(LibraryDocumentDelivery.Block, item.Delivery);
        Assert.IsType<AgentBlobResourceBlock>(item.Block);
    }

    /// <summary>A mid-line <c>.pdf</c> path that does not exist is still not collected.</summary>
    [Fact]
    public async Task Collect_MissingPdfNamedMidLine_CollectsNothing()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), WindowsPathOnly);
        CancellationToken ct = TestContext.Current.CancellationToken;
        (LibraryFileServiceFixture fixture, LibraryDocumentCollector collector, string root) = Build();
        using LibraryFileServiceFixture _ = fixture;
        string pdf = Path.Combine(root, "missing.pdf");

        LibraryDocumentsReport report = await collector.CollectAsync([$"read {pdf} please"], DocumentsOn, ct);

        Assert.Empty(report.Items);
    }

    /// <summary>A 4 MiB PDF is over a 1 MiB image cap yet still a block, because the document cap governs it.</summary>
    [Fact]
    public async Task Collect_PdfOverMaxImageBytes_UsesDocumentCapNotImageCap()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (LibraryFileServiceFixture fixture, LibraryDocumentCollector collector, string root) = Build(options => options.Library.MaxImageBytes = 1024 * 1024);
        using LibraryFileServiceFixture _ = fixture;
        string pdf = Path.Combine(root, "big.pdf");
        byte[] bytes = new byte[4 * 1024 * 1024];
        "%PDF-1.4\n"u8.CopyTo(bytes);
        File.WriteAllBytes(pdf, bytes);

        LibraryDocumentsReport report = await collector.CollectAsync([Link(pdf)], DocumentsOn, ct);

        LibraryDocumentItem item = Assert.Single(report.Items);
        AgentBlobResourceBlock block = Assert.IsType<AgentBlobResourceBlock>(item.Block);
        Assert.Equal(bytes.Length, block.Data.Length);
    }

    /// <summary>A PDF over the document cap is a path line withheld as TurnBytesCap, as Spec §3 says.</summary>
    [Fact]
    public async Task Collect_PdfOverDocumentCap_IsPathWithTurnBytesCap()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (LibraryFileServiceFixture fixture, LibraryDocumentCollector collector, string root) = Build();
        using LibraryFileServiceFixture _ = fixture;
        string pdf = Path.Combine(root, "big.pdf");
        File.WriteAllBytes(pdf, "%PDF-1.4\n0123456789"u8.ToArray());

        LibraryDocumentsReport report = await collector.CollectAsync([Link(pdf)], DocumentsOn with { MaxDocumentBytesPerTurn = 10 }, ct);

        LibraryDocumentItem item = Assert.Single(report.Items);
        Assert.Equal(LibraryDocumentDelivery.Path, item.Delivery);
        Assert.Equal(PromptBlockWithheld.TurnBytesCap, item.Withheld);
    }
}
