namespace Agency.Huddle.Acp.Abstractions;

/// <summary>
/// One Turn's prompt: the text, and the ordered Prompt blocks that follow it. The text block is
/// always first on the wire, because an Adapter reads a command only from the start of a prompt.
/// A <see langword="null"/> or empty <paramref name="Blocks"/> is a text-only prompt, identical on the
/// wire to the string overload.
/// </summary>
/// <param name="Text">The prompt text. Always sent, always the first block.</param>
/// <param name="Blocks">The non-text blocks, in the order they follow the text.</param>
public sealed record AgentPrompt(string Text, IReadOnlyList<AgentPromptBlock>? Blocks = null);

/// <summary>A non-text part of a prompt. Carries data, never a wire type: the DotAcp layer owns base64 and URIs.</summary>
public abstract record AgentPromptBlock;

/// <summary>An image, sent as an ACP <c>image</c> block with its bytes in <c>data</c>.</summary>
/// <param name="MimeType">The image's MIME type, for example <c>image/png</c>.</param>
/// <param name="Data">The raw bytes, never base64.</param>
public sealed record AgentImageBlock(string MimeType, ReadOnlyMemory<byte> Data) : AgentPromptBlock;

/// <summary>A document's text, sent as an ACP embedded <c>resource</c> block.</summary>
/// <param name="Uri">The resource's URI, for example <c>file:///E:/notes.md</c>.</param>
/// <param name="MimeType">The text's MIME type.</param>
/// <param name="Text">The text itself.</param>
public sealed record AgentTextResourceBlock(string Uri, string MimeType, string Text) : AgentPromptBlock;
