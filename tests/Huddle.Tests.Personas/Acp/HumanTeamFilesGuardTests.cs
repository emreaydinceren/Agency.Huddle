using System.Text.Json;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Memory;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Teams;

namespace Agency.Huddle.Tests.Acp;

/// <summary>
/// Pins <see cref="HumanTeamFilesGuard"/> (Team Playbooks spec §7.6): an Agent's Edit, Write, Delete or Move of
/// <c>Teams/&lt;Team&gt;/_member-playbook.md</c> or <c>_manager-playbook.md</c> is refused with <c>systemPrompt.teamFileRefusal</c>; a read, the same
/// names in a reserved folder or at another depth, and every other path are allowed; an alias is refused outright.
/// </summary>
public sealed class HumanTeamFilesGuardTests : IDisposable
{
    private const string Refusal = "This file is the Human's. Propose a change in a retro, or ask the Human.";

    private readonly string sandbox = Path.Combine(Path.GetTempPath(), "huddle-humanfiles-" + Guid.NewGuid().ToString("N"));

    private readonly string teamsRoot;

    private readonly string workDir;

    private readonly HumanTeamFilesGuard guard;

    /// <summary>Creates the Teams root with the Team folder <c>Home</c>, its Project <c>Repairs</c> and the reserved <c>memory</c> and <c>outputs</c> folders.</summary>
    public HumanTeamFilesGuardTests()
    {
        this.teamsRoot = Path.Combine(this.sandbox, "Teams");
        this.workDir = Path.Combine(this.sandbox, "Agents", "Nova", "work");
        foreach (string folder in new[] { "Home/Repairs", "Home/memory", "Home/outputs", "memory" })
        {
            _ = Directory.CreateDirectory(Path.Combine(this.teamsRoot, folder.Replace('/', Path.DirectorySeparatorChar)));
        }

        _ = Directory.CreateDirectory(this.workDir);
        this.guard = new HumanTeamFilesGuard(this.teamsRoot, this.workDir, new FakePromptSource());
    }

    /// <summary>Removes the sandbox.</summary>
    public void Dispose() => Directory.Delete(this.sandbox, recursive: true);

    /// <summary>An Edit, Write, Delete or Move of either Playbook file in a Team folder is refused with the settled text.</summary>
    /// <param name="toolName">The tool's name; a Write arrives as an Edit-kind call.</param>
    /// <param name="kind">The call's kind.</param>
    /// <param name="relative">The path below the Teams root.</param>
    [Theory]
    [InlineData("Edit", ToolKind.Edit, "Home/_member-playbook.md")]
    [InlineData("Write", ToolKind.Edit, "Home/_member-playbook.md")]
    [InlineData("Delete", ToolKind.Delete, "Home/_member-playbook.md")]
    [InlineData("Move", ToolKind.Move, "Home/_member-playbook.md")]
    [InlineData("Edit", ToolKind.Edit, "Home/_manager-playbook.md")]
    [InlineData("Write", ToolKind.Edit, "Home/_manager-playbook.md")]
    [InlineData("Delete", ToolKind.Delete, "Home/_manager-playbook.md")]
    [InlineData("Move", ToolKind.Move, "Home/_manager-playbook.md")]
    public void Write_ToAPlaybookFile_IsRefusedWithTheSettledText(string toolName, ToolKind kind, string relative)
    {
        ArgumentNullException.ThrowIfNull(relative);

        Assert.Equal(Refusal, this.guard.RefusalFor(Call(toolName, kind, this.InTeams(relative))));
    }

    /// <summary>The refusal is the Prompt's default word for word, so a Human's override of the Prompt reaches the Agent.</summary>
    [Fact]
    public void Refusal_IsTheTeamFileRefusalPrompt()
    {
        FakePromptSource prompts = new();
        prompts.SetOverride("systemPrompt.teamFileRefusal", "Ask the Human.");
        HumanTeamFilesGuard overridden = new(this.teamsRoot, this.workDir, prompts);

        Assert.Equal("Ask the Human.", overridden.RefusalFor(Call("Edit", ToolKind.Edit, this.InTeams("Home/_member-playbook.md"))));
    }

    /// <summary>The path is compared ignoring case, as <c>AgentsMdGuard</c> does, so the result is the same on Linux.</summary>
    [Fact]
    public void Write_ToAPlaybookFileInAnotherCase_IsRefused()
    {
        string path = Path.Combine(this.teamsRoot, "home", "_MEMBER-Playbook.MD");

        Assert.Equal(Refusal, this.guard.RefusalFor(Call("Edit", ToolKind.Edit, path)));
    }

