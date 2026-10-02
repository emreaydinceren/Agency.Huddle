using System.Globalization;
using Agency.Huddle.App.Elicitation;
using static Agency.Huddle.Tests.Elicitation.ElicitationTestSupport;

namespace Agency.Huddle.Tests.Elicitation;

/// <summary>
/// Pins <see cref="ElicitationSchemaReader"/> (elicitation bridge, decision E-6): the JSON Schema of an
/// agent's form is untrusted input, read into typed fields when every property is one of the supported
/// shapes and refused whole otherwise, with the reason naming the offending property. Real Adapter
/// schemas (AskUserQuestion, the refusal dialog) and an MCP-style form run through the same reader.
/// </summary>
public sealed class ElicitationSchemaReaderTests
{
    /// <summary>A plain string is a Text field carrying its title, description, length bounds and its place in <c>required</c>.</summary>
    [Fact]
    public void TryRead_PlainString_IsTextWithLengthBounds()
    {
        ElicitationForm form = FormOf(Schema(
            """
            "name":{"type":"string","title":"Your name","description":"As on your badge","minLength":2,"maxLength":40}
            """,
            """["name"]"""));

        ElicitationField field = Assert.Single(form.Fields);
        Assert.Equal("name|Text|label=Your name|desc=As on your badge|header=-|req=True|min=-|max=-|minLen=2|maxLen=40|for=-|opts=[]", Describe(field));
    }

    /// <summary>A string with an <c>enum</c> is a SingleSelect whose value and label are the same text.</summary>
    [Fact]
    public void TryRead_StringEnum_IsSingleSelectWithValueAsLabel()
    {
        ElicitationForm form = FormOf(Schema("""
            "color":{"type":"string","enum":["red","green"]}
            """));

        ElicitationField field = Assert.Single(form.Fields);
        Assert.Equal("color|SingleSelect|label=color|desc=-|header=-|req=False|min=-|max=-|minLen=-|maxLen=-|for=-|opts=[red/red/-;green/green/-]", Describe(field));
    }

    /// <summary>A string with <c>oneOf</c> uses each option's <c>const</c> as the value, its <c>title</c> as the label (the const when absent) and its description.</summary>
    [Fact]
    public void TryRead_StringOneOf_UsesConstTitleAndDescription()
    {
        ElicitationForm form = FormOf(Schema("""
            "size":{"type":"string","title":"Size","description":"Pick","oneOf":[{"const":"s","title":"Small","description":"Under 10"},{"const":"l"}]}
            """));

        ElicitationField field = Assert.Single(form.Fields);
        Assert.Equal("size|SingleSelect|label=Size|desc=Pick|header=-|req=False|min=-|max=-|minLen=-|maxLen=-|for=-|opts=[s/Small/Under 10;l/l/-]", Describe(field));
    }

    /// <summary>Only <c>format: date</c> makes a Date field; every other format stays text.</summary>
    /// <param name="format">The JSON Schema format.</param>
    /// <param name="expected">The kind the field is read as.</param>
    [Theory]
    [InlineData("date", ElicitationFieldKind.Date)]
    [InlineData("date-time", ElicitationFieldKind.Text)]
    [InlineData("email", ElicitationFieldKind.Text)]
    [InlineData("uri", ElicitationFieldKind.Text)]
    public void TryRead_StringFormats_DateIsDateOthersAreText(string format, ElicitationFieldKind expected)
    {
        ElicitationForm form = FormOf(Schema("\"when\":{\"type\":\"string\",\"format\":\"" + format + "\"}"));

        ElicitationField field = Assert.Single(form.Fields);
        Assert.Equal(expected, field.Kind);
    }

