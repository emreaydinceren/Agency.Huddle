using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Services;
using Agency.Huddle.Tests.Factories;

namespace Agency.Huddle.Tests.Acp;

/// <summary>
/// Pins <see cref="PersonaStore.UpdateMany"/> (Team Rename Spec §5.3 step 7): several definition files
/// are rewritten in one call that raises <see cref="PersonaStore.PersonasChanged"/> once, and a stale or
/// invalid edit never overwrites or half-applies. Each event count unsubscribes the instant the call
/// returns, because the file watcher raises a later rescan that <c>IsSameSnapshot</c> drops.
/// </summary>
public sealed class PersonaStoreUpdateManyTests
{
    /// <summary>Three Personas edited in one call raise <c>PersonasChanged</c> exactly once, and every file and the index hold the new text.</summary>
    [Fact]
    public void UpdateMany_ThreePersonas_RaisesOnceAndWritesEveryFile()
    {
        using TempDataDir dir = new();
        using PersonaStore store = dir.CreatePersonaStore();
        string[] names = ["ada", "bo", "cy"];
        List<PersonaTextEdit> edits = [];
        foreach (string name in names)
        {
            Persona added = store.Add(TestPersonas.Identity(name), "old body", "claude-opus-4", "high", "plan");
            edits.Add(new PersonaTextEdit(name, added.Text, TestPersonas.FrontmatterText(name, "new body")));
        }

        int raised = 0;
        void OnChanged() => Interlocked.Increment(ref raised);
        store.PersonasChanged += OnChanged;
        IReadOnlyList<string> skipped = store.UpdateMany(edits);
        store.PersonasChanged -= OnChanged;

        Assert.Empty(skipped);
        Assert.Equal(1, raised);
        AgentPaths paths = new(dir.Options());
        foreach (string name in names)
        {
            string expected = TestPersonas.FrontmatterText(name, "new body");
            Assert.Equal(expected, File.ReadAllText(paths.DefinitionFile(name)));
            Persona? persona = store.Get(name);
            Assert.NotNull(persona);
            Assert.Equal(expected, persona.Text);
            Assert.Equal("claude-opus-4", persona.Model);
            Assert.Equal("high", persona.Effort);
            Assert.Equal("plan", persona.WorkMode);
        }
    }

    /// <summary>An edit whose expected text no longer matches is skipped and reported, its file is untouched, and the other edits apply.</summary>
    [Fact]
    public void UpdateMany_StaleExpectedText_SkipsThatOneAndAppliesTheRest()
    {
        using TempDataDir dir = new();
        using PersonaStore store = dir.CreatePersonaStore();
        Persona ada = store.Add(TestPersonas.Identity("ada"), "old body");
        Persona bo = store.Add(TestPersonas.Identity("bo"), "old body");
        AgentPaths paths = new(dir.Options());
        string boBefore = File.ReadAllText(paths.DefinitionFile("bo"));

        IReadOnlyList<string> skipped = store.UpdateMany(
        [
            new PersonaTextEdit("ada", ada.Text, TestPersonas.FrontmatterText("ada", "new body")),
            new PersonaTextEdit("bo", "a human edited this meanwhile", TestPersonas.FrontmatterText("bo", "new body")),
        ]);

        Assert.Equal(["bo"], skipped);
        Assert.Equal(TestPersonas.FrontmatterText("ada", "new body"), File.ReadAllText(paths.DefinitionFile("ada")));
        Assert.Equal(boBefore, File.ReadAllText(paths.DefinitionFile("bo")));
        Assert.Equal(bo.Text, store.Get("bo")?.Text);
    }

    /// <summary>One invalid candidate refuses the whole batch before any write: no file changes and no event.</summary>
    [Fact]
    public void UpdateMany_InvalidCandidate_RefusesTheBatchBeforeAnyWrite()
    {
        using TempDataDir dir = new();
        using PersonaStore store = dir.CreatePersonaStore();
        Persona ada = store.Add(TestPersonas.Identity("ada"), "old body");
        Persona bo = store.Add(TestPersonas.Identity("bo"), "old body");
        AgentPaths paths = new(dir.Options());
        string adaBefore = File.ReadAllText(paths.DefinitionFile("ada"));
        string boBefore = File.ReadAllText(paths.DefinitionFile("bo"));

        int raised = 0;
        void OnChanged() => Interlocked.Increment(ref raised);
        store.PersonasChanged += OnChanged;
        Assert.Throws<ChatException>(() => store.UpdateMany(
        [
            new PersonaTextEdit("ada", ada.Text, TestPersonas.FrontmatterText("ada", "new body")),
            new PersonaTextEdit("bo", bo.Text, "no frontmatter at all"),
        ]));
        store.PersonasChanged -= OnChanged;

        Assert.Equal(0, raised);
        Assert.Equal(adaBefore, File.ReadAllText(paths.DefinitionFile("ada")));
        Assert.Equal(boBefore, File.ReadAllText(paths.DefinitionFile("bo")));
        Assert.Equal(ada.Text, store.Get("ada")?.Text);
    }
}
