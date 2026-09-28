using Bunit;
using Microsoft.AspNetCore.Components.Rendering;
using Agency.Huddle.App.Components.Library;
using Agency.Huddle.App.Library;
using Agency.Huddle.Tests.Library;

namespace Agency.Huddle.Tests.Ui.Library;

/// <summary>
/// Pins the Backlinks expansion panel (Task 12.4.t, Spec §6.5, §8 Expansion Panels row).
/// </summary>
public sealed class BacklinksPanelTests : IDisposable
{
    private readonly BacklinksFixture fixture = BacklinksFixture.Build();

    /// <summary>Disposes the underlying temp <c>DataDir</c> and stores.</summary>
    public void Dispose() => this.fixture.Dispose();

    /// <summary>The panel title shows "Linked from 2 notes" when two distinct notes contain backlinks.</summary>
    [Fact]
    public void Title_ShowsCount_TwoNotes()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        LibraryPath note1 = this.fixture.LibraryFixture.Resolve(root, "note1.md");
        LibraryPath note2 = this.fixture.LibraryFixture.Resolve(root, "note2.md");

        var backlinks = new[]
        {
            new LibraryBacklinkRow(note1, 1, "Link to [[target]]"),
            new LibraryBacklinkRow(note2, 5, "Another [[target]] here"),
        };

        using BunitContext ctx = new();
        var cut = ctx.Render(builder => RenderBacklinksPanel(builder, (IReadOnlyList<LibraryBacklinkRow>)backlinks, true));