    /// <summary>Integer and number fields keep the kind and whatever bounds the schema gives.</summary>
    [Fact]
    public void TryRead_IntegerAndNumber_CarryKindAndBounds()
    {
        ElicitationForm form = FormOf(Schema("""
            "age":{"type":"integer","title":"Age","minimum":0,"maximum":120},"ratio":{"type":"number","minimum":0.5}
            """));

        Assert.Equal(
            [
                "age|Integer|label=Age|desc=-|header=-|req=False|min=0|max=120|minLen=-|maxLen=-|for=-|opts=[]",
                "ratio|Number|label=ratio|desc=-|header=-|req=False|min=0.5|max=-|minLen=-|maxLen=-|for=-|opts=[]",
            ],
            form.Fields.Select(Describe).ToArray());
    }

    /// <summary>A bound or a length of the wrong JSON type is ignored rather than refused: the schema simply gives none.</summary>
    [Fact]
    public void TryRead_BoundsOfTheWrongType_AreIgnored()
    {
        ElicitationForm form = FormOf(Schema("""
            "n":{"type":"integer","minimum":"low","maximum":null},"s":{"type":"string","minLength":"two","maxLength":1.5}
            """));

        Assert.Equal(
            [
                "n|Integer|label=n|desc=-|header=-|req=False|min=-|max=-|minLen=-|maxLen=-|for=-|opts=[]",
                "s|Text|label=s|desc=-|header=-|req=False|min=-|max=-|minLen=-|maxLen=-|for=-|opts=[]",
            ],
            form.Fields.Select(Describe).ToArray());
    }

    /// <summary>A title or description that is not a string is ignored: the label falls back to the key and there is no description.</summary>
    [Fact]
    public void TryRead_NonStringTitleAndDescription_AreIgnored()
    {
        ElicitationForm form = FormOf(Schema("""
            "a":{"type":"string","title":5,"description":true}
            """));

        ElicitationField field = Assert.Single(form.Fields);
        Assert.Equal("a|Text|label=a|desc=-|header=-|req=False|min=-|max=-|minLen=-|maxLen=-|for=-|opts=[]", Describe(field));
    }

    /// <summary>A boolean is a Boolean field.</summary>
    [Fact]
    public void TryRead_Boolean_IsBoolean()
    {
        ElicitationForm form = FormOf(Schema("""
            "agree":{"type":"boolean","title":"I agree","description":"Terms"}
            """));

        ElicitationField field = Assert.Single(form.Fields);
        Assert.Equal("agree|Boolean|label=I agree|desc=Terms|header=-|req=False|min=-|max=-|minLen=-|maxLen=-|for=-|opts=[]", Describe(field));
    }

    /// <summary>An array whose items are a string <c>enum</c> is a MultiSelect.</summary>
    [Fact]
    public void TryRead_ArrayOfStringEnum_IsMultiSelect()
    {
        ElicitationForm form = FormOf(Schema("""
            "tags":{"type":"array","title":"Tags","items":{"type":"string","enum":["a","b"]}}
            """));

        ElicitationField field = Assert.Single(form.Fields);
        Assert.Equal("tags|MultiSelect|label=Tags|desc=-|header=-|req=False|min=-|max=-|minLen=-|maxLen=-|for=-|opts=[a/a/-;b/b/-]", Describe(field));
    }

    /// <summary>An array whose items are an <c>anyOf</c> of const/title pairs is a MultiSelect with the titles as labels.</summary>
    [Fact]
    public void TryRead_ArrayWithAnyOf_IsMultiSelectWithTitles()
    {
        ElicitationForm form = FormOf(Schema("""
            "f":{"type":"array","items":{"anyOf":[{"const":"x","title":"Ex","description":"d"},{"const":"y"}]}}
            """));

        ElicitationField field = Assert.Single(form.Fields);
        Assert.Equal("f|MultiSelect|label=f|desc=-|header=-|req=False|min=-|max=-|minLen=-|maxLen=-|for=-|opts=[x/Ex/d;y/y/-]", Describe(field));
    }

