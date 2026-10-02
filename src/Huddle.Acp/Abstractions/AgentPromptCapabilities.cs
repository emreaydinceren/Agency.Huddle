namespace Agency.Huddle.Acp.Abstractions;

/// <summary>
/// What kinds of content an agent said it accepts in a prompt, read from
/// <c>agentCapabilities.promptCapabilities</c> on the <c>initialize</c> response. ACP reads an omitted
/// capability as unsupported, so an agent that says nothing is <see cref="None"/>. Audio is not carried
/// because nothing sends it.
/// </summary>
/// <param name="Image">Whether the agent accepts an <c>image</c> content block.</param>
/// <param name="EmbeddedContext">Whether the agent accepts an embedded <c>resource</c> content block.</param>
public sealed record AgentPromptCapabilities(bool Image, bool EmbeddedContext)
{
    /// <summary>The capabilities of an agent that advertised none: a prompt may carry text only.</summary>
    public static AgentPromptCapabilities None { get; } = new(Image: false, EmbeddedContext: false);
}
