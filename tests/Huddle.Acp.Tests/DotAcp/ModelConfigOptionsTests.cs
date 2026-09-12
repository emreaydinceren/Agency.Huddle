namespace Agency.Huddle.Acp.Tests.DotAcp;

using System.Collections.Generic;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.Acp.DotAcp;
using Xunit;

public sealed class ModelConfigOptionsTests
{
    [Fact]
    public void Read_FlatModelOption_ReturnsOptionsInWireOrder()
    {
        dotacp.protocol.SessionConfigOption[] configOptions = ModelConfigOptionsTests.CreateFlatConfigOptions();

        IReadOnlyList<AgentModelOption> models = ModelConfigOptions.Read(configOptions);

        Assert.Equal(
            new[]
            {
                new AgentModelOption("sonnet", "Sonnet", "Balanced model"),
                new AgentModelOption("opus", "Opus", null),
            },
            models);
    }

    [Fact]
    public void Read_GroupedModelOption_FlattensGroupsInWireOrder()
    {
        dotacp.protocol.SessionConfigSelectGroup[] groups =
        [
            new dotacp.protocol.SessionConfigSelectGroup
            {
                Group = "recommended",
                Name = "Recommended",
                Options =
                [
                    new dotacp.protocol.SessionConfigSelectOption { Value = "sonnet", Name = "Sonnet", Description = null },
                ],
            },
            new dotacp.protocol.SessionConfigSelectGroup
            {
                Group = "legacy",
                Name = "Legacy",
                Options =
                [
                    new dotacp.protocol.SessionConfigSelectOption { Value = "opus-3", Name = "Opus 3", Description = null },
                ],
            },
        ];
        dotacp.protocol.SessionConfigOption[] configOptions =
        [
            new dotacp.protocol.SessionConfigSelect
            {
                Id = "model",
                Name = "Model",
                Category = dotacp.protocol.SessionConfigOptionCategory.Model,
                CurrentValue = "sonnet",
                Options = groups,
            },
        ];

        IReadOnlyList<AgentModelOption> models = ModelConfigOptions.Read(configOptions);

        Assert.Equal(
            new[]
            {
                new AgentModelOption("sonnet", "Sonnet", null),
                new AgentModelOption("opus-3", "Opus 3", null),
            },
            models);
    }

    [Fact]
    public void Read_NoModelCategoryOption_ReturnsEmpty()
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

        IReadOnlyList<AgentModelOption> models = ModelConfigOptions.Read(configOptions);

        Assert.Empty(models);
    }

    [Fact]
    public void Read_NullConfigOptions_ReturnsEmpty()
    {
        IReadOnlyList<AgentModelOption> models = ModelConfigOptions.Read(null);

        Assert.Empty(models);
    }

    [Fact]
    public void TryResolve_MatchingValue_ReturnsOwningConfigIdAndValue()
    {
        dotacp.protocol.SessionConfigOption[] configOptions = ModelConfigOptionsTests.CreateFlatConfigOptions();

        bool resolved = ModelConfigOptions.TryResolve(
            configOptions, "opus", out dotacp.protocol.SessionConfigId configId, out dotacp.protocol.SessionConfigValueId value);

        Assert.True(resolved);
        Assert.Equal("model", (string)configId);
        Assert.Equal("opus", (string)value);
    }

    [Fact]
    public void TryResolve_ValueNotInCatalog_ReturnsFalse()
    {
        dotacp.protocol.SessionConfigOption[] configOptions = ModelConfigOptionsTests.CreateFlatConfigOptions();

        bool resolved = ModelConfigOptions.TryResolve(
            configOptions, "gpt-nonexistent", out _, out _);

        Assert.False(resolved);
    }

    [Fact]
    public void TryResolve_NoModelOption_ReturnsFalse()
    {
        bool resolved = ModelConfigOptions.TryResolve(null, "sonnet", out _, out _);

        Assert.False(resolved);
    }

    private static dotacp.protocol.SessionConfigOption[] CreateFlatConfigOptions()
    {
        return
        [
            new dotacp.protocol.SessionConfigSelect
            {
                Id = "model",
                Name = "Model",
                Description = "AI model to use",
                Category = dotacp.protocol.SessionConfigOptionCategory.Model,
                CurrentValue = "sonnet",
                Options = new dotacp.protocol.SessionConfigSelectOption[]
                {
                    new dotacp.protocol.SessionConfigSelectOption { Value = "sonnet", Name = "Sonnet", Description = "Balanced model" },
                    new dotacp.protocol.SessionConfigSelectOption { Value = "opus", Name = "Opus", Description = null },
                },
            },
        ];
    }
}
