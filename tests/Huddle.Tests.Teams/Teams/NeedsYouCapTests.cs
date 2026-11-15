using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Feed;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Feed;
using Agency.Huddle.Tests.Tasks;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.Teams;

/// <summary>
/// The spend cap of a scope that runs inside the Team (ADR-0087): spend counted from the moment the Human set the level, the fall back to
/// Ask me at the cap, the one question per allowance, and Yes granting exactly one more cap.
/// </summary>
public sealed class NeedsYouCapTests
{
    private const string Usd = "USD";
    private const string Dev = "Dev";
    private const string AskText = "Dev has spent 5.00 USD running inside the Team, its cap. Let it spend another 5.00 USD?";

    private const string AskMeText = "Cannot merge: a merge into main waits for the Human's Accept, and no Task of your Team has one. Put the merge in the Action of the Task this merge belongs to with update_task, for example 'Merge feat/x into main'. Huddle moves the Task to Needs You when its Proof is complete; merge after the Human accepts it. Do not ask for the Accept in chat.";

    private static readonly VersionedRef Type = new("development", "1.0.0");

    /// <summary>A catalog of Dev (Nova, Leo; Project Site) and Ops (Ivy), levels over an in-memory Team file, and the stores they count with.</summary>
    private sealed class Rig : IDisposable
    {
        private readonly TempDataDir dir = new();

        public Rig(string? globalLevel = null, decimal? globalCap = null)
        {
            this.Catalog = new FakeTeamCatalog
            {
                Teams =
                [
                    new TeamSummary(Dev, ["Site"], ["Nova", "Leo"], true),
                    new TeamSummary("Ops", [], ["Ivy"], true),
                ],
            };
            this.Folders = new MemoryFolders { Team = new TeamFile(Type, null, null, null, string.Empty) };
            this.Folders.Projects["Site"] = ProjectFile.Empty;
            this.Spend = new PersonaSpendStore(this.dir.Options());
            this.Feed = new RecordingFeedPublisher();
            this.Caps = new NeedsYouCaps(new NeedsYouCapStore(this.dir.Options()), this.Spend, this.Catalog, this.Feed);
            this.Levels = new NeedsYouLevels(
                this.Catalog,
                this.Folders,
                new NeedsYouLevelStore(this.dir.Options()),
                this.dir.Options(options =>
                {
                    options.NeedsYouLevel.Level = globalLevel ?? string.Empty;
                    options.NeedsYouLevel.SpendCap = globalCap;
                }),
                caps: this.Caps);
        }

        public FakeTeamCatalog Catalog { get; }

        public MemoryFolders Folders { get; }

        public PersonaSpendStore Spend { get; }

        public RecordingFeedPublisher Feed { get; }

        public NeedsYouCaps Caps { get; }

        public NeedsYouLevels Levels { get; }

        public EffectiveLevel Current => this.Levels.For(NeedsYouCapTests.Dev, null);

        public void Spent(string persona, decimal amount, string currency = Usd) => this.Spend.Add(persona, null, currency, amount);

        public void Dispose() => this.dir.Dispose();
    }

    /// <summary>Below the cap the Team still runs inside the Team; at the cap it falls back to Ask me and says why.</summary>
    [Fact]
    public void For_TeamAtItsCap_FallsBackToAskMeWithCapReached()
    {
        using Rig rig = new();
        Assert.True(rig.Levels.SetTeam(Dev, NeedsYouLevel.RunInsideTheTeam, 5m));

        rig.Spent("Nova", 4.99m);
        EffectiveLevel under = rig.Current;
        rig.Spent("Nova", 0.01m);
        EffectiveLevel atCap = rig.Current;

        Assert.Equal(new EffectiveLevel(NeedsYouLevel.RunInsideTheTeam, NeedsYouScope.Team, Dev, 5m), under);
        Assert.Equal(new EffectiveLevel(NeedsYouLevel.AskMe, NeedsYouScope.Team, Dev, 5m, CapReached: true), atCap);
    }

    /// <summary>A scope with no cap never falls back, however much it spends, and never asks.</summary>
    [Fact]
    public void For_NoCap_NeverAsks()
    {
        using Rig rig = new();
        Assert.True(rig.Levels.SetTeam(Dev, NeedsYouLevel.RunInsideTheTeam, null));

        rig.Spent("Nova", 5000m);

        Assert.Equal(NeedsYouLevel.RunInsideTheTeam, rig.Current.Level);
        Assert.Empty(rig.Feed.Posts);
        Assert.Empty(rig.Caps.Pending);
    }

