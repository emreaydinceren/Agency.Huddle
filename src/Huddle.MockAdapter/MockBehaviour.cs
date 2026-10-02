using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Agency.Huddle.Acp.Tests.Fakes;

namespace Agency.Huddle.MockAdapter;

/// <summary>
/// Default prompt handling for the mock adapter. Chunked echo is the default per Spec §6.10
/// ("Behaviour"): it proves the thing most likely to be wrong — that text arrives incrementally,
/// becomes a <c>MessageDelta</c> on the pipe, and renders live in the Room.
/// </summary>
internal static class MockBehaviour
{
    private const int TargetChunkCount = 3;

    private static readonly char[] WordSeparators = [' ', '\t', '\r', '\n'];

    /// <summary>
    /// Chunks used when there is not even enough text to split into <see cref="TargetChunkCount"/>
    /// characters — an empty prompt, or one with only one or two characters of real content. Still
    /// more than one, so the "more than one chunk" guarantee holds even with nothing to echo.
    /// </summary>
    private static readonly string[] FallbackChunks = ["(no prompt to echo)", "(end of turn)"];

    /// <summary>
    /// Reported when the client never advertised <c>elicitation.form</c>, so the form was not sent. The
    /// only <see cref="InvalidOperationException"/> the elicitation helper throws is that refusal.
    /// </summary>
    private const string NotAdvertisedReason = "elicitation.form was not advertised";

    private static readonly JsonSerializerOptions ContentJsonOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>
    /// Echoes the prompt back as several chunks, then ends the turn. A prompt that contains an
    /// <c>[elicit:...]</c> marker (see <see cref="MockElicitation"/>) first puts that form to the Human, and
    /// then streams one line stating what came back instead of the echo.
    /// </summary>
    /// <param name="context">The prompt in progress; chunks are sent on it as they are produced.</param>
    /// <returns>
    /// The stop reason reported for the turn: <c>"end_turn"</c>, or <c>"cancelled"</c> when
    /// <c>session/cancel</c> arrives while a form waits for the Human.
    /// </returns>
    internal static async Task<string> ChunkedEchoAsync(PromptContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        string prompt = MockBehaviour.ExtractPromptText(context.Params);

        MockElicitation? elicitation = MockElicitation.Find(prompt);
        if (elicitation is not null)
        {
            string? reply = await MockBehaviour.AskAsync(context, elicitation).ConfigureAwait(false);
            if (reply is null)
            {
                return "cancelled";
            }

            foreach (string replyChunk in MockBehaviour.SplitReplyIntoChunks(reply))
            {
                await context.SendTextChunkAsync(replyChunk).ConfigureAwait(false);
            }

            return "end_turn";
        }

        IReadOnlyList<string> chunks = MockBehaviour.SplitIntoChunks(prompt);

        foreach (string chunk in chunks)
        {
            await context.SendTextChunkAsync(chunk).ConfigureAwait(false);
        }

        return "end_turn";
    }