    /// <summary>A relative path resolves against the Agent's Work Dir; one that climbs to the Playbook file is refused.</summary>
    [Fact]
    public void Write_ToARelativePath_ResolvesAgainstTheWorkDir()
    {
        Assert.Equal(Refusal, this.guard.RefusalFor(Call("Edit", ToolKind.Edit, "../../../Teams/Home/_manager-playbook.md")));
    }

    /// <summary>A move's destination is read: moving a file onto a Playbook file is refused.</summary>
    /// <param name="argument">The destination argument's name.</param>
    [Theory]
    [InlineData("new_path")]
    [InlineData("destination")]
    public void Move_ToAPlaybookFile_IsRefused(string argument)
    {
        ArgumentNullException.ThrowIfNull(argument);
        string source = JsonSerializer.Serialize(Path.Combine(this.sandbox, "elsewhere", "a.md"));
        string destination = JsonSerializer.Serialize(this.InTeams("Home/_member-playbook.md"));
        ToolCallInfo call = new("call-1", "Tool", ToolKind.Move, ToolCallStatus.Pending, $$"""{"old_path":{{source}},"{{argument}}":{{destination}}}""");

        Assert.Equal(Refusal, this.guard.RefusalFor(call));
    }

    /// <summary>A read is never refused (spec D-4).</summary>
    /// <param name="relative">The path below the Teams root.</param>
    [Theory]
    [InlineData("Home/_member-playbook.md")]
    [InlineData("Home/_manager-playbook.md")]
    public void Read_OfAPlaybookFile_IsAllowed(string relative)
    {
        ArgumentNullException.ThrowIfNull(relative);

        Assert.Null(this.guard.RefusalFor(Call("Read", ToolKind.Read, this.InTeams(relative))));
    }

    /// <summary>
    /// The same names in <c>outputs</c> or a reserved folder, at another depth, and other files in the Team folder are written as before: the
    /// guard is exactly depth 2 below a Team that is not a reserved name.
    /// </summary>
    /// <param name="relative">The path below the Teams root.</param>
    [Theory]
    [InlineData("Home/outputs/_member-playbook.md")]
    [InlineData("Home/outputs/_manager-playbook.md")]
    [InlineData("Home/memory/_member-playbook.md")]
    [InlineData("memory/_member-playbook.md")]
    [InlineData("memory/_manager-playbook.md")]
    [InlineData("_member-playbook.md")]
    [InlineData("Home/Repairs/_member-playbook.md")]
    [InlineData("Home/notes.md")]
    [InlineData("Home/_member-playbook.md.bak")]
    [InlineData("Home/_team.md")]
    public void Write_ToAnythingElse_IsAllowed(string relative)
    {
        ArgumentNullException.ThrowIfNull(relative);

        Assert.Null(this.guard.RefusalFor(Call("Edit", ToolKind.Edit, this.InTeams(relative))));
    }

    /// <summary>A Playbook file in the Agent's own Work Dir is not a Team file.</summary>
    [Fact]
    public void Write_ToAPlaybookNamedFileInTheWorkDir_IsAllowed()
    {
        Assert.Null(this.guard.RefusalFor(Call("Edit", ToolKind.Edit, Path.Combine(this.workDir, "_member-playbook.md"))));
    }

    /// <summary>Windows only: a write through an 8.3 short name or a stream name cannot be compared by name, so it is refused with the alias text.</summary>
    /// <param name="relative">The path under the Teams root.</param>
    [Theory]
    [InlineData("Home/_member-playbook.md:stream")]
    [InlineData("Home/_manager-playbook.md:stream")]
    [InlineData("Home/_MEMBE~1.MD")]
    [InlineData("Home/_MANAGE~1.MD")]
    [InlineData("HOME~1/_member-playbook.md")]
    public void Write_ThroughAnAlias_IsRefusedOutright(string relative)
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Short names and streams are Windows file system features.");
        ArgumentNullException.ThrowIfNull(relative);
        string path = this.InTeams(relative);

        string? reason = this.guard.RefusalFor(Call("Edit", ToolKind.Edit, path));

