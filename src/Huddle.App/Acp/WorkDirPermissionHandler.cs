using System.Text;
using System.Text.Json;
using Agency.Huddle.Acp.Abstractions;

namespace Agency.Huddle.App.Acp;

/// <summary>
/// Approves an Agent's permission requests the way <see cref="AutoApprovePermissionHandler"/> does,
/// with one refusal: a tool call that writes inside the Agent's own home configuration directory
/// (<c>~/.claude</c>) is declined.
/// </summary>
/// <remarks>
/// <para>
/// rules.md records that "the Work Dir is not a jail" — <c>Bash</c> and <c>Write</c> run agent-side
/// against the real disk — and that stays true. This is not a jail either. It closes one specific
/// blast radius that a Turn reached in practice: asked to remember a word, a Persona wrote a new file
/// into <c>~/.claude/projects/&lt;repo&gt;/memory/</c> and edited the <c>MEMORY.md</c> index beside it.
/// That directory belongs to the human's own coding agent, not to this application, and nothing in
/// the chat surface asked for it.
/// </para>
/// <para>
/// Two things made it worth a code change rather than a note. The write is invisible: tool activity
/// lives only in the Draft, which is discarded when the Turn ends, so on any Turn the human is not
/// watching live it leaves no trace in the app at all. And it silently falsifies tests about session
/// memory — a Persona that has written a word to disk can read it back after the restart that was
/// supposed to make it forget.
/// </para>
/// <para>
/// Deliberately narrow. The wider question the issue asks — what a Persona's tool surface should be
/// allowed to touch in general — is a product decision, not this type's to make: a Persona working on
/// a repository legitimately writes all over it, so a Work Dir jail would break the feature. What is
/// defensible without that decision is that an Agent has no business writing into the human's agent
/// configuration, whatever it was asked.
/// </para>
/// <para>
/// Only tool calls that name a path in a recognised argument are inspected. A <c>Bash</c> command
/// that redirects into the same directory is not caught, and pretending otherwise by pattern-matching
/// shell text would give a false sense of a boundary that is not there.
/// </para>
/// <para>
/// The one shell command it does read is <c>git merge</c> / <c>git push</c>, and only to ask the optional
/// <see cref="IMergeGate"/> whether the Team's <c>checks-before-merge</c> gate lets it run (PR-7). The
/// match is a convenience guard for an Agent that follows the process, not a boundary. It catches the
/// command at the start of a segment split on <c>&amp;&amp;</c>, <c>||</c>, <c>;</c>, <c>|</c> and newlines,
/// after git's own <c>-C</c>/<c>-c</c>/<c>--option</c> flags, case-insensitively, with <c>git.exe</c> and
/// quoted words accepted. It does NOT catch: a shell alias or git alias for push or merge, an
/// environment-variable prefix (<c>FOO=1 git push</c>), <c>command git push</c>, <c>sudo</c>, a command
/// wrapped in <c>bash -c "..."</c>, <c>$(...)</c> or backticks, a script that runs the push, a push made
/// by another tool (an IDE, <c>gh</c>), or a command in an Adapter work mode that never asks permission.
/// The Human's own approval of a prompt is never intercepted: only the Agent's requests are answered here.
/// </para>
/// </remarks>
/// <param name="protectedDirectory">
/// The directory to refuse writes into, already absolute. Passed in rather than read from the
/// environment here so a test can point it somewhere harmless.
/// </param>
/// <param name="logger">Records each refusal; without a line here the refusal is as invisible as the write was.</param>
/// <param name="mergeGate">Decides <c>git merge</c> and <c>git push</c>; <see langword="null"/> leaves them approved.</param>
/// <param name="personaName">The Persona this handler answers for, passed to <paramref name="mergeGate"/>.</param>
internal sealed class WorkDirPermissionHandler(
    string protectedDirectory,
    ILogger<WorkDirPermissionHandler> logger,
    IMergeGate? mergeGate = null,
    string personaName = "")
    : IPermissionHandler
{
    /// <summary>Argument names that carry a shell command in the tool calls this application sees.</summary>
    private static readonly string[] CommandArgumentNames = ["command", "cmd"];

    /// <summary>
    /// Splits one shell line into commands of words. A word may be wrapped in double or single quotes (so a quoted
    /// path with spaces stays one word, and a separator inside quotes does not split); commands end at <c>;</c>,
    /// <c>&amp;</c>, <c>|</c> (so also <c>&amp;&amp;</c> and <c>||</c>), parentheses and line breaks. Still not handled:
    /// backslash escapes, <c>$(…)</c> or backtick substitution, here-documents and aliases, so a hostile spelling
    /// can still pass; the gate is a guard against honest mistakes, not a sandbox.
    /// </summary>
    /// <param name="command">The shell command line.</param>
    /// <returns>One word list per command, in order.</returns>
    private static List<List<string>> SplitSegments(string command)
    {
        List<List<string>> segments = [];
        List<string> words = [];
        StringBuilder word = new();
        bool inWord = false;
        char quote = '\0';

        void EndWord()
        {
            if (inWord)
            {
                words.Add(word.ToString());
                word.Clear();
                inWord = false;
            }
        }

        void EndSegment()
        {
            EndWord();
            if (words.Count > 0)
            {
                segments.Add(words);
                words = [];
            }
        }

        foreach (char c in command)
        {
            if (quote != '\0' && c == quote)
            {
                quote = '\0';
            }
            else if (quote != '\0')
            {
                word.Append(c);
            }
            else if (c is '"' or '\'')
            {
                quote = c;
                inWord = true;
            }
            else if (c is ' ' or '\t')
            {
                EndWord();
            }
            else if (c is ';' or '&' or '|' or '(' or ')' or '\r' or '\n')
            {
                EndSegment();
            }
            else
            {
                word.Append(c);
                inWord = true;
            }
        }

        EndSegment();
        return segments;
    }

    /// <summary>Git options that consume the next word.</summary>
    private static readonly string[] GitOptionsWithValue = ["-C", "-c", "--git-dir", "--work-tree", "--namespace"];

    /// <summary>
    /// Argument names that carry a filesystem path in the tool calls this application sees. Matched
    /// case-insensitively against the tool call's raw input.
    /// </summary>
    private static readonly string[] PathArgumentNames = ["file_path", "path", "notebook_path"];

    /// <summary>The Agent's home configuration directory for the current user, or null when it cannot be resolved.</summary>
    /// <returns>The absolute path to <c>~/.claude</c>, or <see langword="null"/> when the profile directory is unknown.</returns>
    public static string? DefaultProtectedDirectory()
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return profile.Length == 0 ? null : Path.Combine(profile, ".claude");
    }

    /// <inheritdoc />
    public Task<PermissionDecision> DecideAsync(PermissionRequestContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        if (this.TargetsProtectedDirectory(context.ToolCall, out var path))
        {
            logger.LogWarning(
                "Refused a tool call writing to '{Path}': it is inside the agent's own configuration directory '{ProtectedDirectory}'.",
                path,
                protectedDirectory);

            return Task.FromResult(Refuse(context.Options));
        }

        if (mergeGate is not null
            && ReadCommand(context.ToolCall) is { } command
            && IsMergeOrPush(command)
            && mergeGate.RefusalFor(personaName, command) is { } reason)
        {
            logger.LogWarning(
                "Refused '{Command}' for '{PersonaName}': {Reason}",
                command,
                personaName,
                reason);

            return Task.FromResult(Refuse(context.Options));
        }

        return Task.FromResult(Approve(context.Options));
    }

    /// <summary>Reads the shell command out of a tool call's raw input.</summary>
    /// <param name="toolCall">The call the Agent is asking permission for.</param>
    /// <returns>The command text, or <see langword="null"/> when the input carries none or is not JSON.</returns>
    private static string? ReadCommand(ToolCallInfo toolCall)
    {
        if (toolCall.RawInputJson is not { Length: > 0 } json)
        {
            return null;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            foreach (string name in CommandArgumentNames)
            {
                if (document.RootElement.TryGetProperty(name, out JsonElement value)
                    && value.ValueKind == JsonValueKind.String
                    && value.GetString() is { Length: > 0 } command)
                {
                    return command;
                }
            }
        }
        catch (JsonException)
        {
            // Not something this type can judge; approving matches the "identify positively" rule above.
        }

        return null;
    }

    /// <summary>Whether any segment of the shell line is <c>git [options] merge|push</c>. See the remarks for what this misses.</summary>
    /// <param name="command">The whole shell line.</param>
    /// <returns><see langword="true"/> when a segment runs git merge or git push.</returns>
    private static bool IsMergeOrPush(string command)
    {
        foreach (List<string> words in SplitSegments(command))
        {
            if (words.Count < 2 || !IsGit(Unquote(words[0])))
            {
                continue;
            }

            int index = 1;
            while (index < words.Count && Unquote(words[index]).StartsWith('-'))
            {
                string option = Unquote(words[index]);
                index += GitOptionsWithValue.Contains(option, StringComparer.Ordinal) ? 2 : 1;
            }

            if (index < words.Count)
            {
                string subcommand = Unquote(words[index]);
                if (string.Equals(subcommand, "merge", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(subcommand, "push", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Whether the word names the git executable, with or without a directory or <c>.exe</c>.</summary>
    /// <param name="word">The first word of a segment, unquoted.</param>
    private static bool IsGit(string word)
    {
        string name = word[(word.LastIndexOfAny(['/', '\\']) + 1)..];
        return string.Equals(name, "git", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "git.exe", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Strips surrounding quote characters from a shell word.</summary>
    /// <param name="word">The word.</param>
    private static string Unquote(string word) => word.Trim('"', '\'');

    /// <summary>Picks a refusal option, preferring a one-time refusal, and cancels when none is offered.</summary>
    /// <param name="options">The options the Agent offered.</param>
    /// <returns>The decision to send back.</returns>
    private static PermissionDecision Refuse(IReadOnlyList<PermissionOptionInfo> options) =>
        Pick(options, PermissionOptionKind.RejectOnce) ?? Pick(options, PermissionOptionKind.RejectAlways) ?? PermissionDecision.Cancelled;

    /// <summary>
    /// Approves, preferring a one-time grant over a standing one — the behaviour this type inherits
    /// from <see cref="AutoApprovePermissionHandler"/>, kept identical so the only difference between
    /// them is the refusal above.
    /// </summary>
    /// <param name="options">The options the Agent offered.</param>
    /// <returns>The decision to send back.</returns>
    private static PermissionDecision Approve(IReadOnlyList<PermissionOptionInfo> options) =>
        Pick(options, PermissionOptionKind.AllowOnce) ?? Pick(options, PermissionOptionKind.AllowAlways) ?? PermissionDecision.Cancelled;

    /// <summary>Returns a decision selecting the first option of <paramref name="kind"/>, or null when there is none.</summary>
    /// <param name="options">The options the Agent offered.</param>
    /// <param name="kind">The kind to look for.</param>
    /// <returns>A <see cref="SelectedDecision"/>, or <see langword="null"/>.</returns>
    private static SelectedDecision? Pick(IReadOnlyList<PermissionOptionInfo> options, PermissionOptionKind kind)
    {
        foreach (var option in options)
        {
            if (option.Kind == kind)
            {
                return new SelectedDecision(option.OptionId);
            }
        }

        return null;
    }

    /// <summary>
    /// Whether this tool call names a path inside the protected directory. Malformed or absent input
    /// is not a match: this refuses what it can positively identify and approves everything else,
    /// because the alternative — refusing whatever it cannot parse — would break ordinary work for
    /// every tool whose arguments it does not recognise.
    /// </summary>
    /// <param name="toolCall">The call the Agent is asking permission for.</param>
    /// <param name="path">The offending path, when this returns true.</param>
    /// <returns><see langword="true"/> when the call targets the protected directory.</returns>
    private bool TargetsProtectedDirectory(ToolCallInfo toolCall, out string? path)
    {
        path = null;

        if (toolCall.RawInputJson is not { Length: > 0 } json)
        {
            return false;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            // Not something this type can judge; approving matches the "identify positively" rule above.
            return false;
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            foreach (var name in PathArgumentNames)
            {
                if (document.RootElement.TryGetProperty(name, out var value) &&
                    value.ValueKind == JsonValueKind.String &&
                    value.GetString() is { Length: > 0 } candidate &&
                    this.IsInsideProtectedDirectory(candidate))
                {
                    path = candidate;
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Whether <paramref name="candidate"/> resolves to something inside the protected directory.
    /// Compared case-insensitively: Windows paths are, and rules.md already leans on that elsewhere.
    /// </summary>
    /// <param name="candidate">The path named by the tool call, absolute or relative.</param>
    /// <returns><see langword="true"/> when it is inside the protected directory.</returns>
    private bool IsInsideProtectedDirectory(string candidate)
    {
        string full;
        try
        {
            full = Path.GetFullPath(candidate);
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
        catch (PathTooLongException)
        {
            return false;
        }

        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(protectedDirectory));

        // The separator matters: without it "…\.claudex" would count as inside "…\.claude".
        return full.Equals(root, StringComparison.OrdinalIgnoreCase) ||
               full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
}
