using System.Text.Json;
using System.Text.Json.Nodes;
using Agency.Huddle.App.Library;

namespace Agency.Huddle.App.Acp;

/// <summary>
/// Writes an Adapter hook into an Agent's Work Dir: the script under the App's data folder and the entry in the Work Dir's
/// <c>.claude/settings.local.json</c>. The merge gate hook and the document history Stop hook share it, so neither depends
/// on the other.
/// </summary>
internal static class WorkDirHookSettings
{
    /// <summary>The settings file the entry goes in. <c>settings.local.json</c> is read by the Adapter beside <c>settings.json</c> and is never a file a Human is likely to have shared.</summary>
    internal const string SettingsFile = "settings.local.json";

    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    /// <summary>
    /// Serialises writes of the scripts every Work Dir shares. Agents start sessions at the same time, and on Windows
    /// a move over a file another thread is reading is refused with <see cref="UnauthorizedAccessException"/>, which cost
    /// those agents their hook. The settings files are per Work Dir and need no gate.
    /// </summary>
    private static readonly Lock ScriptGate = new();

    /// <summary>
    /// Writes a hook script under <paramref name="dataDir"/> and adds its entry to the Agent's Work Dir settings, keeping
    /// every other setting and hook. A failure to write is logged and never stops a session.
    /// </summary>
    /// <param name="workDir">The Agent's Work Dir, which is the Adapter's working folder.</param>
    /// <param name="dataDir">The App's data folder, which keeps the script.</param>
    /// <param name="scriptName">The script's file name under <c>{dataDir}/hooks/</c>.</param>
    /// <param name="script">The script's text.</param>
    /// <param name="eventName">The hook event the entry goes under, such as <c>Stop</c> or <c>PreToolUse</c>.</param>
    /// <param name="matcher">The tool matcher for the entry, or <see langword="null"/> for none.</param>
    /// <param name="commandArguments">Text appended after the script path in the command, or empty for none.</param>
    /// <param name="logger">Receives a failure.</param>
    /// <param name="hookLabel">What the hook is called in the warning logged when the script or settings cannot be written.</param>
    internal static void Install(
        string workDir,
        string dataDir,
        string scriptName,
        string script,
        string eventName,
        string? matcher,
        string commandArguments,
        ILogger logger,
        string hookLabel)
    {
        try
        {
            string scriptPath = Path.Combine(dataDir, "hooks", scriptName);
            lock (ScriptGate)
            {
                WriteIfChanged(scriptPath, script);
            }

            string command = commandArguments.Length == 0
                ? $"node \"{scriptPath}\""
                : $"node \"{scriptPath}\" {commandArguments}";
            AddToSettings(Path.Combine(workDir, ".claude", SettingsFile), eventName, matcher, scriptName, command, logger);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "Could not install the {HookLabel} hook in {WorkDir}.", hookLabel, workDir);
        }
    }

    /// <summary>Adds or replaces one hook's entry in a settings file, keeping every other setting and hook.</summary>
    /// <param name="settingsPath">The settings file, which may not exist yet.</param>
    /// <param name="eventName">The hook event the entry goes under, such as <c>Stop</c> or <c>PreToolUse</c>.</param>
    /// <param name="matcher">The tool matcher for the entry, or <see langword="null"/> for none.</param>
    /// <param name="scriptName">The script's file name, by which an earlier entry of the same hook is recognised and replaced.</param>
    /// <param name="command">The command the hook runs.</param>
    /// <param name="logger">Receives a settings file that cannot be read as JSON.</param>
    internal static void AddToSettings(string settingsPath, string eventName, string? matcher, string scriptName, string command, ILogger logger)
    {
        JsonObject root = [];
        if (File.Exists(settingsPath))
        {
            try
            {
                root = JsonNode.Parse(File.ReadAllText(settingsPath)) as JsonObject ?? [];
            }
            catch (JsonException exception)
            {
                logger.LogWarning(exception, "Left {Path} alone: it is not valid JSON.", settingsPath);
                return;
            }
        }

        JsonObject hooks = root["hooks"] as JsonObject ?? [];
        JsonArray entries = hooks[eventName] as JsonArray ?? [];
        for (int index = entries.Count - 1; index >= 0; index--)
        {
            if (entries[index]?.ToJsonString().Contains(scriptName, StringComparison.Ordinal) == true)
            {
                entries.RemoveAt(index);
            }
        }

        JsonObject entry = [];
        if (matcher is not null)
        {
            entry["matcher"] = matcher;
        }

        entry["hooks"] = new JsonArray(new JsonObject { ["type"] = "command", ["command"] = command });
        entries.Add(entry);
        hooks[eventName] = entries;
        root["hooks"] = hooks;

        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath) ?? ".");
        WriteIfChanged(settingsPath, root.ToJsonString(WriteOptions));
    }

    /// <summary>Writes a text file only when its content differs, so a session start does not touch an unchanged file.</summary>
    /// <param name="path">The file.</param>
    /// <param name="text">The text it should hold.</param>
    internal static void WriteIfChanged(string path, string text)
    {
        if (File.Exists(path) && string.Equals(File.ReadAllText(path), text, StringComparison.Ordinal))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        AtomicFile.WriteText(path, text);
    }
}
