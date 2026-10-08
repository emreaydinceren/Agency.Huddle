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

/// <summary>The Request fields an ordinary Task file carries (RQ-1, Spec §RequestFile). A Task is a Request when it has a <c>request</c> key.</summary>
/// <param name="Kind">The kind of Request.</param>
/// <param name="FromTeam">The Team that filed the Request.</param>
/// <param name="Requester">The name of the Persona who filed it.</param>
/// <param name="Triage">The triage state.</param>
/// <param name="DeclineReason">Why the Request was declined; empty otherwise.</param>
/// <param name="ConfirmedBy">The requester-side QA who confirmed the Request is done; required to close it.</param>
internal sealed record RequestFields(RequestKind Kind, string FromTeam, string Requester, TriageState Triage, string DeclineReason, string ConfirmedBy)
{
    /// <summary>The frontmatter key whose presence marks a Task as a Request; its value is the kind.</summary>
    private const string RequestKey = "request";

    /// <summary>The frontmatter key naming the Team that filed the Request.</summary>
    private const string FromTeamKey = "from_team";

    /// <summary>The frontmatter key naming the Persona who filed the Request.</summary>
    private const string RequesterKey = "requester";

    /// <summary>The frontmatter key holding the triage state.</summary>
    private const string TriageKey = "triage";

    /// <summary>The frontmatter key holding the decline reason.</summary>
    private const string DeclineReasonKey = "decline_reason";

    /// <summary>The frontmatter key holding the requester-side QA who confirmed the Request.</summary>
    private const string ConfirmedByKey = "confirmed_by";

    /// <summary>Frontmatter wire values for each <see cref="RequestKind"/>.</summary>
    private static readonly Dictionary<string, RequestKind> KindsByWire = new(StringComparer.Ordinal)
    {
        ["bug"] = RequestKind.Bug,
        ["feature"] = RequestKind.Feature,
        ["feedback"] = RequestKind.Feedback,
        ["question"] = RequestKind.Question,
    };

    /// <summary>Frontmatter wire values for each <see cref="TriageState"/>; the empty string is <see cref="TriageState.None"/>.</summary>
    private static readonly Dictionary<string, TriageState> TriageByWire = new(StringComparer.Ordinal)
    {
        [string.Empty] = TriageState.None,
        ["needs-info"] = TriageState.NeedsInfo,
        ["accepted"] = TriageState.Accepted,
        ["declined"] = TriageState.Declined,
    };

    /// <summary>Reads the Request fields from a Task's frontmatter.</summary>
    /// <param name="frontmatter">The Task's frontmatter keys and values.</param>
    /// <param name="fields">The Request fields when the result is true; otherwise <see langword="null"/>.</param>
    /// <returns>False when <c>request</c> is absent, when its kind is not recognised, or when <c>triage</c> holds an unrecognised value.</returns>
    public static bool TryRead(IReadOnlyDictionary<string, string> frontmatter, out RequestFields? fields)
    {
        ArgumentNullException.ThrowIfNull(frontmatter);
        fields = null;

        if (!frontmatter.TryGetValue(RequestKey, out string? kindRaw))
        {
            return false;
        }

        if (!KindsByWire.TryGetValue(kindRaw.Trim(), out RequestKind kind))
        {
            return false;
        }

        if (!TriageByWire.TryGetValue(ValueOrEmpty(frontmatter, TriageKey), out TriageState triage))
        {
            return false;
        }

        fields = new RequestFields(
            kind,
            ValueOrEmpty(frontmatter, FromTeamKey),
            ValueOrEmpty(frontmatter, RequesterKey),
            triage,
            ValueOrEmpty(frontmatter, DeclineReasonKey),
            ValueOrEmpty(frontmatter, ConfirmedByKey));
        return true;
    }

    /// <summary>Writes the Request fields as the six frontmatter keys, with their wire values.</summary>
    /// <returns>A new dictionary keyed by frontmatter key.</returns>
    public IReadOnlyDictionary<string, string> ToFrontmatter() => new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [RequestKey] = KindWire(this.Kind),
        [FromTeamKey] = this.FromTeam,
        [RequesterKey] = this.Requester,
        [TriageKey] = TriageWire(this.Triage),
        [DeclineReasonKey] = this.DeclineReason,
        [ConfirmedByKey] = this.ConfirmedBy,
    };

    /// <summary>The Markdown section headings a Request of <paramref name="kind"/> must carry in its text (RQ-1).</summary>
    /// <param name="kind">The kind of Request.</param>
    /// <returns>The required headings, in the order the template gives them.</returns>
    public static IReadOnlyList<string> RequiredSections(RequestKind kind) => kind switch
    {
        RequestKind.Bug => ["Steps to reproduce", "Expected and actual"],
        RequestKind.Feature => ["Problem", "Desired outcome"],
        RequestKind.Feedback => ["What you saw"],
        RequestKind.Question => ["Question"],
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>Reads one frontmatter value, trimmed, or the empty string when the key is absent.</summary>
    /// <param name="frontmatter">The Task's frontmatter.</param>
    /// <param name="key">The key to read.</param>
    /// <returns>The trimmed value, or an empty string.</returns>
    private static string ValueOrEmpty(IReadOnlyDictionary<string, string> frontmatter, string key) =>
        frontmatter.TryGetValue(key, out string? value) ? value.Trim() : string.Empty;

    /// <summary>The frontmatter wire value of a kind.</summary>
    /// <param name="kind">The kind to write.</param>
    /// <returns>The lowercase wire value.</returns>
    private static string KindWire(RequestKind kind) => kind switch
    {
        RequestKind.Bug => "bug",
        RequestKind.Feature => "feature",
        RequestKind.Feedback => "feedback",
        RequestKind.Question => "question",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>The frontmatter wire value of a triage state.</summary>
    /// <param name="triage">The triage state to write.</param>
    /// <returns>The wire value; empty for <see cref="TriageState.None"/>.</returns>
    private static string TriageWire(TriageState triage) => triage switch
    {
        TriageState.None => string.Empty,
        TriageState.NeedsInfo => "needs-info",
        TriageState.Accepted => "accepted",
        TriageState.Declined => "declined",
        _ => throw new ArgumentOutOfRangeException(nameof(triage)),
    };
}
