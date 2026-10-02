using System.Globalization;
using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Elicitation;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Elicitation;
using static Agency.Huddle.Tests.Elicitation.ElicitationTestSupport;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Pins the Elicitation card (elicitation bridge, decisions E-5 and E-6): every form waiting in a Room is
/// shown in arrival order with one input per field, all agent-authored text as plain text; Send is held
/// back until the composer says the values may be sent and hands <c>ElicitationService</c> exactly the
/// values shape the composer reads; Skip declines and posts nothing; a card another tab answered, a
/// typed Message or an archive removed vanishes on <see cref="RoomEvents.ElicitationsChanged"/>. Built
/// like <c>QuestionCardTests</c>: the real <see cref="ElicitationFixture"/> collaborators over one
/// <see cref="TempDataDir"/>, registered into a <see cref="MudBunitContext"/>.
/// </summary>
public sealed class ElicitationCardTests
{
    private static readonly TimeSpan BoundedWait = TimeSpan.FromSeconds(5);

    /// <summary>E-5: every waiting card of the Room is shown, in arrival order, each its own labelled region headed by its asker.</summary>
    [Fact]
    public async Task TwoCards_RenderInArrivalOrder_EachAsItsOwnRegionHeadedByItsAsker()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using ElicitationFixture fixture = await ElicitationFixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        User nova = await fixture.AddAgentAsync("Nova", ct);
        Room room = await fixture.CreateRoomAsync([coach, nova], ct);
        _ = fixture.Elicitations.Add(room.Id, coach.Id, coach.Name, FormOf(SingleQuestionSchema, SingleQuestionMessage));
        _ = fixture.Elicitations.Add(room.Id, nova.Id, nova.Name, FormOf(RefusalSchema, RefusalMessage));
        await using MudBunitContext ctx = new();
        Register(ctx, fixture);

        IRenderedComponent<ContainerFragment> cut = RenderCard(ctx, room.Id);

