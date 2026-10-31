using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;

namespace Agency.Huddle.Tests.Factories;

/// <summary>
/// Builds the disk-backed stores a test needs over a <see cref="TempDataDir"/> (or over options that point at one),
/// so the constructor argument lists live in one place.
/// </summary>
internal static class TestStores
{
    /// <summary>Creates a <see cref="SqliteTeamDirectory"/> over <paramref name="dir"/>, not yet initialised.</summary>
    /// <param name="dir">The temporary data directory.</param>
    public static SqliteTeamDirectory CreateDirectory(this TempDataDir dir) => dir.Options().CreateDirectory();

    /// <summary>Creates a <see cref="SqliteTeamDirectory"/> over <paramref name="options"/>, not yet initialised.</summary>
    /// <param name="options">The options whose data directory holds the database.</param>
    public static SqliteTeamDirectory CreateDirectory(this IOptions<TeamOptions> options) => new(options);

    /// <summary>Creates a <see cref="SqliteTeamDirectory"/> over <paramref name="dir"/> and initialises it, so the Human row exists.</summary>
    /// <param name="dir">The temporary data directory.</param>
    /// <param name="ct">Cancels the initialisation.</param>
    /// <param name="humanName">The Human's name.</param>
    public static Task<SqliteTeamDirectory> CreateDirectoryAsync(this TempDataDir dir, CancellationToken ct, string humanName = "You") =>
        dir.Options().CreateDirectoryAsync(ct, humanName);

    /// <summary>Creates a <see cref="SqliteTeamDirectory"/> over <paramref name="options"/> and initialises it, so the Human row exists.</summary>
    /// <param name="options">The options whose data directory holds the database.</param>
    /// <param name="ct">Cancels the initialisation.</param>
    /// <param name="humanName">The Human's name.</param>
    public static async Task<SqliteTeamDirectory> CreateDirectoryAsync(this IOptions<TeamOptions> options, CancellationToken ct, string humanName = "You")
    {
        SqliteTeamDirectory directory = options.CreateDirectory();
        await directory.InitializeAsync(humanName, ct);
        return directory;
    }

    /// <summary>Creates a <see cref="FileChatStore"/> over <paramref name="dir"/>.</summary>
    /// <param name="dir">The temporary data directory.</param>
    public static FileChatStore CreateChatStore(this TempDataDir dir) => dir.Options().CreateChatStore();

    /// <summary>Creates a <see cref="FileChatStore"/> over <paramref name="options"/>.</summary>
    /// <param name="options">The options whose data directory holds the transcripts.</param>
    public static FileChatStore CreateChatStore(this IOptions<TeamOptions> options) => new(options, NullLogger<FileChatStore>.Instance);

    /// <summary>Creates a real <see cref="PersonaStore"/> over <paramref name="dir"/>; also the store a Task store under test checks Team labels against.</summary>
    /// <param name="dir">The temporary data directory.</param>
    public static PersonaStore CreatePersonaStore(this TempDataDir dir) => dir.Options().CreatePersonaStore();

    /// <summary>Creates a real <see cref="PersonaStore"/> over <paramref name="options"/>.</summary>
    /// <param name="options">The options whose data directory holds the Agents.</param>
    public static PersonaStore CreatePersonaStore(this IOptions<TeamOptions> options) =>
        new(new AgentPaths(options), new PersonaModelStore(options), new PersonaEffortStore(options), new PersonaWorkModeStore(options), NullLogger<PersonaStore>.Instance);
}
