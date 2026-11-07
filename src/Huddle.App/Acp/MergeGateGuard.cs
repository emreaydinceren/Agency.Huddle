using System.Text.Json;
using Agency.Huddle.Acp.Abstractions;

namespace Agency.Huddle.App.Acp;

/// <summary>
/// Asks the Team's <see cref="IMergeGate"/> about a shell command that runs <c>git merge</c>, <c>git push</c> or a <c>git pull</c> of a
/// branch other than main (PR-7), and refuses when the gate has a reason. The match is a convenience guard for an Agent that follows the
/// process, not a boundary; see <see cref="GitCommandLine"/> for what it catches and what it does not.
/// </summary>
/// <param name="mergeGate">Decides the command.</param>
internal sealed class MergeGateGuard(IMergeGate mergeGate) : IToolCallGuard
{
    /// <summary>Argument names that carry a shell command in the tool calls this application sees.</summary>
    private static readonly string[] CommandArgumentNames = ["command", "cmd"];

    /// <inheritdoc />
    public string? RefusalFor(ToolCallInfo toolCall, string personaName)
    {
        ArgumentNullException.ThrowIfNull(toolCall);

        if (ReadCommand(toolCall) is { } command
            && GitCommandLine.NeedsGate(command)
            && mergeGate.RefusalFor(personaName, command) is { } reason)
        {
            return $"'{command}' is held by the merge gate: {reason}";
        }

        return null;
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
            // Not something this type can judge; approving matches the "identify positively" rule.
        }

        return null;
    }
}