    /// <summary>Only the fields the top-level <c>required</c> list names are required; a name that is not a property is ignored.</summary>
    [Fact]
    public void TryRead_RequiredList_MarksOnlyListedFields()
    {
        ElicitationForm form = FormOf(Schema("""
            "a":{"type":"string"},"b":{"type":"string"}
            """, """["b","ghost"]"""));

        Assert.Equal(
            [("a", false), ("b", true)],
            form.Fields.Select(field => (field.Key, field.Required)).ToArray());
    }

    /// <summary>A top-level schema with no <c>type</c> is read as the object the Adapter always means.</summary>
    [Fact]
    public void TryRead_RootWithoutType_IsReadAsAnObject()
    {
        ElicitationForm form = FormOf("""{"properties":{"a":{"type":"string"}}}""");

        ElicitationField field = Assert.Single(form.Fields);
        Assert.Equal("a", field.Key);
    }

    /// <summary>An MCP server's form: text with bounds, integer, number, boolean, date, enum and multi select, in schema order, with <c>required</c> applied.</summary>
    [Fact]
    public void TryRead_McpStyleForm_ReadsEveryFieldInOrder()
    {
        ElicitationForm form = FormOf(McpSchema, "Tell us about you.");

        Assert.Equal("Tell us about you.", form.Message);
        Assert.Equal(
            [
                "name|Text|label=Name|desc=Your full name|header=-|req=True|min=-|max=-|minLen=2|maxLen=40|for=-|opts=[]",
                "age|Integer|label=Age|desc=-|header=-|req=False|min=0|max=120|minLen=-|maxLen=-|for=-|opts=[]",
                "ratio|Number|label=Ratio|desc=-|header=-|req=False|min=0.5|max=9.5|minLen=-|maxLen=-|for=-|opts=[]",
                "agree|Boolean|label=I agree|desc=-|header=-|req=True|min=-|max=-|minLen=-|maxLen=-|for=-|opts=[]",
                "due|Date|label=Due|desc=-|header=-|req=False|min=-|max=-|minLen=-|maxLen=-|for=-|opts=[]",
                "color|SingleSelect|label=Color|desc=-|header=-|req=False|min=-|max=-|minLen=-|maxLen=-|for=-|opts=[red/red/-;green/green/-]",
                "tags|MultiSelect|label=Tags|desc=-|header=-|req=False|min=-|max=-|minLen=-|maxLen=-|for=-|opts=[a/a/-;b/b/-;c/c/-]",
            ],
            form.Fields.Select(Describe).ToArray());
    }

    /// <summary>
    /// The Adapter's single AskUserQuestion: the prompt rides in the request message and becomes the
    /// question's label, the header is the property title, and the <c>_custom</c> field is tied to its
    /// question.
    /// </summary>
    [Fact]
    public void TryRead_AskUserQuestionSingle_LabelIsTheMessageAndHeaderTheTitle()
    {
        ElicitationForm form = FormOf(SingleQuestionSchema, SingleQuestionMessage);

        Assert.Equal(SingleQuestionMessage, form.Message);
        Assert.Equal(
            [
                "question_0|SingleSelect|label=Which database?|desc=-|header=DB|req=False|min=-|max=-|minLen=-|maxLen=-|for=-|opts=[Postgres/Postgres/Relational;SQLite/SQLite/-]",
                "question_0_custom|Text|label=Other|desc=Type your own answer instead of choosing an option above (optional).|header=-|req=False|min=-|max=-|minLen=-|maxLen=-|for=question_0|opts=[]",
            ],
            form.Fields.Select(Describe).ToArray());
    }

