using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Agents;
using Agency.Huddle.App.Data;
using Agency.Huddle.Tests.Factories;

namespace Agency.Huddle.Tests.Agents;

/// <summary>
/// Only <c>PersonaStore.SetBrowser</c> (and <c>Add</c> for a new Agent) writes the browser row (issue #551): <c>propose_agents</c>,
/// a Pack update, <c>PersonaStore.Update</c> and a key in an Agent's own file leave it alone.
/// </summary>
public sealed class BrowserCannotChangeTests
{
    /// <summary>A Candidate has no browser field and the <c>propose_agents</c> schema offers none, so a proposal cannot set it.</summary>
    [Fact]
    public void ProposeAgents_CannotSetBrowser()
    {
        string[] members = [.. typeof(Candidate).GetProperties().Select(static property => property.Name)];
        string schema = CandidateJson.Schema().ToJsonString();

        Assert.NotEmpty(members);
        Assert.DoesNotContain(members, static name => name.Contains("browser", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("browser", schema, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A Pack update rewrites the Agent's text through <c>Update</c> with the Agent's own values; an Agent switched off stays off.</summary>
    [Fact]
    public void PackUpdate_KeepsOff()
    {
        using TempDataDir dir = new();
        using PersonaStore store = dir.CreatePersonaStore();
        _ = store.Add(TestPersonas.Identity("nova"), "You are Nova.");
        store.SetBrowser("nova", false);
        Persona persona = store.Get("nova") ?? throw new InvalidOperationException("nova is missing.");

        _ = store.Update(persona.Name, persona.Text + "\nUse the documents pack.", persona.Model, persona.Effort, persona.WorkMode);

        Assert.True(store.Get("nova")?.BrowserOff);
        Assert.True(new PersonaBrowserStore(dir.Options()).IsOff("nova"));
    }

    /// <summary><c>Update</c> takes no browser flag: an edit neither switches an Agent's browser off nor back on.</summary>
    [Fact]
    public void Update_DoesNotTouchBrowser()
    {
        using TempDataDir dir = new();
        using PersonaStore store = dir.CreatePersonaStore();
        Persona on = store.Add(TestPersonas.Identity("nova"), "You are Nova.");
        Persona off = store.Add(TestPersonas.Identity("sable"), "You are Sable.", browserOff: true);

        _ = store.Update("nova", on.Text + "\nMore.", model: null, effort: null, workMode: null);
        _ = store.Update("sable", off.Text + "\nMore.", model: null, effort: null, workMode: null);

        Assert.False(store.Get("nova")?.BrowserOff);
        Assert.True(store.Get("sable")?.BrowserOff);
    }

    /// <summary>A <c>browser: off</c> or <c>browserOff: true</c> key in an Agent's file changes nothing, in either direction.</summary>
    [Fact]
    public void Frontmatter_KeyIgnored()
    {
        using TempDataDir dir = new();
        using PersonaStore store = dir.CreatePersonaStore();
        Persona on = store.Add(TestPersonas.Identity("nova"), "You are Nova.");
        Persona off = store.Add(TestPersonas.Identity("sable"), "You are Sable.", browserOff: true);
        File.WriteAllText(store.PathFor("nova"), WithKeys(on.Text, "browser: off\nbrowserOff: true\n"));
        File.WriteAllText(store.PathFor("sable"), WithKeys(off.Text, "browser: on\nbrowserOff: false\n"));

        store.RescanNow();

        Assert.NotNull(store.Get("nova"));
        Assert.NotNull(store.Get("sable"));
        Assert.False(store.Get("nova")?.BrowserOff);
        Assert.True(store.Get("sable")?.BrowserOff);
    }

    private static string WithKeys(string text, string keys)
    {
        int firstBreak = text.IndexOf('\n', StringComparison.Ordinal);
        return text[..(firstBreak + 1)] + keys + text[(firstBreak + 1)..];
    }
}