    /// <summary>Reaching the cap posts exactly one Notification with the asked wording, however often the level is read.</summary>
    [Fact]
    public void For_CapReached_PostsOneNotificationPerAllowance()
    {
        using Rig rig = new();
        _ = rig.Levels.SetTeam(Dev, NeedsYouLevel.RunInsideTheTeam, 5m);
        rig.Spent("Nova", 5m);

        _ = rig.Current;
        _ = rig.Current;
        _ = rig.Levels.For(Dev, "Site");

        FeedMessage post = Assert.Single(rig.Feed.Posts);
        Assert.Equal(FeedSourceType.SpendCapReached, post.SourceType);
        Assert.True(post.Notify);
        Assert.Equal("team:Dev", post.TargetKey);
        Assert.Equal(new SpendCapReachedPayload("team:Dev", Dev, 5m, AskText), post.Payload);
        CapAsk ask = Assert.Single(rig.Caps.Pending);
        Assert.Equal(new CapAsk("team:Dev", Dev, Dev, null, 5m, 5m), ask);
    }

    /// <summary>Yes grants exactly one more cap: Run inside the Team returns, the Notification resolves and the question comes back after another cap.</summary>
    [Fact]
    public void Grant_AddsOneCap_AndAsksAgainAfterAnother()
    {
        using Rig rig = new();
        _ = rig.Levels.SetTeam(Dev, NeedsYouLevel.RunInsideTheTeam, 5m);
        rig.Spent("Nova", 5m);
        _ = rig.Current;
        int changed = 0;
        rig.Caps.Changed += () => changed++;

        Assert.True(rig.Caps.Grant("team:Dev"));

        Assert.Equal(1, changed);
        Assert.Empty(rig.Caps.Pending);
        Assert.Equal(new FeedResolution("team:Dev", FeedSourceType.SpendCapReached), Assert.Single(rig.Feed.Resolutions));
        Assert.Equal(NeedsYouLevel.RunInsideTheTeam, rig.Current.Level);
        rig.Spent("Leo", 4.99m);
        Assert.Equal(NeedsYouLevel.RunInsideTheTeam, rig.Current.Level);
        Assert.Single(rig.Feed.Posts);
        rig.Spent("Leo", 0.01m);
        Assert.Equal(NeedsYouLevel.AskMe, rig.Current.Level);
        Assert.Equal(2, rig.Feed.Posts.Count);
        Assert.Equal("Dev has spent 10.00 USD running inside the Team, its allowance. Let it spend another 5.00 USD?", Assert.IsType<SpendCapReachedPayload>(rig.Feed.Posts[1].Payload).Message);
    }

    /// <summary>An answer with no question open changes nothing: a second Yes does not hand out a second cap.</summary>
    [Fact]
    public void Grant_NoQuestionOpen_IsRefused()
    {
        using Rig rig = new();
        _ = rig.Levels.SetTeam(Dev, NeedsYouLevel.RunInsideTheTeam, 5m);
        rig.Spent("Nova", 5m);
        _ = rig.Current;

        Assert.True(rig.Caps.Grant("team:Dev"));
        Assert.False(rig.Caps.Grant("team:Dev"));
        Assert.False(rig.Caps.Decline("team:Dev"));
        Assert.False(rig.Caps.Grant("global"));

        rig.Spent("Nova", 5m);
        Assert.Equal(NeedsYouLevel.AskMe, rig.Current.Level);
    }

    /// <summary>No keeps the scope at Ask me, closes the question and does not ask again, until the Human sets the level again.</summary>
    [Fact]
    public void Decline_KeepsAskMeUntilTheHumanSetsTheLevelAgain()
    {
        using Rig rig = new();
        _ = rig.Levels.SetTeam(Dev, NeedsYouLevel.RunInsideTheTeam, 5m);
        rig.Spent("Nova", 5m);
        _ = rig.Current;

        Assert.True(rig.Caps.Decline("team:Dev"));

        Assert.Empty(rig.Caps.Pending);
        Assert.Equal(new FeedResolution("team:Dev", FeedSourceType.SpendCapReached), Assert.Single(rig.Feed.Resolutions));
        rig.Spent("Nova", 20m);
        Assert.True(rig.Current.CapReached);
        Assert.Equal(NeedsYouLevel.AskMe, rig.Current.Level);
        Assert.Single(rig.Feed.Posts);

        _ = rig.Levels.SetTeam(Dev, NeedsYouLevel.RunInsideTheTeam, 5m);

        Assert.Equal(NeedsYouLevel.RunInsideTheTeam, rig.Current.Level);
        rig.Spent("Nova", 5m);
        Assert.Equal(NeedsYouLevel.AskMe, rig.Current.Level);
        Assert.Equal(2, rig.Feed.Posts.Count);
    }

