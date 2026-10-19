using Agency.Huddle.Acp.Abstractions;

namespace Agency.Huddle.App.Library;

/// <summary>One Library document mentioned in a Turn (Spec §6.14).</summary>
/// <param name="FullPath">The resolved absolute path on disk.</param>
/// <param name="Location">The §6.14 location label.</param>
/// <param name="Size">The document's size, formatted with <see cref="LibrarySize.Format"/>.</param>
/// <param name="Text">The decoded, possibly-truncated text, or <see langword="null"/> when not inlined.</param>
/// <param name="Truncated">Whether <see cref="Text"/> was cut short of the document's full content.</param>
/// <param name="Length">The document's true size in bytes.</param>
/// <param name="TooLarge">Whether the document is over <see cref="LibraryOptions.MaxEditableBytes"/>.</param>
/// <param name="Delivery">How the document reaches the model; <see cref="LibraryDocumentDelivery.Path"/>, today's line, unless the collector was given a <see cref="PromptDelivery"/>.</param>
/// <param name="Block">The Prompt block that carries the document when <paramref name="Delivery"/> is <see cref="LibraryDocumentDelivery.Block"/>.</param>
/// <param name="Withheld">Why an image that could have been a block was not sent, for the Information log; <see cref="PromptBlockWithheld.None"/> otherwise.</param>
internal sealed record LibraryDocumentItem(
    string FullPath,
    string Location,
    string Size,
    string? Text,
    bool Truncated,
    long Length,
    bool TooLarge,
    LibraryDocumentDelivery Delivery = LibraryDocumentDelivery.Path,
    AgentPromptBlock? Block = null,
    PromptBlockWithheld Withheld = PromptBlockWithheld.None);
