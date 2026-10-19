namespace Agency.Huddle.App.Tasks;

/// <summary>The kind of a Request: what the requesting Team asks of the receiving Team (frontmatter <c>request</c>).</summary>
internal enum RequestKind
{
    /// <summary>A defect report (<c>request: bug</c>).</summary>
    Bug,

    /// <summary>A feature request (<c>request: feature</c>).</summary>
    Feature,

    /// <summary>General feedback (<c>request: feedback</c>).</summary>
    Feedback,

    /// <summary>A question for the receiving Team (<c>request: question</c>).</summary>
    Question,
}
