namespace Agency.Huddle.Tests.Acp;

using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Teams;

/// <summary>
/// Pins correction C5 (Spec §8.1): the two process blocks are gone. A Team's process now reaches every member
/// through the Team's <c>AGENTS.md</c> (slot 3, D-47), so a prompt carries no block of the retired wording.
/// </summary>
public sealed class ProcessBlocksRetiredTests
{
    /// <summary>The distinctive line of the retired manager block's default text (read from the catalog before removal).</summary>
    private const string ManagerBlockLine = "This is your Team's process. Follow it and keep it current.";

    /// <summary>The distinctive line of the retired member block's default text (read from the catalog before removal).</summary>
    private const string MemberBlockLine = "Your Team follows a written process. Read it before you start work:";

    private const string ProcessText = "Ship small and review every change.";

    private static readonly IReadOnlyList<string> ToolNames = ["mcp__team__get_help"];

    private static readonly RoleRef Role = new("development", "developer", "1");

    /// <summary>Composes the system prompt of one member of Apollo, with a Team <c>AGENTS.md</c> that holds the process.</summary>
    /// <param name="name">The member's Name; Grace manages Apollo and Ada is a member.</param>
    /// <returns>The composed prompt.</returns>
    private static string ComposeFor(string name)
    {
        FakeTeamCatalog catalog = new()
        {
            Teams = [new TeamSummary("Apollo", [], ["Ada", "Grace"], true) { Manager = "Grace" }],
        };
        PersonaIdentity identity = new(name, "Developer", name.ToLowerInvariant(), ["Apollo"], Role: Role);
        Assert.True(PersonaFrontmatter.TryReadIdentity(PersonaFrontmatter.Compose(identity, "Body."), out PersonaIdentity? read, out _));
        Assert.NotNull(read);
        RoleBlockInput? roleBlock = RoleBlockBuilder.Build(read, [], catalog, _ => null);
        AgentsMdSnapshot agentsMd = new(
            [new AgentsMdSection(AgentsMdLevel.Team, "Apollo", null, "Teams/Apollo/AGENTS.md", ProcessText, false)],
            []);

        return SystemPromptComposer.Compose(
            new Persona(name, $"You are {name}."),
            new FakePromptSource(),
            "mcp__team__get_help",
            ToolNames,
            [],
            string.Empty,
            memory: null,
            SessionScope.Shared,
            roleBlock: roleBlock,
            agentsMd: agentsMd);
    }

    /// <summary>A manager's prompt carries the process through <c>AGENTS.md</c> and no process-block text.</summary>
    [Fact]
    public void Compose_Manager_CarriesNoProcessBlock()
    {
        string composed = ComposeFor("Grace");

        Assert.Contains(ProcessText, composed);
        Assert.DoesNotContain(ManagerBlockLine, composed);
        Assert.DoesNotContain(MemberBlockLine, composed);
        Assert.DoesNotContain("Process.md", composed);
    }

    /// <summary>A member's prompt carries the process through <c>AGENTS.md</c> and no process-block text.</summary>
    [Fact]
    public void Compose_Member_CarriesNoProcessBlock()
    {
        string composed = ComposeFor("Ada");

        Assert.Contains(ProcessText, composed);
        Assert.DoesNotContain(ManagerBlockLine, composed);
        Assert.DoesNotContain(MemberBlockLine, composed);
        Assert.DoesNotContain("Process.md", composed);
    }

    /// <summary>The catalog no longer defines either process-block prompt.</summary>
    /// <param name="key">The retired prompt key.</param>
    [Theory]
    [InlineData("systemPrompt.processBlockManager")]
    [InlineData("systemPrompt.processBlockMember")]
    public void Catalog_RetiredKey_IsAbsent(string key)
    {
        Assert.DoesNotContain(PromptCatalog.All, prompt => string.Equals(prompt.Key, key, StringComparison.Ordinal));
    }
}
