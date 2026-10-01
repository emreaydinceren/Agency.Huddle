using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.Acp.DotAcp;
using Xunit;

namespace Agency.Huddle.Acp.Tests.DotAcp;

/// <summary>
/// Covers reading the "mode" category <c>configOptions</c> entry and resolving a requested mode id
/// against it. Mirrors <see cref="EffortConfigOptionsTests"/>; the differences pinned here are the
/// ones that make a mode NOT a copy of effort: the adapter's "default" is a real mode (Manual) and
/// must survive <see cref="ModeConfigOptions.Read"/>, and the owning id in these fixtures is
/// "permission", never the "mode" an adapter happens to use.
/// </summary>
public sealed class ModeConfigOptionsTests
{
    /// <summary>A flat option list is flattened in wire order, with each description kept.</summary>
    [Fact]
    public void Read_FlatModeOption_ReturnsOptionsInWireOrder()
    {
        dotacp.protocol.SessionConfigOption[] configOptions = ModeConfigOptionsTests.CreateFlatConfigOptions();

        IReadOnlyList<AgentModeOption> modes = ModeConfigOptions.Read(configOptions);

        Assert.Equal(
            [
                new AgentModeOption("default", "Manual", "Always ask before making changes"),
                new AgentModeOption("acceptEdits", "Accept edits", null),
                new AgentModeOption("plan", "Plan", "Create a plan before making changes"),
            ],
            modes);
    }

    /// <summary>A grouped option list is flattened across groups, in wire order.</summary>
    [Fact]
    public void Read_GroupedModeOption_FlattensGroupsInWireOrder()
    {
        dotacp.protocol.SessionConfigSelectGroup[] groups =
        [
            new dotacp.protocol.SessionConfigSelectGroup
            {
                Group = "standard",
                Name = "Standard",
                Options = [new dotacp.protocol.SessionConfigSelectOption { Value = "default", Name = "Manual", Description = null }],
            },
            new dotacp.protocol.SessionConfigSelectGroup
            {
                Group = "elevated",
                Name = "Elevated",
                Options = [new dotacp.protocol.SessionConfigSelectOption { Value = "auto", Name = "Auto", Description = null }],
            },
        ];
        dotacp.protocol.SessionConfigOption[] configOptions =
        [
            new dotacp.protocol.SessionConfigSelect
            {
                Id = "permission",
                Name = "Mode",
                Category = dotacp.protocol.SessionConfigOptionCategory.Mode,
                CurrentValue = "default",
                Options = groups,
            },
        ];

        IReadOnlyList<AgentModeOption> modes = ModeConfigOptions.Read(configOptions);

        Assert.Equal(
            [
                new AgentModeOption("default", "Manual", null),
                new AgentModeOption("auto", "Auto", null),
            ],
            modes);
    }

    /// <summary>No "mode" category option present at all means an empty, not null, result.</summary>
    [Fact]
    public void Read_NoModeCategoryOption_ReturnsEmpty()
    {
        dotacp.protocol.SessionConfigOption[] configOptions =
        [
            new dotacp.protocol.SessionConfigSelect
            {
                Id = "effort",
                Name = "Effort",
                Category = dotacp.protocol.SessionConfigOptionCategory.ThoughtLevel,
                CurrentValue = "default",
                Options = new dotacp.protocol.SessionConfigSelectOption[]
                {
                    new dotacp.protocol.SessionConfigSelectOption { Value = "default", Name = "Default", Description = null },
                },
            },
        ];

        IReadOnlyList<AgentModeOption> modes = ModeConfigOptions.Read(configOptions);

        Assert.Empty(modes);
    }

    /// <summary>A null <c>configOptions</c> array (the agent never sent one) reads as empty.</summary>
    [Fact]
    public void Read_NullConfigOptions_ReturnsEmpty()
    {
        IReadOnlyList<AgentModeOption> modes = ModeConfigOptions.Read(null);

        Assert.Empty(modes);
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

        IReadOnlyList<AgentModeOption> modes = ModeConfigOptions.Read(configOptions);

        Assert.Empty(modes);
    }

