using Agency.Huddle.Seeder.Model;
using Agency.Huddle.Seeder.Scenarios;

namespace Agency.Huddle.Tests.Seeder;

/// <summary>Pins the <c>uat</c> scenario: two mock-adapter Agents and nothing a normal install would not have to opt into. Seeder spec §8.</summary>
public sealed class UatScenarioTests
{
    private static SeedPlan Plan() => new UatScenario().Build(SeededFolder.Today);

    /// <summary>SEED-UAT-1: The cast is exactly Ada and Bo, in no Team and holding no Skill, so they can never be mistaken for real Agents.</summary>
    [Fact]
    public void Plan_IsAdaAndBoOnly()
    {
        SeedPlan plan = Plan();

        Assert.Equal(["Ada", "Bo"], plan.Agents.Select(t => t.Name));
        Assert.All(plan.Agents, t => Assert.Empty(t.Teams));
        Assert.All(plan.Agents, t => Assert.Empty(t.Skills));
        Assert.Empty(plan.Teams);
        Assert.Empty(plan.Tasks);
        Assert.Empty(plan.Files);
        Assert.Empty(plan.Skills);
    }

    /// <summary>SEED-UAT-2: Each Agent has an empty one-to-one Room named after it, which is what the old demo agents left behind.</summary>
    [Fact]
    public void Plan_HasOneEmptyDirectRoomPerAgent()
    {
        SeedPlan plan = Plan();

        Assert.Equal(["Ada", "Bo"], plan.Rooms.Select(r => r.Name));
        Assert.All(plan.Rooms, r => Assert.Single(r.Members));
        Assert.All(plan.Rooms, r => Assert.Empty(r.Messages));
        Assert.All(plan.Rooms, r => Assert.False(r.Archived));
    }

    /// <summary>SEED-UAT-3: The scenario is reachable by name, and is never the default, so nothing runs it unless asked.</summary>
    [Fact]
    public void Catalog_ListsUatButDoesNotDefaultToIt()
    {
        IScenario? found = ScenarioCatalog.Find("uat");

        Assert.NotNull(found);
        Assert.NotEqual("uat", ScenarioCatalog.DefaultName);
    }
}
