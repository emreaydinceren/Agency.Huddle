namespace Agency.Huddle.Tests.Acp;

using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Teams;

/// <summary>Pins the process block <see cref="SystemPromptComposer"/> appends for a member of a Team that has a <c>Process.md</c> (Task 58, PR-4).</summary>
public sealed class ProcessBlockTests
{
    private static readonly IReadOnlyList<string> ToolNames = ["mcp__team__get_help"];

    private static readonly RoleRef Role = new("development", "developer", "1");

    /// <summary>Builds the Team: Grace manages Apollo and Ada is a member.</summary>
    /// <returns>The catalog.</returns>
    private static FakeTeamCatalog Catalog() => new()
    {
        Teams = [new TeamSummary("Apollo", [], ["Ada", "Grace"], true) { Manager = "Grace" }],
    };

    /// <summary>Reads the identity of a hired member of Apollo.</summary>
    /// <param name="name">The member's Name.</param>
    /// <returns>The identity.</returns>
    private static PersonaIdentity Identity(string name)
    {
        PersonaIdentity identity = new(name, "Developer", name.ToLowerInvariant(), ["Apollo"], Role: Role);
        Assert.True(PersonaFrontmatter.TryReadIdentity(PersonaFrontmatter.Compose(identity, "Body."), out PersonaIdentity? read, out _));
        Assert.NotNull(read);
        return read;
    }

    /// <summary>Builds the role block of a member and composes its system prompt.</summary>
    /// <param name="name">The member's Name.</param>
    /// <param name="teamsRoot">The Teams root folder.</param>
    /// <returns>The composed prompt.</returns>
    private static string ComposeFor(string name, string teamsRoot)
    {
        RoleBlockInput? block = RoleBlockBuilder.Build(Identity(name), [], Catalog(), _ => null, teamsRoot);
        return SystemPromptComposer.Compose(
            new Persona(name, $"You are {name}."),
            new FakePromptSource(),
            "mcp__team__get_help",
            ToolNames,
            [],
            string.Empty,
            null,
            SessionScope.Shared,
            roleBlock: block);
    }

    /// <summary>Writes a Team's <c>Process.md</c> under the Teams root.</summary>
    /// <param name="teamsRoot">The Teams root folder.</param>
    /// <param name="text">The file text.</param>
    private static void WriteProcess(string teamsRoot, string text)
    {
        Directory.CreateDirectory(Path.Combine(teamsRoot, "Apollo"));
        File.WriteAllText(Path.Combine(teamsRoot, "Apollo", "Process.md"), text);
    }

    /// <summary>The manager's prompt carries the whole <c>Process.md</c> text.</summary>
    [Fact]
    public void Compose_ManagerWithProcess_CarriesFullText()
    {
        using TempDataDir dir = new();
        WriteProcess(dir.Path, "# Process\n\nShip small.\n");

        string composed = ComposeFor("Grace", dir.Path);

        Assert.Contains("Ship small.", composed);
        Assert.Contains("This is your Team's process. Follow it and keep it current.", composed);
    }

    /// <summary>Another member gets one line with the Library path and not the text.</summary>
    [Fact]
    public void Compose_MemberWithProcess_CarriesOnlyThePath()
    {
        using TempDataDir dir = new();
        WriteProcess(dir.Path, "# Process\n\nShip small.\n");

        string composed = ComposeFor("Ada", dir.Path);

        Assert.Contains("Your Team follows a written process. Read it before you start work: Teams/Apollo/Process.md", composed);
        Assert.DoesNotContain("Ship small.", composed);
    }

    /// <summary>A Team with no <c>Process.md</c> gets neither block.</summary>
    [Fact]
    public void Compose_NoProcessFile_AddsNeitherBlock()
    {
        using TempDataDir dir = new();

        string manager = ComposeFor("Grace", dir.Path);
        string member = ComposeFor("Ada", dir.Path);

        Assert.DoesNotContain("This is your Team's process", manager);
        Assert.DoesNotContain("Your Team follows a written process", member);
    }

    /// <summary>An edit to <c>Process.md</c> between two composes reaches the second one, because the file is read each time.</summary>
    [Fact]
    public void Compose_ProcessEditedBetweenComposes_SecondComposeSeesEdit()
    {
        using TempDataDir dir = new();
        WriteProcess(dir.Path, "# Process\n\nFirst rule.\n");
        string first = ComposeFor("Grace", dir.Path);

        WriteProcess(dir.Path, "# Process\n\nSecond rule.\n");
        string second = ComposeFor("Grace", dir.Path);

        Assert.Contains("First rule.", first);
        Assert.Contains("Second rule.", second);
        Assert.DoesNotContain("First rule.", second);
    }

    /// <summary>Without a Teams root the builder adds no process block, so older callers are unchanged.</summary>
    [Fact]
    public void Build_NoTeamsRoot_HasNoProcessBlock()
    {
        RoleBlockInput? block = RoleBlockBuilder.Build(Identity("Grace"), [], Catalog(), _ => null);

        Assert.NotNull(block);
        Assert.Null(block.Process);
    }
}
