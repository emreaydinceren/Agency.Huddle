namespace Agency.Huddle.App.Acp.Tools;

using System.Text.Json.Nodes;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Questions;
using Agency.Huddle.App.Services;

/// <summary>
/// Puts one to three multiple-choice Questions to the Human (Questions spec §6.2). Returns at
/// once: a successful call only stores a <see cref="PendingQuestions"/> card for the Human to
/// answer, and the answer arrives later as a Message from the Human that Mentions the asker. See
/// <c>QuestionService</c> for what happens next. Offered to every Persona (D-10): asking costs
/// nothing and creates nothing.
/// </summary>
internal sealed class AskHumanTool(
    QuestionStore questions,
    ChatService chat,
    ITeamDirectory directory,
    TimeProvider clock,
    string callerAgentId,
    IPromptSource prompts) : IAppTool
{
    private const int MaxQuestions = 3;
    private const int MinOptions = 2;
    private const int MaxOptions = 4;
    private const int MaxQuestionLength = 200;
    private const int MaxOptionLength = 60;

    private const string MentionReason = "must not contain '@'. The answer is posted as the Human, so a Mention in it would speak for them.";

    private static readonly IReadOnlyDictionary<string, string> NoValues = new Dictionary<string, string>();

    public string Name => "ask_human";

    public string Description => prompts.Render("tool.askHuman.description", NoValues);

    public JsonObject InputSchema => new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["roomId"] = new JsonObject { ["type"] = "string" },
            ["questions"] = new JsonObject
            {
                ["type"] = "array",
                ["minItems"] = 1,
                ["maxItems"] = MaxQuestions,
                ["items"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["question"] = new JsonObject { ["type"] = "string" },
                        ["options"] = new JsonObject
                        {
                            ["type"] = "array",
                            ["minItems"] = MinOptions,
                            ["maxItems"] = MaxOptions,
                            ["items"] = new JsonObject { ["type"] = "string" },
                        },
                        ["type"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["enum"] = new JsonArray { "single_select", "multi_select", "rank_priorities" },
                        },
                    },
                    ["required"] = new JsonArray { "question", "options" },
                },
            },
        },
        ["required"] = new JsonArray { "roomId", "questions" },
    };

    /// <summary>
    /// Runs Questions spec §6.2's checks, in order: both arguments present, the Room exists, the
    /// caller is a Member, the Room is not Archived, its Budget is not spent, one to three questions,
    /// and every question and option well-formed - all of those problems reported at once. Only then
    /// is the card handed to <see cref="QuestionStore.TryPut"/>. Every early exit is a string; this
    /// never throws for an expected failure. The schema's limits are advisory for the model, and
    /// these checks are the rule.
    /// </summary>
    public async Task<string> InvokeAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        string? roomId = ReadString(arguments["roomId"]);
        if (string.IsNullOrWhiteSpace(roomId) || arguments["questions"] is not JsonArray questionsArray)
        {
            return "Both 'roomId' and 'questions' are required arguments.";
        }

        Room? room = await directory.GetRoomAsync(roomId, cancellationToken);
        if (room is null)
        {
            return $"Unknown room '{roomId}'.";
        }

        IReadOnlyList<User> members = await directory.GetRoomMembersAsync(roomId, cancellationToken);
        if (!members.Any(member => string.Equals(member.Id, callerAgentId, StringComparison.Ordinal)))
        {
            return "You are not a Member of that Room.";
        }

        if (room.Archived)
        {
            return "That Room is Archived.";
        }

        // Advisory, not check-then-act critical: a Room that pauses after this check still cannot be
        // answered without a Human tap, and a tap is the Human speaking.
        if (chat.GetBudget(roomId).Exhausted)
        {
            return "That Room is paused: its Budget is spent. Do not retry; nothing can be asked there until the Human speaks.";
        }

        if (questionsArray.Count is 0 or > MaxQuestions)
        {
            return $"Ask 1 to {MaxQuestions} questions; you asked {questionsArray.Count}.";
        }

        List<string> problems = [];
        List<Question> parsed = [];
        for (int index = 0; index < questionsArray.Count; index++)
        {
            Question? question = ParseQuestion(questionsArray[index], index + 1, problems);
            if (question is not null)
            {
                parsed.Add(question);
            }
        }

        if (problems.Count > 0)
        {
            return string.Join('\n', problems);
        }

        User? caller = await directory.GetUserAsync(callerAgentId, cancellationToken);
        PendingQuestions pending = new(
            Guid.NewGuid().ToString("N"),
            roomId,
            callerAgentId,
            caller?.Name ?? callerAgentId,
            parsed,
            clock.GetUtcNow());

        QuestionPut put = questions.TryPut(pending);
        if (put.Result == QuestionPutResult.Refused)
        {
            string askerName = put.Existing?.AskerName ?? "another Agent";
            return $"Questions from {askerName} are already waiting in this Room. Wait for the Human to answer them.";
        }

        string noun = parsed.Count == 1 ? "question" : "questions";
        string asked =
            $"Asked the Human {parsed.Count} {noun} in Room '{room.Name}' (id {room.Id}). End your Turn now, and do not guess " +
            "the answers. The answer will arrive later as a Message from the Human in that Room, quoting each " +
            "question. If they write their own reply instead, that reply is the answer.";

        return put.Result == QuestionPutResult.Replaced ? $"Replaced your waiting questions. {asked}" : asked;
    }

    /// <summary>
    /// Parses one question object, appending every problem it has to <paramref name="problems"/>,
    /// each prefixed with its position. Returns <see langword="null"/> when the question has any.
    /// </summary>
    /// <param name="node">The array element the model sent.</param>
    /// <param name="number">The question's one-based position, for the problem prefix.</param>
    /// <param name="problems">Collects every problem found, across all questions.</param>
    private static Question? ParseQuestion(JsonNode? node, int number, List<string> problems)
    {
        string prefix = $"Question {number}";
        if (node is not JsonObject obj)
        {
            problems.Add($"{prefix}: must be an object with 'question' and 'options'.");
            return null;
        }

        int before = problems.Count;
        string text = (ReadString(obj["question"]) ?? string.Empty).Trim();
        CheckText(text, MaxQuestionLength, $"{prefix}:", problems);

        List<string> options = [];
        if (obj["options"] is JsonArray optionsArray)
        {
            for (int index = 0; index < optionsArray.Count; index++)
            {
                string option = (ReadString(optionsArray[index]) ?? string.Empty).Trim();
                string optionPrefix = $"{prefix}, option {index + 1}:";
                int optionProblems = problems.Count;
                CheckText(option, MaxOptionLength, optionPrefix, problems, keepShort: true);

                // Distinct ignoring case, reported against the later of the two.
                int earlier = options.FindIndex(other => string.Equals(other, option, StringComparison.OrdinalIgnoreCase));
                if (problems.Count == optionProblems && earlier >= 0)
                {
                    problems.Add($"{optionPrefix} repeats option {earlier + 1}.");
                }

                options.Add(option);
            }
        }

        if (options.Count is < MinOptions or > MaxOptions)
        {
            problems.Add($"{prefix}: give {MinOptions} to {MaxOptions} options; it has {options.Count}.");
        }

        QuestionKind? kind = ParseKind(obj["type"]);
        if (kind is null)
        {
            problems.Add($"{prefix}: type must be single_select, multi_select or rank_priorities.");
        }

        return problems.Count == before && kind is not null
            ? new Question(text, options, kind.Value)
            : null;
    }

    /// <summary>Checks one question or option text: not empty, within its limit, one line, and free of <c>@</c>.</summary>
    /// <param name="text">The trimmed text.</param>
    /// <param name="limit">The longest it may be.</param>
    /// <param name="prefix">The position prefix, ending in a colon.</param>
    /// <param name="problems">Collects each problem.</param>
    /// <param name="keepShort">Whether to word a length problem as advice to keep options short.</param>
    private static void CheckText(string text, int limit, string prefix, List<string> problems, bool keepShort = false)
    {
        if (text.Length == 0)
        {
            problems.Add($"{prefix} is empty.");
            return;
        }

        if (text.Length > limit)
        {
            string clause = keepShort ? "keep options short, the limit is " : "the limit is ";
            problems.Add($"{prefix} is {text.Length} characters; {clause}{limit}.");
        }

        if (text.Contains('\n') || text.Contains('\r'))
        {
            problems.Add($"{prefix} must be one line.");
        }

        if (text.Contains('@'))
        {
            problems.Add($"{prefix} {MentionReason}");
        }
    }

    /// <summary>Parses the wire <c>type</c>: absent means <see cref="QuestionKind.SingleSelect"/>, an unknown value is <see langword="null"/>.</summary>
    /// <param name="node">The <c>type</c> value the model sent, if any.</param>
    private static QuestionKind? ParseKind(JsonNode? node)
    {
        if (node is null)
        {
            return QuestionKind.SingleSelect;
        }

        return ReadString(node) switch
        {
            "single_select" => QuestionKind.SingleSelect,
            "multi_select" => QuestionKind.MultiSelect,
            "rank_priorities" => QuestionKind.RankPriorities,
            _ => null,
        };
    }

    /// <summary>Reads a JSON string, or <see langword="null"/> for anything else (a number, an object, a missing value).</summary>
    /// <param name="node">The node to read.</param>
    private static string? ReadString(JsonNode? node)
    {
        return node is JsonValue value && value.TryGetValue(out string? text) ? text : null;
    }
}
