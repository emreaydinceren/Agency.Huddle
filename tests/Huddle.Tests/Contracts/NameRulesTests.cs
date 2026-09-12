using Agency.Huddle.Contracts;

namespace Agency.Huddle.Tests.Contracts;

public sealed class NameRulesTests
{
    public static TheoryData<string?, bool> AgentNames()
    {
        return new TheoryData<string?, bool>
        {
            { "echo", true },
            { "my-agent_2", true },
            { "A1", true },

            // A Name is a display name, so it may hold single interior spaces.
            { "Emily Lee", true },
            { "Chief of Staff", true },
            { "a b", true },

            // ... but not leading, trailing or doubled ones. A Name is also a filename
            // ({name}.md), and Windows silently strips a trailing space, so "a " and "a"
            // would otherwise be one Persona file answering to two different Names.
            { " a", false },
            { "a ", false },
            { "a  b", false },
            { "\ta", false },
            { "a\tb", false },
            { "a\nb", false },

            // .NET's $ matches before a trailing newline; \z is why this is rejected.
            { "coo\n", false },

            { string.Empty, false },
            { " ", false },
            { "-x", false },
            { "a/b", false },
            { "a\\b", false },
            { "../x", false },
            { "a.b", false },
            { new string('a', 64), true },
            { new string('a', 65), false },

            // The 64-character budget counts spaces: a Name is measured as it is written.
            { "a " + new string('b', 62), true },
            { "a " + new string('b', 63), false },

            { null, false },
        };
    }

    public static TheoryData<string?, bool> Ids()
    {
        return new TheoryData<string?, bool>
        {
            { "reply-1", true },
            { "0192f3a1c0f27b3e9d3e2c1a5b6d7e8f", true },
            { "../x", false },
            { "a b", false },
            { string.Empty, false },
            { new string('a', 65), false },
        };
    }

    [Theory]
    [MemberData(nameof(AgentNames))]
    public void IsValidAgentName_AcceptsAndRejects(string? name, bool expected)
    {
        Assert.Equal(expected, NameRules.IsValidAgentName(name));
    }

    [Theory]
    [MemberData(nameof(Ids))]
    public void IsValidId_AcceptsAndRejects(string? id, bool expected)
    {
        Assert.Equal(expected, NameRules.IsValidId(id));
    }
}