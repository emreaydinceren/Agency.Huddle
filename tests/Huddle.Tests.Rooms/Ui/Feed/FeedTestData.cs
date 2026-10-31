using Agency.Huddle.App.Feed;
using Agency.Huddle.App.Tabs;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Library;
using Microsoft.Extensions.DependencyInjection;

namespace Agency.Huddle.Tests.Ui;

/// <summary>Items, a clock and a context for the Feed row and list tests: Tuesday 20 October 2026, noon UTC, in a UTC Human zone.</summary>
internal static class FeedTestData
{
    /// <summary>The fixed "now" of every Feed component test.</summary>
    public static readonly DateTimeOffset Now = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    /// <summary>A Feed item with the fields the component tests vary.</summary>
    /// <param name="type">The source type.</param>
    /// <param name="payload">The payload.</param>
    /// <param name="notify">Whether it is a Notification.</param>
    /// <param name="at">When it happened; defaults to <see cref="Now"/>.</param>
    /// <param name="resolved">When it was resolved, or <see langword="null"/>.</param>
    /// <param name="actor">The actor; defaults to the Agent Nova.</param>
    /// <param name="id">The item id.</param>
    /// <returns>The item.</returns>
    public static FeedItem Item(
        FeedSourceType? type,
        FeedPayload? payload,
        bool notify = false,
        DateTimeOffset? at = null,
        DateTimeOffset? resolved = null,
        FeedActor? actor = null,
        long id = 1) =>
        new(id, at ?? Now, type, "target", actor ?? new FeedActor(FeedActorKind.Agent, "Nova"), notify, null, null, resolved, payload);

    /// <summary>A bUnit context holding what the Feed components inject, with the clock at <see cref="Now"/>.</summary>
    /// <param name="library">The Library stack that resolves a document chip's path; the caller owns and disposes it.</param>
    /// <param name="tasks">The Tasks the chips can resolve, or <see langword="null"/> for none.</param>
    /// <returns>The context; the caller disposes it.</returns>
    public static MudBunitContext NewContext(LibraryFileServiceFixture library, FakeTaskReferenceResolver? tasks = null)
    {
        MudBunitContext ctx = new();
        ctx.Services.AddSingleton<TimeProvider>(new ManualTimeProvider(Now, TimeZoneInfo.Utc));
        ctx.Services.AddSingleton<ITaskReferenceResolver>(tasks ?? new FakeTaskReferenceResolver());
        ctx.Services.AddSingleton(library.Resolver);
        ctx.Services.AddSingleton<TabService>();
        return ctx;
    }
}
