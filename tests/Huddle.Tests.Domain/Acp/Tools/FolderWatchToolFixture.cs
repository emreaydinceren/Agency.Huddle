using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.FileChanges;

namespace Agency.Huddle.Tests.Acp.Tools;

/// <summary>
/// A real <see cref="FileChangeTracker"/> and its collaborators over an isolated <see cref="TempDataDir"/>,
/// shared by <c>WatchFolderToolTests</c> and <c>UnwatchFolderToolTests</c> and matching
/// <c>FileChangeTrackerTests.CreateFixtureAsync</c>'s shape. The Agent "Nova" is registered, so
/// every tool test can bind that caller Name at construction.
/// </summary>
/// <param name="dataDir">The temp data directory, disposed with the fixture.</param>
/// <param name="personas">The Persona store, disposed with the fixture.</param>
/// <param name="tracker">The tracker under test.</param>
/// <param name="options">The bound options the tracker resolves paths against.</param>
internal sealed class FolderWatchToolFixture(TempDataDir dataDir, PersonaStore personas, FileChangeTracker tracker, IOptions<TeamOptions> options) : IDisposable
{
    /// <summary>The <see cref="FileChangeTracker"/> under test.</summary>
    public FileChangeTracker Tracker { get; } = tracker;

    /// <summary>The bound options this fixture's tracker resolves paths against.</summary>
    public IOptions<TeamOptions> Options { get; } = options;

    /// <summary>Builds a fixture with a real <see cref="SqliteTeamDirectory"/>, <see cref="PersonaStore"/>, <see cref="FileStateStore"/> and <see cref="WatchedFolderResolver"/>.</summary>
    /// <param name="ct">Cancels the Team Directory's initialisation.</param>
    /// <returns>The fixture; the caller disposes it.</returns>
    public static async Task<FolderWatchToolFixture> CreateAsync(CancellationToken ct)
    {
        TempDataDir dataDir = new();
        IOptions<TeamOptions> options = dataDir.Options();
        SqliteTeamDirectory directory = new(options);
        await directory.InitializeAsync("You", ct);

        AgentPaths agentPaths = new(options);
        PersonaStore personas = new(
            agentPaths,
            new PersonaModelStore(options),
            new PersonaEffortStore(options), new PersonaWorkModeStore(options),
            NullLogger<PersonaStore>.Instance);
        FileStateStore store = new(options, NullLogger<FileStateStore>.Instance);
        WatchedFolderResolver resolver = new(options, agentPaths);
        FileChangeTracker tracker = new(store, personas, directory, resolver, options, agentPaths, NullLogger<FileChangeTracker>.Instance);

        // Registered so "Nova" resolves via the Agent-Name rule (FC §6.3) to its own Work Dir,
        // matching the caller Name every tool test binds at construction.
        personas.Add(new PersonaIdentity("Nova", "Nova", "Nova", []), "You are Nova.");

        return new FolderWatchToolFixture(dataDir, personas, tracker, options);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        personas.Dispose();
        dataDir.Dispose();
    }
}
