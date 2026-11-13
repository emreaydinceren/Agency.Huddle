using System.Reflection;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Teams;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Agency.Huddle.Tests.SecondBrain;

/// <summary>Pins that the real host wires the Team rename journal into the tools and the permission guards the redirects rely on (batch 3 review).</summary>
public sealed class TeamRenameToolWiringTests
{
    private static ServiceProvider Build(TempDataDir dataDir)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Team:DataDir"] = dataDir.Path,
                ["Team:HumanName"] = "You",
                ["Team:DemoAgent:Enabled"] = "false",
                ["Team:Acp:Enabled"] = "false",
            })
            .Build();
        ServiceCollection services = new();
        services.AddLogging();
        services.AddTeamServices(configuration);
        return services.BuildServiceProvider();
    }

    /// <summary>The guard list the production factory builds for a Persona contains the <see cref="RenamedFolderGuard"/>.</summary>
    [Fact]
    public async Task DotAcpAgentHostFactory_Guards_ContainRenamedFolderGuard()
    {
        using TempDataDir dataDir = new();
        await using ServiceProvider provider = Build(dataDir);
        DotAcpAgentHostFactory factory = provider.GetRequiredService<DotAcpAgentHostFactory>();
        IPromptSource prompts = provider.GetRequiredService<IPromptSource>();
        BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        MethodInfo sourcesMethod = typeof(DotAcpAgentHostFactory).GetMethod("CreatePromptSources", flags) ?? throw new InvalidOperationException("CreatePromptSources is missing.");
        MethodInfo guardsMethod = typeof(DotAcpAgentHostFactory).GetMethod("CreateGuards", flags) ?? throw new InvalidOperationException("CreateGuards is missing.");
        object sources = sourcesMethod.Invoke(factory, [prompts]) ?? throw new InvalidOperationException("No sources.");

        object? guards = guardsMethod.Invoke(factory, [prompts, Path.Combine(dataDir.Path, "work"), sources]);

        Assert.Contains(Assert.IsAssignableFrom<IReadOnlyList<IToolCallGuard>>(guards), static guard => guard is RenamedFolderGuard);
    }

    /// <summary>The Memory tool and the file_request tool built the production way hold the rename journal.</summary>
    [Fact]
    public async Task MemoryAndFileRequestTools_BuiltByActivator_HoldTheRenameJournal()
    {
        using TempDataDir dataDir = new();
        await using ServiceProvider provider = Build(dataDir);

        Assert.NotNull(JournalOf(ActivatorUtilities.CreateInstance<MemoryTool>(provider, "Nova")));
        Assert.NotNull(JournalOf(ActivatorUtilities.CreateInstance<FileRequestTool>(provider, "agent-1")));
    }

    private static object? JournalOf(object tool)
    {
        FieldInfo field = Assert.Single(
            tool.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public),
            static f => f.FieldType == typeof(ITeamRenames));
        return field.GetValue(tool);
    }
}
