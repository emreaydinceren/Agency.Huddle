namespace Agency.Huddle.Acp.Tests.Fakes;

using System.Text.Json.Nodes;
using System.Threading.Tasks;

/// <summary>
/// Handed to a <see cref="FakeAcpAgent.OnPrompt"/> script so a test can drive one turn: send
/// session updates, make agent-initiated requests of the client, and observe cancellation.
/// </summary>
internal sealed class PromptContext
{
    private readonly FakeAcpAgent agent;

    private readonly TaskCompletionSource cancelSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

    internal PromptContext(FakeAcpAgent agent, string sessionId, JsonObject parameters)
    {
        this.agent = agent;
        this.SessionId = sessionId;
        this.Params = parameters;
    }

    internal string SessionId { get; }

    internal JsonObject Params { get; }

    internal Task SendTextChunkAsync(string text)
    {
        return this.SendChunkAsync("agent_message_chunk", text);
    }

    internal Task SendThoughtChunkAsync(string text)
    {
        return this.SendChunkAsync("agent_thought_chunk", text);
    }

    internal Task SendToolCallAsync(string id, string title, string kind, string status, JsonNode? rawInput = null)
    {
        JsonObject update = new JsonObject
        {
            ["sessionUpdate"] = "tool_call",
            ["toolCallId"] = id,
            ["title"] = title,
            ["kind"] = kind,
            ["status"] = status,
        };

        if (rawInput is not null)
        {
            update["rawInput"] = rawInput;
        }

        return this.SendUpdateAsync(update);
    }

    internal Task SendToolCallUpdateAsync(string id, string status, JsonNode? rawOutput = null)
    {
        JsonObject update = new JsonObject
        {
            ["sessionUpdate"] = "tool_call_update",
            ["toolCallId"] = id,
            ["status"] = status,
        };

        if (rawOutput is not null)
        {
            update["rawOutput"] = rawOutput;
        }

        return this.SendUpdateAsync(update);
    }

    internal Task SendUpdateAsync(JsonObject rawUpdate)
    {
        JsonObject message = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["method"] = "session/update",
            ["params"] = new JsonObject
            {
                ["sessionId"] = this.SessionId,
                ["update"] = rawUpdate,
            },
        };

        return this.agent.WriteMessageAsync(message);
    }

    internal async Task<JsonObject> RequestPermissionAsync(JsonObject toolCall, JsonObject[] options)
    {
        JsonArray optionsArray = new JsonArray();
        foreach (JsonObject option in options)
        {
            optionsArray.Add(option.DeepClone());
        }

        JsonObject parameters = new JsonObject
        {
            ["sessionId"] = this.SessionId,
            ["toolCall"] = toolCall.DeepClone(),
            ["options"] = optionsArray,
        };

        JsonObject result = await this.agent.SendRequestAsync("session/request_permission", parameters).ConfigureAwait(false);
        return (JsonObject)result["outcome"]!;
    }

    /// <summary>
    /// Sends the real wire request <c>elicitation/create</c> in form mode, as the adapter does for an
    /// AskUserQuestion, a refusal dialog or an MCP form, and returns the whole result object
    /// (<c>action</c>, and <c>content</c> when accepted). Refuses, as the real adapter would not send
    /// the request at all, when the client never advertised <c>elicitation.form</c> in
    /// <c>initialize</c>.
    /// </summary>
    /// <param name="message">The prompt text.</param>
    /// <param name="requestedSchema">The form's JSON Schema, an object with <c>type</c> and <c>properties</c>.</param>
    /// <param name="toolCallId">The triggering tool call id, or null to send none (a refusal dialog or an MCP form carries none).</param>
    /// <returns>The client's result object.</returns>
    /// <exception cref="InvalidOperationException">The client never advertised <c>elicitation.form</c>.</exception>
    internal async Task<JsonObject> RequestElicitationAsync(string message, JsonObject requestedSchema, string? toolCallId = null)
    {
        if (!this.agent.ElicitationFormAdvertised)
        {
            throw new InvalidOperationException(
                "The client did not advertise elicitation.form in initialize, so a real adapter would not send elicitation/create; the fake refuses to.");
        }

        JsonObject parameters = new JsonObject
        {
            ["sessionId"] = this.SessionId,
            ["mode"] = "form",
            ["message"] = message,
            ["requestedSchema"] = requestedSchema.DeepClone(),
        };

        if (toolCallId is not null)
        {
            parameters["toolCallId"] = toolCallId;
        }

        return await this.agent.SendRequestAsync("elicitation/create", parameters).ConfigureAwait(false);
    }


    internal Task WaitForCancelAsync()
    {
        return this.cancelSource.Task;
    }

    internal void SignalCancel()
    {
        this.cancelSource.TrySetResult();
    }

    private Task SendChunkAsync(string discriminator, string text)
    {
        JsonObject update = new JsonObject
        {
            ["sessionUpdate"] = discriminator,
            ["content"] = new JsonObject
            {
                ["type"] = "text",
                ["text"] = text,
            },
        };

        return this.SendUpdateAsync(update);
    }
}
