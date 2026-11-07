using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.DocHistory;

namespace Agency.Huddle.Tests.DocHistory;

/// <summary>Pins what <see cref="MergeGateHook.Install"/> leaves on disk (issue #396): the script, one PreToolUse entry for the shell tools, and every setting and hook that was already there, including the doc history Stop hook.</summary>
public sealed class MergeGateHookInstallTests
{
    /// <summary>Install writes the script under the data folder and a PreToolUse entry on the shell tools that runs it.</summary>
    [Fact]
    public void Install_WritesTheScriptAndAPreToolUseEntry()
    {
        using TempDataDir dir = new();
        string workDir = Path.Combine(dir.Path, "work");

        MergeGateHook.Install(workDir, dir.Path, NullLogger.Instance);

        string script = Path.Combine(dir.Path, "hooks", MergeGateHook.ScriptName);
        Assert.Equal(MergeGateHook.Script, File.ReadAllText(script));
        JsonObject entry = Assert.IsType<JsonObject>(Assert.Single(Hooks(workDir, "PreToolUse")));
        Assert.Equal("Bash|PowerShell", (string?)entry["matcher"]);
        Assert.Equal($"node \"{script}\"", (string?)entry["hooks"]?[0]?["command"]);
    }

    /// <summary>Installing again, as every session start does, leaves one entry.</summary>
    [Fact]
    public void Install_Twice_KeepsOneEntry()
    {
        using TempDataDir dir = new();
        string workDir = Path.Combine(dir.Path, "work");

        MergeGateHook.Install(workDir, dir.Path, NullLogger.Instance);
        MergeGateHook.Install(workDir, dir.Path, NullLogger.Instance);

        Assert.Single(Hooks(workDir, "PreToolUse"));
    }

    /// <summary>An install made before this hook existed, holding the Stop hook and a hook of the user's own, gains the entry and loses nothing.</summary>
    [Fact]
    public void Install_IntoAnExistingFile_KeepsTheStopHookAndOtherHooks()
    {
        using TempDataDir dir = new();
        string workDir = Path.Combine(dir.Path, "work");
        DocHistoryStopHook.Install(workDir, dir.Path, "mcp__team__log_change", NullLogger.Instance);
        string path = Path.Combine(workDir, ".claude", WorkDirHookSettings.SettingsFile);
        JsonObject before = ReadSettings(workDir);
        before["model"] = "haiku";
        (before["hooks"] as JsonObject)?.Add("PreToolUse", new JsonArray(new JsonObject
        {
            ["matcher"] = "Edit",
            ["hooks"] = new JsonArray(new JsonObject { ["type"] = "command", ["command"] = "echo mine" }),
        }));
        File.WriteAllText(path, before.ToJsonString());

        MergeGateHook.Install(workDir, dir.Path, NullLogger.Instance);

        Assert.Equal("haiku", (string?)ReadSettings(workDir)["model"]);
        Assert.Single(Hooks(workDir, "Stop"));
        JsonArray pre = Hooks(workDir, "PreToolUse");
        Assert.Equal(2, pre.Count);
        Assert.Contains("echo mine", pre.ToJsonString(), StringComparison.Ordinal); // contains-ok: pins that a hook of the user's own survives; the text is the marker.
        Assert.Contains(MergeGateHook.ScriptName, pre.ToJsonString(), StringComparison.Ordinal); // contains-ok: pins that the new entry names the script; the file name is the marker.
    }

    /// <summary>Installing the doc history hook afterwards keeps this one: the two share a file and neither replaces the other.</summary>
    [Fact]
    public void DocHistoryInstall_AfterMergeGateInstall_KeepsTheMergeGateEntry()
    {
        using TempDataDir dir = new();
        string workDir = Path.Combine(dir.Path, "work");
        MergeGateHook.Install(workDir, dir.Path, NullLogger.Instance);

        DocHistoryStopHook.Install(workDir, dir.Path, "mcp__team__log_change", NullLogger.Instance);

        Assert.Single(Hooks(workDir, "PreToolUse"));
        Assert.Single(Hooks(workDir, "Stop"));
    }

    /// <summary>A settings file that is not JSON is left exactly as it was.</summary>
    [Fact]
    public void Install_InvalidSettings_AreLeftAlone()
    {
        using TempDataDir dir = new();
        string workDir = Path.Combine(dir.Path, "work");
        Directory.CreateDirectory(Path.Combine(workDir, ".claude"));
        string path = Path.Combine(workDir, ".claude", WorkDirHookSettings.SettingsFile);
        File.WriteAllText(path, "{ not json");

        MergeGateHook.Install(workDir, dir.Path, NullLogger.Instance);

        Assert.Equal("{ not json", File.ReadAllText(path));
    }

    /// <summary>With no merge gate registered nothing is written, and with one the hook is installed.</summary>
    [Fact]
    public void InstallMergeGateHook_OnlyWhenAGateIsRegistered()
    {
        using TempDataDir dir = new();
        string none = Path.Combine(dir.Path, "none");
        string gated = Path.Combine(dir.Path, "gated");

        DotAcpAgentHostFactory.InstallMergeGateHook(none, dir.Path, null, NullLogger.Instance);
        DotAcpAgentHostFactory.InstallMergeGateHook(gated, dir.Path, new AnyGate(), NullLogger.Instance);

        Assert.False(Directory.Exists(Path.Combine(none, ".claude")));
        Assert.Single(Hooks(gated, "PreToolUse"));
    }

    private static JsonObject ReadSettings(string workDir) =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(workDir, ".claude", WorkDirHookSettings.SettingsFile))) as JsonObject
        ?? throw new InvalidOperationException("The settings file is not a JSON object.");

    private static JsonArray Hooks(string workDir, string eventName) =>
        (ReadSettings(workDir)["hooks"] as JsonObject)?[eventName] as JsonArray
        ?? throw new InvalidOperationException($"No {eventName} hooks in the settings file.");

    /// <summary>A gate that allows everything; only its presence matters.</summary>
    private sealed class AnyGate : IMergeGate
    {
        /// <inheritdoc />
        public string? RefusalFor(string personaName, string command) => null;
    }
}
