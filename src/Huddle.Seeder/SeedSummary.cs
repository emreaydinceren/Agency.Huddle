using Agency.Huddle.Seeder.Model;
using Agency.Huddle.Seeder.Writing;

namespace Agency.Huddle.Seeder;

/// <summary>What was built.</summary>
/// <param name="Root">The seed root.</param>
/// <param name="DataDir">The data folder Huddle should be pointed at.</param>
/// <param name="Plan">The plan that was written.</param>
/// <param name="Ids">The ids the database assigned.</param>
internal sealed record SeedSummary(string Root, string DataDir, SeedPlan Plan, DatabaseResult Ids);
