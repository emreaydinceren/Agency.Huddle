using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp.Sessions;

namespace Agency.Huddle.Tests.Acp.Sessions;

/// <summary>
/// Pins <see cref="RoomSessionStore"/> (RS §6.6): <c>{DataDir}/room-sessions/&lt;Name&gt;.json</c>,
/// keyed by Room id, atomic writes, corruption tolerance, <c>Forget</c>/<c>ForgetAll</c>/<c>Prune</c>,
/// and <c>Rename</c>/<c>Remove</c> (correction items 18-20).
/// </summary>
public sealed class RoomSessionStoreTests
{
    /// <summary>A Room with no stored entry reads as <see langword="null"/>, not an exception.</summary>
    [Fact]
    public void Get_Missing_ReturnsNull()
    {
        using TempDataDir dataDir = new();
        RoomSessionStore store = new(dataDir.Options(), NullLogger<RoomSessionStore>.Instance);

        RoomSessionEntry? entry = store.Get("Nova", "room-porto");

        Assert.Null(entry);
    }

    /// <summary>Every field, including a <see langword="null"/> Model, round-trips through <c>Put</c> then <c>Get</c>.</summary>
    [Fact]
    public void PutThenGet_RoundTrips()
    {
        using TempDataDir dataDir = new();
        RoomSessionStore store = new(dataDir.Options(), NullLogger<RoomSessionStore>.Instance);
        RoomSessionEntry entry = new(
            "5c1e-session",
            "claude",
            null,
            "low",
            "01J8MESSAGE",
            new DateTimeOffset(2026, 9, 22, 16, 2, 11, TimeSpan.Zero));

        store.Put("Nova", "room-porto", entry);
        RoomSessionEntry? loaded = store.Get("Nova", "room-porto");

        Assert.Equal(entry, loaded);
    }

    /// <summary>Putting a second Room's entry leaves the first Room's entry unchanged.</summary>
    [Fact]
    public void Put_SecondRoom_KeepsFirst()
    {
        using TempDataDir dataDir = new();
        RoomSessionStore store = new(dataDir.Options(), NullLogger<RoomSessionStore>.Instance);
        RoomSessionEntry first = new("sess-a", "claude", "opus", null, null, DateTimeOffset.UtcNow);
        RoomSessionEntry second = new("sess-b", "claude", null, null, null, DateTimeOffset.UtcNow);

        store.Put("Nova", "room-a", first);
        store.Put("Nova", "room-b", second);

        Assert.Equal(first, store.Get("Nova", "room-a"));
        Assert.Equal(second, store.Get("Nova", "room-b"));
    }

    /// <summary>A <c>Put</c> writes under <c>{DataDir}/room-sessions/</c>, keyed by the Agent's Name, with camelCase keys and a null Model omitted (RS §6.6; correction item 18: the store's JSON options already ignore nulls when writing).</summary>
    [Fact]
    public void Put_WritesRoomSessionsFolderKeyedByName()
    {
        using TempDataDir dataDir = new();
        RoomSessionStore store = new(dataDir.Options(), NullLogger<RoomSessionStore>.Instance);
        RoomSessionEntry entry = new(
            "5c1e-session",
            "claude",
            null,
            "low",
            "01J8MESSAGE",
            new DateTimeOffset(2026, 9, 22, 16, 2, 11, TimeSpan.Zero));

        store.Put("Nova", "01J8PORTO", entry);

        string path = Path.Combine(dataDir.Path, "room-sessions", "Nova.json");
        Assert.True(File.Exists(path));
        string json = File.ReadAllText(path);
        Assert.Contains("\"rooms\"", json, StringComparison.Ordinal);
        Assert.Contains("\"sessionId\"", json, StringComparison.Ordinal);
        Assert.Contains("\"adapterId\"", json, StringComparison.Ordinal);
        Assert.Contains("\"01J8PORTO\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"model\"", json, StringComparison.Ordinal);
    }

