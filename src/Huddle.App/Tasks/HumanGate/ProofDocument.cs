using Agency.Huddle.App.DocHistory;

namespace Agency.Huddle.App.Tasks.HumanGate;

/// <summary>An attached Markdown document and its history.</summary>
/// <param name="File">The file name, shown in the item's detail.</param>
/// <param name="History">The history entries in file order, oldest first.</param>
internal sealed record ProofDocument(string File, IReadOnlyList<DocHistoryEntry> History);
