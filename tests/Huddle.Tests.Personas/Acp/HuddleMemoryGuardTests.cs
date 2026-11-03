using System.Text.Json;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Memory;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Teams;

namespace Agency.Huddle.Tests.Acp;

/// <summary>
/// Pins <see cref="TeamMemoryWriteGuard"/> for Huddle-wide Memory (Spec §6.6): a write below <c>Teams/memory/</c>, or a delete or move
/// of that folder itself, is refused unless the Persona is the Chief of Staff, while a Team's own <c>memory</c> folder keeps its rules.
/// </summary>
public sealed class HuddleMemoryGuardTests
{
    /// <summary>Settled text T7, character for character.</summary>
    private const string T7 = "Huddle-wide Memory is kept by the Chief of Staff. Ask the Chief of Staff to record it.";

    private static readonly string Root = Path.Combine(Path.GetTempPath(), "huddle-hmguard");

    private static readonly string TeamsRoot = Path.Combine(Root, "Teams");

    private static readonly string WorkDir = Path.Combine(Root, "Agents", "Nova", "work");

    /// <summary>Builds the guard: Ada is the Chief of Staff; Home is a Team managed by Bea with Nova as a member.</summary>
    private static TeamMemoryWriteGuard Guard()
    {
        FakeTeamCatalog catalog = new()
        {
            Teams = [new TeamSummary("Home", [], ["Nova"], true) { Manager = "Bea" }],
        };
        TeamMemoryAccess access = new(catalog, static name => string.Equals(name, "Ada", StringComparison.Ordinal));
        return new TeamMemoryWriteGuard(TeamsRoot, WorkDir, access, new FakePromptSource());
    }

    /// <summary>A pending tool call of one kind carrying one path below the Teams root.</summary>
    /// <param name="kind">The tool call's kind.</param>
    /// <param name="relative">The path under the Teams root, with forward slashes.</param>
    private static ToolCallInfo Call(ToolKind kind, string relative)
    {
        string path = Path.Combine(TeamsRoot, relative.Replace('/', Path.DirectorySeparatorChar));
        return new ToolCallInfo("call-1", "Tool", kind, ToolCallStatus.Pending, $$"""{"file_path":{{JsonSerializer.Serialize(path)}}}""");
    }

    /// <summary>A non-Chief Persona's write in Huddle-wide Memory, or a delete or move of the folder, is refused with T7.</summary>
    /// <param name="kind">The write kind.</param>
    /// <param name="relative">The path under the Teams root.</param>
    [Theory]
    [InlineData(ToolKind.Edit, "memory/x.md")]
    [InlineData(ToolKind.Edit, "memory/new.md")]
    [InlineData(ToolKind.Delete, "memory")]
    [InlineData(ToolKind.Move, "memory")]
    [InlineData(ToolKind.Edit, "MEMORY/x.md")]
    public void RefusalFor_NonChiefWritesHuddleMemory_IsT7(ToolKind kind, string relative)
    {
        ArgumentNullException.ThrowIfNull(relative);

        string? refusal = Guard().RefusalFor(Call(kind, relative), "Nova");

        Assert.Equal(T7, refusal);
    }

    /// <summary>The Chief of Staff writes, deletes and moves in Huddle-wide Memory freely.</summary>
    /// <param name="kind">The write kind.</param>
    /// <param name="relative">The path under the Teams root.</param>
    [Theory]
    [InlineData(ToolKind.Edit, "memory/x.md")]
    [InlineData(ToolKind.Edit, "memory/new.md")]
    [InlineData(ToolKind.Delete, "memory")]
    [InlineData(ToolKind.Move, "memory")]
    public void RefusalFor_ChiefWritesHuddleMemory_IsAllowed(ToolKind kind, string relative)
    {
        ArgumentNullException.ThrowIfNull(relative);

        string? refusal = Guard().RefusalFor(Call(kind, relative), "Ada");

        Assert.Null(refusal);
    }

    /// <summary>A read of Huddle-wide Memory is never refused.</summary>
    [Fact]
    public void RefusalFor_ReadOfHuddleMemory_IsAllowed()
    {
        string? refusal = Guard().RefusalFor(Call(ToolKind.Read, "memory/x.md"), "Nova");

        Assert.Null(refusal);
    }

    /// <summary>A Team's own memory folder keeps today's rule: refused with the manager-only text, not T7.</summary>
    [Fact]
    public void RefusalFor_TeamMemoryNeighbour_KeepsTheManagerOnlyRule()
    {
        string? refusal = Guard().RefusalFor(Call(ToolKind.Edit, "Home/memory/x.md"), "Nova");

        // Home's manager is Bea: the Team Memory refusal names her, as TeamMemoryWriteGuardTests pins for Dev.
        Assert.Equal("Only Home's manager, Bea, writes Home's Team Memory. Tell Bea, or keep it in your own Memory.", refusal);
    }

    /// <summary>The default of <c>systemPrompt.huddleMemoryRefusal</c> is T7, character for character.</summary>
    [Fact]
    public void HuddleMemoryRefusal_Default_IsT7()
    {
        Assert.Equal(T7, PromptCatalog.Get("systemPrompt.huddleMemoryRefusal").Default);
    }
}
