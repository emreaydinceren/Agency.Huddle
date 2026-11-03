using System.Text.Json;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.Tests.Acp.Fakes;

namespace Agency.Huddle.Tests.Acp;

/// <summary>
/// Pins <see cref="RawGuard"/> (Spec §8.6, SB-R05): an Agent may not edit, delete or move an original in a <c>raw/</c> folder,
/// in the Raw root or in the Archive, but may add a new file beside it, read it, and write in every other folder.
/// </summary>
public sealed class RawGuardTests : IDisposable
{
    private const string Refusal =
        "Files in raw/ are kept as they arrived. Add a new file beside the original instead, for example \"<name> - extract.md\".";

    private static readonly string[] ExistingFiles =
    [
        "Teams/Home/raw/r1/a.pdf",
        "Teams/Home/Repairs/RAW/r1/a.pdf",
        "Raw/r1/a.pdf",
        "Archive/Home/raw/r1/a.pdf",
        "Teams/Home/outputs/raw/x.md",
        "Teams/Home/memory/raw/x.md",
        "Teams/Home/notes.md",
        "Teams/memory/raw/x.md",
        "Teams/Home/outputs/old.md",
    ];

    private readonly string baseDir = Path.Combine(Path.GetTempPath(), "huddle-rawguard-" + Guid.NewGuid().ToString("N"));

    /// <summary>Builds the temp tree: Teams, Raw, Archive and an Agent's Work Dir, with every file in <see cref="ExistingFiles"/>.</summary>
    public RawGuardTests()
    {
        foreach (string relative in ExistingFiles)
        {
            string full = this.Full(relative);
            _ = Directory.CreateDirectory(Path.Combine(full, ".."));
            File.WriteAllBytes(full, [1]);
        }

        Directory.CreateDirectory(this.WorkDir);
    }

    private string WorkDir => this.Full("Agents/Nova/work");

    /// <summary>Removes the temp tree.</summary>
    public void Dispose()
    {
        Directory.Delete(this.baseDir, recursive: true);
    }

    /// <summary>An edit of an existing original, and a delete or move of one, is refused with the settled text, in each raw place and spelling of its name.</summary>
    /// <param name="kind">The tool kind.</param>
    /// <param name="relative">The path below the temp tree.</param>
    [Theory]
    [InlineData(ToolKind.Delete, "Teams/Home/raw/r1/a.pdf")]
    [InlineData(ToolKind.Move, "Teams/Home/raw/r1/a.pdf")]
    [InlineData(ToolKind.Edit, "Teams/Home/raw/r1/a.pdf")]
    [InlineData(ToolKind.Edit, "Teams/Home/Repairs/RAW/r1/a.pdf")]
    [InlineData(ToolKind.Edit, "Raw/r1/a.pdf")]
    [InlineData(ToolKind.Edit, "Archive/Home/raw/r1/a.pdf")]
    [InlineData(ToolKind.Delete, "Teams/Home")]
    [InlineData(ToolKind.Delete, "Raw")]
    public void RefusalFor_AnOriginalOrAFolderHoldingOne_IsRefused(ToolKind kind, string relative)
    {
        ArgumentNullException.ThrowIfNull(relative);

        string? refusal = this.Guard().RefusalFor(Call(kind, this.Full(relative)));

        Assert.Equal(Refusal, refusal);
    }

    /// <summary>A path written with backslash separators is read as the same path, on every operating system.</summary>
    [Fact]
    public void RefusalFor_APathWithBackslashes_IsRefused()
    {
        string path = this.Full("Teams") + @"\Home\raw\r1\a.pdf";

        string? refusal = this.Guard().RefusalFor(Call(ToolKind.Edit, path));

        Assert.Equal(Refusal, refusal);
    }

    /// <summary>A relative path is resolved against the Work Dir before it is judged.</summary>
    [Fact]
    public void RefusalFor_ARelativePathLandingInRaw_IsRefused()
    {
        string? refusal = this.Guard().RefusalFor(Call(ToolKind.Edit, "../../../Teams/Home/raw/r1/a.pdf"));

        Assert.Equal(Refusal, refusal);
    }

    /// <summary>Adding a file beside an original, reading an original, and writing anywhere that is not a raw position is left alone.</summary>
    /// <param name="kind">The tool kind.</param>
    /// <param name="relative">The path below the temp tree.</param>
    [Theory]
    [InlineData(ToolKind.Edit, "Teams/Home/raw/r1/a - extract.md")]
    [InlineData(ToolKind.Read, "Teams/Home/raw/r1/a.pdf")]
    [InlineData(ToolKind.Edit, "Teams/Home/outputs/raw/x.md")]
    [InlineData(ToolKind.Edit, "Teams/Home/memory/raw/x.md")]
    [InlineData(ToolKind.Edit, "Teams/Home/notes.md")]
    [InlineData(ToolKind.Edit, "Teams/memory/raw/x.md")]
    [InlineData(ToolKind.Delete, "Teams/Home/outputs/old.md")]
    public void RefusalFor_ANonRawWrite_IsAllowed(ToolKind kind, string relative)
    {
        ArgumentNullException.ThrowIfNull(relative);

        string? refusal = this.Guard().RefusalFor(Call(kind, this.Full(relative)));

        Assert.Null(refusal);
    }

    /// <summary>Builds the guard under test over the temp tree, with the refusal text set to the settled one.</summary>
    private RawGuard Guard()
    {
        FakePromptSource prompts = new();
        prompts.SetOverride("systemPrompt.rawRefusal", Refusal);
        return new RawGuard(this.Full("Teams"), this.Full("Raw"), this.Full("Archive"), this.WorkDir, prompts);
    }

    /// <summary>A path below the temp tree.</summary>
    /// <param name="relative">The path with forward slashes.</param>
    private string Full(string relative) =>
        Path.Combine(this.baseDir, relative.Replace('/', Path.DirectorySeparatorChar));

    /// <summary>A pending tool call of one kind carrying one path argument.</summary>
    /// <param name="kind">The tool call's kind.</param>
    /// <param name="path">The path argument.</param>
    private static ToolCallInfo Call(ToolKind kind, string path) =>
        new("call-1", "Tool", kind, ToolCallStatus.Pending, $$"""{"file_path":{{JsonSerializer.Serialize(path)}}}""");
}