    /// <summary>A Team scope counts only its members' spend, in USD only.</summary>
    [Fact]
    public void Counting_TeamScope_CountsOnlyMembersAndOnlyUsd()
    {
        using Rig rig = new();
        _ = rig.Levels.SetTeam(Dev, NeedsYouLevel.RunInsideTheTeam, 5m);

        rig.Spent("Ivy", 50m);
        rig.Spent("Nova", 500m, "EUR");
        Assert.Equal(NeedsYouLevel.RunInsideTheTeam, rig.Current.Level);

        rig.Spent("Nova", 2m);
        rig.Spent("leo", 2.5m);
        Assert.Equal(NeedsYouLevel.RunInsideTheTeam, rig.Current.Level);
        rig.Spent("Nova", 0.5m);
        Assert.Equal(NeedsYouLevel.AskMe, rig.Current.Level);
    }

    /// <summary>The global scope counts every Persona, whichever Team it is in.</summary>
    [Fact]
    public void Counting_GlobalScope_CountsEveryone()
    {
        using Rig rig = new();
        rig.Levels.SetGlobal(NeedsYouLevel.RunInsideTheTeam, 5m);

        rig.Spent("Ivy", 3m);
        rig.Spent("Nova", 1.5m);
        rig.Spent("Unlisted", 0.4m);
        Assert.Equal(NeedsYouLevel.RunInsideTheTeam, rig.Current.Level);

        rig.Spent("Ivy", 0.1m);
        EffectiveLevel level = rig.Current;

        Assert.Equal(new EffectiveLevel(NeedsYouLevel.AskMe, NeedsYouScope.Global, null, 5m, CapReached: true), level);
        Assert.Equal("Huddle has spent 5.00 USD running inside the Team, its cap. Let it spend another 5.00 USD?", Assert.IsType<SpendCapReachedPayload>(Assert.Single(rig.Feed.Posts).Payload).Message);
    }

    /// <summary>Spend before the level was set is not counted: the count starts at the setter.</summary>
    [Fact]
    public void Counting_BaselineExcludesSpendBeforeTheLevelWasSet()
    {
        using Rig rig = new();
        rig.Spent("Nova", 40m);

        _ = rig.Levels.SetTeam(Dev, NeedsYouLevel.RunInsideTheTeam, 5m);

        Assert.Equal(NeedsYouLevel.RunInsideTheTeam, rig.Current.Level);
        rig.Spent("Nova", 4.99m);
        Assert.Equal(NeedsYouLevel.RunInsideTheTeam, rig.Current.Level);
        rig.Spent("Nova", 0.01m);
        Assert.Equal(NeedsYouLevel.AskMe, rig.Current.Level);
    }

    /// <summary>A cap edited by hand in the file starts a fresh count at the spend of that moment.</summary>
    [Fact]
    public void For_CapEditedByHand_RestartsTheCount()
    {
        using Rig rig = new();
        _ = rig.Levels.SetTeam(Dev, NeedsYouLevel.RunInsideTheTeam, 5m);
        rig.Spent("Nova", 5m);
        Assert.Equal(NeedsYouLevel.AskMe, rig.Current.Level);

        rig.Folders.Team = rig.Folders.Team! with { SpendCap = 8m };

        Assert.Equal(NeedsYouLevel.RunInsideTheTeam, rig.Current.Level);
        Assert.Empty(rig.Caps.Pending);
        Assert.Equal(new FeedResolution("team:Dev", FeedSourceType.SpendCapReached), Assert.Single(rig.Feed.Resolutions));
        rig.Spent("Nova", 8m);
        Assert.Equal(NeedsYouLevel.AskMe, rig.Current.Level);
    }

    /// <summary>A scope read for the first time with a cap and no row starts counting then, not from zero.</summary>
    [Fact]
    public void For_ConfiguredCapFirstRead_StartsCountingThen()
    {
        using Rig rig = new("run-inside-team", 5m);
        rig.Spent("Ivy", 30m);

        Assert.Equal(NeedsYouLevel.RunInsideTheTeam, rig.Current.Level);

        rig.Spent("Ivy", 5m);
        Assert.Equal(NeedsYouLevel.AskMe, rig.Current.Level);
    }

    /// <summary>Setting a scope back to Ask me or to no level ends its count and cancels an open question.</summary>
    [Fact]
    public void SetTeam_AskMe_CancelsTheOpenQuestion()
    {
        using Rig rig = new();
        _ = rig.Levels.SetTeam(Dev, NeedsYouLevel.RunInsideTheTeam, 5m);
        rig.Spent("Nova", 5m);
        _ = rig.Current;
        Assert.Single(rig.Caps.Pending);

        _ = rig.Levels.SetTeam(Dev, NeedsYouLevel.AskMe, 5m);

        Assert.Empty(rig.Caps.Pending);
        Assert.Equal(new FeedResolution("team:Dev", FeedSourceType.SpendCapReached), Assert.Single(rig.Feed.Resolutions));
        Assert.False(rig.Current.CapReached);
        Assert.Equal(NeedsYouLevel.AskMe, rig.Current.Level);
    }

