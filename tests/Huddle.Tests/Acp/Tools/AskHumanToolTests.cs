namespace Agency.Huddle.Tests.Acp.Tools;

using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Questions;
using Agency.Huddle.App.Services;
using Agency.Huddle.Tests.Acp.Fakes;

/// <summary>
/// Pins <see cref="AskHumanTool"/> against Questions spec §6.2: the checks in their order, every
/// problem reported at once, the success and replacement texts, and the rule that nothing is stored
/// unless every check passes. Real collaborators throughout, the same style
/// <c>ProposeTeammatesToolTests</c> uses for this layer.
/// </summary>
public sealed class AskHumanToolTests
{
    /// <summary>Spec §6.2, first check: a missing <c>roomId</c> or <c>questions</c> is refused with the one required-arguments text.</summary>
    [Fact]
    public async Task InvokeAsync_MissingArguments_ReturnsRequiredArgumentsText()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        AskHumanTool tool = fixture.CreateTool(coach.Id);
        Room room = await fixture.Directory.CreateRoomAsync("Workout", [KnownIds.Human, coach.Id], ct);

        string noQuestions = await tool.InvokeAsync(new JsonObject { ["roomId"] = room.Id }, ct);
        string noRoom = await tool.InvokeAsync(new JsonObject { ["questions"] = Questions(GoalQuestion()) }, ct);

