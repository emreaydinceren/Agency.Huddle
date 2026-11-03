namespace Agency.Huddle.Tests.Acp;

using System.Text.Json;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.Tests.Acp.Fakes;

/// <summary>
/// Pins <see cref="AgentsMdGuard"/> (Spec §6.6, SB-R09; corrections-7 §7.1): an Agent's edit, delete or move of an <c>AGENTS.md</c>
/// at the Teams root, in a Team folder or in a Project folder is refused with the settled text T5, ignoring case; deleting or moving a
/// folder that holds one is refused too, except the reserved <c>memory</c> folders; a read and every other path are allowed.
/// </summary>
public sealed class AgentsMdGuardTests : IDisposable
{
    private const string Refusal = "AGENTS.md is the Human's file. Propose a change in a retrospective instead.";

    private readonly string sandbox = Path.Combine(Path.GetTempPath(), "huddle-agentsmd-" + Guid.NewGuid().ToString("N"));

    private readonly string teamsRoot;

    private readonly string workDir;

    private readonly AgentsMdGuard guard;

    /// <summary>Creates the Teams root with the Team folder <c>Home</c>, its Project <c>Repairs</c> and both reserved <c>memory</c> folders.</summary>
    public AgentsMdGuardTests()
    {
        this.teamsRoot = Path.Combine(this.sandbox, "Teams");
        this.workDir = Path.Combine(this.sandbox, "Agents", "Nova", "work");
        foreach (string folder in new[] { "Home/Repairs/Deep", "Home/memory", "Home/outputs", "memory" })
        {
            _ = Directory.CreateDirectory(Path.Combine(this.teamsRoot, folder.Replace('/', Path.DirectorySeparatorChar)));
        }

        _ = Directory.CreateDirectory(this.workDir);
        this.guard = new AgentsMdGuard(this.teamsRoot, this.workDir, new FakePromptSource());
    }

    /// <summary>Removes the sandbox.</summary>
    public void Dispose() => Directory.Delete(this.sandbox, recursive: true);

    /// <summary>An edit, delete or move of an <c>AGENTS.md</c> at each of the three positions is refused with T5.</summary>
    /// <param name="kind">The write kind.</param>
    /// <param name="relative">The path below the Teams root.</param>
    [Theory]
    [InlineData(ToolKind.Edit, "AGENTS.md")]
    [InlineData(ToolKind.Delete, "AGENTS.md")]
    [InlineData(ToolKind.Move, "AGENTS.md")]
    [InlineData(ToolKind.Edit, "Home/AGENTS.md")]
    [InlineData(ToolKind.Delete, "Home/AGENTS.md")]
    [InlineData(ToolKind.Move, "Home/AGENTS.md")]
    [InlineData(ToolKind.Edit, "Home/Repairs/AGENTS.md")]
    [InlineData(ToolKind.Delete, "Home/Repairs/AGENTS.md")]
    [InlineData(ToolKind.Move, "Home/Repairs/AGENTS.md")]
    public void Write_ToAnAgentsMdFile_IsRefusedWithT5(ToolKind kind, string relative)
    {
        ArgumentNullException.ThrowIfNull(relative);
        string? reason = this.guard.RefusalFor(Call(kind, this.InTeams(relative)));

        Assert.Equal(Refusal, reason);
    }

    /// <summary>The path is compared ignoring case, as <c>PathContainment</c> does, so the result is the same on Linux.</summary>
    [Fact]
    public void Write_ToAnAgentsMdFileInAnotherCase_IsRefusedWithT5()
    {
        // Same folder as Home/AGENTS.md under the root, spelled "Teams/home/agents.md" with the root's own case kept.
        string path = Path.Combine(this.teamsRoot, "home", "agents.md");

        Assert.Equal(Refusal, this.guard.RefusalFor(Call(ToolKind.Edit, path)));
        Assert.Equal(Refusal, this.guard.RefusalFor(Call(ToolKind.Edit, path.ToUpperInvariant().Replace(this.sandbox.ToUpperInvariant(), this.sandbox, StringComparison.Ordinal))));
    }

    /// <summary>A relative path resolves against the Agent's Work Dir; one that climbs to the Team file is refused.</summary>
    [Fact]
    public void Write_ToARelativePath_ResolvesAgainstTheWorkDir()
    {
        // workDir = sandbox/Agents/Nova/work, so ../../../Teams/Home/AGENTS.md = sandbox/Teams/Home/AGENTS.md.
        string? reason = this.guard.RefusalFor(Call(ToolKind.Edit, "../../../Teams/Home/AGENTS.md"));

        Assert.Equal(Refusal, reason);
    }

    /// <summary>A move's destination is read: moving a file onto an <c>AGENTS.md</c> is refused.</summary>
    /// <param name="argument">The destination argument's name.</param>
    [Theory]
    [InlineData("new_path")]
    [InlineData("destination")]
    public void Move_ToAnAgentsMdFile_IsRefusedWithT5(string argument)
    {
        ArgumentNullException.ThrowIfNull(argument);
        string source = JsonSerializer.Serialize(Path.Combine(this.sandbox, "elsewhere", "a.md"));
        string destination = JsonSerializer.Serialize(this.InTeams("Home/AGENTS.md"));
        ToolCallInfo call = new("call-1", "Tool", ToolKind.Move, ToolCallStatus.Pending, $$"""{"old_path":{{source}},"{{argument}}":{{destination}}}""");

        Assert.Equal(Refusal, this.guard.RefusalFor(call));
    }