    /// <summary>A Project that sets its own level and cap counts its Team's members and is named with its Team.</summary>
    [Fact]
    public void For_ProjectAtItsCap_AsksNamingTheProject()
    {
        using Rig rig = new();
        Assert.True(rig.Levels.SetProject(Dev, "Site", NeedsYouLevel.RunInsideTheTeam, 2m));

        rig.Spent("Leo", 2m);
        EffectiveLevel project = rig.Levels.For(Dev, "Site");

        Assert.True(project.CapReached);
        Assert.Equal(NeedsYouScope.Project, project.Scope);
        Assert.Equal("Dev / Site has spent 2.00 USD running inside the Team, its cap. Let it spend another 2.00 USD?", Assert.IsType<SpendCapReachedPayload>(Assert.Single(rig.Feed.Posts).Payload).Message);
        CapAsk ask = Assert.Single(rig.Caps.Pending);
        Assert.Equal(("project:Dev/Site", Dev, "Site"), (ask.Scope, ask.Team, ask.Project));
        Assert.Equal(NeedsYouLevel.AskMe, rig.Levels.For(Dev, "Site").Level);
    }

    /// <summary>The watcher opens the question as the spend arrives, without anything reading the level, and stops listening when stopped.</summary>
    [Fact]
    public async Task Watcher_SpendReachesTheCap_OpensTheQuestion()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = new();
        _ = rig.Levels.SetTeam(Dev, NeedsYouLevel.RunInsideTheTeam, 5m);
        PersonaSpend spend = new(NullLogger<PersonaSpend>.Instance, rig.Spend);
        NeedsYouCapWatcher watcher = new(spend, rig.Levels, rig.Catalog);
        await watcher.StartAsync(ct);

        spend.Add("Nova", "session-1", 5m, Usd);

        _ = Assert.Single(rig.Feed.Posts);
        _ = Assert.Single(rig.Caps.Pending);
        await watcher.StopAsync(ct);
        Assert.Equal(0, spend.SubscriberCount);
    }

    /// <summary>Through the Task service and the merge gate: at the cap a ready Task goes to Needs You instead of being released, and a release already written stops carrying a merge.</summary>
    [Fact]
    public void CapReached_TaskServiceAndMergeGateFallBackToAskMe()
    {
        using NeedsYouLevelRig rig = new(globalLevel: "run-inside-team", globalCap: 5m);
        TaskItem first = rig.CreateInReview();
        _ = rig.AddEvidence(first);
        Assert.Single(rig.Releases(first));
        Assert.Null(rig.Gate.RefusalFor("Nova", "git merge feat/x"));

        rig.Spend.Add("Nova", null, Usd, 5m);
        TaskItem second = rig.CreateInReview();
        _ = rig.AddEvidence(second);

        Assert.Equal(TaskState.NeedsYou, rig.Read(second).Status);
        Assert.Empty(rig.Releases(second));
        Assert.Equal(AskMeText, rig.Gate.RefusalFor("Nova", "git merge feat/x"));
        Assert.Single(rig.Feed.Posts, static post => post.SourceType == FeedSourceType.SpendCapReached);
    }

    /// <summary>An in-memory <see cref="ITeamFolders"/> holding one Team file and Project files by name.</summary>
    private sealed class MemoryFolders : ITeamFolders
    {
        public TeamFile? Team { get; set; }

        public Dictionary<string, ProjectFile> Projects { get; } = new(StringComparer.OrdinalIgnoreCase);

        public LibraryResult<LibraryPath> EnsureTeam(string name) => throw new NotSupportedException();

        public LibraryResult<LibraryPath> FindTeam(string name) => throw new NotSupportedException();

        public LibraryResult<LibraryPath> EnsureProjectIn(string team, string project) => throw new NotSupportedException();

        public TeamCreateResult CreateTeam(string team, TeamFile file, bool leaveExistingAlone = false) => throw new NotSupportedException();

        public bool UpdateTeamFile(string team, Func<TeamFile, TeamFile> change)
        {
            if (this.Team is null)
            {
                return false;
            }

            this.Team = change(this.Team);
            return true;
        }

        public TeamFile? ReadTeamFile(string team) => this.Team;

        public ProjectFile? ReadProjectFile(string team, string project) => this.Projects.GetValueOrDefault(project);

        public bool UpdateProjectFile(string team, string project, Func<ProjectFile, ProjectFile> change)
        {
            if (!this.Projects.TryGetValue(project, out ProjectFile? current))
            {
                return false;
            }

            this.Projects[project] = change(current);
            return true;
        }
    }
}
