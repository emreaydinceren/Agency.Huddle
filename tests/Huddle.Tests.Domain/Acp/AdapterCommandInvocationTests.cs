using Agency.Huddle.App.Acp;

namespace Agency.Huddle.Tests.Acp;

/// <summary>
/// Pins <see cref="AdapterCommandInvocation"/>: the pure decision, from a Message's text alone, of
/// whether it is an Adapter command addressed to one Teammate. It resolves the Mention against the
/// Teammate's own handles and never by pattern, because a Name may contain spaces.
/// </summary>
public sealed class AdapterCommandInvocationTests
{
    /// <summary>Text that is a command for <c>Nova</c>, with the name and arguments it should yield.</summary>
    public static TheoryData<string, string, string> Commands => new()
    {
        { "@Nova /compact", "compact", "" },
        { "@nova /compact", "compact", "" },
        { "@Nova   /compact", "compact", "" },
        { "   @Nova /compact", "compact", "" },
        { "\n@Nova /compact", "compact", "" },
        { "@Nova /compact\n", "compact", "" },
        { "@Nova /compact keep the decisions", "compact", "keep the decisions" },
        { "@Nova /compact   padded  ", "compact", "padded" },
        { "@Nova /compact keep this\nand this", "compact", "keep this\nand this" },
        { "@Nova /COMPACT", "COMPACT", "" },
        { "@Nova /code-review:code-review now", "code-review:code-review", "now" },
        { "@Nova /Create-PR", "Create-PR", "" },
        { "@jar /compact", "compact", "" },
        { "@Nova /compact /init", "compact", "/init" },
    };

    /// <summary>A Name that contains a space still resolves: the handle, not a pattern, decides where the Mention ends.</summary>
    public static TheoryData<string, string, string> MultiWordCommands => new()
    {
        { "@Emily Lee /compact", "compact", "" },
        { "@emily lee /compact focus", "compact", "focus" },
    };

    /// <summary>Text that is not a command for <c>Nova</c> (Alias <c>jar</c>).</summary>
    public static TheoryData<string> NotCommands => new()
    {
        { "" },
        { "   " },
        { "/compact" },
        { "compact" },
        { "@Nova" },
        { "@Nova " },
        { "@Nova /" },
        { "@Nova /  compact" },
        { "@Nova/compact" },
        { "@Nova compact" },
        { "@Nova please /compact" },
        { "hello @Nova /compact" },
        { "@Nova @Luna /compact" },
        { "@Luna @Nova /compact" },
        { "@Luna /compact" },
        { "@Novak /compact" },
        { "@Nova- /compact" },
        { "@Nova_ /compact" },
        { "@Nova2 /compact" },
        { "Nova /compact" },
        { "@@Nova /compact" },
        { "@Nova. /compact" },
        { "@Nova: /compact" },
    };

    /// <summary>A Message that opens with this Teammate's Mention, then a slash name, is a command, with the name as typed and the arguments trimmed.</summary>
    /// <param name="text">The Message text.</param>
    /// <param name="expectedName">The command name after the slash.</param>
    /// <param name="expectedArguments">Everything after the name, trimmed.</param>
    [Theory]
    [MemberData(nameof(Commands))]
    public void TryParse_ALeadingMentionThenSlash_IsACommand(string text, string expectedName, string expectedArguments)
    {
        bool parsed = AdapterCommandInvocation.TryParse(text, ["Nova", "jar"], out AdapterCommandCall? call);

        Assert.True(parsed);
        Assert.Equal(new AdapterCommandCall(expectedName, expectedArguments), call);
    }

    /// <summary>The Mention is resolved against the Teammate's own handle, so a Name with a space is one Mention.</summary>
    /// <param name="text">The Message text.</param>
    /// <param name="expectedName">The command name after the slash.</param>
    /// <param name="expectedArguments">Everything after the name, trimmed.</param>
    [Theory]
    [MemberData(nameof(MultiWordCommands))]
    public void TryParse_AMultiWordName_IsACommand(string text, string expectedName, string expectedArguments)
    {
        bool parsed = AdapterCommandInvocation.TryParse(text, ["Emily Lee"], out AdapterCommandCall? call);

        Assert.True(parsed);
        Assert.Equal(new AdapterCommandCall(expectedName, expectedArguments), call);
    }

    /// <summary>Anything but the exact leading shape is an ordinary Message.</summary>
    /// <param name="text">The Message text.</param>
    [Theory]
    [MemberData(nameof(NotCommands))]
    public void TryParse_AnyOtherShape_IsNotACommand(string text)
    {
        bool parsed = AdapterCommandInvocation.TryParse(text, ["Nova", "jar"], out AdapterCommandCall? call);

        Assert.False(parsed);
        Assert.Null(call);
    }

    /// <summary>With a longer handle listed first or last, the longest one that fits wins, so <c>Emily</c> never swallows <c>Emily Lee</c>.</summary>
    [Fact]
    public void TryParse_TwoHandlesWhereOneIsAPrefix_TheLongestFittingWins()
    {
        bool parsed = AdapterCommandInvocation.TryParse("@Emily Lee /compact", ["Emily", "Emily Lee"], out AdapterCommandCall? call);

        Assert.True(parsed);
        Assert.Equal(new AdapterCommandCall("compact", ""), call);
    }

    /// <summary>When only the shorter handle is the Teammate's, the surname is not a command name: <c>@Emily Lee /compact</c> is not for her.</summary>
    [Fact]
    public void TryParse_TheShorterHandleOnly_DoesNotTreatTheSurnameAsACommand()
    {
        bool parsed = AdapterCommandInvocation.TryParse("@Emily Lee /compact", ["Emily"], out AdapterCommandCall? call);

        Assert.False(parsed);
        Assert.Null(call);
    }

    /// <summary>A Teammate with no handles is never addressed.</summary>
    [Fact]
    public void TryParse_NoHandles_IsNotACommand()
    {
        bool parsed = AdapterCommandInvocation.TryParse("@Nova /compact", [], out AdapterCommandCall? call);

        Assert.False(parsed);
        Assert.Null(call);
    }

    /// <summary>A blank handle never matches, so it cannot make a bare <c>@ /compact</c> a command.</summary>
    [Fact]
    public void TryParse_ABlankHandle_IsIgnored()
    {
        bool parsed = AdapterCommandInvocation.TryParse("@ /compact", ["", "Nova"], out AdapterCommandCall? call);

        Assert.False(parsed);
        Assert.Null(call);
    }
}
