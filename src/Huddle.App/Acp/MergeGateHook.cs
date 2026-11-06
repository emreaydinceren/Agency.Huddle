using Agency.Huddle.App.DocHistory;

namespace Agency.Huddle.App.Acp;

/// <summary>
/// Installs the <c>PreToolUse</c> hook that makes <see cref="WorkDirPermissionHandler"/> see a <c>git merge</c> or
/// <c>git push</c> (issue #396). In the Adapter's <c>auto</c> permission mode, which a user's own
/// <c>~/.claude/settings.json</c> can set (<c>defaultMode</c>) whatever Huddle sends, the Adapter approves a Bash
/// command itself and never sends a permission request, so the Team's merge gate was never consulted. A hook that
/// answers <c>ask</c> for those two commands, and says nothing for every other command, turns the call back into a
/// request that Huddle decides. Huddle still owns the decision: the hook only makes sure it is asked.
/// </summary>
/// <remarks>
/// Only the Claude Code Adapter reads <c>.claude/settings.local.json</c>. Codex has no equivalent hook, so under
/// Codex the gate still covers what Codex sends as a permission request and nothing else (see known-limits.md).
/// </remarks>
internal static class MergeGateHook
{
    /// <summary>The hook script's file name, under <c>{DataDir}/hooks/</c>. The settings entry is recognised by it.</summary>
    internal const string ScriptName = "merge-gate-ask.js";

    /// <summary>The tools the hook watches: the Adapter's shell tools.</summary>
    internal const string Matcher = "Bash|PowerShell";

    private static readonly Lock ScriptGate = new();

    /// <summary>
    /// Writes the script under <paramref name="dataDir"/> and adds the hook to the Agent's Work Dir settings, keeping every
    /// other setting and hook. Safe to call on every session start, so an install made before this hook existed gains it.
    /// A failure is logged and never stops a session.
    /// </summary>
    /// <param name="workDir">The Agent's Work Dir, which is the Adapter's working folder.</param>
    /// <param name="dataDir">The App's data folder, which keeps the script.</param>
    /// <param name="logger">Receives a failure.</param>
    internal static void Install(string workDir, string dataDir, ILogger logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workDir);
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDir);
        ArgumentNullException.ThrowIfNull(logger);

        try
        {
            string script = Path.Combine(dataDir, "hooks", ScriptName);
            lock (ScriptGate)
            {
                DocHistoryStopHook.WriteIfChanged(script, Script);
            }

            DocHistoryStopHook.AddToSettings(
                Path.Combine(workDir, ".claude", DocHistoryStopHook.SettingsFile),
                "PreToolUse",
                Matcher,
                ScriptName,
                $"node \"{script}\"",
                logger);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "Could not install the merge gate hook in {WorkDir}.", workDir);
        }
    }

    /// <summary>
    /// The hook script. Claude Code runs it before a shell tool and passes a JSON object on standard input. When any
    /// segment of the command line has a word that names git followed by the word <c>merge</c> or <c>push</c>, it answers
    /// <c>permissionDecision: ask</c>. It looks for those words anywhere after git rather than only in the subcommand
    /// position, so it may ask for a command the gate then allows, and it never fails to ask for one the gate would
    /// refuse. Any other command, and any doubt or error, produces no output, so nothing else changes.
    /// </summary>
    internal const string Script = """
        // Written by Agency.Huddle (merge gate). Makes the Adapter ask Huddle before a git merge or git push, even in auto mode.
        let raw = "";
        process.stdin.setEncoding("utf8");
        process.stdin.on("data", (chunk) => (raw += chunk));
        process.stdin.on("end", () => {
          try {
            const input = JSON.parse(raw || "{}");
            const command = input.tool_input && (input.tool_input.command || input.tool_input.cmd);
            if (typeof command !== "string") return;
            const unquote = (word) => word.replace(/^["']+|["']+$/g, "");
            const isGit = (word) => /^(.*[\\/])?git(\.exe)?$/i.test(unquote(word));
            const split = (text) => {
              const segments = [];
              let words = [];
              let word = "";
              let inWord = false;
              let quote = "";
              const endWord = () => { if (inWord) { words.push(word); word = ""; inWord = false; } };
              const endSegment = () => { endWord(); if (words.length) { segments.push(words); words = []; } };
              for (const c of text) {
                if (quote && c === quote) quote = "";
                else if (quote) word += c;
                else if (c === '"' || c === "'") { quote = c; inWord = true; }
                else if (c === " " || c === "\t") endWord();
                else if (";&|()`\r\n".includes(c)) endSegment();
                else { word += c; inWord = true; }
              }
              endSegment();
              return segments;
            };
            const isRisky = (text, depth) => split(text).some((words) => {
              if (depth < 3 && words.some((word) => /\s/.test(word) && isRisky(word, depth + 1))) return true;
              const at = words.findIndex(isGit);
              return at >= 0 && words.slice(at + 1).some((word) => /^(merge|push|pull)$/i.test(unquote(word)));
            });
            if (!isRisky(command, 0)) return;
            process.stdout.write(JSON.stringify({
              hookSpecificOutput: {
                hookEventName: "PreToolUse",
                permissionDecision: "ask",
                permissionDecisionReason: "Huddle checks git merge and git push before they run.",
              },
            }));
          } catch (error) {
            // Never block or change a command because the check failed.
          }
        });
        """;
}