        var content = cut.Markup;
        Assert.Contains("Linked from 2 notes", content);
    }

    /// <summary>The panel title shows "Linked from 1 note" when exactly one note contains backlinks (singular).</summary>
    [Fact]
    public void Title_ShowsCount_OneNote()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        LibraryPath note1 = this.fixture.LibraryFixture.Resolve(root, "note1.md");

        var backlinks = new[]
        {
            new LibraryBacklinkRow(note1, 1, "Link to [[target]]"),
        };

        using BunitContext ctx = new();
        var cut = ctx.Render(builder => RenderBacklinksPanel(builder, (IReadOnlyList<LibraryBacklinkRow>)backlinks, true));

        var content = cut.Markup;
        Assert.Contains("Linked from 1 note", content);
    }

    /// <summary>The panel title shows "No notes link here" when no backlinks exist.</summary>
    [Fact]
    public void Title_ShowsCount_NoNotes()
    {
        using BunitContext ctx = new();
        var emptyBacklinks = (IReadOnlyList<LibraryBacklinkRow>)Array.Empty<LibraryBacklinkRow>();
        var cut = ctx.Render(builder => RenderBacklinksPanel(builder, emptyBacklinks, true));

        var content = cut.Markup;
        Assert.Contains("No notes link here", content);
    }

    /// <summary>The panel is collapsed by default.</summary>
    [Fact]
    public void CollapsedByDefault()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        LibraryPath note1 = this.fixture.LibraryFixture.Resolve(root, "note1.md");
        var backlinks = new[] { new LibraryBacklinkRow(note1, 1, "Link") };

        using BunitContext ctx = new();
        var cut = ctx.Render(builder => RenderBacklinksPanel(builder, (IReadOnlyList<LibraryBacklinkRow>)backlinks, true));

        var content = cut.Markup;
        Assert.DoesNotContain("mud-expanded", content);
    }

    /// <summary>Each backlink row shows the note's path and line text.</summary>
    [Fact]
    public void Row_ShowsNotePathAndLineText()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        LibraryPath note = this.fixture.LibraryFixture.Resolve(root, "sub/page.md");

        var backlinks = new[]
        {
            new LibraryBacklinkRow(note, 42, "This is the line with [[the link]]"),
        };

        using BunitContext ctx = new();
        var cut = ctx.Render(builder => RenderBacklinksPanel(builder, (IReadOnlyList<LibraryBacklinkRow>)backlinks, true));

        var content = cut.Markup;
        Assert.Contains("sub/page.md", content);
        Assert.Contains("This is the line with [[the link]]", content);
    }

    /// <summary>Two backlinks from the same note on different lines show "Linked from 1 note" (counts notes, not lines).</summary>
    [Fact]
    public void Title_TwoLinesFromOneNote_CountsAsOneNote()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        LibraryPath note1 = this.fixture.LibraryFixture.Resolve(root, "note1.md");

        var backlinks = new[]
        {
            new LibraryBacklinkRow(note1, 1, "First [[link]] here"),
            new LibraryBacklinkRow(note1, 5, "Second [[link]] here"),
        };

        using BunitContext ctx = new();
        var cut = ctx.Render(builder => RenderBacklinksPanel(builder, (IReadOnlyList<LibraryBacklinkRow>)backlinks, true));

        var content = cut.Markup;
        Assert.Contains("Linked from 1 note", content);
    }

    /// <summary>Two backlinks from the same note on different lines render as two separate rows.</summary>
    [Fact]
    public void Rows_FromSameNote_RenderBoth()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        LibraryPath note1 = this.fixture.LibraryFixture.Resolve(root, "note1.md");

        var backlinks = new[]
        {
            new LibraryBacklinkRow(note1, 1, "First [[link]] here"),
            new LibraryBacklinkRow(note1, 5, "Second [[link]] here"),
        };

        using BunitContext ctx = new();
        var cut = ctx.Render(builder => RenderBacklinksPanel(builder, (IReadOnlyList<LibraryBacklinkRow>)backlinks, true));

        var content = cut.Markup;
        Assert.Contains("First [[link]] here", content);
        Assert.Contains("Second [[link]] here", content);
    }

    /// <summary>When IsAvailable is false, only the unavailable text is shown, not the count title.</summary>
    [Fact]
    public void IndexUnavailable_ShowsText()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        LibraryPath note1 = this.fixture.LibraryFixture.Resolve(root, "note1.md");

        var backlinks = new[]
        {
            new LibraryBacklinkRow(note1, 1, "Link [[here]]"),
        };

        using BunitContext ctx = new();
        var cut = ctx.Render(builder => RenderBacklinksPanel(builder, (IReadOnlyList<LibraryBacklinkRow>)backlinks, false));

        var content = cut.Markup;
        Assert.Contains("Backlinks aren't available for this folder: it has too many files.", content);
        Assert.DoesNotContain("Linked from", content);
    }

    /// <summary>Clicking a backlink row raises the OnNavigate callback with the note's path.</summary>
    [Fact]
    public void Click_RaisesOnNavigate()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        LibraryPath note = this.fixture.LibraryFixture.Resolve(root, "page.md");
        LibraryPath? navigatedTo = null;

        var backlinks = new[]
        {
            new LibraryBacklinkRow(note, 1, "Link [[here]]"),
        };

        using BunitContext ctx = new();
        var cut = ctx.Render(builder => RenderBacklinksPanel(builder, (IReadOnlyList<LibraryBacklinkRow>)backlinks, true, path => navigatedTo = path));

        var button = cut.Find("button.backlink-button");
        button.Click();

        Assert.NotNull(navigatedTo);
        Assert.Equal(note, navigatedTo);
    }

    private static void RenderBacklinksPanel(
        RenderTreeBuilder builder,
        IReadOnlyList<LibraryBacklinkRow> backlinks,
        bool isAvailable,
        Action<LibraryPath>? onNavigate = null)
    {
        builder.OpenComponent<BacklinksPanel>(0);
        builder.AddAttribute(1, nameof(BacklinksPanel.Backlinks), backlinks);
        builder.AddAttribute(2, nameof(BacklinksPanel.IsAvailable), isAvailable);
        if (onNavigate is not null)
        {
            var handler = new Action<LibraryPath>(onNavigate);
            builder.AddAttribute(3, nameof(BacklinksPanel.OnNavigate), Microsoft.AspNetCore.Components.EventCallback.Factory.Create<LibraryPath>(new object(), handler));
        }
        builder.CloseComponent();
    }

    /// <summary>An isolated fixture for BacklinksPanel tests with a real <see cref="LibraryFileServiceFixture"/>.</summary>
    private sealed class BacklinksFixture : IDisposable
    {
        private BacklinksFixture(LibraryFileServiceFixture libraryFixture) => this.LibraryFixture = libraryFixture;

        /// <summary>The real Library stack over this fixture's temp <c>DataDir</c>.</summary>
        public LibraryFileServiceFixture LibraryFixture { get; }

        /// <summary>Builds a fixture with the standard layout.</summary>
        public static BacklinksFixture Build() => new(LibraryFileServiceFixture.Build());

        /// <summary>Disposes the underlying stores and temp <c>DataDir</c>.</summary>
        public void Dispose() => this.LibraryFixture.Dispose();
    }
}
