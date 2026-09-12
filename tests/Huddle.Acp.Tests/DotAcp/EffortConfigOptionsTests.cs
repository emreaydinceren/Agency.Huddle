namespace Agency.Huddle.Acp.Tests.DotAcp;

using System.Collections.Generic;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.Acp.DotAcp;
using Xunit;

/// <summary>
/// Covers reading the "thought_level" category <c>configOptions</c> entry and resolving a
/// requested effort id against it. Mirrors <see cref="ModelConfigOptionsTests"/>'s structure and
/// fixture shape; the differences pinned here are the ones that make effort NOT a copy of model:
/// an absent thought_level entry is the expected case, the adapter's own id ("thinking" in these
/// fixtures) must never be assumed to be "effort", and the "default" sentinel must survive
/// <see cref="EffortConfigOptions.Read"/> unfiltered.
/// </summary>
public sealed class EffortConfigOptionsTests
{
    /// <summary>A flat option list is flattened in wire order, options unchanged.</summary>
    [Fact]
    public void Read_FlatThoughtLevelOption_ReturnsOptionsInWireOrder()
    {
        dotacp.protocol.SessionConfigOption[] configOptions = EffortConfigOptionsTests.CreateFlatConfigOptions();

        IReadOnlyList<AgentEffortOption> levels = EffortConfigOptions.Read(configOptions);

        Assert.Equal(
            new[]
            {
                new AgentEffortOption("default", "Default", null),
                new AgentEffortOption("low", "Low", null),
                new AgentEffortOption("high", "High", "More thorough reasoning"),
            },
            levels);
    }

    /// <summary>A grouped option list is flattened across groups, in wire order.</summary>
    [Fact]
    public void Read_GroupedThoughtLevelOption_FlattensGroupsInWireOrder()
    {
        dotacp.protocol.SessionConfigSelectGroup[] groups =
        [
            new dotacp.protocol.SessionConfigSelectGroup
            {
                Group = "standard",
                Name = "Standard",
                Options =
                [
                    new dotacp.protocol.SessionConfigSelectOption { Value = "default", Name = "Default", Description = null },
                ],
            },
            new dotacp.protocol.SessionConfigSelectGroup
            {
                Group = "extended",
                Name = "Extended",
                Options =
                [
                    new dotacp.protocol.SessionConfigSelectOption { Value = "max", Name = "Max", Description = null },
                ],
            },
        ];
        dotacp.protocol.SessionConfigOption[] configOptions =
        [
            new dotacp.protocol.SessionConfigSelect
            {
                Id = "thinking",
                Name = "Effort",
                Category = dotacp.protocol.SessionConfigOptionCategory.ThoughtLevel,
                CurrentValue = "default",
                Options = groups,
            },
        ];

        IReadOnlyList<AgentEffortOption> levels = EffortConfigOptions.Read(configOptions);

        Assert.Equal(
            new[]
            {
                new AgentEffortOption("default", "Default", null),
                new AgentEffortOption("max", "Max", null),
            },
            levels);
    }

    /// <summary>No "thought_level" category option present at all means an empty, not null, result.</summary>
    [Fact]
    public void Read_NoThoughtLevelCategoryOption_ReturnsEmpty()
    {
        dotacp.protocol.SessionConfigOption[] configOptions =
        [
            new dotacp.protocol.SessionConfigSelect
            {
                Id = "mode",
                Name = "Mode",
                Category = dotacp.protocol.SessionConfigOptionCategory.Mode,
                CurrentValue = "default",
                Options = new dotacp.protocol.SessionConfigSelectOption[]
                {
                    new dotacp.protocol.SessionConfigSelectOption { Value = "default", Name = "Default", Description = null },
                },
            },
        ];

        IReadOnlyList<AgentEffortOption> levels = EffortConfigOptions.Read(configOptions);

        Assert.Empty(levels);
    }

    /// <summary>A null <c>configOptions</c> array (the agent never sent one) reads as empty.</summary>
    [Fact]
    public void Read_NullConfigOptions_ReturnsEmpty()
    {
        IReadOnlyList<AgentEffortOption> levels = EffortConfigOptions.Read(null);

        Assert.Empty(levels);
    }

