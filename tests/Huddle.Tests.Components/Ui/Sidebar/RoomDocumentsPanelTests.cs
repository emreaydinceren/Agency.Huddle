using Bunit;
using AngleSharp.Dom;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Components.Sidebar;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Sidebar;
using Agency.Huddle.App.Tabs;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Library;
using Agency.Huddle.Tests.Factories;

namespace Agency.Huddle.Tests.Ui.Sidebar;

/// <summary>Pins <see cref="RoomDocumentsPanel"/>: the Library files a Room's Messages mention, resolved for existence (Spec Documents in this chat, D-8).</summary>
public sealed class RoomDocumentsPanelTests
{
    private const string RoomId = "r1";
    private const string PlanPath = "C:/Data/Teams/Notes/plan.md";
    private const string GonePath = "C:/Data/Teams/Notes/gone.md";

    private static ChatMessage FileMessage(string id, string key, string label)
    {
        return new ChatMessage(id, DateTimeOffset.UtcNow, "human", "You", "see it")
        {
            References = [new MessageReference(ReferenceKind.LibraryFile, key, label)],
        };
    }

    private static MessagePostedEvent Posted(string roomId, ChatMessage message)
    {
        return new MessagePostedEvent(new Room(roomId, "room", DateTimeOffset.UtcNow), message, [], [], new RoomBudget(0, 40));
    }

    private sealed class Rig : IAsyncDisposable
    {
        private readonly TempDataDir dir = new();
        private readonly LibraryFileServiceFixture library = LibraryFileServiceFixture.Build();

        public Rig(Func<string, LibraryReference?> resolvePath)
        {
            this.Context = new MudBunitContext();
            this.Store = this.dir.CreateChatStore();
            this.Events = new RoomEvents(NullLogger<RoomEvents>.Instance);
            this.Tabs = new TabService();
            this.Context.Services.AddSingleton<IChatStore>(this.Store);
            this.Context.Services.AddSingleton(this.Events);
            this.Context.Services.AddSingleton(this.Tabs);
            this.Context.Services.AddSingleton<ILibraryReferenceResolver>(new FakeLibraryNoteResolver(resolvePath));
            this.Context.Services.AddSingleton(this.library.Index);
        }

        public MudBunitContext Context { get; }

        public FileChatStore Store { get; }

        public RoomEvents Events { get; }

        public TabService Tabs { get; }

        public SidebarPanelContext Panel { get; } = new();

        public WikiLinkIndex Index => this.library.Index;

        public IRenderedComponent<CascadingValue<SidebarPanelContext>> Render(PageSubject subject, string tabId)
        {
            return this.Context.Render<CascadingValue<SidebarPanelContext>>(p => p
                .Add(c => c.Value, this.Panel)
                .Add(c => c.IsFixed, true)
                .AddChildContent<RoomDocumentsPanel>(s => s
                    .Add(c => c.Subject, subject)
                    .Add(c => c.TabId, tabId)));
        }

        public async ValueTask DisposeAsync()
        {
            await this.Context.DisposeAsync();
            this.library.Dispose();
            this.dir.Dispose();
        }
    }

    private static LibraryReference? ExistingPlan(string path)
    {
        return string.Equals(path, RoomDocumentsPanelTests.PlanPath, StringComparison.Ordinal)
            ? new LibraryReference("teams", "Notes/plan.md", true)
            : null;
    }

    /// <summary>Each distinct file is listed under its snapshot Label, and the header count follows.</summary>
    [Fact]
    public async Task Documents_ListsFileNames()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using Rig rig = new(RoomDocumentsPanelTests.ExistingPlan);
        await rig.Store.AppendAsync(RoomDocumentsPanelTests.RoomId, RoomDocumentsPanelTests.FileMessage("m1", RoomDocumentsPanelTests.PlanPath, "plan.md"), ct);

        IRenderedComponent<CascadingValue<SidebarPanelContext>> cut = rig.Render(new RoomSubject(RoomDocumentsPanelTests.RoomId), "t1");

