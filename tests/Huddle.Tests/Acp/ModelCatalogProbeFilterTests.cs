using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Acp;

namespace Agency.Huddle.Tests.Acp;

/// <summary>
/// Tests <see cref="ModelCatalogProbe.WithoutAdapterDefault"/> in isolation. This is deliberately the
/// only kind of test <see cref="ModelCatalogProbe"/> gets in this suite: the method is pure — no I/O,
/// no adapter process — so it is reachable directly through <c>InternalsVisibleTo</c> without
/// tripping <c>docs/engineering/rules.md</c> row 35 ("No test may reach the real
/// <see cref="ModelCatalogProbe"/>"), which spawns a real <c>node</c> process.
/// </summary>
public sealed class ModelCatalogProbeFilterTests
{
    [Fact]
    public void WithoutAdapterDefault_DropsTheAdaptersDefaultSentinel()
    {
        var levels = new List<AgentEffortOption>
        {
            new("default", "Default", null),
            new("high", "High", "Extra reasoning time"),
        };

        var result = ModelCatalogProbe.WithoutAdapterDefault(levels);

        Assert.DoesNotContain(result, level => level.Id == "default");
    }

    [Fact]
    public void WithoutAdapterDefault_KeepsEveryOtherLevelInWireOrder()
    {
        var levels = new List<AgentEffortOption>
        {
            new("default", "Default", null),
            new("low", "Low", null),
            new("medium", "Medium", null),
            new("high", "High", null),
        };

        var result = ModelCatalogProbe.WithoutAdapterDefault(levels);

        Assert.Equal(["low", "medium", "high"], result.Select(level => level.Id));
    }

    [Fact]
    public void WithoutAdapterDefault_NoDefaultEntry_ReturnsTheListUnchanged()
    {
        var levels = new List<AgentEffortOption>
        {
            new("low", "Low", null),
            new("high", "High", null),
        };

        var result = ModelCatalogProbe.WithoutAdapterDefault(levels);

        Assert.Equal(levels, result);
    }

    [Fact]
    public void WithoutAdapterDefault_EmptyList_ReturnsEmpty()
    {
        IReadOnlyList<AgentEffortOption> levels = [];

        var result = ModelCatalogProbe.WithoutAdapterDefault(levels);

        Assert.Empty(result);
    }
}
