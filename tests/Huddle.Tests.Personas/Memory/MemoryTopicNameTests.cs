using Agency.Huddle.App.Memory;

namespace Agency.Huddle.Tests.Memory;

/// <summary>Pins <see cref="MemoryFileRules.IsValidFileName"/> on which names a Memory topic may take: the folder names a Team reserves must stay free for topics.</summary>
public sealed class MemoryTopicNameTests
{
    /// <summary>
    /// A topic named after a folder a Team reserves for itself (<c>raw</c>, <c>outputs</c>) is still a valid Memory topic:
    /// the reservation covers project folder names, not the file names inside a memory folder.
    /// </summary>
    /// <param name="file">The topic file name as an Agent might write it.</param>
    [Theory]
    [InlineData("raw.md")]
    [InlineData("outputs.md")]
    [InlineData("roof-decision.md")]
    public void IsValidFileName_TopicName_IsAccepted(string file)
    {
        Assert.True(MemoryFileRules.IsValidFileName(file));
    }

    /// <summary>
    /// The index, the instructions file, the agent-guide file and an underscore-prefixed draft are never topics:
    /// the index and instructions names are reserved, <c>AGENTS.md</c> is refused as a guide name, and a leading underscore marks a draft.
    /// </summary>
    /// <param name="file">The file name as an Agent might write it.</param>
    [Theory]
    [InlineData("memory.md")]
    [InlineData("Instructions.md")]
    [InlineData("AGENTS.md")]
    [InlineData("_draft.md")]
    public void IsValidFileName_ReservedOrUnsafeName_IsRefused(string file)
    {
        Assert.False(MemoryFileRules.IsValidFileName(file));
    }
}