    /// <summary>With several questions each carries its own description as the question text; a multi select is an array, and each custom field names its own question.</summary>
    [Fact]
    public void TryRead_AskUserQuestionTwoQuestions_LabelIsTheDescription()
    {
        ElicitationForm form = FormOf(TwoQuestionSchema, TwoQuestionMessage);

        Assert.Equal(TwoQuestionMessage, form.Message);
        Assert.Equal(
            [
                "question_0|SingleSelect|label=Which database?|desc=-|header=DB|req=False|min=-|max=-|minLen=-|maxLen=-|for=-|opts=[Postgres/Postgres/Relational;SQLite/SQLite/-]",
                "question_0_custom|Text|label=Other|desc=Type your own answer instead of choosing an option above (optional).|header=-|req=False|min=-|max=-|minLen=-|maxLen=-|for=question_0|opts=[]",
                "question_1|MultiSelect|label=Which features?|desc=-|header=Features|req=False|min=-|max=-|minLen=-|maxLen=-|for=-|opts=[Auth/Auth/-;Billing/Billing/-;Search/Search/-]",
                "question_1_custom|Text|label=Other|desc=Type your own answer instead of choosing an option above (optional).|header=-|req=False|min=-|max=-|minLen=-|maxLen=-|for=question_1|opts=[]",
            ],
            form.Fields.Select(Describe).ToArray());
    }

    /// <summary>A single question with no header: the message is the label and there is no header.</summary>
    [Fact]
    public void TryRead_AskUserQuestionWithoutHeader_HasNoHeader()
    {
        ElicitationForm form = FormOf(
            Schema("""
                "question_0":{"type":"string","oneOf":[{"const":"A","title":"A"},{"const":"B","title":"B"}]}
                """),
            "Pick a letter");

        ElicitationField field = Assert.Single(form.Fields);
        Assert.Equal("question_0|SingleSelect|label=Pick a letter|desc=-|header=-|req=False|min=-|max=-|minLen=-|maxLen=-|for=-|opts=[A/A/-;B/B/-]", Describe(field));
    }

    /// <summary>With several questions and no description, the label falls back to the title, then to the key; the generic message is never taken for a question.</summary>
    [Fact]
    public void TryRead_AskUserQuestionsWithoutDescriptions_FallBackToTitleThenKey()
    {
        ElicitationForm form = FormOf(
            Schema("""
                "question_0":{"type":"string","title":"First","oneOf":[{"const":"A"}]},"question_1":{"type":"string","oneOf":[{"const":"B"}]}
                """),
            TwoQuestionMessage);

        Assert.Equal(
            [
                "question_0|SingleSelect|label=First|desc=-|header=First|req=False|min=-|max=-|minLen=-|maxLen=-|for=-|opts=[A/A/-]",
                "question_1|SingleSelect|label=question_1|desc=-|header=-|req=False|min=-|max=-|minLen=-|maxLen=-|for=-|opts=[B/B/-]",
            ],
            form.Fields.Select(Describe).ToArray());
    }

    /// <summary>A custom-answer marker naming a question the form does not have is just a text field: it is tied to nothing.</summary>
    [Fact]
    public void TryRead_CustomFieldNamingAnUnknownQuestion_IsAPlainTextField()
    {
        ElicitationForm form = FormOf(Schema("""
            "note":{"type":"string","title":"Other","_meta":{"_askUserQuestionCustomAnswer":{"questionId":"question_9","isCustomAnswer":true}}}
            """));

        ElicitationField field = Assert.Single(form.Fields);
        Assert.Equal("note|Text|label=Other|desc=-|header=-|req=False|min=-|max=-|minLen=-|maxLen=-|for=-|opts=[]", Describe(field));
    }

    /// <summary>Two custom-answer fields that name each other tie to nothing: a custom answer is tied to a question, never to another custom answer, so both are plain text fields.</summary>
    [Fact]
    public void TryRead_CustomFieldsNamingEachOther_ArePlainTextFields()
    {
        ElicitationForm form = FormOf(Schema("""
            "a":{"type":"string","_meta":{"_askUserQuestionCustomAnswer":{"questionId":"b"}}},"b":{"type":"string","_meta":{"_askUserQuestionCustomAnswer":{"questionId":"a"}}}
            """));

        Assert.Equal(
            [
                "a|Text|label=a|desc=-|header=-|req=False|min=-|max=-|minLen=-|maxLen=-|for=-|opts=[]",
                "b|Text|label=b|desc=-|header=-|req=False|min=-|max=-|minLen=-|maxLen=-|for=-|opts=[]",
            ],
            form.Fields.Select(Describe).ToArray());
    }

