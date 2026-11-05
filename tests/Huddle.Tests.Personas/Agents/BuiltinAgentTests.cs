using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Agents;

namespace Agency.Huddle.Tests.Agents;

/// <summary>
/// Guards <see cref="BuiltinAgent.DefaultText"/> - the embedded Chief of Staff Persona file -
/// against a drift that would break <see cref="BuiltinAgentSeeder"/> silently: it must always
/// parse as a valid Persona identity and carry the marker and Skill Spec §6.12 requires.
/// </summary>
public sealed class BuiltinAgentTests
{
    /// <summary>
    /// The embedded default parses cleanly and its identity carries the <c>_builtin: chief-of-staff</c>
    /// marker and the <c>team-building</c> Skill (Spec §6.12).
    /// </summary>
    [Fact]
    public void DefaultText_ParsesWithMarkerAndSkill()
    {
        var parsed = PersonaFrontmatter.TryReadIdentity(BuiltinAgent.DefaultText, out PersonaIdentity? identity, out var error);

        Assert.True(parsed, error);
        Assert.NotNull(identity);
        Assert.Equal(BuiltinAgent.ChiefOfStaffMarker, identity.Builtin);
        Assert.Equal(["team-building", "team-rollup", "ingest", "wrap-up", "brain-check"], identity.Skills);
    }

    /// <summary>RS §6.11 (RS-T13): the built-in Chief of Staff's own text teaches keeping one memory file per piece of work it coordinates, conditioned on having a memory folder at all (D31 correction 20).</summary>
    [Fact]
    public void ChiefOfStaff_TeachesMemoryFilePerProject()
    {
        Assert.Contains("memory folder", BuiltinAgent.DefaultText, StringComparison.Ordinal); // contains-ok: key term of model-facing text
        Assert.Contains("project-", BuiltinAgent.DefaultText, StringComparison.Ordinal); // contains-ok: key term of model-facing text
    }

    /// <summary>RS §6.11: the built-in Chief of Staff says plainly that another Room's own conversation is not visible to it unless reported.</summary>
    [Fact]
    public void ChiefOfStaff_SaysOtherRoomsAreNotVisible()
    {
        Assert.Contains("not visible", BuiltinAgent.DefaultText, StringComparison.Ordinal); // contains-ok: key term of model-facing text
    }

    /// <summary>#343: a thing dropped in passing is sorted by the Teams' <c>owns:</c> lines; a Team fact is proposed, a dated item becomes a Task, and what cannot be placed gets one question in one list.</summary>
    [Fact]
    public void ChiefOfStaff_SortsCapturesByOwnsAndFilesThem()
    {
        string text = BuiltinAgent.DefaultText;

        Assert.Contains("owns:", text, StringComparison.Ordinal); // contains-ok: key term of model-facing text
        Assert.Contains("list_teams", text, StringComparison.Ordinal); // contains-ok: key term of model-facing text
        Assert.Contains("propose_team_memory", text, StringComparison.Ordinal); // contains-ok: key term of model-facing text
        Assert.Contains("becomes a Task", text, StringComparison.Ordinal); // contains-ok: key term of model-facing text
        Assert.Contains("one short question", text, StringComparison.Ordinal); // contains-ok: key term of model-facing text
        Assert.Contains("in one list", text, StringComparison.Ordinal); // contains-ok: key term of model-facing text
    }

    /// <summary>#344: a fact any Agent could need goes to the <c>memory</c> folder, a rule goes to <c>AGENTS.md</c> as a proposed line, and a message holding both is split with each destination named.</summary>
    [Fact]
    public void ChiefOfStaff_SplitsAFactFromARule()
    {
        string text = BuiltinAgent.DefaultText;

        Assert.Contains("folder `memory`", text, StringComparison.Ordinal); // contains-ok: key term of model-facing text
        Assert.Contains("for everyone", text, StringComparison.Ordinal); // contains-ok: key term of model-facing text
        Assert.Contains("proposed line", text, StringComparison.Ordinal); // contains-ok: key term of model-facing text
        Assert.Contains("AGENTS.md", text, StringComparison.Ordinal); // contains-ok: key term of model-facing text
        Assert.Contains("Split it", text, StringComparison.Ordinal); // contains-ok: key term of model-facing text
        Assert.Contains("where each part went", text, StringComparison.Ordinal); // contains-ok: key term of model-facing text
    }

