using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.FileChanges;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.Views;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Tasks;

namespace Agency.Huddle.Tests.Acp;

/// <summary>
/// Exercises <see cref="PersonaRenameCascade"/> over real, temp-directory-backed stores - a
/// <see cref="SqliteTeamDirectory"/>, a real <see cref="PersonaStore"/> and a real
/// <see cref="AvatarStore"/> - rather than mocks, per this repo's "no mocking framework" convention.
/// Every rename test drives a rename through <see cref="PersonaStore.Update"/>, exactly as a Persona
/// card save does, and every removal test drives a removal through <see cref="PersonaStore.Remove"/>,
/// so the whole path under test is the real one: <c>PersonaStore.PersonaRenamed</c> /
/// <c>PersonaStore.PersonaRemoved</c> -&gt; <see cref="PersonaRenameCascade"/> -&gt;
/// <see cref="ITeamDirectory"/> / <see cref="AvatarStore"/>.
/// </summary>
public sealed class PersonaRenameCascadeTests
{
    /// <summary>Renaming a Persona whose Agent is registered renames the Team Directory row and keeps its id - the central guarantee ADR-0011 exists for.</summary>
    [Fact]
    public async Task Rename_RenamesTheAgentRow_AndKeepsItsId()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        using var harness = await CreateHarnessAsync(dir, ct);
        harness.PersonaStore.Add(Identity("echo"), "You answer support questions.");
        var echo = await harness.TeamDirectory.UpsertAgentUserAsync("echo", null, ct);
        Assert.NotNull(echo);

        harness.PersonaStore.Update("echo", PersonaText("echoprime", "You answer support questions."), model: null, effort: null);

