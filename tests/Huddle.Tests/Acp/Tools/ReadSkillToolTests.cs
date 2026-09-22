namespace Agency.Huddle.Tests.Acp.Tools;

using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Skills;
using Agency.Huddle.Tests.Acp.Fakes;

/// <summary>
/// Pins <c>read_skill</c> against Spec §6.6 and Spec §5.3 Contract B, using a real
/// <see cref="PersonaStore"/> and a real <see cref="SkillStore"/> in a <see cref="TempDataDir"/> rather
/// than fakes - the same shape <c>CreateRoomToolTests</c> uses - so the "held" check runs against the
/// actual frontmatter parser and the actual shipped <c>team-building</c> Skill.
/// </summary>
public sealed class ReadSkillToolTests
{
    /// <summary>Holding the Skill with no <c>file</c> argument returns its <c>SKILL.md</c>, headed and with no leading frontmatter.</summary>
    [Fact]
    public async Task InvokeAsync_HeldNoFile_ReturnsSkillMdWithHeaderAndNoFrontmatter()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        using var personas = CreatePersonaStore(dir);
        using var skills = CreateSkillStore(dir);
        SeedNova(personas, ["team-building"]);
        var tool = new ReadSkillTool(skills, personas, "Nova", new FakePromptSource());

        var result = await tool.InvokeAsync(new JsonObject { ["name"] = "team-building" }, ct);

