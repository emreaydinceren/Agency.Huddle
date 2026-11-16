using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Agents;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Feed;
using Agency.Huddle.Tests.Factories;
using Agency.Huddle.Tests.Feed;

namespace Agency.Huddle.Tests.Acp;

/// <summary>
/// Pins the browser switch store and its one writer, <c>PersonaStore.SetBrowser</c> (issue #551): on by default, a row means the
/// Human switched it off, the row moves with a renamed Agent and goes when the Agent is removed.
/// </summary>
public sealed class PersonaBrowserTests
{
    /// <summary>A new Agent with no row has its browser on.</summary>
    [Fact]
    public void Get_NoRow_BrowserOn()
    {
        using TempDataDir dir = new();
        using PersonaStore store = dir.CreatePersonaStore();

        _ = store.Add(TestPersonas.Identity("nova"), "You are Nova.");

        Assert.False(store.Get("nova")?.BrowserOff);
        Assert.False(new PersonaBrowserStore(dir.Options()).IsOff("nova"));
    }

    /// <summary>Switching off writes the row and raises <c>PersonasChanged</c> once, so the host restarts.</summary>
    [Fact]
    public void SetBrowser_Off_WritesRowAndRaisesChanged()
    {
        using TempDataDir dir = new();
        using PersonaStore store = dir.CreatePersonaStore();
        _ = store.Add(TestPersonas.Identity("nova"), "You are Nova.");
        int raised = 0;
        store.PersonasChanged += () => raised++;

        store.SetBrowser("nova", false);

        Assert.Equal(1, raised);
        Assert.True(store.Get("nova")?.BrowserOff);
        Assert.True(new PersonaBrowserStore(dir.Options()).IsOff("nova"));

        store.SetBrowser("nova", true);

        Assert.Equal(2, raised);
        Assert.False(store.Get("nova")?.BrowserOff);
        Assert.False(new PersonaBrowserStore(dir.Options()).IsOff("nova"));
    }

    /// <summary>Writing the value the Agent already has raises nothing and posts nothing.</summary>
    [Fact]
    public void SetBrowser_SameValue_NoOp()
    {
        using TempDataDir dir = new();
        RecordingFeedPublisher feed = new();
        using PersonaStore store = dir.Options().CreatePersonaStore(feed);
        _ = store.Add(TestPersonas.Identity("nova"), "You are Nova.");
        int raised = 0;
        store.PersonasChanged += () => raised++;

        store.SetBrowser("nova", true);
        store.SetBrowser("nova", false);
        store.SetBrowser("nova", false);

        Assert.Equal(1, raised);
        _ = Assert.Single(feed.Posts);
    }

    /// <summary>Each change in either direction records one Feed item with the new value.</summary>
    [Fact]
    public void SetBrowser_PostsFeedItem()
    {
        using TempDataDir dir = new();
        RecordingFeedPublisher feed = new();
        using PersonaStore store = dir.Options().CreatePersonaStore(feed);
        _ = store.Add(TestPersonas.Identity("nova"), "You are Nova.");

        store.SetBrowser("nova", false);
        store.SetBrowser("nova", true);

        Assert.Equal(2, feed.Posts.Count);
        Assert.Equal(FeedSourceType.AgentBrowserChanged, feed.Posts[0].SourceType);
        Assert.Equal(new AgentBrowserPayload("nova", false), feed.Posts[0].Payload);
        Assert.Equal(new AgentBrowserPayload("nova", true), feed.Posts[1].Payload);
        Assert.False(feed.Posts[0].Notify);
    }

    /// <summary>A rename moves the row with the Agent: off stays off under the new name and nothing is left under the old one.</summary>
    [Fact]
    public void Rename_Off_StaysOff()
    {
        using TempDataDir dir = new();
        using PersonaStore store = dir.CreatePersonaStore();
        Persona added = store.Add(TestPersonas.Identity("nova"), "You are Nova.");
        store.SetBrowser("nova", false);
        string renamed = added.Text.Replace("name: 'nova'", "name: 'nova2'", StringComparison.Ordinal);
        Assert.NotEqual(added.Text, renamed);

        _ = store.Update("nova", renamed, model: null, effort: null, workMode: null);

        Assert.True(store.Get("nova2")?.BrowserOff);
        PersonaBrowserStore rows = new(dir.Options());
        Assert.True(rows.IsOff("nova2"));
        Assert.False(rows.IsOff("nova"));
    }

