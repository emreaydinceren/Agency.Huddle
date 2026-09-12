using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Data;

public sealed class DataInitializer : IHostedService
{
    private readonly ITeamDirectory teamDirectory;
    private readonly TeamOptions options;

    public DataInitializer(ITeamDirectory teamDirectory, IOptions<TeamOptions> options)
    {
        ArgumentNullException.ThrowIfNull(teamDirectory);
        ArgumentNullException.ThrowIfNull(options);

        this.teamDirectory = teamDirectory;
        this.options = options.Value;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(this.options.DataDir);
        return this.teamDirectory.InitializeAsync(this.options.HumanName, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}