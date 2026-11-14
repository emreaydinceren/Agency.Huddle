using Agency.Huddle.App.Components.Feed;
using Agency.Huddle.App.Feed;
using Agency.Huddle.Tests.Library;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Agency.Huddle.App.Components.Shared;

namespace Agency.Huddle.Tests.Ui;

/// <summary>The panel's seen marks, batch dismissal and arrow keys.</summary>
public sealed partial class NotificationsPanelTests
{
    private static string ChosenWord(IRenderedComponent<NotificationsPanel> cut) =>
        Whitespace().Replace(cut.Find(".mud-chip[aria-checked='true']").TextContent, " ").Trim().Split(' ')[0];

    private static FeedItem PostSameActor(FeedPanelHost host, string target, int minutesAgo) =>
        host.Store.Apply(
        [
            new PostCommand(
                new FeedMessage(FeedSourceType.TaskNeedsYou, target, new FeedActor(FeedActorKind.Agent, "Nova"), true, new TaskNeedsYouPayload(target, "Draft " + target, "Growth", 1, 2)),
                FeedTestData.Now.AddMinutes(-minutesAgo)),
        ]).Added.Single();

    /// <summary>A Notification parked behind the "new items" pill is not marked seen until the pill is pressed.</summary>
    [Fact]
    public async Task ParkedNotification_IsMarkedSeen_OnlyWhenThePillInsertsIt()
    {
        using FeedPanelHost host = new();
        using LibraryFileServiceFixture library = LibraryFileServiceFixture.Build();
        _ = host.PostNeedsYou(5);
        await using MudBunitContext ctx = NewContext(host, library);
        ctx.Module(JsModule.Feed).Setup<bool>("isScrolled", _ => true).SetResult(true);
        ctx.Module(JsModule.Feed).SetupVoid("scrollToTop", _ => true).SetVoidResult();
        IRenderedComponent<NotificationsPanel> cut = Render(ctx);
        _ = host.ApplyQueued();
        FeedItem fresh = host.PostNeedsYou();

        await cut.InvokeAsync(() => host.Events.RaiseItemsAdded([fresh]));

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".feed-new-pill")));
        Assert.Empty(host.ApplyQueued());
        await cut.Find(".feed-new-pill").ClickAsync(new());
        MarkSeenCommand seen = Assert.IsType<MarkSeenCommand>(Assert.Single(host.ApplyQueued()));
        Assert.Equal(fresh.Id, seen.UpToId);
    }

    /// <summary>The x on a grouped row dismisses every member in one go, from one callback.</summary>
    [Fact]
    public async Task DismissingAGroup_DismissesAllMembersTogether()
    {
        using FeedPanelHost host = new();
        using LibraryFileServiceFixture library = LibraryFileServiceFixture.Build();
        FeedItem[] members = [PostSameActor(host, "GRO-1", 3), PostSameActor(host, "GRO-2", 2), PostSameActor(host, "GRO-3", 1)];
        await using MudBunitContext ctx = NewContext(host, library);
        IRenderedComponent<NotificationsPanel> cut = Render(ctx);
        Assert.Single(cut.FindAll(".feed-row"));
        _ = host.ApplyQueued();

        await cut.Find("button[aria-label='Dismiss']").ClickAsync(new());

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".feed-row")));
        long[] dismissed = [.. host.ApplyQueued().OfType<DismissCommand>().Select(static command => command.Id)];
        Assert.Equal([.. members.Select(static member => member.Id).Order()], dismissed.Order());
    }

    /// <summary>The arrow keys move the Category choice between All and Needs you, wrapping around the ends.</summary>
    [Fact]
    public async Task ArrowKeys_MoveTheCategory_AndWrap()
    {
        using FeedPanelHost host = new();
        using LibraryFileServiceFixture library = LibraryFileServiceFixture.Build();
        await using MudBunitContext ctx = NewContext(host, library);
        ctx.Module(JsModule.Feed).SetupVoid("focusChecked", _ => true).SetVoidResult();
        IRenderedComponent<NotificationsPanel> cut = Render(ctx);
        Assert.Equal("Needs", ChosenWord(cut));

        await cut.Find("[role='radiogroup']").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowRight" });
        cut.WaitForAssertion(() => Assert.Equal("All", ChosenWord(cut)));

        await cut.Find("[role='radiogroup']").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowLeft" });
        cut.WaitForAssertion(() => Assert.Equal("Needs", ChosenWord(cut)));
    }
}
