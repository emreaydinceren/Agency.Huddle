namespace Agency.Huddle.Acp.Tests.Console;

using System.IO;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.Console;
using Xunit;

/// <summary>
/// Covers <see cref="Program.ParseArguments"/>'s handling of the --system-prompt family of flags.
/// REQUIRED VISIBILITY CHANGE for the implementing agent: <c>ParseArguments</c> is private today.
/// These tests call it directly, so it must become <c>internal static</c> (InternalsVisibleTo
/// Team.Acp.Tests is already declared in Team.Console.csproj). Its return type must also widen to
/// carry the parsed <see cref="SystemPromptOptions"/>; these tests assume a 4-tuple
/// <c>(string Cwd, bool AutoApprove, bool TraceWire, SystemPromptOptions? SystemPrompt)?</c>, matching
/// the existing 3-tuple convention with one member appended. If the implementing agent instead
/// introduces a named type, update the destructuring below to match - the assertions themselves do
/// not need to change.
/// </summary>
public sealed class ProgramArgumentTests
{
    [Fact]
    public void SystemPromptFlag_WithText_ParsesAppendModeText()
    {
        (string Cwd, bool AutoApprove, bool TraceWire, SystemPromptOptions? SystemPrompt)? parsed =
            Program.ParseArguments(["--system-prompt", "You are the COO"]);

        Assert.NotNull(parsed);
        Assert.NotNull(parsed.Value.SystemPrompt);
        Assert.Equal("You are the COO", parsed.Value.SystemPrompt!.Text);
        Assert.Equal(SystemPromptMode.Append, parsed.Value.SystemPrompt!.Mode);
    }

    [Fact]
    public void SystemPromptFileFlag_WithExistingFile_ParsesTextFromFileContentsAppendMode()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "You are the COO");

            (string Cwd, bool AutoApprove, bool TraceWire, SystemPromptOptions? SystemPrompt)? parsed =
                Program.ParseArguments(["--system-prompt-file", path]);

            Assert.NotNull(parsed);
            Assert.NotNull(parsed.Value.SystemPrompt);
            Assert.Equal("You are the COO", parsed.Value.SystemPrompt!.Text);
            Assert.Equal(SystemPromptMode.Append, parsed.Value.SystemPrompt!.Mode);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void SystemPromptFlagAndSystemPromptFileFlag_Together_ParseFailure()
    {
        (string Cwd, bool AutoApprove, bool TraceWire, SystemPromptOptions? SystemPrompt)? parsed =
            Program.ParseArguments(["--system-prompt", "text", "--system-prompt-file", "path.txt"]);

        Assert.Null(parsed);
    }

    [Fact]
    public void SystemPromptFlag_MissingValue_ParseFailure()
    {
        (string Cwd, bool AutoApprove, bool TraceWire, SystemPromptOptions? SystemPrompt)? parsed =
            Program.ParseArguments(["--system-prompt"]);

        Assert.Null(parsed);
    }

    [Fact]
    public void SystemPromptFlag_WhitespaceValue_ParseFailure()
    {
        // ParseArguments runs before Main's try/catch, so a whitespace value must be
        // reported as usage here rather than thrown out of the SystemPromptOptions ctor.
        (string Cwd, bool AutoApprove, bool TraceWire, SystemPromptOptions? SystemPrompt)? parsed =
            Program.ParseArguments(["--system-prompt", "   "]);

        Assert.Null(parsed);
    }

    [Fact]
    public void SystemPromptReplaceFlag_WithText_ParsesReplaceMode()
    {
        (string Cwd, bool AutoApprove, bool TraceWire, SystemPromptOptions? SystemPrompt)? parsed =
            Program.ParseArguments(["--system-prompt", "You are the COO", "--system-prompt-replace"]);

        Assert.NotNull(parsed);
        Assert.NotNull(parsed.Value.SystemPrompt);
        Assert.Equal(SystemPromptMode.Replace, parsed.Value.SystemPrompt!.Mode);
    }

    [Fact]
    public void SystemPromptReplaceFlag_Alone_ParseFailure()
    {
        (string Cwd, bool AutoApprove, bool TraceWire, SystemPromptOptions? SystemPrompt)? parsed =
            Program.ParseArguments(["--system-prompt-replace"]);

        Assert.Null(parsed);
    }

    [Fact]
    public void NoSystemPromptFlags_SystemPromptIsNull_ExistingFlagsUnaffected()
    {
        (string Cwd, bool AutoApprove, bool TraceWire, SystemPromptOptions? SystemPrompt)? parsed =
            Program.ParseArguments(["--cwd", Path.GetTempPath(), "--auto-approve"]);

        Assert.NotNull(parsed);
        Assert.Null(parsed.Value.SystemPrompt);
        Assert.True(parsed.Value.AutoApprove);
    }
}
