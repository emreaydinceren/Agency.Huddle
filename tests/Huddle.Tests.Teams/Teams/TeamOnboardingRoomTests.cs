using Agency.Huddle.App.Agents;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Questions;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Factories;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.Teams;

/// <summary>
/// Issue 574: the real <see cref="TeamOnboarding"/> over the real <see cref="ChatOnboardingPoster"/> posts the onboarding
/// prompt in the manager's Team-scoped one-to-one, the Room the Team page's Message button opens, and not at the root.
/// </summary>
public sealed class TeamOnboardingRoomTests
{
    private const string Team = "Marketing";
    private const string Agents = "# Home\n\n## Onboarding\n\n- Read the workspace.\n";

    /// <summary>After onboarding, the Team one-to-one holds the prompt, no root one-to-one exists, and the marker is set.</summary>
    [Fact]
    public async Task PostIfDueAsync_RealPoster_PostsInTeamOneToOneNotRoot()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using TempDataDir teamFolder = new();
        File.WriteAllText(Path.Combine(teamFolder.Path, TeamPaths.AgentsMdFileName), Agents);
        DiskTeamFolders folders = new(teamFolder.Path);
        SqliteTeamDirectory directory = new(dir.Options());
        await directory.InitializeAsync("You", ct);
        RoomEvents events = new(NullLogger<RoomEvents>.Instance);
        ChatService chat = new(
            directory, new FileChatStore(dir.Options(), NullLogger<FileChatStore>.Instance), events, new FakeMentionAliasSource(), TestOptions.Create(), new ProposalStore(events), new QuestionStore(events), NullLogger<ChatService>.Instance)
        {
            TeamCatalog = new FakeTeamCatalog { Teams = [new TeamSummary(Team, [], [], true)] },
        };
        List<MessagePostedEvent> posts = [];
        events.MessagePosted += posts.Add;
        User? nova = await directory.UpsertAgentUserAsync("Nova", null, ct);
        Assert.NotNull(nova);
        TeamOnboarding onboarding = new(folders, new FakePromptSource(), new ChatOnboardingPoster(chat, directory));

        bool posted = await onboarding.PostIfDueAsync(Team, "Nova", ct);

        Assert.True(posted);
        MessagePostedEvent post = Assert.Single(posts);
        Assert.Equal(Team, post.Room.Team);
        Assert.Equal(KnownIds.Human, post.Message.SenderId);
        Assert.Equal("Your Team has just started. Work through these onboarding steps, then tell the Human what you found:\n\n- Read the workspace.\n\nEnd by suggesting one starter Task in your reply, and ask the Human whether to start it. Do not create a Task or assign anyone until the Human says yes.", post.Message.Text);
        Room teamOneToOne = await chat.EnsureTeamOneToOneAsync(Team, nova, ct);
        Assert.Equal(post.Room.Id, teamOneToOne.Id);
        Assert.Null(await directory.FindRoomWithExactMembersAsync(KnownIds.Human, nova.Id, null, null, ct));
        Assert.True(folders.File?.Onboarded);
    }
}
