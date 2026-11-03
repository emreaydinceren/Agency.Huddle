using Agency.Huddle.App.Library;

namespace Agency.Huddle.Tests.Library;

/// <summary>
/// Tests for <see cref="LibraryFileService.ReadDocumentAsync"/>: a PDF's bytes when it is within the
/// cap and really is one, and a refusal that is data, never an exception, when it is not.
/// </summary>
public sealed class LibraryFileServicePdfTests
{
    private static readonly byte[] Pdf = "%PDF-1.4\n%fake\n"u8.ToArray();

    /// <summary>A PDF within the cap returns its own bytes.</summary>
    [Fact]
    public async Task ReadDocument_Pdf_ReturnsBytes()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        File.WriteAllBytes(Path.Combine(vault, "a.pdf"), Pdf);
        LibraryFileService service = fixture.CreateService();

        LibraryDocumentResult result = await service.ReadDocumentAsync(fixture.Resolve(vault, "a.pdf"), 1024, ct);

        Assert.Equal(Pdf, Assert.IsType<LibraryDocumentRead>(result).Bytes);
    }

    /// <summary>A PDF exactly at the cap is read; one byte over is refused as too large.</summary>
    [Fact]
    public async Task ReadDocument_AtAndOverCap_ReadsThenRefuses()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        File.WriteAllBytes(Path.Combine(vault, "a.pdf"), Pdf);
        LibraryFileService service = fixture.CreateService();

        LibraryDocumentResult atCap = await service.ReadDocumentAsync(fixture.Resolve(vault, "a.pdf"), Pdf.Length, ct);
        LibraryDocumentResult over = await service.ReadDocumentAsync(fixture.Resolve(vault, "a.pdf"), Pdf.Length - 1, ct);

        Assert.IsType<LibraryDocumentRead>(atCap);
        Assert.Equal(LibraryDocumentRefusal.TooLarge, Assert.IsType<LibraryDocumentRefused>(over).Reason);
    }

    /// <summary>A budget of zero or less accepts nothing.</summary>
    [Fact]
    public async Task ReadDocument_ZeroBudget_IsTooLarge()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        File.WriteAllBytes(Path.Combine(vault, "a.pdf"), Pdf);
        LibraryFileService service = fixture.CreateService();

        LibraryDocumentResult result = await service.ReadDocumentAsync(fixture.Resolve(vault, "a.pdf"), 0, ct);

        Assert.Equal(LibraryDocumentRefusal.TooLarge, Assert.IsType<LibraryDocumentRefused>(result).Reason);
    }

    /// <summary>A file named <c>.pdf</c> without the <c>%PDF-</c> magic is unreadable as a document.</summary>
    [Fact]
    public async Task ReadDocument_NoMagic_IsUnreadable()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        File.WriteAllText(Path.Combine(vault, "fake.pdf"), "<html>hi</html>");
        LibraryFileService service = fixture.CreateService();

        LibraryDocumentResult result = await service.ReadDocumentAsync(fixture.Resolve(vault, "fake.pdf"), 1024, ct);

        Assert.Equal(LibraryDocumentRefusal.Unreadable, Assert.IsType<LibraryDocumentRefused>(result).Reason);
    }

    /// <summary>A file deleted after the path was resolved is unreadable, not an exception.</summary>
    [Fact]
    public async Task ReadDocument_FileGone_IsUnreadable()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        string file = Path.Combine(vault, "gone.pdf");
        File.WriteAllBytes(file, Pdf);
        LibraryPath path = fixture.Resolve(vault, "gone.pdf");
        File.Delete(file);
        LibraryFileService service = fixture.CreateService();

        LibraryDocumentResult result = await service.ReadDocumentAsync(path, 1024, ct);

        Assert.Equal(LibraryDocumentRefusal.Unreadable, Assert.IsType<LibraryDocumentRefused>(result).Reason);
    }

    /// <summary>
    /// A real binary PDF, under or over the 2 MiB editing cap, reads like an unrecognised file: kind
    /// <see cref="LibraryFileKind.Pdf"/>, no text and no view-only reason, so the viewer shows its card.
    /// </summary>
    [Theory]
    [InlineData(1024)]
    [InlineData(3 * 1024 * 1024)]
    public async Task Read_BinaryPdf_ReadsLikeAnOtherFile(int size)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        byte[] bytes = new byte[size];
        Array.Fill(bytes, (byte)0x80);
        Pdf.CopyTo(bytes, 0);
        File.WriteAllBytes(Path.Combine(vault, "a.pdf"), bytes);
        LibraryFileService service = fixture.CreateService();

        LibraryDocumentContent content = await service.ReadAsync(fixture.Resolve(vault, "a.pdf"), ct);

        Assert.Equal(LibraryFileKind.Pdf, content.Kind);
        Assert.Null(content.Text);
        Assert.Null(content.ViewOnlyReason);
        Assert.Equal(size, content.Length);
    }

    /// <summary>A cancelled token propagates as cancellation, not a refusal.</summary>
    [Fact]
    public async Task ReadDocument_Cancelled_Throws()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        File.WriteAllBytes(Path.Combine(vault, "a.pdf"), Pdf);
        LibraryPath path = fixture.Resolve(vault, "a.pdf");
        LibraryFileService service = fixture.CreateService();
        using CancellationTokenSource cancelled = new();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ReadDocumentAsync(path, 1024, cancelled.Token));
    }
}