        Assert.StartsWith("[Skill: team-building · file: SKILL.md · also: ", result, StringComparison.Ordinal);
        var separatorIndex = result.IndexOf("\n\n", StringComparison.Ordinal);
        Assert.True(separatorIndex >= 0, "Expected the header to be followed by a blank line before the file's body.");
        var body = result[(separatorIndex + 2)..];
        Assert.False(body.TrimStart().StartsWith("---", StringComparison.Ordinal), "The returned SKILL.md body must have its frontmatter stripped.");
    }

    /// <summary>A <c>file</c> argument naming a real supporting file returns exactly that file's resolved text.</summary>
    [Fact]
    public async Task InvokeAsync_HeldWithFile_ReturnsThatFilesResolvedText()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        using var personas = CreatePersonaStore(dir);
        using var skills = CreateSkillStore(dir);
        SeedNova(personas, ["team-building"]);
        var tool = new ReadSkillTool(skills, personas, "Nova", new FakePromptSource());
        var expectedRolesText = skills.ReadFile("team-building", "roles.md");
        Assert.NotNull(expectedRolesText);

        var result = await tool.InvokeAsync(new JsonObject { ["name"] = "team-building", ["file"] = "roles.md" }, ct);

        Assert.Contains("file: roles.md", result, StringComparison.Ordinal);
        Assert.Contains(expectedRolesText, result, StringComparison.Ordinal);
    }

    /// <summary>A Skill the caller's Persona does not list in its frontmatter is refused, naming the Skills it does hold.</summary>
    [Fact]
    public async Task InvokeAsync_SkillNotHeld_ReturnsRefusalNamingHeldSkills()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        using var personas = CreatePersonaStore(dir);
        using var skills = CreateSkillStore(dir);
        SeedNova(personas, ["team-building"]);
        var tool = new ReadSkillTool(skills, personas, "Nova", new FakePromptSource());

        var result = await tool.InvokeAsync(new JsonObject { ["name"] = "other-skill" }, ct);

        Assert.Equal("You do not hold the Skill 'other-skill'. Your Skills: team-building.", result);
    }

    /// <summary>A name the Persona's frontmatter lists, but that resolves to no real Skill, is a distinct refusal from "not held".</summary>
    [Fact]
    public async Task InvokeAsync_HeldNameButSkillDoesNotExist_ReturnsDoesNotExistRefusal()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        using var personas = CreatePersonaStore(dir);
        using var skills = CreateSkillStore(dir);
        SeedNova(personas, ["ghost-skill"]);
        var tool = new ReadSkillTool(skills, personas, "Nova", new FakePromptSource());

        var result = await tool.InvokeAsync(new JsonObject { ["name"] = "ghost-skill" }, ct);

        Assert.Equal("The Skill 'ghost-skill' does not exist.", result);
    }

    /// <summary>An unknown <c>file</c> argument is refused, listing every file the held Skill actually has.</summary>
    [Fact]
    public async Task InvokeAsync_UnknownFile_ListsTheSkillsRealFiles()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        using var personas = CreatePersonaStore(dir);
        using var skills = CreateSkillStore(dir);
        SeedNova(personas, ["team-building"]);
        var tool = new ReadSkillTool(skills, personas, "Nova", new FakePromptSource());
        var teamBuilding = skills.Get("team-building");
        Assert.NotNull(teamBuilding);

        var result = await tool.InvokeAsync(new JsonObject { ["name"] = "team-building", ["file"] = "nope.md" }, ct);

        foreach (var file in teamBuilding.Files)
        {
            Assert.Contains(file, result, StringComparison.Ordinal);
        }
    }

    /// <summary>The tool's <c>InputSchema</c> marks <c>name</c> as required.</summary>
    [Fact]
    public void InputSchema_RequiresName()
    {
        using var dir = new TempDataDir();
        using var personas = CreatePersonaStore(dir);
        using var skills = CreateSkillStore(dir);
        var tool = new ReadSkillTool(skills, personas, "Nova", new FakePromptSource());

        var required = tool.InputSchema["required"] as JsonArray;

        Assert.NotNull(required);
        Assert.Contains(required, node => string.Equals((string?)node, "name", StringComparison.Ordinal));
    }

    /// <summary>The tool's name is exactly <c>read_skill</c>, the name Contract B and the Skill Index both name.</summary>
    [Fact]
    public void Name_IsReadSkill()
    {
        using var dir = new TempDataDir();
        using var personas = CreatePersonaStore(dir);
        using var skills = CreateSkillStore(dir);
        var tool = new ReadSkillTool(skills, personas, "Nova", new FakePromptSource());

        Assert.Equal("read_skill", tool.Name);
    }

    /// <summary>
    /// Holding a Skill is checked live: a caller that held <c>team-building</c> when the tool was
    /// constructed, but whose Persona file was then hand-edited to drop it, is refused on the very next
    /// call - proving <see cref="ReadSkillTool"/> re-resolves <see cref="PersonaStore.ResolveByNameOrAlias"/>
    /// per call rather than caching the Skills list it saw at construction. Writes the file directly to
    /// disk, exactly as <c>PersonaStoreTests.ExternalFileChange_IsNoticedThroughPersonasChanged</c> does,
    /// and waits for <see cref="PersonaStore.PersonasChanged"/> before calling, so this proves the live
    /// path end to end rather than merely the in-process <see cref="PersonaStore.Update"/> path.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_SkillRemovedFromPersonaFileAfterConstruction_RefusesOnTheNextCall()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        using var personas = CreatePersonaStore(dir);
        using var skills = CreateSkillStore(dir);
        SeedNova(personas, ["team-building"]);
        var tool = new ReadSkillTool(skills, personas, "Nova", new FakePromptSource());
        var path = personas.PathFor("Nova");

        var personasChanged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        personas.PersonasChanged += () => personasChanged.TrySetResult();

        await File.WriteAllTextAsync(path, PersonaTextWithNoSkills("Nova", "You are Nova."), ct);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var registration = timeout.Token.Register(() => personasChanged.TrySetCanceled());
        await personasChanged.Task;

        var result = await tool.InvokeAsync(new JsonObject { ["name"] = "team-building" }, ct);

        Assert.StartsWith("You do not hold the Skill 'team-building'.", result, StringComparison.Ordinal);
    }

    /// <summary>A real <see cref="PersonaStore"/> over a fresh <see cref="TempDataDir"/>, matching <c>CreateRoomToolTests</c>'s shape.</summary>
    /// <param name="dir">Supplies the store's <c>DataDir</c>.</param>
    private static PersonaStore CreatePersonaStore(TempDataDir dir)
    {
        return new PersonaStore(dir.Options(), new PersonaModelStore(dir.Options()), new PersonaEffortStore(dir.Options()), NullLogger<PersonaStore>.Instance);
    }

    /// <summary>A real <see cref="SkillStore"/> over a fresh <see cref="TempDataDir"/>, resolving the real shipped <c>team-building</c> Skill.</summary>
    /// <param name="dir">Supplies the store's <c>DataDir</c>.</param>
    private static SkillStore CreateSkillStore(TempDataDir dir)
    {
        return new SkillStore(dir.Options(), NullLogger<SkillStore>.Instance);
    }

    /// <summary>Writes <c>Nova.md</c> holding <paramref name="skillNames"/> in its frontmatter, through the real <see cref="PersonaStore.Add"/> composer.</summary>
    /// <param name="personas">The store to add the Persona to.</param>
    /// <param name="skillNames">The Skill names for Nova's frontmatter <c>skills</c> field.</param>
    private static void SeedNova(PersonaStore personas, IReadOnlyList<string> skillNames)
    {
        PersonaIdentity identity = new("Nova", "Nova", "Nova", [], Skills: skillNames);
        personas.Add(identity, "You are Nova.");
    }

    /// <summary>Minimal valid Persona frontmatter for <paramref name="name"/>, with no <c>skills</c> field at all.</summary>
    /// <param name="name">The Persona's Name, Title and Alias.</param>
    /// <param name="body">The Persona's system-prompt body.</param>
    private static string PersonaTextWithNoSkills(string name, string body)
    {
        return $"---\nName: {name}\nTitle: {name}\nAlias: {name}\n---\n{body}";
    }
}
