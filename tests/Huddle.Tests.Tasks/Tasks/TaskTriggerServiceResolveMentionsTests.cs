using Agency.Huddle.App;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Tasks;
using static Agency.Huddle.Tests.Tasks.MentionTestSupport;
using static Agency.Huddle.Tests.Tasks.TaskTriggerTestSupport;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>
/// <c>TaskTriggerService.ResolveCommentMentionsAsync(task, comment, author, ct)</c> (smart wakes spec 3.3, correction 9): the Agent names a comment would wake
/// after the guards, which <c>add_comment</c> reports back and the batch fire uses. Nova is the assignee, Kai the usual author, Rex has the Alias
/// <c>rexy</c>, and Ghost is an Agent user that is no registered Persona.
/// </summary>
public sealed class TaskTriggerServiceResolveMentionsTests
{
    /// <summary>Builds the harness, the Cast, Rex and Ghost, and a Task assigned to Nova; the caller disposes the harness.</summary>
    private static async Task<(Harness Harness, Cast Cast, TaskItem Task)> ArrangeAsync(TempDataDir dir, TeamOptions options, CancellationToken ct)
    {
        Harness harness = await CreateHarnessAsync(dir, options, ct);
        Cast cast = await StartWakingAsync(harness, ct);
        Room room = await AddRexAsync(harness, cast, ct);
        await AddGhostUserAsync(harness, ct);
        return (harness, cast, harness.Seed(TestTasks.Make(assignee: "Nova", originRoomId: room.Id)));
    }

    /// <summary>A Name resolves to its Persona's canonical Name whatever case it was typed in, and an Alias resolves to the Persona it belongs to.</summary>
    /// <param name="text">The comment text.</param>
    [Theory]
    [InlineData("Please look, @Rex.")]
    [InlineData("Please look, @REX.")]
    [InlineData("Please look, @rexy.")]
    public async Task Resolve_NameOrAlias_ReturnsTheCanonicalName(string text)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        (Harness harness, Cast cast, TaskItem task) = await ArrangeAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        using Harness owned = harness;

        IReadOnlyList<string> names = await harness.Trigger.ResolveCommentMentionsAsync(task, text, cast.KaiActor, ct);

