using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Components.Feed;
using Agency.Huddle.App.Feed;
using Bunit;

namespace Agency.Huddle.Tests.Ui;

/// <summary>A Feed card for a goldfish's change draws the Agent's own stored avatar: the stamp is display, not identity.</summary>
public sealed class GoldfishFeedAvatarTests
{
    /// <summary>The card of an item whose actor is "Nova · goldfish" shows Nova's stored picture.</summary>
    [Fact]
    public async Task Card_GoldfishActor_DrawsTheAgentsPicture()
    {
        using FeedCardHost host = await FeedCardHost.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        host.Avatars.Save("Nova", new Avatar(Label: null, Image: "nova.png", Background: null));
        await using MudBunitContext ctx = host.NewContext();
        FeedItem item = FeedTestData.Item(
            FeedSourceType.TaskNeedsYou,
            new TaskNeedsYouPayload("GRO-1", "Draft the pricing page", "Growth", 1, 2),
            actor: new FeedActor(FeedActorKind.Agent, "Nova · goldfish")) with { TargetKey = "GRO-1" };

        IRenderedComponent<FeedCard> card = ctx.Render<FeedCard>(parameters =>
        {
            parameters.Add(c => c.Group, new FeedGroup([item]));
            parameters.Add(c => c.Expanded, true);
        });

        Assert.Equal("/agent-avatars/nova.png", card.Find(".feed-card-avatar .mud-avatar img").GetAttribute("src"));
    }
}
