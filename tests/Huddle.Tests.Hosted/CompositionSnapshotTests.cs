using System.Text;
using Agency.Huddle.App;
using Agency.Huddle.App.Sidebar;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Agency.Huddle.Tests;

/// <summary>
/// Pins what <see cref="ServiceCollectionExtensions.AddTeamServices"/> registers while wiring moves into Feature Modules, so a move
/// that loses or reorders something fails here. Only registration order that can change behaviour is pinned: services are sorted by
/// type, entries of a type registered more than once keep their order, because a consumer of the list sees that order, and sidebar
/// panels sort by <see cref="SidebarPanel.Order"/>, as the catalog does, then by id: panels that tie apply to different Subjects, and
/// <c>SidebarPanelRegistrationTests</c> pins each Subject's list. A change that means to alter the wiring deletes
/// <c>Acp/Golden/composition.txt</c> and reseeds it as the prompt goldens do. This is scaffolding for the move: delete it with the
/// golden once the migration ends, because it makes every Pull Request that registers a service edit one more shared file.
/// </summary>
public sealed class CompositionSnapshotTests
{
    /// <summary>The default configuration registers exactly what the golden lists.</summary>
    [Fact]
    public void AddTeamServices_MatchesTheGolden()
    {
        using TempDataDir dataDir = new();
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Team:DataDir"] = dataDir.Path })
            .Build();
        ServiceCollection services = new();

        services.AddTeamServices(configuration);

        AssertMatchesGolden(Snapshot(services));
    }

    /// <summary>Renders the Huddle registrations, one per line, grouped by service type.</summary>
    /// <param name="services">The composed services.</param>
    /// <returns>The snapshot text, with LF line ends.</returns>
    private static string Snapshot(ServiceCollection services)
    {
        StringBuilder text = new();
        foreach (IGrouping<string, ServiceDescriptor> group in services
            .GroupBy(static descriptor => descriptor.ServiceType.ToString(), StringComparer.Ordinal)
            .OrderBy(static group => group.Key, StringComparer.Ordinal))
        {
            IEnumerable<ServiceDescriptor> ordered = group.Key == typeof(SidebarPanel).ToString()
                ? group
                    .OrderBy(static descriptor => descriptor.ImplementationInstance is SidebarPanel panel ? panel.Order : int.MaxValue)
                    .ThenBy(static descriptor => descriptor.ImplementationInstance is SidebarPanel panel ? panel.Id : string.Empty, StringComparer.Ordinal)
                : group;
            foreach (ServiceDescriptor descriptor in ordered)
            {
                string line = $"{descriptor.Lifetime} {group.Key} -> {Implementation(descriptor)}";
                if (line.Contains("Agency.Huddle", StringComparison.Ordinal))
                {
                    text.Append(line).Append('\n');
                }
            }
        }

        return text.ToString();
    }

    /// <summary>Names what a registration resolves to, whichever way it was registered.</summary>
    /// <param name="descriptor">The registration.</param>
    /// <returns>The implementation's name.</returns>
    private static string Implementation(ServiceDescriptor descriptor)
    {
        if (descriptor.IsKeyedService)
        {
            return $"keyed({descriptor.ServiceKey}) {descriptor.KeyedImplementationType?.ToString() ?? "instance or factory"}";
        }

        return descriptor switch
        {
            { ImplementationInstance: SidebarPanel panel } => $"panel {panel.Id} order {panel.Order} {panel.Component}",
            { ImplementationInstance: { } instance } => $"instance {instance.GetType()}",
            { ImplementationFactory: { } factory } => $"factory {factory.GetType().GenericTypeArguments[^1]}",
            { ImplementationType: { } type } => type.ToString(),
            _ => "unknown",
        };
    }

    /// <summary>Compares with the golden, seeding it and failing once when it does not exist yet.</summary>
    /// <param name="actual">The snapshot text.</param>
    private static void AssertMatchesGolden(string actual)
    {
        string path = RepoPaths.Data(Path.Combine("Acp", "Golden", "composition.txt"));
        if (!File.Exists(path))
        {
            File.WriteAllText(path, actual);
            Assert.Fail($"Golden '{path}' did not exist and has been seeded. Inspect it, then re-run.");
        }

        Assert.Equal(File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal), actual);
    }
}
