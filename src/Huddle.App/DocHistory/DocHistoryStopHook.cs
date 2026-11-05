using System.Text.Json;
using System.Text.Json.Nodes;
using Agency.Huddle.App.Library;

namespace Agency.Huddle.App.DocHistory;

/// <summary>
/// Installs the Stop hook that reminds an Agent to call <c>log_change</c> (DocHistory spec §6.3, §10). Measured on Haiku at
/// low Effort, a system prompt paragraph left creates and reviews unlogged, and a reminder on every Message fixed them but
/// cost tokens on every Turn of every conversation. A Stop hook costs nothing on a Turn that touched no Markdown file: it
/// runs when the Agent is about to finish, looks at what the Turn did, and only then sends the Agent back once. Huddle
/// still infers nothing and attributes nothing (ADR-0060): the Agent decides and reports.
/// </summary>
internal static class DocHistoryStopHook
{
    /// <summary>The hook script's file name, under <c>{DataDir}/hooks/</c>. The settings entry is recognised by it.</summary>
    internal const string ScriptName = "doc-history-stop.js";

    /// <summary>The settings file the entry goes in. <c>settings.local.json</c> is read by the Adapter beside <c>settings.json</c> and is never a file a Human is likely to have shared.</summary>
    internal const string SettingsFile = "settings.local.json";

    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    /// <summary>
    /// Serialises writes of the one script every Work Dir shares. Agents start sessions at the same time, and on Windows
    /// a move over a file another thread is reading is refused with <see cref="UnauthorizedAccessException"/>, which cost
    /// those agents their hook. The settings files are per Work Dir and need no gate.
    /// </summary>
    private static readonly Lock ScriptGate = new();

    /// <summary>
    /// Writes the script under <paramref name="dataDir"/> and adds the hook to the Agent's Work Dir settings, keeping every
    /// other setting. Safe to call on every session start. A settings file that is not valid JSON is left alone, and a
    /// failure to write is logged: the hook is a nudge, never a reason to refuse to start a session.
    /// </summary>
    /// <param name="workDir">The Agent's Work Dir, which is the Adapter's working folder.</param>
    /// <param name="dataDir">The App's data folder, which keeps the script.</param>
    /// <param name="logChangeToolName">The tool's prefixed name, which the hook's message tells the Agent to call.</param>
    /// <param name="logger">Receives a failure.</param>
    internal static void Install(string workDir, string dataDir, string logChangeToolName, ILogger logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workDir);
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDir);
        ArgumentException.ThrowIfNullOrWhiteSpace(logChangeToolName);
        ArgumentNullException.ThrowIfNull(logger);

        try
        {
            string script = Path.Combine(dataDir, "hooks", ScriptName);
            lock (ScriptGate)
            {
                WriteIfChanged(script, Script);
            }

            AddToSettings(Path.Combine(workDir, ".claude", SettingsFile), $"node \"{script}\" {logChangeToolName}", logger);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "Could not install the document history hook in {WorkDir}.", workDir);
        }
    }

    /// <summary>Adds or replaces this hook's entry in a settings file.</summary>
    /// <param name="settingsPath">The settings file, which may not exist yet.</param>
    /// <param name="command">The command the hook runs.</param>
    /// <param name="logger">Receives a settings file that cannot be read as JSON.</param>
    private static void AddToSettings(string settingsPath, string command, ILogger logger)
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
        JsonArray stop = hooks["Stop"] as JsonArray ?? [];
        for (int index = stop.Count - 1; index >= 0; index--)
        {
            if (stop[index]?.ToJsonString().Contains(ScriptName, StringComparison.Ordinal) == true)
            {
                stop.RemoveAt(index);
            }
        }

        stop.Add(new JsonObject
        {
            ["hooks"] = new JsonArray(new JsonObject { ["type"] = "command", ["command"] = command }),
        });
        hooks["Stop"] = stop;
        root["hooks"] = hooks;

        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath) ?? ".");
        WriteIfChanged(settingsPath, root.ToJsonString(WriteOptions));
    }

    /// <summary>Writes a text file only when its content differs, so a session start does not touch an unchanged file.</summary>
    /// <param name="path">The file.</param>
    /// <param name="text">The text it should hold.</param>
    private static void WriteIfChanged(string path, string text)
    {
        if (File.Exists(path) && string.Equals(File.ReadAllText(path), text, StringComparison.Ordinal))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        AtomicFile.WriteText(path, text);
    }

    /// <summary>
    /// The hook script. Claude Code runs it when the Agent is about to stop and passes a JSON object on standard input; it
    /// reads the Turn's transcript, and when the Turn wrote, edited or opened a Markdown file and made no <c>log_change</c>
    /// call, answers <c>{"decision":"block","reason":…}</c>, which sends the Agent back once with the reason as its next
    /// instruction. A <c>Read</c> of a file named <c>AGENTS.md</c> (any case, either slash) is following the rules, not
    /// reviewing a document, so it is not counted; a write to it still is. On the second stop (<c>stop_hook_active</c>) or any doubt it says nothing, so it cannot loop.
    /// </summary>
    internal const string Script = """
        // Written by Agency.Huddle (DocHistory). Reminds the Agent to call log_change after it touched a .md file.
        const fs = require("fs");
        const tool = process.argv[2] || "log_change";
        let raw = "";
        process.stdin.setEncoding("utf8");
        process.stdin.on("data", (chunk) => (raw += chunk));
        process.stdin.on("end", () => {
          try {
            const input = JSON.parse(raw || "{}");
            if (input.stop_hook_active || !input.transcript_path) return;
            const entries = fs.readFileSync(input.transcript_path, "utf8").split("\n").filter(Boolean).map((line) => {
              try { return JSON.parse(line); } catch { return null; }
            }).filter(Boolean);
            const isPrompt = (entry) => entry.type === "user" && entry.message && (typeof entry.message.content === "string" ||
              (Array.isArray(entry.message.content) && entry.message.content.some((block) => block.type === "text")));
            let start = 0;
            entries.forEach((entry, index) => { if (isPrompt(entry)) start = index + 1; });
            const wrote = new Set();
            const opened = new Set();
            let logged = false;
            for (const entry of entries.slice(start)) {
              const content = entry.type === "assistant" && entry.message ? entry.message.content : null;
              if (!Array.isArray(content)) continue;
              for (const block of content) {
                if (block.type !== "tool_use") continue;
                const file = block.input && (block.input.file_path || block.input.notebook_path);
                const markdown = typeof file === "string" && /\.md$/i.test(file);
                if (/log_change$/.test(block.name)) logged = true;
                else if (markdown && ["Write", "Edit", "MultiEdit", "NotebookEdit"].includes(block.name)) wrote.add(file);
                else if (markdown && block.name === "Read" && file.split(/[\\/]/).pop().toLowerCase() !== "agents.md") opened.add(file);
              }
            }
            if (logged || (wrote.size === 0 && opened.size === 0)) return;
            const reason = wrote.size > 0
              ? `You wrote or edited ${[...wrote].join(", ")} this turn but have not logged it. Call ${tool} once for each file with path, kind (created for a new file, edited for an existing one) and summary (one line saying what you did and why), then finish your reply.`
              : `You opened ${[...opened].join(", ")} this turn. If you reviewed or checked it (kind reviewed) or disagree with part of it (kind objected), call ${tool} with path, kind and summary, then finish your reply. If you only read it to answer a question or summarise it, there is nothing to log: just finish.`;
            process.stdout.write(JSON.stringify({ decision: "block", reason }));
          } catch (error) {
            // Never block an Agent because the reminder failed.
          }
        });
        """;
}