    /// <summary>A question has one custom-answer field: when two claim it the first is tied and the second is a plain text field.</summary>
    [Fact]
    public void TryRead_TwoCustomFieldsForOneQuestion_OnlyTheFirstIsTied()
    {
        ElicitationForm form = FormOf(
            Schema("""
                "question_0":{"type":"string","oneOf":[{"const":"A"}]},"c1":{"type":"string","title":"One","_meta":{"_askUserQuestionCustomAnswer":{"questionId":"question_0"}}},"c2":{"type":"string","title":"Two","_meta":{"_askUserQuestionCustomAnswer":{"questionId":"question_0"}}}
                """),
            "Pick");

        Assert.Equal(
            [
                "question_0|SingleSelect|label=Pick|desc=-|header=-|req=False|min=-|max=-|minLen=-|maxLen=-|for=-|opts=[A/A/-]",
                "c1|Text|label=One|desc=-|header=-|req=False|min=-|max=-|minLen=-|maxLen=-|for=question_0|opts=[]",
                "c2|Text|label=Two|desc=-|header=-|req=False|min=-|max=-|minLen=-|maxLen=-|for=-|opts=[]",
            ],
            form.Fields.Select(Describe).ToArray());
    }

    /// <summary>The refusal-fallback dialog is a SingleSelect over its two result constants, with the dialog text as the form message.</summary>
    [Fact]
    public void TryRead_RefusalDialog_IsASingleSelectOverTheTwoResults()
    {
        ElicitationForm form = FormOf(RefusalSchema, RefusalMessage);

        Assert.Equal(RefusalMessage, form.Message);
        ElicitationField field = Assert.Single(form.Fields);
        Assert.Equal(
            "choice|SingleSelect|label=choice|desc=-|header=-|req=False|min=-|max=-|minLen=-|maxLen=-|for=-|opts=[retry_fallback/Retry with Opus/The session continues on Opus.;cancelled/Keep the refusal/You can send a new message.]",
            Describe(field));
    }

    /// <summary>Model-authored text is carried verbatim - markup, a newline, even a megabyte - because the Room renders it as plain text and the reader neither cleans nor cuts it.</summary>
    [Fact]
    public void TryRead_HostileText_IsCarriedVerbatim()
    {
        string huge = new('x', 1_000_000);
        string schema = Schema(
            "\"a\":{\"type\":\"string\",\"title\":\"<script>alert(1)</script>\",\"description\":\"" + huge + "\",\"oneOf\":[{\"const\":\"<i>\",\"title\":\"<b>bold</b>\",\"description\":\"<img src=x onerror=alert(2)>\"}]},"
            + "\"b\":{\"type\":\"string\",\"title\":\"line1\\nline2\"}");

        ElicitationForm form = FormOf(schema, "<script>alert(3)</script>");

        Assert.Equal("<script>alert(3)</script>", form.Message);
        ElicitationField first = form.Fields[0];
        Assert.Equal("<script>alert(1)</script>", first.Label);
        Assert.Equal(huge, first.Description);
        ElicitationOption option = Assert.Single(first.Options);
        Assert.Equal("<i>", option.Value);
        Assert.Equal("<b>bold</b>", option.Label);
        Assert.Equal("<img src=x onerror=alert(2)>", option.Description);
        Assert.Equal("line1\nline2", form.Fields[1].Label);
    }

