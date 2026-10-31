using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>
/// Tests for <see cref="RequestFields"/>: the Request keys an ordinary Task file carries (RQ-1, Spec §RequestFile, Task 66).
/// </summary>
public sealed class RequestFieldsTests
{
    /// <summary>Builds the frontmatter of the Spec §RequestFile example, as the Task parser hands it over.</summary>
    /// <returns>The six Request keys with the example's values.</returns>
    private static Dictionary<string, string> SpecExampleFrontmatter() => new(StringComparer.Ordinal)
    {
        ["request"] = "bug",
        ["from_team"] = "Web Team",
        ["requester"] = "Joe",
        ["triage"] = "needs-info",
        ["decline_reason"] = "",
        ["confirmed_by"] = "",
    };

    /// <summary>The Spec §RequestFile example reads into every Request field.</summary>
    [Fact]
    public void TryRead_SpecExample_ReadsEveryField()
    {
        bool result = RequestFields.TryRead(SpecExampleFrontmatter(), out var fields);

        Assert.True(result);
        RequestFields read = Assert.IsType<RequestFields>(fields);
        Assert.Equal(new RequestFields(RequestKind.Bug, "Web Team", "Joe", TriageState.NeedsInfo, "", ""), read);
    }

    /// <summary>An empty <c>triage</c> value reads as <see cref="TriageState.None"/>.</summary>
    [Fact]
    public void TryRead_EmptyTriage_ReadsAsNone()
    {
        Dictionary<string, string> frontmatter = SpecExampleFrontmatter();
        frontmatter["triage"] = "";

        bool result = RequestFields.TryRead(frontmatter, out var fields);

        Assert.True(result);
        Assert.Equal(TriageState.None, Assert.IsType<RequestFields>(fields).Triage);
    }

    /// <summary>A Task without a <c>request</c> key is an ordinary Task, so reading returns false and no fields.</summary>
    [Fact]
    public void TryRead_NoRequestKey_ReturnsFalse()
    {
        Dictionary<string, string> frontmatter = SpecExampleFrontmatter();
        _ = frontmatter.Remove("request");

        bool result = RequestFields.TryRead(frontmatter, out var fields);

        Assert.False(result);
        Assert.Null(fields);
    }

    /// <summary>A <c>request</c> value outside bug, feature, feedback and question reads as false.</summary>
    [Fact]
    public void TryRead_UnknownRequestKind_ReturnsFalse()
    {
        Dictionary<string, string> frontmatter = SpecExampleFrontmatter();
        frontmatter["request"] = "epic";

        bool result = RequestFields.TryRead(frontmatter, out var fields);

        Assert.False(result);
        Assert.Null(fields);
    }

    /// <summary>A <c>triage</c> value outside the four triage states reads as false.</summary>
    [Fact]
    public void TryRead_UnknownTriage_ReturnsFalse()
    {
        Dictionary<string, string> frontmatter = SpecExampleFrontmatter();
        frontmatter["triage"] = "maybe";

        bool result = RequestFields.TryRead(frontmatter, out var fields);

        Assert.False(result);
        Assert.Null(fields);
    }

    /// <summary>Writing the fields to frontmatter and reading them back gives the same record.</summary>
    [Fact]
    public void ToFrontmatter_RoundTrip_ReadsBackEqual()
    {
        RequestFields.TryRead(SpecExampleFrontmatter(), out var first);
        RequestFields original = Assert.IsType<RequestFields>(first);

        bool result = RequestFields.TryRead(original.ToFrontmatter(), out var second);

        Assert.True(result);
        Assert.Equal(original, second);
    }

    /// <summary>A declined Request writes six keys under their frontmatter names, with the triage and reason as wire values.</summary>
    [Fact]
    public void ToFrontmatter_Declined_WritesSixWireKeys()
    {
        RequestFields declined = new(RequestKind.Feedback, "Web Team", "Joe", TriageState.Declined, "Out of scope", "");

        IReadOnlyDictionary<string, string> written = declined.ToFrontmatter();

        Assert.Equal(6, written.Count);
        Assert.Equal("feedback", written["request"]);
        Assert.Equal("Web Team", written["from_team"]);
        Assert.Equal("Joe", written["requester"]);
        Assert.Equal("declined", written["triage"]);
        Assert.Equal("Out of scope", written["decline_reason"]);
        Assert.Equal("", written["confirmed_by"]);
    }

    /// <summary>A Bug Request requires the sections "Steps to reproduce" and "Expected and actual", in that order.</summary>
    [Fact]
    public void RequiredSections_Bug_ListsReproductionAndOutcome()
    {
        string[] expected = ["Steps to reproduce", "Expected and actual"];

        Assert.Equal(expected, RequestFields.RequiredSections(RequestKind.Bug));
    }

    /// <summary>A Feature Request requires the sections "Problem" and "Desired outcome", in that order.</summary>
    [Fact]
    public void RequiredSections_Feature_ListsProblemAndOutcome()
    {
        string[] expected = ["Problem", "Desired outcome"];

        Assert.Equal(expected, RequestFields.RequiredSections(RequestKind.Feature));
    }

    /// <summary>A Feedback Request requires the section "What you saw".</summary>
    [Fact]
    public void RequiredSections_Feedback_ListsWhatYouSaw()
    {
        string[] expected = ["What you saw"];

        Assert.Equal(expected, RequestFields.RequiredSections(RequestKind.Feedback));
    }

    /// <summary>A Question Request requires the section "Question".</summary>
    [Fact]
    public void RequiredSections_Question_ListsQuestion()
    {
        string[] expected = ["Question"];

        Assert.Equal(expected, RequestFields.RequiredSections(RequestKind.Question));
    }
}
