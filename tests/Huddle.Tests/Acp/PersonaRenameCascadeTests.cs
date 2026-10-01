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
using Agency.Huddle.Tests.Acp.Fakes;
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

        harness.PersonaStore.Update("echo", PersonaText("echoprime", "You answer support questions."), model: null, effort: null, workMode: null);

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

        harness.PersonaStore.Update("echo", PersonaText("echoprime", "You answer support questions."), model: null, effort: null, workMode: null);

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

        harness.PersonaStore.Update("echo", PersonaText("echoprime", "You answer support questions."), model: null, effort: null, workMode: null);
        await harness.FolderMoves.WhenSettledAsync("echoprime", ct);

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

        harness.PersonaStore.Update("echo", PersonaText("echoprime", "You answer support questions."), model: null, effort: null, workMode: null);
        await harness.FolderMoves.WhenSettledAsync("echoprime", ct);

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

        harness.PersonaStore.Update("ghost", PersonaText("ghostprime", "Nobody has ever started this one."), model: null, effort: null, workMode: null);

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

        harness.PersonaStore.Update("echo", PersonaText("alpha", "You answer support questions."), model: null, effort: null, workMode: null);

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

        harness.PersonaStore.Update("You", PersonaText("Someone", "A coincidental namesake of the Human."), model: null, effort: null, workMode: null);

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

        harness.PersonaStore.Update("echo", PersonaText("echoprime", "You answer support questions."), model: null, effort: null, workMode: null);
        await harness.FolderMoves.WhenSettledAsync("echoprime", ct);

        Assert.False(Directory.Exists(oldWorkDir));
        var newWorkDir = new TeammatePaths(dir.Options()).WorkDir("echoprime");
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
        harness.PersonaStore.Update("echo", PersonaText("echoprime", "You answer support questions."), model: null, effort: null, workMode: null);
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
        harness.PersonaStore.Update("echo", PersonaText("echoprime", "You answer support questions."), model: null, effort: null, workMode: null);
        await harness.FolderMoves.WhenSettledAsync("echoprime", ct);

        // Back: echoprime -> echo.
        harness.PersonaStore.Update("echoprime", PersonaText("echo", "You answer support questions."), model: null, effort: null, workMode: null);
        await harness.FolderMoves.WhenSettledAsync("echo", ct);

        // Forward again: the exact same (echo -> echoprime) transition as the first step.
        harness.PersonaStore.Update("echo", PersonaText("echoprime", "You answer support questions."), model: null, effort: null, workMode: null);
        await harness.FolderMoves.WhenSettledAsync("echoprime", ct);

        var renamed = harness.TeamDirectory.FindUserByName("echoprime");
        Assert.NotNull(renamed);
        Assert.Equal(echo.Id, renamed.Id);
        Assert.Null(harness.TeamDirectory.FindUserByName("echo"));

        var users = await harness.TeamDirectory.GetUsersAsync(ct);
        Assert.Equal(2, users.Count); // The Human, plus exactly one Agent row - no duplicate was minted.

        var updatedRoom = await harness.TeamDirectory.GetRoomAsync(room.Id, ct);
        Assert.NotNull(updatedRoom);
        Assert.Equal("echoprime", updatedRoom.Name);

        Assert.False(Directory.Exists(new TeammatePaths(dir.Options()).WorkDir("echo")));
        Assert.True(Directory.Exists(new TeammatePaths(dir.Options()).WorkDir("echoprime")));
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

        harness.PersonaStore.Update("ghost", PersonaText("ghostprime", "Nobody has ever started this one."), model: null, effort: null, workMode: null);

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

        harness.PersonaStore.Update("echo", PersonaText("echoprime", "You answer support questions."), model: null, effort: null, workMode: null);

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

        harness.PersonaStore.Update("echo", PersonaText("echoprime", "You answer support questions."), model: null, effort: null, workMode: null);

        Assert.True(File.Exists(imagePath));
        Assert.Equal(imageFile, harness.AvatarStore.Get("echoprime").Image);
    }

    /// <summary>
    /// A rename moves a Persona's Spend even when NO Agent has ever registered under the old Name:
    /// the stock case, since <c>Team:Acp:Enabled</c> is false by default. Pins the placement of the
    /// Spend move above the "no Agent row" early return, exactly as
    /// <see cref="Rename_PersonaWithNoRegisteredAgent_MovesTheAvatarKey"/> does for the Avatar.
    /// </summary>
    [Fact]
    public async Task Rename_MovesSpend_EvenWhenNoAgentRowExists()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        using var harness = await CreateHarnessAsync(dir, ct);
        harness.PersonaStore.Add(Identity("ghost"), "Nobody has ever started this one.");
        harness.Spend.Add("ghost", "s1", 0.25m, "USD");

        harness.PersonaStore.Update("ghost", PersonaText("ghostprime", "Nobody has ever started this one."), model: null, effort: null, workMode: null);

        Assert.Equal([new SpendAmount(0.25m, "USD")], harness.Spend.Get("ghostprime"));
        Assert.Empty(harness.Spend.Get("ghost"));
    }

    /// <summary>
    /// A rename moves a Persona's offered Adapter commands even when no Agent has registered under the
    /// old Name, for the same reason as Spend: the move sits above the "no Agent row" early return.
    /// </summary>
    [Fact]
    public async Task Rename_MovesOfferedCommands_EvenWhenNoAgentRowExists()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        using var harness = await CreateHarnessAsync(dir, ct);
        harness.PersonaStore.Add(Identity("ghost"), "Nobody has ever started this one.");
        var compact = new AdapterCommand("compact", "Free up context", null);
        harness.Commands.Set("ghost", [compact]);

        harness.PersonaStore.Update("ghost", PersonaText("ghostprime", "Nobody has ever started this one."), model: null, effort: null, workMode: null);

        Assert.Equal([compact], harness.Commands.Get("ghostprime"));
        Assert.Empty(harness.Commands.Get("ghost"));
    }

    /// <summary>Removing a Persona forgets its offered commands, so a new Persona that takes the Name starts with none.</summary>
    [Fact]
    public async Task Removed_ForgetsOfferedCommands()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        using var harness = await CreateHarnessAsync(dir, ct);
        harness.PersonaStore.Add(Identity("echo"), "You answer support questions.");
        harness.Commands.Set("echo", [new AdapterCommand("compact", "Free up context", null)]);

        harness.PersonaStore.Remove("echo");

        Assert.Empty(harness.Commands.Get("echo"));
    }

    /// <summary>Removing a Persona forgets its Spend, so a new Persona that takes the Name starts from zero.</summary>
    [Fact]
    public async Task Removed_ForgetsSpend()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        using var harness = await CreateHarnessAsync(dir, ct);
        harness.PersonaStore.Add(Identity("echo"), "You answer support questions.");
        harness.Spend.Add("echo", "s1", 0.25m, "USD");

        harness.PersonaStore.Remove("echo");

        Assert.Empty(harness.Spend.Get("echo"));
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

        harness.PersonaStore.Update("ghost", PersonaText("ghostprime", "Nobody has ever started this one."), model: null, effort: null, workMode: null);

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

        harness.PersonaStore.Update("Nova", PersonaText("Star", "You are Nova."), model: null, effort: null, workMode: null);

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
        harness.PersonaStore.Update("Coach", coachText, model: null, effort: null, workMode: null);

        harness.PersonaStore.Update("Nova", PersonaText("Star", "You are Nova."), model: null, effort: null, workMode: null);

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

        harness.PersonaStore.Update("ghost", PersonaText("ghostprime", "Nobody has ever started this one."), model: null, effort: null, workMode: null);

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

        // Seeded through TaskStore.Create, not a raw file write: a raw write is an "outside edit", which
        // TaskService.OnOutsideEdit answers by appending a Change log entry - and when the store's own
        // debounced watcher (not the test thread) is the one that finds it, that append can land between
        // RenameTeammate's read and its write, fail the version check and skip the rename.
        TaskItem seed = TestTasks.Make(id: "PLAT-0001", assignee: "nova", location: new("Platform", null, false), changeLog: [TestTasks.Entry("2026-01-01T00:00:00Z", "You", "created")]);
        TaskItem toWrite = seed with { Path = TaskLayout.PathFor(harness.TaskStore.RootDirectory, seed.Location, seed.Id) };
        TaskItem created = harness.TaskStore.Create(toWrite, TaskFileFormat.Compose(toWrite))
            ?? throw new InvalidOperationException("fixture task already exists");
        TaskId id = created.Id;
        Assert.NotNull(harness.TaskStore.Get(id));

        harness.PersonaStore.Update("nova", PersonaText("novaprime", "You help."), model: null, effort: null, workMode: null);

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

        harness.PersonaStore.Update("nova", PersonaText("novaprime", "You help."), model: null, effort: null, workMode: null);

        TaskView updated = harness.ViewStore.Get("custom-1") ?? throw new InvalidOperationException("fixture view missing");
        Assert.Equal(["novaprime"], updated.Filter.Assignees);
    }

    /// <summary>Spec §6.15: a rename moves the whole Teammate folder to the new Name and renames the definition inside it, so the moved Work Dir's contents survive under the new folder.</summary>
    [Fact]
    public async Task Rename_MovesTeammateFolderAndRenamesDefinition()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        using var harness = await CreateHarnessAsync(dir, ct);
        harness.PersonaStore.Add(Identity("Old"), "You help.");
        var paths = new TeammatePaths(dir.Options());
        Directory.CreateDirectory(Path.Combine(paths.WorkDir("Old"), "memory"));
        await File.WriteAllTextAsync(Path.Combine(paths.WorkDir("Old"), "memory", "x.md"), "notes", ct);

        harness.PersonaStore.Update("Old", PersonaText("New", "You help."), model: null, effort: null, workMode: null);
        await harness.FolderMoves.WhenSettledAsync("New", ct);

        Assert.True(File.Exists(paths.DefinitionFile("New")));
        Assert.True(File.Exists(Path.Combine(paths.WorkDir("New"), "memory", "x.md")));
        Assert.False(Directory.Exists(paths.TeammateFolder("Old")));
    }

    /// <summary>corrections-B2 item 21: the definition is moved exactly once - the moved folder holds only the new definition file, never a leftover <c>Old.md</c> alongside it.</summary>
    [Fact]
    public async Task Rename_MovesTheDefinitionFileExactlyOnce()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        using var harness = await CreateHarnessAsync(dir, ct);
        harness.PersonaStore.Add(Identity("Old"), "You help.");
        var paths = new TeammatePaths(dir.Options());

        harness.PersonaStore.Update("Old", PersonaText("New", "You help."), model: null, effort: null, workMode: null);
        await harness.FolderMoves.WhenSettledAsync("New", ct);

        var topLevelFiles = Directory.GetFiles(paths.TeammateFolder("New")).Select(Path.GetFileName).ToList();
        Assert.Equal(["New.md"], topLevelFiles);
    }

    /// <summary>When the target Teammate folder already exists, nothing moves: both folders and their definitions are left exactly as they were, and the existing *target exists* warning is logged.</summary>
    [Fact]
    public async Task Rename_TargetFolderExists_LeavesBoth()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var recordingLogger = new RecordingLogger<PersonaRenameCascade>();
        using var harness = await CreateHarnessAsync(dir, ct, recordingLogger);
        harness.PersonaStore.Add(Identity("Old"), "You help.");
        var echo = await harness.TeamDirectory.UpsertAgentUserAsync("Old", null, ct);
        Assert.NotNull(echo);
        var paths = new TeammatePaths(dir.Options());
        Directory.CreateDirectory(paths.TeammateFolder("New"));

        harness.PersonaStore.Update("Old", PersonaText("New", "You help."), model: null, effort: null, workMode: null);
        await WaitForAgentRenameToSettleAsync(harness.TeamDirectory, "New", ct);

        Assert.True(Directory.Exists(paths.TeammateFolder("Old")));
        Assert.True(File.Exists(paths.DefinitionFile("Old")));
        Assert.Contains(recordingLogger.Entries, e => e.Level == LogLevel.Warning);
    }

    /// <summary>When the old Teammate folder is held open by another process, the cascade gives up after several retries and leaves both folders in place, logging the *after several attempts* warning. Windows-only: mandatory file locking via <see cref="FileShare.None"/> is not enforced on Linux.</summary>
    [Fact]
    public async Task Rename_FolderHeld_GivesUpAfterRetries()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Linux does not mandatory-lock files opened with FileShare.None");
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var recordingLogger = new RecordingLogger<PersonaRenameCascade>();
        var timeProvider = new FiringTimeProvider();
        using var harness = await CreateHarnessAsync(dir, ct, recordingLogger, timeProvider);
        harness.PersonaStore.Add(Identity("Old"), "You help.");
        var echo = await harness.TeamDirectory.UpsertAgentUserAsync("Old", null, ct);
        Assert.NotNull(echo);
        var paths = new TeammatePaths(dir.Options());
        Directory.CreateDirectory(paths.WorkDir("Old"));
        var heldFile = Path.Combine(paths.TeammateFolder("Old"), "held.txt");
        await File.WriteAllTextAsync(heldFile, "locked", ct);
        using var lockHandle = new FileStream(heldFile, FileMode.Open, FileAccess.Read, FileShare.None);

        harness.PersonaStore.Update("Old", PersonaText("New", "You help."), model: null, effort: null, workMode: null);
        await AdvanceUntilAsync(timeProvider, () => recordingLogger.Entries.Exists(e => e.Level == LogLevel.Warning), TimeSpan.FromMilliseconds(250), ct);

        Assert.True(Directory.Exists(paths.TeammateFolder("Old")));
        Assert.False(Directory.Exists(paths.TeammateFolder("New")));
    }

    /// <summary>corrections-B2 item 18: the Teammate-folder move sits ABOVE the "no Agent row" early return, so it still runs for a Persona whose Agent has never registered - the normal case since <c>Acp:Enabled</c> defaults to <see langword="false"/>.</summary>
    [Fact]
    public async Task Rename_NoAgentRow_StillMovesTeammateFolder()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        using var harness = await CreateHarnessAsync(dir, ct);
        harness.PersonaStore.Add(Identity("ghost"), "Nobody has ever started this one.");
        var paths = new TeammatePaths(dir.Options());

        harness.PersonaStore.Update("ghost", PersonaText("ghostprime", "Nobody has ever started this one."), model: null, effort: null, workMode: null);
        await harness.FolderMoves.WhenSettledAsync("ghostprime", ct);

        Assert.True(Directory.Exists(paths.TeammateFolder("ghostprime")));
        Assert.False(Directory.Exists(paths.TeammateFolder("ghost")));
    }

    /// <summary>corrections-B2 item 22: a case-only rename (<c>Nova</c> -&gt; <c>NOVA</c>) still moves the folder - on a case-insensitive file system the target must be routed through a temporary sibling name rather than mistaken for an existing collision. Runs on both operating systems.</summary>
    [Fact]
    public async Task Rename_CaseOnlyChange_MovesTheFolder()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        using var harness = await CreateHarnessAsync(dir, ct);
        harness.PersonaStore.Add(Identity("Nova"), "You are Nova.");
        var paths = new TeammatePaths(dir.Options());
        Directory.CreateDirectory(Path.Combine(paths.WorkDir("Nova"), "memory"));

        harness.PersonaStore.Update("Nova", PersonaText("NOVA", "You are Nova."), model: null, effort: null, workMode: null);
        await WaitForExactCasingAsync(paths.DefinitionsRoot, "NOVA", ct);

        Assert.True(File.Exists(paths.DefinitionFile("NOVA")));
        Assert.True(Directory.Exists(Path.Combine(paths.WorkDir("NOVA"), "memory")));
        var entries = Directory.GetDirectories(paths.DefinitionsRoot).Select(Path.GetFileName).ToList();
        Assert.Equal(["NOVA"], entries);
    }

    /// <summary>corrections-B2 item 20: a host starting mid-move awaits <see cref="TeammateFolderMoves.WhenSettledAsync"/> rather than racing the folder move - the wait is still pending immediately after the rename while the old folder is held open, and completes, with the folder already at its new location, once the move finally succeeds. Gate-driven via <see cref="FiringTimeProvider"/> and a held file handle, never a sleep. Windows-only: mandatory file locking via <see cref="FileShare.None"/> is not enforced on Linux.</summary>
    [Fact]
    public async Task Rename_HostStartsDuringMove_WaitsThenUsesMovedFolder()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Linux does not mandatory-lock files opened with FileShare.None");
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var timeProvider = new FiringTimeProvider();
        using var harness = await CreateHarnessAsync(dir, ct, timeProvider: timeProvider);
        harness.PersonaStore.Add(Identity("Old"), "You help.");
        var echo = await harness.TeamDirectory.UpsertAgentUserAsync("Old", null, ct);
        Assert.NotNull(echo);
        var paths = new TeammatePaths(dir.Options());
        Directory.CreateDirectory(paths.WorkDir("Old"));
        var heldFile = Path.Combine(paths.TeammateFolder("Old"), "held.txt");
        await File.WriteAllTextAsync(heldFile, "locked", ct);
        using var lockHandle = new FileStream(heldFile, FileMode.Open, FileAccess.Read, FileShare.None);

        harness.PersonaStore.Update("Old", PersonaText("New", "You help."), model: null, effort: null, workMode: null);
        var settled = harness.FolderMoves.WhenSettledAsync("New", ct);
        Assert.False(settled.IsCompleted);

        lockHandle.Dispose();
        await AdvanceUntilAsync(timeProvider, () => settled.IsCompleted, TimeSpan.FromMilliseconds(250), ct);
        await settled;

        Assert.True(Directory.Exists(paths.TeammateFolder("New")));
    }

    /// <summary>Builds a real <see cref="ITeamDirectory"/>, <see cref="PersonaStore"/>, <see cref="AvatarStore"/> and started <see cref="PersonaRenameCascade"/> over <paramref name="dir"/>, seeding the Human.</summary>
    /// <param name="dir">The temp <c>DataDir</c> every store in the harness is built over.</param>
    /// <param name="ct">Cancels the Human seed and Team Directory initialisation.</param>
    /// <param name="cascadeLogger">A logger to observe the cascade's warnings, or <see langword="null"/> for a no-op logger.</param>
    /// <param name="timeProvider">Drives the Teammate folder move's retry backoff, or <see langword="null"/> for <see cref="TimeProvider.System"/>.</param>
    private static async Task<Harness> CreateHarnessAsync(TempDataDir dir, CancellationToken ct, ILogger<PersonaRenameCascade>? cascadeLogger = null, TimeProvider? timeProvider = null)
    {
        var teamDirectory = new SqliteTeamDirectory(dir.Options());
        await teamDirectory.InitializeAsync("You", ct);

        var personaStore = new PersonaStore(
            new TeammatePaths(dir.Options()),
            new PersonaModelStore(dir.Options()),
            new PersonaEffortStore(dir.Options()), new PersonaWorkModeStore(dir.Options()),
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
        var folderMoves = new TeammateFolderMoves();
        var spend = new PersonaSpend(NullLogger<PersonaSpend>.Instance);
        var commands = new PersonaCommands(NullLogger<PersonaCommands>.Instance);
        var cascade = new PersonaRenameCascade(
            teamDirectory,
            personaStore,
            roomEvents,
            avatarStore,
            fileState,
            roomSessions,
            new(dir.Options()),
            timeProvider ?? TimeProvider.System,
            cascadeLogger ?? NullLogger<PersonaRenameCascade>.Instance,
            tasks: taskService,
            views: viewStore,
            folderMoves: folderMoves,
            spend: spend,
            commands: commands);

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
            FolderMoves = folderMoves,
            Spend = spend,
            Commands = commands,
        };
    }

    /// <summary>Creates the Work Dir folder a Persona named <paramref name="name"/> would have as its <c>cwd</c>, and returns its path.</summary>
    private static string CreateWorkDir(TempDataDir dir, string name)
    {
        var path = new TeammatePaths(dir.Options()).WorkDir(name);
        Directory.CreateDirectory(path);
        return path;
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

    /// <summary>
    /// Repeatedly advances <paramref name="timeProvider"/> by <paramref name="step"/>, firing every
    /// due retry-backoff timer, until <paramref name="condition"/> is satisfied - the gate-driven
    /// replacement for a real wall-clock sleep when a test must drive a
    /// <see cref="PersonaRenameCascade"/> retry loop to completion under a <see cref="FiringTimeProvider"/>.
    /// </summary>
    private static async Task AdvanceUntilAsync(FiringTimeProvider timeProvider, Func<bool> condition, TimeSpan step, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(condition);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);

        while (!condition())
        {
            timeProvider.Advance(step);
            linked.Token.ThrowIfCancellationRequested();
            await Task.Delay(20, linked.Token);
        }
    }

    /// <summary>
    /// Polls until <paramref name="parentDir"/> holds a sub-directory whose ON-DISK name is exactly
    /// <paramref name="expectedName"/> (ordinal, case-sensitive comparison) - unlike
    /// <see cref="Directory.Exists"/>, which a case-only rename target would already satisfy on a
    /// case-insensitive file system before the rename has actually happened.
    /// </summary>
    private static async Task WaitForExactCasingAsync(string parentDir, string expectedName, CancellationToken ct)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);

        while (!Directory.Exists(parentDir) || !Directory.GetDirectories(parentDir).Select(Path.GetFileName).Contains(expectedName, StringComparer.Ordinal))
        {
            linked.Token.ThrowIfCancellationRequested();
            await Task.Delay(20, linked.Token);
        }
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

        /// <summary>The display-only <see cref="PersonaSpend"/> table the cascade renames and forgets entries in.</summary>
        public required PersonaSpend Spend { get; init; }

        /// <summary>The <see cref="PersonaCommands"/> table the cascade renames and forgets entries in.</summary>
        public required PersonaCommands Commands { get; init; }

        /// <summary>The <see cref="TeammateFolderMoves"/> gate the cascade signals around a Teammate folder move, so a test can await <see cref="TeammateFolderMoves.WhenSettledAsync"/> the same way a host starting mid-rename would.</summary>
        public required TeammateFolderMoves FolderMoves { get; init; }

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
