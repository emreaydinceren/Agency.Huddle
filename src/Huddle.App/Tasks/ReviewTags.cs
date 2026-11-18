namespace Agency.Huddle.App.Tasks;

/// <summary>The tags that make a Task a review or a QA check (#669): the assignee of such a Task is asked to record an outcome on it.</summary>
internal static class ReviewTags
{
    /// <summary>The tag of a Task that reviews another Task's work.</summary>
    public const string Review = "review";

    /// <summary>The tag of a Task that verifies another Task's work.</summary>
    public const string Verify = "verify";

    /// <summary>Whether the tags include <see cref="Review"/> or <see cref="Verify"/>, compared ignoring case.</summary>
    /// <param name="tags">A Task's tags.</param>
    /// <returns><see langword="true"/> when the Task is a review or verify Task.</returns>
    public static bool IsReviewTask(IEnumerable<string> tags) =>
        tags.Contains(Review, StringComparer.OrdinalIgnoreCase) || tags.Contains(Verify, StringComparer.OrdinalIgnoreCase);
}
