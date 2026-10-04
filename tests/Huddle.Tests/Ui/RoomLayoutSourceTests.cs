namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Source-text guards for the two layout defects that only showed on screen when an Agent's form was put
/// to the Human (the elicitation bridge): a Room whose Transcript was taller than the window could not
/// be scrolled at all, and a form taller than the window pushed Send and the composer out of reach.
/// Nothing in this suite renders a browser, so each property is pinned in the stylesheet or the markup
/// that carries it, the same kind of assertion <see cref="AppStylesheetTests"/> and
/// <c>ThemeSourceTests</c> make; the real check was looking at it in the browser pane.
/// </summary>
public sealed class RoomLayoutSourceTests
{
    /// <summary>
    /// The Room's own column may shrink below its content. A flex item's <c>min-height</c> is <c>auto</c>
    /// by default, which refuses to go under the content's height; without <c>min-height: 0</c> the column
    /// grew past the window, <c>.main-column</c> clipped its top, and <c>.message-list</c> never got a
    /// bounded height to scroll in, so the header and every earlier Message were unreachable.
    /// </summary>
    [Fact]
    public void AppCss_RoomColumn_MayShrinkSoTheMessageListScrolls()
    {
        string body = RuleBody(ReadAppCss(), ".room-column");

        Assert.Contains("min-height: 0;", body, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
    }

    /// <summary>
    /// <c>Chat.razor</c> puts the shrink class on the column that holds the header, the message list, the
    /// cards and the composer. The rule above is inert unless the element wears it.
    /// </summary>
    [Fact]
    public void ChatPage_TheRoomColumnWearsTheShrinkClass()
    {
        string markup = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "Components", "Pages", "Chat.razor"));