        Assert.Equal($"{path} goes through a short name or a stream name, so it cannot be checked. Write the file by its full name.", reason);
    }

    /// <summary>Windows only: a short name or a stream outside the guarded names is an ordinary path.</summary>
    /// <param name="relative">The path under the Teams root.</param>
    [Theory]
    [InlineData("Home/outputs/_MEMBE~1.MD")]
    [InlineData("Home/outputs/x.md:Zone.Identifier")]
    [InlineData("Home/notes~2.md")]
    public void Write_ThroughAnAliasOutsideTheGuardedNames_IsAllowed(string relative)
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Short names and streams are Windows file system features.");
        ArgumentNullException.ThrowIfNull(relative);

        Assert.Null(this.guard.RefusalFor(Call("Edit", ToolKind.Edit, this.InTeams(relative))));
    }

    /// <summary>The guard sits right after the <c>AGENTS.md</c> guard in the list, with and without the protected-directory guard in front.</summary>
    /// <param name="withProtectedDirectory">Whether the <c>~/.claude</c> guard is in the list.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PermissionGuardList_PutsTheGuardRightAfterAgentsMd(bool withProtectedDirectory)
    {
        FakePromptSource prompts = new();
        string inert = Path.Combine(this.sandbox, "Inert");
        AgentsMdGuard agentsMd = new(this.teamsRoot, this.workDir, prompts);
        RawGuard raw = new(inert, Path.Combine(inert, "Raw"), Path.Combine(inert, "Archive"), this.workDir, prompts);

        IReadOnlyList<IToolCallGuard> guards = PermissionGuardList.InOrder(
            agentsMd,
            new TeamMemoryWriteGuard(inert, this.workDir, new TeamMemoryAccess(new FakeTeamCatalog(), static _ => false), prompts),
            raw,
            new PromptsRootGuard(Path.Combine(inert, "Prompts"), this.workDir, prompts),
            humanTeamFiles: this.guard,
            protectedDirectory: withProtectedDirectory ? new ProtectedDirectoryGuard(Path.Combine(this.sandbox, ".claude"), this.workDir) : null);

        int agentsMdAt = IndexOf(guards, agentsMd);
        Assert.Same(this.guard, guards[agentsMdAt + 1]);
        Assert.Same(raw, guards[agentsMdAt + 2]);
        Assert.Equal(withProtectedDirectory ? 1 : 0, agentsMdAt);
    }

    /// <summary>The handler refuses a Playbook edit through the list and approves another file.</summary>
    [Fact]
    public async Task Handler_WithTheGuardInTheList_RefusesAPlaybookEdit_AndApprovesAnotherFile()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakePromptSource prompts = new();
        string inert = Path.Combine(this.sandbox, "Inert");
        IReadOnlyList<IToolCallGuard> guards = PermissionGuardList.InOrder(
            new AgentsMdGuard(this.teamsRoot, this.workDir, prompts),
            new TeamMemoryWriteGuard(inert, this.workDir, new TeamMemoryAccess(new FakeTeamCatalog(), static _ => false), prompts),
            new RawGuard(inert, Path.Combine(inert, "Raw"), Path.Combine(inert, "Archive"), this.workDir, prompts),
            new PromptsRootGuard(Path.Combine(inert, "Prompts"), this.workDir, prompts),
            humanTeamFiles: this.guard);
        WorkDirPermissionHandler handler = new(Microsoft.Extensions.Logging.Abstractions.NullLogger<WorkDirPermissionHandler>.Instance, guards, workDir: this.workDir);

        PermissionDecision refused = await handler.DecideAsync(Request(this.InTeams("Home/_member-playbook.md")), ct);
        PermissionDecision allowed = await handler.DecideAsync(Request(this.InTeams("Home/notes.md")), ct);

        Assert.Equal("reject-1", Assert.IsType<SelectedDecision>(refused).OptionId);
        Assert.Equal("allow-1", Assert.IsType<SelectedDecision>(allowed).OptionId);
    }

    /// <summary>The position of a guard in the list, by reference.</summary>
    /// <param name="guards">The list.</param>
    /// <param name="guard">The guard to find.</param>
    private static int IndexOf(IReadOnlyList<IToolCallGuard> guards, IToolCallGuard guard)
    {
        for (int index = 0; index < guards.Count; index++)
        {
            if (ReferenceEquals(guards[index], guard))
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>A permission request for an Edit-kind call carrying one path argument, with an allow and a reject option.</summary>
    /// <param name="path">The path argument.</param>
    private static PermissionRequestContext Request(string path) =>
        new(
            "session-1",
            Call("Edit", ToolKind.Edit, path),
            [
                new PermissionOptionInfo("allow-1", "Allow once", PermissionOptionKind.AllowOnce),
                new PermissionOptionInfo("reject-1", "Reject once", PermissionOptionKind.RejectOnce),
            ]);

    /// <summary>A path below the Teams root.</summary>
    /// <param name="relative">The path with forward slashes.</param>
    private string InTeams(string relative) =>
        Path.Combine(this.teamsRoot, relative.Replace('/', Path.DirectorySeparatorChar));

    /// <summary>Builds a tool call of one kind carrying one path argument.</summary>
    /// <param name="toolName">The tool's name.</param>
    /// <param name="kind">The tool call's kind.</param>
    /// <param name="path">The path argument.</param>
    private static ToolCallInfo Call(string toolName, ToolKind kind, string path) =>
        new("call-1", toolName, kind, ToolCallStatus.Pending, $$"""{"file_path":{{JsonSerializer.Serialize(path)}}}""");
}
