using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.DocHistory;

namespace Agency.Huddle.Tests.DocHistory;

/// <summary>Pins who gets the read reminder (#354): <see cref="DocHistoryStopHook.Install"/> adds <c>--remind-reads</c> to the hook command only when asked, and a later start moves it either way.</summary>
public sealed class DocHistoryStopHookRemindReadsTests
{
    private const string Tool = "mcp__team__log_change";

    /// <summary>Install for a reviewer adds the flag after the tool name.</summary>
    [Fact]
    public void Install_Reviewer_AddsTheFlag()
    {
        using TempDataDir dir = new();
        string workDir = Path.Combine(dir.Path, "work");

        DocHistoryStopHook.Install(workDir, dir.Path, Tool, NullLogger.Instance, remindReads: true);

        Assert.EndsWith($" {Tool} --remind-reads", CommandIn(workDir), StringComparison.Ordinal); // contains-ok: the command's tail is the flag's contract
    }

    /// <summary>Install for anyone else leaves the flag out, so the command is the tool name alone.</summary>
    [Fact]
    public void Install_NotAReviewer_LeavesTheFlagOut()
    {
        using TempDataDir dir = new();
        string workDir = Path.Combine(dir.Path, "work");

        DocHistoryStopHook.Install(workDir, dir.Path, Tool, NullLogger.Instance, remindReads: false);

        string command = CommandIn(workDir);
        Assert.EndsWith($" {Tool}", command, StringComparison.Ordinal); // contains-ok: the command's tail is the tool name when there is no flag
        Assert.DoesNotContain("--remind-reads", command, StringComparison.Ordinal);
    }

    /// <summary>A second start replaces the entry in both directions, leaves one entry, and keeps the other settings and hooks.</summary>
    [Fact]
    public void Install_SecondStart_UpdatesTheEntryAndKeepsOtherSettings()
    {
        using TempDataDir dir = new();
        string workDir = Path.Combine(dir.Path, "work");
        Directory.CreateDirectory(Path.Combine(workDir, ".claude"));
        File.WriteAllText(
            Path.Combine(workDir, ".claude", WorkDirHookSettings.SettingsFile),
            """{"model":"haiku","hooks":{"Stop":[{"hooks":[{"type":"command","command":"echo mine"}]}]}}""");

        DocHistoryStopHook.Install(workDir, dir.Path, Tool, NullLogger.Instance);
        Assert.DoesNotContain("--remind-reads", File.ReadAllText(SettingsPath(workDir)), StringComparison.Ordinal);

        DocHistoryStopHook.Install(workDir, dir.Path, Tool, NullLogger.Instance, remindReads: true);
        Assert.Contains("--remind-reads", File.ReadAllText(SettingsPath(workDir)), StringComparison.Ordinal); // contains-ok: a command-line flag in a JSON settings file

        DocHistoryStopHook.Install(workDir, dir.Path, Tool, NullLogger.Instance, remindReads: false);
        string text = File.ReadAllText(SettingsPath(workDir));
        Assert.DoesNotContain("--remind-reads", text, StringComparison.Ordinal);
        JsonObject settings = JsonNode.Parse(text) as JsonObject ?? throw new InvalidOperationException("The settings file is not a JSON object.");
        Assert.Equal("haiku", (string)settings["model"]!);
        Assert.Equal(2, ((JsonArray)settings["hooks"]!["Stop"]!).Count);
        Assert.Contains("echo mine", text, StringComparison.Ordinal); // contains-ok: another hook's command in a JSON settings file
    }

    private static string SettingsPath(string workDir) => Path.Combine(workDir, ".claude", WorkDirHookSettings.SettingsFile);

    private static string CommandIn(string workDir)
    {
        JsonObject settings = JsonNode.Parse(File.ReadAllText(SettingsPath(workDir))) as JsonObject ?? throw new InvalidOperationException("The settings file is not a JSON object.");
        return settings["hooks"]?["Stop"]?[0]?["hooks"]?[0]?["command"]?.GetValue<string>() ?? throw new InvalidOperationException("The Stop hook entry has no command.");
    }
}
