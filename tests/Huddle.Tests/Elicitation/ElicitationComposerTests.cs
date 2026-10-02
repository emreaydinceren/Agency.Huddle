using Agency.Huddle.App.Elicitation;
using static Agency.Huddle.Tests.Elicitation.ElicitationTestSupport;

namespace Agency.Huddle.Tests.Elicitation;

/// <summary>
/// Pins <see cref="ElicitationComposer"/> (elicitation bridge, decisions E-5 and E-6): what the Human
/// typed becomes both the Transcript Message (a quote of each label, a blank line, the answer, as
/// <c>QuestionService.Compose</c> writes it) and the content the wire carries (plain CLR values of the
/// matching JSON type, only the filled keys, a typed custom answer beating its selection as the
/// Adapter does), and <c>CanSend</c> says whether the Human may send at all.
/// </summary>
public sealed class ElicitationComposerTests
{
    private const string NameSchema = """{"type":"object","properties":{"name":{"type":"string","minLength":2,"maxLength":4}}}""";

    private const string OptionalSchema = """{"type":"object","properties":{"a":{"type":"string"},"b":{"type":"string"}}}""";

    /// <summary>One question: the Transcript quotes the question, then a blank line, then the answer, and the wire carries the option value under the question key.</summary>
    [Fact]
    public void Compose_SingleQuestion_QuotesTheQuestionThenABlankLineThenTheAnswer()
    {
        ElicitationForm form = FormOf(SingleQuestionSchema, SingleQuestionMessage);

        ElicitationAnswer answer = ElicitationComposer.Compose(form, Values(Pair("question_0", "Postgres")));

        Assert.Equal("> Which database?\n\nPostgres", answer.Text);
        Assert.Equal(["question_0"], answer.Content.Keys.ToArray());
        Assert.Equal("Postgres", Assert.IsType<string>(answer.Content["question_0"]));
    }

    /// <summary>Two questions: the blocks are separated by one blank line, and a multi select is joined with a comma in the Transcript but is an array on the wire.</summary>
    [Fact]
    public void Compose_TwoQuestions_SeparatesBlocksWithABlankLine()
    {
        ElicitationForm form = FormOf(TwoQuestionSchema, TwoQuestionMessage);

        ElicitationAnswer answer = ElicitationComposer.Compose(form, Values(Pair("question_0", "Postgres"), Pair("question_1", "Auth", "Billing")));

        Assert.Equal("> Which database?\n\nPostgres\n\n> Which features?\n\nAuth, Billing", answer.Text);
        Assert.Equal(["question_0", "question_1"], answer.Content.Keys.ToArray());
        Assert.Equal(["Auth", "Billing"], Assert.IsType<string[]>(answer.Content["question_1"]));
    }

    /// <summary>A typed custom answer beats the selection exactly as the Adapter does: only the custom key goes on the wire, trimmed, and the Transcript quotes the custom text.</summary>
    [Fact]
    public void Compose_CustomText_BeatsTheSelection()
    {
        ElicitationForm form = FormOf(SingleQuestionSchema, SingleQuestionMessage);

        ElicitationAnswer answer = ElicitationComposer.Compose(form, Values(Pair("question_0", "Postgres"), Pair("question_0_custom", "  Oracle  ")));

        Assert.Equal("> Which database?\n\nOracle", answer.Text);
        Assert.Equal(["question_0_custom"], answer.Content.Keys.ToArray());
        Assert.Equal("Oracle", Assert.IsType<string>(answer.Content["question_0_custom"]));
    }

    /// <summary>A custom answer on its own is sent the same way: the question key is absent.</summary>
    [Fact]
    public void Compose_CustomTextWithoutASelection_SendsOnlyTheCustomKey()
    {
        ElicitationForm form = FormOf(SingleQuestionSchema, SingleQuestionMessage);

        ElicitationAnswer answer = ElicitationComposer.Compose(form, Values(Pair("question_0_custom", "Oracle")));

        Assert.Equal("> Which database?\n\nOracle", answer.Text);
        Assert.Equal(["question_0_custom"], answer.Content.Keys.ToArray());
    }

