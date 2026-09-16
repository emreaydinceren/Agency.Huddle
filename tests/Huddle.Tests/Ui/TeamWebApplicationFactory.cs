using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Pipes;
using Agency.Huddle.Tests.Acp.Fakes;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Hosts the real <see cref="Agency.Huddle.App"/> composition root (via <c>Program</c> and
/// <see cref="Agency.Huddle.App.ServiceCollectionExtensions.AddTeamServices"/>) bound to a unique named pipe and an
/// isolated temp data directory, so UI host tests exercise a real, fully-composed application over HTTP.
/// </summary>
public sealed class TeamWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly TempDataDir dataDir = new();

    public string PipeName { get; } = "team-test-" + Guid.NewGuid().ToString("N");

    /// <summary>
    /// The Team Library directory this factory's data dir resolves to, matching
    /// <see cref="Agency.Huddle.App.Acp.AcpOptions"/>'s default <c>TeamsDir</c> ("Teams"), which is never
    /// overridden by <see cref="ConfigureWebHost"/>. Tests use this to seed Persona files directly.
    /// </summary>
    public string TeamsDirPath => Path.Combine(this.dataDir.Path, "Teams");

    /// <summary>
    /// The path <see cref="Agency.Huddle.App.Hooks.HookStore"/> resolves its override file to under
    /// this factory's isolated data directory, i.e. <see cref="Agency.Huddle.App.Hooks.HookStore.FilePath"/>'s
    /// value for the instance this factory's app composes. Tests assert the Settings page shows this
    /// exact path rather than a hardcoded guess, since <c>Team:DataDir</c> is redirected to a fresh
    /// temp directory per factory.
    /// </summary>
    public string HooksJsonPath => Path.Combine(this.dataDir.Path, "hooks.json");

    /// <summary>
    /// The path <see cref="Agency.Huddle.App.Appearance.AppearanceStore"/> resolves its override file
    /// to under this factory's isolated data directory, i.e.
    /// <see cref="Agency.Huddle.App.Appearance.AppearanceStore.FilePath"/>'s value for the instance
    /// this factory's app composes. Tests write here before <see cref="WebApplicationFactory{TEntryPoint}.CreateClient()"/>
    /// so the store's constructor reads it on first resolve.
    /// </summary>
    public string AppearanceJsonPath => Path.Combine(this.dataDir.Path, "appearance.json");

    /// <summary>
    /// The <see cref="IModelCatalog"/> this factory wires in place of the real
    /// <see cref="ModelCatalogProbe"/>. Both fixtures must set <c>DemoAgent:Enabled=false</c>
    /// because a real demo agent spends nothing but still needs a Human to be online; this is the
    /// same idea one step further — the real catalog spawns an adapter *process*, and no test may.
    /// </summary>
    public FakeModelCatalog FakeModelCatalog { get; } = new();

    /// <summary>
    /// The <see cref="IAgentGateway"/> this factory wires in place of the real <c>AgentGateway</c>.
    /// Both default to every Agent offline, so a test that never touches this behaves exactly as one
    /// that never knew this fake existed; a test can call <see cref="FakeAgentGateway.SetOnline"/> to
    /// prove the member-health strip and status badges reflect a truly connected Agent.
    /// </summary>
    internal FakeAgentGateway FakeAgentGateway { get; } = new();

    /// <summary>
    /// Overrides <see cref="Agency.Huddle.App.TeamOptions.AgentMessageBudget"/> (default 40) for this
    /// factory's host, so a UI test can pause a Room without posting forty Messages first. Tests set
    /// this before <see cref="WebApplicationFactory{TEntryPoint}.CreateClient()"/> — the same
    /// constraint <see cref="AppearanceJsonPath"/>'s doc records for its file — because that is when
    /// the host is built and <see cref="ConfigureWebHost"/> runs.
    /// </summary>
    public int? AgentMessageBudget { get; set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // WebApplicationFactory<Program> resolves the content root from assembly metadata, which points at
        // the test project rather than Huddle.App. Static assets are served through the build-time manifest
        // that MapStaticAssets publishes for the real project, so the content root must be the actual
        // src/Huddle.App directory rather than a copy of its wwwroot into the test project.
        builder.UseContentRoot(FindAppContentRoot());

        builder.UseSetting("Team:PipeName", this.PipeName);
        builder.UseSetting("Team:DataDir", this.dataDir.Path);
        builder.UseSetting("Team:HumanName", "You");
        builder.UseSetting("Team:DemoAgent:Enabled", "false");
        builder.UseSetting("Team:Acp:Enabled", "false");

        if (this.AgentMessageBudget is int budget)
        {
            builder.UseSetting("Team:AgentMessageBudget", budget.ToString(CultureInfo.InvariantCulture));
        }

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IModelCatalog>(this.FakeModelCatalog);
            services.AddSingleton<IAgentGateway>(this.FakeAgentGateway);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            this.dataDir.Dispose();
        }
    }

    private static string FindAppContentRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Huddle.slnx")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException(
                $"Could not locate Huddle.slnx by walking up from '{AppContext.BaseDirectory}'.");
        }

        return Path.Combine(directory.FullName, "src", "Huddle.App");
    }
}