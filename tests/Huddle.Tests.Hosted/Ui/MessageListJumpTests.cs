using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Library;
using Agency.Huddle.Tests.Css;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Tests for the Room jump in <see cref="MessageList"/> (Global Search spec §9.1): every row carries
/// its Message id, a target scrolls to that row instead of the bottom, and the stick-to-bottom rule
/// stays held off until the Human returns to the bottom or sends. bUnit runs no layout, so the
/// scripts are observed as recorded <c>JSInterop</c> invocations, never as scroll positions.
/// </summary>
public sealed class MessageListJumpTests
{
    /// <summary>The script that scrolls to a target row.</summary>
    private const string ToMessage = "teamScroll.toMessage";

    /// <summary>The script that sticks to the bottom.</summary>
    private const string ToBottom = "teamScroll.toBottom";

    /// <summary>The script that reports whether the list is scrolled to its bottom.</summary>
    private const string IsAtBottom = "teamScroll.isAtBottom";

    /// <summary>The Human's user id in these tests.</summary>
    private const string HumanId = "u-human";

    /// <summary>A Task wake-up in the default wording, which renders as the slim system row.</summary>
    private const string WakeText = "@Grace Task PLAT-0004 \"Fix login\" (Done, Platform) was changed by You:\n- status: Review -> Done\nCall get_task with taskId PLAT-0004 for the full task.";

    /// <summary>
    /// The Human's own Message renders the avatar and meta line a style pack shows (ADR-0057), and <c>app.css</c> hides
    /// each of those direct children by the class it really carries, so every Theme without a pack keeps the bare
    /// bubble. <c>ProfileAvatar</c>'s root is <c>.profile-avatar</c>, not the <c>avatar-transcript</c> passed to it.
    /// </summary>
    [Fact]
    public async Task HumanRow_EveryChildBesideTheBubble_IsHiddenByAppCss()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = MessageListJumpTests.NewContext(factory);

