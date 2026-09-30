using System.Globalization;
using System.Text.Json;
using Agency.Huddle.Acp.Abstractions;

namespace Agency.Huddle.App.Acp.Sessions;

/// <summary>
/// The fixed line a Teammate posts to say what its Adapter command did (Commands spec, section 8.3).
/// An Adapter's <c>/compact</c> sends no message text, only a tool call whose output carries
/// <c>{preTokens, postTokens, durationMs}</c>, so without this line a Room could not tell a finished
/// compaction from a failed one. It is interface copy in code, not a Prompt: it is read by the Human and
/// by every Teammate in the Room and is never sent to a model as an instruction.
/// </summary>
internal static class AdapterCommandOutcome
{
    private const string Arrow = "→";

    /// <summary>The line to post for a finished command Turn.</summary>
    /// <param name="commandName">The command that ran, without the slash.</param>
    /// <param name="reason">How the Turn ended.</param>
    /// <param name="outputJson">The last tool-call output of the Turn that was a JSON object, or <see langword="null"/> when there was none.</param>
    /// <param name="toolCallFailed">Whether any tool call of the Turn reached <see cref="ToolCallStatus.Failed"/>.</param>
    /// <returns>
    /// The line, or <see langword="null"/> when nothing should be posted: a Stop and a refusal post
    /// nothing, and a Turn cut off by a token or request limit is reported by the existing
    /// incomplete-stop path instead.
    /// </returns>
    internal static string? Describe(string commandName, StopReason reason, string? outputJson, bool toolCallFailed)
    {
        if (reason != StopReason.EndTurn)
        {
            return null;
        }

        if (toolCallFailed)
        {
            return $"/{commandName} did not finish.";
        }

        if (!TryReadFigures(outputJson, out long preTokens, out long postTokens, out double? durationMs))
        {
            return $"Ran /{commandName}.";
        }

        string before = preTokens.ToString("N0", CultureInfo.InvariantCulture);
        string after = postTokens.ToString("N0", CultureInfo.InvariantCulture);

        // Only compact is worded as a compaction, and only when the figures actually fell: an increase
        // is reported plainly rather than described as something it was not.
        if (postTokens > preTokens || !string.Equals(commandName, "compact", StringComparison.OrdinalIgnoreCase))
        {
            return $"Ran /{commandName}: {before} {Arrow} {after} tokens.";
        }

        if (durationMs is not { } milliseconds)
        {
            return $"Compacted my conversation: {before} {Arrow} {after} tokens.";
        }

        int seconds = Math.Max(1, (int)Math.Round(milliseconds / 1000.0, MidpointRounding.AwayFromZero));
        return string.Create(CultureInfo.InvariantCulture, $"Compacted my conversation: {before} {Arrow} {after} tokens in {seconds} s.");
    }

    /// <summary>
    /// Reads <c>{preTokens, postTokens, durationMs}</c> from a tool call's output. An unparseable output,
    /// a missing field or a figure of the wrong type is "no figures", never an error: a vendor changing
    /// the shape must degrade the line, not break the Turn.
    /// </summary>
    /// <param name="outputJson">The output, or <see langword="null"/>.</param>
    /// <param name="preTokens">The context size before, when this returns <see langword="true"/>.</param>
    /// <param name="postTokens">The context size after, when this returns <see langword="true"/>.</param>
    /// <param name="durationMs">How long it took, or <see langword="null"/> when the output carries no usable duration.</param>
    /// <returns><see langword="true"/> when both token figures were read.</returns>
    private static bool TryReadFigures(string? outputJson, out long preTokens, out long postTokens, out double? durationMs)
    {
        preTokens = 0;
        postTokens = 0;
        durationMs = null;
        if (string.IsNullOrWhiteSpace(outputJson))
        {
            return false;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(outputJson);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("preTokens", out JsonElement pre)
                || !root.TryGetProperty("postTokens", out JsonElement post)
                || pre.ValueKind != JsonValueKind.Number
                || post.ValueKind != JsonValueKind.Number
                || !pre.TryGetInt64(out preTokens)
                || !post.TryGetInt64(out postTokens)
                || preTokens < 0
                || postTokens < 0)
            {
                return false;
            }

            if (root.TryGetProperty("durationMs", out JsonElement duration)
                && duration.ValueKind == JsonValueKind.Number
                && duration.TryGetDouble(out double milliseconds)
                && milliseconds >= 0)
            {
                durationMs = milliseconds;
            }

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
