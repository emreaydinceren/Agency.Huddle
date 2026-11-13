using System.Reflection;
using Agency.Huddle.App;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Teams;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Agency.Huddle.Tests.SecondBrain;

/// <summary>Pins that the real host fills the optional Chat service and Prompt source of the Team rename (corrections-batch3 tr10 item 2).</summary>
public sealed class TeamRenameRoomLineRegistrationTests
{
    /// <summary>The resolved rename service holds the host's Chat service and Prompt source, so the Team Room line is posted in production.</summary>
    [Fact]
    public async Task AddTeamServices_RenameServiceGetsTheChatServiceAndPromptSource()
    {
        using TempDataDir dataDir = new();
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

        await using ServiceProvider provider = services.BuildServiceProvider();

        TeamRenameService rename = provider.GetRequiredService<TeamRenameService>();
        Assert.Same(provider.GetRequiredService<ChatService>(), HeldOf<ChatService>(rename));
        Assert.Same(provider.GetRequiredService<IPromptSource>(), HeldOf<IPromptSource>(rename));
    }

    /// <summary>The one instance of <typeparamref name="T"/> a service holds, found by field type.</summary>
    /// <typeparam name="T">The field type.</typeparam>
    /// <param name="service">The service.</param>
    private static T HeldOf<T>(object service)
        where T : class
    {
        FieldInfo field = Assert.Single(
            service.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public),
            static f => f.FieldType == typeof(T));
        return Assert.IsAssignableFrom<T>(field.GetValue(service));
    }
}
