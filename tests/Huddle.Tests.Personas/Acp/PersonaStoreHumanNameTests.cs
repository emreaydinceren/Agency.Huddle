using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Profile;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Factories;

namespace Agency.Huddle.Tests.Acp;

/// <summary>Pins the rule that no Agent is written with the Human's Name or Alias, and that a file already on disk still loads (issue 351).</summary>
public sealed class PersonaStoreHumanNameTests
{
    private static PersonaStore Create(TempDataDir dir)
    {
        return new PersonaStore(
            new AgentPaths(dir.Options()), new PersonaModelStore(dir.Options()), new PersonaEffortStore(dir.Options()), new PersonaWorkModeStore(dir.Options()),
            NullLogger<PersonaStore>.Instance, new ConfiguredHumanIdentity("Sam"));
    }

    /// <summary>Adding an Agent whose Name is the Human's, in any case, is refused and writes nothing.</summary>
    [Theory]
    [InlineData("Sam")]
    [InlineData("SAM")]
    public void Add_NameIsTheHumans_IsRefusedAndWritesNothing(string name)
    {
        using TempDataDir dir = new();
        using PersonaStore store = Create(dir);

        ChatException ex = Assert.Throws<ChatException>(() => store.Add(TestPersonas.Identity(name), "You help."));

        Assert.Equal(ErrorCodes.BadMessage, ex.Code);
        Assert.Empty(store.Entries);
    }

    /// <summary>Adding an Agent whose Alias is the Human's, in any case, is refused.</summary>
    [Fact]
    public void Add_AliasIsTheHumans_IsRefused()
    {
        using TempDataDir dir = new();
        using PersonaStore store = Create(dir);

        _ = Assert.Throws<ChatException>(() => store.Add(TestPersonas.Identity("coo", alias: "sAm"), "You help."));

        Assert.Empty(store.Entries);
    }

    /// <summary>Renaming an Agent to the Human's Name is refused and the file keeps its old text.</summary>
    [Fact]
    public void Update_RenameToTheHumansName_IsRefused()
    {
        using TempDataDir dir = new();
        using PersonaStore store = Create(dir);
        _ = store.Add(TestPersonas.Identity("coo"), "first");
        string before = File.ReadAllText(store.PathFor("coo"));

        _ = Assert.Throws<ChatException>(() => store.Update("coo", TestPersonas.FrontmatterText("sam", "second"), model: null, effort: null, workMode: null));

        Assert.Equal(before, File.ReadAllText(store.PathFor("coo")));
    }

    /// <summary>Changing an Agent's Alias to the Human's is refused.</summary>
    [Fact]
    public void Update_AliasChangedToTheHumans_IsRefused()
    {
        using TempDataDir dir = new();
        using PersonaStore store = Create(dir);
        _ = store.Add(TestPersonas.Identity("coo"), "first");

        _ = Assert.Throws<ChatException>(() => store.Update("coo", TestPersonas.FrontmatterText("coo", "second", alias: "SAM"), model: null, effort: null, workMode: null));

        Assert.Equal("coo", Assert.Single(store.Entries).Alias);
    }

    /// <summary>A hand-written file that already carries the Human's Name still loads on a rescan, so the Agent does not vanish.</summary>
    [Fact]
    public void Rescan_HandWrittenFileWithTheHumansName_StillLoads()
    {
        using TempDataDir dir = new();
        using PersonaStore store = Create(dir);
        TestPersonaFiles.Write(new AgentPaths(dir.Options()), "Sam", TestPersonas.FrontmatterText("Sam", "You were here first."));

        store.RescanNow();

        Assert.NotNull(store.Get("Sam"));
        Assert.Empty(store.RejectedFiles);
    }

    /// <summary>An Agent that already matches the Human can still be saved, as long as its Name and Alias do not change.</summary>
    [Fact]
    public void Update_ExistingMatchSavedUnchanged_IsAllowed()
    {
        using TempDataDir dir = new();
        using PersonaStore store = Create(dir);
        TestPersonaFiles.Write(new AgentPaths(dir.Options()), "Sam", TestPersonas.FrontmatterText("Sam", "You were here first."));
        store.RescanNow();

        Persona updated = store.Update("Sam", TestPersonas.FrontmatterText("Sam", "New instructions."), model: null, effort: null, workMode: null);

        Assert.Contains("New instructions.", updated.Text, StringComparison.Ordinal); // contains-ok: the saved text carries front matter around the body.
    }

    /// <summary>An Agent that matches the Human can be renamed to anything else.</summary>
    [Fact]
    public void Update_ExistingMatchRenamedAway_IsAllowed()
    {
        using TempDataDir dir = new();
        using PersonaStore store = Create(dir);
        TestPersonaFiles.Write(new AgentPaths(dir.Options()), "Sam", TestPersonas.FrontmatterText("Sam", "You were here first."));
        store.RescanNow();

        Persona updated = store.Update("Sam", TestPersonas.FrontmatterText("Pat", "You were here first."), model: null, effort: null, workMode: null);

        Assert.NotNull(store.Get(updated.Name));
        Assert.Equal("Pat", updated.Name);
    }

    /// <summary>A store built without a Human refuses nothing, so the many hand-built stores in tests behave as before.</summary>
    [Fact]
    public void Add_NoHumanSupplied_AcceptsAnyName()
    {
        using TempDataDir dir = new();
        using PersonaStore store = dir.Options().CreatePersonaStore();

        Persona added = store.Add(TestPersonas.Identity("Sam"), "You help.");

        Assert.Equal("Sam", added.Name);
    }
}
