namespace Agency.Huddle.Tests.Ui.Tasks;

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Components.Tasks;
using Agency.Huddle.App.Tasks;

/// <summary>
/// A Task's History draws each actor with the avatar stored for them (#461): the Human reads "ME" here, as in the Transcript and
/// the Members panel, not "YO" built from the name "You".
/// </summary>
public sealed class TaskHistoryAvatarTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);

    /// <summary>The Human's History entry wears the Human's stored label, and an Agent's keeps its own label.</summary>
    [Fact]
    public async Task Render_Actors_WearTheirStoredAvatars()
    {
        using TempDataDir dir = new();
        using AvatarStore store = new(dir.Options(), NullLogger<AvatarStore>.Instance);
        store.Save("You", new Avatar(Label: "ME", Image: null, Background: null));
        store.Save("Nova", new Avatar(Label: "NV", Image: null, Background: null));
        await using MudBunitContext ctx = new();
        ctx.Services.AddSingleton<TimeProvider>(new FakeClock(Now));
        ctx.Services.AddSingleton(store);

        IRenderedComponent<TaskHistory> history = ctx.Render<TaskHistory>(parameters => parameters
            .Add(p => p.HumanName, "You")
            .Add(p => p.Entries, [new ChangeLogEntry(Now, "You", "status: todo to in-progress"), new ChangeLogEntry(Now, "Nova", "assignee: none to Nova")]));

        IReadOnlyList<string> faces = [.. history.FindAll(".doc-history-actor .mud-avatar").Select(avatar => avatar.TextContent.Trim())];
        Assert.Equal(["NV", "ME"], faces);
    }

    private sealed class FakeClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