    /// <summary>Twenty properties are read; the twenty-first refuses the whole form.</summary>
    /// <param name="count">How many properties the form has.</param>
    /// <param name="accepted">Whether the reader takes it.</param>
    [Theory]
    [InlineData(20, true)]
    [InlineData(21, false)]
    public void TryRead_PropertyCap_IsTwenty(int count, bool accepted)
    {
        string properties = string.Join(",", Enumerable.Range(0, count).Select(index => "\"p" + index.ToString(CultureInfo.InvariantCulture) + "\":{\"type\":\"string\"}"));

        bool read = ElicitationSchemaReader.TryRead(Request(Schema(properties)), out ElicitationForm? form, out string? problem);

        Assert.Equal(accepted, read);
        if (accepted)
        {
            Assert.NotNull(form);
            Assert.Equal(count, form.Fields.Count);
        }
        else
        {
            Assert.Null(form);
            Assert.Equal("The form has 21 properties; the limit is 20.", problem);
        }
    }

    /// <summary>Fifty options on one field are read in every select shape; the fifty-first refuses the whole form.</summary>
    /// <param name="shape">Which select shape carries the options: oneOf, enum, anyOf (array) or itemsEnum (array).</param>
    /// <param name="count">How many options the field has.</param>
    /// <param name="accepted">Whether the reader takes it.</param>
    [Theory]
    [InlineData("oneOf", 50, true)]
    [InlineData("oneOf", 51, false)]
    [InlineData("enum", 50, true)]
    [InlineData("enum", 51, false)]
    [InlineData("anyOf", 50, true)]
    [InlineData("anyOf", 51, false)]
    [InlineData("itemsEnum", 50, true)]
    [InlineData("itemsEnum", 51, false)]
    public void TryRead_OptionCap_IsFifty(string shape, int count, bool accepted)
    {
        string[] values = [.. Enumerable.Range(0, count).Select(index => "v" + index.ToString(CultureInfo.InvariantCulture))];
        string consts = string.Join(",", values.Select(value => "{\"const\":\"" + value + "\"}"));
        string names = string.Join(",", values.Select(value => "\"" + value + "\""));
        string field = shape switch
        {
            "oneOf" => "\"f\":{\"type\":\"string\",\"oneOf\":[" + consts + "]}",
            "enum" => "\"f\":{\"type\":\"string\",\"enum\":[" + names + "]}",
            "anyOf" => "\"f\":{\"type\":\"array\",\"items\":{\"anyOf\":[" + consts + "]}}",
            _ => "\"f\":{\"type\":\"array\",\"items\":{\"type\":\"string\",\"enum\":[" + names + "]}}",
        };

        bool read = ElicitationSchemaReader.TryRead(Request(Schema(field)), out ElicitationForm? form, out string? problem);

        Assert.Equal(accepted, read);
        if (accepted)
        {
            Assert.NotNull(form);
            Assert.Equal(count, Assert.Single(form.Fields).Options.Count);
        }
        else
        {
            Assert.Null(form);
            Assert.Equal("Property 'f' has 51 options; the limit is 50.", problem);
        }
    }