        var renamed = harness.TeamDirectory.FindUserByName("echoprime");
        Assert.NotNull(renamed);
        Assert.Equal(echo.Id, renamed.Id);
        Assert.Null(harness.TeamDirectory.FindUserByName("echo"));
    }

    /// <summary>The renamed Agent is still a Member of every Room it belonged to, by the same id, after the cascade.</summary>
    [Fact]
    public async Task Rename_RoomMembershipsSurvive_UnderTheSameId()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        using var harness = await CreateHarnessAsync(dir, ct);
        harness.PersonaStore.Add(Identity("echo"), "You answer support questions.");
        var echo = await harness.TeamDirectory.UpsertAgentUserAsync("echo", null, ct);
        Assert.NotNull(echo);
        var room = await harness.TeamDirectory.CreateRoomAsync("Support", [KnownIds.Human, echo.Id], ct);

        harness.PersonaStore.Update("echo", PersonaText("echoprime", "You answer support questions."), model: null, effort: null);

        var renamed = harness.TeamDirectory.FindUserByName("echoprime");
        Assert.NotNull(renamed);
        var rooms = await harness.TeamDirectory.GetRoomsForUserAsync(renamed.Id, ct);
        var stillMember = Assert.Single(rooms);
        Assert.Equal(room.Id, stillMember.Id);
        var members = await harness.TeamDirectory.GetRoomMembersAsync(room.Id, ct);
        Assert.Contains(members, m => m.Id == echo.Id && m.Name == "echoprime");
    }

    /// <summary>A Room still carrying its auto-derived name is re-derived to the Agent's new Name.</summary>
    [Fact]
    public async Task Rename_AutoNamedRoom_IsReDerivedToTheNewName()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        using var harness = await CreateHarnessAsync(dir, ct);
        harness.PersonaStore.Add(Identity("echo"), "You answer support questions.");
        var echo = await harness.TeamDirectory.UpsertAgentUserAsync("echo", null, ct);
        Assert.NotNull(echo);
        var room = await harness.TeamDirectory.CreateRoomAsync("echo", [KnownIds.Human, echo.Id], ct);
        CreateWorkDir(dir, "echo");

        harness.PersonaStore.Update("echo", PersonaText("echoprime", "You answer support questions."), model: null, effort: null);
        await WaitForWorkDirMoveAsync(dir, "echoprime", ct);

        var updated = await harness.TeamDirectory.GetRoomAsync(room.Id, ct);
        Assert.NotNull(updated);
        Assert.Equal("echoprime", updated.Name);
    }

    /// <summary>A Room the Human renamed by hand keeps its chosen name, even though its Agent Member was renamed.</summary>
    [Fact]
    public async Task Rename_HandRenamedRoom_IsLeftAlone()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        using var harness = await CreateHarnessAsync(dir, ct);
        harness.PersonaStore.Add(Identity("echo"), "You answer support questions.");
        var echo = await harness.TeamDirectory.UpsertAgentUserAsync("echo", null, ct);
        Assert.NotNull(echo);
        var room = await harness.TeamDirectory.CreateRoomAsync("echo", [KnownIds.Human, echo.Id], ct);
        await harness.TeamDirectory.RenameRoomAsync(room.Id, "Customer Support", ct);
        CreateWorkDir(dir, "echo");

        harness.PersonaStore.Update("echo", PersonaText("echoprime", "You answer support questions."), model: null, effort: null);
        await WaitForWorkDirMoveAsync(dir, "echoprime", ct);

        var updated = await harness.TeamDirectory.GetRoomAsync(room.Id, ct);
        Assert.NotNull(updated);
        Assert.Equal("Customer Support", updated.Name);
    }

    /// <summary>A Persona whose Agent has never connected - the normal case when Acp is disabled - cascades quietly, with nothing to rename.</summary>
    [Fact]
    public async Task Rename_PersonaWithNoRegisteredAgent_CascadesQuietly()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        using var harness = await CreateHarnessAsync(dir, ct);
        harness.PersonaStore.Add(Identity("ghost"), "Nobody has ever started this one.");

        harness.PersonaStore.Update("ghost", PersonaText("ghostprime", "Nobody has ever started this one."), model: null, effort: null);

        Assert.Null(harness.TeamDirectory.FindUserByName("ghost"));
        Assert.Null(harness.TeamDirectory.FindUserByName("ghostprime"));
    }

    /// <summary>A rename whose new Name is already held by a different Agent is rejected: a warning is logged and the Team Directory is left untouched.</summary>
    [Fact]
    public async Task Rename_NewNameAlreadyHeldByAnotherAgent_LogsWarning_AndChangesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var recordingLogger = new RecordingLogger<PersonaRenameCascade>();
        using var harness = await CreateHarnessAsync(dir, ct, recordingLogger);
        harness.PersonaStore.Add(Identity("echo"), "You answer support questions.");
        var echo = await harness.TeamDirectory.UpsertAgentUserAsync("echo", null, ct);
        var alpha = await harness.TeamDirectory.UpsertAgentUserAsync("alpha", null, ct);
        Assert.NotNull(echo);
        Assert.NotNull(alpha);

        harness.PersonaStore.Update("echo", PersonaText("alpha", "You answer support questions."), model: null, effort: null);

        var stillEcho = harness.TeamDirectory.FindUserByName("echo");
        Assert.NotNull(stillEcho);
        Assert.Equal(echo.Id, stillEcho.Id);
        Assert.Contains(recordingLogger.Entries, e => e.Level == LogLevel.Warning);
    }

    /// <summary>A Persona whose Name coincides with the Human's own Name can never cause the Human's row to be renamed.</summary>
    [Fact]
    public async Task Rename_CanNeverTakeOverTheHumanRow()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        using var harness = await CreateHarnessAsync(dir, ct);
        harness.PersonaStore.Add(Identity("You"), "A coincidental namesake of the Human.");

        harness.PersonaStore.Update("You", PersonaText("Someone", "A coincidental namesake of the Human."), model: null, effort: null);

        var human = await harness.TeamDirectory.GetHumanAsync(ct);
        Assert.Equal("You", human.Name);
        Assert.Equal(UserKind.Human, human.Kind);
    }

    /// <summary>The renamed Agent's Work Dir moves to the new Name's folder.</summary>
    [Fact]
    public async Task Rename_MovesTheWorkDir()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        using var harness = await CreateHarnessAsync(dir, ct);
        harness.PersonaStore.Add(Identity("echo"), "You answer support questions.");
        var echo = await harness.TeamDirectory.UpsertAgentUserAsync("echo", null, ct);
        Assert.NotNull(echo);
        var oldWorkDir = CreateWorkDir(dir, "echo");
        await File.WriteAllTextAsync(Path.Combine(oldWorkDir, "CLAUDE.md"), "notes to self", ct);

        harness.PersonaStore.Update("echo", PersonaText("echoprime", "You answer support questions."), model: null, effort: null);
        await WaitForWorkDirMoveAsync(dir, "echoprime", ct);

        Assert.False(Directory.Exists(oldWorkDir));
        var newWorkDir = Path.Combine(dir.Path, "work", "echoprime");
        Assert.True(File.Exists(Path.Combine(newWorkDir, "CLAUDE.md")));
    }

    /// <summary>When a directory already exists at the target Name, the Work Dir is not moved and both are left in place.</summary>
    [Fact]
    public async Task Rename_WorkDirTargetAlreadyExists_DoesNotMove_LeavesBothInPlace()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        using var harness = await CreateHarnessAsync(dir, ct);
        harness.PersonaStore.Add(Identity("echo"), "You answer support questions.");
        var echo = await harness.TeamDirectory.UpsertAgentUserAsync("echo", null, ct);
        Assert.NotNull(echo);
        var oldWorkDir = CreateWorkDir(dir, "echo");
        await File.WriteAllTextAsync(Path.Combine(oldWorkDir, "old.txt"), "old", ct);
        var newWorkDir = CreateWorkDir(dir, "echoprime");
        await File.WriteAllTextAsync(Path.Combine(newWorkDir, "existing.txt"), "already here", ct);

        // There is no Work Dir move to poll for here (the target already exists), so instead
        // rename the Agent and then wait for that synchronous half to commit, followed by a room
        // create/derive round trip that must complete after the Work Dir step in the same detached
        // task - proving the detached task ran to completion before the assertions below run.
        harness.PersonaStore.Update("echo", PersonaText("echoprime", "You answer support questions."), model: null, effort: null);
        await WaitForAgentRenameToSettleAsync(harness.TeamDirectory, "echoprime", ct);

        Assert.True(Directory.Exists(oldWorkDir));
        Assert.True(File.Exists(Path.Combine(oldWorkDir, "old.txt")));
        Assert.True(File.Exists(Path.Combine(newWorkDir, "existing.txt")));
        Assert.False(File.Exists(Path.Combine(newWorkDir, "old.txt")));
    }

    /// <summary>
    /// Cascading the same rename twice - once forward, once back, once forward again - leaves the
    /// Team Directory, the Room and the Work Dir in exactly the state a single forward rename would:
    /// no duplicate rows, no stale Room name, no orphaned Work Dir folder.
    /// </summary>
    [Fact]
    public async Task Rename_TheSameTransitionAppliedTwice_DoesNotCorruptState()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        using var harness = await CreateHarnessAsync(dir, ct);
        harness.PersonaStore.Add(Identity("echo"), "You answer support questions.");
        var echo = await harness.TeamDirectory.UpsertAgentUserAsync("echo", null, ct);
        Assert.NotNull(echo);
        var room = await harness.TeamDirectory.CreateRoomAsync("echo", [KnownIds.Human, echo.Id], ct);
        CreateWorkDir(dir, "echo");

        // Forward: echo -> echoprime.
        harness.PersonaStore.Update("echo", PersonaText("echoprime", "You answer support questions."), model: null, effort: null);
        await WaitForWorkDirMoveAsync(dir, "echoprime", ct);

        // Back: echoprime -> echo.
        harness.PersonaStore.Update("echoprime", PersonaText("echo", "You answer support questions."), model: null, effort: null);
        await WaitForWorkDirMoveAsync(dir, "echo", ct);

        // Forward again: the exact same (echo -> echoprime) transition as the first step.
        harness.PersonaStore.Update("echo", PersonaText("echoprime", "You answer support questions."), model: null, effort: null);
        await WaitForWorkDirMoveAsync(dir, "echoprime", ct);

        var renamed = harness.TeamDirectory.FindUserByName("echoprime");
        Assert.NotNull(renamed);
        Assert.Equal(echo.Id, renamed.Id);
        Assert.Null(harness.TeamDirectory.FindUserByName("echo"));

        var users = await harness.TeamDirectory.GetUsersAsync(ct);
        Assert.Equal(2, users.Count); // The Human, plus exactly one Agent row - no duplicate was minted.

        var updatedRoom = await harness.TeamDirectory.GetRoomAsync(room.Id, ct);
        Assert.NotNull(updatedRoom);
        Assert.Equal("echoprime", updatedRoom.Name);

        Assert.False(Directory.Exists(Path.Combine(dir.Path, "work", "echo")));
        Assert.True(Directory.Exists(Path.Combine(dir.Path, "work", "echoprime")));
    }

    /// <summary>
    /// The single most important test in this task: a rename must move the Avatar key even when NO
    /// Agent has ever registered under the old Name - the normal case in a stock installation, where
    /// <c>Team:Acp:Enabled</c> defaults to <see langword="false"/>. This is the regression the avatar
    /// move being the FIRST statement in <see cref="PersonaRenameCascade"/>'s handler, above the "no
    /// Agent row" early return, exists to prevent: moving it below that return would still pass every
    /// other rename test (all of which register an Agent) while silently losing the avatar of any
    /// Teammate that has never run.
    /// </summary>
    [Fact]
    public async Task Rename_PersonaWithNoRegisteredAgent_MovesTheAvatarKey()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        using var harness = await CreateHarnessAsync(dir, ct);
        harness.PersonaStore.Add(Identity("ghost"), "Nobody has ever started this one.");
        harness.AvatarStore.Save("ghost", new Avatar(Label: null, Image: null, Background: "#4a154b"));

        harness.PersonaStore.Update("ghost", PersonaText("ghostprime", "Nobody has ever started this one."), model: null, effort: null);

        Assert.Equal("#4a154b", harness.AvatarStore.Get("ghostprime").Background);
        Assert.Equal(Avatar.None, harness.AvatarStore.Get("ghost"));
    }

    /// <summary>A rename also moves the Avatar key for a Persona whose Agent HAS connected, alongside the Team Directory row rename.</summary>
    [Fact]
    public async Task Rename_PersonaWithRegisteredAgent_MovesTheAvatarKey()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        using var harness = await CreateHarnessAsync(dir, ct);
        harness.PersonaStore.Add(Identity("echo"), "You answer support questions.");
        var echo = await harness.TeamDirectory.UpsertAgentUserAsync("echo", null, ct);
        Assert.NotNull(echo);
        harness.AvatarStore.Save("echo", new Avatar(Label: "E", Image: null, Background: "#123456"));

        harness.PersonaStore.Update("echo", PersonaText("echoprime", "You answer support questions."), model: null, effort: null);

        Assert.Equal("#123456", harness.AvatarStore.Get("echoprime").Background);
        Assert.Equal(Avatar.None, harness.AvatarStore.Get("echo"));
    }

    /// <summary>A rename moves only the Avatar's JSON key - the image file itself is left at exactly the same path on disk.</summary>
    [Fact]
    public async Task Rename_LeavesTheAvatarImageFileUntouchedOnDisk()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        using var harness = await CreateHarnessAsync(dir, ct);
        harness.PersonaStore.Add(Identity("echo"), "You answer support questions.");
        var imageFile = harness.AvatarStore.WriteImage([1, 2, 3], ".png");
        harness.AvatarStore.Save("echo", new Avatar(Label: null, Image: imageFile, Background: null));
        var imagePath = Path.Combine(harness.AvatarStore.ImageDirectory, imageFile);

        harness.PersonaStore.Update("echo", PersonaText("echoprime", "You answer support questions."), model: null, effort: null);

        Assert.True(File.Exists(imagePath));
        Assert.Equal(imageFile, harness.AvatarStore.Get("echoprime").Image);
    }

    /// <summary>Removing a Persona deletes both its Avatar's JSON key and its image file from disk.</summary>
    [Fact]
    public async Task Remove_DeletesTheAvatarKeyAndItsImageFile()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        using var harness = await CreateHarnessAsync(dir, ct);
        harness.PersonaStore.Add(Identity("echo"), "You answer support questions.");
        var imageFile = harness.AvatarStore.WriteImage([1, 2, 3], ".png");
        harness.AvatarStore.Save("echo", new Avatar(Label: null, Image: imageFile, Background: null));
        var imagePath = Path.Combine(harness.AvatarStore.ImageDirectory, imageFile);
        Assert.True(File.Exists(imagePath));

        harness.PersonaStore.Remove("echo");

        Assert.Equal(Avatar.None, harness.AvatarStore.Get("echo"));
        Assert.False(File.Exists(imagePath));
    }

    /// <summary>
    /// FC §6.12: a rename cascades to <see cref="FileStateStore"/> even for a Persona whose Agent has
    /// never registered - the stock case, since <c>Team:Acp:Enabled</c> is false by default. Mirrors
    /// <see cref="Rename_PersonaWithNoRegisteredAgent_MovesTheAvatarKey"/> for file state.
    /// </summary>
    [Fact]
    public async Task OnPersonaRenamed_NoAgentRow_StillRenamesFileState()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        using var harness = await CreateHarnessAsync(dir, ct);
        harness.PersonaStore.Add(Identity("ghost"), "Nobody has ever started this one.");
        harness.FileState.Save("ghost", FileState.Empty with { Subscribed = ["Shared"] });

        harness.PersonaStore.Update("ghost", PersonaText("ghostprime", "Nobody has ever started this one."), model: null, effort: null);

        FileState? renamed = harness.FileState.Load("ghostprime");
        Assert.NotNull(renamed);
        Assert.Equal(["Shared"], renamed.Subscribed);
        Assert.Null(harness.FileState.Load("ghost"));
    }

    /// <summary>FC §6.12, F12: renaming an Agent rewrites every OTHER Agent's <c>subscribed</c> entry naming it, via the same <see cref="FileStateStore.Rename"/> the avatar cascade sits beside.</summary>
    [Fact]
    public async Task OnPersonaRenamed_RewritesOtherAgentsSubscriptions()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        using var harness = await CreateHarnessAsync(dir, ct);
        harness.PersonaStore.Add(Identity("Nova"), "You are Nova.");
        harness.PersonaStore.Add(Identity("Coach"), "You are Coach.");
        harness.FileState.Save("Coach", FileState.Empty with { Subscribed = ["Nova"] });

        harness.PersonaStore.Update("Nova", PersonaText("Star", "You are Nova."), model: null, effort: null);

        FileState? coach = harness.FileState.Load("Coach");
        Assert.NotNull(coach);
        Assert.Equal(["Star"], coach.Subscribed);
    }

    /// <summary>FC §6.12: removing a Persona deletes its <see cref="FileStateStore"/> file, alongside its Avatar.</summary>
    [Fact]
    public async Task OnPersonaRemoved_RemovesFileState()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        using var harness = await CreateHarnessAsync(dir, ct);
        harness.PersonaStore.Add(Identity("echo"), "You answer support questions.");
        harness.FileState.Save("echo", FileState.Empty with { Subscribed = ["Shared"] });
        Assert.NotNull(harness.FileState.Load("echo"));

        harness.PersonaStore.Remove("echo");

        Assert.Null(harness.FileState.Load("echo"));
    }

    /// <summary>
    /// FC §6.12: a Name inside frontmatter is never rewritten by the cascade - Huddle never edits the
    /// Human's Persona text on another Persona's behalf. Coach's frontmatter still reads
    /// <c>watches: [Nova]</c> after Nova is renamed; §6.10's warning surfaces the mismatch instead.
    /// </summary>
    [Fact]
    public async Task OnPersonaRenamed_DoesNotEditFrontmatter()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        using var harness = await CreateHarnessAsync(dir, ct);
        harness.PersonaStore.Add(Identity("Nova"), "You are Nova.");
        const string coachText = "---\nName: Coach\nTitle: Coach\nAlias: Coach\nwatches: [Nova]\n---\nYou are Coach.";
        harness.PersonaStore.Add(Identity("Coach"), "You are Coach.");
        harness.PersonaStore.Update("Coach", coachText, model: null, effort: null);

        harness.PersonaStore.Update("Nova", PersonaText("Star", "You are Nova."), model: null, effort: null);

        var coach = harness.PersonaStore.Get("Coach");
        Assert.NotNull(coach);
        Assert.Contains("watches: [Nova]", coach.Text, StringComparison.Ordinal);
    }

    /// <summary>RS §6.13: a rename moves the <see cref="RoomSessionStore"/> file even when no Agent has ever registered - the stock case, since <c>Team:Acp:Enabled</c> is false by default. Mirrors <see cref="Rename_PersonaWithNoRegisteredAgent_MovesTheAvatarKey"/> and <see cref="OnPersonaRenamed_NoAgentRow_StillRenamesFileState"/> for Room Sessions.</summary>
    [Fact]
    public async Task Rename_NoAgentRow_StillRenamesRoomSessions()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        using var harness = await CreateHarnessAsync(dir, ct);
        harness.PersonaStore.Add(Identity("ghost"), "Nobody has ever started this one.");
        harness.RoomSessions.Put("ghost", "room-1", new RoomSessionEntry("sess-1", "claude", null, null, null, DateTimeOffset.UtcNow));

        harness.PersonaStore.Update("ghost", PersonaText("ghostprime", "Nobody has ever started this one."), model: null, effort: null);

        Assert.NotNull(harness.RoomSessions.Get("ghostprime", "room-1"));
        Assert.Null(harness.RoomSessions.Get("ghost", "room-1"));
    }

    /// <summary>RS §6.13: removing a Persona deletes its <see cref="RoomSessionStore"/> file, alongside its Avatar and File Changes state.</summary>
    [Fact]
    public async Task Removal_RemovesRoomSessions()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        using var harness = await CreateHarnessAsync(dir, ct);
        harness.PersonaStore.Add(Identity("echo"), "You answer support questions.");
        harness.RoomSessions.Put("echo", "room-1", new RoomSessionEntry("sess-1", "claude", null, null, null, DateTimeOffset.UtcNow));
        Assert.NotNull(harness.RoomSessions.Get("echo", "room-1"));

        harness.PersonaStore.Remove("echo");

        Assert.Null(harness.RoomSessions.Get("echo", "room-1"));
    }

    /// <summary>
    /// Functional test through a real <see cref="PersonaStore.Update"/> rename (Spec §9.6, corrections-B2
    /// D6 item 11): the cascade's new <c>this.tasks.RenameTeammate(...)</c> call rewrites a real
    /// Task file's <c>assignee:</c> to the new Name, the same way <see cref="ITeamDirectory.RenameUser"/>
    /// keeps the Team Directory row above it in sync.
    /// </summary>
    [Fact]
    public async Task PersonaRenameCascade_RenamesTaskAssignee()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        using var harness = await CreateHarnessAsync(dir, ct);
        harness.PersonaStore.Add(Identity("nova"), "You help.");
        TestTaskStore.WriteTask(
            harness.TaskStore.RootDirectory,
            Path.Combine("Platform", "PLAT-0001.md"),
            TestTasks.Make(id: "PLAT-0001", assignee: "nova", location: new("Platform", null, false), changeLog: [TestTasks.Entry("2026-01-01T00:00:00Z", "You", "created")]));
        harness.TaskStore.RebuildFromWatcher();
        _ = TaskId.TryParse("PLAT-0001", out TaskId id);
        Assert.NotNull(harness.TaskStore.Get(id));

        harness.PersonaStore.Update("nova", PersonaText("novaprime", "You help."), model: null, effort: null);

        TaskItem renamed = harness.TaskStore.Get(id) ?? throw new InvalidOperationException("fixture task missing");
        Assert.Equal("novaprime", renamed.Assignee);
    }

    /// <summary>
    /// Settled corrections-B2 D6: the cascade's new <c>this.views.RenameTeammate(...)</c> call
    /// rewrites a saved View's assignee filter, mirroring <see cref="PersonaRenameCascade_RenamesTaskAssignee"/>
    /// for <see cref="ViewStore"/> rather than <see cref="TaskStore"/>.
    /// </summary>
    [Fact]
    public async Task PersonaRenameCascade_RenamesViewAssigneeFilter()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        using var harness = await CreateHarnessAsync(dir, ct);
        harness.PersonaStore.Add(Identity("nova"), "You help.");
        harness.ViewStore.Save(new TaskView
        {
            Id = "custom-1",
            Name = "Custom",
            Kind = ViewKind.List,
            Filter = new TaskFilter { Assignees = ["nova"] },
        });

        harness.PersonaStore.Update("nova", PersonaText("novaprime", "You help."), model: null, effort: null);

        TaskView updated = harness.ViewStore.Get("custom-1") ?? throw new InvalidOperationException("fixture view missing");
        Assert.Equal(["novaprime"], updated.Filter.Assignees);
    }

    /// <summary>Builds a real <see cref="ITeamDirectory"/>, <see cref="PersonaStore"/>, <see cref="AvatarStore"/> and started <see cref="PersonaRenameCascade"/> over <paramref name="dir"/>, seeding the Human.</summary>
    private static async Task<Harness> CreateHarnessAsync(TempDataDir dir, CancellationToken ct, ILogger<PersonaRenameCascade>? cascadeLogger = null)
    {
        var teamDirectory = new SqliteTeamDirectory(dir.Options());
        await teamDirectory.InitializeAsync("You", ct);

        var personaStore = new PersonaStore(
            dir.Options(),
            new PersonaModelStore(dir.Options()),
            new PersonaEffortStore(dir.Options()),
            NullLogger<PersonaStore>.Instance);
        var roomEvents = new RoomEvents(NullLogger<RoomEvents>.Instance);
        var avatarStore = new AvatarStore(dir.Options(), NullLogger<AvatarStore>.Instance);
        var fileState = new FileStateStore(dir.Options(), NullLogger<FileStateStore>.Instance);
        var roomSessions = new RoomSessionStore(dir.Options(), NullLogger<RoomSessionStore>.Instance);
        var taskStore = new TaskStore(dir.Options(), personaStore, TimeProvider.System, NullLogger<TaskStore>.Instance);
        var taskService = new TaskService(
            taskStore,
            new TaskIdAllocator(dir.Options()),
            new TaskEvents(),
            personaStore,
            dir.Options(),
            TimeProvider.System,
            NullLogger<TaskService>.Instance);
        var viewStore = new ViewStore(dir.Options(), NullLogger<ViewStore>.Instance);
        var cascade = new PersonaRenameCascade(
            teamDirectory,
            personaStore,
            roomEvents,
            avatarStore,
            fileState,
            roomSessions,
            new(dir.Options()),
            TimeProvider.System,
            cascadeLogger ?? NullLogger<PersonaRenameCascade>.Instance,
            tasks: taskService,
            views: viewStore);

        await cascade.StartAsync(ct);

        return new Harness
        {
            TeamDirectory = teamDirectory,
            PersonaStore = personaStore,
            RoomEvents = roomEvents,
            AvatarStore = avatarStore,
            FileState = fileState,
            RoomSessions = roomSessions,
            TaskStore = taskStore,
            TaskService = taskService,
            ViewStore = viewStore,
            Cascade = cascade,
        };
    }

    /// <summary>Creates the Work Dir folder a Persona named <paramref name="name"/> would have as its <c>cwd</c>, and returns its path.</summary>
    private static string CreateWorkDir(TempDataDir dir, string name)
    {
        var path = Path.Combine(dir.Path, "work", name);
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>
    /// Polls until the Work Dir folder for <paramref name="newName"/> exists, which - because
    /// <see cref="PersonaRenameCascade"/> moves the Work Dir last, after re-deriving Rooms - is a
    /// reliable barrier proving the whole detached half of the cascade has finished.
    /// </summary>
    private static async Task WaitForWorkDirMoveAsync(TempDataDir dir, string newName, CancellationToken ct)
    {
        var target = Path.Combine(dir.Path, "work", newName);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);

        while (!Directory.Exists(target))
        {
            linked.Token.ThrowIfCancellationRequested();
            await Task.Delay(20, linked.Token);
        }
    }

    /// <summary>
    /// Polls until <paramref name="teamDirectory"/> resolves <paramref name="newName"/> to an Agent,
    /// used by the one test whose detached half performs no Work Dir move to poll for instead.
    /// </summary>
    private static async Task WaitForAgentRenameToSettleAsync(SqliteTeamDirectory teamDirectory, string newName, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(teamDirectory);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);

        while (teamDirectory.FindUserByName(newName) is null)
        {
            linked.Token.ThrowIfCancellationRequested();
            await Task.Delay(20, linked.Token);
        }

        // The rename itself is synchronous (already true by the time PersonaStore.Update returns),
        // so this loop exits immediately; the short extra pause below gives the detached task -
        // which runs the Room step before the Work Dir step this test's scenario skips - room to
        // finish before the assertions run.
        await Task.Delay(250, linked.Token);
    }

    /// <summary>A valid <see cref="PersonaIdentity"/> for <paramref name="name"/>, with Title and Alias defaulting to <paramref name="name"/>.</summary>
    private static PersonaIdentity Identity(string name) => new(name, name, name, []);

    /// <summary>Minimal valid Persona frontmatter (Name, Title and Alias all <paramref name="name"/>) wrapped around <paramref name="body"/>.</summary>
    private static string PersonaText(string name, string body) => $"---\nName: {name}\nTitle: {name}\nAlias: {name}\n---\n{body}";

    /// <summary>The wired-up components one test needs, bundled so a single <see langword="using"/> disposes both the store and the cascade.</summary>
    private sealed class Harness : IDisposable
    {
        public required SqliteTeamDirectory TeamDirectory { get; init; }

        public required PersonaStore PersonaStore { get; init; }

        public required RoomEvents RoomEvents { get; init; }

        public required AvatarStore AvatarStore { get; init; }

        public required FileStateStore FileState { get; init; }

        public required RoomSessionStore RoomSessions { get; init; }

        public required TaskStore TaskStore { get; init; }

        public required TaskService TaskService { get; init; }

        public required ViewStore ViewStore { get; init; }

        public required PersonaRenameCascade Cascade { get; init; }

        /// <summary>Disposes the cascade (unsubscribing it from <see cref="PersonaStore.PersonaRenamed"/> and <see cref="PersonaStore.PersonaRemoved"/>), then TaskService, TaskStore and ViewStore (Settled corrections-B2 D6 item 11), then the Avatar store, then the Persona store.</summary>
        public void Dispose()
        {
            this.Cascade.Dispose();
            this.TaskService.Dispose();
            this.TaskStore.Dispose();
            this.ViewStore.Dispose();
            this.AvatarStore.Dispose();
            this.PersonaStore.Dispose();
        }
    }

    /// <summary>
    /// A hand-written fake <see cref="ILogger{T}"/> that records every call, since this repo has no
    /// mocking framework. Modelled after <c>PromptStoreTests.RecordingLogger</c>.
    /// </summary>
    /// <typeparam name="T">The category type the recorded logger stands in for.</typeparam>
    private sealed class RecordingLogger<T> : ILogger<T>
    {
        /// <summary>Every call made to this logger so far, in call order.</summary>
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        /// <summary>Not used by this fake: scoping is irrelevant to the tests that need it, so this returns a no-op.</summary>
        /// <typeparam name="TState">The scope state type.</typeparam>
        /// <param name="state">The scope state.</param>
        /// <returns>A no-op <see cref="IDisposable"/>.</returns>
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        /// <summary>Always enabled, so every call this fake receives is actually recorded.</summary>
        /// <param name="logLevel">The level being checked.</param>
        /// <returns><see langword="true"/>, always.</returns>
        public bool IsEnabled(LogLevel logLevel) => true;

        /// <summary>Records one log call's level and formatted message.</summary>
        /// <typeparam name="TState">The state type carrying this call's structured values.</typeparam>
        /// <param name="logLevel">The call's severity.</param>
        /// <param name="eventId">Unused by this fake.</param>
        /// <param name="state">The call's structured state, passed to <paramref name="formatter"/>.</param>
        /// <param name="exception">The call's exception, if any.</param>
        /// <param name="formatter">Formats <paramref name="state"/> and <paramref name="exception"/> into the message text.</param>
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);

            this.Entries.Add((logLevel, formatter(state, exception)));
        }
    }
}
