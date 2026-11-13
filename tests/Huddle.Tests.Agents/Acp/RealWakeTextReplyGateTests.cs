using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Jobs;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.Tests.Acp;

/// <summary>
/// Pins that the Huddle posts which wake an Agent (ADR-0065, V-3) still do, using the real texts: the Job run text from
/// <see cref="JobRunner.TextOf"/> and the <c>task.wake.message</c> Prompt rendered as production renders it, not a hand-typed string.
/// </summary>
public sealed class RealWakeTextReplyGateTests
{
    private static readonly User Nova = new("u-nova", "Nova", UserKind.Agent, null);

    private static ReplyDecision Decide(string text)
    {
        IReadOnlyList<User> members = [new User("u-human", "You", UserKind.Human, null), Nova, new User("u-kai", "Kai", UserKind.Agent, null)];
        bool mentioned = MentionParser.Parse(text, members).Any(static user => user.Id == Nova.Id);
        return ReplyGate.Decide(mentioned, members.Count, 0, 10, false, fromHuddle: true);
    }

    /// <summary>A Job run text for an Agent Mentions it, so a Huddle post of it gets a Reply even in a Room of three Members.</summary>
    [Fact]
    public void JobRunnerText_ForAnAgent_GivesReply()
    {
        Job job = new("job-1", JobOwner.Agent("Nova"), "Digest", "Do it.", JobRecurrence.EveryHours(2), null, null, false);

        Assert.Equal(ReplyDecision.Reply, Decide(JobRunner.TextOf(job, Nova)));
    }

    /// <summary>The default <c>task.wake.message</c> text, rendered with the values production passes, gives Reply.</summary>
    [Fact]
    public void TaskWakeMessage_RenderedAsProductionDoes_GivesReply()
    {
        string raw = PromptCatalog.All.Single(static definition => definition.Key == "task.wake.message").Default;
        string text = PromptRenderer.Render(raw, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["{{assignee}}"] = "Nova",
            ["{{taskId}}"] = "PLAT-1",
            ["{{title}}"] = "Plan",
            ["{{status}}"] = "todo",
            ["{{team}}"] = "Platform",
            ["{{actor}}"] = "You",
            ["{{changes}}"] = "- Status changed",
        });

        Assert.Equal(ReplyDecision.Reply, Decide(text));
    }

    /// <summary>A Huddle post that Mentions nobody gets CatchUp: the check above can fail.</summary>
    [Fact]
    public void HuddlePostWithoutMention_GivesCatchUp() =>
        Assert.Equal(ReplyDecision.CatchUp, Decide("Team 'Old' is now 'New'."));
}
