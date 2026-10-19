namespace Agency.Huddle.Tests.Acp;

using Agency.Huddle.App.Acp;
using Agency.Huddle.Tests.Acp.Fakes;

/// <summary>The system prompt paragraph that tells an Agent to log document changes (DocHistory spec section 10): sent only when the tool is offered, with its prefixed name substituted.</summary>
public sealed class DocHistoryPromptTests
{
    /// <summary>When <c>log_change</c> is among the offered tools, the paragraph names it exactly as offered.</summary>
    [Fact]
    public void Compose_LogChangeOffered_AddsTheParagraphWithThePrefixedName()
    {
        string prompt = Compose(["mcp__team__get_help", "mcp__team__log_change"]);

        Assert.Contains("call mcp__team__log_change once for it", prompt, StringComparison.Ordinal);
    }

    /// <summary>When the tool is not offered there is no paragraph, so a session without it reads exactly what it read before.</summary>
    [Fact]
    public void Compose_LogChangeNotOffered_AddsNothing()
    {
        string prompt = Compose(["mcp__team__get_help"]);

        Assert.DoesNotContain("log_change", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("Markdown (.md) document", prompt, StringComparison.Ordinal);
    }

    private static string Compose(IReadOnlyList<string> toolNames) =>
        SystemPromptComposer.Compose(new Persona("Nova", "You are Nova."), new FakePromptSource(), "mcp__team__get_help", toolNames, [], string.Empty, null, SessionScope.Shared);
}