    /// <summary>A stale off row under the target name does not decide the renamed Agent: it takes the source's value, on.</summary>
    [Fact]
    public void Rename_StaleTargetRow_TakesSourceValue()
    {
        using TempDataDir dir = new();
        using PersonaStore store = dir.CreatePersonaStore();
        Persona added = store.Add(TestPersonas.Identity("nova"), "You are Nova.");
        new PersonaBrowserStore(dir.Options()).Set("nova2", false);
        string renamed = added.Text.Replace("name: 'nova'", "name: 'nova2'", StringComparison.Ordinal);

        _ = store.Update("nova", renamed, model: null, effort: null, workMode: null);

        Assert.False(store.Get("nova2")?.BrowserOff);
        Assert.False(new PersonaBrowserStore(dir.Options()).IsOff("nova2"));
    }

    /// <summary>Removing an Agent deletes its row, so an Agent created later with the same name starts on.</summary>
    [Fact]
    public void Remove_DeletesRow()
    {
        using TempDataDir dir = new();
        using PersonaStore store = dir.CreatePersonaStore();
        _ = store.Add(TestPersonas.Identity("nova"), "You are Nova.");
        store.SetBrowser("nova", false);

        store.Remove("nova");

        Assert.False(new PersonaBrowserStore(dir.Options()).IsOff("nova"));
        _ = store.Add(TestPersonas.Identity("nova"), "You are Nova.");
        Assert.False(store.Get("nova")?.BrowserOff);
    }

    /// <summary>A Persona file that disappears and is seen by a rescan takes its row with it.</summary>
    [Fact]
    public void Rescan_FileGone_DropsTheRow()
    {
        using TempDataDir dir = new();
        using PersonaStore store = dir.CreatePersonaStore();
        _ = store.Add(TestPersonas.Identity("nova"), "You are Nova.");
        store.SetBrowser("nova", false);

        File.Delete(Assert.Single(store.Entries).Path);
        store.RescanNow();

        Assert.False(new PersonaBrowserStore(dir.Options()).IsOff("nova"));
    }

    /// <summary>A stale off row under a name (left by a file deleted outside Remove) does not make a new Agent of that name start off.</summary>
    [Fact]
    public void Add_StaleRow_StartsOn()
    {
        using TempDataDir dir = new();
        using PersonaStore store = dir.CreatePersonaStore();
        new PersonaBrowserStore(dir.Options()).Set("nova", false);

        _ = store.Add(TestPersonas.Identity("nova"), "You are Nova.");

        Assert.False(store.Get("nova")?.BrowserOff);
        Assert.False(new PersonaBrowserStore(dir.Options()).IsOff("nova"));
    }

    /// <summary>Adding with <c>browserOff</c> writes the row in the same write, so the first read already shows it off.</summary>
    [Fact]
    public void Add_BrowserOff_WritesRow()
    {
        using TempDataDir dir = new();
        using PersonaStore store = dir.CreatePersonaStore();
        bool? offAtFirstEvent = null;
        store.PersonasChanged += () => offAtFirstEvent ??= store.Get("nova")?.BrowserOff;

        Persona added = store.Add(TestPersonas.Identity("nova"), "You are Nova.", browserOff: true);

        Assert.True(added.BrowserOff);
        Assert.True(offAtFirstEvent);
        Assert.True(store.Get("nova")?.BrowserOff);
        Assert.True(new PersonaBrowserStore(dir.Options()).IsOff("nova"));
    }

    /// <summary>Resetting a built-in Agent whose browser is off returns it to on.</summary>
    [Fact]
    public void Reset_Off_ReturnsOn()
    {
        using TempDataDir dir = new();
        using PersonaStore store = dir.CreatePersonaStore();
        _ = store.Add(TestPersonas.Identity("Chief"), "You are the Chief.");
        store.SetBrowser("Chief", false);

        _ = new BuiltinAgentReset(store).ResetToDefault("Chief");

        Assert.False(store.Get("Chief")?.BrowserOff);
        Assert.False(new PersonaBrowserStore(dir.Options()).IsOff("Chief"));
    }

    /// <summary>A store built by hand without a browser store reads every Persona as on and refuses <c>SetBrowser</c>.</summary>
    [Fact]
    public void NoBrowserStore_ReadsOn_AndSetBrowserThrows()
    {
        using TempDataDir dir = new();
        using PersonaStore store = new(new AgentPaths(dir.Options()), new PersonaModelStore(dir.Options()), new PersonaEffortStore(dir.Options()), new PersonaWorkModeStore(dir.Options()), Microsoft.Extensions.Logging.Abstractions.NullLogger<PersonaStore>.Instance);
        _ = store.Add(TestPersonas.Identity("nova"), "You are Nova.", browserOff: true);

        Assert.False(store.Get("nova")?.BrowserOff);
        _ = Assert.Throws<InvalidOperationException>(() => store.SetBrowser("nova", false));
    }
}
