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
                into a Room other than the one you were addressed in, to track work as Tasks and
                hand it to a Teammate, and to ask to be woken by
                every message in a Room you are waiting on. Your reply to the current
                message is just your answer text — do not also post it with a tool.
                Never answer questions about agents or Rooms from the codebase.
                """,
            Placeholders: ["{{toolNames}}"],
            RequiredPlaceholders: ["{{toolNames}}"],
            Timing: PromptTiming.NextSession),

        new PromptDefinition(
            Key: "systemPrompt.skills",
            Label: "Skills block",
            HelperText:
                "The closing block naming a Persona's assigned Skills, appended only when it holds at " +
                "least one. Must keep {{readSkillTool}} and {{skillIndex}}: the first is the only mention " +
                "of the read_skill tool's name, the second is where the Skill Index itself is substituted in.",
            Default:
                """
                You hold these Skills. Each is know-how for one kind of work. When the conversation
                calls for one, read it with {{readSkillTool}} before acting, and follow it.

                {{skillIndex}}
                """,
            Placeholders: ["{{skillIndex}}", "{{readSkillTool}}"],
            RequiredPlaceholders: ["{{skillIndex}}", "{{readSkillTool}}"],
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
            Key: "turn.transcriptHeader",
            Label: "Transcript header",
            HelperText:
                "The line introducing a fresh Room Session's first Turn (RS §6.5): the Transcript " +
                "range that replaces the catch-up buffer on that Turn only. {{roomLabel}} is required.",
            Default:
                """
                {{roomLabel}} This is a new session for this Room. Its recent Messages, oldest first, including your own:
                """,
            Placeholders: ["{{roomLabel}}"],
            RequiredPlaceholders: ["{{roomLabel}}"],
            Timing: PromptTiming.Live),

        new PromptDefinition(
            Key: "turn.transcriptResumedHeader",
            Label: "Transcript resumed header",
            HelperText:
                "The line introducing a resumed Room Session's first Turn (RS §6.5): only the " +
                "Messages posted after the stored LastMessageId. {{roomLabel}} is required.",
            Default:
                """
                {{roomLabel}} While this session was closed, these Messages were posted here:
                """,
            Placeholders: ["{{roomLabel}}"],
            RequiredPlaceholders: ["{{roomLabel}}"],
            Timing: PromptTiming.Live),

        new PromptDefinition(
            Key: "turn.transcriptOmitted",
            Label: "Transcript omitted",
            HelperText:
                "The line stating how many earlier Messages in range were left out of the Transcript " +
                "block (RS §6.5). {{count}} is required: without it the line does not say how many.",
            Default:
                """
                …{{count}} earlier Messages are not shown.
                """,
            Placeholders: ["{{count}}"],
            RequiredPlaceholders: ["{{count}}"],
            Timing: PromptTiming.Live),

        new PromptDefinition(
            Key: "turn.ownPostLine",
            Label: "Own post line",
            HelperText:
                "One line of catch-up context for a Message the Agent itself posted into this Room " +
                "from a Turn in another Room (RS §6.7). {{text}} is required: without it the line " +
                "says nothing of what was posted.",
            Default:
                """
                You, from another Room: {{text}}
                """,
            Placeholders: ["{{text}}"],
            RequiredPlaceholders: ["{{text}}"],
            Timing: PromptTiming.Live),

        new PromptDefinition(
            Key: "turn.fileChangesHeader",
            Label: "File Changes header",
            HelperText:
                "The line introducing the File Changes block, shown once above it. \"Read one only if " +
                "it matters to what you are doing now\" is the most important part of this sentence " +
                "(FC §6.13): without it an eager model reads every listed file on every Turn.",
            Default:
                """
                Since your last Turn in this Room, these files changed. Read one only if it matters to what you are doing now:
                """,
            Placeholders: [],
            RequiredPlaceholders: [],
            Timing: PromptTiming.Live),

        new PromptDefinition(
            Key: "turn.fileAdded",
            Label: "File added",
            HelperText:
                "One line of the File Changes block, repeated once per file added since the Agent's " +
                "last Turn in this Room. {{path}} is required: without it the line names no file.",
            Default:
                """
                added {{path}}
                """,
            Placeholders: ["{{path}}"],
            RequiredPlaceholders: ["{{path}}"],
            Timing: PromptTiming.Live),

        new PromptDefinition(
            Key: "turn.fileChanged",
            Label: "File changed",
            HelperText:
                "One line of the File Changes block, repeated once per file changed since the Agent's " +
                "last Turn in this Room. {{path}} is required: without it the line names no file.",
            Default:
                """
                changed {{path}}
                """,
            Placeholders: ["{{path}}"],
            RequiredPlaceholders: ["{{path}}"],
            Timing: PromptTiming.Live),

        new PromptDefinition(
            Key: "turn.fileDeleted",
            Label: "File deleted",
            HelperText:
                "One line of the File Changes block, repeated once per file deleted since the Agent's " +
                "last Turn in this Room. {{path}} is required: without it the line names no file.",
            Default:
                """
                deleted {{path}}
                """,
            Placeholders: ["{{path}}"],
            RequiredPlaceholders: ["{{path}}"],
            Timing: PromptTiming.Live),

        new PromptDefinition(
            Key: "turn.fileChangesMore",
            Label: "File Changes more",
            HelperText:
                "The closing line of the File Changes block when more changes exist than were listed. " +
                "{{count}} is required: without it the line does not say how many were left out.",
            Default:
                """
                …and {{count}} more.
                """,
            Placeholders: ["{{count}}"],
            RequiredPlaceholders: ["{{count}}"],
            Timing: PromptTiming.Live),

        new PromptDefinition(
            Key: "turn.folderUnchecked",
            Label: "Folder unchecked",
            HelperText:
                "One line of the File Changes block, repeated once per Watched Folder that held more " +
                "files than the cap and so was not scanned. {{path}} is required: {{max}} is available " +
                "for an override that wants to state the cap too.",
            Default:
                """
                {{path}} has more than {{max}} files, so it was not checked.
                """,
            Placeholders: ["{{path}}", "{{max}}"],
            RequiredPlaceholders: ["{{path}}"],
            Timing: PromptTiming.Live),

        new PromptDefinition(
            Key: "turn.greeting",
            Label: "Greeting",
            HelperText:
                "The Chief of Staff's unprompted first message to a new Human, sent once, when its room " +
                "with the Human has taken no messages yet. {{roomLabel}} is required: without it the " +
                "model has no room to post its reply into.",
            Default:
                """
                {{roomLabel}}
                The Human has just started using this application and has not written anything yet. This is
                your first Message to them, and nothing prompted it. Greet them: if you hold a Skill for
                this, read it first and follow it. Keep it to one Message that ends with one question.
                """,
            Placeholders: ["{{roomLabel}}"],
            RequiredPlaceholders: ["{{roomLabel}}"],
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

        new PromptDefinition(
            Key: "tool.readSkill.description",
            Label: "read_skill description",
            HelperText:
                "The one-line job description a model reads for the read_skill tool. Takes no " +
                "placeholders.",
            Default:
                """
                Reads one of your Skills: its main file, or a supporting file it names. Give the Skill's name, and optionally 'file'. Read a Skill before acting on it.
                """,
            Placeholders: [],
            RequiredPlaceholders: [],
            Timing: PromptTiming.NextSession),

        new PromptDefinition(
            Key: "tool.validateTeammate.description",
            Label: "validate_teammate description",
            HelperText:
                "The one-line job description a model reads for the validate_teammate tool. Takes no " +
                "placeholders.",
            Default:
                """
                Checks one proposed Teammate without creating anything. Returns 'Valid.' or every problem, one per line. Free: call it until the Candidate is clean.
                """,
            Placeholders: [],
            RequiredPlaceholders: [],
            Timing: PromptTiming.NextSession),

        new PromptDefinition(
            Key: "tool.proposeTeammates.description",
            Label: "propose_teammates description",
            HelperText:
                "The one-line job description a model reads for the propose_teammates tool. Takes no " +
                "placeholders.",
            Default:
                """
                Asks the Human to approve new Teammates. Give the id of the Room you are talking in and one to four Candidates. Nothing is created until the Human approves; you will be told the outcome in that Room.
                """,
            Placeholders: [],
            RequiredPlaceholders: [],
            Timing: PromptTiming.NextSession),

        new PromptDefinition(
            Key: "tool.watchFolder.description",
            Label: "watch_folder description",
            HelperText:
                "The one-line job description a model reads for the watch_folder tool. Takes no " +
                "placeholders.",
            Default:
                """
                Watches a folder, so that on each of your later Turns the files added, changed or deleted there since your last Turn in that Room are listed, by full path, at the top of your prompt. A change you make yourself is not listed in the Room you made it in. Name a Teammate to watch their working folder, or give a folder inside App_Data. Your own working folder is always watched. Use it for folders you depend on but do not own, such as a shared notes folder or another Teammate's output. It lasts until you call unwatch_folder, even across a restart. Read a listed file only when it matters to what you are doing.
                """,
            Placeholders: [],
            RequiredPlaceholders: [],
            Timing: PromptTiming.NextSession),

        new PromptDefinition(
            Key: "tool.unwatchFolder.description",
            Label: "unwatch_folder description",
            HelperText:
                "The one-line job description a model reads for the unwatch_folder tool. Takes no " +
                "placeholders.",
            Default:
                """
                Stops listing file changes for a folder you started watching with watch_folder. Your own folder, and folders your Persona lists, stay watched.
                """,
            Placeholders: [],
            RequiredPlaceholders: [],
            Timing: PromptTiming.NextSession),

        new PromptDefinition(
            Key: "turn.fileByYouSuffix",
            Label: "File Changes: by-you suffix",
            HelperText:
                "Appended to a changed file's line when this Agent's own earlier Turn, in another Room, " +
                "wrote it last (FC §6.15). Must keep {{roomName}}: it names which of this Agent's Rooms " +
                "made the edit. Keep the leading space: it joins directly onto the file's own line.",
            Default: " (by you, in Room '{{roomName}}')",
            Placeholders: ["{{roomName}}"],
            RequiredPlaceholders: ["{{roomName}}"],
            Timing: PromptTiming.Live),

        new PromptDefinition(
            Key: "systemPrompt.memory",
            Label: "Memory block",
            HelperText:
                "Explains an Agent's memory folder and shows its current index, appended to the system " +
                "prompt after Skills (FC §6.15). Must keep {{memoryPath}} and {{memoryIndex}}: without " +
                "the first the Agent cannot name where to write, and without the second it is told " +
                "nothing of what it already remembers.",
            Default:
                """
                Your memory is the folder {{memoryPath}}. It belongs to you, not to any one Room: it survives
                restarts, and every copy of you in your other Rooms reads the same folder. To remember
                something from now on, write one Markdown file there per fact. Make its first line the fact
                itself, in one sentence, such as "The Human prefers C# for all code.", and give the file a
                short descriptive name. When a fact changes, edit its file, and delete it when it no longer
                holds. Remember only what should hold in every Room: preferences, standing decisions, facts
                about ongoing work. Do not write down something said for one Room's audience only, and do not
                copy the conversation itself. When a memory file changes, including when another copy of you
                writes it, the change is listed at the start of your next Turn in each Room. Do not claim to
                remember what is not in your memory or in this conversation. Your memory now holds:
                {{memoryIndex}}
                """,
            Placeholders: ["{{memoryPath}}", "{{memoryIndex}}"],
            RequiredPlaceholders: ["{{memoryPath}}", "{{memoryIndex}}"],
            Timing: PromptTiming.NextSession),

        new PromptDefinition(
            Key: "systemPrompt.memoryEntry",
            Label: "Memory index entry",
            HelperText:
                "One line of the memory index per remembered fact. Must keep {{summary}} and {{path}}: " +
                "the summary is what the Agent reads at a glance, the path is where to read the rest.",
            Default: "- {{summary}} ({{path}})",
            Placeholders: ["{{summary}}", "{{path}}"],
            RequiredPlaceholders: ["{{summary}}", "{{path}}"],
            Timing: PromptTiming.NextSession),

        new PromptDefinition(
            Key: "systemPrompt.memoryEmpty",
            Label: "Memory index, empty",
            HelperText: "Shown in place of the memory index when the Agent has written nothing yet.",
            Default: "Nothing yet.",
            Placeholders: [],
            RequiredPlaceholders: [],
            Timing: PromptTiming.NextSession),

        new PromptDefinition(
            Key: "systemPrompt.memoryMore",
            Label: "Memory index, more",
            HelperText:
                "Appended after the listed memory entries when more exist than the cap shows. Must keep " +
                "{{count}}: without it the Agent has no idea how much more memory it is not seeing.",
            Default: "…and {{count}} more in {{memoryPath}}.",
            Placeholders: ["{{count}}", "{{memoryPath}}"],
            RequiredPlaceholders: ["{{count}}"],
            Timing: PromptTiming.NextSession),

        new PromptDefinition(
            Key: "systemPrompt.roomSessions",
            Label: "Room Sessions",
            HelperText:
                "D28, RS §6.9: told to every Persona whose resolved Adapter Profile has SessionPerRoom " +
                "true. Describes the one session per Room, truthfully. Takes no placeholders, so an " +
                "override is plain prose, and must not name any one Room: identity stays in the " +
                "Turn's own label (RS §6.9, \"Room identity stays out of the system prompt\").",
            Default:
                """
                Each Room you are in is a separate conversation, and this session holds exactly one of them.
                Every Message you receive here comes from the Room its label names, and you answer into that
                Room. Your other Rooms have sessions of their own, which you cannot see from here. Treat each
                Room as its own audience: do not assume the people here know what was said in another Room, and
                do not bring it up here. If a Message seems to continue something you cannot see, say so and ask
                rather than guess. Describe your own memory truthfully: you remember this Room's conversation,
                and you do not remember your other Rooms' conversations.
                """,
            Placeholders: [],
            RequiredPlaceholders: [],
            Timing: PromptTiming.NextSession),

        new PromptDefinition(
            Key: "systemPrompt.roomSessionsCarry",
            Label: "Room Sessions, what still carries",
            HelperText:
                "D28, RS §6.9: appended after systemPrompt.roomSessions only when the resolved Adapter " +
                "can read files (FC §6.11) - the two routes named here (memory, File Changes) exist " +
                "only then. Takes no placeholders.",
            Default:
                """
                Two things do cross between your Rooms: the files in your memory folder, and the file changes
                listed at the start of a Turn. If something should hold in every Room, write it to your memory.
                """,
            Placeholders: [],
            RequiredPlaceholders: [],
            Timing: PromptTiming.NextSession),

        new PromptDefinition(
            Key: "systemPrompt.sharedSession",
            Label: "Shared session",
            HelperText:
                "D16 P0-1: told to every Persona, in every mode, while SessionPerRoom stays false in " +
                "Phase 0 (RS §6.9, §8.1). Describes the one session every Room's Messages arrive in, " +
                "truthfully. Takes no placeholders, so an override is plain prose, and must not name " +
                "any one Room: identity stays in the Turn's own label.",
            Default:
                """
                This one session spans every Room you are in. Messages from all of them arrive here, each opening
                with its Room's label, and you answer into the Room the label names. Treat each Room as a separate
                audience. Answer a Message from what was said in its own Room, and do not carry a decision, a
                language or a request from one Room into another unless the Human says it applies everywhere. A
                short reply such as "yes" or "option 2" belongs to the Room its label names, and refers to what
                was said there, however recently another Room spoke. Rooms can share a name; the id in the label
                tells them apart. Describe your own memory truthfully: you can see earlier Messages from all your
                Rooms in this session, and a restart clears them.
                """,
            Placeholders: [],
            RequiredPlaceholders: [],
            Timing: PromptTiming.NextSession),

        new PromptDefinition(
            Key: "task.wake.message",
            Label: "Task wake-up message",
            HelperText:
                "Posted in a Room to wake a Task's assignee after the Task changes. The Mention must " +
                "stay first. An override may drop the leading {{assignee}} Mention, but a Room's Reply " +
                "Gate then may not recognise the assignee as addressed.",
            Default:
                """
                @{{assignee}} Task {{taskId}} "{{title}}" ({{status}}, {{team}}) was changed by {{actor}}:
                {{changes}}
                Call get_task with taskId {{taskId}} for the full task.
                """,
            Placeholders: ["{{assignee}}", "{{taskId}}", "{{title}}", "{{actor}}", "{{changes}}", "{{status}}", "{{team}}"],
            RequiredPlaceholders: ["{{assignee}}", "{{taskId}}", "{{title}}", "{{actor}}", "{{changes}}", "{{status}}", "{{team}}"],
            Timing: PromptTiming.Live),

        new PromptDefinition(
            Key: "tool.createTask.description",
            Label: "create_task description",
            HelperText:
                "The one-line job description a model reads for the create_task tool. Takes no " +
                "placeholders.",
            Default:
                """
                Creates a Task: durable work tracked outside this conversation. Create one to hand work to a Teammate so it is not lost when this conversation ends, or to track your own multi-step work across Turns. Give it a title, a Team, and optionally an assignee; creating it wakes the assignee unless you assign it to yourself. Pass originRoomId with the id of the Room you are acting in, so the Task remembers where it came from.
                """,
            Placeholders: [],
            RequiredPlaceholders: [],
            Timing: PromptTiming.NextSession),

        new PromptDefinition(
            Key: "tool.getTask.description",
            Label: "get_task description",
            HelperText:
                "The one-line job description a model reads for the get_task tool. Must keep the " +
                "sentence about a Task id in a Message (Spec §13.13): it is what tells a model what " +
                "an id such as PLAT-0042 seen in a Message means. Takes no placeholders.",
            Default:
                """
                Reads one Task by its id, such as PLAT-0042, with an option to also see its full Change log. Task ids such as PLAT-0042 seen in a Message refer to Tasks; call get_task to read one.
                """,
            Placeholders: [],
            RequiredPlaceholders: [],
            Timing: PromptTiming.NextSession),

        new PromptDefinition(
            Key: "tool.listTasks.description",
            Label: "list_tasks description",
            HelperText:
                "The one-line job description a model reads for the list_tasks tool. Takes no " +
                "placeholders.",
            Default:
                """
                Lists Tasks matching the filters you give: scope ("active" or "closed"), team, project, assignee, status, or priority. Give assignee "me" to find your own work, or "unassigned" for Tasks no one owns yet. text searches both id and title, case-insensitively.
                """,
            Placeholders: [],
            RequiredPlaceholders: [],
            Timing: PromptTiming.NextSession),

        new PromptDefinition(
            Key: "tool.updateTask.description",
            Label: "update_task description",
            HelperText:
                "The one-line job description a model reads for the update_task tool. Takes no " +
                "placeholders.",
            Default:
                """
                Changes one or more fields on a Task. Changing a field wakes the assignee, unless you are the assignee. Set status as you work through it: To Do, then In Progress, then Review, then Done. Setting status to Duplicate also needs duplicate_of, the id of the Task it duplicates.
                """,
            Placeholders: [],
            RequiredPlaceholders: [],
            Timing: PromptTiming.NextSession),

        new PromptDefinition(
            Key: "tool.closeTask.description",
            Label: "close_task description",
            HelperText:
                "The one-line job description a model reads for the close_task tool. Takes no " +
                "placeholders.",
            Default:
                """
                Closes a Task, hiding it from active lists. Closing is separate from marking a Task Done: a Task can be Done and still open, or Closed at any status.
                """,
            Placeholders: [],
            RequiredPlaceholders: [],
            Timing: PromptTiming.NextSession),

        new PromptDefinition(
            Key: "tool.reopenTask.description",
            Label: "reopen_task description",
            HelperText:
                "The one-line job description a model reads for the reopen_task tool. Takes no " +
                "placeholders.",
            Default:
                """
                Reopens a Closed Task, returning it to the active lists it was hidden from.
                """,
            Placeholders: [],
            RequiredPlaceholders: [],
            Timing: PromptTiming.NextSession),

        new PromptDefinition(
            Key: "getHelp.tasks",
            Label: "Help: tasks",
            HelperText:
                "A short TASKS section in get_help's output, added right after BUDGET only when the " +
                "Tasks tools are offered (Spec §11.9). Takes no placeholders.",
            Default:
                """
                TASKS
                Tasks track work outside this conversation. Changing one wakes its assignee, unless
                you are the assignee. Use create_task, get_task, list_tasks, update_task, close_task
                and reopen_task to work with them. Write a Task's id, for example PLAT-0042, to refer
                to it in a Message; the Human sees it as a link.
                """,
            Placeholders: [],
            RequiredPlaceholders: [],
            Timing: PromptTiming.Live),
    ];
}
