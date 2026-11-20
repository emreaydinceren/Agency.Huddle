namespace Agency.Huddle.App.Tasks.Wake;

/// <summary>The comment bullet a wake Message lists for a comment (smart wakes spec 5.3): the author and the text, cut at a limit.</summary>
internal static class CommentExcerpt
{
    /// <summary>The most characters of a comment a Message shows; a comment of exactly this length is shown whole.</summary>
    internal const int Limit = 1000;

    /// <summary>What follows a cut comment, inside the closing quote.</summary>
    private const string CutMarker = "… (get_task has the rest)";

    /// <summary>The raw <paramref name="text"/> cut at <see cref="Limit"/> characters, with the marker appended when it was cut.</summary>
    /// <param name="text">The comment as written.</param>
    /// <returns>The text to quote, before Mentions are neutralised.</returns>
    public static string Cut(string text) =>
        text.Length > Limit ? string.Concat(text.AsSpan(0, Limit), CutMarker) : text;

    /// <summary>The bullet line for a comment: <c>- comment by Kai: "text"</c>, the text cut and then neutralised by <paramref name="neutralise"/>.</summary>
    /// <param name="author">The author's Name.</param>
    /// <param name="text">The comment as written.</param>
    /// <param name="neutralise">Makes Mentions in the shown text inert.</param>
    /// <returns>The bullet.</returns>
    public static string Bullet(string author, string text, Func<string, string> neutralise) =>
        $"- comment by {author}: \"{neutralise(Cut(text))}\"";
}