    /// <summary>A file that cannot be parsed logs a Warning and reads as <see langword="null"/>, never throwing.</summary>
    [Fact]
    public void Get_Corrupt_ReturnsNullAndWarns()
    {
        using TempDataDir dataDir = new();
        RoomSessionStore store = new(dataDir.Options(), NullLogger<RoomSessionStore>.Instance);
        string folder = Path.Combine(dataDir.Path, "room-sessions");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "Nova.json"), "{not json");

        RoomSessionEntry? entry = store.Get("Nova", "room-porto");

        Assert.Null(entry);
    }

    /// <summary>A <c>Put</c> never leaves the temporary <c>.tmp</c> file behind.</summary>
    [Fact]
    public void Put_LeavesNoTmp()
    {
        using TempDataDir dataDir = new();
        RoomSessionStore store = new(dataDir.Options(), NullLogger<RoomSessionStore>.Instance);

        store.Put("Nova", "room-porto", new RoomSessionEntry("sess-1", "claude", null, null, null, DateTimeOffset.UtcNow));

        string folder = Path.Combine(dataDir.Path, "room-sessions");
        Assert.Empty(Directory.GetFiles(folder, "*.tmp"));
    }

    /// <summary>A stored Work Mode round-trips through the file.</summary>
    [Fact]
    public void PutThenGet_RoundTripsTheWorkMode()
    {
        using TempDataDir dataDir = new();
        RoomSessionStore store = new(dataDir.Options(), NullLogger<RoomSessionStore>.Instance);

        store.Put("Nova", "room-a", new RoomSessionEntry("sess-a", "claude", null, null, null, DateTimeOffset.UtcNow, "plan"));

        RoomSessionEntry? stored = store.Get("Nova", "room-a");
        Assert.NotNull(stored);
        Assert.Equal("plan", stored.WorkMode);
    }

    /// <summary>A null Work Mode is omitted from the file, so a Persona with none writes the file it always wrote.</summary>
    [Fact]
    public void Put_NullWorkMode_IsNotWritten()
    {
        using TempDataDir dataDir = new();
        RoomSessionStore store = new(dataDir.Options(), NullLogger<RoomSessionStore>.Instance);

        store.Put("Nova", "room-a", new RoomSessionEntry("sess-a", "claude", null, null, null, DateTimeOffset.UtcNow));

        string json = File.ReadAllText(Path.Combine(dataDir.Path, "room-sessions", "Nova.json"));
        Assert.DoesNotContain("workMode", json, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A file written before Work Modes existed has no <c>workMode</c> key. It must still load, with the
    /// mode read as null, so existing entries stay valid for a Persona that has none: no migration.
    /// </summary>
    [Fact]
    public void Get_FileWrittenBeforeWorkModes_LoadsWithANullWorkMode()
    {
        using TempDataDir dataDir = new();
        RoomSessionStore store = new(dataDir.Options(), NullLogger<RoomSessionStore>.Instance);
        store.Put("Nova", "room-a", new RoomSessionEntry("sess-a", "claude", "m1", "e1", "msg-1", DateTimeOffset.UtcNow));
        string path = Path.Combine(dataDir.Path, "room-sessions", "Nova.json");
        Assert.DoesNotContain("workMode", File.ReadAllText(path), StringComparison.OrdinalIgnoreCase);

        RoomSessionEntry? entry = store.Get("Nova", "room-a");

        Assert.NotNull(entry);
        Assert.Equal("sess-a", entry.SessionId);
        Assert.Null(entry.WorkMode);
    }

    /// <summary><c>Forget</c> removes one Room's entry and leaves the others.</summary>
    [Fact]
    public void Forget_RemovesOneRoom()
    {
        using TempDataDir dataDir = new();
        RoomSessionStore store = new(dataDir.Options(), NullLogger<RoomSessionStore>.Instance);
        store.Put("Nova", "room-a", new RoomSessionEntry("sess-a", "claude", null, null, null, DateTimeOffset.UtcNow));
        store.Put("Nova", "room-b", new RoomSessionEntry("sess-b", "claude", null, null, null, DateTimeOffset.UtcNow));

        store.Forget("Nova", "room-a");

        Assert.Null(store.Get("Nova", "room-a"));
        Assert.NotNull(store.Get("Nova", "room-b"));
    }

    /// <summary><c>ForgetAll</c> deletes the Agent's whole file (Restart and Persona edits, RS §6.13).</summary>
    [Fact]
    public void ForgetAll_DeletesTheFile()
    {
        using TempDataDir dataDir = new();
        RoomSessionStore store = new(dataDir.Options(), NullLogger<RoomSessionStore>.Instance);
        store.Put("Nova", "room-a", new RoomSessionEntry("sess-a", "claude", null, null, null, DateTimeOffset.UtcNow));

        store.ForgetAll("Nova");

        Assert.False(File.Exists(Path.Combine(dataDir.Path, "room-sessions", "Nova.json")));
        Assert.Null(store.Get("Nova", "room-a"));
    }

    /// <summary><c>Prune</c> keeps only the entries whose Room id is in the live set, dropping the rest (RS §6.6: "Pruned at start against Welcome.Rooms").</summary>
    [Fact]
    public void Prune_KeepsOnlyLiveRoomIds()
    {
        using TempDataDir dataDir = new();
        RoomSessionStore store = new(dataDir.Options(), NullLogger<RoomSessionStore>.Instance);
        store.Put("Nova", "room-a", new RoomSessionEntry("sess-a", "claude", null, null, null, DateTimeOffset.UtcNow));
        store.Put("Nova", "room-b", new RoomSessionEntry("sess-b", "claude", null, null, null, DateTimeOffset.UtcNow));

        store.Prune("Nova", ["room-a"]);

        Assert.NotNull(store.Get("Nova", "room-a"));
        Assert.Null(store.Get("Nova", "room-b"));
    }

    /// <summary><c>Rename</c> moves the Agent's file to the new Name, only - no cross-file rewrite (correction item 20, unlike <see cref="Agency.Huddle.App.FileChanges.FileStateStore.Rename"/>).</summary>
    [Fact]
    public void Rename_MovesTheFile()
    {
        using TempDataDir dataDir = new();
        RoomSessionStore store = new(dataDir.Options(), NullLogger<RoomSessionStore>.Instance);
        store.Put("Nova", "room-a", new RoomSessionEntry("sess-a", "claude", null, null, null, DateTimeOffset.UtcNow));

        store.Rename("Nova", "Nora");

        Assert.False(File.Exists(Path.Combine(dataDir.Path, "room-sessions", "Nova.json")));
        Assert.True(File.Exists(Path.Combine(dataDir.Path, "room-sessions", "Nora.json")));
        Assert.NotNull(store.Get("Nora", "room-a"));
    }

    /// <summary><c>Remove</c> deletes the Agent's file (Persona removal, RS §6.13).</summary>
    [Fact]
    public void Remove_DeletesTheFile()
    {
        using TempDataDir dataDir = new();
        RoomSessionStore store = new(dataDir.Options(), NullLogger<RoomSessionStore>.Instance);
        store.Put("Nova", "room-a", new RoomSessionEntry("sess-a", "claude", null, null, null, DateTimeOffset.UtcNow));

        store.Remove("Nova");

        Assert.False(File.Exists(Path.Combine(dataDir.Path, "room-sessions", "Nova.json")));
    }

    /// <summary>A file written before Hook State existed has no <c>hookState</c> key. It must still load, with the state read as null, so an existing entry resumes with no migration.</summary>
    [Fact]
    public void Entry_OldFile_LoadsHookStateAsNull()
    {
        using TempDataDir dataDir = new();
        RoomSessionStore store = new(dataDir.Options(), NullLogger<RoomSessionStore>.Instance);
        store.Put("Nova", "room-a", new RoomSessionEntry("sess-a", "claude", "m1", "e1", "msg-1", DateTimeOffset.UtcNow));
        string path = Path.Combine(dataDir.Path, "room-sessions", "Nova.json");
        Assert.DoesNotContain("hookState", File.ReadAllText(path), StringComparison.OrdinalIgnoreCase);

        RoomSessionEntry? entry = store.Get("Nova", "room-a");

        Assert.NotNull(entry);
        Assert.Null(entry.HookState);
    }

    /// <summary>A stored Hook State, keyed by hook name, round-trips through the file.</summary>
    [Fact]
    public void Entry_HookState_RoundTrips()
    {
        using TempDataDir dataDir = new();
        RoomSessionStore store = new(dataDir.Options(), NullLogger<RoomSessionStore>.Instance);
        Dictionary<string, string?> hookState = new(StringComparer.Ordinal) { ["AgentsMd"] = "h1" };
        store.Put("Nova", "room-a", new RoomSessionEntry("sess-a", "claude", null, null, null, DateTimeOffset.UtcNow, HookState: hookState));

        RoomSessionEntry? stored = store.Get("Nova", "room-a");

        Assert.NotNull(stored);
        Assert.NotNull(stored.HookState);
        Assert.Single(stored.HookState);
        Assert.Equal("h1", stored.HookState["AgentsMd"]);
    }

    /// <summary>A hook name the store does not know, such as one whose hook was removed, is carried through the file untouched.</summary>
    [Fact]
    public void Entry_UnknownHookStateKeys_Survive()
    {
        using TempDataDir dataDir = new();
        RoomSessionStore store = new(dataDir.Options(), NullLogger<RoomSessionStore>.Instance);
        Dictionary<string, string?> hookState = new(StringComparer.Ordinal) { ["AgentsMd"] = "h1", ["Retired"] = "r1" };
        store.Put("Nova", "room-a", new RoomSessionEntry("sess-a", "claude", null, null, null, DateTimeOffset.UtcNow, HookState: hookState));

        RoomSessionEntry? stored = store.Get("Nova", "room-a");

        Assert.NotNull(stored);
        Assert.NotNull(stored.HookState);
        Assert.Equal(2, stored.HookState.Count);
        Assert.Equal("h1", stored.HookState["AgentsMd"]);
        Assert.Equal("r1", stored.HookState["Retired"]);
    }

    /// <summary><c>UpdateHookState</c> replaces only the Hook State of an existing entry; every other field keeps its value.</summary>
    [Fact]
    public void UpdateHookState_RewritesOnlyThatField()
    {
        using TempDataDir dataDir = new();
        RoomSessionStore store = new(dataDir.Options(), NullLogger<RoomSessionStore>.Instance);
        DateTimeOffset lastTurn = new(2026, 9, 22, 16, 2, 11, TimeSpan.Zero);
        Dictionary<string, string?> before = new(StringComparer.Ordinal) { ["AgentsMd"] = "h1" };
        store.Put("Nova", "room-a", new RoomSessionEntry("sess-a", "claude", "opus", "low", "msg-1", lastTurn, "plan", 5000L, 200000L, HookState: before));
        Dictionary<string, string?> after = new(StringComparer.Ordinal) { ["AgentsMd"] = "h2" };

        store.UpdateHookState("Nova", "room-a", "sess-a", after);

        RoomSessionEntry? stored = store.Get("Nova", "room-a");
        Assert.NotNull(stored);
        Assert.NotNull(stored.HookState);
        Assert.Equal("h2", stored.HookState["AgentsMd"]);
        Assert.Equal("sess-a", stored.SessionId);
        Assert.Equal("claude", stored.AdapterId);
        Assert.Equal("opus", stored.Model);
        Assert.Equal("low", stored.Effort);
        Assert.Equal("msg-1", stored.LastMessageId);
        Assert.Equal(lastTurn, stored.LastTurnUtc);
        Assert.Equal("plan", stored.WorkMode);
        Assert.Equal(5000L, stored.ContextUsed);
        Assert.Equal(200000L, stored.ContextSize);
    }

    /// <summary><c>UpdateHookState</c> for a Room with no stored entry does nothing: it creates no entry and no file.</summary>
    [Fact]
    public void UpdateHookState_NoEntry_DoesNothing()
    {
        using TempDataDir dataDir = new();
        RoomSessionStore store = new(dataDir.Options(), NullLogger<RoomSessionStore>.Instance);
        Dictionary<string, string?> state = new(StringComparer.Ordinal) { ["AgentsMd"] = "h1" };

        store.UpdateHookState("Nova", "room-a", "sess-a", state);

        Assert.Null(store.Get("Nova", "room-a"));
        Assert.False(File.Exists(Path.Combine(dataDir.Path, "room-sessions", "Nova.json")));
    }

    /// <summary><c>UpdateHookState</c> for an entry of another session does nothing: a fresh session's /compact must not write onto an older session's entry.</summary>
    [Fact]
    public void UpdateHookState_OtherSession_DoesNothing()
    {
        using TempDataDir dataDir = new();
        RoomSessionStore store = new(dataDir.Options(), NullLogger<RoomSessionStore>.Instance);
        Dictionary<string, string?> before = new(StringComparer.Ordinal) { ["AgentsMd"] = "h1" };
        store.Put("Nova", "room-a", new RoomSessionEntry("sess-old", "claude", "opus", "low", "msg-1", DateTimeOffset.UtcNow, HookState: before));
        Dictionary<string, string?> other = new(StringComparer.Ordinal) { ["AgentsMd"] = "h2" };

        store.UpdateHookState("Nova", "room-a", "sess-new", other);

        Assert.Equal("h1", store.Get("Nova", "room-a")?.HookState?["AgentsMd"]);
    }

    /// <summary><c>Rename</c> moves the whole file, so the Hook State travels with the entry under the new Name.</summary>
    [Fact]
    public void Rename_CarriesHookState()
    {
        using TempDataDir dataDir = new();
        RoomSessionStore store = new(dataDir.Options(), NullLogger<RoomSessionStore>.Instance);
        Dictionary<string, string?> hookState = new(StringComparer.Ordinal) { ["AgentsMd"] = "h1" };
        store.Put("Nova", "room-a", new RoomSessionEntry("sess-a", "claude", null, null, null, DateTimeOffset.UtcNow, HookState: hookState));

        store.Rename("Nova", "Nora");

        RoomSessionEntry? stored = store.Get("Nora", "room-a");
        Assert.NotNull(stored);
        Assert.NotNull(stored.HookState);
        Assert.Equal("h1", stored.HookState["AgentsMd"]);
    }

    /// <summary>Two concurrent <c>Put</c> calls, one per Room, both survive: each is a whole read-modify-write under the store's lock, mirroring <c>FileStateStore.Update</c> (finding P-18).</summary>
    [Fact]
    public async Task ConcurrentPuts_TwoRooms_BothSurvive()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        RoomSessionStore store = new(dataDir.Options(), NullLogger<RoomSessionStore>.Instance);

        const int iterations = 50;

        Task first = Task.Run(
            () =>
            {
                for (int i = 0; i < iterations; i++)
                {
                    store.Put("Nova", "room-a", new RoomSessionEntry("sess-a", "claude", null, null, null, DateTimeOffset.UtcNow));
                }
            },
            cancellationToken);

        Task second = Task.Run(
            () =>
            {
                for (int i = 0; i < iterations; i++)
                {
                    store.Put("Nova", "room-b", new RoomSessionEntry("sess-b", "claude", null, null, null, DateTimeOffset.UtcNow));
                }
            },
            cancellationToken);

        await Task.WhenAll(first, second);

        Assert.NotNull(store.Get("Nova", "room-a"));
        Assert.NotNull(store.Get("Nova", "room-b"));
    }
}
