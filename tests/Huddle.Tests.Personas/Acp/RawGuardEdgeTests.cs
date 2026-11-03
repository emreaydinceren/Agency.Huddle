using System.Text.Json;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.Tests.Acp.Fakes;

namespace Agency.Huddle.Tests.Acp;

/// <summary>Pins the edges of <see cref="RawGuard"/>: stream names, Project folders, the roots themselves and a different-case root spelling.</summary>
public sealed class RawGuardEdgeTests : IDisposable
{
    private const string Refusal = "refused-raw";
    private const string AliasRefusal = "refused-alias";

    private readonly string baseDir = Path.Combine(Path.GetTempPath(), "huddle-rawedge-" + Guid.NewGuid().ToString("N"));

    /// <summary>Builds the temp tree with an original in Raw, one in a Team's raw and an existing Project folder.</summary>
    public RawGuardEdgeTests()
    {
        foreach (string relative in new[] { "Raw/r1/a.pdf", "Teams/Home/raw/r1/a.pdf", "Teams/Home/Repairs/notes.md", "Archive/Home/raw/a.pdf" })
        {
            string full = this.Full(relative);
            _ = Directory.CreateDirectory(Path.Combine(full, ".."));
            File.WriteAllBytes(full, [1]);
        }

        _ = Directory.CreateDirectory(this.WorkDir);
    }

    private string WorkDir => this.Full("Agents/Nova/work");

    /// <summary>Removes the temp tree.</summary>
    public void Dispose()
    {
        Directory.Delete(this.baseDir, recursive: true);
    }

    /// <summary>A stream name on a path in the Raw root or the Archive is refused with the alias text, not the raw one.</summary>
    /// <param name="relative">The path below the temp tree.</param>
    [Theory]
    [InlineData("Raw/r1/a.pdf:s")]
    [InlineData("Archive/Home/raw/a.pdf:s")]
    public void RefusalFor_AStreamNameInRawOrArchive_IsRefusedAsAnAlias(string relative)
    {
        ArgumentNullException.ThrowIfNull(relative);

        string? refusal = this.Guard().RefusalFor(Call(ToolKind.Edit, this.Full(relative)));

        Assert.Equal(AliasRefusal, refusal);
    }

    /// <summary>Deleting a Project folder, the Teams root or the Archive root is refused, as each holds an original or a place for one.</summary>
    /// <param name="relative">The path below the temp tree.</param>
    [Theory]
    [InlineData("Teams/Home/Repairs")]
    [InlineData("Teams")]
    [InlineData("Archive")]
    public void RefusalFor_DeletingAFolderHoldingRaw_IsRefused(string relative)
    {
        ArgumentNullException.ThrowIfNull(relative);

        string? refusal = this.Guard().RefusalFor(Call(ToolKind.Delete, this.Full(relative)));

        Assert.Equal(Refusal, refusal);
    }

    /// <summary>An edit spelled with the Teams root in another case is judged as the same path where the file system ignores case.</summary>
    [Fact]
    public void RefusalFor_ARootSpelledInAnotherCase_IsRefused()
    {
        string path = Path.Combine(this.baseDir, "TEAMS", "Home", "raw", "r1", "a.pdf");

        string? refusal = this.Guard().RefusalFor(Call(ToolKind.Edit, path));

        Assert.Equal(OperatingSystem.IsWindows() ? Refusal : null, refusal);
    }

    /// <summary>Builds the guard under test over the temp tree, with both refusal texts set to markers.</summary>
    /// <returns>The guard.</returns>
    private RawGuard Guard()
    {
        FakePromptSource prompts = new();
        prompts.SetOverride("systemPrompt.rawRefusal", Refusal);
        prompts.SetOverride("systemPrompt.pathAliasRefusal", AliasRefusal);
        return new RawGuard(this.Full("Teams"), this.Full("Raw"), this.Full("Archive"), this.WorkDir, prompts);
    }

    /// <summary>A path below the temp tree.</summary>
    /// <param name="relative">The path with forward slashes.</param>
    /// <returns>The full path.</returns>
    private string Full(string relative) =>
        Path.Combine(this.baseDir, relative.Replace('/', Path.DirectorySeparatorChar));

    /// <summary>A pending tool call of one kind carrying one path argument.</summary>
    /// <param name="kind">The tool call's kind.</param>
    /// <param name="path">The path argument.</param>
    /// <returns>The call.</returns>
    private static ToolCallInfo Call(ToolKind kind, string path) =>
        new("call-1", "Tool", kind, ToolCallStatus.Pending, $$"""{"file_path":{{JsonSerializer.Serialize(path)}}}""");
}
