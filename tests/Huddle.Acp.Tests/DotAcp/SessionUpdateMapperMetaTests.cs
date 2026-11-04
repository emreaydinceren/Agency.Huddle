namespace Agency.Huddle.Acp.Tests.DotAcp;

using System.Collections.Generic;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.Acp.DotAcp;
using Xunit;

/// <summary>
/// Pins how <see cref="SessionUpdateMapper"/> reads <c>_meta["_claude/model"]</c> from a usage update, for the
/// shapes dotacp can hand over: a plain string (the live wire), a <c>JValue</c> string (built by hand), and
/// values that are not strings at all.
/// </summary>
public sealed class SessionUpdateMapperMetaTests
{
    /// <summary>A plain C# string under <c>_claude/model</c>, as dotacp delivers a top-level primitive, maps to <see cref="UsageUpdated.Model"/>.</summary>
    [Fact]
    public void UsageUpdate_MetaModelAsPlainString_MapsToModel()
    {
        dotacp.protocol.UsageUpdate update = new dotacp.protocol.UsageUpdate
        {
            Size = 1000,
            Used = 250,
            Meta = new Dictionary<string, object>
            {
                ["_claude/model"] = "claude-haiku-5-5",
            },
        };

        UsageUpdated result = Assert.IsType<UsageUpdated>(SessionUpdateMapper.Map("s", update));
        Assert.Equal("claude-haiku-5-5", result.Model);
    }

    /// <summary>The same model id carried as a <c>JValue</c> string maps to <see cref="UsageUpdated.Model"/>.</summary>
    [Fact]
    public void UsageUpdate_MetaModelAsJValueString_MapsToModel()
    {
        dotacp.protocol.UsageUpdate update = new dotacp.protocol.UsageUpdate
        {
            Size = 1000,
            Used = 250,
            Meta = new Dictionary<string, object>
            {
                ["_claude/model"] = new Newtonsoft.Json.Linq.JValue("claude-haiku-5-5"),
            },
        };

        UsageUpdated result = Assert.IsType<UsageUpdated>(SessionUpdateMapper.Map("s", update));
        Assert.Equal("claude-haiku-5-5", result.Model);
    }

    /// <summary>A plain integer under <c>_claude/model</c> is not a model id, so <see cref="UsageUpdated.Model"/> is <see langword="null"/>.</summary>
    [Fact]
    public void UsageUpdate_MetaModelAsPlainInt_ModelIsNull()
    {
        dotacp.protocol.UsageUpdate update = new dotacp.protocol.UsageUpdate
        {
            Size = 1000,
            Used = 250,
            Meta = new Dictionary<string, object>
            {
                ["_claude/model"] = 42,
            },
        };

        UsageUpdated result = Assert.IsType<UsageUpdated>(SessionUpdateMapper.Map("s", update));
        Assert.Null(result.Model);
    }

    /// <summary>A JSON object under <c>_claude/model</c> is not a model id, so <see cref="UsageUpdated.Model"/> is <see langword="null"/>.</summary>
    [Fact]
    public void UsageUpdate_MetaModelAsJObject_ModelIsNull()
    {
        dotacp.protocol.UsageUpdate update = new dotacp.protocol.UsageUpdate
        {
            Size = 1000,
            Used = 250,
            Meta = new Dictionary<string, object>
            {
                ["_claude/model"] = new Newtonsoft.Json.Linq.JObject(),
            },
        };

        UsageUpdated result = Assert.IsType<UsageUpdated>(SessionUpdateMapper.Map("s", update));
        Assert.Null(result.Model);
    }
}
