using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Agency.Huddle.App;
using Agency.Huddle.App.Components.Library;
using Agency.Huddle.App.Components.Sidebar;
using Agency.Huddle.App.DocHistory;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Sidebar;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Library;
using static Agency.Huddle.Tests.DocHistory.DocHistoryTestPaths;

namespace Agency.Huddle.Tests.Ui.DocHistory;

/// <summary>Pins what a Human sees of a document's history: the byline, the History at the bottom and the History panel (DocHistory spec §6.7, §6.8, §6.10).</summary>
public sealed class DocHistoryComponentsTests
{
    private const string TabId = "t1";

    private static readonly DateTimeOffset Now = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    /// <summary>With no history the byline renders nothing at all, not "No history".</summary>
    [Fact]
    public async Task Byline_NoHistory_RendersNothing()
    {
        using Harness harness = new();
        await using MudBunitContext ctx = harness.NewContext();

        IRenderedComponent<DocumentByline> cut = ctx.Render<DocumentByline>(p => p.Add(c => c.Path, Document("findings.md")));

        Assert.Empty(cut.FindAll(".doc-byline"));
    }

    /// <summary>The byline names who wrote, edited and reviewed, with a relative time, and a click raises the callback.</summary>
    [Fact]
    public async Task Byline_ShowsWhoDidWhat_AndRaisesOpenHistory()
    {
        using Harness harness = new();
        await using MudBunitContext ctx = harness.NewContext();
        harness.Store.Append(Document("findings.md"), At(9, 0, "Mira", DocChangeKind.Created, "Drafted the findings"));
        harness.Store.Append(Document("findings.md"), At(10, 0, "Ruth", DocChangeKind.Reviewed, "Verified 41 citations"));
        harness.Store.Append(Document("findings.md"), At(11, 2, "Emre", DocChangeKind.Edited, actorKind: DocActorKind.Human));
        int opened = 0;

        IRenderedComponent<DocumentByline> cut = ctx.Render<DocumentByline>(p => p
            .Add(c => c.Path, Document("findings.md"))
            .Add(c => c.OnOpenHistory, EventCallback.Factory.Create(ctx, () => opened++)));
        string text = cut.Find(".doc-byline").TextContent;
        await cut.Find(".doc-byline-time").ClickAsync(new());

        Assert.Contains("Written by", text, StringComparison.Ordinal);
        Assert.Contains("Mira", text, StringComparison.Ordinal);
        Assert.Contains("Edited by", text, StringComparison.Ordinal);
        Assert.Contains("Emre", text, StringComparison.Ordinal);
        Assert.Contains("Reviewed by", text, StringComparison.Ordinal);
        Assert.Contains("Ruth", text, StringComparison.Ordinal);
        Assert.Contains("Today 11:02", text, StringComparison.Ordinal);
        Assert.Equal(1, opened);
    }

    /// <summary>A new entry for the document appears in the byline without a reload.</summary>
    [Fact]
    public async Task Byline_RefreshesWhenAnEntryIsAdded()
    {
        using Harness harness = new();
        await using MudBunitContext ctx = harness.NewContext();
        IRenderedComponent<DocumentByline> cut = ctx.Render<DocumentByline>(p => p.Add(c => c.Path, Document("findings.md")));

        harness.Store.Append(Document("findings.md"), At(9, 0, "Mira", DocChangeKind.Created, "Drafted"));

        cut.WaitForAssertion(() => Assert.Contains("Mira", cut.Find(".doc-byline").TextContent, StringComparison.Ordinal));
    }

    /// <summary>An entry for another document does not touch this one's byline.</summary>
    [Fact]
    public async Task Byline_IgnoresOtherDocuments()
    {
        using Harness harness = new();
        await using MudBunitContext ctx = harness.NewContext();
        IRenderedComponent<DocumentByline> cut = ctx.Render<DocumentByline>(p => p.Add(c => c.Path, Document("findings.md")));

        harness.Store.Append(Document("other.md"), At(9, 0, "Mira", DocChangeKind.Created, "Drafted"));

        Assert.Empty(cut.FindAll(".doc-byline"));
    }

    /// <summary>With no history the section says where history will appear.</summary>
    [Fact]
    public async Task History_Empty_SaysNoChangesLoggedYet()
    {
        using Harness harness = new();
        await using MudBunitContext ctx = harness.NewContext();

        IRenderedComponent<DocumentHistory> cut = ctx.Render<DocumentHistory>(p => p.Add(c => c.Path, Document("findings.md")));

        Assert.Equal("No changes logged yet.", cut.Find(".doc-history-empty").TextContent.Trim());
    }