    /// <summary>Proves the filter is by category, not "any select present".</summary>
    [Fact]
    public void Read_ModelCategoryOnly_ReturnsEmpty()
    {
        dotacp.protocol.SessionConfigOption[] configOptions =
        [
            new dotacp.protocol.SessionConfigSelect
            {
                Id = "model",
                Name = "Model",
                Category = dotacp.protocol.SessionConfigOptionCategory.Model,
                CurrentValue = "sonnet",
                Options = new dotacp.protocol.SessionConfigSelectOption[]
                {
                    new dotacp.protocol.SessionConfigSelectOption { Value = "sonnet", Name = "Sonnet", Description = null },
                },
            },
        ];

        IReadOnlyList<AgentEffortOption> levels = EffortConfigOptions.Read(configOptions);

        Assert.Empty(levels);
    }

    /// <summary>Pins that the protocol layer does NOT filter "default" - that is an app-layer decision made elsewhere.</summary>
    [Fact]
    public void Read_KeepsTheAdaptersDefaultSentinel()
    {
        dotacp.protocol.SessionConfigOption[] configOptions = EffortConfigOptionsTests.CreateFlatConfigOptions();

        IReadOnlyList<AgentEffortOption> levels = EffortConfigOptions.Read(configOptions);

        Assert.Contains(levels, option => option.Id == "default");
    }

    /// <summary>A matching value resolves to its owning option's own id and the requested value.</summary>
    [Fact]
    public void TryResolve_MatchingValue_ReturnsOwningConfigIdAndValue()
    {
        dotacp.protocol.SessionConfigOption[] configOptions = EffortConfigOptionsTests.CreateFlatConfigOptions();

        bool resolved = EffortConfigOptions.TryResolve(
            configOptions, "high", out dotacp.protocol.SessionConfigId configId, out dotacp.protocol.SessionConfigValueId value);

        Assert.True(resolved);
        Assert.Equal("thinking", (string)configId);
        Assert.Equal("high", (string)value);
    }

    /// <summary>A value absent from the catalog resolves to false, never throws.</summary>
    [Fact]
    public void TryResolve_ValueNotInCatalog_ReturnsFalse()
    {
        dotacp.protocol.SessionConfigOption[] configOptions = EffortConfigOptionsTests.CreateFlatConfigOptions();

        bool resolved = EffortConfigOptions.TryResolve(configOptions, "ultra", out _, out _);

        Assert.False(resolved);
    }

    /// <summary>No effort option at all resolves to false, never throws.</summary>
    [Fact]
    public void TryResolve_NoThoughtLevelOption_ReturnsFalse()
    {
        bool resolved = EffortConfigOptions.TryResolve(null, "low", out _, out _);

        Assert.False(resolved);
    }

    /// <summary>The never-hardcode guard: the fixture's id is "thinking", not "effort" or "thought_level".</summary>
    [Fact]
    public void TryResolve_ConfigIdIsTheOptionsOwnId_NotTheCategoryName()
    {
        dotacp.protocol.SessionConfigOption[] configOptions = EffortConfigOptionsTests.CreateFlatConfigOptions();

        bool resolved = EffortConfigOptions.TryResolve(
            configOptions, "low", out dotacp.protocol.SessionConfigId configId, out _);

        Assert.True(resolved);
        Assert.Equal("thinking", (string)configId);
    }

    /// <summary>Builds a flat "thought_level" configOptions array, its owning id deliberately "thinking".</summary>
    private static dotacp.protocol.SessionConfigOption[] CreateFlatConfigOptions()
    {
        return
        [
            new dotacp.protocol.SessionConfigSelect
            {
                Id = "thinking",
                Name = "Effort",
                Description = "Available effort levels for this model",
                Category = dotacp.protocol.SessionConfigOptionCategory.ThoughtLevel,
                CurrentValue = "default",
                Options = new dotacp.protocol.SessionConfigSelectOption[]
                {
                    new dotacp.protocol.SessionConfigSelectOption { Value = "default", Name = "Default", Description = null },
                    new dotacp.protocol.SessionConfigSelectOption { Value = "low", Name = "Low", Description = null },
                    new dotacp.protocol.SessionConfigSelectOption { Value = "high", Name = "High", Description = "More thorough reasoning" },
                },
            },
        ];
    }
}
