using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Time.Testing;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Agents;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Feed;
using Agency.Huddle.Tests.Factories;
using Agency.Huddle.Tests.Feed;

namespace Agency.Huddle.Tests.Acp;

/// <summary>
/// Pins the account-connectors store and its one writer, <c>PersonaStore.SetConnectors</c>
/// (docs/Huddle.AgentConnectors-Specifications.md T-1, T-7, T-11, T-16): off by default, a row means on, only <c>SetConnectors</c> writes
/// it, and it goes when the Agent is removed or renamed.
/// </summary>
public sealed class PersonaConnectorsTests
{
    private static readonly DateTimeOffset Noon = new(2026, 11, 6, 12, 0, 0, TimeSpan.Zero);

    /// <summary>With no row, a Persona reads as off and has no date.</summary>
    [Fact]
    public void Store_NoRow_ReadsOff()
    {
        using TempDataDir dir = new();
        PersonaConnectorsStore store = new(dir.Options());

        Assert.False(store.IsOn("nova"));
        Assert.Null(store.EnabledAt("nova"));
    }

    /// <summary>Switching on writes the moment from the clock, and switching off deletes the row.</summary>
    [Fact]
    public void Store_SetOnThenOff_WritesEnabledAtThenDeletes()
    {
        using TempDataDir dir = new();
        FakeTimeProvider clock = new(Noon);
        PersonaConnectorsStore store = new(dir.Options(), clock);

        store.Set("nova", true);

        Assert.True(store.IsOn("nova"));
        Assert.Equal(Noon, store.EnabledAt("nova"));

        store.Set("nova", false);

        Assert.False(store.IsOn("nova"));
        Assert.Null(store.EnabledAt("nova"));
    }

    /// <summary>Names match without regard to case, so a renamed-by-case Agent keeps one row.</summary>
    [Fact]
    public void Store_KeysAreCaseInsensitive()
    {
        using TempDataDir dir = new();
        PersonaConnectorsStore store = new(dir.Options());

        store.Set("Nova", true);

        Assert.True(store.IsOn("NOVA"));
    }

    /// <summary>A database from before this feature has no table; opening the store creates it.</summary>
    [Fact]
    public void Store_DatabaseWithoutTheTable_GetsOne()
    {
        using TempDataDir dir = new();
        _ = dir.CreateDirectory();

        PersonaConnectorsStore store = new(dir.Options());
        store.Set("nova", true);

        using SqliteConnection connection = new($"Data Source={Path.Combine(dir.Options().Value.DataDir, "team.db")};Pooling=False");
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT count(*) FROM persona_connectors;";
        Assert.Equal(1L, command.ExecuteScalar());
    }

    /// <summary>A new Agent is off, and <c>SetConnectors</c> raises <c>PersonasChanged</c> once so the host restarts.</summary>
    [Fact]
    public void SetConnectors_Flip_RaisesPersonasChangedOnceAndReadsOn()
    {
        using TempDataDir dir = new();
        using PersonaStore store = dir.CreatePersonaStore();
        _ = store.Add(TestPersonas.Identity("nova"), "You are Nova.");
        Assert.False(store.Get("nova")?.Connectors);
        int raised = 0;
        store.PersonasChanged += () => raised++;

        store.SetConnectors("nova", true);

        Assert.Equal(1, raised);
        Assert.True(store.Get("nova")?.Connectors);
        Assert.NotNull(store.ConnectorsEnabledAt("nova"));
    }

    /// <summary><c>Update</c>, which every caller except the card passes the Agent's own values to, neither reads nor clears the row.</summary>
    [Fact]
    public void Update_LeavesTheConnectorsRowAlone()
    {
        using TempDataDir dir = new();
        using PersonaStore store = dir.CreatePersonaStore();
        Persona added = store.Add(TestPersonas.Identity("nova"), "You are Nova.");
        store.SetConnectors("nova", true);

        _ = store.Update("nova", added.Text, model: null, effort: null, workMode: null);

        Assert.True(store.Get("nova")?.Connectors);
    }

    /// <summary>Removing an Agent deletes its row, so an Agent created later with the same name starts off.</summary>
    [Fact]
    public void Remove_DeletesTheRow_AndARecreatedAgentIsOff()
    {
        using TempDataDir dir = new();
        using PersonaStore store = dir.CreatePersonaStore();
        _ = store.Add(TestPersonas.Identity("nova"), "You are Nova.");
        store.SetConnectors("nova", true);

        store.Remove("nova");
        _ = store.Add(TestPersonas.Identity("nova"), "You are Nova.");

        Assert.False(store.Get("nova")?.Connectors);
    }

    /// <summary>F5: a stale row under a name (left by a file deleted outside Remove) does not make a new Agent of that name start on.</summary>
    [Fact]
    public void Add_ClearsAStaleRow()
    {
        using TempDataDir dir = new();
        using PersonaStore store = dir.CreatePersonaStore();
        new PersonaConnectorsStore(dir.Options()).Set("nova", true);

        _ = store.Add(TestPersonas.Identity("nova"), "You are Nova.");

        Assert.False(store.Get("nova")?.Connectors);
        Assert.False(new PersonaConnectorsStore(dir.Options()).IsOn("nova"));
    }

