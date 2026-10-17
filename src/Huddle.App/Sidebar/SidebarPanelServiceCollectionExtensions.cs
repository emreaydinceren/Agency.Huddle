namespace Agency.Huddle.App.Sidebar;

/// <summary>Registers Right sidebar panels for the catalog to find.</summary>
internal static class SidebarPanelServiceCollectionExtensions
{
    /// <summary>Registers one panel descriptor as a singleton, for <see cref="SidebarPanelCatalog"/> to read.</summary>
    /// <param name="services">The service collection to add the panel to.</param>
    /// <param name="panel">The panel descriptor to register.</param>
    /// <returns>The same service collection, for chaining.</returns>
    internal static IServiceCollection AddSidebarPanel(this IServiceCollection services, SidebarPanel panel)
    {
        return services.AddSingleton(panel);
    }
}