    /// <summary>#384: an undecided idea or plan becomes a Task to decide it, and an instruction on how Agents behave is a rule even inside a fact, with its exact line given to the Human.</summary>
    [Fact]
    public void ChiefOfStaff_FilesUndecidedIdeasAsTasksAndRulesInsideFacts()
    {
        string text = BuiltinAgent.DefaultText.ReplaceLineEndings(" ");

        Assert.Contains("not yet decided becomes a Task", text, StringComparison.Ordinal); // contains-ok: key term of model-facing text
        Assert.Contains("the Helper declines an undecided idea", text, StringComparison.Ordinal); // contains-ok: key term of model-facing text
        Assert.Contains("is a rule even inside a fact", text, StringComparison.Ordinal); // contains-ok: key term of model-facing text
        Assert.Contains("the exact line to add to AGENTS.md", text, StringComparison.Ordinal); // contains-ok: key term of model-facing text
    }

    /// <summary>#343 and #344: the Chief of Staff's own memory is for its own coordination only, never a fact a Team or every Agent needs.</summary>
    [Fact]
    public void ChiefOfStaff_OwnMemoryIsForCoordinationOnly()
    {
        Assert.Contains("only for your own coordination", BuiltinAgent.DefaultText, StringComparison.Ordinal); // contains-ok: key term of model-facing text
    }

    /// <summary>D14: the Chief is told to load the deferred tools first, to name the memory folder, and that its rules outrank the general Memory and Second Brain text, where "your memory" means private notes only.</summary>
    [Fact]
    public void ChiefOfStaff_LoadsDeferredToolsNamesFolderAndOverridesGeneralText()
    {
        string text = BuiltinAgent.DefaultText;

        Assert.Contains("load it with tool search before you first use it", text, StringComparison.Ordinal); // contains-ok: key term of model-facing text
        Assert.Contains("so load it with tool search first", text, StringComparison.Ordinal); // contains-ok: key term of model-facing text
        Assert.Contains("needs its folder named", text, StringComparison.Ordinal); // contains-ok: key term of model-facing text
        Assert.Contains("writes your own private notes", text, StringComparison.Ordinal); // contains-ok: key term of model-facing text
        Assert.Contains("take precedence over the general Memory and Second Brain text", text, StringComparison.Ordinal); // contains-ok: key term of model-facing text
        Assert.Contains("private notes for yourself", text, StringComparison.Ordinal); // contains-ok: key term of model-facing text
    }

    /// <summary>D14: a Team's fact is proposed, never written through the memory tool to the Team's folder; the load step is concrete; the description states the fact; the reply names only where the item was filed.</summary>
    [Fact]
    public void ChiefOfStaff_ProposesTeamFactsAndStatesTheFactInTheDescription()
    {
        string text = BuiltinAgent.DefaultText;

        Assert.DoesNotContain("Team's folder", text, StringComparison.Ordinal);
        Assert.Contains("a Team's fact never goes through the memory tool", text, StringComparison.Ordinal); // contains-ok: key term of model-facing text
        Assert.Contains("search tools for propose_team_memory", text, StringComparison.Ordinal); // contains-ok: key term of model-facing text
        Assert.Contains("once per fact, before you reply", text, StringComparison.Ordinal); // contains-ok: key term of model-facing text
        Assert.Contains("the description line states the fact itself", text, StringComparison.Ordinal); // contains-ok: key term of model-facing text
        Assert.Contains("never say an item is \"already filed\"", text, StringComparison.Ordinal); // contains-ok: key term of model-facing text
        Assert.Contains("say you proposed it", text, StringComparison.Ordinal); // contains-ok: key term of model-facing text
        Assert.Contains("missing what makes it usable", text, StringComparison.Ordinal); // contains-ok: key term of model-facing text
        Assert.Contains("gets the one\n  question instead of a proposal", text.Replace("\r\n", "\n", StringComparison.Ordinal), StringComparison.Ordinal); // contains-ok: key term of model-facing text
    }

    /// <summary>#362: Execution Bias says to act rather than stop at a plan, and names its three exceptions: Skill gates such as ingest, money and prices, and work not asked for.</summary>
    [Fact]
    public void ChiefOfStaff_HasExecutionBiasWithNamedExceptions()
    {
        string text = BuiltinAgent.DefaultText;

        Assert.Contains("do not stop at a plan", text, StringComparison.Ordinal); // contains-ok: key term of model-facing text
        Assert.Contains("such as ingest", text, StringComparison.Ordinal); // contains-ok: key term of model-facing text
        Assert.Contains("money and prices", text, StringComparison.Ordinal); // contains-ok: key term of model-facing text
        Assert.Contains("did not ask for", text, StringComparison.Ordinal); // contains-ok: key term of model-facing text
    }

    /// <summary>The model-facing text uses placeholders and bare tool names, never the <c>mcp__team__</c> prefix.</summary>
    [Fact]
    public void ChiefOfStaff_TextHasNoToolPrefix()
    {
        Assert.DoesNotContain("mcp__team__", BuiltinAgent.DefaultText, StringComparison.Ordinal);
    }
}