    /// <summary>"default" is Manual, a real mode: unlike Effort's sentinel it must not be filtered.</summary>
    [Fact]
    public void Read_KeepsDefaultBecauseItIsTheManualMode()
    {
        dotacp.protocol.SessionConfigOption[] configOptions = ModeConfigOptionsTests.CreateFlatConfigOptions();

        IReadOnlyList<AgentModeOption> modes = ModeConfigOptions.Read(configOptions);

        Assert.Contains(modes, mode => mode.Id == "default" && mode.Name == "Manual");
    }

    /// <summary>A matching value resolves to its owning option's own id and the requested value.</summary>
    [Fact]
    public void TryResolve_MatchingValue_ReturnsOwningConfigIdAndValue()
    {
        dotacp.protocol.SessionConfigOption[] configOptions = ModeConfigOptionsTests.CreateFlatConfigOptions();

        bool resolved = ModeConfigOptions.TryResolve(
            configOptions, "plan", out dotacp.protocol.SessionConfigId configId, out dotacp.protocol.SessionConfigValueId value);

        Assert.True(resolved);
        Assert.Equal("permission", (string)configId);
        Assert.Equal("plan", (string)value);
    }

    /// <summary>A value absent from the catalog resolves to false, never throws.</summary>
    [Fact]
    public void TryResolve_ValueNotInCatalog_ReturnsFalse()
    {
        dotacp.protocol.SessionConfigOption[] configOptions = ModeConfigOptionsTests.CreateFlatConfigOptions();

        bool resolved = ModeConfigOptions.TryResolve(configOptions, "bypassPermissions", out _, out _);

        Assert.False(resolved);
    }

    /// <summary>No mode option at all resolves to false, never throws.</summary>
    [Fact]
    public void TryResolve_NoModeOption_ReturnsFalse()
    {
        bool resolved = ModeConfigOptions.TryResolve(null, "plan", out _, out _);

        Assert.False(resolved);
    }

    /// <summary>A select entry that is not a select (no options) resolves to false, never throws.</summary>
    [Fact]
    public void TryResolve_ModeOptionIsNotASelect_ReturnsFalse()
    {
        dotacp.protocol.SessionConfigOption[] configOptions =
        [
            new dotacp.protocol.SessionConfigBoolean
            {
                Id = "permission",
                Name = "Mode",
                Category = dotacp.protocol.SessionConfigOptionCategory.Mode,
                CurrentValue = true,
            },
        ];

        bool resolved = ModeConfigOptions.TryResolve(configOptions, "plan", out _, out _);

        Assert.False(resolved);
    }

    /// <summary>The never-hardcode guard: the fixture's id is "permission", not "mode".</summary>
    [Fact]
    public void TryResolve_ConfigIdIsTheOptionsOwnId_NotTheCategoryName()
    {
        dotacp.protocol.SessionConfigOption[] configOptions = ModeConfigOptionsTests.CreateFlatConfigOptions();

        bool resolved = ModeConfigOptions.TryResolve(
            configOptions, "acceptEdits", out dotacp.protocol.SessionConfigId configId, out _);

        Assert.True(resolved);
        Assert.Equal("permission", (string)configId);
    }

    /// <summary>Builds a flat "mode" configOptions array, its owning id deliberately "permission".</summary>
    private static dotacp.protocol.SessionConfigOption[] CreateFlatConfigOptions()
    {
        return
        [
            new dotacp.protocol.SessionConfigSelect
            {
                Id = "permission",
                Name = "Mode",
                Description = "Session permission mode",
                Category = dotacp.protocol.SessionConfigOptionCategory.Mode,
                CurrentValue = "default",
                Options = new dotacp.protocol.SessionConfigSelectOption[]
                {
                    new dotacp.protocol.SessionConfigSelectOption { Value = "default", Name = "Manual", Description = "Always ask before making changes" },
                    new dotacp.protocol.SessionConfigSelectOption { Value = "acceptEdits", Name = "Accept edits", Description = null },
                    new dotacp.protocol.SessionConfigSelectOption { Value = "plan", Name = "Plan", Description = "Create a plan before making changes" },
                },
            },
        ];
    }
}
