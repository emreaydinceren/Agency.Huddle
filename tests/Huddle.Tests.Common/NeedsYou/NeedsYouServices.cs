using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Elicitation;
using Agency.Huddle.App.NeedsYou;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Questions;

namespace Agency.Huddle.Tests.NeedsYou;

/// <summary>Registers a real <see cref="NeedsYouTracker"/> for a component context whose Room rows show the Needs you pill.</summary>
internal static class NeedsYouServices
{
    /// <summary>
    /// Adds a <see cref="NeedsYouTracker"/> over the context's own <see cref="RoomEvents"/> and <see cref="ITeamDirectory"/>,
    /// resolved when first asked for, so the call may come before either is registered.
    /// </summary>
    /// <param name="services">The context's services.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddNeedsYouTracker(this IServiceCollection services)
    {
        return services.AddSingleton(provider =>
        {
            RoomEvents events = provider.GetRequiredService<RoomEvents>();
            return new NeedsYouTracker(
                events,
                new QuestionStore(events),
                new ElicitationStore(events),
                provider.GetRequiredService<ITeamDirectory>(),
                NullLogger<NeedsYouTracker>.Instance);
        });
    }
}