        Assert.Contains("<MudStack Class=\"h-100 room-column\" Spacing=\"0\">", markup, StringComparison.Ordinal); // contains-ok: Razor source text, not rendered markup
    }

    /// <summary>
    /// The Elicitation card is capped at half the window, so a long form cannot push Send, Skip and the
    /// composer off the page.
    /// </summary>
    [Fact]
    public void AppCss_ElicitationCard_IsCappedAtHalfTheWindow()
    {
        string body = RuleBody(ReadAppCss(), ".elicitation-card");

        Assert.Contains("max-height: 50vh;", body, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.Contains("display: flex;", body, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.Contains("flex-direction: column;", body, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
    }

    /// <summary>
    /// The fields scroll inside the capped card while the actions stay in view. <c>min-height: 0</c> is
    /// what lets the flex child shrink below its content and scroll instead of overflowing the card.
    /// </summary>
    [Fact]
    public void AppCss_ElicitationCard_FieldsScrollAndActionsStayPut()
    {
        string css = ReadAppCss();
        string fields = RuleBody(css, ".elicitation-card-fields");
        string actions = RuleBody(css, ".elicitation-card-actions");

        Assert.Contains("overflow-y: auto;", fields, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.Contains("min-height: 0;", fields, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.Contains("flex: 0 0 auto;", actions, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
    }

    /// <summary>
    /// The card has no scoped stylesheet. Its root is a <c>MudPaper</c> and its controls are Mud
    /// components, which Blazor's CSS isolation never stamps its attribute onto, so every
    /// <c>::deep</c> rule there compiled to a selector with no scoped ancestor and silently never
    /// applied. Rules for a card of Mud components live in <c>app.css</c> (see MudBlazorImplementation.md).
    /// </summary>
    [Fact]
    public void ElicitationCard_HasNoScopedStylesheetThatCouldNeverMatch()
    {
        string scoped = CssSource.RepoPath("src", "Huddle.App", "Components", "Shared", "ElicitationCard.razor.css");

        Assert.False(File.Exists(scoped), "ElicitationCard.razor.css must not exist: ::deep cannot reach a MudPaper root, so its rules never apply. Put them in app.css.");
    }

    /// <summary>
    /// A Question card's option buttons are at least 44 pixels tall (Questions spec §6.7, so the card
    /// works at phone width) and keep the case the Agent wrote. The rule is global because the buttons
    /// are <c>MudButton</c>s, which Blazor's CSS isolation never stamps its attribute onto.
    /// </summary>
    [Fact]
    public void AppCss_QuestionCardOption_IsTouchSizedAndKeepsItsCase()
    {
        string body = RuleBody(ReadAppCss(), ".question-card-option");

        Assert.Contains("min-height: 44px;", body, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.Contains("text-transform: none;", body, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
    }

    /// <summary>A ranking row, whose arrow buttons are <c>MudIconButton</c>s, is at least 44 pixels tall.</summary>
    [Fact]
    public void AppCss_QuestionCardRankRow_IsTouchSized()
    {
        string body = RuleBody(ReadAppCss(), ".question-card-rank-row");

        Assert.Contains("min-height: 44px;", body, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
    }

    /// <summary>
    /// The Send row sits below the last Question. Its element is a <c>MudStack</c> that is a direct child
    /// of the <c>MudPaper</c> root, so a scoped <c>::deep</c> rule had no scoped ancestor to hang from
    /// and its margin never applied.
    /// </summary>
    [Fact]
    public void AppCss_QuestionCardActions_SitBelowTheQuestions()
    {
        string body = RuleBody(ReadAppCss(), ".question-card-actions");

        Assert.Contains("margin-top: 0.75rem;", body, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
    }

    /// <summary>
    /// The two rules that styled plain elements keep their effect after the move: each Question is set
    /// off from the one above, and a ranking row's label takes the space the arrows leave.
    /// </summary>
    [Fact]
    public void AppCss_QuestionCardPlainElements_KeepTheirSpacingAndFlex()
    {
        string css = ReadAppCss();

        Assert.Contains("margin-top: 0.75rem;", RuleBody(css, ".question-card-question"), StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.Contains("flex: 1 1 auto;", RuleBody(css, ".question-card-rank-label"), StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
    }

    /// <summary>
    /// The Question card has no scoped stylesheet. Its root is a <c>MudPaper</c> and its controls are Mud
    /// components, so a <c>::deep</c> rule there only matched what happened to sit inside the plain
    /// <c>div</c> wrapping each Question - the Send row, outside it, never matched - and reshaping that
    /// div would have broken the rest without a test failing. Rules for a card of Mud components live in
    /// <c>app.css</c> (see MudBlazorImplementation.md).
    /// </summary>
    [Fact]
    public void QuestionCard_HasNoScopedStylesheetThatCouldNeverMatch()
    {
        string scoped = CssSource.RepoPath("src", "Huddle.App", "Components", "Shared", "QuestionCard.razor.css");

        Assert.False(File.Exists(scoped), "QuestionCard.razor.css must not exist: ::deep cannot reach a MudPaper root or its Mud children, so its rules apply by accident or never. Put them in app.css.");
    }

    /// <summary>Reads <c>app.css</c> from the repository.</summary>
    /// <returns>The stylesheet's text.</returns>
    private static string ReadAppCss() => File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "wwwroot", "app.css"));

    /// <summary>The declarations inside the rule whose selector is exactly <paramref name="selector"/>, so <c>.a</c> never matches <c>.a-b</c>.</summary>
    /// <param name="css">The stylesheet's text.</param>
    /// <param name="selector">The selector, as written at the start of a line.</param>
    /// <returns>The text between the rule's braces.</returns>
    private static string RuleBody(string css, string selector)
    {
        string opening = "\n" + selector + " {";
        int start = css.IndexOf(opening, StringComparison.Ordinal);
        Assert.True(start >= 0, $"app.css has no rule with the selector '{selector}'.");

        int end = css.IndexOf('}', start);
        return css[(start + opening.Length)..end];
    }
}
