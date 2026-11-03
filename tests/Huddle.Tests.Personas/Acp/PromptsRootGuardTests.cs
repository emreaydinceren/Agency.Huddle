namespace Agency.Huddle.Tests.Acp;

using System.Text.Json;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.Tests.Acp.Fakes;

/// <summary>
/// Pins <see cref="PromptsRootGuard"/> (Spec §6.6, SB-R09; corrections-9 §9.3): an Agent's edit (creating or overwriting), delete or move of
/// anything in the Prompts folder is refused with the settled text T6, ignoring case; deleting or moving the folder itself or a folder above
/// it is refused too; a move whose destination is in the folder is refused; a read and every other path are allowed.
/// </summary>
public sealed class PromptsRootGuardTests : IDisposable
{
    private const string Refusal = "Files in the Prompts folder are Huddle's prompts and only the Human changes them.";

    private readonly string dataDir = Path.Combine(Path.GetTempPath(), "huddle-promptsroot-" + Guid.NewGuid().ToString("N"));

    private readonly string promptsRoot;

    private readonly string workDir;

    private readonly PromptsRootGuard guard;

    /// <summary>Creates a Data Dir with a Prompts folder holding one prompt file, a Team folder <c>Home</c> and an Agent Work Dir.</summary>
    public PromptsRootGuardTests()
    {
        this.promptsRoot = Path.Combine(this.dataDir, "Prompts");
        this.workDir = Path.Combine(this.dataDir, "Agents", "Nova", "work");
        _ = Directory.CreateDirectory(this.promptsRoot);
        _ = Directory.CreateDirectory(Path.Combine(this.dataDir, "Teams", "Home"));
        _ = Directory.CreateDirectory(this.workDir);
        File.WriteAllText(Path.Combine(this.promptsRoot, "x.md"), "text");
        this.guard = new PromptsRootGuard(this.promptsRoot, this.workDir, new FakePromptSource());
    }

    /// <summary>Removes the Data Dir.</summary>
    public void Dispose() => Directory.Delete(this.dataDir, recursive: true);

    /// <summary>T6 is the catalog default, word for word, with no placeholder, applying at the next session.</summary>
    [Fact]
    public void PromptsRefusal_DefaultIsT6()
    {
        PromptDefinition prompt = PromptCatalog.Get("systemPrompt.promptsRefusal");

        Assert.Equal(Refusal, prompt.Default);
        Assert.Equal(PromptTiming.NextSession, prompt.Timing);
        Assert.Empty(prompt.Placeholders);
    }

    /// <summary>An edit (overwriting <c>x.md</c> or creating <c>new.md</c>), delete or move of a file in the Prompts folder is refused with T6.</summary>
    /// <param name="kind">The write kind.</param>
    /// <param name="file">The file name in the Prompts folder.</param>
    [Theory]
    [InlineData(ToolKind.Edit, "x.md")]
    [InlineData(ToolKind.Edit, "new.md")]
    [InlineData(ToolKind.Delete, "x.md")]
    [InlineData(ToolKind.Move, "x.md")]
    public void Write_ToAFileInThePromptsFolder_IsRefusedWithT6(ToolKind kind, string file)
    {
        ArgumentNullException.ThrowIfNull(file);

        Assert.Equal(Refusal, this.guard.RefusalFor(Call(kind, Path.Combine(this.promptsRoot, file))));
    }

    /// <summary>The path is compared ignoring case, so the result is the same on Linux, where <c>PROMPTS/x.md</c> is another path.</summary>
    [Fact]
    public void Write_ToThePromptsFolderInAnotherCase_IsRefusedWithT6()
    {
        string upper = Path.Combine(this.dataDir, "PROMPTS", "X.MD");

        Assert.Equal(Refusal, this.guard.RefusalFor(Call(ToolKind.Edit, upper)));
        Assert.Equal(Refusal, this.guard.RefusalFor(Call(ToolKind.Edit, this.promptsRoot.ToUpperInvariant().Replace(this.dataDir.ToUpperInvariant(), this.dataDir, StringComparison.Ordinal))));
    }