    /// <summary>F5: a stale row under the name an Agent is renamed to is cleared, so the renamed Agent starts off.</summary>
    [Fact]
    public void Update_RenameTarget_ClearsAStaleRow()
    {
        using TempDataDir dir = new();
        using PersonaStore store = dir.CreatePersonaStore();
        Persona added = store.Add(TestPersonas.Identity("nova"), "You are Nova.");
        new PersonaConnectorsStore(dir.Options()).Set("nova2", true);
        string renamed = added.Text.Replace("name: 'nova'", "name: 'nova2'", StringComparison.Ordinal);

        _ = store.Update("nova", renamed, model: null, effort: null, workMode: null);

        Assert.False(store.Get("nova2")?.Connectors);
        Assert.False(new PersonaConnectorsStore(dir.Options()).IsOn("nova2"));
    }

    /// <summary>F5: a Persona file that disappears and is seen by a rescan takes its row with it, so a re-created Agent starts off.</summary>
    [Fact]
    public void Rescan_FileGone_DropsTheRow()
    {
        using TempDataDir dir = new();
        using PersonaStore store = dir.CreatePersonaStore();
        _ = store.Add(TestPersonas.Identity("nova"), "You are Nova.");
        store.SetConnectors("nova", true);
        string path = Assert.Single(store.Entries).Path;

        File.Delete(path);
        store.RescanNow();

        Assert.False(new PersonaConnectorsStore(dir.Options()).IsOn("nova"));
    }

    /// <summary>A rename drops access: the old row goes and nothing is written for the new name (OQ-4).</summary>
    [Fact]
    public void Update_Rename_DropsTheRow()
    {
        using TempDataDir dir = new();
        using PersonaStore store = dir.CreatePersonaStore();
        Persona added = store.Add(TestPersonas.Identity("nova"), "You are Nova.");
        store.SetConnectors("nova", true);
        string renamed = added.Text.Replace("name: 'nova'", "name: 'nova2'", StringComparison.Ordinal);
        Assert.NotEqual(added.Text, renamed);

        _ = store.Update("nova", renamed, model: null, effort: null, workMode: null);

        Assert.False(store.Get("nova2")?.Connectors);
        Assert.False(new PersonaConnectorsStore(dir.Options()).IsOn("nova"));
    }

    /// <summary>Resetting the Chief of Staff to its default clears the connectors row, so it returns to off (spec section 5).</summary>
    [Fact]
    public void BuiltinAgentReset_ClearsTheConnectorsRow()
    {
        using TempDataDir dir = new();
        using PersonaStore store = dir.CreatePersonaStore();
        _ = store.Add(TestPersonas.Identity("Chief"), "You are the Chief.");
        store.SetConnectors("Chief", true);

        _ = new BuiltinAgentReset(store).ResetToDefault("Chief");

        Assert.False(store.Get("Chief")?.Connectors);
    }

    /// <summary>A frontmatter key <c>connectors: true</c> in an Agent's file grants nothing (T-11).</summary>
    [Fact]
    public void Frontmatter_ConnectorsKey_IsIgnored()
    {
        using TempDataDir dir = new();
        using PersonaStore store = dir.CreatePersonaStore();
        Persona added = store.Add(TestPersonas.Identity("nova"), "You are Nova.");
        int firstBreak = added.Text.IndexOf('\n', StringComparison.Ordinal);
        string forged = added.Text[..(firstBreak + 1)] + "connectors: true\n" + added.Text[(firstBreak + 1)..];
        File.WriteAllText(store.PathFor("nova"), forged);

        store.RescanNow();

        Assert.NotNull(store.Get("nova"));
        Assert.False(store.Get("nova")?.Connectors);
    }

    /// <summary>Turning on records one Feed item; the same value again records none (T-16).</summary>
    [Fact]
    public void SetConnectors_PublishesOneFeedItem_AndTheSameValueAgainNone()
    {
        using TempDataDir dir = new();
        RecordingFeedPublisher feed = new();
        using PersonaStore store = dir.Options().CreatePersonaStore(feed);
        _ = store.Add(TestPersonas.Identity("nova"), "You are Nova.");

        store.SetConnectors("nova", true);
        store.SetConnectors("nova", true);

        FeedMessage message = Assert.Single(feed.Posts);
        Assert.Equal(FeedSourceType.AgentConnectorsChanged, message.SourceType);
        Assert.Equal(new AgentConnectorsPayload("nova", true), message.Payload);
        Assert.False(message.Notify);
    }

    /// <summary>Turning off records a second item, with <c>On</c> false, because a deleted row has no date to show it.</summary>
    [Fact]
    public void SetConnectors_TurnOff_PublishesAnOffItem()
    {
        using TempDataDir dir = new();
        RecordingFeedPublisher feed = new();
        using PersonaStore store = dir.Options().CreatePersonaStore(feed);
        _ = store.Add(TestPersonas.Identity("nova"), "You are Nova.");

        store.SetConnectors("nova", true);
        store.SetConnectors("nova", false);

        Assert.Equal(2, feed.Posts.Count);
        Assert.Equal(new AgentConnectorsPayload("nova", false), feed.Posts[1].Payload);
    }
}
