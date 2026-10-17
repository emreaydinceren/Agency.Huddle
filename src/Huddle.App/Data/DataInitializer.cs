using Microsoft.Extensions.Options;
using Agency.Huddle.App.Profile;

namespace Agency.Huddle.App.Data;

/// <summary>
/// Creates the Team Directory at startup and gives the Human's row the profile's current Name. When
/// the row carried a different Name - the Human was renamed while the app was stopped, by a hand edit
/// to <c>profile.json</c> or a changed <see cref="TeamOptions.HumanName"/> before any profile was
/// saved - the rest of the rename cascades too, so Tasks and the Avatar follow.
/// </summary>
internal sealed class DataInitializer : IHostedService
{
    private readonly ITeamDirectory teamDirectory;
    private readonly TeamOptions options;
    private readonly IHumanIdentity human;
    private readonly HumanRenameCascade cascade;

    /// <summary>Wires the initializer.</summary>
    /// <param name="teamDirectory">The directory to create and seed.</param>
    /// <param name="options">Supplies <see cref="TeamOptions.DataDir"/>.</param>
    /// <param name="human">Supplies the Human's current Name.</param>
    /// <param name="cascade">Moves Tasks, Views and the Avatar after a rename made while stopped.</param>
    public DataInitializer(ITeamDirectory teamDirectory, IOptions<TeamOptions> options, IHumanIdentity human, HumanRenameCascade cascade)
    {
        ArgumentNullException.ThrowIfNull(teamDirectory);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(human);
        ArgumentNullException.ThrowIfNull(cascade);

        this.teamDirectory = teamDirectory;
        this.options = options.Value;
        this.human = human;
        this.cascade = cascade;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(this.options.DataDir);

        var previous = await this.teamDirectory.ReadHumanNameAsync(cancellationToken);
        var name = this.human.Name;
        await this.teamDirectory.InitializeAsync(name, cancellationToken);

        if (previous is not null && !string.Equals(previous, name, StringComparison.Ordinal))
        {
            this.cascade.Run(previous, name);
        }
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
