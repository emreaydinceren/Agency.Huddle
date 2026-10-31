namespace Agency.Huddle.App.Library;

/// <summary>The Library documents mentioned across a Turn, capped and deduped (Spec §6.14).</summary>
/// <param name="Items">The kept documents, in first-seen order.</param>
/// <param name="NotListed">How many further, distinct documents were mentioned beyond the cap.</param>
/// <param name="MaxInlineBytes">The <see cref="LibraryOptions.MaxInlineBytes"/> cap in effect when this report was built; Spec §6.14's <c>turn.libraryDocTruncated</c> line names it.</param>
internal sealed record LibraryDocumentsReport(IReadOnlyList<LibraryDocumentItem> Items, int NotListed, int MaxInlineBytes)
{
    /// <summary>Whether nothing was collected at all.</summary>
    internal bool IsEmpty => this.Items.Count == 0;
}
