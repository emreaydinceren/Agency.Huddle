using Agency.Huddle.Seeder.Model;

namespace Agency.Huddle.Seeder.Scenarios;

/// <summary>A named dataset the seeder can build. Adding a scenario means adding one implementation and listing it in <see cref="ScenarioCatalog"/>.</summary>
internal interface IScenario
{
    /// <summary>The name used on the command line, such as <c>software-co</c>.</summary>
    string Name { get; }

    /// <summary>One line describing what the scenario contains.</summary>
    string Description { get; }

    /// <summary>Builds the plan with every date measured from <paramref name="today"/>.</summary>
    /// <param name="today">The run date.</param>
    /// <returns>The plan the seeder writes out.</returns>
    SeedPlan Build(DateOnly today);
}
