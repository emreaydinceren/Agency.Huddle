namespace Agency.Huddle.App.Prompts;

/// <summary>
/// The fixed catalog of every model-facing prompt this application renders, together with the default
/// wording each one falls back to when no override is configured.
/// </summary>
/// <remarks>
/// <para>
/// This is the authority for what prompts exist. A later JSON configuration file only ever supplies an
/// override for <see cref="PromptDefinition.Default"/> keyed by <see cref="PromptDefinition.Key"/>; it can
/// never add, remove, or change the shape of a prompt.
/// </para>
/// <para>
/// Every default below was lifted verbatim — character for character, including em-dashes, alignment
/// and line wrapping — from the source it replaces, so that reproducing today's output byte-for-byte
/// (see <c>PromptGoldenTests</c>) is a matter of substituting the same values back in, not retyping
/// prose. Nothing here is rendered or joined by this type; that is left to later call sites.
/// </para>
/// </remarks>
internal static class PromptCatalog
{
    /// <summary>Every prompt this application knows about, in a stable, human-meaningful order.</summary>
    internal static IReadOnlyList<PromptDefinition> All { get; } = BuildAll();

    /// <summary>Looks a prompt up by its <see cref="PromptDefinition.Key"/>.</summary>
    /// <param name="key">The prompt's key, e.g. <c>"turn.roomLabel"</c>.</param>
    /// <returns>The matching <see cref="PromptDefinition"/>.</returns>
    /// <exception cref="KeyNotFoundException">No prompt in <see cref="All"/> has this key.</exception>
    internal static PromptDefinition Get(string key)
    {
        foreach (var prompt in All)
        {
            if (string.Equals(prompt.Key, key, StringComparison.Ordinal))
            {
                return prompt;
            }
        }

        throw new KeyNotFoundException($"No prompt is registered with key '{key}'.");
    }

