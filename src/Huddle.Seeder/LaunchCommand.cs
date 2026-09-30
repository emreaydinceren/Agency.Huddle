namespace Agency.Huddle.Seeder;

/// <summary>Builds the command that runs Huddle against a seeded data folder without spending money.</summary>
internal static class LaunchCommand
{
    /// <summary>The port used for the seeded instance, chosen not to clash with the normal one on 5100.</summary>
    internal const int Port = 5199;

    /// <summary>Builds the <c>dotnet run</c> command line.</summary>
    /// <param name="dataDir">The seeded data folder.</param>
    /// <returns>A single command line, ready to paste into a shell.</returns>
    internal static string Build(string dataDir)
    {
        string? repo = SeedRootGuard.FindRepositoryRoot(AppContext.BaseDirectory) ?? SeedRootGuard.FindRepositoryRoot(Environment.CurrentDirectory);
        string mock = repo is null
            ? "<path to mock-acp.exe>"
            : Path.Combine(repo, "src", "Huddle.MockAdapter", "bin", "Debug", "net10.0", "mock-acp.exe");
        string project = repo is null ? "src/Huddle.App" : Path.Combine(repo, "src", "Huddle.App");

        return string.Join(' ',
            "dotnet run --project", Quote(project), "--",
            $"--urls http://localhost:{Port}",
            Quote($"--Team:DataDir={dataDir}"),
            "--Team:PipeName=huddle-seed",
            "--Team:DemoAgent:Enabled=false",
            "--Team:Tasks:WakeEnabled=false",
            "--Team:Acp:Enabled=true",
            "--Team:Acp:Adapters:0:Id=mock",
            Quote($"--Team:Acp:Adapters:0:Command={mock}"),
            "--Team:Acp:Adapters:0:Args:0=--mock",
            "--Team:Acp:Adapters:0:UsesToolNamePrefix=false");
    }

    private static string Quote(string value) => value.Contains(' ', StringComparison.Ordinal) ? $"\"{value}\"" : value;
}
