namespace Agency.Huddle.Acp.Tests.Abstractions;

using System;
using Agency.Huddle.Acp.Abstractions;
using Xunit;

public sealed class SystemPromptOptionsTests
{
    [Fact]
    public void Ctor_NullText_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new SystemPromptOptions(null!));
    }

    [Fact]
    public void Ctor_WhitespaceText_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new SystemPromptOptions("   "));
    }

    [Fact]
    public void Ctor_ValidTextNoMode_DefaultsToAppend()
    {
        SystemPromptOptions options = new SystemPromptOptions("You are the COO");

        Assert.Equal(SystemPromptMode.Append, options.Mode);
    }

    [Fact]
    public void Ctor_ValidTextAndReplace_RoundTripsTextAndMode()
    {
        SystemPromptOptions options = new SystemPromptOptions("You are the COO", SystemPromptMode.Replace);

        Assert.Equal("You are the COO", options.Text);
        Assert.Equal(SystemPromptMode.Replace, options.Mode);
    }

    [Fact]
    public void Ctor_UndefinedMode_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SystemPromptOptions("You are the COO", (SystemPromptMode)42));
    }
}