        Assert.Equal(["Coach is asking", "Nova is asking"], [.. cut.FindAll(".elicitation-card-title").Select(title => title.TextContent.Trim())]);
        Assert.Equal([RefusalMessage], [.. cut.FindAll(".elicitation-card-message").Select(message => message.TextContent.Trim())]);
        Assert.Equal(["Form from Coach", "Form from Nova"], [.. cut.FindAll("[role=region]").Select(region => region.GetAttribute("aria-label") ?? string.Empty)]);
    }

    /// <summary>A Room with no waiting card renders nothing at all.</summary>
    [Fact]
    public async Task NoCard_RendersNothing()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using ElicitationFixture fixture = await ElicitationFixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);
        await using MudBunitContext ctx = new();
        Register(ctx, fixture);

        IRenderedComponent<ContainerFragment> cut = RenderCard(ctx, room.Id);

        Assert.Empty(cut.FindAll(".elicitation-card"));
    }

    /// <summary>E-5: an Archived Room shows no card, even when one was stored directly; the drop on archive is ChatService's job.</summary>
    [Fact]
    public async Task ArchivedRoom_ShowsNoCard()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using ElicitationFixture fixture = await ElicitationFixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);
        _ = fixture.AddCard(room.Id, coach);
        await fixture.Directory.SetRoomArchivedAsync(room.Id, true, ct);
        await using MudBunitContext ctx = new();
        Register(ctx, fixture);

        IRenderedComponent<ContainerFragment> cut = RenderCard(ctx, room.Id);

        Assert.Empty(cut.FindAll(".elicitation-card"));
    }

    /// <summary>E-6: a card in another Room is not this Room's card.</summary>
    [Fact]
    public async Task CardOfAnotherRoom_IsNotShown()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using ElicitationFixture fixture = await ElicitationFixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        User nova = await fixture.AddAgentAsync("Nova", ct);
        Room mine = await fixture.CreateRoomAsync([coach], ct);
        Room other = await fixture.CreateRoomAsync([nova], ct);
        _ = fixture.AddCard(other.Id, nova);
        await using MudBunitContext ctx = new();
        Register(ctx, fixture);

        IRenderedComponent<ContainerFragment> cut = RenderCard(ctx, mine.Id);

        Assert.Empty(cut.FindAll(".elicitation-card"));
    }

    /// <summary>
    /// E-6: every agent-authored text - the message, a title, a description, an option and its
    /// description, an AskUserQuestion header - is plain text: markup in it is shown as the literal
    /// characters and becomes no element.
    /// </summary>
    [Fact]
    public async Task AgentText_IsRenderedAsPlainText_NeverAsMarkup()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using ElicitationFixture fixture = await ElicitationFixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);
        string schema = Schema(
            """
            "note":{"type":"string","title":"<b>Label</b>","description":"<i>help</i>"},
            "question_0":{"type":"string","title":"<u>Head</u>","description":"<em>Question</em>","oneOf":[{"const":"a","title":"<b>opt</b>","description":"<script>d</script>"}]},
            "question_1":{"type":"array","title":"<s>Head2</s>","description":"<mark>Question2</mark>","items":{"anyOf":[{"const":"x","title":"<kbd>chk</kbd>","description":"<blink>cd</blink>"}]}}
            """);
        _ = fixture.Elicitations.Add(room.Id, coach.Id, coach.Name, FormOf(schema, "<script>alert(1)</script>"));
        await using MudBunitContext ctx = new();
        Register(ctx, fixture);

        IRenderedComponent<ContainerFragment> cut = RenderCard(ctx, room.Id);

        Assert.Equal("<script>alert(1)</script>", cut.Find(".elicitation-card-message").TextContent.Trim());
        Assert.Equal("<b>Label</b>", cut.Find("label.mud-input-label").TextContent.Trim());
        Assert.Equal("<i>help</i>", cut.Find(".mud-input-helper-text").TextContent.Trim());
        Assert.Equal(["<u>Head</u>", "<s>Head2</s>"], [.. cut.FindAll(".elicitation-card-field-header").Select(header => header.TextContent.Trim())]);
        Assert.Equal(["<em>Question</em>", "<mark>Question2</mark>"], [.. cut.FindAll(".elicitation-card-field-label").Select(label => label.TextContent.Trim())]);
        Assert.Equal(["<b>opt</b>", "<kbd>chk</kbd>"], [.. cut.FindAll(".elicitation-card-option-label").Select(label => label.TextContent.Trim())]);
        Assert.Equal(["<script>d</script>", "<blink>cd</blink>"], [.. cut.FindAll(".elicitation-card-option-description").Select(description => description.TextContent.Trim())]);
        Assert.Empty(cut.Find(".elicitation-card").QuerySelectorAll("b, i, u, em, s, mark, kbd, blink, script"));
    }

    /// <summary>
    /// An AskUserQuestion form shows each question under its header with its options in the schema's
    /// order, the question's description as its label and each option's own description, one labelled
    /// group per question; the typed "Other" answer sits inside its question, not as a field of its own.
    /// </summary>
    [Fact]
    public async Task QuestionForm_RendersHeaderLabelGroupOptionsAndOtherUnderItsQuestion()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using ElicitationFixture fixture = await ElicitationFixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);
        _ = fixture.Elicitations.Add(room.Id, coach.Id, coach.Name, FormOf(TwoQuestionSchema, TwoQuestionMessage));
        await using MudBunitContext ctx = new();
        Register(ctx, fixture);

        IRenderedComponent<ContainerFragment> cut = RenderCard(ctx, room.Id);

        Assert.Equal(["DB", "Features"], [.. cut.FindAll(".elicitation-card-field-header").Select(header => header.TextContent.Trim())]);
        Assert.Equal(["Which database?", "Which features?"], [.. cut.FindAll(".elicitation-card-field-label").Select(label => label.TextContent.Trim())]);
        Assert.Equal(["Which database?", "Which features?"], [.. cut.FindAll(".elicitation-card-group[role=group]").Select(group => group.GetAttribute("aria-label") ?? string.Empty)]);
        Assert.Equal(["Postgres", "SQLite", "Auth", "Billing", "Search"], [.. cut.FindAll(".elicitation-card-option-label").Select(label => label.TextContent.Trim())]);
        Assert.Equal(["Relational"], [.. cut.FindAll(".elicitation-card-option-description").Select(description => description.TextContent.Trim())]);
        Assert.Equal(2, cut.FindAll(".elicitation-card-field").Count);
        Assert.Equal(["Other", "Other"], [.. cut.FindAll("label.mud-input-label").Select(label => label.TextContent.Trim())]);
        Assert.Equal(2, cut.FindAll(".elicitation-card-field label.mud-input-label").Count);
    }

    /// <summary>A text field is a text input labelled with the schema's title and helped by its description; its typed value is sent as a string.</summary>
    [Fact]
    public async Task TextField_Typed_ReachesTheAnswerAsAString()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using ElicitationFixture fixture = await ElicitationFixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);
        PendingElicitation card = fixture.Elicitations.Add(room.Id, coach.Id, coach.Name, FormOf(McpSchema));
        await using MudBunitContext ctx = new();
        Register(ctx, fixture);
        IRenderedComponent<ContainerFragment> cut = RenderCard(ctx, room.Id);
        Assert.Equal("Your full name", FieldControl(cut, 0, "Name").QuerySelector(".mud-input-helper-text")?.TextContent.Trim());

        await TypeAsync(cut, 0, "Name", "Ada Lovelace");
        await ToggleAsync(cut, 0, "label.mud-switch", "I agree");
        await SendAsync(cut, 0);

        Assert.Equal(["agree=Boolean:True", "name=String:Ada Lovelace"], await AcceptedAsync(card, ct));
    }

    /// <summary>A number field holds the typed text and is sent as a double.</summary>
    [Fact]
    public async Task NumberField_Typed_ReachesTheAnswerAsADouble()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using ElicitationFixture fixture = await ElicitationFixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);
        PendingElicitation card = fixture.Elicitations.Add(room.Id, coach.Id, coach.Name, FormOf(Schema("\"ratio\":{\"type\":\"number\",\"title\":\"Ratio\",\"minimum\":0.5,\"maximum\":9.5}")));
        await using MudBunitContext ctx = new();
        Register(ctx, fixture);
        IRenderedComponent<ContainerFragment> cut = RenderCard(ctx, room.Id);

        await TypeAsync(cut, 0, "Ratio", "2.5");
        await SendAsync(cut, 0);

        Assert.Equal(["ratio=Double:2.5"], await AcceptedAsync(card, ct));
    }

    /// <summary>An integer field holds the typed text and is sent as a long.</summary>
    [Fact]
    public async Task IntegerField_Typed_ReachesTheAnswerAsALong()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using ElicitationFixture fixture = await ElicitationFixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);
        PendingElicitation card = fixture.Elicitations.Add(room.Id, coach.Id, coach.Name, FormOf(Schema("\"age\":{\"type\":\"integer\",\"title\":\"Age\",\"minimum\":0,\"maximum\":120}")));
        await using MudBunitContext ctx = new();
        Register(ctx, fixture);
        IRenderedComponent<ContainerFragment> cut = RenderCard(ctx, room.Id);

        await TypeAsync(cut, 0, "Age", "42");
        await SendAsync(cut, 0);

        Assert.Equal(["age=Int64:42"], await AcceptedAsync(card, ct));
    }

    /// <summary>A number the composer cannot parse (text, or outside its bounds) keeps Send disabled; a valid one enables it.</summary>
    [Theory]
    [InlineData("abc", false)]
    [InlineData("12", false)]
    [InlineData("1,5", false)]
    [InlineData("2.5", true)]
    public async Task NumberField_SendIsEnabledOnlyForAValidNumber(string typed, bool enabled)
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using ElicitationFixture fixture = await ElicitationFixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);
        _ = fixture.Elicitations.Add(room.Id, coach.Id, coach.Name, FormOf(Schema("\"ratio\":{\"type\":\"number\",\"title\":\"Ratio\",\"minimum\":0.5,\"maximum\":9.5}")));
        await using MudBunitContext ctx = new();
        Register(ctx, fixture);
        IRenderedComponent<ContainerFragment> cut = RenderCard(ctx, room.Id);

        await TypeAsync(cut, 0, "Ratio", typed);

        Assert.Equal(!enabled, SendButton(cut, 0).HasAttribute("disabled"));
    }

    /// <summary>A date field is sent as the ISO date text <c>yyyy-MM-dd</c>, whatever the machine's culture.</summary>
    [Fact]
    public async Task DateField_Typed_ReachesTheAnswerAsIsoText()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using ElicitationFixture fixture = await ElicitationFixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);
        PendingElicitation card = fixture.Elicitations.Add(room.Id, coach.Id, coach.Name, FormOf(Schema("\"due\":{\"type\":\"string\",\"title\":\"Due\",\"format\":\"date\"}")));
        await using MudBunitContext ctx = new();
        Register(ctx, fixture);
        IRenderedComponent<ContainerFragment> cut = RenderCard(ctx, room.Id);
        Assert.NotNull(cut.Find(".elicitation-card .mud-picker"));

        await ChangeAsync(cut, 0, "Due", "2026-10-05");
        await SendAsync(cut, 0);

        Assert.Equal(["due=String:2026-10-05"], await AcceptedAsync(card, ct));
    }

    /// <summary>A boolean is a switch: off and untouched is no answer, on sends true as a bool, and on then off sends false.</summary>
    [Fact]
    public async Task BooleanField_Switched_ReachesTheAnswerAsABool()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using ElicitationFixture fixture = await ElicitationFixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);
        PendingElicitation card = fixture.Elicitations.Add(room.Id, coach.Id, coach.Name, FormOf(Schema("\"agree\":{\"type\":\"boolean\",\"title\":\"I agree\"}")));
        await using MudBunitContext ctx = new();
        Register(ctx, fixture);
        IRenderedComponent<ContainerFragment> cut = RenderCard(ctx, room.Id);
        Assert.True(SendButton(cut, 0).HasAttribute("disabled"));

        await ToggleAsync(cut, 0, "label.mud-switch", "I agree");
        Assert.False(SendButton(cut, 0).HasAttribute("disabled"));
        await ToggleAsync(cut, 0, "label.mud-switch", "I agree");
        Assert.False(SendButton(cut, 0).HasAttribute("disabled"));
        await SendAsync(cut, 0);

        Assert.Equal(["agree=Boolean:False"], await AcceptedAsync(card, ct));
    }

    /// <summary>A single select is a radio group showing each option's title and description; choosing one sends its value, not its title.</summary>
    [Fact]
    public async Task SingleSelect_Chosen_ReachesTheAnswerAsTheOptionValue_AndIsPostedToTheTranscript()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using ElicitationFixture fixture = await ElicitationFixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);
        string schema = Schema("\"color\":{\"type\":\"string\",\"title\":\"Color\",\"oneOf\":[{\"const\":\"r\",\"title\":\"Red\",\"description\":\"Warm\"},{\"const\":\"g\",\"title\":\"Green\"}]}");
        PendingElicitation card = fixture.Elicitations.Add(room.Id, coach.Id, coach.Name, FormOf(schema));
        await using MudBunitContext ctx = new();
        Register(ctx, fixture);
        IRenderedComponent<ContainerFragment> cut = RenderCard(ctx, room.Id);
        Assert.Equal(["Red", "Green"], [.. cut.FindAll("label.mud-radio .elicitation-card-option-label").Select(label => label.TextContent.Trim())]);
        Assert.Equal(["Warm"], [.. cut.FindAll("label.mud-radio .elicitation-card-option-description").Select(description => description.TextContent.Trim())]);

        await ToggleAsync(cut, 0, "label.mud-radio", "Green");
        await SendAsync(cut, 0);

        Assert.Equal(["color=String:g"], await AcceptedAsync(card, ct));
        ChatMessage posted = Assert.Single(await fixture.Transcript.ReadAllAsync(room.Id, ct));
        Assert.Equal(KnownIds.Human, posted.SenderId);
        Assert.Equal("> Color\n\nGreen", posted.Text);
    }

    /// <summary>A multi select is a checkbox per option; toggling adds and removes, and the answer is a string array in option order.</summary>
    [Fact]
    public async Task MultiSelect_Toggled_ReachesTheAnswerAsAStringArrayInOptionOrder()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using ElicitationFixture fixture = await ElicitationFixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);
        string schema = Schema("\"tags\":{\"type\":\"array\",\"title\":\"Tags\",\"items\":{\"type\":\"string\",\"enum\":[\"a\",\"b\",\"c\"]}}");
        PendingElicitation card = fixture.Elicitations.Add(room.Id, coach.Id, coach.Name, FormOf(schema));
        await using MudBunitContext ctx = new();
        Register(ctx, fixture);
        IRenderedComponent<ContainerFragment> cut = RenderCard(ctx, room.Id);
        Assert.Equal(["a", "b", "c"], [.. cut.FindAll("label.mud-checkbox .elicitation-card-option-label").Select(label => label.TextContent.Trim())]);
        Assert.True(SendButton(cut, 0).HasAttribute("disabled"));

        await ToggleAsync(cut, 0, "label.mud-checkbox", "c");
        await ToggleAsync(cut, 0, "label.mud-checkbox", "a");
        await ToggleAsync(cut, 0, "label.mud-checkbox", "b");
        await ToggleAsync(cut, 0, "label.mud-checkbox", "b");
        Assert.Equal([true, false, true], [.. OptionLabels(cut, 0, "label.mud-checkbox").Select(IsChecked)]);
        await SendAsync(cut, 0);

        Assert.Equal(["tags=String[]:a,c"], await AcceptedAsync(card, ct));
    }

    /// <summary>The refusal dialog is one single select, <c>choice</c>; whichever option is chosen is accepted under that key.</summary>
    [Theory]
    [InlineData("Retry with Opus", "retry_fallback")]
    [InlineData("Keep the refusal", "cancelled")]
    public async Task RefusalDialog_ChoiceChosen_IsAcceptedUnderChoice(string optionLabel, string expected)
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using ElicitationFixture fixture = await ElicitationFixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);
        PendingElicitation card = fixture.Elicitations.Add(room.Id, coach.Id, coach.Name, FormOf(RefusalSchema, RefusalMessage));
        await using MudBunitContext ctx = new();
        Register(ctx, fixture);
        IRenderedComponent<ContainerFragment> cut = RenderCard(ctx, room.Id);
        Assert.Equal(RefusalMessage, cut.Find(".elicitation-card-message").TextContent.Trim());

        await ToggleAsync(cut, 0, "label.mud-radio", optionLabel);
        await SendAsync(cut, 0);

        Assert.Equal(["choice=String:" + expected], await AcceptedAsync(card, ct));
    }

    /// <summary>E-6: a schema-required field is marked, and Send stays disabled until it is filled; once it is, Send is enabled.</summary>
    [Fact]
    public async Task RequiredField_IsMarked_AndHoldsSendBackUntilFilled()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using ElicitationFixture fixture = await ElicitationFixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);
        string schema = Schema("\"name\":{\"type\":\"string\",\"title\":\"Name\"},\"note\":{\"type\":\"string\",\"title\":\"Note\"}", "[\"name\"]");
        _ = fixture.Elicitations.Add(room.Id, coach.Id, coach.Name, FormOf(schema));
        await using MudBunitContext ctx = new();
        Register(ctx, fixture);
        IRenderedComponent<ContainerFragment> cut = RenderCard(ctx, room.Id);
        Assert.True(FieldControl(cut, 0, "Name").QuerySelector("input")?.HasAttribute("required"));
        Assert.False(FieldControl(cut, 0, "Note").QuerySelector("input")?.HasAttribute("required"));
        Assert.True(SendButton(cut, 0).HasAttribute("disabled"));

        await TypeAsync(cut, 0, "Note", "only the note");
        Assert.True(SendButton(cut, 0).HasAttribute("disabled"));

        await TypeAsync(cut, 0, "Name", "Ada");
        Assert.False(SendButton(cut, 0).HasAttribute("disabled"));
    }

    /// <summary>A required select is marked in its group (a visible mark and <c>aria-required</c>); an optional one is not.</summary>
    [Fact]
    public async Task RequiredSelect_IsMarkedInItsGroup_AndAnOptionalOneIsNot()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using ElicitationFixture fixture = await ElicitationFixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);
        _ = fixture.Elicitations.Add(room.Id, coach.Id, coach.Name, FormOf(RequiredSingleQuestionSchema, SingleQuestionMessage));
        _ = fixture.Elicitations.Add(room.Id, coach.Id, coach.Name, FormOf(SingleQuestionSchema, SingleQuestionMessage));
        await using MudBunitContext ctx = new();
        Register(ctx, fixture);

        IRenderedComponent<ContainerFragment> cut = RenderCard(ctx, room.Id);

        Assert.Equal(["true", string.Empty], [.. cut.FindAll(".elicitation-card-group[role=group]").Select(group => group.GetAttribute("aria-required") ?? string.Empty)]);
        Assert.Single(cut.FindAll(".elicitation-card-required"));
        Assert.Single(Card(cut, 0).QuerySelectorAll(".elicitation-card-required"));
        Assert.True(SendButton(cut, 0).HasAttribute("disabled"));
    }

    /// <summary>E-6: with no field required, Send needs at least one field filled.</summary>
    [Fact]
    public async Task NoRequiredField_SendNeedsOneFilledField()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using ElicitationFixture fixture = await ElicitationFixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);
        string schema = Schema("\"a\":{\"type\":\"string\",\"title\":\"A\"},\"b\":{\"type\":\"string\",\"title\":\"B\"}");
        _ = fixture.Elicitations.Add(room.Id, coach.Id, coach.Name, FormOf(schema));
        await using MudBunitContext ctx = new();
        Register(ctx, fixture);
        IRenderedComponent<ContainerFragment> cut = RenderCard(ctx, room.Id);
        Assert.True(SendButton(cut, 0).HasAttribute("disabled"));

        await TypeAsync(cut, 0, "B", "something");
        Assert.False(SendButton(cut, 0).HasAttribute("disabled"));

        await TypeAsync(cut, 0, "B", "   ");
        Assert.True(SendButton(cut, 0).HasAttribute("disabled"));
    }

    /// <summary>Skip declines the waiting request and posts nothing - even with a field filled in - and the card goes.</summary>
    [Fact]
    public async Task Skip_DeclinesAndPostsNothing_EvenWithAFieldFilled()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using ElicitationFixture fixture = await ElicitationFixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);
        PendingElicitation card = fixture.Elicitations.Add(room.Id, coach.Id, coach.Name, FormOf(Schema("\"note\":{\"type\":\"string\",\"title\":\"Note\"}")));
        await using MudBunitContext ctx = new();
        Register(ctx, fixture);
        IRenderedComponent<ContainerFragment> cut = RenderCard(ctx, room.Id);
        await TypeAsync(cut, 0, "Note", "half written");

        await cut.InvokeAsync(() => SkipButton(cut, 0).ClickAsync());

        Assert.IsType<ElicitationDeclined>(await card.Completion.Task.WaitAsync(BoundedWait, ct));
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".elicitation-card")), BoundedWait);
        Assert.Empty(await fixture.Transcript.ReadAllAsync(room.Id, ct));
        Assert.Empty(fixture.Elicitations.Get(room.Id));
    }

    /// <summary>
    /// E-5: answering the first card leaves the second one waiting, unanswered, and what the Human had
    /// typed into it is still there - an entry resets only when that card's own id changes.
    /// </summary>
    [Fact]
    public async Task FirstCardAnswered_SecondCardIsUnaffected_AndKeepsWhatWasTyped()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using ElicitationFixture fixture = await ElicitationFixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        User nova = await fixture.AddAgentAsync("Nova", ct);
        Room room = await fixture.CreateRoomAsync([coach, nova], ct);
        PendingElicitation first = fixture.Elicitations.Add(room.Id, coach.Id, coach.Name, FormOf(Schema("\"note\":{\"type\":\"string\",\"title\":\"First note\"}")));
        PendingElicitation second = fixture.Elicitations.Add(room.Id, nova.Id, nova.Name, FormOf(Schema("\"note\":{\"type\":\"string\",\"title\":\"Second note\"}")));
        await using MudBunitContext ctx = new();
        Register(ctx, fixture);
        IRenderedComponent<ContainerFragment> cut = RenderCard(ctx, room.Id);
        await TypeAsync(cut, 1, "Second note", "typed for Nova");
        await TypeAsync(cut, 0, "First note", "typed for Coach");

        await SendAsync(cut, 0);

        Assert.Equal(["note=String:typed for Coach"], await AcceptedAsync(first, ct));
        cut.WaitForAssertion(() => Assert.Equal(["Nova is asking"], [.. cut.FindAll(".elicitation-card-title").Select(title => title.TextContent.Trim())]), BoundedWait);
        Assert.False(second.Completion.Task.IsCompleted);
        Assert.Equal("typed for Nova", FieldControl(cut, 0, "Second note").QuerySelector("input")?.GetAttribute("value"));
        Assert.False(SendButton(cut, 0).HasAttribute("disabled"));
    }

    /// <summary>Q12-style: another tab answers the same card through the service, and this card, which did not cause the change, still vanishes on the change event.</summary>
    [Fact]
    public async Task OtherTabAnswers_CardVanishes()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using ElicitationFixture fixture = await ElicitationFixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);
        PendingElicitation card = fixture.AddCard(room.Id, coach);
        await using MudBunitContext ctx = new();
        Register(ctx, fixture);
        IRenderedComponent<ContainerFragment> cut = RenderCard(ctx, room.Id);
        Assert.Single(cut.FindAll(".elicitation-card"));

        _ = await fixture.Service.AnswerAsync(room.Id, card.Id, Values(Pair("question_0", "SQLite")), ct);

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".elicitation-card")), BoundedWait);
    }

    /// <summary>Another tab skips the card: it vanishes here too, and only that card - the Room's other card stays.</summary>
    [Fact]
    public async Task OtherTabSkips_ThatCardVanishes_AndTheOtherStays()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using ElicitationFixture fixture = await ElicitationFixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        User nova = await fixture.AddAgentAsync("Nova", ct);
        Room room = await fixture.CreateRoomAsync([coach, nova], ct);
        PendingElicitation first = fixture.AddCard(room.Id, coach);
        _ = fixture.AddCard(room.Id, nova);
        await using MudBunitContext ctx = new();
        Register(ctx, fixture);
        IRenderedComponent<ContainerFragment> cut = RenderCard(ctx, room.Id);
        Assert.Equal(2, cut.FindAll(".elicitation-card").Count);

        Assert.True(await fixture.Service.DeclineAsync(room.Id, first.Id));

        cut.WaitForAssertion(() => Assert.Equal(["Nova is asking"], [.. cut.FindAll(".elicitation-card-title").Select(title => title.TextContent.Trim())]), BoundedWait);
    }

    /// <summary>E-5: a Message the Human types drops every card of the Room, and the cards leave the screen on the change event.</summary>
    [Fact]
    public async Task TypedHumanMessage_DropsTheCards_AndTheyVanish()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using ElicitationFixture fixture = await ElicitationFixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        User nova = await fixture.AddAgentAsync("Nova", ct);
        Room room = await fixture.CreateRoomAsync([coach, nova], ct);
        PendingElicitation first = fixture.AddCard(room.Id, coach);
        _ = fixture.AddCard(room.Id, nova);
        await using MudBunitContext ctx = new();
        Register(ctx, fixture);
        IRenderedComponent<ContainerFragment> cut = RenderCard(ctx, room.Id);
        Assert.Equal(2, cut.FindAll(".elicitation-card").Count);

        _ = await fixture.Chat.PostAsync(room.Id, KnownIds.Human, "never mind, do it your way", ct: ct);

        Assert.IsType<ElicitationCancelled>(await first.Completion.Task.WaitAsync(BoundedWait, ct));
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".elicitation-card")), BoundedWait);
    }

    /// <summary>A card that arrives while the Room is open appears on the change event, after the cards already there.</summary>
    [Fact]
    public async Task CardArrivingLater_AppearsAfterTheCardsAlreadyShown()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using ElicitationFixture fixture = await ElicitationFixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        User nova = await fixture.AddAgentAsync("Nova", ct);
        Room room = await fixture.CreateRoomAsync([coach, nova], ct);
        _ = fixture.AddCard(room.Id, coach);
        await using MudBunitContext ctx = new();
        Register(ctx, fixture);
        IRenderedComponent<ContainerFragment> cut = RenderCard(ctx, room.Id);
        Assert.Single(cut.FindAll(".elicitation-card"));

        _ = fixture.AddCard(room.Id, nova);

        cut.WaitForAssertion(() => Assert.Equal(["Coach is asking", "Nova is asking"], [.. cut.FindAll(".elicitation-card-title").Select(title => title.TextContent.Trim())]), BoundedWait);
    }

    /// <summary>
    /// While an answer is in flight the card shows its busy view - Send reads "Sending…" and every
    /// control is disabled - and it stays on screen even though taking the card raised the change event;
    /// once the answer is posted the card goes.
    /// </summary>
    [Fact]
    public async Task AnswerInFlight_ShowsTheBusyView_ThenTheCardGoes()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        TaskCompletionSource gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using ElicitationFixture fixture = await ElicitationFixture.CreateAsync(ct, inner => new GatedStore(inner, gate.Task));
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);
        PendingElicitation card = fixture.Elicitations.Add(room.Id, coach.Id, coach.Name, FormOf(Schema("\"note\":{\"type\":\"string\",\"title\":\"Note\"}")));
        await using MudBunitContext ctx = new();
        Register(ctx, fixture);
        IRenderedComponent<ContainerFragment> cut = RenderCard(ctx, room.Id);
        await TypeAsync(cut, 0, "Note", "on its way");

        try
        {
            Task click = cut.InvokeAsync(() => SendButton(cut, 0).ClickAsync());

            cut.WaitForAssertion(
                () =>
                {
                    Assert.Equal("Sending…", SendButton(cut, 0).TextContent.Trim());
                    Assert.True(SendButton(cut, 0).HasAttribute("disabled"));
                    Assert.True(SkipButton(cut, 0).HasAttribute("disabled"));
                    Assert.True(FieldControl(cut, 0, "Note").QuerySelector("input")?.HasAttribute("disabled"));
                },
                BoundedWait);
            Assert.Single(cut.FindAll(".elicitation-card"));

            gate.SetResult();
            await click.WaitAsync(BoundedWait, ct);
        }
        finally
        {
            _ = gate.TrySetResult();
        }

        Assert.Equal(["note=String:on its way"], await AcceptedAsync(card, ct));
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".elicitation-card")), BoundedWait);
    }

    /// <summary>rules.md: the card unsubscribes from the singleton hub when it is disposed, and a late event afterwards throws nothing.</summary>
    [Fact]
    public async Task Dispose_Unsubscribes_AndALateEventIsHarmless()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using ElicitationFixture fixture = await ElicitationFixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);
        _ = fixture.AddCard(room.Id, coach);
        await using MudBunitContext ctx = new();
        Register(ctx, fixture);
        IRenderedComponent<ContainerFragment> cut = RenderCard(ctx, room.Id);
        Assert.Single(cut.FindAll(".elicitation-card"));
        Assert.Equal(1, SubscriberCount(fixture.Events, nameof(RoomEvents.ElicitationsChanged)));

        await ctx.DisposeComponentsAsync();

        Assert.Equal(0, SubscriberCount(fixture.Events, nameof(RoomEvents.ElicitationsChanged)));
        Assert.Null(Record.Exception(() => fixture.Events.PublishElicitationsChanged(room.Id)));
    }

    /// <summary>
    /// E-6, AskUserQuestion: typing in a question's "Other" field clears that question's selection and
    /// disables its options (custom beats selection, as the Adapter does); only the custom key goes on the wire.
    /// </summary>
    [Fact]
    public async Task OtherTyped_ClearsAndDisablesTheSelection_AndOnlyTheCustomKeyIsSent()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using ElicitationFixture fixture = await ElicitationFixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);
        PendingElicitation card = fixture.AddCard(room.Id, coach);
        await using MudBunitContext ctx = new();
        Register(ctx, fixture);
        IRenderedComponent<ContainerFragment> cut = RenderCard(ctx, room.Id);
        await ToggleAsync(cut, 0, "label.mud-radio", "Postgres");
        Assert.Equal([true, false], [.. OptionLabels(cut, 0, "label.mud-radio").Select(IsChecked)]);

        await TypeAsync(cut, 0, "Other", "  Mongo  ");

        Assert.Equal([false, false], [.. OptionLabels(cut, 0, "label.mud-radio").Select(IsChecked)]);
        Assert.Equal([true, true], [.. OptionLabels(cut, 0, "label.mud-radio").Select(IsDisabled)]);
        await SendAsync(cut, 0);

        Assert.Equal(["question_0_custom=String:Mongo"], await AcceptedAsync(card, ct));
        ChatMessage posted = Assert.Single(await fixture.Transcript.ReadAllAsync(room.Id, ct));
        Assert.Equal("> Which database?\n\nMongo", posted.Text);
    }

    /// <summary>Typing in a multi select's "Other" field clears its ticked options too.</summary>
    [Fact]
    public async Task OtherTyped_ClearsAMultiSelectsTickedOptions()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using ElicitationFixture fixture = await ElicitationFixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);
        PendingElicitation card = fixture.Elicitations.Add(room.Id, coach.Id, coach.Name, FormOf(TwoQuestionSchema, TwoQuestionMessage));
        await using MudBunitContext ctx = new();
        Register(ctx, fixture);
        IRenderedComponent<ContainerFragment> cut = RenderCard(ctx, room.Id);
        await ToggleAsync(cut, 0, "label.mud-radio", "SQLite");
        await ToggleAsync(cut, 0, "label.mud-checkbox", "Auth");
        await ToggleAsync(cut, 0, "label.mud-checkbox", "Search");
        Assert.Equal([true, false, true], [.. OptionLabels(cut, 0, "label.mud-checkbox").Select(IsChecked)]);
        Assert.Equal(["Other", "Other"], [.. Card(cut, 0).QuerySelectorAll("label.mud-input-label").Select(label => label.TextContent.Trim())]);

        await TypeAtAsync(cut, 0, 1, "Anything else");

        Assert.Equal([false, false, false], [.. OptionLabels(cut, 0, "label.mud-checkbox").Select(IsChecked)]);
        Assert.Equal([true, true, true], [.. OptionLabels(cut, 0, "label.mud-checkbox").Select(IsDisabled)]);
        Assert.Equal([false, true], [.. OptionLabels(cut, 0, "label.mud-radio").Select(IsChecked)]);
        await SendAsync(cut, 0);

        Assert.Equal(["question_0=String:SQLite", "question_1_custom=String:Anything else"], await AcceptedAsync(card, ct));
    }

    /// <summary>Emptying the "Other" field gives the options back, enabled and unselected.</summary>
    [Fact]
    public async Task OtherCleared_EnablesTheOptionsAgain()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using ElicitationFixture fixture = await ElicitationFixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);
        _ = fixture.AddCard(room.Id, coach);
        await using MudBunitContext ctx = new();
        Register(ctx, fixture);
        IRenderedComponent<ContainerFragment> cut = RenderCard(ctx, room.Id);
        await TypeAsync(cut, 0, "Other", "Mongo");
        Assert.Equal([true, true], [.. OptionLabels(cut, 0, "label.mud-radio").Select(IsDisabled)]);

        await TypeAsync(cut, 0, "Other", string.Empty);

        Assert.Equal([false, false], [.. OptionLabels(cut, 0, "label.mud-radio").Select(IsDisabled)]);
        Assert.Equal([false, false], [.. OptionLabels(cut, 0, "label.mud-radio").Select(IsChecked)]);
        Assert.True(SendButton(cut, 0).HasAttribute("disabled"));
    }

    /// <summary>
    /// A one-question form whose message is the question (what the Adapter sends) shows that sentence
    /// once, as the question's label, not again as a message line above it; a message that differs only
    /// by surrounding whitespace is the same sentence.
    /// </summary>
    /// <param name="schema">The form's JSON Schema text.</param>
    /// <param name="message">The request's message.</param>
    [Theory]
    [InlineData(SingleQuestionSchema, "Which database?")]
    [InlineData(SingleQuestionSchema, "  Which database?\n")]
    [InlineData("""{"type":"object","properties":{"question_0":{"type":"string","description":" Which database? ","oneOf":[{"const":"A","title":"A"}]}}}""", "Which database?")]
    public async Task OneQuestionForm_MessageRepeatingTheQuestion_ShowsItOnce(string schema, string message)
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using ElicitationFixture fixture = await ElicitationFixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);
        _ = fixture.Elicitations.Add(room.Id, coach.Id, coach.Name, FormOf(schema, message));
        await using MudBunitContext ctx = new();
        Register(ctx, fixture);

        IRenderedComponent<ContainerFragment> cut = RenderCard(ctx, room.Id);

        Assert.Empty(cut.FindAll(".elicitation-card-message"));
        Assert.Equal(["Which database?"], [.. cut.FindAll(".elicitation-card-field-label").Select(label => label.TextContent.Trim())]);
        Assert.Equal(1, LeafTextCount(Card(cut, 0), "Which database?"));
    }

    /// <summary>A message that is not the question's sentence - another text, or the same words in another case - is still shown above it, beside the question.</summary>
    /// <param name="description">The question's own text.</param>
    /// <param name="message">The request's message.</param>
    [Theory]
    [InlineData("Which database?", "Please answer the following question.")]
    [InlineData("which database?", "Which database?")]
    public async Task OneQuestionForm_MessageDifferingFromTheQuestion_ShowsBoth(string description, string message)
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using ElicitationFixture fixture = await ElicitationFixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);
        string schema = Schema("\"question_0\":{\"type\":\"string\",\"description\":\"" + description + "\",\"oneOf\":[{\"const\":\"A\",\"title\":\"A\"}]}");
        _ = fixture.Elicitations.Add(room.Id, coach.Id, coach.Name, FormOf(schema, message));
        await using MudBunitContext ctx = new();
        Register(ctx, fixture);

        IRenderedComponent<ContainerFragment> cut = RenderCard(ctx, room.Id);

        Assert.Equal([message], [.. cut.FindAll(".elicitation-card-message").Select(line => line.TextContent.Trim())]);
        Assert.Equal([description], [.. cut.FindAll(".elicitation-card-field-label").Select(label => label.TextContent.Trim())]);
    }

    /// <summary>A form with two fields keeps its message even when it reads the same as one field's label: the message is the form's prompt, not that field's.</summary>
    [Fact]
    public async Task TwoFieldForm_MessageMatchingOneLabel_ShowsBoth()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using ElicitationFixture fixture = await ElicitationFixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);
        string schema = Schema("\"a\":{\"type\":\"string\",\"title\":\"Same\"},\"b\":{\"type\":\"string\",\"title\":\"Other thing\"}");
        _ = fixture.Elicitations.Add(room.Id, coach.Id, coach.Name, FormOf(schema, "Same"));
        await using MudBunitContext ctx = new();
        Register(ctx, fixture);

        IRenderedComponent<ContainerFragment> cut = RenderCard(ctx, room.Id);

        Assert.Equal(["Same"], [.. cut.FindAll(".elicitation-card-message").Select(line => line.TextContent.Trim())]);
        Assert.Equal(["Same", "Other thing"], [.. cut.FindAll("label.mud-input-label").Select(label => label.TextContent.Trim())]);
    }

    /// <summary>The message is dropped for any kind of lone field, not only a select: a text field titled as its message keeps its label and loses the message line.</summary>
    [Fact]
    public async Task OneTextField_MessageRepeatingItsTitle_ShowsItOnce()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using ElicitationFixture fixture = await ElicitationFixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);
        _ = fixture.Elicitations.Add(room.Id, coach.Id, coach.Name, FormOf(Schema("\"name\":{\"type\":\"string\",\"title\":\"Your name\"}"), "Your name"));
        await using MudBunitContext ctx = new();
        Register(ctx, fixture);

        IRenderedComponent<ContainerFragment> cut = RenderCard(ctx, room.Id);

        Assert.Empty(cut.FindAll(".elicitation-card-message"));
        Assert.Equal(["Your name"], [.. cut.FindAll("label.mud-input-label").Select(label => label.TextContent.Trim())]);
    }

    /// <summary>
    /// The refusal dialog's one field has only the key <c>choice</c> for a label: the card shows no
    /// heading for it - the message above is the prompt - and the key appears nowhere in the card, while
    /// the group's accessible name is the message.
    /// </summary>
    [Fact]
    public async Task RefusalDialog_ShowsNoKeyHeading_AndTheGroupIsNamedByTheMessage()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using ElicitationFixture fixture = await ElicitationFixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);
        _ = fixture.Elicitations.Add(room.Id, coach.Id, coach.Name, FormOf(RefusalSchema, RefusalMessage));
        await using MudBunitContext ctx = new();
        Register(ctx, fixture);

        IRenderedComponent<ContainerFragment> cut = RenderCard(ctx, room.Id);

        Assert.Equal([RefusalMessage], [.. cut.FindAll(".elicitation-card-message").Select(line => line.TextContent.Trim())]);
        Assert.Empty(cut.FindAll(".elicitation-card-field-label"));
        Assert.Empty(cut.FindAll(".elicitation-card-field-heading"));
        Assert.Equal(0, LeafTextCount(Card(cut, 0), "choice"));
        Assert.DoesNotContain("choice", Card(cut, 0).TextContent, StringComparison.Ordinal);
        Assert.Equal([RefusalMessage], [.. cut.FindAll(".elicitation-card-group[role=group]").Select(group => group.GetAttribute("aria-label") ?? string.Empty)]);
    }

    /// <summary>A message that happens to be the bare key is still the prompt: the label (only the key) is what is dropped, so the text is shown once, as the message.</summary>
    [Fact]
    public async Task RefusalShapedForm_MessageEqualToTheKey_StillShowsTheMessageOnce()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using ElicitationFixture fixture = await ElicitationFixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);
        _ = fixture.Elicitations.Add(room.Id, coach.Id, coach.Name, FormOf(RefusalSchema, "choice"));
        await using MudBunitContext ctx = new();
        Register(ctx, fixture);

        IRenderedComponent<ContainerFragment> cut = RenderCard(ctx, room.Id);

        Assert.Equal(["choice"], [.. cut.FindAll(".elicitation-card-message").Select(line => line.TextContent.Trim())]);
        Assert.Empty(cut.FindAll(".elicitation-card-field-label"));
        Assert.Equal(1, LeafTextCount(Card(cut, 0), "choice"));
    }

    /// <summary>A required key-labelled select still shows its required mark and says so to assistive technology, though it shows no label.</summary>
    [Fact]
    public async Task RefusalShapedForm_Required_KeepsItsMarkWithoutALabel()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using ElicitationFixture fixture = await ElicitationFixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);
        string schema = Schema("\"choice\":{\"type\":\"string\",\"oneOf\":[{\"const\":\"a\",\"title\":\"Alpha\"}]}", "[\"choice\"]");
        _ = fixture.Elicitations.Add(room.Id, coach.Id, coach.Name, FormOf(schema, RefusalMessage));
        await using MudBunitContext ctx = new();
        Register(ctx, fixture);

        IRenderedComponent<ContainerFragment> cut = RenderCard(ctx, room.Id);

        Assert.Empty(cut.FindAll(".elicitation-card-field-label"));
        Assert.Single(cut.FindAll(".elicitation-card-required"));
        Assert.Equal(["true"], [.. cut.FindAll(".elicitation-card-group[role=group]").Select(group => group.GetAttribute("aria-required") ?? string.Empty)]);
    }

    /// <summary>
    /// A lone key-labelled field of any other kind shows no label either, and its field is a group named
    /// by the message: the key is never shown to the Human.
    /// </summary>
    /// <param name="property">The one property of the form, as JSON.</param>
    [Theory]
    [InlineData("\"x\":{\"type\":\"string\"}")]
    [InlineData("\"x\":{\"type\":\"integer\"}")]
    [InlineData("\"x\":{\"type\":\"number\"}")]
    [InlineData("\"x\":{\"type\":\"string\",\"format\":\"date\"}")]
    [InlineData("\"x\":{\"type\":\"boolean\"}")]
    [InlineData("\"x\":{\"type\":\"array\",\"items\":{\"type\":\"string\",\"enum\":[\"a\",\"b\"]}}")]
    [InlineData("\"x\":{\"type\":\"string\",\"enum\":[\"a\",\"b\"]}")]
    public async Task OneKeyLabelledField_OfAnyKind_ShowsNoKey_AndIsNamedByTheMessage(string property)
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using ElicitationFixture fixture = await ElicitationFixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);
        _ = fixture.Elicitations.Add(room.Id, coach.Id, coach.Name, FormOf(Schema(property), "Tell me about it."));
        await using MudBunitContext ctx = new();
        Register(ctx, fixture);

        IRenderedComponent<ContainerFragment> cut = RenderCard(ctx, room.Id);

        Assert.Equal(["Tell me about it."], [.. cut.FindAll(".elicitation-card-message").Select(line => line.TextContent.Trim())]);
        Assert.Equal(0, LeafTextCount(Card(cut, 0), "x"));
        Assert.Empty(cut.FindAll(".elicitation-card-field-label"));
        Assert.Empty(cut.FindAll("label.mud-input-label"));
        Assert.Equal(["Tell me about it."], [.. cut.FindAll(".elicitation-card-field[role=group], .elicitation-card-group[role=group]").Select(group => group.GetAttribute("aria-label") ?? string.Empty)]);
    }

    /// <summary>With several fields an untitled one is still labelled by its key, as before: the message is the form's prompt, not any one field's name.</summary>
    [Fact]
    public async Task SeveralKeyLabelledFields_KeepTheirKeysAsLabels()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using ElicitationFixture fixture = await ElicitationFixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);
        string schema = Schema("\"alpha\":{\"type\":\"string\",\"oneOf\":[{\"const\":\"a\"}]},\"beta\":{\"type\":\"string\",\"oneOf\":[{\"const\":\"b\"}]},\"gamma\":{\"type\":\"string\"}");
        _ = fixture.Elicitations.Add(room.Id, coach.Id, coach.Name, FormOf(schema, "Fill these in."));
        await using MudBunitContext ctx = new();
        Register(ctx, fixture);

        IRenderedComponent<ContainerFragment> cut = RenderCard(ctx, room.Id);

        Assert.Equal(["alpha", "beta"], [.. cut.FindAll(".elicitation-card-field-label").Select(label => label.TextContent.Trim())]);
        Assert.Equal(["alpha", "beta"], [.. cut.FindAll(".elicitation-card-group[role=group]").Select(group => group.GetAttribute("aria-label") ?? string.Empty)]);
        Assert.Equal(["gamma"], [.. cut.FindAll("label.mud-input-label").Select(label => label.TextContent.Trim())]);
    }

    /// <summary>A lone field with a description but no title keeps its key as its heading: the description is its own text, so the field is not the bare key the dialog case is.</summary>
    [Fact]
    public async Task OneDescribedUntitledSelect_KeepsItsKeyHeading()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using ElicitationFixture fixture = await ElicitationFixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);
        string schema = Schema("\"mode\":{\"type\":\"string\",\"description\":\"How to proceed\",\"oneOf\":[{\"const\":\"a\",\"title\":\"Alpha\"}]}");
        _ = fixture.Elicitations.Add(room.Id, coach.Id, coach.Name, FormOf(schema, "Which way?"));
        await using MudBunitContext ctx = new();
        Register(ctx, fixture);

        IRenderedComponent<ContainerFragment> cut = RenderCard(ctx, room.Id);

        Assert.Equal(["mode"], [.. cut.FindAll(".elicitation-card-field-label").Select(label => label.TextContent.Trim())]);
        Assert.Equal(["How to proceed"], [.. cut.FindAll(".elicitation-card-field-description").Select(text => text.TextContent.Trim())]);
        Assert.Equal(["mode"], [.. cut.FindAll(".elicitation-card-group[role=group]").Select(group => group.GetAttribute("aria-label") ?? string.Empty)]);
    }

    /// <summary>A bounded number field says its allowed range under the input, in plain words, so the Human knows what Send waits for.</summary>
    /// <param name="type">The field's JSON type.</param>
    /// <param name="bounds">The bound keywords, as JSON members after the type.</param>
    /// <param name="expected">The helper text under the input.</param>
    [Theory]
    [InlineData("integer", ",\"minimum\":1", "At least 1.")]
    [InlineData("integer", ",\"maximum\":20", "At most 20.")]
    [InlineData("integer", ",\"minimum\":1,\"maximum\":20", "Between 1 and 20.")]
    [InlineData("number", ",\"minimum\":0.5", "At least 0.5.")]
    [InlineData("number", ",\"maximum\":9.5", "At most 9.5.")]
    [InlineData("number", ",\"minimum\":0.5,\"maximum\":9.5", "Between 0.5 and 9.5.")]
    public async Task BoundedNumberField_ShowsItsRangeAsHelperText(string type, string bounds, string expected)
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using ElicitationFixture fixture = await ElicitationFixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);
        _ = fixture.Elicitations.Add(room.Id, coach.Id, coach.Name, FormOf(Schema("\"count\":{\"type\":\"" + type + "\",\"title\":\"Count\"" + bounds + "}")));
        await using MudBunitContext ctx = new();
        Register(ctx, fixture);

        IRenderedComponent<ContainerFragment> cut = RenderCard(ctx, room.Id);

        Assert.Equal(expected, FieldControl(cut, 0, "Count").QuerySelector(".mud-input-helper-text")?.TextContent.Trim());
    }

    /// <summary>A field with no declared bounds shows no range: not an unbounded number, not a text with length limits, not a boolean.</summary>
    /// <param name="property">The one property of the form, as JSON.</param>
    [Theory]
    [InlineData("\"count\":{\"type\":\"integer\",\"title\":\"Count\"}")]
    [InlineData("\"count\":{\"type\":\"number\",\"title\":\"Count\"}")]
    [InlineData("\"count\":{\"type\":\"string\",\"title\":\"Count\",\"minLength\":1,\"maxLength\":20}")]
    public async Task FieldWithoutBounds_ShowsNoRange(string property)
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using ElicitationFixture fixture = await ElicitationFixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);
        _ = fixture.Elicitations.Add(room.Id, coach.Id, coach.Name, FormOf(Schema(property)));
        await using MudBunitContext ctx = new();
        Register(ctx, fixture);

        IRenderedComponent<ContainerFragment> cut = RenderCard(ctx, room.Id);

        Assert.Empty(FieldControl(cut, 0, "Count").QuerySelectorAll(".mud-input-helper-text"));
    }

    /// <summary>A bounded number field's own description stays visible, on its own line above the range.</summary>
    [Fact]
    public async Task BoundedNumberField_WithADescription_ShowsTheDescriptionThenTheRange()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using ElicitationFixture fixture = await ElicitationFixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);
        string schema = Schema("\"count\":{\"type\":\"integer\",\"title\":\"Count\",\"description\":\"How many seats?\",\"minimum\":1,\"maximum\":20}");
        _ = fixture.Elicitations.Add(room.Id, coach.Id, coach.Name, FormOf(schema));
        await using MudBunitContext ctx = new();
        Register(ctx, fixture);

        IRenderedComponent<ContainerFragment> cut = RenderCard(ctx, room.Id);

        Assert.Equal("How many seats?\nBetween 1 and 20.", FieldControl(cut, 0, "Count").QuerySelector(".mud-input-helper-text")?.TextContent.Trim());
    }

    /// <summary>
    /// A bounded number typed outside its range puts the field into its error state with the composer's
    /// own reason as the error text, and Send stays disabled; a value inside the range, or nothing typed,
    /// is no error and the range helper shows again.
    /// </summary>
    /// <param name="typed">What the Human typed.</param>
    /// <param name="expectedText">The text under the input.</param>
    /// <param name="expectedError">Whether the field is in its error state.</param>
    [Theory]
    [InlineData("21", "Field 'count' must be at most 20.", true)]
    [InlineData("0", "Field 'count' must be at least 1.", true)]
    [InlineData("abc", "Field 'count' must be a whole number.", true)]
    [InlineData("2.5", "Field 'count' must be a whole number.", true)]
    [InlineData("1", "Between 1 and 20.", false)]
    [InlineData("20", "Between 1 and 20.", false)]
    [InlineData("", "Between 1 and 20.", false)]
    [InlineData("   ", "Between 1 and 20.", false)]
    public async Task BoundedIntegerField_TypedValue_ShowsTheProblemOnlyWhenItFailsTheCheck(string typed, string expectedText, bool expectedError)
    {
        ArgumentNullException.ThrowIfNull(typed);
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using ElicitationFixture fixture = await ElicitationFixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);
        _ = fixture.Elicitations.Add(room.Id, coach.Id, coach.Name, FormOf(Schema("\"count\":{\"type\":\"integer\",\"title\":\"Count\",\"minimum\":1,\"maximum\":20}")));
        await using MudBunitContext ctx = new();
        Register(ctx, fixture);
        IRenderedComponent<ContainerFragment> cut = RenderCard(ctx, room.Id);

        await TypeAsync(cut, 0, "Count", typed);

        IElement control = FieldControl(cut, 0, "Count");
        Assert.Equal(expectedText, control.QuerySelector(".mud-input-helper-text")?.TextContent.Trim());
        Assert.Equal(expectedError, control.QuerySelector("input")?.GetAttribute("aria-invalid") == "true");
        Assert.Equal(expectedError, control.QuerySelector(".mud-input-helper-text")?.ClassList.Contains("mud-input-error"));
        Assert.Equal(!expectedError && typed.Trim().Length > 0, !SendButton(cut, 0).HasAttribute("disabled"));
    }

    /// <summary>A bounded number field that is fixed shows the range again once the value is back inside it.</summary>
    [Fact]
    public async Task BoundedNumberField_CorrectedValue_LeavesTheErrorState()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using ElicitationFixture fixture = await ElicitationFixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);
        _ = fixture.Elicitations.Add(room.Id, coach.Id, coach.Name, FormOf(Schema("\"ratio\":{\"type\":\"number\",\"title\":\"Ratio\",\"minimum\":0.5,\"maximum\":9.5}")));
        await using MudBunitContext ctx = new();
        Register(ctx, fixture);
        IRenderedComponent<ContainerFragment> cut = RenderCard(ctx, room.Id);
        await TypeAsync(cut, 0, "Ratio", "9.6");
        Assert.Equal("Field 'ratio' must be at most 9.5.", FieldControl(cut, 0, "Ratio").QuerySelector(".mud-input-helper-text")?.TextContent.Trim());

        await TypeAsync(cut, 0, "Ratio", "9.5");

        IElement control = FieldControl(cut, 0, "Ratio");
        Assert.Equal("Between 0.5 and 9.5.", control.QuerySelector(".mud-input-helper-text")?.TextContent.Trim());
        Assert.NotEqual("true", control.QuerySelector("input")?.GetAttribute("aria-invalid"));
    }

    /// <summary>A text field never shows the number error state, whatever the Human typed: only number and integer fields do.</summary>
    [Fact]
    public async Task TextField_TooShort_IsNotInTheErrorState()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using ElicitationFixture fixture = await ElicitationFixture.CreateAsync(ct);
        User coach = await fixture.AddAgentAsync("Coach", ct);
        Room room = await fixture.CreateRoomAsync([coach], ct);
        _ = fixture.Elicitations.Add(room.Id, coach.Id, coach.Name, FormOf(Schema("\"code\":{\"type\":\"string\",\"title\":\"Code\",\"minLength\":5}")));
        await using MudBunitContext ctx = new();
        Register(ctx, fixture);
        IRenderedComponent<ContainerFragment> cut = RenderCard(ctx, room.Id);

        await TypeAsync(cut, 0, "Code", "ab");

        IElement control = FieldControl(cut, 0, "Code");
        Assert.NotEqual("true", control.QuerySelector("input")?.GetAttribute("aria-invalid"));
        Assert.Empty(control.QuerySelectorAll(".mud-input-helper-text"));
        Assert.True(SendButton(cut, 0).HasAttribute("disabled"));
    }

    /// <summary>How many elements under <paramref name="scope"/> have no child element and read exactly <paramref name="text"/>: the number of times a sentence is shown.</summary>
    /// <param name="scope">The element to look inside.</param>
    /// <param name="text">The text to count.</param>
    private static int LeafTextCount(IElement scope, string text)
    {
        return scope.QuerySelectorAll("*").Count(element => element.ChildElementCount == 0 && string.Equals(element.TextContent.Trim(), text, StringComparison.Ordinal));
    }

    /// <summary>Registers every real collaborator <c>ElicitationCard</c> injects into <paramref name="ctx"/>'s container; it needs no Drafts, because it never waits for the asker to finish writing.</summary>
    /// <param name="ctx">The bUnit context to register into.</param>
    /// <param name="fixture">The collaborators to register.</param>
    private static void Register(MudBunitContext ctx, ElicitationFixture fixture)
    {
        ctx.Services.AddSingleton<ITeamDirectory>(fixture.Directory);
        ctx.Services.AddSingleton(fixture.Events);
        ctx.Services.AddSingleton(fixture.Elicitations);
        ctx.Services.AddSingleton(fixture.Service);
    }

    /// <summary>Renders the card for a Room beside the popover and dialog providers, which a date picker needs.</summary>
    /// <param name="ctx">The context to render into.</param>
    /// <param name="roomId">The Room id passed as the card's <c>RoomId</c>.</param>
    private static IRenderedComponent<ContainerFragment> RenderCard(MudBunitContext ctx, string roomId)
    {
        return ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<Agency.Huddle.App.Components.Shared.ElicitationCard>(0);
            builder.AddAttribute(1, nameof(Agency.Huddle.App.Components.Shared.ElicitationCard.RoomId), roomId);
            builder.CloseComponent();
        });
    }

    /// <summary>The <paramref name="index"/>-th rendered card, in arrival order.</summary>
    /// <param name="cut">The rendered cards.</param>
    /// <param name="index">Which card, from zero.</param>
    private static IElement Card(IRenderedComponent<ContainerFragment> cut, int index)
    {
        return cut.FindAll(".elicitation-card")[index];
    }

    /// <summary>A card's Send button.</summary>
    /// <param name="cut">The rendered cards.</param>
    /// <param name="card">Which card, from zero.</param>
    private static IElement SendButton(IRenderedComponent<ContainerFragment> cut, int card)
    {
        return Card(cut, card).QuerySelector("button.elicitation-card-send") ?? throw new InvalidOperationException("The card has no Send button.");
    }

    /// <summary>A card's Skip button.</summary>
    /// <param name="cut">The rendered cards.</param>
    /// <param name="card">Which card, from zero.</param>
    private static IElement SkipButton(IRenderedComponent<ContainerFragment> cut, int card)
    {
        return Card(cut, card).QuerySelector("button.elicitation-card-skip") ?? throw new InvalidOperationException("The card has no Skip button.");
    }

    /// <summary>The input control of the card's text-like field whose label reads <paramref name="label"/>.</summary>
    /// <param name="cut">The rendered cards.</param>
    /// <param name="card">Which card, from zero.</param>
    /// <param name="label">The field's label.</param>
    private static IElement FieldControl(IRenderedComponent<ContainerFragment> cut, int card, string label)
    {
        return Card(cut, card).QuerySelectorAll("div.mud-input-control")
            .First(control => string.Equals(control.QuerySelector("label")?.TextContent.Trim(), label, StringComparison.Ordinal));
    }

    /// <summary>Types <paramref name="text"/> into the card's field labelled <paramref name="label"/>, as an input event.</summary>
    /// <param name="cut">The rendered cards.</param>
    /// <param name="card">Which card, from zero.</param>
    /// <param name="label">The field's label.</param>
    /// <param name="text">The text typed.</param>
    private static async Task TypeAsync(IRenderedComponent<ContainerFragment> cut, int card, string label, string text)
    {
        await cut.InvokeAsync(() => (FieldControl(cut, card, label).QuerySelector("input") ?? throw new InvalidOperationException("The field has no input.")).InputAsync(new ChangeEventArgs { Value = text }));
    }

    /// <summary>Types <paramref name="text"/> into the <paramref name="position"/>-th text input of the card.</summary>
    /// <param name="cut">The rendered cards.</param>
    /// <param name="card">Which card, from zero.</param>
    /// <param name="position">Which text input of the card, from zero, in page order.</param>
    /// <param name="text">The text typed.</param>
    private static async Task TypeAtAsync(IRenderedComponent<ContainerFragment> cut, int card, int position, string text)
    {
        await cut.InvokeAsync(() => Card(cut, card).QuerySelectorAll("input.mud-input-root")[position].InputAsync(new ChangeEventArgs { Value = text }));
    }

    /// <summary>Commits <paramref name="text"/> into the card's field labelled <paramref name="label"/>, as a change event (what a date picker waits for).</summary>
    /// <param name="cut">The rendered cards.</param>
    /// <param name="card">Which card, from zero.</param>
    /// <param name="label">The field's label.</param>
    /// <param name="text">The text committed.</param>
    private static async Task ChangeAsync(IRenderedComponent<ContainerFragment> cut, int card, string label, string text)
    {
        await cut.InvokeAsync(() => (FieldControl(cut, card, label).QuerySelector("input") ?? throw new InvalidOperationException("The field has no input.")).ChangeAsync(new ChangeEventArgs { Value = text }));
    }

    /// <summary>The option labels of one control kind (<c>label.mud-radio</c>, <c>label.mud-checkbox</c>, <c>label.mud-switch</c>) inside a card, in page order.</summary>
    /// <param name="cut">The rendered cards.</param>
    /// <param name="card">Which card, from zero.</param>
    /// <param name="selector">The control's label selector.</param>
    private static List<IElement> OptionLabels(IRenderedComponent<ContainerFragment> cut, int card, string selector)
    {
        return [.. Card(cut, card).QuerySelectorAll(selector)];
    }

    /// <summary>Clicks the option or switch of the card whose visible label is <paramref name="text"/>.</summary>
    /// <param name="cut">The rendered cards.</param>
    /// <param name="card">Which card, from zero.</param>
    /// <param name="selector">The control's label selector.</param>
    /// <param name="text">The option's own title (its label, not its description).</param>
    private static async Task ToggleAsync(IRenderedComponent<ContainerFragment> cut, int card, string selector, string text)
    {
        await cut.InvokeAsync(() =>
        {
            IElement option = OptionLabels(cut, card, selector).First(label => string.Equals(OptionText(label), text, StringComparison.Ordinal));
            IElement input = option.QuerySelector("input") ?? throw new InvalidOperationException("The option has no input.");

            // MudBlazor binds a radio to its input's click and a checkbox or switch to its input's change.
            return selector.Contains("radio", StringComparison.Ordinal)
                ? input.ClickAsync()
                : input.ChangeAsync(new ChangeEventArgs { Value = !IsChecked(option) });
        });
    }

    /// <summary>The visible title of an option or switch label: its own text hook, or the whole label when there is none.</summary>
    /// <param name="label">The rendered <c>label</c> element.</param>
    private static string OptionText(IElement label)
    {
        return (label.QuerySelector(".elicitation-card-option-label") ?? label.QuerySelector(".elicitation-card-field-label") ?? label).TextContent.Trim();
    }

    /// <summary>Whether the input inside an option label is checked.</summary>
    /// <param name="label">The rendered <c>label</c> element.</param>
    private static bool IsChecked(IElement label)
    {
        return label.QuerySelector("input")?.HasAttribute("checked") ?? false;
    }

    /// <summary>Whether the input inside an option label is disabled.</summary>
    /// <param name="label">The rendered <c>label</c> element.</param>
    private static bool IsDisabled(IElement label)
    {
        return label.QuerySelector("input")?.HasAttribute("disabled") ?? false;
    }

    /// <summary>Clicks a card's Send button, awaiting the handler.</summary>
    /// <param name="cut">The rendered cards.</param>
    /// <param name="card">Which card, from zero.</param>
    private static async Task SendAsync(IRenderedComponent<ContainerFragment> cut, int card)
    {
        await cut.InvokeAsync(() => SendButton(cut, card).ClickAsync());
    }

    /// <summary>Waits for the request behind <paramref name="card"/> to be answered and spells what was accepted.</summary>
    /// <param name="card">The card whose request is waiting.</param>
    /// <param name="ct">Cancels the wait.</param>
    private static async Task<string[]> AcceptedAsync(PendingElicitation card, CancellationToken ct)
    {
        ElicitationResult result = await card.Completion.Task.WaitAsync(BoundedWait, ct);
        ElicitationAccepted accepted = Assert.IsType<ElicitationAccepted>(result);
        return Wire(accepted.Content);
    }

    /// <summary>Spells wire content as sorted <c>key=Type:value</c> lines, so one assertion shows the keys, the CLR types and the values.</summary>
    /// <param name="content">The accepted content.</param>
    private static string[] Wire(IReadOnlyDictionary<string, object> content)
    {
        return [.. content
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => string.Create(
                CultureInfo.InvariantCulture,
                $"{pair.Key}={pair.Value.GetType().Name}:{(pair.Value is string[] list ? string.Join(",", list) : pair.Value)}"))];
    }

    /// <summary>How many handlers are subscribed to <paramref name="eventName"/> on <paramref name="events"/>, read from the event's backing field.</summary>
    /// <param name="events">The hub to inspect.</param>
    /// <param name="eventName">The event's name; its backing field has the same name.</param>
    private static int SubscriberCount(RoomEvents events, string eventName)
    {
        System.Reflection.FieldInfo? field = typeof(RoomEvents).GetField(eventName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(field);
        return (field.GetValue(events) as Delegate)?.GetInvocationList().Length ?? 0;
    }

    /// <summary>A Transcript store whose append waits for a gate, so a test can hold an answer in flight, and forwards everything else.</summary>
    /// <param name="inner">The real store.</param>
    /// <param name="gate">Completed when the append may go on.</param>
    private sealed class GatedStore(IChatStore inner, Task gate) : IChatStore
    {
        /// <inheritdoc/>
        public async Task AppendAsync(string roomId, ChatMessage message, CancellationToken ct = default)
        {
            await gate.WaitAsync(ct);
            await inner.AppendAsync(roomId, message, ct);
        }

        /// <inheritdoc/>
        public Task<IReadOnlyList<ChatMessage>> ReadAllAsync(string roomId, CancellationToken ct = default)
        {
            return inner.ReadAllAsync(roomId, ct);
        }

        /// <inheritdoc/>
        public Task DeleteAsync(string roomId, CancellationToken ct = default)
        {
            return inner.DeleteAsync(roomId, ct);
        }

        /// <inheritdoc/>
        public Task<bool> HasMessagesAsync(string roomId, CancellationToken ct = default)
        {
            return inner.HasMessagesAsync(roomId, ct);
        }
    }
}