    /// <summary>
    /// Sends <paramref name="elicitation"/> to the client as a real <c>elicitation/create</c> request and waits
    /// for the answer, or for <c>session/cancel</c>, whichever comes first.
    /// </summary>
    /// <returns>
    /// The one-line reply that states what the "agent" received (or why it could not ask), or
    /// <see langword="null"/> when the turn was cancelled while the form waited.
    /// </returns>
    private static async Task<string?> AskAsync(PromptContext context, MockElicitation elicitation)
    {
        Task<JsonObject> request = context.RequestElicitationAsync(elicitation.Message, elicitation.BuildSchema(), elicitation.ToolCallId);
        Task cancelled = context.WaitForCancelAsync();

        if (ReferenceEquals(await Task.WhenAny(request, cancelled).ConfigureAwait(false), cancelled))
        {
            // The client may still answer, or never answer; either way the turn is over, so only
            // observe a late failure instead of letting it go unobserved.
            _ = request.ContinueWith(static task => _ = task.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            return null;
        }

        try
        {
            JsonObject result = await request.ConfigureAwait(false);
            string content = result["content"] is { } node ? node.ToJsonString(MockBehaviour.ContentJsonOptions) : "none";
            return string.Concat("Got action=", (string?)result["action"], " content=", content);
        }
        catch (InvalidOperationException)
        {
            return $"Elicitation unavailable: {MockBehaviour.NotAdvertisedReason}";
        }
        catch (FakeRpcError error)
        {
            return $"Elicitation unavailable: error {error.Code.ToString(CultureInfo.InvariantCulture)}: {error.Message}";
        }
    }

    /// <summary>
    /// Splits a reply into <see cref="TargetChunkCount"/> chunks at word boundaries without losing a
    /// character: every word keeps the space that followed it, so the chunks join back into exactly
    /// <paramref name="reply"/>. (The echo's splitter drops those spaces, which would corrupt a reply
    /// whose exact text the test reads.)
    /// </summary>
    private static List<string> SplitReplyIntoChunks(string reply)
    {
        string[] words = reply.Split(' ');
        for (int i = 0; i < words.Length - 1; i++)
        {
            words[i] += " ";
        }

        return MockBehaviour.GroupIntoChunks(words, string.Empty, MockBehaviour.TargetChunkCount);
    }

    /// <summary>Concatenates every text content block of a <c>session/prompt</c> request's <c>prompt</c> array.</summary>
    private static string ExtractPromptText(JsonObject parameters)
    {
        if (parameters["prompt"] is not JsonArray blocks)
        {
            return string.Empty;
        }

        List<string> texts = new();
        foreach (JsonNode? block in blocks)
        {
            if (block is JsonObject blockObject
                && string.Equals((string?)blockObject["type"], "text", StringComparison.Ordinal)
                && (string?)blockObject["text"] is string text)
            {
                texts.Add(text);
            }
        }

        return string.Join(' ', texts);
    }

    /// <summary>
    /// Splits <paramref name="prompt"/> into at least <see cref="TargetChunkCount"/> chunks
    /// whenever there is enough material to do so. Word-splitting is tried first, since it
    /// produces the most natural-looking chunks. A prompt with fewer than
    /// <see cref="TargetChunkCount"/> words falls back to splitting its characters instead, which
    /// can still reach the target from as little as <see cref="TargetChunkCount"/> characters —
    /// "hi there" (two words, eight characters) still yields three chunks this way. Only a prompt
    /// with fewer than <see cref="TargetChunkCount"/> characters, including an empty one, truly
    /// cannot reach the target; that case returns <see cref="FallbackChunks"/>, which keeps the
    /// "more than one chunk" guarantee that streaming depends on even when there is nothing to echo.
    /// </summary>
    private static IReadOnlyList<string> SplitIntoChunks(string prompt)
    {
        string trimmed = prompt.Trim();
        string[] words = trimmed.Split(MockBehaviour.WordSeparators, StringSplitOptions.RemoveEmptyEntries);

        if (words.Length >= MockBehaviour.TargetChunkCount)
        {
            return MockBehaviour.GroupIntoChunks(words, " ", MockBehaviour.TargetChunkCount);
        }

        if (trimmed.Length >= MockBehaviour.TargetChunkCount)
        {
            string[] characters = new string[trimmed.Length];
            for (int i = 0; i < trimmed.Length; i++)
            {
                characters[i] = trimmed[i].ToString();
            }

            return MockBehaviour.GroupIntoChunks(characters, string.Empty, MockBehaviour.TargetChunkCount);
        }

        return MockBehaviour.FallbackChunks;
    }

    /// <summary>Distributes <paramref name="tokens"/> into <paramref name="groupCount"/> non-empty, near-even groups, joined by <paramref name="separator"/>.</summary>
    private static List<string> GroupIntoChunks(string[] tokens, string separator, int groupCount)
    {
        List<string> chunks = new(groupCount);
        int baseSize = tokens.Length / groupCount;
        int remainder = tokens.Length % groupCount;
        int index = 0;

        for (int i = 0; i < groupCount; i++)
        {
            int size = baseSize + (i < remainder ? 1 : 0);
            chunks.Add(string.Join(separator, tokens.Skip(index).Take(size)));
            index += size;
        }

        return chunks;
    }
}
