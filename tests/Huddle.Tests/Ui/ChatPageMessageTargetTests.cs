using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MudBlazor;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Components.Pages;
using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Elicitation;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Pipes;
using Agency.Huddle.App.Questions;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Tabs;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teammates;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Library;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Tests for the Room page's Message target (Global Search spec §9.1 steps 1 and 5): the plain
/// <c>Message</c> parameter reaches <see cref="MessageList"/>, the jump drops <c>?message=</c> from the
/// Tab's URL, and a new target on the same Room scrolls without reloading it.
/// </summary>
public sealed class ChatPageMessageTargetTests
{
    /// <summary>The script that scrolls to a target row.</summary>
    private const string ToMessage = "teamScroll.toMessage";

    /// <summary>The <c>Message</c> parameter is handed to <see cref="MessageList"/> as its target.</summary>
    [Fact]
    public async Task ChatPage_MessageParameter_ReachesMessageList()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = ChatPageMessageTargetTests.NewContext(factory);
        ctx.JSInterop.Setup<bool>(ChatPageMessageTargetTests.ToMessage, static _ => true).SetResult(true);
        Room room = await ChatPageMessageTargetTests.SeedRoomAsync(factory, ct);

        IRenderedComponent<Host> cut = ctx.Render<Host>(parameters => parameters
            .Add(host => host.RoomId, room.Id)
            .Add(host => host.Message, "m-1"));