        cut.WaitForAssertion(() => Assert.Equal(["plan.md"], cut.FindAll(".documents-row").Select(static row => row.TextContent.Trim()).ToList()));
        Assert.Equal(1, rig.Panel.Count);
    }

    /// <summary>A file the Library no longer has is secondary text, not a link, with the tooltip.</summary>
    [Fact]
    public async Task Documents_Gone_IsDisabledWithTooltip()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using Rig rig = new(RoomDocumentsPanelTests.ExistingPlan);
        await rig.Store.AppendAsync(RoomDocumentsPanelTests.RoomId, RoomDocumentsPanelTests.FileMessage("m1", RoomDocumentsPanelTests.GonePath, "gone.md"), ct);

        IRenderedComponent<CascadingValue<SidebarPanelContext>> cut = rig.Render(new RoomSubject(RoomDocumentsPanelTests.RoomId), "t1");

        cut.WaitForAssertion(() =>
        {
            IElement gone = cut.Find(".documents-row .mud-text-secondary");
            Assert.Equal("gone.md", gone.TextContent.Trim());
            Assert.Equal("This file is no longer in the Library.", gone.GetAttribute("title"));
            Assert.Empty(cut.FindAll(".documents-row a"));
        });
    }

    /// <summary>An existing file links to its Library page address and carries its full path as the title.</summary>
    [Fact]
    public async Task Documents_Click_OpensInTheLibrary()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using Rig rig = new(RoomDocumentsPanelTests.ExistingPlan);
        await rig.Store.AppendAsync(RoomDocumentsPanelTests.RoomId, RoomDocumentsPanelTests.FileMessage("m1", RoomDocumentsPanelTests.PlanPath, "plan.md"), ct);

        IRenderedComponent<CascadingValue<SidebarPanelContext>> cut = rig.Render(new RoomSubject(RoomDocumentsPanelTests.RoomId), "t1");

        cut.WaitForAssertion(() =>
        {
            IElement link = cut.Find(".documents-row a");
            Assert.Equal("/library?root=teams&path=Notes%2Fplan.md", link.GetAttribute("href"));
            Assert.Equal(RoomDocumentsPanelTests.PlanPath, link.GetAttribute("title"));
        });
    }

    /// <summary>The jump button navigates the same Tab to the newest Message that mentions the file.</summary>
    [Fact]
    public async Task Documents_JumpButton_NavigatesTheSameTab()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using Rig rig = new(RoomDocumentsPanelTests.ExistingPlan);
        await rig.Store.AppendAsync(RoomDocumentsPanelTests.RoomId, RoomDocumentsPanelTests.FileMessage("m1", RoomDocumentsPanelTests.PlanPath, "plan.md"), ct);
        await rig.Store.AppendAsync(RoomDocumentsPanelTests.RoomId, RoomDocumentsPanelTests.FileMessage("m 2", RoomDocumentsPanelTests.PlanPath, "plan.md"), ct);
        Tab tab = rig.Tabs.Open("/rooms/r1");
        IRenderedComponent<CascadingValue<SidebarPanelContext>> cut = rig.Render(new RoomSubject(RoomDocumentsPanelTests.RoomId), tab.Id);
        cut.WaitForElement("button[aria-label='Show where it was mentioned']");

        await cut.InvokeAsync(() => cut.Find("button[aria-label='Show where it was mentioned']").ClickAsync());

        cut.WaitForAssertion(() => Assert.Equal("/rooms/r1?message=m%202", rig.Tabs.Tabs.Single(t => t.Id == tab.Id).Url));
    }

    /// <summary>An Invalidated event for any root re-resolves the files, so a deleted file turns into the gone line.</summary>
    [Fact]
    public async Task Documents_InvalidatedAnyRoot_Rechecks()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        bool exists = true;
        await using Rig rig = new(path => exists ? RoomDocumentsPanelTests.ExistingPlan(path) : null);
        await rig.Store.AppendAsync(RoomDocumentsPanelTests.RoomId, RoomDocumentsPanelTests.FileMessage("m1", RoomDocumentsPanelTests.PlanPath, "plan.md"), ct);
        IRenderedComponent<CascadingValue<SidebarPanelContext>> cut = rig.Render(new RoomSubject(RoomDocumentsPanelTests.RoomId), "t1");
        cut.WaitForElement(".documents-row a");

        exists = false;
        rig.Index.Invalidate("some-other-root");

        cut.WaitForAssertion(() =>
        {
            Assert.Empty(cut.FindAll(".documents-row a"));
            Assert.Equal("This file is no longer in the Library.", cut.Find(".documents-row .mud-text-secondary").GetAttribute("title"));
        });
    }

    /// <summary>A Message posted in another Room does not reload this panel.</summary>
    [Fact]
    public async Task Documents_MessagePostedInAnotherRoom_DoesNothing()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using Rig rig = new(RoomDocumentsPanelTests.ExistingPlan);
        await rig.Store.AppendAsync(RoomDocumentsPanelTests.RoomId, RoomDocumentsPanelTests.FileMessage("m1", RoomDocumentsPanelTests.PlanPath, "plan.md"), ct);
        IRenderedComponent<CascadingValue<SidebarPanelContext>> cut = rig.Render(new RoomSubject(RoomDocumentsPanelTests.RoomId), "t1");
        cut.WaitForElement(".documents-row a");
        ChatMessage second = RoomDocumentsPanelTests.FileMessage("m2", RoomDocumentsPanelTests.GonePath, "gone.md");
        await rig.Store.AppendAsync(RoomDocumentsPanelTests.RoomId, second, ct);

        rig.Events.PublishMessagePosted(RoomDocumentsPanelTests.Posted("other", second));

        Assert.Single(cut.FindAll(".documents-row"));
        Assert.Equal(1, rig.Panel.Count);
    }

    /// <summary>A Message posted in this Room reloads the list.</summary>
    [Fact]
    public async Task Documents_MessagePostedInThisRoom_Reloads()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using Rig rig = new(RoomDocumentsPanelTests.ExistingPlan);
        await rig.Store.AppendAsync(RoomDocumentsPanelTests.RoomId, RoomDocumentsPanelTests.FileMessage("m1", RoomDocumentsPanelTests.PlanPath, "plan.md"), ct);
        IRenderedComponent<CascadingValue<SidebarPanelContext>> cut = rig.Render(new RoomSubject(RoomDocumentsPanelTests.RoomId), "t1");
        cut.WaitForElement(".documents-row a");
        ChatMessage second = RoomDocumentsPanelTests.FileMessage("m2", RoomDocumentsPanelTests.GonePath, "gone.md");
        await rig.Store.AppendAsync(RoomDocumentsPanelTests.RoomId, second, ct);

        rig.Events.PublishMessagePosted(RoomDocumentsPanelTests.Posted(RoomDocumentsPanelTests.RoomId, second));

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".documents-row").Count));
    }

    /// <summary>A Room with no file mentions shows the empty line and reports a count of 0.</summary>
    [Fact]
    public async Task Documents_None_ShowsEmptyLine()
    {
        await using Rig rig = new(RoomDocumentsPanelTests.ExistingPlan);

        IRenderedComponent<CascadingValue<SidebarPanelContext>> cut = rig.Render(new RoomSubject(RoomDocumentsPanelTests.RoomId), "t1");

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("No documents mentioned in this chat yet.", cut.Find("[role='status']").TextContent.Trim());
            Assert.Equal(0, rig.Panel.Count);
        });
    }
}