    /// <summary>Builds the catalog's fixed contents.</summary>
    /// <returns>The full list of prompt definitions.</returns>
    private static IReadOnlyList<PromptDefinition> BuildAll() =>
    [
        new PromptDefinition(
            Key: "systemPrompt.orientation",
            Label: "Orientation",
            HelperText:
                "The opening lines of every Persona's system prompt: what kind of application this is and " +
                "when to call the help tool. Must keep {{helpTool}}: it is the only mention of the help tool's " +
                "name that reaches a model before it has read anything else.",
            Default:
                """
                You are a teammate in Team, a chat application. You talk to a human and to other agents
                in chat Rooms; you are not working alone at a terminal.

                Before you use any Team tool, and whenever you are unsure how something here works, call
                {{helpTool}}. It explains Rooms, mentions and who is expected to reply when, and
                it lists every tool available to you with its exact name. It takes no arguments.
                """,
            Placeholders: ["{{helpTool}}"],
            RequiredPlaceholders: ["{{helpTool}}"],
            Timing: PromptTiming.NextSession),

        new PromptDefinition(
            Key: "systemPrompt.identity",
            Label: "Identity",
            HelperText:
                "The single line that tells a Persona its own speaking name. Must keep {{personaName}}: " +
                "without it every Agent would answer as the same unnamed voice.",
            Default:
                """
                You are a member of the Team chat application. You speak as "{{personaName}}".
                """,
            Placeholders: ["{{personaName}}"],
            RequiredPlaceholders: ["{{personaName}}"],
            Timing: PromptTiming.NextSession),

        new PromptDefinition(
            Key: "systemPrompt.chatRules",
            Label: "Chat rules",
            HelperText:
                "The paragraph explaining when to answer (a two-member Room vs. a group) and how to " +
                "write a mention. Takes no placeholders, so an override is plain prose.",
            Default:
                """
                A Room with two members is a private conversation with the human: answer every
                message. A Room with three or more members is a group: answer only when you are
                @-mentioned. Address another member by writing @ followed by their exact name.
                A name may contain spaces, so "@Chief of Staff" is one mention of one member;
                write the name exactly as it is given to you, with no quotes around it.
                """,
            Placeholders: [],
            RequiredPlaceholders: [],
            Timing: PromptTiming.NextSession),

        new PromptDefinition(
            Key: "systemPrompt.tools",
            Label: "Tools",
            HelperText:
                "The closing paragraph that names the application's own tools and explains how to use " +
                "them. Must keep {{toolNames}}: it is where the current, exact list of tool names is " +
                "substituted in.",
            Default:
                """
                These tools run inside the application process:
                {{toolNames}}.
                Use them to learn how this application works, to find out who exists, to start a
                Room with other agents, to add an agent to a Room that already exists, to speak
                into a Room other than the one you were addressed in, and to ask to be woken by
                every message in a Room you are waiting on. Your reply to the current
                message is just your answer text — do not also post it with a tool.
                Never answer questions about agents or Rooms from the codebase.
                """,
            Placeholders: ["{{toolNames}}"],
            RequiredPlaceholders: ["{{toolNames}}"],
            Timing: PromptTiming.NextSession),

        new PromptDefinition(
            Key: "turn.roomLabel",
            Label: "Room label",
            HelperText:
                "How each incoming message names the Room it came from. Must keep {{roomId}}: it is the " +
                "only way an agent learns a Room's id.",
            Default:
                """
                [Room: {{roomName}} (id: {{roomId}})]
                """,
            Placeholders: ["{{roomName}}", "{{roomId}}"],
            RequiredPlaceholders: ["{{roomId}}"],
            Timing: PromptTiming.Live),

        new PromptDefinition(
            Key: "turn.message",
            Label: "Message",
            HelperText:
                "How a single delivered message is shown to the model that was addressed. All three " +
                "placeholders are required: drop any one and the model loses the Room, the speaker, or " +
                "what was said.",
            Default:
                """
                {{roomLabel}} {{sender}}: {{text}}
                """,
            Placeholders: ["{{roomLabel}}", "{{sender}}", "{{text}}"],
            RequiredPlaceholders: ["{{roomLabel}}", "{{sender}}", "{{text}}"],
            Timing: PromptTiming.Live),

        new PromptDefinition(
            Key: "turn.catchUpHeader",
            Label: "Catch-up header",
            HelperText:
                "The line introducing earlier messages the model was not addressed in, shown once above " +
                "them. {{roomLabel}} is optional here since the Room is usually already established by " +
                "the surrounding turn.",
            Default:
                """
                {{roomLabel}} You were not addressed in these earlier messages, they are context only:
                """,
            Placeholders: ["{{roomLabel}}"],
            RequiredPlaceholders: [],
            Timing: PromptTiming.Live),

        new PromptDefinition(
            Key: "turn.catchUpLine",
            Label: "Catch-up line",
            HelperText:
                "One line of catch-up context, repeated once per missed message. Both placeholders are " +
                "required: without the sender a line of catch-up context is unattributed.",
            Default:
                """
                {{sender}}: {{text}}
                """,
            Placeholders: ["{{sender}}", "{{text}}"],
            RequiredPlaceholders: ["{{sender}}", "{{text}}"],
            Timing: PromptTiming.Live),

        new PromptDefinition(
            Key: "getHelp.intro",
            Label: "Help: introduction",
            HelperText:
                "The opening lines of get_help's output, before any section heading. Takes no " +
                "placeholders.",
            Default:
                """
                Team is a chat application. You are one Teammate in it, talking to a human and to other
                agents in Rooms. You are not working alone at a terminal.
                """,
            Placeholders: [],
            RequiredPlaceholders: [],
            Timing: PromptTiming.Live),

        new PromptDefinition(
            Key: "getHelp.rooms",
            Label: "Help: Rooms",
            HelperText:
                "The ROOMS section of get_help: what a Room is and when to answer in one. Takes no " +
                "placeholders.",
            Default:
                """
                ROOMS
                A Room is a conversation with a fixed set of members. There is only one kind of Room, and
                how you behave in it follows from how many members it has:
                  - two members  — a private conversation with the human. Answer every message.
                  - three or more — a group. Answer only when you are @-mentioned.
                """,
            Placeholders: [],
            RequiredPlaceholders: [],
            Timing: PromptTiming.Live),

        new PromptDefinition(
            Key: "getHelp.messages",
            Label: "Help: messages",
            HelperText:
                "The MESSAGES section of get_help: how the Room label is written and what \"context " +
                "only\" means. Takes no placeholders.",
            Default:
                """
                MESSAGES
                Every message you are given starts with the Room it came from, written as
                "[Room: <name> (id: <id>)]". That id is what the tools below mean by a room id.
                Messages marked "context only" are ones you were not addressed in; read them for
                background, do not answer them.
                """,
            Placeholders: [],
            RequiredPlaceholders: [],
            Timing: PromptTiming.Live),

        new PromptDefinition(
            Key: "getHelp.mentions",
            Label: "Help: mentions",
            HelperText:
                "The MENTIONS section of get_help: how to address another member by name. Takes no " +
                "placeholders.",
            Default:
                """
                MENTIONS
                Address another member by writing @ followed by their exact name. A name may contain
                spaces, so "@Chief of Staff" is one mention of one member. Write the name exactly as it
                was given to you, with no quotes around it.
                """,
            Placeholders: [],
            RequiredPlaceholders: [],
            Timing: PromptTiming.Live),

        new PromptDefinition(
            Key: "getHelp.replying",
            Label: "Help: replying",
            HelperText:
                "The REPLYING section of get_help: that a reply is plain answer text, not a tool call. " +
                "Takes no placeholders.",
            Default:
                """
                REPLYING
                Your reply to the message you were given is simply your answer text. Do not also post it
                with a tool — that would deliver it twice.
                """,
            Placeholders: [],
            RequiredPlaceholders: [],
            Timing: PromptTiming.Live),

        new PromptDefinition(
            Key: "getHelp.budget",
            Label: "Help: budget",
            HelperText:
                "The BUDGET section of get_help: why a Room caps agent messages and what a refusal " +
                "means. Takes no placeholders.",
            Default:
                """
                BUDGET
                A Room takes only so many agent messages between one human message and the next, so that
                two agents answering each other cannot run on unattended. When a Room reaches that limit
                it stops accepting agent messages and tells the human, who can allow more. A refusal that
                says the budget is spent is final: do not retry it, and do not work around it by posting
                to another Room. Say nothing further there until the human speaks.
                """,
            Placeholders: [],
            RequiredPlaceholders: [],
            Timing: PromptTiming.Live),

        new PromptDefinition(
            Key: "getHelp.toolsHeader",
            Label: "Help: tools heading",
            HelperText: "The heading introducing the catalog of tools in get_help's output. Takes no placeholders.",
            Default:
                """
                TOOLS
                """,
            Placeholders: [],
            RequiredPlaceholders: [],
            Timing: PromptTiming.Live),

        new PromptDefinition(
            Key: "getHelp.toolEntry",
            Label: "Help: tool entry",
            HelperText:
                "One tool's line in get_help's catalog, repeated once per tool. {{toolName}} arrives " +
                "already prefixed (e.g. mcp__team__get_help); both placeholders are required or the " +
                "entry names no tool or explains nothing about it.",
            Default:
                """
                  {{toolName}}
                      {{toolDescription}}


                """,
            Placeholders: ["{{toolName}}", "{{toolDescription}}"],
            RequiredPlaceholders: ["{{toolName}}", "{{toolDescription}}"],
            Timing: PromptTiming.Live),

        new PromptDefinition(
            Key: "getHelp.footer",
            Label: "Help: footer",
            HelperText:
                "The closing lines of get_help's output, after the tool catalog: that the tools are the " +
                "only source of truth. Takes no placeholders.",
            Default:
                """
                Never answer a question about who exists, or about Rooms, from a codebase or from memory.
                Call the tools: they are the only source of truth about this application.
                """,
            Placeholders: [],
            RequiredPlaceholders: [],
            Timing: PromptTiming.Live),

        new PromptDefinition(
            Key: "tool.getHelp.description",
            Label: "get_help description",
            HelperText:
                "The one-line job description a model reads for the get_help tool itself, in its own " +
                "tool listing. Takes no placeholders.",
            Default:
                """
                Explains how the Team chat application works — Rooms, members, mentions, and who is expected to reply when — and lists every tool you can call here with its exact name. Call this before using any other Team tool, and whenever you are unsure how something in the chat works. It takes no arguments.
                """,
            Placeholders: [],
            RequiredPlaceholders: [],
            Timing: PromptTiming.NextSession),

        new PromptDefinition(
            Key: "tool.listAgents.description",
            Label: "list_agents description",
            HelperText:
                "The one-line job description a model reads for the list_agents tool. Takes no " +
                "placeholders.",
            Default:
                """
                Lists every agent known to the Team application: each registered Agent, noting whether it is currently online, and each available Persona that could be brought online. Each entry includes its job description, drawn from the Persona's frontmatter when one is present. Call this before creating a Room or naming another agent, so you know which agent names actually exist and what they are for.
                """,
            Placeholders: [],
            RequiredPlaceholders: [],
            Timing: PromptTiming.NextSession),

        new PromptDefinition(
            Key: "tool.createRoom.description",
            Label: "create_room description",
            HelperText:
                "The one-line job description a model reads for the create_room tool. Takes no " +
                "placeholders.",
            Default:
                """
                Creates a new Room and adds the named agents to it, alongside the calling Agent and the Human. Use this to start a side conversation with one or more other agents. Provide each agent's name in the 'agents' array; call list_agents first if you are unsure which names exist. Pass 'seed' as well: the opening message to post into the new Room as it is created, so the agents you named learn why they are there in the same turn they are added — a Room that arrives with no statement of why leaves them nothing to act on. The Room is named after its Agents, the same way every other Room is.
                """,
            Placeholders: [],
            RequiredPlaceholders: [],
            Timing: PromptTiming.NextSession),

        new PromptDefinition(
            Key: "tool.inviteAgent.description",
            Label: "invite_agent description",
            HelperText:
                "The one-line job description a model reads for the invite_agent tool. Takes no " +
                "placeholders.",
            Default:
                """
                Adds an agent to a Room that already exists, so it starts receiving that Room's messages. Give the Room's id — the id shown in the '[Room: ...]' line at the start of every message you receive — and the agent's exact name; call list_agents first if you are unsure which names exist. Use create_room instead when the Room does not exist yet. The Room is renamed after its Agents, as it is on every other change.
                """,
            Placeholders: [],
            RequiredPlaceholders: [],
            Timing: PromptTiming.NextSession),

        new PromptDefinition(
            Key: "tool.postMessage.description",
            Label: "post_message description",
            HelperText:
                "The one-line job description a model reads for the post_message tool. Takes no " +
                "placeholders.",
            Default:
                """
                Posts a Message into a Room, as the calling Agent. Use this to speak into a Room other than the one you were addressed in — for example a Room you just created with create_room — because your reply in the current turn is only ever delivered to that Room, never to another one. Requires the target Room's id and the text to post.
                """,
            Placeholders: [],
            RequiredPlaceholders: [],
            Timing: PromptTiming.NextSession),

        new PromptDefinition(
            Key: "tool.followRoom.description",
            Label: "follow_room description",
            HelperText:
                "The one-line job description a model reads for the follow_room tool. Takes no " +
                "placeholders.",
            Default:
                """
                Asks to be woken by every Message in a Room, even when you are not mentioned. Use this on a Room you created for other agents to work in, so you hear their answers without each of them having to name you. Give the Room's id — the id create_room returned, or the id shown in the '[Room: ...]' line at the start of every message you receive. Following spends a turn on every message posted there, including exchanges between other agents, so follow only while you are waiting on that Room and call unfollow_room when the work is done.
                """,
            Placeholders: [],
            RequiredPlaceholders: [],
            Timing: PromptTiming.NextSession),

        new PromptDefinition(
            Key: "tool.unfollowRoom.description",
            Label: "unfollow_room description",
            HelperText:
                "The one-line job description a model reads for the unfollow_room tool. Takes no " +
                "placeholders.",
            Default:
                """
                Stops being woken by every Message in a Room you are following. After this you are woken there only when a message mentions you by name, which is the ordinary rule. Give the Room's id — the same id you gave to follow_room. Calling this for a Room you are not following changes nothing and says so.
                """,
            Placeholders: [],
            RequiredPlaceholders: [],
            Timing: PromptTiming.NextSession),
    ];
}
