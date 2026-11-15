using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.Tests.Acp.Sessions;

/// <summary>
/// <see cref="ToolCallSummary"/>: how a tool call's raw input and output JSON become the short
/// <see cref="ToolActivity.Arguments"/> and <see cref="ToolActivity.Result"/> text that crosses the pipe.
/// </summary>
public sealed class ToolCallSummaryTests
{
    /// <summary>Input that says nothing yet (null, blank, not JSON, or an empty object) has no arguments, so a stored value is left alone.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData(" { } ")]
    public void Arguments_NothingUsable_IsNull(string? rawInputJson)
    {
        Assert.Null(ToolCallSummary.Arguments(rawInputJson));
    }

    /// <summary>An object is re-written without indentation and keeps non-ASCII text and quotes readable.</summary>
    [Fact]
    public void Arguments_Object_IsCompactAndReadable()
    {
        string? arguments = ToolCallSummary.Arguments("{\n  \"text\": \"café \\\"x\\\"\",\n  \"n\": 1\n}");

        Assert.Equal("{\"text\":\"café \\\"x\\\"\",\"n\":1}", arguments);
    }

    /// <summary>An input longer than the limit is clipped to it.</summary>
    [Fact]
    public void Arguments_LongInput_IsClipped()
    {
        string? arguments = ToolCallSummary.Arguments("{\"command\":\"" + new string('x', 2000) + "\"}");

        Assert.Equal(ToolActivityLimits.MaxArgumentsLength, arguments?.Length);
    }

    /// <summary>The text blocks of an MCP content array are joined by a newline and other blocks are skipped.</summary>
    [Fact]
    public void Result_ContentArray_JoinsTextBlocks()
    {
        string? result = ToolCallSummary.Result("""[{"type":"text","text":"one"},{"type":"image","data":"x"},{"type":"text","text":"two"}]""");

        Assert.Equal("one\ntwo", result);
    }

    /// <summary>A JSON string is its value.</summary>
    [Fact]
    public void Result_String_IsItsValue()
    {
        Assert.Equal("Nothing was added.", ToolCallSummary.Result("\"Nothing was added.\""));
    }

    /// <summary>An object, a missing output and text that is not JSON have no result.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("{\"text\":\"x\"}")]
    [InlineData("oops")]
    [InlineData("42")]
    public void Result_NotTextOrArray_IsNull(string? rawOutputJson)
    {
        Assert.Null(ToolCallSummary.Result(rawOutputJson));
    }

    /// <summary>A result longer than the limit is clipped to it.</summary>
    [Fact]
    public void Result_LongText_IsClipped()
    {
        string? result = ToolCallSummary.Result("\"" + new string('y', 2000) + "\"");

        Assert.Equal(ToolActivityLimits.MaxResultLength, result?.Length);
    }
}