    /// <summary>A relative path resolves against the Work Dir: <c>../../../Prompts/x.md</c> from <c>Agents/Nova/work</c> is the Prompts file.</summary>
    [Fact]
    public void Write_ToARelativePath_ResolvesAgainstTheWorkDir()
    {
        Assert.Equal(Refusal, this.guard.RefusalFor(Call(ToolKind.Edit, "../../../Prompts/x.md")));
    }

    /// <summary>Deleting or moving the Prompts folder, or a folder above it (the Data Dir and its parent), takes the prompts with it.</summary>
    /// <param name="kind">The delete or move kind.</param>
    /// <param name="levelsUp">How many folders above the Prompts folder: 0 is the folder itself, 1 the Data Dir, 2 its parent.</param>
    [Theory]
    [InlineData(ToolKind.Delete, 0)]
    [InlineData(ToolKind.Move, 0)]
    [InlineData(ToolKind.Delete, 1)]
    [InlineData(ToolKind.Move, 1)]
    [InlineData(ToolKind.Delete, 2)]
    public void Remove_OfThePromptsFolderOrAnAncestor_IsRefusedWithT6(ToolKind kind, int levelsUp)
    {
        string path = this.promptsRoot;
        for (int level = 0; level < levelsUp; level++)
        {
            path = Path.GetFullPath(Path.Combine(path, ".."));
        }

        Assert.Equal(Refusal, this.guard.RefusalFor(Call(kind, path)));
    }

    /// <summary>A move's destination is read: moving a file from elsewhere into the Prompts folder is refused.</summary>
    /// <param name="argument">The destination argument's name.</param>
    [Theory]
    [InlineData("new_path")]
    [InlineData("destination")]
    public void Move_IntoThePromptsFolder_IsRefusedWithT6(string argument)
    {
        ArgumentNullException.ThrowIfNull(argument);
        string source = JsonSerializer.Serialize(Path.Combine(this.dataDir, "Teams", "Home", "a.md"));
        string destination = JsonSerializer.Serialize(Path.Combine(this.promptsRoot, "x.md"));
        ToolCallInfo call = new("call-1", "Tool", ToolKind.Move, ToolCallStatus.Pending, $$"""{"old_path":{{source}},"{{argument}}":{{destination}}}""");

        Assert.Equal(Refusal, this.guard.RefusalFor(call));
    }

    /// <summary>Windows only: a write to a prompt file through an 8.3 short name or a stream name is still inside the Prompts folder, so it is refused.</summary>
    /// <param name="file">The file name below the Prompts folder.</param>
    [Theory]
    [InlineData("X~1.MD")]
    [InlineData("x.md:stream")]
    public void Write_ThroughAnAliasInThePromptsFolder_IsRefused(string file)
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Short names and streams are Windows file system features.");
        ArgumentNullException.ThrowIfNull(file);

        Assert.NotNull(this.guard.RefusalFor(Call(ToolKind.Edit, Path.Combine(this.promptsRoot, file))));
    }

    /// <summary>A read of a prompt file, or of the folder, is never refused.</summary>
    [Fact]
    public void Read_OfAPromptFile_IsAllowed()
    {
        Assert.Null(this.guard.RefusalFor(Call(ToolKind.Read, Path.Combine(this.promptsRoot, "x.md"))));
        Assert.Null(this.guard.RefusalFor(Call(ToolKind.Read, this.promptsRoot)));
    }

    /// <summary>Other places are written as before, including a sibling whose name only starts with <c>Prompts</c>.</summary>
    /// <param name="relative">The path below the Data Dir.</param>
    [Theory]
    [InlineData("Teams/Home/notes.md")]
    [InlineData("PromptsBackup/x.md")]
    [InlineData("Agents/Nova/work/x.md")]
    public void Write_ToAnythingElse_IsAllowed(string relative)
    {
        ArgumentNullException.ThrowIfNull(relative);

        Assert.Null(this.guard.RefusalFor(Call(ToolKind.Edit, Path.Combine(this.dataDir, relative.Replace('/', Path.DirectorySeparatorChar)))));
    }

    /// <summary>Builds a tool call of one kind carrying one path argument.</summary>
    /// <param name="kind">The tool call's kind.</param>
    /// <param name="path">The path argument.</param>
    private static ToolCallInfo Call(ToolKind kind, string path) =>
        new("call-1", "Tool", kind, ToolCallStatus.Pending, $$"""{"file_path":{{JsonSerializer.Serialize(path)}}}""");
}