        IRenderedComponent<MessageList> cut = ctx.Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, [MessageListJumpTests.Human("m-1", "hello")])
            .Add(p => p.HumanIds, [MessageListJumpTests.HumanId]));

        string[] extraChildren = [.. cut.Find("div.message-row.human").Children
            .Select(static child => child.ClassList[0])
            .Where(static name => name is not ("message-bubble" or "message-references"))];
        string[] expected = ["profile-avatar", "message-meta"];
        Assert.Equal(expected, extraChildren);

        string css = CssText.AppCss(CssNewlines.AsIs);
        foreach (string name in extraChildren)
        {
            Assert.Contains($".message-row.human > .{name}", css, StringComparison.Ordinal); // contains-ok: haystack is the stylesheet source
        }
    }

    /// <summary>A Human bubble, an Agent row and the task-wake system row each carry <c>data-message-id</c> and <c>tabindex="-1"</c> (GS-T22).</summary>
    [Fact]
    public async Task Rows_CarryDataMessageId()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = MessageListJumpTests.NewContext(factory);
        ChatMessage[] messages =
        [
            MessageListJumpTests.Human("m-1", "hello"),
            MessageListJumpTests.Agent("m-2", "hi back"),
            MessageListJumpTests.Human("m-3", MessageListJumpTests.WakeText),
        ];

        IRenderedComponent<MessageList> cut = ctx.Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.HumanIds, [MessageListJumpTests.HumanId]));

        string?[] ids = [.. cut.FindAll("[data-message-id]").Select(row => row.GetAttribute("data-message-id"))];
        string?[] expectedIds = ["m-1", "m-2", "m-3"];
        Assert.Equal(expectedIds, ids);
        string?[] tabIndexes = [.. cut.FindAll("[data-message-id]").Select(row => row.GetAttribute("tabindex"))];
        string?[] expectedTabIndexes = ["-1", "-1", "-1"];
        Assert.Equal(expectedTabIndexes, tabIndexes);
        Assert.Single(cut.FindAll("details.system-row[data-message-id='m-3']"));
        Assert.Single(cut.FindAll("div.message-row.human[data-message-id='m-1']"));
    }

    /// <summary>With a target the first render scrolls to that row and never to the bottom (GS-T21).</summary>
    [Fact]
    public async Task WithTarget_FirstRenderCallsToMessageNotToBottom()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = MessageListJumpTests.NewContext(factory);
        ctx.JSInterop.Setup<bool>(MessageListJumpTests.ToMessage, static _ => true).SetResult(true);
        int jumped = 0;

        IRenderedComponent<MessageList> cut = ctx.Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, MessageListJumpTests.Three())
            .Add(p => p.TargetMessageId, "m-2")
            .Add(p => p.OnJumped, EventCallback.Factory.Create(this, () => jumped++)));

        cut.WaitForAssertion(() => Assert.Equal(1, jumped));
        Assert.Equal(["m-2"], MessageListJumpTests.TargetsJumpedTo(ctx));
        Assert.Equal(0, MessageListJumpTests.Calls(ctx, MessageListJumpTests.ToBottom));
    }

    /// <summary>A Message that arrives after the jump is rendered but does not move the view, because stick-to-bottom is held off (GS-T21).</summary>
    [Fact]
    public async Task WithTarget_LaterMessage_DoesNotCallToBottom()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = MessageListJumpTests.NewContext(factory);
        ctx.JSInterop.Setup<bool>(MessageListJumpTests.ToMessage, static _ => true).SetResult(true);
        ChatMessage[] messages = MessageListJumpTests.Three();
        IRenderedComponent<MessageList> cut = ctx.Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.HumanIds, [MessageListJumpTests.HumanId])
            .Add(p => p.TargetMessageId, "m-2"));

        ChatMessage[] more = [.. messages, MessageListJumpTests.Agent("m-4", "arrived meanwhile")];
        cut.Render(parameters => parameters.Add(p => p.Messages, more));

        Assert.Single(cut.FindAll("[data-message-id='m-4']"));
        Assert.Equal(0, MessageListJumpTests.Calls(ctx, MessageListJumpTests.ToBottom));
    }

    /// <summary>Once the list reports it is scrolled to the bottom, the next growth sticks to the bottom again.</summary>
    [Fact]
    public async Task AfterHumanReturnsToBottom_StickResumes()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = MessageListJumpTests.NewContext(factory);
        ctx.JSInterop.Setup<bool>(MessageListJumpTests.ToMessage, static _ => true).SetResult(true);
        ctx.JSInterop.Setup<bool>(MessageListJumpTests.IsAtBottom, static _ => true).SetResult(true);
        ChatMessage[] messages = MessageListJumpTests.Three();
        IRenderedComponent<MessageList> cut = ctx.Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.HumanIds, [MessageListJumpTests.HumanId])
            .Add(p => p.TargetMessageId, "m-2"));

        ChatMessage[] more = [.. messages, MessageListJumpTests.Agent("m-4", "next")];
        cut.Render(parameters => parameters.Add(p => p.Messages, more));

        cut.WaitForAssertion(() => Assert.Equal(1, MessageListJumpTests.Calls(ctx, MessageListJumpTests.ToBottom)));
    }

    /// <summary>A Message the Human sends ends the hold even when the list is not at the bottom, so their own Message is followed.</summary>
    [Fact]
    public async Task AfterHumanSends_StickResumes()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = MessageListJumpTests.NewContext(factory);
        ctx.JSInterop.Setup<bool>(MessageListJumpTests.ToMessage, static _ => true).SetResult(true);
        ChatMessage[] messages = MessageListJumpTests.Three();
        IRenderedComponent<MessageList> cut = ctx.Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.HumanIds, [MessageListJumpTests.HumanId])
            .Add(p => p.TargetMessageId, "m-2"));

        ChatMessage[] more = [.. messages, MessageListJumpTests.Human("m-4", "my reply")];
        cut.Render(parameters => parameters.Add(p => p.Messages, more));

        cut.WaitForAssertion(() => Assert.Equal(1, MessageListJumpTests.Calls(ctx, MessageListJumpTests.ToBottom)));
    }

    /// <summary>A target that is not in the Transcript opens the Room at the bottom, as today, and still reports the jump handled (§9.1 step 6).</summary>
    [Fact]
    public async Task UnknownTarget_FallsBackToBottom()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = MessageListJumpTests.NewContext(factory);
        ctx.JSInterop.Setup<bool>(MessageListJumpTests.ToMessage, static _ => true).SetResult(false);
        int jumped = 0;

        IRenderedComponent<MessageList> cut = ctx.Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, MessageListJumpTests.Three())
            .Add(p => p.TargetMessageId, "gone")
            .Add(p => p.OnJumped, EventCallback.Factory.Create(this, () => jumped++)));

        cut.WaitForAssertion(() => Assert.Equal(1, MessageListJumpTests.Calls(ctx, MessageListJumpTests.ToBottom)));
        Assert.Equal(1, jumped);
    }

    /// <summary>A new target on the same list jumps again, and the same target is not jumped to twice.</summary>
    [Fact]
    public async Task NewTarget_JumpsOnce_PerTarget()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = MessageListJumpTests.NewContext(factory);
        ctx.JSInterop.Setup<bool>(MessageListJumpTests.ToMessage, static _ => true).SetResult(true);
        ChatMessage[] messages = MessageListJumpTests.Three();
        IRenderedComponent<MessageList> cut = ctx.Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, messages)
            .Add(p => p.TargetMessageId, "m-1"));

        cut.Render(parameters => parameters.Add(p => p.TargetMessageId, "m-1"));
        cut.Render(parameters => parameters.Add(p => p.TargetMessageId, "m-3"));

        string[] expected = ["m-1", "m-3"];
        cut.WaitForAssertion(() => Assert.Equal(expected, MessageListJumpTests.TargetsJumpedTo(ctx)));
    }

    /// <summary>How many times the script <paramref name="identifier"/> was invoked.</summary>
    /// <param name="ctx">The context whose recorded invocations to count.</param>
    /// <param name="identifier">The script name.</param>
    private static int Calls(MudBunitContext ctx, string identifier) =>
        ctx.JSInterop.Invocations.Count(invocation => string.Equals(invocation.Identifier, identifier, StringComparison.Ordinal));

    /// <summary>The Message ids <c>teamScroll.toMessage</c> was called with, in order.</summary>
    /// <param name="ctx">The context whose recorded invocations to read.</param>
    private static string[] TargetsJumpedTo(MudBunitContext ctx) =>
        [.. ctx.JSInterop.Invocations
            .Where(invocation => string.Equals(invocation.Identifier, MessageListJumpTests.ToMessage, StringComparison.Ordinal))
            .Select(invocation => (string)invocation.Arguments[1]!)];

    /// <summary>Three settled Messages: the Human, an Agent and the Agent again.</summary>
    private static ChatMessage[] Three() =>
    [
        MessageListJumpTests.Human("m-1", "hello"),
        MessageListJumpTests.Agent("m-2", "hi back"),
        MessageListJumpTests.Agent("m-3", "anything else?"),
    ];

    /// <summary>A Message from the Human.</summary>
    /// <param name="id">The Message id.</param>
    /// <param name="text">The Message text.</param>
    private static ChatMessage Human(string id, string text) =>
        new(id, DateTimeOffset.UnixEpoch, MessageListJumpTests.HumanId, "You", text);

    /// <summary>A Message from an Agent.</summary>
    /// <param name="id">The Message id.</param>
    /// <param name="text">The Message text.</param>
    private static ChatMessage Agent(string id, string text) =>
        new(id, DateTimeOffset.UnixEpoch, "u-agent", "echo", text);

    /// <summary>Registers the services <see cref="MessageList"/> injects, as <c>MessageListTests</c> does.</summary>
    /// <param name="factory">The factory whose composed singletons to reuse.</param>
    private static MudBunitContext NewContext(TeamWebApplicationFactory factory)
    {
        MudBunitContext ctx = new();
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<AvatarStore>());
        ctx.Services.AddSingleton<ITaskReferenceResolver>(new FakeTaskReferenceResolver());
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<IOptions<TeamOptions>>());
        ctx.Services.AddSingleton<ILibraryReferenceResolver>(new FakeLibraryNoteResolver());
        return ctx;
    }
}