    /// <summary>The custom answer beats a multi select's selection too, and only for its own question.</summary>
    [Fact]
    public void Compose_CustomText_BeatsAMultiSelectSelection_ForItsOwnQuestionOnly()
    {
        ElicitationForm form = FormOf(TwoQuestionSchema, TwoQuestionMessage);

        ElicitationAnswer answer = ElicitationComposer.Compose(
            form,
            Values(Pair("question_0", "SQLite"), Pair("question_1", "Auth", "Billing"), Pair("question_1_custom", "Everything")));

        Assert.Equal("> Which database?\n\nSQLite\n\n> Which features?\n\nEverything", answer.Text);
        Assert.Equal(["question_0", "question_1_custom"], answer.Content.Keys.ToArray());
    }

    /// <summary>A custom field holding only blanks says nothing, so the selection stands and the custom key is not sent.</summary>
    [Fact]
    public void Compose_BlankCustomText_LeavesTheSelectionStanding()
    {
        ElicitationForm form = FormOf(SingleQuestionSchema, SingleQuestionMessage);

        ElicitationAnswer answer = ElicitationComposer.Compose(form, Values(Pair("question_0", "Postgres"), Pair("question_0_custom", "   ")));

        Assert.Equal("> Which database?\n\nPostgres", answer.Text);
        Assert.Equal(["question_0"], answer.Content.Keys.ToArray());
    }

    /// <summary>A field the Human left alone is left out of both the Transcript and the wire content.</summary>
    [Fact]
    public void Compose_UnansweredFields_AreOmitted()
    {
        ElicitationForm form = FormOf(TwoQuestionSchema, TwoQuestionMessage);

        ElicitationAnswer answer = ElicitationComposer.Compose(form, Values(Pair("question_1", "Search")));

        Assert.Equal("> Which features?\n\nSearch", answer.Text);
        Assert.Equal(["question_1"], answer.Content.Keys.ToArray());
    }

    /// <summary>Every kind goes on the wire as the plain CLR type the JSON type needs, never a string for a number or a boolean, and a multi select in option order whatever order it was ticked in.</summary>
    [Fact]
    public void Compose_Content_HasTheExactKeysAndClrTypes()
    {
        ElicitationForm form = FormOf(McpSchema);

        ElicitationAnswer answer = ElicitationComposer.Compose(
            form,
            Values(
                Pair("name", "Ada"),
                Pair("age", "36"),
                Pair("ratio", "2.5"),
                Pair("agree", "true"),
                Pair("due", "2026-10-01"),
                Pair("color", "green"),
                Pair("tags", "c", "a")));

        Assert.Equal(["name", "age", "ratio", "agree", "due", "color", "tags"], answer.Content.Keys.ToArray());
        Assert.Equal("Ada", Assert.IsType<string>(answer.Content["name"]));
        Assert.Equal(36L, Assert.IsType<long>(answer.Content["age"]));
        Assert.Equal(2.5, Assert.IsType<double>(answer.Content["ratio"]));
        Assert.True(Assert.IsType<bool>(answer.Content["agree"]));
        Assert.Equal("2026-10-01", Assert.IsType<string>(answer.Content["due"]));
        Assert.Equal("green", Assert.IsType<string>(answer.Content["color"]));
        Assert.Equal(["a", "c"], Assert.IsType<string[]>(answer.Content["tags"]));
    }