    /// <summary>Deleting or moving a folder that holds a guarded file takes it with it: the Teams root, a Team folder and a Project folder.</summary>
    /// <param name="kind">The delete or move kind.</param>
    /// <param name="relative">The path below the Teams root; empty is the root.</param>
    [Theory]
    [InlineData(ToolKind.Delete, "")]
    [InlineData(ToolKind.Move, "")]
    [InlineData(ToolKind.Delete, "Home")]
    [InlineData(ToolKind.Move, "Home")]
    [InlineData(ToolKind.Delete, "Home/Repairs")]
    [InlineData(ToolKind.Move, "Home/Repairs")]
    public void Remove_OfAFolderHoldingAnAgentsMd_IsRefusedWithT5(ToolKind kind, string relative)
    {
        ArgumentNullException.ThrowIfNull(relative);

        Assert.Equal(Refusal, this.guard.RefusalFor(Call(kind, this.InTeams(relative))));
    }

    /// <summary>A folder above the Teams root holds the root, so removing it is refused.</summary>
    [Fact]
    public void Delete_OfAnAncestorOfTheTeamsRoot_IsRefusedWithT5()
    {
        Assert.Equal(Refusal, this.guard.RefusalFor(Call(ToolKind.Delete, this.sandbox)));
    }

    /// <summary>The reserved <c>memory</c> folders hold no Team or Project, so removing them is not an AGENTS.md matter (corrections-7 §7.1 item 4).</summary>
    /// <param name="kind">The delete or move kind.</param>
    /// <param name="relative">The path below the Teams root.</param>
    [Theory]
    [InlineData(ToolKind.Delete, "memory")]
    [InlineData(ToolKind.Delete, "Home/memory")]
    [InlineData(ToolKind.Move, "memory")]
    [InlineData(ToolKind.Delete, "Home/outputs")]
    public void Remove_OfAReservedFolder_IsNotRefused(ToolKind kind, string relative)
    {
        ArgumentNullException.ThrowIfNull(relative);

        Assert.Null(this.guard.RefusalFor(Call(kind, this.InTeams(relative))));
    }

    /// <summary>A read of an <c>AGENTS.md</c> is never refused, wherever it is.</summary>
    /// <param name="relative">The path below the Teams root.</param>
    [Theory]
    [InlineData("AGENTS.md")]
    [InlineData("Home/AGENTS.md")]
    [InlineData("Home/Repairs/AGENTS.md")]
    public void Read_OfAnAgentsMdFile_IsAllowed(string relative)
    {
        ArgumentNullException.ThrowIfNull(relative);

        Assert.Null(this.guard.RefusalFor(Call(ToolKind.Read, this.InTeams(relative))));
    }

    /// <summary>
    /// Other files, and an <c>AGENTS.md</c> outside the three positions, are written as before. Spec §6.6 (the table at 476) lists exactly
    /// <c>Teams/AGENTS.md</c>, <c>Teams/&lt;T&gt;/AGENTS.md</c> and <c>Teams/&lt;T&gt;/&lt;P&gt;/AGENTS.md</c>; a fourth level is not a position,
    /// and <c>outputs</c> is a reserved name, not a Project (<c>TeamNames.IsReservedProjectName</c>), which task 65 resolves for depth 3 by
    /// the same reserved-name rule as <c>RawGuard.HoldsRawBelow</c> and is therefore not pinned here.
    /// </summary>
    /// <param name="relative">The path below the Teams root.</param>
    [Theory]
    [InlineData("Home/notes.md")]
    [InlineData("Home/Repairs/Deep/AGENTS.md")]
    [InlineData("Home/Repairs/notes.md")]
    [InlineData("memory/AGENTS.md.bak")]
    public void Write_ToAnythingElse_IsAllowed(string relative)
    {
        ArgumentNullException.ThrowIfNull(relative);

        Assert.Null(this.guard.RefusalFor(Call(ToolKind.Edit, this.InTeams(relative))));
    }

    /// <summary>Huddle's generated <c>AGENTS.md</c> in an Agent's Work Dir is not guarded here: the Work Dir level is gone.</summary>
    [Fact]
    public void Write_ToTheAgentsOwnWorkDirAgentsMd_IsAllowed()
    {
        Assert.Null(this.guard.RefusalFor(Call(ToolKind.Edit, Path.Combine(this.workDir, "AGENTS.md"))));
        Assert.Null(this.guard.RefusalFor(Call(ToolKind.Edit, Path.Combine(this.sandbox, "Agents", "Other", "work", "AGENTS.md"))));
    }

    /// <summary>Windows only: a write through an 8.3 short name or a stream name cannot be compared by name, so it is refused.</summary>
    /// <param name="relative">The path under the Teams root.</param>
    [Theory]
    [InlineData("AGENTS.md:stream")]
    [InlineData("HOME~1/AGENTS.md")]
    [InlineData("Home/AGENTS.md:stream")]
    public void Write_ThroughAnAlias_IsRefused(string relative)
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Short names and streams are Windows file system features.");
        ArgumentNullException.ThrowIfNull(relative);

        Assert.NotNull(this.guard.RefusalFor(Call(ToolKind.Edit, this.InTeams(relative))));
    }

    /// <summary>A path below the Teams root.</summary>
    /// <param name="relative">The path with forward slashes; empty is the root.</param>
    private string InTeams(string relative) =>
        Path.Combine(this.teamsRoot, relative.Replace('/', Path.DirectorySeparatorChar));

    /// <summary>Builds a tool call of one kind carrying one path argument.</summary>
    /// <param name="kind">The tool call's kind.</param>
    /// <param name="path">The path argument.</param>
    private static ToolCallInfo Call(ToolKind kind, string path) =>
        new("call-1", "Tool", kind, ToolCallStatus.Pending, $$"""{"file_path":{{JsonSerializer.Serialize(path)}}}""");
}
