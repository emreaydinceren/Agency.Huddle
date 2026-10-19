using Agency.Huddle.Acp.Abstractions;

namespace Agency.Huddle.App.Library;

/// <summary>The planner's decision for one document.</summary>
/// <param name="Delivery">How the document reaches the model.</param>
/// <param name="Block">The Prompt block when <paramref name="Delivery"/> is <see cref="LibraryDocumentDelivery.Block"/>; otherwise <see langword="null"/>.</param>
/// <param name="Withheld">Why an eligible image was not sent, or <see cref="PromptBlockWithheld.None"/>.</param>
internal sealed record PlannedDocument(LibraryDocumentDelivery Delivery, AgentPromptBlock? Block = null, PromptBlockWithheld Withheld = PromptBlockWithheld.None);
