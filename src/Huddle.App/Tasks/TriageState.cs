namespace Agency.Huddle.App.Tasks;

/// <summary>Where a Request stands in triage (frontmatter <c>triage</c>). <see cref="None"/> is the empty value.</summary>
internal enum TriageState
{
    /// <summary>Not yet triaged (<c>triage: ''</c>).</summary>
    None,

    /// <summary>The receiving manager asked the requester for more information (<c>triage: needs-info</c>).</summary>
    NeedsInfo,

    /// <summary>The receiving manager accepted the Request (<c>triage: accepted</c>).</summary>
    Accepted,

    /// <summary>The receiving manager declined the Request (<c>triage: declined</c>).</summary>
    Declined,
}