        Assert.Equal("Both 'roomId' and 'questions' are required arguments.", noQuestions);
        Assert.Equal("Both 'roomId' and 'questions' are required arguments.", noRoom);
    }

    /// <summary>Spec §6.2: the Room named in the call does not exist.</summary>
    [Fact]
    public async Task InvokeAsync_UnknownRoom_ReturnsUnknownRoomText()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);

        string result = await fixture.CreateTool(coach.Id).InvokeAsync(Arguments("no-such-room", GoalQuestion()), ct);

        Assert.Equal("Unknown room 'no-such-room'.", result);
    }

    /// <summary>Spec §6.2: the Room exists, but the caller is not one of its Members.</summary>
    [Fact]
    public async Task InvokeAsync_CallerNotAMember_ReturnsNotAMemberText()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        User member = await fixture.AddAgentAsync("Member", ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.Directory.CreateRoomAsync("Workout", [KnownIds.Human, member.Id], ct);

        string result = await fixture.CreateTool(coach.Id).InvokeAsync(Arguments(room.Id, GoalQuestion()), ct);

        Assert.Equal("You are not a Member of that Room.", result);
        Assert.Null(fixture.Questions.Get(room.Id));
    }

    /// <summary>Spec §6.2: the caller is a Member, but the Room is Archived.</summary>
    [Fact]
    public async Task InvokeAsync_RoomArchived_ReturnsArchivedText()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.Directory.CreateRoomAsync("Workout", [KnownIds.Human, coach.Id], ct);
        await fixture.Directory.SetRoomArchivedAsync(room.Id, true, ct);

        string result = await fixture.CreateTool(coach.Id).InvokeAsync(Arguments(room.Id, GoalQuestion()), ct);

        Assert.Equal("That Room is Archived.", result);
        Assert.Null(fixture.Questions.Get(room.Id));
    }

    /// <summary>
    /// Spec §6.2 and D-5: a Room whose Budget is spent refuses the tool, worded as terminal like
    /// <c>post_message</c>'s Budget refusal, so a model does not retry it or ask somewhere else.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_BudgetSpent_ReturnsTerminalPausedText()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct, agentMessageBudget: 1);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.Directory.CreateRoomAsync("Workout", [KnownIds.Human, coach.Id], ct);
        _ = await fixture.Chat.PostAsync(room.Id, coach.Id, "One message spends the whole Budget.", ct: ct);
        Assert.True(fixture.Chat.GetBudget(room.Id).Exhausted);

        string result = await fixture.CreateTool(coach.Id).InvokeAsync(Arguments(room.Id, GoalQuestion()), ct);

        Assert.Equal(
            "That Room is paused: its Budget is spent. Do not retry; nothing can be asked there until the Human speaks.",
            result);
        Assert.Null(fixture.Questions.Get(room.Id));
    }

    /// <summary>Spec §6.2: the checks run in order, so an Archived Room is reported before a question list that is also wrong.</summary>
    [Fact]
    public async Task InvokeAsync_ArchivedAndMalformed_ReportsTheEarlierCheck()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.Directory.CreateRoomAsync("Workout", [KnownIds.Human, coach.Id], ct);
        await fixture.Directory.SetRoomArchivedAsync(room.Id, true, ct);

        string result = await fixture.CreateTool(coach.Id).InvokeAsync(
            new JsonObject { ["roomId"] = room.Id, ["questions"] = new JsonArray() }, ct);

        Assert.Equal("That Room is Archived.", result);
    }

    /// <summary>Spec §6.2: one to three questions. Zero and four are both out of range, and the text says how many were asked.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public async Task InvokeAsync_QuestionCountOutOfRange_ReturnsCountText(int count)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.Directory.CreateRoomAsync("Workout", [KnownIds.Human, coach.Id], ct);
        JsonArray questions = [];
        for (int index = 0; index < count; index++)
        {
            questions.Add(GoalQuestion());
        }

        string result = await fixture.CreateTool(coach.Id).InvokeAsync(
            new JsonObject { ["roomId"] = room.Id, ["questions"] = questions }, ct);

        Assert.Equal($"Ask 1 to 3 questions; you asked {count}.", result);
        Assert.Null(fixture.Questions.Get(room.Id));
    }

    /// <summary>
    /// Spec §6.2: every problem in every question is reported in one result, one line each and
    /// prefixed with its position, and nothing is stored - a card is never partially accepted.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_SeveralProblems_ReportsEveryOneAndStoresNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.Directory.CreateRoomAsync("Workout", [KnownIds.Human, coach.Id], ct);
        JsonObject good = GoalQuestion();
        JsonObject bad = Question("   ", ["Only one"], type: "ranked");
        JsonObject worse = Question("Pick one", ["Same", "same", new string('x', 61), "two\nlines"]);

        string result = await fixture.CreateTool(coach.Id).InvokeAsync(
            new JsonObject { ["roomId"] = room.Id, ["questions"] = Questions(good, bad, worse) }, ct);

        string[] lines = result.Split('\n');
        Assert.Contains("Question 2: is empty.", lines);
        Assert.Contains("Question 2: give 2 to 4 options; it has 1.", lines);
        Assert.Contains("Question 2: type must be single_select, multi_select or rank_priorities.", lines);
        Assert.Contains("Question 3, option 2: repeats option 1.", lines);
        Assert.Contains("Question 3, option 3: is 61 characters; keep options short, the limit is 60.", lines);
        Assert.Contains("Question 3, option 4: must be one line.", lines);
        Assert.DoesNotContain(lines, line => line.StartsWith("Question 1", StringComparison.Ordinal));
        Assert.Null(fixture.Questions.Get(room.Id));
    }

    /// <summary>Spec §6.2: a question over 200 characters is refused and its length is reported.</summary>
    [Fact]
    public async Task InvokeAsync_QuestionTooLong_ReturnsLengthProblem()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.Directory.CreateRoomAsync("Workout", [KnownIds.Human, coach.Id], ct);

        string result = await fixture.CreateTool(coach.Id).InvokeAsync(
            Arguments(room.Id, Question(new string('q', 240), ["A", "B"])), ct);

        Assert.Equal("Question 1: is 240 characters; the limit is 200.", result);
    }

    /// <summary>Spec §6.2 and D-3: an <c>@</c> in a question or an option is refused, because the answer is posted as the Human.</summary>
    [Fact]
    public async Task InvokeAsync_AtSignInQuestionOrOption_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.Directory.CreateRoomAsync("Workout", [KnownIds.Human, coach.Id], ct);
        const string Reason = "must not contain '@'. The answer is posted as the Human, so a Mention in it would speak for them.";

        string inOption = await fixture.CreateTool(coach.Id).InvokeAsync(
            Arguments(room.Id, Question("Who goes first?", ["@Coder go ahead", "Wait"])), ct);
        string inQuestion = await fixture.CreateTool(coach.Id).InvokeAsync(
            Arguments(room.Id, Question("Ask @Nova?", ["Yes", "No"])), ct);

        Assert.Equal($"Question 1, option 1: {Reason}", inOption);
        Assert.Equal($"Question 1: {Reason}", inQuestion);
        Assert.Null(fixture.Questions.Get(room.Id));
    }

    /// <summary>Spec §6.2: <c>type</c> defaults to <c>single_select</c>, and the other two wire values parse into their kinds.</summary>
    [Fact]
    public async Task InvokeAsync_TypeOmittedOrGiven_ParsesIntoKinds()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.Directory.CreateRoomAsync("Workout", [KnownIds.Human, coach.Id], ct);
        JsonArray questions = Questions(
            Question("Goal?", ["Strength", "Cardio"]),
            Question("Days?", ["Mon", "Wed", "Fri"], type: "multi_select"),
            Question("Matters most?", ["Cost", "Speed", "Quality"], type: "rank_priorities"));

        string result = await fixture.CreateTool(coach.Id).InvokeAsync(
            new JsonObject { ["roomId"] = room.Id, ["questions"] = questions }, ct);

        Assert.Equal(Asked(3, room), result);
        PendingQuestions? stored = fixture.Questions.Get(room.Id);
        Assert.NotNull(stored);
        Assert.Equal(
            [QuestionKind.SingleSelect, QuestionKind.MultiSelect, QuestionKind.RankPriorities],
            stored.Questions.Select(question => question.Kind).ToArray());
    }

    /// <summary>
    /// Spec §6.2's success text: names the Room and its id, tells the asker to end its Turn and not
    /// to guess, and the stored card carries the trimmed text, the asker's id and its current Name.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_Valid_StoresTrimmedCardAndReturnsSuccessText()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.Directory.CreateRoomAsync("Workout", [KnownIds.Human, coach.Id], ct);

        string result = await fixture.CreateTool(coach.Id).InvokeAsync(
            Arguments(room.Id, Question("  What is your main goal?  ", ["  Strength ", "Cardio"])), ct);

        Assert.Equal(Asked(1, room), result);
        PendingQuestions? stored = fixture.Questions.Get(room.Id);
        Assert.NotNull(stored);
        Assert.Equal(coach.Id, stored.AskerAgentId);
        Assert.Equal("Coach", stored.AskerName);
        Assert.Equal(room.Id, stored.RoomId);
        Question question = Assert.Single(stored.Questions);
        Assert.Equal("What is your main goal?", question.Text);
        Assert.Equal(["Strength", "Cardio"], question.Options.ToArray());
    }

    /// <summary>Q6: the same Agent asking again replaces its own waiting card, and the text says so before the usual instruction.</summary>
    [Fact]
    public async Task InvokeAsync_SameAskerAgain_ReplacesAndSaysSo()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.Directory.CreateRoomAsync("Workout", [KnownIds.Human, coach.Id], ct);
        AskHumanTool tool = fixture.CreateTool(coach.Id);
        _ = await tool.InvokeAsync(Arguments(room.Id, GoalQuestion()), ct);
        PendingQuestions? first = fixture.Questions.Get(room.Id);

        string result = await tool.InvokeAsync(Arguments(room.Id, Question("How many days?", ["2", "3"])), ct);

        Assert.Equal($"Replaced your waiting questions. {Asked(1, room)}", result);
        PendingQuestions? second = fixture.Questions.Get(room.Id);
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal("How many days?", Assert.Single(second.Questions).Text);
    }

    /// <summary>Q7: another Agent asking while a card waits is refused, naming the asker, and the waiting card is untouched.</summary>
    [Fact]
    public async Task InvokeAsync_OtherAskerWhileWaiting_IsRefusedNamingTheAsker()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        User nova = await fixture.AddAgentAsync("Nova", ct);
        Room room = await fixture.Directory.CreateRoomAsync("Workout", [KnownIds.Human, coach.Id, nova.Id], ct);
        _ = await fixture.CreateTool(coach.Id).InvokeAsync(Arguments(room.Id, GoalQuestion()), ct);
        PendingQuestions? waiting = fixture.Questions.Get(room.Id);

        string result = await fixture.CreateTool(nova.Id).InvokeAsync(Arguments(room.Id, Question("Budget?", ["Low", "High"])), ct);

        Assert.Equal("Questions from Coach are already waiting in this Room. Wait for the Human to answer them.", result);
        Assert.Equal(waiting, fixture.Questions.Get(room.Id));
    }

    /// <summary>The tool's identity and schema: its name, and the three required-shape facts a model reads.</summary>
    [Fact]
    public async Task NameAndSchema_DescribeTheTool()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        AskHumanTool tool = fixture.CreateTool("caller");

        JsonNode? required = tool.InputSchema["required"];

        Assert.Equal("ask_human", tool.Name);
        Assert.Equal(["roomId", "questions"], Assert.IsType<JsonArray>(required).Select(node => (string?)node).ToArray());
        Assert.NotNull(tool.InputSchema["properties"]?["questions"]?["items"]?["properties"]?["options"]);
    }

    /// <summary>A question that is not an object is reported by its position, with what it should have been.</summary>
    [Fact]
    public async Task InvokeAsync_QuestionIsNotAnObject_ReportsItsPosition()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.Directory.CreateRoomAsync("Workout", [KnownIds.Human, coach.Id], ct);
        JsonArray questions = [JsonValue.Create("What is your main goal?")];

        string result = await fixture.CreateTool(coach.Id).InvokeAsync(
            new JsonObject { ["roomId"] = room.Id, ["questions"] = questions }, ct);

        Assert.Equal("Question 1: must be an object with 'question' and 'options'.", result);
        Assert.Null(fixture.Questions.Get(room.Id));
    }

    /// <summary>The success text for <paramref name="count"/> questions asked in <paramref name="room"/>, as §6.2 words it.</summary>
    /// <param name="count">How many questions were asked.</param>
    /// <param name="room">The Room they were asked in.</param>
    private static string Asked(int count, Room room)
    {
        string noun = count == 1 ? "question" : "questions";
        return $"Asked the Human {count} {noun} in Room '{room.Name}' (id {room.Id}). End your Turn now, and do not guess " +
            "the answers. The answer will arrive later as a Message from the Human in that Room, quoting each " +
            "question. If they write their own reply instead, that reply is the answer.";
    }

    /// <summary>Builds the tool's arguments for a Room and the given questions.</summary>
    /// <param name="roomId">The Room id.</param>
    /// <param name="questions">The question objects.</param>
    private static JsonObject Arguments(string roomId, params JsonObject[] questions)
    {
        return new JsonObject { ["roomId"] = roomId, ["questions"] = Questions(questions) };
    }

    /// <summary>Builds a <c>questions</c> array from question objects.</summary>
    /// <param name="questions">The question objects.</param>
    private static JsonArray Questions(params JsonObject[] questions)
    {
        JsonArray array = [];
        foreach (JsonObject question in questions)
        {
            array.Add(question);
        }

        return array;
    }

    /// <summary>One valid <c>single_select</c> question, for tests about something else.</summary>
    private static JsonObject GoalQuestion()
    {
        return Question("What is your main goal?", ["Strength", "Cardio", "Weight loss"]);
    }

    /// <summary>Builds one question object as a model would send it.</summary>
    /// <param name="text">The question text.</param>
    /// <param name="options">The option texts.</param>
    /// <param name="type">The wire <c>type</c>, or <see langword="null"/> to omit it.</param>
    private static JsonObject Question(string text, string[] options, string? type = null)
    {
        JsonArray optionArray = [];
        foreach (string option in options)
        {
            optionArray.Add(option);
        }

        JsonObject question = new() { ["question"] = text, ["options"] = optionArray };
        if (type is not null)
        {
            question["type"] = type;
        }

        return question;
    }

    /// <summary>The real collaborators the tool runs against, in one <see cref="TempDataDir"/>.</summary>
    private sealed class Fixture : IDisposable
    {
        private readonly TempDataDir dataDir;

        private Fixture(TempDataDir dataDir, SqliteTeamDirectory directory, QuestionStore questions, ChatService chat)
        {
            this.dataDir = dataDir;
            this.Directory = directory;
            this.Questions = questions;
            this.Chat = chat;
        }

        /// <summary>The Team Directory a test arranges Rooms and Members through.</summary>
        public SqliteTeamDirectory Directory { get; }

        /// <summary>The <see cref="QuestionStore"/> a test reads back after calling the tool.</summary>
        public QuestionStore Questions { get; }

        /// <summary>The real <see cref="ChatService"/> the tool reads the Budget from.</summary>
        public ChatService Chat { get; }

        /// <summary>Builds a fixture with an initialised Team Directory (Human Name <c>"You"</c>).</summary>
        /// <param name="ct">Cancels initialisation.</param>
        /// <param name="agentMessageBudget">The per-Room Agent Message Budget; small, so a test can spend it.</param>
        public static async Task<Fixture> CreateAsync(CancellationToken ct, int agentMessageBudget = 40)
        {
            TempDataDir dataDir = new();
            IOptions<TeamOptions> options = Microsoft.Extensions.Options.Options.Create(
                new TeamOptions { DataDir = dataDir.Path, AgentMessageBudget = agentMessageBudget });
            SqliteTeamDirectory directory = new(options);
            await directory.InitializeAsync("You", ct);
            FileChatStore store = new(options, NullLogger<FileChatStore>.Instance);
            RoomEvents events = new(NullLogger<RoomEvents>.Instance);
            QuestionStore questions = new(events);
            ChatService chat = new(
                directory, store, events, new FakeMentionAliasSource(), options, new Agency.Huddle.App.Teammates.ProposalStore(events), questions, NullLogger<ChatService>.Instance);

            return new Fixture(dataDir, directory, questions, chat);
        }

        /// <summary>Registers an Agent User under <paramref name="name"/>.</summary>
        /// <param name="name">The Agent's Name.</param>
        /// <param name="ct">Cancels the write.</param>
        public async Task<User> AddAgentAsync(string name, CancellationToken ct)
        {
            User? agent = await this.Directory.UpsertAgentUserAsync(name, null, ct);
            Assert.NotNull(agent);
            return agent;
        }

        /// <summary>Builds the tool calling as <paramref name="callerAgentId"/>.</summary>
        /// <param name="callerAgentId">The Agent id the tool invokes as.</param>
        public AskHumanTool CreateTool(string callerAgentId)
        {
            return new AskHumanTool(this.Questions, this.Chat, this.Directory, TimeProvider.System, callerAgentId, new FakePromptSource());
        }

        /// <summary>Releases the temp data directory.</summary>
        public void Dispose()
        {
            this.dataDir.Dispose();
        }
    }
}
