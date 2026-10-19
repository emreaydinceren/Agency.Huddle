using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.DocHistory;

namespace Agency.Huddle.Tests.DocHistory;

/// <summary>Pins what <see cref="DocHistoryStopHook.Install"/> leaves on disk: the script, one settings entry that names the tool, and every setting that was already there.</summary>
public sealed class DocHistoryStopHookInstallTests
{
    private const string Tool = "mcp__team__log_change";

    /// <summary>Install writes the script under the data folder and a Stop hook in the Work Dir's local settings that runs it with the tool's name.</summary>
    [Fact]
    public void Install_WritesTheScriptAndTheHookEntry()
    {
        using TempDataDir dir = new();
        string workDir = Path.Combine(dir.Path, "work");

        DocHistoryStopHook.Install(workDir, dir.Path, Tool, NullLogger.Instance);

        string script = Path.Combine(dir.Path, "hooks", DocHistoryStopHook.ScriptName);
        Assert.Equal(DocHistoryStopHook.Script, File.ReadAllText(script));
        JsonObject settings = ReadSettings(workDir);
        string command = (string)settings["hooks"]!["Stop"]![0]!["hooks"]![0]!["command"]!;
        Assert.Equal($"node \"{script}\" {Tool}", command);
    }

    /// <summary>Install twice leaves one entry, and a changed tool name replaces the old one.</summary>
    [Fact]
    public void Install_Twice_KeepsOneEntryAndTheLatestName()
    {
        using TempDataDir dir = new();
        string workDir = Path.Combine(dir.Path, "work");

        DocHistoryStopHook.Install(workDir, dir.Path, Tool, NullLogger.Instance);
        DocHistoryStopHook.Install(workDir, dir.Path, "log_change", NullLogger.Instance);

        JsonArray stop = (JsonArray)ReadSettings(workDir)["hooks"]!["Stop"]!;
        string command = (string)Assert.Single(stop)!["hooks"]![0]!["command"]!;
        Assert.EndsWith(" log_change", command, StringComparison.Ordinal);
    }

    /// <summary>Settings and hooks that were already there stay.</summary>
    [Fact]
    public void Install_KeepsOtherSettingsAndOtherHooks()
    {
        using TempDataDir dir = new();
        string workDir = Path.Combine(dir.Path, "work");
        Directory.CreateDirectory(Path.Combine(workDir, ".claude"));
        File.WriteAllText(
            Path.Combine(workDir, ".claude", DocHistoryStopHook.SettingsFile),
            """{"model":"haiku","hooks":{"Stop":[{"hooks":[{"type":"command","command":"echo mine"}]}],"PreToolUse":[]}}""");

        DocHistoryStopHook.Install(workDir, dir.Path, Tool, NullLogger.Instance);

        JsonObject settings = ReadSettings(workDir);
        Assert.Equal("haiku", (string)settings["model"]!);
        Assert.Equal(2, ((JsonArray)settings["hooks"]!["Stop"]!).Count);
        Assert.Contains("echo mine", settings["hooks"]!["Stop"]!.ToJsonString(), StringComparison.Ordinal);
        Assert.NotNull(settings["hooks"]!["PreToolUse"]);
    }

    /// <summary>A settings file that is not JSON is left exactly as it was.</summary>
    [Fact]
    public void Install_InvalidSettings_AreLeftAlone()
    {
        using TempDataDir dir = new();
        string workDir = Path.Combine(dir.Path, "work");
        Directory.CreateDirectory(Path.Combine(workDir, ".claude"));
        string path = Path.Combine(workDir, ".claude", DocHistoryStopHook.SettingsFile);
        File.WriteAllText(path, "{ not json");

        DocHistoryStopHook.Install(workDir, dir.Path, Tool, NullLogger.Instance);

        Assert.Equal("{ not json", File.ReadAllText(path));
    }

    /// <summary>
    /// Teammates whose sessions start at once all write the one shared script; none of them may lose its hook to the
    /// others' writes (a replace over a file another thread holds open is refused on Windows).
    /// </summary>
    [Fact]
    public async Task Install_ManyAtOnce_EveryWorkDirGetsItsHook()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        const int Teammates = 16;
        string script = Path.Combine(dir.Path, "hooks", DocHistoryStopHook.ScriptName);

        for (int round = 0; round < 10; round++)
        {
            if (File.Exists(script))
            {
                File.Delete(script);
            }

            using Barrier barrier = new(Teammates);
            string[] workDirs = [.. Enumerable.Range(0, Teammates).Select(index => Path.Combine(dir.Path, $"r{round}", $"w{index}"))];
            await Task.WhenAll(workDirs.Select(workDir => Task.Run(
                () =>
                {
                    barrier.SignalAndWait(ct);
                    DocHistoryStopHook.Install(workDir, dir.Path, Tool, NullLogger.Instance);
                },
                ct)));

            Assert.All(workDirs, workDir => Assert.True(File.Exists(Path.Combine(workDir, ".claude", DocHistoryStopHook.SettingsFile))));
        }

        Assert.Equal(DocHistoryStopHook.Script, File.ReadAllText(script));
    }

    private static JsonObject ReadSettings(string workDir) =>
        (JsonObject)JsonNode.Parse(File.ReadAllText(Path.Combine(workDir, ".claude", DocHistoryStopHook.SettingsFile)))!;
}
