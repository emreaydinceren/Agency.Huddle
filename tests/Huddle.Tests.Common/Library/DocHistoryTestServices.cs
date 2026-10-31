using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.DocHistory;

namespace Agency.Huddle.Tests.Library;

/// <summary>
/// The services a hosted <c>LibraryDocument</c> needs for its byline and History (DocHistory spec §6.7, §6.10), for the
/// tests that render a document and are not about its history: a store over a data folder that holds no history, so
/// every document reads as having none.
/// </summary>
internal static class DocHistoryTestServices
{
    /// <summary>Registers an empty <see cref="DocHistoryStore"/> and the system clock, keeping any the test already registered.</summary>
    /// <param name="services">The container to register into.</param>
    /// <returns><paramref name="services"/>, for chaining.</returns>
    internal static IServiceCollection AddDocHistoryServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton(new DocHistoryStore(
            Options.Create(new TeamOptions { DataDir = Path.Combine(Path.GetTempPath(), "dochistory-none-" + Guid.NewGuid().ToString("N")) }),
            NullLogger<DocHistoryStore>.Instance));
        return services;
    }
}
