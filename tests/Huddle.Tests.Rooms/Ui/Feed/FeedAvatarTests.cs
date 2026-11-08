using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Components.Feed;
using Agency.Huddle.App.Feed;
using Agency.Huddle.App.Tasks;
using Bunit;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// The Feed card and its previews draw an Agent's or the Human's stored avatar, not a monogram built from the Name (#461):
/// the picture on the card's 40px avatar, and the stored label, which the Human's own face carries everywhere, on the 24px
/// avatars of the Task and Room previews.
/// </summary>
public sealed class FeedAvatarTests
{
    private static IRenderedComponent<FeedCard> Render(MudBunitContext ctx, FeedItem item) =>
        ctx.Render<FeedCard>(parameters =>
        {
            parameters.Add(card => card.Group, new FeedGroup([item]));
            parameters.Add(card => card.Expanded, true);
        });

    private static FeedItem NeedsYou(FeedActor? actor = null) =>
        FeedTestData.Item(FeedSourceType.TaskNeedsYou, new TaskNeedsYouPayload("GRO-1", "Draft the pricing page", "Growth", 1, 2), actor: actor) with { TargetKey = "GRO-1" };

    /// <summary>An Agent's card avatar is its stored picture, drawn at the card's 40px size.</summary>
    [Fact]
    public async Task Card_AgentWithPicture_DrawsThePicture()
    {
        using FeedCardHost host = await FeedCardHost.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        host.Avatars.Save("Nova", new Avatar(Label: null, Image: "nova.png", Background: null));
        await using MudBunitContext ctx = host.NewContext();

        IRenderedComponent<FeedCard> card = Render(ctx, NeedsYou());

        Assert.Equal("/agent-avatars/nova.png", card.Find(".feed-card-avatar .mud-avatar img").GetAttribute("src"));
    }

    /// <summary>The Human's card avatar is the face the Human chose, the same label the Transcript and the Members panel draw, not "YO" from the card's "You".</summary>
    [Fact]
    public async Task Card_HumanActor_DrawsTheHumansLabel()
    {
        using FeedCardHost host = await FeedCardHost.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        host.Avatars.Save("You", new Avatar(Label: "ME", Image: null, Background: null));
        await using MudBunitContext ctx = host.NewContext();

        IRenderedComponent<FeedCard> card = Render(ctx, NeedsYou(new FeedActor(FeedActorKind.Human, "You")));

        Assert.Equal("ME", card.Find(".feed-card-avatar .mud-avatar").TextContent.Trim());
    }

    /// <summary>The Task preview's assignee wears the avatar stored for them.</summary>
    [Fact]
    public async Task TaskPreview_Assignee_DrawsTheStoredAvatar()
    {
        using FeedCardHost host = await FeedCardHost.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        _ = host.SeedTask("GRO-1", status: TaskState.NeedsYou, assignee: "Nova");
        host.Avatars.Save("Nova", new Avatar(Label: "NV", Image: null, Background: null));
        await using MudBunitContext ctx = host.NewContext();

        IRenderedComponent<FeedCard> card = Render(ctx, NeedsYou());

        Assert.Equal("NV", card.Find(".feed-preview-person .mud-avatar").TextContent.Trim());
    }

    /// <summary>The Room preview's Members wear their stored avatars, so the Human reads "ME" here as everywhere.</summary>
    [Fact]
    public async Task RoomPreview_Members_DrawTheirStoredAvatars()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using FeedCardHost host = await FeedCardHost.CreateAsync(ct);
        Agency.Huddle.App.Data.Room room = await NeedsYouTaskTestHost.WaitingRoomAsync(host.NeedsYou, ct);
        host.Avatars.Save("You", new Avatar(Label: "ME", Image: null, Background: null));
        await using MudBunitContext ctx = host.NewContext();
        FeedItem item = FeedTestData.Item(FeedSourceType.RoomMention, new RoomWaitPayload(room.Id, room.Name, "Growth"), notify: true);

        IRenderedComponent<FeedCard> card = Render(ctx, item);
        card.WaitForAssertion(() => Assert.NotEmpty(card.FindAll(".feed-room-member")));

        IReadOnlyList<string> faces = [.. card.FindAll(".feed-room-member .mud-avatar").Select(avatar => avatar.TextContent.Trim()).Order(StringComparer.Ordinal)];
        Assert.Equal(["ME", "NO"], faces);
    }
}