    /// <summary>The Transcript shows each label with what the Human saw: Yes or No for a boolean, the option's label for a select, the choices joined with a comma.</summary>
    [Fact]
    public void Compose_Transcript_ShowsLabelsYesNoAndJoinedChoices()
    {
        ElicitationForm form = FormOf(McpSchema);

        ElicitationAnswer answer = ElicitationComposer.Compose(
            form,
            Values(
                Pair("name", "Ada"),
                Pair("age", "36"),
                Pair("ratio", "2.5"),
                Pair("agree", "true"),
                Pair("due", "2026-10-01"),
                Pair("color", "green"),
                Pair("tags", "c", "a")));

        Assert.Equal(
            "> Name\n\nAda\n\n> Age\n\n36\n\n> Ratio\n\n2.5\n\n> I agree\n\nYes\n\n> Due\n\n2026-10-01\n\n> Color\n\ngreen\n\n> Tags\n\na, c",
            answer.Text);
    }

    /// <summary>A boolean answered No is a filled field: <c>false</c> goes on the wire and the Transcript says No.</summary>
    [Fact]
    public void Compose_BooleanFalse_IsFilledAndSentAsFalse()
    {
        ElicitationForm form = FormOf(McpSchema);

        ElicitationAnswer answer = ElicitationComposer.Compose(form, Values(Pair("name", "Ada"), Pair("agree", "false")));

        Assert.False(Assert.IsType<bool>(answer.Content["agree"]));
        Assert.Equal("> Name\n\nAda\n\n> I agree\n\nNo", answer.Text);
    }

    /// <summary>A select whose value differs from its title sends the value on the wire and shows the title in the Transcript.</summary>
    [Fact]
    public void Compose_SelectWithDistinctValueAndTitle_SendsTheValueShowsTheTitle()
    {
        ElicitationForm form = FormOf(Schema("""
            "size":{"type":"string","oneOf":[{"const":"s","title":"Small"},{"const":"l","title":"Large"}]}
            """));

        ElicitationAnswer answer = ElicitationComposer.Compose(form, Values(Pair("size", "s")));

        Assert.Equal("s", Assert.IsType<string>(answer.Content["size"]));
        Assert.Equal("> size\n\nSmall", answer.Text);
    }

    /// <summary>A label with line breaks stays on one quote line, so model text cannot break out of the quote.</summary>
    [Fact]
    public void Compose_LabelWithLineBreaks_StaysOnOneQuoteLine()
    {
        ElicitationForm form = FormOf(Schema("""
            "a":{"type":"string","title":"line1\r\nline2\nline3"}
            """));

        ElicitationAnswer answer = ElicitationComposer.Compose(form, Values(Pair("a", "x")));

        Assert.Equal("> line1 line2 line3\n\nx", answer.Text);
    }

    /// <summary>A number is read with the invariant culture whatever the machine's own: under a German culture <c>2.5</c> is 2.5, not 25.</summary>
    [Fact]
    public void Compose_Number_ParsesInvariantWhateverTheCurrentCulture()
    {
        ElicitationForm form = FormOf(McpSchema);
        ElicitationAnswer? answer = null;

        UnderCulture("de-DE", () => answer = ElicitationComposer.Compose(form, Values(Pair("name", "Ada"), Pair("agree", "true"), Pair("ratio", "2.5"))));

        Assert.NotNull(answer);
        Assert.Equal(2.5, Assert.IsType<double>(answer.Content["ratio"]));
        Assert.Equal("> Name\n\nAda\n\n> Ratio\n\n2.5\n\n> I agree\n\nYes", answer.Text);
    }

    /// <summary>An integer written with a decimal point but no fraction is whole, and goes out as a <see cref="long"/>.</summary>
    [Fact]
    public void Compose_Integer_AcceptsAWholeNumberWrittenWithADecimalPoint()
    {
        ElicitationForm form = FormOf(McpSchema);

        ElicitationAnswer answer = ElicitationComposer.Compose(form, Values(Pair("name", "Ada"), Pair("agree", "true"), Pair("age", "3.0")));

        Assert.Equal(3L, Assert.IsType<long>(answer.Content["age"]));
        Assert.Equal("> Name\n\nAda\n\n> Age\n\n3\n\n> I agree\n\nYes", answer.Text);
    }

