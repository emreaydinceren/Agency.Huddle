namespace Agency.Huddle.Seeder.Scenarios;

/// <summary>The scenarios the seeder knows about.</summary>
internal static class ScenarioCatalog
{
    /// <summary>The scenario used when none is named.</summary>
    internal const string DefaultName = "software-co";

    /// <summary>Every scenario, in the order they are listed in help.</summary>
    internal static IReadOnlyList<IScenario> All { get; } = [new SoftwareCoScenario()];

    /// <summary>Finds a scenario by name, ignoring case.</summary>
    /// <param name="name">The scenario name.</param>
    /// <returns>The scenario, or <see langword="null"/> when none has that name.</returns>
    internal static IScenario? Find(string name) =>
        All.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
}