        Assert.Equal(["Rex"], names);
    }

    /// <summary>Several Mentions come back in first-mention order with a repeat (even by Alias or in another case) listed once.</summary>
    [Fact]
    public async Task Resolve_SeveralMentions_ReturnsFirstMentionOrderWithoutRepeats()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        (Harness harness, Cast cast, TaskItem task) = await ArrangeAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        using Harness owned = harness;

        IReadOnlyList<string> names = await harness.Trigger.ResolveCommentMentionsAsync(task, "@rexy then @Nova, again @REX and @nova.", cast.KaiActor, ct);

        Assert.Equal(["Rex", "Nova"], names);
    }

    /// <summary>The author, the Human, a name nobody has and a user that is no registered Persona are each left out, while the Agents beside them stay.</summary>
    [Fact]
    public async Task Resolve_AuthorHumanUnknownAndUnregistered_AreLeftOut()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        (Harness harness, Cast cast, TaskItem task) = await ArrangeAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        using Harness owned = harness;

        IReadOnlyList<string> names = await harness.Trigger.ResolveCommentMentionsAsync(task, "@Kai @You @Zed @Ghost @Nova", cast.KaiActor, ct);

        Assert.Equal(["Nova"], names);
    }

    /// <summary>The Human can Mention an Agent, and is the one name left out when an Agent Mentions the Human back.</summary>
    [Fact]
    public async Task Resolve_HumanAuthor_ReturnsTheMentionedAgents()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        (Harness harness, _, TaskItem task) = await ArrangeAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        using Harness owned = harness;

        IReadOnlyList<string> names = await harness.Trigger.ResolveCommentMentionsAsync(task, "@Kai and @Nova, from me, @You.", HumanActor, ct);

        Assert.Equal(["Kai", "Nova"], names);
    }

    /// <summary><c>@all</c> wakes no one, alone or beside a named Agent (correction 9).</summary>
    [Fact]
    public async Task Resolve_AtAll_ReturnsNobodyButStillReturnsTheNamedAgent()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        (Harness harness, Cast cast, TaskItem task) = await ArrangeAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        using Harness owned = harness;

        IReadOnlyList<string> alone = await harness.Trigger.ResolveCommentMentionsAsync(task, "Everyone, @all please read.", cast.KaiActor, ct);
        IReadOnlyList<string> beside = await harness.Trigger.ResolveCommentMentionsAsync(task, "@all and @Rex.", cast.KaiActor, ct);

        Assert.Empty(alone);
        Assert.Equal(["Rex"], beside);
    }

    /// <summary>A comment with no <c>@</c>, or with an <c>@</c> that is part of an address, returns nobody.</summary>
    /// <param name="text">The comment text.</param>
    [Theory]
    [InlineData("Nothing to see here.")]
    [InlineData("Write to rex@example.com.")]
    public async Task Resolve_NoMention_ReturnsNobody(string text)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        (Harness harness, Cast cast, TaskItem task) = await ArrangeAsync(dir, new TeamOptions { HumanName = "You" }, ct);
        using Harness owned = harness;
        Assert.Equal(["Rex"], await harness.Trigger.ResolveCommentMentionsAsync(task, "@Rex", cast.KaiActor, ct));

        IReadOnlyList<string> names = await harness.Trigger.ResolveCommentMentionsAsync(task, text, cast.KaiActor, ct);

        Assert.Empty(names);
    }

    /// <summary>With <c>Tasks.WakeEnabled</c> turned off (the options are read live) the answer is nobody: add_comment must not promise a wake that will not happen.</summary>
    [Fact]
    public async Task Resolve_WakeUpsOff_ReturnsNobody()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        TeamOptions options = new() { HumanName = "You" };
        (Harness harness, Cast cast, TaskItem task) = await ArrangeAsync(dir, options, ct);
        using Harness owned = harness;
        Assert.Equal(["Rex"], await harness.Trigger.ResolveCommentMentionsAsync(task, "@Rex", cast.KaiActor, ct));
        options.Tasks.WakeEnabled = false;

        IReadOnlyList<string> names = await harness.Trigger.ResolveCommentMentionsAsync(task, "@Rex", cast.KaiActor, ct);

        Assert.Empty(names);
    }

    /// <summary>With <c>Tasks.Enabled</c> turned off the answer is nobody.</summary>
    [Fact]
    public async Task Resolve_TasksOff_ReturnsNobody()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        TeamOptions options = new() { HumanName = "You" };
        (Harness harness, Cast cast, TaskItem task) = await ArrangeAsync(dir, options, ct);
        using Harness owned = harness;
        Assert.Equal(["Rex"], await harness.Trigger.ResolveCommentMentionsAsync(task, "@Rex", cast.KaiActor, ct));
        options.Tasks.Enabled = false;

        IReadOnlyList<string> names = await harness.Trigger.ResolveCommentMentionsAsync(task, "@Rex", cast.KaiActor, ct);

        Assert.Empty(names);
    }

    /// <summary>The Task's wake budget is a delivery matter: an Agent author whose budget is spent still gets the names back, because the wake is held rather than dropped.</summary>
    [Fact]
    public async Task Resolve_AgentAuthorWithASpentBudget_StillReturnsTheNames()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        TeamOptions options = new() { HumanName = "You" };
        options.Tasks.AgentWakeBudget = 1;
        (Harness harness, Cast cast, TaskItem task) = await ArrangeAsync(dir, options, ct);
        using Harness owned = harness;
        Assert.True(harness.Activity.TryConsumeAgentWake(task.Id));

        IReadOnlyList<string> names = await harness.Trigger.ResolveCommentMentionsAsync(task, "@Rex", cast.KaiActor, ct);

        Assert.Equal(["Rex"], names);
    }
}