    /// <summary>A text value goes out exactly as typed, spaces and an <c>@</c> included: the answer mentions nobody, so there is nothing to strip.</summary>
    [Fact]
    public void Compose_TextValue_IsSentAsTyped_AtSignsIncluded()
    {
        ElicitationForm form = FormOf(McpSchema);

        ElicitationAnswer answer = ElicitationComposer.Compose(form, Values(Pair("name", "  ask @Nova "), Pair("agree", "true")));

        Assert.Equal("  ask @Nova ", Assert.IsType<string>(answer.Content["name"]));
        Assert.Equal("> Name\n\n  ask @Nova \n\n> I agree\n\nYes", answer.Text);
    }

    /// <summary>Composing values that fail the check throws with the check's reason, so a UI bug cannot put a wrong type on the wire.</summary>
    [Fact]
    public void Compose_ValuesThatFailTheCheck_Throw()
    {
        ElicitationForm form = FormOf(McpSchema);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => ElicitationComposer.Compose(form, Values(Pair("name", "Ada"), Pair("agree", "true"), Pair("age", "abc"))));

        Assert.Equal("Field 'age' must be a whole number.", exception.Message);
    }

    /// <summary>
    /// <c>CanSend</c> and its reason: every schema-required field set, at least one field filled when
    /// none is required, numbers parsed with the invariant culture inside their bounds, an integer
    /// whole, text inside its lengths, a date as <c>yyyy-MM-dd</c>, a select among its options, and a
    /// typed custom answer standing in for a selection that would otherwise be refused.
    /// </summary>
    /// <param name="schema">The form's JSON Schema text.</param>
    /// <param name="valuesSpec">The values, as <c>key=a|b;key=c</c>.</param>
    /// <param name="expectedProblem">The reason the check gives, or <see langword="null"/> when the form may be sent.</param>
    [Theory]
    [InlineData(McpSchema, "", "Field 'name' is required.")]
    [InlineData(McpSchema, "name=Ada", "Field 'agree' is required.")]
    [InlineData(McpSchema, "name= ;agree=true", "Field 'name' is required.")]
    [InlineData(McpSchema, "name=Ada;agree=true", null)]
    [InlineData(McpSchema, "name=Ada;agree=false", null)]
    [InlineData(McpSchema, "name=Ada;agree=maybe", "Field 'agree' must be true or false.")]
    [InlineData(McpSchema, "name=A;agree=true", "Field 'name' must be at least 2 characters.")]
    [InlineData(McpSchema, "name=Ada;agree=true;age=2.5", "Field 'age' must be a whole number.")]
    [InlineData(McpSchema, "name=Ada;agree=true;age=abc", "Field 'age' must be a whole number.")]
    [InlineData(McpSchema, "name=Ada;agree=true;age=99999999999999999999", "Field 'age' must be a whole number.")]
    [InlineData(McpSchema, "name=Ada;agree=true;age=-1", "Field 'age' must be at least 0.")]
    [InlineData(McpSchema, "name=Ada;agree=true;age=121", "Field 'age' must be at most 120.")]
    [InlineData(McpSchema, "name=Ada;agree=true;age=0", null)]
    [InlineData(McpSchema, "name=Ada;agree=true;age=120", null)]
    [InlineData(McpSchema, "name=Ada;agree=true;ratio=abc", "Field 'ratio' must be a number.")]
    [InlineData(McpSchema, "name=Ada;agree=true;ratio=1,5", "Field 'ratio' must be a number.")]
    [InlineData(McpSchema, "name=Ada;agree=true;ratio=NaN", "Field 'ratio' must be a number.")]
    [InlineData(McpSchema, "name=Ada;agree=true;ratio=Infinity", "Field 'ratio' must be a number.")]
    [InlineData(McpSchema, "name=Ada;agree=true;ratio=0.4", "Field 'ratio' must be at least 0.5.")]
    [InlineData(McpSchema, "name=Ada;agree=true;ratio=9.6", "Field 'ratio' must be at most 9.5.")]
    [InlineData(McpSchema, "name=Ada;agree=true;ratio=0.5", null)]
    [InlineData(McpSchema, "name=Ada;agree=true;ratio=9.5", null)]
    [InlineData(McpSchema, "name=Ada;agree=true;due=2026-02-30", "Field 'due' must be a date in the form yyyy-MM-dd.")]
    [InlineData(McpSchema, "name=Ada;agree=true;due=10/01/2026", "Field 'due' must be a date in the form yyyy-MM-dd.")]
    [InlineData(McpSchema, "name=Ada;agree=true;due=2026-10-01", null)]
    [InlineData(McpSchema, "name=Ada;agree=true;color=blue", "Field 'color' must be one of its options.")]
    [InlineData(McpSchema, "name=Ada;agree=true;color=red|green", "Field 'color' takes one value.")]
    [InlineData(McpSchema, "name=Ada;agree=true;tags=a|z", "Field 'tags' must be one of its options.")]
    [InlineData(McpSchema, "name=Ada;agree=true;tags=a|b", null)]
    [InlineData(McpSchema, "ghost=1;name=Ada;agree=true", "The form has no field 'ghost'.")]
    [InlineData(NameSchema, "name=A", "Field 'name' must be at least 2 characters.")]
    [InlineData(NameSchema, "name=Adams", "Field 'name' must be at most 4 characters.")]
    [InlineData(NameSchema, "name=Ada", null)]
    [InlineData(OptionalSchema, "", "Fill in at least one field.")]
    [InlineData(OptionalSchema, "a= ", "Fill in at least one field.")]
    [InlineData(OptionalSchema, "a=x", null)]
    [InlineData(TwoQuestionSchema, "", "Fill in at least one field.")]
    [InlineData(TwoQuestionSchema, "question_0=Postgres", null)]
    [InlineData(TwoQuestionSchema, "question_0_custom=Oracle", null)]
    [InlineData(TwoQuestionSchema, "question_0=Nope", "Field 'question_0' must be one of its options.")]
    [InlineData(TwoQuestionSchema, "question_0=Nope;question_0_custom=Oracle", null)]
    [InlineData(TwoQuestionSchema, "question_0_custom=a|b", "Field 'question_0_custom' takes one value.")]
    [InlineData(RequiredSingleQuestionSchema, "", "Field 'question_0' is required.")]
    [InlineData(RequiredSingleQuestionSchema, "question_0=Postgres", null)]
    [InlineData(RequiredSingleQuestionSchema, "question_0_custom=Oracle", null)]
    [InlineData(RequiredSingleQuestionSchema, "question_0_custom= ", "Field 'question_0' is required.")]
    public void Check_GivesTheReason_AndCanSendAgrees(string schema, string valuesSpec, string? expectedProblem)
    {
        ArgumentNullException.ThrowIfNull(valuesSpec);
        ElicitationForm form = FormOf(schema);
        IReadOnlyDictionary<string, IReadOnlyList<string>> values = ParseValues(valuesSpec);

        string? problem = ElicitationComposer.Check(form, values);

        Assert.Equal(expectedProblem, problem);
        Assert.Equal(expectedProblem is null, ElicitationComposer.CanSend(form, values));
    }

    /// <summary>A number the German culture would write with a comma is refused, and the same number with a point is taken: the check never uses the machine's culture.</summary>
    [Fact]
    public void Check_NumberInANonInvariantCulture_StillUsesInvariantParsing()
    {
        ElicitationForm form = FormOf(McpSchema);
        string? withComma = "unset";
        string? withPoint = "unset";

        UnderCulture(
            "de-DE",
            () =>
            {
                withComma = ElicitationComposer.Check(form, Values(Pair("name", "Ada"), Pair("agree", "true"), Pair("ratio", "2,5")));
                withPoint = ElicitationComposer.Check(form, Values(Pair("name", "Ada"), Pair("agree", "true"), Pair("ratio", "2.5")));
            });

        Assert.Equal("Field 'ratio' must be a number.", withComma);
        Assert.Null(withPoint);
    }
}
