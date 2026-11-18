namespace Agency.Huddle.Tests.Acp;

using Agency.Huddle.App.Acp;

/// <summary>Pins how the shipped Huddle rules fill their tool placeholders (Team Playbooks spec §4, §4.1, §4.2).</summary>
public sealed class HuddleRulesRenderTests
{
    private const string Unavailable = "(not available here; tell the Human)";

    private static readonly string[] PrefixedTools =
    [
        "mcp__team__add_comment",
        "mcp__team__add_evidence",
        "mcp__team__log_change",
        "mcp__team__create_task",
        "mcp__team__update_task",
        "mcp__team__close_task",
        "mcp__team__triage_request",
        "mcp__team__memory",
    ];

    /// <summary>Each Member placeholder is filled with the Adapter's prefixed tool name, in the sentence that names it.</summary>
    [Fact]
    public void RenderMember_Prefixed_FillsEveryPlaceholder()
    {
        string text = HuddleRules.RenderMember(PrefixedTools);

        Assert.Contains("A note goes on the Task with mcp__team__add_comment.", text, StringComparison.Ordinal); // contains-ok: the placeholder filled inside its sentence; the goldens pin the whole rendered text
        Assert.Contains("is Evidence, posted\n  with mcp__team__add_evidence;", text, StringComparison.Ordinal); // contains-ok: the placeholder filled inside its sentence; the goldens pin the whole rendered text
        Assert.Contains("logs\n  the review on it with mcp__team__log_change and the kind `reviewed`", text, StringComparison.Ordinal); // contains-ok: the placeholder filled inside its sentence; the goldens pin the whole rendered text
        Assert.Contains("create it with mcp__team__create_task as a child", text, StringComparison.Ordinal); // contains-ok: the placeholder filled inside its sentence; the goldens pin the whole rendered text
        Assert.Contains("`blocked_by` with mcp__team__update_task.", text, StringComparison.Ordinal); // contains-ok: the placeholder filled inside its sentence; the goldens pin the whole rendered text
    }

    /// <summary>With the bare names an Adapter without a prefix offers, the same sentences carry the bare names.</summary>
    [Fact]
    public void RenderMember_Unprefixed_FillsWithBareNames()
    {
        string[] bare = [.. PrefixedTools.Select(static name => name["mcp__team__".Length..])];

        string text = HuddleRules.RenderMember(bare);

        Assert.Contains("A note goes on the Task with add_comment.", text, StringComparison.Ordinal); // contains-ok: the placeholder filled inside its sentence; the goldens pin the whole rendered text
        Assert.Contains("create it with create_task as a child", text, StringComparison.Ordinal); // contains-ok: the placeholder filled inside its sentence; the goldens pin the whole rendered text
        Assert.DoesNotContain("mcp__team__", text, StringComparison.Ordinal);
        Assert.DoesNotContain("{{", text, StringComparison.Ordinal);
    }

    /// <summary>Each Manager placeholder is filled with the prefixed tool name.</summary>
    [Fact]
    public void RenderManager_Prefixed_FillsEveryPlaceholder()
    {
        string text = HuddleRules.RenderManager(PrefixedTools);

        Assert.Contains("Triage one in your Backlog with mcp__team__triage_request:", text, StringComparison.Ordinal); // contains-ok: the placeholder filled inside its sentence; the goldens pin the whole rendered text
        Assert.Contains("Closing a Task with mcp__team__close_task is not marking it Done", text, StringComparison.Ordinal); // contains-ok: the placeholder filled inside its sentence; the goldens pin the whole rendered text
        Assert.Contains("A child Task made with mcp__team__create_task, `parent` set", text, StringComparison.Ordinal); // contains-ok: the placeholder filled inside its sentence; the goldens pin the whole rendered text
        Assert.Contains("a Project's included, with mcp__team__memory.", text, StringComparison.Ordinal); // contains-ok: the placeholder filled inside its sentence; the goldens pin the whole rendered text
        Assert.DoesNotContain("{{", text, StringComparison.Ordinal);
    }

    /// <summary>A tool the session does not offer renders the unavailable text in its place, once per placeholder, and no <c>{{</c> remains.</summary>
    [Fact]
    public void Render_AbsentTools_RenderTheUnavailableText()
    {
        string member = HuddleRules.RenderMember([]);
        string manager = HuddleRules.RenderManager([]);

        Assert.Equal(5, Count(member, Unavailable));
        Assert.Equal(4, Count(manager, Unavailable));
        Assert.Contains("A note goes on the Task with " + Unavailable + ".", member, StringComparison.Ordinal); // contains-ok: the placeholder filled inside its sentence; the goldens pin the whole rendered text
        Assert.DoesNotContain("{{", member, StringComparison.Ordinal);
        Assert.DoesNotContain("{{", manager, StringComparison.Ordinal);
    }

    /// <summary>A name that only ends like a tool (<c>my_memory</c>) is not that tool.</summary>
    [Fact]
    public void RenderManager_NameThatOnlyEndsLikeATool_IsNotMatched()
    {
        string text = HuddleRules.RenderManager(["mcp__team__my_memory"]);

        Assert.Contains("with " + Unavailable + ".", text, StringComparison.Ordinal); // contains-ok: the placeholder filled inside its sentence; the goldens pin the whole rendered text
        Assert.DoesNotContain("my_memory", text, StringComparison.Ordinal);
    }

    private static int Count(string text, string needle)
    {
        int count = 0;
        int at = 0;
        while ((at = text.IndexOf(needle, at, StringComparison.Ordinal)) >= 0)
        {
            count++;
            at += needle.Length;
        }

        return count;
    }
}
