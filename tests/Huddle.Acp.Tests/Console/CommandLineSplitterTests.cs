namespace Agency.Huddle.Acp.Tests.Console;

using Agency.Huddle.Console.Configuration;
using Xunit;

public sealed class CommandLineSplitterTests
{
    [Fact]
    public void Split_Whitespace()
    {
        string[] result = CommandLineSplitter.Split("a b  c");

        Assert.Equal(new[] { "a", "b", "c" }, result);
    }

    [Fact]
    public void Split_DoubleQuotedArgWithSpaces()
    {
        string[] result = CommandLineSplitter.Split("node \"C:\\my dir\\x.js\" --flag");

        Assert.Equal(3, result.Length);
        Assert.Equal("node", result[0]);
        Assert.Equal("C:\\my dir\\x.js", result[1]);
        Assert.Equal("--flag", result[2]);
    }

    [Fact]
    public void Split_Empty_ReturnsEmpty()
    {
        string[] fromNull = CommandLineSplitter.Split(null);
        string[] fromEmpty = CommandLineSplitter.Split(string.Empty);

        Assert.Empty(fromNull);
        Assert.Empty(fromEmpty);
    }
}