    /// <summary>The History groups by day, labels each kind, shows summaries in full and a bare Human save as Saved.</summary>
    [Fact]
    public async Task History_GroupsByDay_AndShowsSummariesInFull()
    {
        using Harness harness = new();
        await using MudBunitContext ctx = harness.NewContext();
        string longSummary = "Rewrote the findings around the 2026 survey and moved the methodology note to the appendix because the reviewers read it first.";
        harness.Store.Append(Document("findings.md"), new DocHistoryEntry(new DateTimeOffset(2026, 10, 17, 16, 20, 0, TimeSpan.Zero), "Quinn", DocActorKind.Agent, DocChangeKind.Objected, "Disagree with the sample size"));
        harness.Store.Append(Document("findings.md"), At(9, 0, "Mira", DocChangeKind.Edited, longSummary));
        harness.Store.Append(Document("findings.md"), At(11, 2, "Emre", DocChangeKind.Edited, actorKind: DocActorKind.Human));

        IRenderedComponent<DocumentHistory> cut = ctx.Render<DocumentHistory>(p => p.Add(c => c.Path, Document("findings.md")));

        Assert.Equal(["Today", "Today", "Yesterday"], cut.FindAll(".doc-history-day-label").Select(static label => label.TextContent.Trim()));
        Assert.Equal(["Saved", "Edited", "Objected"], cut.FindAll(".doc-history-kind").Select(static kind => kind.TextContent.Trim()));
        Assert.Equal(["you", "Agent", "Agent"], cut.FindAll(".doc-history-head .chip-meta").Select(static chip => chip.TextContent.Trim()));
        Assert.Contains(longSummary, cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>Each History entry's kind is a tinted status chip with a leading icon, Objected in the Warning family and Edited and Saved in Default.</summary>
    [Fact]
    public async Task History_KindLabels_AreTintedStatusChips()
    {
        using Harness harness = new();
        await using MudBunitContext ctx = harness.NewContext();
        harness.Store.Append(Document("findings.md"), new DocHistoryEntry(new DateTimeOffset(2026, 10, 17, 16, 20, 0, TimeSpan.Zero), "Quinn", DocActorKind.Agent, DocChangeKind.Objected, "Disagree with the sample size"));
        harness.Store.Append(Document("findings.md"), At(9, 0, "Mira", DocChangeKind.Edited, "Rewrote the intro"));
        harness.Store.Append(Document("findings.md"), At(11, 2, "Emre", DocChangeKind.Edited, actorKind: DocActorKind.Human));

        IRenderedComponent<DocumentHistory> cut = ctx.Render<DocumentHistory>(p => p.Add(c => c.Path, Document("findings.md")));

        Assert.Equal(3, cut.FindAll(".doc-history-kind.status-chip").Count);
        Assert.Single(cut.FindAll(".doc-history-kind.status-chip-warning"));
        Assert.Equal(2, cut.FindAll(".doc-history-kind.status-chip-default").Count);
        Assert.Equal(3, cut.FindAll(".doc-history-kind svg").Count);
    }

    /// <summary>Every kind maps to its colour family, a bare Human save to Default whatever its kind.</summary>
    [Theory]
    [InlineData(DocChangeKind.Created, "Hello", DocActorKind.Agent, MudBlazor.Color.Info)]
    [InlineData(DocChangeKind.Edited, "Hello", DocActorKind.Agent, MudBlazor.Color.Default)]
    [InlineData(DocChangeKind.Reviewed, "Hello", DocActorKind.Agent, MudBlazor.Color.Success)]
    [InlineData(DocChangeKind.Objected, "Hello", DocActorKind.Agent, MudBlazor.Color.Warning)]
    [InlineData(DocChangeKind.Edited, "", DocActorKind.Human, MudBlazor.Color.Default)]
    public void KindStyle_MapsEachKindToItsColour(DocChangeKind kind, string summary, DocActorKind actorKind, MudBlazor.Color expected)
    {
        DocHistoryEntry entry = new(Now, "Mira", actorKind, kind, summary);

        Assert.Equal(expected, DocHistoryKindStyle.ColorFor(entry));
        Assert.NotEmpty(DocHistoryKindStyle.IconFor(entry));
    }

    /// <summary>A summary is rendered as Markdown but model text cannot inject markup.</summary>
    [Fact]
    public async Task History_Summary_IsMarkdownWithoutRawHtml()
    {
        using Harness harness = new();
        await using MudBunitContext ctx = harness.NewContext();
        harness.Store.Append(Document("findings.md"), At(9, 0, "Mira", DocChangeKind.Edited, "Made **bold** claims <script>alert(1)</script>"));

        IRenderedComponent<DocumentHistory> cut = ctx.Render<DocumentHistory>(p => p.Add(c => c.Path, Document("findings.md")));

        Assert.Contains("<strong>bold</strong>", cut.Find(".doc-history-summary").InnerHtml, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll(".doc-history-summary script"));
    }

    /// <summary>Fifty entries show at first; Show older reveals the rest.</summary>
    [Fact]
    public async Task History_ShowsFiftyAtATime()
    {
        using Harness harness = new();
        await using MudBunitContext ctx = harness.NewContext();
        for (int index = 0; index < 60; index++)
        {
            harness.Store.Append(Document("findings.md"), At(0, 0, "Mira", DocChangeKind.Edited, "change " + index));
        }

        IRenderedComponent<DocumentHistory> cut = ctx.Render<DocumentHistory>(p => p.Add(c => c.Path, Document("findings.md")));
        cut.WaitForAssertion(() => Assert.Equal(50, cut.FindAll(".doc-history-entry").Count));
        await cut.Find(".doc-history-older").ClickAsync(new());

        cut.WaitForAssertion(() => Assert.Equal(60, cut.FindAll(".doc-history-entry").Count));
        Assert.Empty(cut.FindAll(".doc-history-older"));
    }

    /// <summary>The panel draws the whole History as the document once did: every entry, newest first, with "Saved" for a bare Human save, under no heading of its own.</summary>
    [Fact]
    public async Task Panel_ShowsTheWholeHistory()
    {
        using Harness harness = new();
        await using MudBunitContext ctx = harness.NewContext();
        for (int index = 0; index < 6; index++)
        {
            harness.Store.Append(Document("findings.md"), At(index, 0, "Mira", DocChangeKind.Edited, "change " + index));
        }

        harness.Store.Append(Document("findings.md"), At(7, 0, "Emre", DocChangeKind.Edited, actorKind: DocActorKind.Human));
        SidebarPanelContext panelCtx = new();

        IRenderedComponent<CascadingValue<SidebarPanelContext>> cut = RenderPanel(ctx, panelCtx, Document("findings.md"));

        cut.WaitForAssertion(() => Assert.Equal(7, cut.FindAll(".doc-history-entry").Count));
        Assert.Equal("Saved", cut.FindAll(".doc-history-kind")[0].TextContent.Trim());
        Assert.Equal("change 5", cut.FindAll(".doc-history-summary")[0].TextContent.Trim());
        Assert.Empty(cut.FindAll(".doc-history-title"));
        Assert.Equal(7, panelCtx.Count);
    }

    /// <summary>With no entries the panel says so in one line.</summary>
    [Fact]
    public async Task Panel_Empty_SaysNoEditsLogged()
    {
        using Harness harness = new();
        await using MudBunitContext ctx = harness.NewContext();

        IRenderedComponent<CascadingValue<SidebarPanelContext>> cut = RenderPanel(ctx, new SidebarPanelContext(), Document("findings.md"));

        Assert.Equal("No changes logged yet.", cut.Find(".doc-history-empty").TextContent.Trim());
    }

    /// <summary>Renders the panel for <paramref name="path"/> inside a context the frame would give it.</summary>
    /// <param name="ctx">The test context.</param>
    /// <param name="panelCtx">The context the panel reports its count to.</param>
    /// <param name="path">The open document.</param>
    /// <returns>The rendered panel.</returns>
    private static IRenderedComponent<CascadingValue<SidebarPanelContext>> RenderPanel(MudBunitContext ctx, SidebarPanelContext panelCtx, LibraryPath path) =>
        ctx.Render<CascadingValue<SidebarPanelContext>>(p => p
            .Add(c => c.Value, panelCtx)
            .Add(c => c.IsFixed, true)
            .AddChildContent<RecentEditsPanel>(s => s
                .Add(c => c.Subject, new DocumentSubject(path, LibraryFileKind.Markdown, null))
                .Add(c => c.TabId, TabId)));

    /// <summary>A real store over a temporary data directory, and a context with every service the three components resolve.</summary>
    private sealed class Harness : IDisposable
    {
        private readonly TempDataDir dir = new();
        private readonly ProfileFixture profiles = new();

        /// <summary>Builds the store over a fresh data directory.</summary>
        public Harness()
        {
            this.Options = this.dir.Options();
            this.Store = new DocHistoryStore(this.Options, NullLogger<DocHistoryStore>.Instance);
        }

        /// <summary>The store the components read.</summary>
        public DocHistoryStore Store { get; }

        /// <summary>The bound options.</summary>
        public IOptions<TeamOptions> Options { get; }

        /// <summary>A context with a UTC clock fixed at noon on 2026-10-18.</summary>
        /// <returns>The context.</returns>
        public MudBunitContext NewContext()
        {
            MudBunitContext ctx = this.profiles.NewContext();
            FakeTimeProvider clock = new(Now);
            clock.SetLocalTimeZone(TimeZoneInfo.Utc);
            ctx.Services.AddSingleton<TimeProvider>(clock);
            ctx.Services.AddSingleton(this.Store);
            ctx.Services.AddSingleton(this.Options);
            ctx.Services.AddSingleton(this.profiles.Avatars);
            ctx.Services.AddSingleton<ITaskReferenceResolver>(new FakeTaskReferenceResolver());
            ctx.Services.AddSingleton<ILibraryNoteResolver>(new FakeLibraryNoteResolver());
            return ctx;
        }

        /// <summary>Removes the temporary data directory.</summary>
        public void Dispose()
        {
            this.profiles.Dispose();
            this.dir.Dispose();
        }
    }
}
