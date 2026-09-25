namespace Agency.Huddle.Tests.Ui;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Pipes;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Teammates;

/// <summary>
/// Renders <see cref="InviteTeammate"/> on its own. The control lives behind a click on the Room
/// header, so an HTTP GET of the chat page only ever returns the prerender without it — this is the
/// layer that can see what the panel actually offers.
/// </summary>
public sealed class InviteTeammateTests
{
    [Fact]
    public async Task Panel_OffersOnlyAgentsThatAreNotAlreadyMembers()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var inside = await directory.UpsertAgentUserAsync("inside", null, ct);
        var outside = await directory.UpsertAgentUserAsync("outside", null, ct);
        Assert.NotNull(inside);
        Assert.NotNull(outside);
        var chat = CreateChatService(dir, directory);
        var room = await chat.EnsureRoomForAsync(inside, ct);

        var html = await RenderAsync(dir, directory, chat, room.Id);

        Assert.Contains("outside", html, StringComparison.Ordinal);
        Assert.DoesNotContain(">inside", html, StringComparison.Ordinal);
        Assert.Contains("Add teammate", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Panel_SaysSoWhenEveryAgentIsAlreadyInTheRoom()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var only = await directory.UpsertAgentUserAsync("only", null, ct);
        Assert.NotNull(only);
        var chat = CreateChatService(dir, directory);
        var room = await chat.EnsureRoomForAsync(only, ct);

        var html = await RenderAsync(dir, directory, chat, room.Id);

        Assert.Contains("Every agent is already in this room.", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Panel_StartsClosed()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var agent = await directory.UpsertAgentUserAsync("agent", null, ct);
        Assert.NotNull(agent);
        var chat = CreateChatService(dir, directory);
        var room = await chat.EnsureRoomForAsync(agent, ct);

        var html = await RenderAsync(dir, directory, chat, room.Id);

        // The panel's markup is now a MudCollapse: closed means its container carries MudBlazor's
        // own "invisible" class rather than the hand-rolled hidden attribute this test used to pin.
        Assert.Contains("mud-collapse-container invisible", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// The control and the Agent's own tool are two doors onto one behaviour. This pins that the
    /// door the Human uses ends in the same rename the Agent's does.
    /// </summary>
    [Fact]
    public async Task Invite_RenamesTheRoomAfterItsAgents()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var first = await directory.UpsertAgentUserAsync("first", null, ct);
        var second = await directory.UpsertAgentUserAsync("second", null, ct);
        Assert.NotNull(first);
        Assert.NotNull(second);
        var chat = CreateChatService(dir, directory);
        var room = await chat.EnsureRoomForAsync(first, ct);

        await chat.InviteAsync(room.Id, "second", ct);

        var renamed = await directory.GetRoomAsync(room.Id, ct);
        Assert.NotNull(renamed);
        Assert.Contains("first", renamed.Name, StringComparison.Ordinal);
        Assert.Contains("second", renamed.Name, StringComparison.Ordinal);
        var members = await directory.GetRoomMembersAsync(room.Id, ct);
        Assert.Equal(3, members.Count);
    }

    /// <summary>
    /// The filter must not blind the panel to Agents with no Persona at all (a raw pipe client with
    /// no file behind it, or the echo/alpha demo agents): such an Agent has no Teams, so it can
    /// only ever appear under "All teams" - this proves it still does, and that the filter's own
    /// options come from <see cref="PersonaStore.Teams"/>, even once a Persona-backed Team exists.
    /// </summary>
    [Fact]
    public async Task Panel_OffersATeamFilter_AndStillInvitesAnAgentWithNoPersonaBehindIt()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        Directory.CreateDirectory(Path.Combine(dir.Path, "Teams"));
        await File.WriteAllTextAsync(
            Path.Combine(dir.Path, "Teams", "coo.md"),
            "---\nName: coo\nTitle: Chief of Staff\nAlias: coo\nTeams: Business\n---\nbody",
            ct);
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var member = await directory.UpsertAgentUserAsync("member", null, ct);
        var raw = await directory.UpsertAgentUserAsync("rawagent", null, ct);
        Assert.NotNull(member);
        Assert.NotNull(raw);
        var chat = CreateChatService(dir, directory);

        // The Room is with "member" only, so "rawagent" is not yet a Member and shows up as an
        // invite candidate - the whole point of this test.
        var room = await chat.EnsureRoomForAsync(member, ct);

        var html = await RenderAsync(dir, directory, chat, room.Id);

        Assert.Contains("All teams", html, StringComparison.Ordinal);
        Assert.Contains("Business", html, StringComparison.Ordinal);
        Assert.Contains("rawagent", html, StringComparison.Ordinal);
    }

    /// <summary>Each invite candidate row renders a <see cref="TeammateAvatar"/> beside its name, matching the gutter <see cref="MessageList"/>'s transcript rows already carry.</summary>
    [Fact]
    public async Task Panel_RendersAnAvatarBesideEachCandidate()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);
        var inside = await directory.UpsertAgentUserAsync("inside", null, ct);
        var outside = await directory.UpsertAgentUserAsync("outside", null, ct);
        Assert.NotNull(inside);
        Assert.NotNull(outside);
        var chat = CreateChatService(dir, directory);
        var room = await chat.EnsureRoomForAsync(inside, ct);

        var html = await RenderAsync(dir, directory, chat, room.Id);

        Assert.Contains("mud-avatar", html, StringComparison.Ordinal);
    }

    private static ChatService CreateChatService(TempDataDir dir, ITeamDirectory directory)
    {
        var store = new FileChatStore(dir.Options(), NullLogger<FileChatStore>.Instance);
        var events = new RoomEvents(NullLogger<RoomEvents>.Instance);
        var proposals = new ProposalStore(events);
        return new ChatService(directory, store, events, new FakeMentionAliasSource(), Options.Create(new TeamOptions()), proposals, NullLogger<ChatService>.Instance);
    }

    private static async Task<string> RenderAsync(
        TempDataDir dir, ITeamDirectory directory, ChatService chat, string roomId)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(dir.Options());
        services.AddSingleton(directory);
        services.AddSingleton(chat);
        services.AddSingleton<IAgentGateway>(new FakeAgentGateway());
        services.AddSingleton(new PersonaHealth(TimeProvider.System, NullLogger<PersonaHealth>.Instance));
        services.AddSingleton(new RoomEvents(NullLogger<RoomEvents>.Instance));

        // The panel's own team filter needs a PersonaStore; created and disposed within this one
        // render so its FileSystemWatcher never outlives the test.
        using var personas = new PersonaStore(
            new TeammatePaths(dir.Options()), new PersonaModelStore(dir.Options()), new PersonaEffortStore(dir.Options()), NullLogger<PersonaStore>.Instance);
        services.AddSingleton(personas);

        // The panel now renders a TeammateAvatar beside each candidate; created and disposed within
        // this one render, the same lifetime PersonaStore above is given, so its own FileSystemWatcher
        // never outlives the test either.
        using var avatars = new AvatarStore(dir.Options(), NullLogger<AvatarStore>.Instance);
        services.AddSingleton(avatars);
        await using var provider = services.BuildServiceProvider();

        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());

        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<InviteTeammate>(
                ParameterView.FromDictionary(new Dictionary<string, object?> { ["RoomId"] = roomId }));

            return output.ToHtmlString();
        });
    }
}