        // The target is cleared once the jump has happened, so the jump itself is what proves it arrived.
        cut.WaitForAssertion(() => Assert.Equal(["m-1"], ChatPageMessageTargetTests.JumpTargets(ctx)));
    }

    /// <summary>After the jump the Tab's URL loses <c>?message=</c>, so a reload or a return to the Tab does not jump again (OP-16).</summary>
    [Fact]
    public async Task ChatPage_AfterJump_DropsMessageFromTheTabUrl()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = ChatPageMessageTargetTests.NewContext(factory);
        ctx.JSInterop.Setup<bool>(ChatPageMessageTargetTests.ToMessage, static _ => true).SetResult(true);
        Room room = await ChatPageMessageTargetTests.SeedRoomAsync(factory, ct);
        TabService tabs = ctx.Services.GetRequiredService<TabService>();
        tabs.Open($"/rooms/{room.Id}?message=m-1");

        IRenderedComponent<Host> cut = ctx.Render<Host>(parameters => parameters
            .Add(host => host.RoomId, room.Id)
            .Add(host => host.Message, "m-1"));

        cut.WaitForAssertion(() => Assert.Equal($"/rooms/{room.Id}", tabs.Tabs.Single().Url));
    }

    /// <summary>A new target on the Room already shown scrolls to it without loading the Room again.</summary>
    [Fact]
    public async Task ChatPage_SameRoom_NewTarget_ScrollsWithoutReload()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = ChatPageMessageTargetTests.NewContext(factory);
        ctx.JSInterop.Setup<bool>(ChatPageMessageTargetTests.ToMessage, static _ => true).SetResult(true);
        Room room = await ChatPageMessageTargetTests.SeedRoomAsync(factory, ct);
        IRenderedComponent<Host> cut = ctx.Render<Host>(parameters => parameters
            .Add(host => host.RoomId, room.Id)
            .Add(host => host.Message, "m-1"));
        cut.WaitForAssertion(() => Assert.Equal(1, ChatPageMessageTargetTests.Jumps(ctx)));
        IRenderedComponent<MessageList> list = cut.FindComponent<MessageList>();

        cut.Render(parameters => parameters.Add(host => host.Message, "m-2"));

        cut.WaitForAssertion(() => Assert.Equal(2, ChatPageMessageTargetTests.Jumps(ctx)));
        Assert.Same(list.Instance, cut.FindComponent<MessageList>().Instance);
        string[] expected = ["m-1", "m-2"];
        Assert.Equal(expected, ChatPageMessageTargetTests.JumpTargets(ctx));
    }

    /// <summary>A hit on the Message the open Room was already jumped to is a new jump: the first one dropped <c>?message=</c>, so the same id arriving again must scroll again.</summary>
    [Fact]
    public async Task ChatPage_SameRoom_SameTarget_AfterJump_JumpsAgain()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = ChatPageMessageTargetTests.NewContext(factory);
        ctx.JSInterop.Setup<bool>(ChatPageMessageTargetTests.ToMessage, static _ => true).SetResult(true);
        Room room = await ChatPageMessageTargetTests.SeedRoomAsync(factory, ct);
        IRenderedComponent<Host> cut = ctx.Render<Host>(parameters => parameters
            .Add(host => host.RoomId, room.Id)
            .Add(host => host.Message, "m-1"));
        cut.WaitForAssertion(() => Assert.Equal(1, ChatPageMessageTargetTests.Jumps(ctx)));

        cut.Render(parameters => parameters.Add(host => host.Message, null));
        cut.Render(parameters => parameters.Add(host => host.Message, "m-1"));

        cut.WaitForAssertion(() => Assert.Equal(2, ChatPageMessageTargetTests.Jumps(ctx)));
    }

    /// <summary>How many times <c>teamScroll.toMessage</c> was invoked.</summary>
    /// <param name="ctx">The context whose recorded invocations to count.</param>
    private static int Jumps(MudBunitContext ctx) => ChatPageMessageTargetTests.JumpTargets(ctx).Length;

    /// <summary>The Message ids <c>teamScroll.toMessage</c> was called with, in order.</summary>
    /// <param name="ctx">The context whose recorded invocations to read.</param>
    private static string[] JumpTargets(MudBunitContext ctx) =>
        [.. ctx.JSInterop.Invocations
            .Where(invocation => string.Equals(invocation.Identifier, ChatPageMessageTargetTests.ToMessage, StringComparison.Ordinal))
            .Select(invocation => (string)invocation.Arguments[1]!)];

    /// <summary>Creates a Room with an Agent and appends two Messages, <c>m-1</c> and <c>m-2</c>, to it.</summary>
    /// <param name="factory">The factory whose directory and store to write to.</param>
    /// <param name="ct">The test's cancellation token.</param>
    private static async Task<Room> SeedRoomAsync(TeamWebApplicationFactory factory, CancellationToken ct)
    {
        ITeamDirectory directory = factory.Services.GetRequiredService<ITeamDirectory>();
        ChatService chat = factory.Services.GetRequiredService<ChatService>();
        IChatStore store = factory.Services.GetRequiredService<IChatStore>();
        User? agent = await directory.UpsertAgentUserAsync("echo", "test agent", ct);
        Assert.NotNull(agent);
        Room room = await chat.EnsureRoomForAsync(agent, ct);
        await store.AppendAsync(room.Id, new ChatMessage("m-1", DateTimeOffset.UnixEpoch, KnownIds.Human, "You", "first"), ct);
        await store.AppendAsync(room.Id, new ChatMessage("m-2", DateTimeOffset.UnixEpoch.AddMinutes(1), agent.Id, agent.Name, "second"), ct);
        return room;
    }

    /// <summary>Registers the services the Room page injects (the set <c>ChatPageTests</c> uses) and a Tab service.</summary>
    /// <param name="factory">The factory whose composed singletons to reuse.</param>
    private static MudBunitContext NewContext(TeamWebApplicationFactory factory)
    {
        MudBunitContext ctx = new();
        IServiceProvider source = factory.Services;
        ctx.Services.AddSingleton(source.GetRequiredService<ITeamDirectory>());
        ctx.Services.AddSingleton(source.GetRequiredService<IChatStore>());
        ctx.Services.AddSingleton(source.GetRequiredService<RoomEvents>());
        ctx.Services.AddSingleton(source.GetRequiredService<ChatService>());
        ctx.Services.AddSingleton(source.GetRequiredService<Drafts>());
        ctx.Services.AddSingleton(source.GetRequiredService<IAgentGateway>());
        ctx.Services.AddSingleton(source.GetRequiredService<PersonaHealth>());
        ctx.Services.AddSingleton(source.GetRequiredService<RoomFollows>());
        ctx.Services.AddSingleton(source.GetRequiredService<PersonaStore>());
        ctx.Services.AddSingleton(source.GetRequiredService<AvatarStore>());
        ctx.Services.AddSingleton(source.GetRequiredService<ProposalStore>());
        ctx.Services.AddSingleton(source.GetRequiredService<ProposalService>());
        ctx.Services.AddSingleton(source.GetRequiredService<HireLimitGuard>());
        ctx.Services.AddSingleton(source.GetRequiredService<SprintProposalStore>());
        ctx.Services.AddSingleton(source.GetRequiredService<SprintProposalService>());
        ctx.Services.AddSingleton(source.GetRequiredService<TaskStore>());
        ctx.Services.AddSingleton(source.GetRequiredService<Agency.Huddle.App.Teams.RetroProposalStore>());
        ctx.Services.AddSingleton(source.GetRequiredService<Agency.Huddle.App.Teams.RetroProposalService>());
        ctx.Services.AddSingleton(source.GetRequiredService<QuestionStore>());
        ctx.Services.AddSingleton(source.GetRequiredService<QuestionService>());
        ctx.Services.AddSingleton(source.GetRequiredService<IOptions<TeamOptions>>());
        ctx.Services.AddSingleton(source.GetRequiredService<ElicitationStore>());
        ctx.Services.AddSingleton(source.GetRequiredService<ElicitationService>());
        ctx.Services.AddSingleton<ITaskReferenceResolver>(new FakeTaskReferenceResolver());
        ctx.Services.AddSingleton<ILibraryReferenceResolver>(new FakeLibraryNoteResolver());
        ctx.Services.AddSingleton(new TabService());
        ctx.Services.AddFakeCircuitConnection();
        return ctx;
    }

    /// <summary>Hosts the Room page beside the popover and dialog providers it needs, with <c>Message</c> as a parameter of its own so a test can render again with a new target.</summary>
    private sealed class Host : ComponentBase
    {
        /// <summary>The Room id handed to the page.</summary>
        [Parameter]
        public string? RoomId { get; set; }

        /// <summary>The target Message id handed to the page.</summary>
        [Parameter]
        public string? Message { get; set; }

        /// <inheritdoc />
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.OpenComponent<MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<MudDialogProvider>(1);
            builder.CloseComponent();
            builder.OpenComponent<Chat>(2);
            builder.AddComponentParameter(3, nameof(Chat.RoomId), this.RoomId);
            if (this.Message is not null)
            {
                // Omitted, not passed as null, when there is no target: TabPageHost's DynamicComponent leaves a
                // parameter its URL no longer carries out of the dictionary, so the page keeps its old value.
                builder.AddComponentParameter(4, nameof(Chat.Message), this.Message);
            }

            builder.CloseComponent();
        }
    }
}
