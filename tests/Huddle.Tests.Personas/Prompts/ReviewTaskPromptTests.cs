namespace Agency.Huddle.Tests.Prompts;

using Agency.Huddle.App.Prompts;

/// <summary>
/// Pins #669 (spec V6b): the Task tool prompts name no Review state, and say how to ask for a review, which is a
/// Task tagged review.
/// </summary>
public sealed class ReviewTaskPromptTests
{
    private static readonly string[] ReviewStateKeys = ["tool.updateTask.description", "getHelp.tasks", "tool.createTask.description", "systemPrompt.needsYou"];

    private static readonly string[] ReviewStateWording = ["Review state", "to Review", "in Review", "In Review", "from Review", "Review, then", "then Review"];

    private static string Default(string key) => PromptCatalog.All.Single(p => p.Key == key).Default;

    /// <summary>No Task tool prompt, nor the Needs You system prompt, names a Review state.</summary>
    /// <param name="key">The prompt key.</param>
    [Theory]
    [InlineData("tool.updateTask.description")]
    [InlineData("getHelp.tasks")]
    [InlineData("tool.createTask.description")]
    [InlineData("systemPrompt.needsYou")]
    public void TaskPrompts_NameNoReviewState(string key)
    {
        string text = Default(key);

        Assert.Contains(key, ReviewStateKeys);
        foreach (string wording in ReviewStateWording)
        {
            Assert.DoesNotContain(wording, text, StringComparison.Ordinal);
        }
    }

    /// <summary>create_task and get_help say how to ask for a review: a Task tagged review, assigned to the reviewer, a child, in blocked_by.</summary>
    /// <param name="key">The prompt key.</param>
    [Theory]
    [InlineData("tool.createTask.description")]
    [InlineData("getHelp.tasks")]
    public void CreateTaskAndGetHelp_SayHowToAskForAReview(string key)
    {
        string text = Default(key);

        Assert.Contains("tagged review", text, StringComparison.Ordinal); // contains-ok: pins the wording an Agent reads
        Assert.Contains("assigned to the reviewer", text, StringComparison.Ordinal); // contains-ok: pins the wording an Agent reads
        Assert.Contains("child", text, StringComparison.Ordinal); // contains-ok: pins the wording an Agent reads
        Assert.Contains("blocked_by", text, StringComparison.Ordinal); // contains-ok: pins the wording an Agent reads
    }

    /// <summary>update_task names the states an Agent sets and says Huddle moves a ready Task to Needs You when the Proof is complete.</summary>
    [Fact]
    public void UpdateTask_StatusSequence_IsToDoInProgressDone()
    {
        string text = Default("tool.updateTask.description");

        Assert.Contains("To Do, then In Progress, then Done", text, StringComparison.Ordinal); // contains-ok: pins the wording an Agent reads
        Assert.Contains("when its Proof is complete", text, StringComparison.Ordinal); // contains-ok: pins the wording an Agent reads
    }
}
