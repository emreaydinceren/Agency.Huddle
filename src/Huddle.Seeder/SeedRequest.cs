using Agency.Huddle.Seeder.Scenarios;

namespace Agency.Huddle.Seeder;

/// <summary>What the caller asked the seeder to build. Seeder spec §3.</summary>
/// <param name="Scenario">The scenario to build.</param>
/// <param name="Root">The seed root; wiped first.</param>
/// <param name="Today">The run date every offset is measured from.</param>
internal sealed record SeedRequest(IScenario Scenario, string Root, DateOnly Today);
