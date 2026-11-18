using Agency.Huddle.App.Library;
using Agency.Huddle.App.Teams;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.SecondBrain;

/// <summary>
/// One-shot start-up write of the default <c>Teams/AGENTS.md</c>: the embedded conduct file is written only when the
/// Human has no such file yet, and an existing file is never touched (Spec §6.4, D-40).
/// </summary>
/// <remarks>It also prunes the Playbook defaults no Team's Playbook file names (Team Playbooks spec §5.2), the other Teams-folder housekeeping done once at start.</remarks>
/// <param name="options">The app options, for the data folder.</param>
/// <param name="playbooks">The Playbook store, whose unused bases are pruned; <see langword="null"/> prunes nothing.</param>
internal sealed class DefaultAgentsMdService(IOptions<TeamOptions> options, PlaybookStore? playbooks = null) : IHostedService
{
    private const string ResourceName = "Overlays/second-brain/AGENTS.md";

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        playbooks?.PruneBases();
        string teams = Path.Combine(options.Value.DataDir, "Teams");
        string path = Path.Combine(teams, TeamPaths.AgentsMdFileName);
        if (File.Exists(path))
        {
            return Task.CompletedTask;
        }

        Directory.CreateDirectory(teams);
        AtomicFile.WriteBytes(path, EmbeddedBytes());
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Reads the embedded conduct file's raw bytes, so the written file matches the resource byte for byte.</summary>
    /// <returns>The resource's bytes.</returns>
    private static byte[] EmbeddedBytes()
    {
        using Stream stream = EmbeddedResources.Open(typeof(DefaultAgentsMdService).Assembly, ResourceName);
        using MemoryStream buffer = new();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