    /// <summary>Every shape the bridge cannot show faithfully is refused whole, with a reason that names the offending property, never degraded to a text box the server would reject.</summary>
    /// <param name="schema">The form's JSON Schema text.</param>
    /// <param name="expectedProblem">The reason the reader gives.</param>
    [Theory]
    [InlineData("not json", "The form is not valid JSON.")]
    [InlineData("", "The form is not valid JSON.")]
    [InlineData("[]", "The form's top level must be an object.")]
    [InlineData("""{"type":"string"}""", "The form's top level must be an object.")]
    [InlineData("""{"type":"object"}""", "The form has no properties.")]
    [InlineData("""{"type":"object","properties":{}}""", "The form has no properties.")]
    [InlineData("""{"type":"object","properties":[]}""", "The form has no properties.")]
    [InlineData("""{"type":"object","$ref":"#/x","properties":{"a":{"type":"string"}}}""", "The form uses '$ref', which is not supported.")]
    [InlineData("""{"type":"object","allOf":[],"properties":{"a":{"type":"string"}}}""", "The form uses 'allOf', which is not supported.")]
    [InlineData("""{"type":"object","properties":{"a":{"type":"string"},"a":{"type":"string"}}}""", "Property 'a' appears more than once.")]
    [InlineData("""{"type":"object","properties":{"a":true}}""", "Property 'a' is not an object.")]
    [InlineData("""{"type":"object","properties":{"a":{"$ref":"#/defs/x"}}}""", "Property 'a' uses '$ref', which is not supported.")]
    [InlineData("""{"type":"object","properties":{"a":{"allOf":[{"type":"string"}],"type":"string"}}}""", "Property 'a' uses 'allOf', which is not supported.")]
    [InlineData("""{"type":"object","properties":{"a":{"description":"no type here"}}}""", "Property 'a' has no type.")]
    [InlineData("""{"type":"object","properties":{"a":{"type":["string","null"]}}}""", "Property 'a' has a type that is not a single name.")]
    [InlineData("""{"type":"object","properties":{"a":{"type":"object","properties":{"b":{"type":"string"}}}}}""", "Property 'a' has the unsupported type 'object'.")]
    [InlineData("""{"type":"object","properties":{"a":{"type":"null"}}}""", "Property 'a' has the unsupported type 'null'.")]
    [InlineData("""{"type":"object","properties":{"a":{"type":"array","items":{"type":"object","properties":{}}}}}""", "Property 'a' is an array of objects, which is not supported.")]
    [InlineData("""{"type":"object","properties":{"a":{"type":"array"}}}""", "Property 'a' is an array whose items are not a list of strings.")]
    [InlineData("""{"type":"object","properties":{"a":{"type":"array","items":{"type":"string"}}}}""", "Property 'a' is an array whose items are not a list of strings.")]
    [InlineData("""{"type":"object","properties":{"a":{"type":"array","items":{"type":"string","enum":["x",1]}}}}""", "Property 'a' has an option that is not a string constant.")]
    [InlineData("""{"type":"object","properties":{"a":{"type":"string","enum":["x",1]}}}""", "Property 'a' has an option that is not a string constant.")]
    [InlineData("""{"type":"object","properties":{"a":{"type":"string","oneOf":[{"title":"no const"}]}}}""", "Property 'a' has an option that is not a string constant.")]
    [InlineData("""{"type":"object","properties":{"a":{"type":"string","oneOf":["x"]}}}""", "Property 'a' has an option that is not a string constant.")]
    [InlineData("""{"type":"object","properties":{"a":{"type":"string","oneOf":[]}}}""", "Property 'a' has no options.")]
    [InlineData("""{"type":"object","properties":{"a":{"type":"string","oneOf":"x"}}}""", "Property 'a' has no options.")]
    [InlineData("""{"type":"object","properties":{"a":{"type":"string","enum":[]}}}""", "Property 'a' has no options.")]
    [InlineData("""{"type":"object","properties":{"a":{"type":"string","anyOf":[{"const":"x"}]}}}""", "Property 'a' uses 'anyOf', which is not supported.")]
    [InlineData("""{"type":"object","properties":{"a":{"type":"number","enum":[1,2]}}}""", "Property 'a' is a number with a fixed set of values, which is not supported.")]
    [InlineData("""{"type":"object","properties":{"a":{"type":"integer","oneOf":[{"const":1}]}}}""", "Property 'a' is a number with a fixed set of values, which is not supported.")]
    public void TryRead_UnsupportedShape_IsRefusedNamingTheProblem(string schema, string expectedProblem)
    {
        bool read = ElicitationSchemaReader.TryRead(Request(schema), out ElicitationForm? form, out string? problem);

        Assert.False(read);
        Assert.Null(form);
        Assert.Equal(expectedProblem, problem);
    }
}
