using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.Acp;

/// <summary>
/// Pins how <see cref="PersonaStore"/> carries a Persona's Work Mode: joined on read, stored on add
/// and update, moved on rename and removed with the Persona, exactly as Model and Effort are.
/// </summary>
public sealed class PersonaStoreWorkModeTests
{
    /// <summary>The file and the Work Mode row are written separately, yet <see cref="PersonaStore.Get"/> returns both.</summary>
    [Fact]
    public void Get_JoinsTheStoredWorkModeToTheFile()
    {
        using TempDataDir dir = new();
        TestPersonaFiles.Write(new TeammatePaths(dir.Options()), "coo", PersonaText("coo", "You are the Chief of Staff."));
        new PersonaWorkModeStore(dir.Options()).Set("coo", "plan");
        using PersonaStore store = CreateStore(dir);

        Persona? persona = store.Get("coo");

        Assert.NotNull(persona);
        Assert.Equal("plan", persona.WorkMode);
    }

    /// <summary>A Persona added with a Work Mode stores it.</summary>
    [Fact]
    public void Add_WithAWorkMode_StoresIt()
    {
        using TempDataDir dir = new();
        using PersonaStore store = CreateStore(dir);

        Persona persona = store.Add(Identity("coo"), "text", "claude-opus-4", "high", "acceptEdits");

        Assert.Equal("acceptEdits", persona.WorkMode);
        Assert.Equal("acceptEdits", StoredWorkMode(store, "coo"));
    }

    /// <summary>A Persona added without a Work Mode has none, so the wire stays what it is today.</summary>
    [Fact]
    public void Add_WithoutAWorkMode_LeavesItUnset()
    {
        using TempDataDir dir = new();
        using PersonaStore store = CreateStore(dir);

        Persona persona = store.Add(Identity("coo"), "text");

        Assert.Null(persona.WorkMode);
        Assert.Null(StoredWorkMode(store, "coo"));
    }

    /// <summary>Changing only the Work Mode keeps the text, Model and Effort.</summary>
    [Fact]
    public void Update_ChangingOnlyTheWorkMode_KeepsTheTextTheModelAndTheEffort()
    {
        using TempDataDir dir = new();
        using PersonaStore store = CreateStore(dir);
        store.Add(Identity("coo"), "unchanged text", "claude-opus-4", "high");

        Persona updated = store.Update("coo", PersonaText("coo", "unchanged text"), "claude-opus-4", "high", "plan");

        Assert.Equal(PersonaText("coo", "unchanged text"), updated.Text);
        Assert.Equal("claude-opus-4", updated.Model);
        Assert.Equal("high", updated.Effort);
        Assert.Equal("plan", updated.WorkMode);
        Assert.Equal("plan", StoredWorkMode(store, "coo"));
    }

    /// <summary>Passing null clears a stored Work Mode: <c>Update</c> has no default, so a caller must say so.</summary>
    [Fact]
    public void Update_WithNullWorkMode_ClearsTheStoredOne()
    {
        using TempDataDir dir = new();
        using PersonaStore store = CreateStore(dir);
        store.Add(Identity("coo"), "text", workMode: "plan");

        store.Update("coo", PersonaText("coo", "text"), model: null, effort: null, workMode: null);

        Assert.Null(StoredWorkMode(store, "coo"));
    }

    /// <summary>Renaming a Persona moves the Work Mode row, so the old Name's mode cannot resurrect on a later Persona of that Name.</summary>
    [Fact]
    public void Update_ChangingTheName_MovesTheWorkModeRow()
    {
        using TempDataDir dir = new();
        using PersonaStore store = CreateStore(dir);
        store.Add(Identity("coo"), "You are the Chief of Staff.", workMode: "plan");

        Persona updated = store.Update("coo", PersonaText("vp", "You are the VP."), model: null, effort: null, workMode: "plan");

        Assert.Equal("vp", updated.Name);
        Assert.Equal("plan", StoredWorkMode(store, "vp"));
        Assert.Null(new PersonaWorkModeStore(dir.Options()).Get("coo"));
    }

    /// <summary>Removing a Persona removes its Work Mode, so re-creating the Name does not inherit one nobody chose.</summary>
    [Fact]
    public void Remove_AlsoRemovesTheStoredWorkMode()
    {
        using TempDataDir dir = new();
        using PersonaStore store = CreateStore(dir);
        store.Add(Identity("coo"), "text", workMode: "plan");

        store.Remove("coo");
        Persona recreated = store.Add(Identity("coo"), "new text");

        Assert.Null(recreated.WorkMode);
    }

    private static string? StoredWorkMode(PersonaStore store, string name)
    {
        Persona? persona = store.Get(name);
        Assert.NotNull(persona);
        return persona.WorkMode;
    }

    private static PersonaStore CreateStore(TempDataDir dir)
    {
        return new PersonaStore(
            new TeammatePaths(dir.Options()),
            new PersonaModelStore(dir.Options()),
            new PersonaEffortStore(dir.Options()),
            new PersonaWorkModeStore(dir.Options()),
            NullLogger<PersonaStore>.Instance);
    }

    private static PersonaIdentity Identity(string name)
    {
        return new PersonaIdentity(name, name, name, []);
    }

    private static string PersonaText(string name, string body)
    {
        return $"---\nName: {name}\nTitle: {name}\nAlias: {name}\n---\n{body}";
    }
}
