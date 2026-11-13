using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Pipes;
using Agency.Huddle.Tests.Waiting;
using Agency.Huddle.Tests.Factories;

namespace Agency.Huddle.Tests.Acp;

/// <summary>
/// A Message Huddle posts itself (ADR-0065) wakes an Agent only when it Mentions that Agent (Spec §6.4, V-3):
/// the Agent's Room with the Human has two Members, which alone would make it answer everything.
/// </summary>
public sealed class PersonaRunnerHuddlePostTests
{
    /// <summary>A Huddle post with no Mention in the two-Member Room starts no Turn: the next Human post is the first prompt.</summary>
    [Fact]
    public async Task HuddlePostWithoutMention_InTheAgentsHumanRoom_StartsNoTurn()
    {
        using BehaviourBudget cts = new(TimeSpan.FromSeconds(10));
        CancellationToken ct = cts.Token;
        await using PipeHostFixture fixture = await PipeHostFixture.StartAsync(cts);
        FakeAgentHostFactory factory = new();
        await using PersonaRunner runner = CreateRunner(fixture, factory);
        await runner.StartAsync(ct);
        (string _, string roomId) = await Eventually.DirectRoomAsync(fixture.Services, "nova", ct);
        ChatService chat = fixture.Services.GetRequiredService<ChatService>();

        _ = await chat.PostAsHuddleAsync(roomId, "Team 'Old' is now 'New'.", ct);
        _ = await chat.PostAsync(roomId, KnownIds.Human, "probe", ct: ct);
        IReadOnlyList<string> prompts = await WaitForPrompts(factory.Session, ct);

        Assert.Contains("probe", Assert.Single(prompts), StringComparison.Ordinal);
    }

    /// <summary>A Huddle post that Mentions the Agent still starts a Turn, as the Job and Task wakes rely on.</summary>
    [Fact]
    public async Task HuddlePostMentioningTheAgent_StartsATurn()
    {
        using BehaviourBudget cts = new(TimeSpan.FromSeconds(10));
        CancellationToken ct = cts.Token;
        await using PipeHostFixture fixture = await PipeHostFixture.StartAsync(cts);
        FakeAgentHostFactory factory = new();
        await using PersonaRunner runner = CreateRunner(fixture, factory);
        await runner.StartAsync(ct);
        (string _, string roomId) = await Eventually.DirectRoomAsync(fixture.Services, "nova", ct);
        ChatService chat = fixture.Services.GetRequiredService<ChatService>();

        _ = await chat.PostAsHuddleAsync(roomId, "@nova wake up", ct);
        IReadOnlyList<string> prompts = await WaitForPrompts(factory.Session, ct);

        Assert.Contains("wake up", Assert.Single(prompts), StringComparison.Ordinal);
    }

    private static PersonaRunner CreateRunner(PipeHostFixture fixture, FakeAgentHostFactory factory)
    {
        IOptions<TeamOptions> options = fixture.Services.GetRequiredService<IOptions<TeamOptions>>();
        return new PersonaRunner(TestPersonas.Nova, options, factory, new FakePromptSource(), new RoomFollows(), NullLogger<PersonaRunner>.Instance);
    }

    private static Task<IReadOnlyList<string>> WaitForPrompts(FakeAgentSession session, CancellationToken ct)
    {
        return Eventually.ValueAsync(
            _ => Task.FromResult(session.Prompts),
            prompts => prompts.Count >= 1,
            ct,
            TestBudgets.Slowest,
            TimeSpan.FromMilliseconds(5),
            what: "at least 1 recorded prompt");
    }
}
