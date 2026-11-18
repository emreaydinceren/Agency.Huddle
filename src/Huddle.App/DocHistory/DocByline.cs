namespace Agency.Huddle.App.DocHistory;

/// <summary>The parts of a document's byline (DocHistory spec §6.6). Each list is distinct Names, most recent first.</summary>
/// <param name="Writer">Who wrote the document: the first <see cref="DocChangeKind.Created"/> entry's actor, else the first entry's.</param>
/// <param name="EditedBy">Actors of <see cref="DocChangeKind.Edited"/> entries other than the writer.</param>
/// <param name="ReviewedBy">Actors of <see cref="DocChangeKind.Reviewed"/> entries.</param>
/// <param name="ObjectedBy">Actors of <see cref="DocChangeKind.Objected"/> entries.</param>
/// <param name="When">The newest entry's time.</param>
/// <param name="GoldfishEditedBy">Actors of <see cref="DocChangeKind.Edited"/> entries written as a goldfish.</param>
/// <param name="GoldfishReviewedBy">Actors of <see cref="DocChangeKind.Reviewed"/> entries written as a goldfish.</param>
public sealed record DocByline(
    string Writer,
    IReadOnlyList<string> EditedBy,
    IReadOnlyList<string> ReviewedBy,
    IReadOnlyList<string> ObjectedBy,
    DateTimeOffset When,
    IReadOnlyList<string> GoldfishEditedBy,
    IReadOnlyList<string> GoldfishReviewedBy);
