using Agency.Huddle.Tests.Css;

namespace Agency.Huddle.Tests.Ui.Tasks;

/// <summary>
/// Pins Spec §13.9's <c>storage.js</c> helper, §13.13.1's <c>clipboard.js</c> helper and the composer, scroll and
/// search modules (the former <c>app.js</c> globals) as source facts, because the repo has no JavaScript test
/// runner - the same source-text approach <c>AppStylesheetTests</c> and <c>ThemeSourceTests</c> use for <c>app.css</c>.
/// </summary>
public sealed class AppJsSourceTests
{
    /// <summary><c>storage.js</c> exports <c>get</c> and <c>set</c> (the export line is pinned) and reads through <c>localStorage.getItem</c> inside a try/catch (Spec §13.9).</summary>
    [Fact]
    public void StorageJs_ExportsGetAndSet_WithTryCatch()
    {
        string text = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "wwwroot", "storage.js"));

        // contains-ok: source-fact test, no JS runner - storage.js's own text is what's pinned.
        Assert.Contains("export const { get, set } = storage;", text, StringComparison.Ordinal);
        // contains-ok: source-fact test, no JS runner - storage.js's own text is what's pinned.
        Assert.Contains("localStorage.getItem", text, StringComparison.Ordinal);
        // contains-ok: source-fact test, no JS runner - storage.js's own text is what's pinned.
        Assert.Contains("catch", text, StringComparison.Ordinal);
    }

    /// <summary><c>clipboard.js</c> exports <c>copy</c> (the export line is pinned), checks <c>isSecureContext</c> and falls back to <c>execCommand("copy")</c> (Spec §13.13.1).</summary>
    [Fact]
    public void ClipboardJs_ExportsCopy_WithSecureContextCheckAndFallback()
    {
        string text = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "wwwroot", "clipboard.js"));

        // contains-ok: source-fact test, no JS runner - clipboard.js's own text is what's pinned.
        Assert.Contains("export const { copy } = clipboard;", text, StringComparison.Ordinal);
        // contains-ok: source-fact test, no JS runner - clipboard.js's own text is what's pinned.
        Assert.Contains("isSecureContext", text, StringComparison.Ordinal);
        // contains-ok: source-fact test, no JS runner - clipboard.js's own text is what's pinned.
        Assert.Contains("execCommand(\"copy\")", text, StringComparison.Ordinal);
    }

    /// <summary>The pre-existing composer and scroll helpers keep their public surface: <c>composer.js</c> and <c>scroll.js</c> export exactly the members C# calls (Task 11.2.i).</summary>
    [Fact]
    public void ComposerJsAndScrollJs_KeepTheirPublicExports()
    {
        string composer = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "wwwroot", "composer.js"));
        string scroll = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "wwwroot", "scroll.js"));

        // contains-ok: source-fact test, no JS runner - composer.js's own text is what's pinned.
        Assert.Contains("export const { attach, detach, insertReference, pickerClosed, send, setDraftKey } = composer;", composer, StringComparison.Ordinal);
        // contains-ok: source-fact test, no JS runner - scroll.js's own text is what's pinned.
        Assert.Contains("export const { toBottom, toMessage, intoView, isAtBottom } = scroll;", scroll, StringComparison.Ordinal);
    }

    /// <summary><c>scroll.toMessage</c> (in <c>scroll.js</c>) finds the row by <c>data-message-id</c> with <c>CSS.escape</c>, centres it, focuses it without scrolling, flashes it and clears the flash on <c>animationend</c> or after two seconds (Global Search spec §9.1 step 3).</summary>
    [Fact]
    public void ScrollJs_ToMessage_CentresFocusesAndFlashesTheRow()
    {
        string block = AppJsSourceTests.ScrollBlock();

        // contains-ok: source-fact test, no JS runner - scroll.js's own text is what's pinned.
        Assert.Contains("toMessage(el, id)", block, StringComparison.Ordinal);
        // contains-ok: source-fact test, no JS runner - scroll.js's own text is what's pinned.
        Assert.Contains("[data-message-id=\"", block, StringComparison.Ordinal);
        // contains-ok: source-fact test, no JS runner - scroll.js's own text is what's pinned.
        Assert.Contains("CSS.escape(id)", block, StringComparison.Ordinal);
        // contains-ok: source-fact test, no JS runner - scroll.js's own text is what's pinned.
        Assert.Contains("scrollIntoView({ block: \"center\" })", block, StringComparison.Ordinal);
        // contains-ok: source-fact test, no JS runner - scroll.js's own text is what's pinned.
        Assert.Contains("focus({ preventScroll: true })", block, StringComparison.Ordinal);
        // contains-ok: source-fact test, no JS runner - scroll.js's own text is what's pinned.
        Assert.Contains("classList.add(\"search-hit-flash\")", block, StringComparison.Ordinal);
        // contains-ok: source-fact test, no JS runner - scroll.js's own text is what's pinned.
        Assert.Contains("animationend", block, StringComparison.Ordinal);
        // contains-ok: source-fact test, no JS runner - scroll.js's own text is what's pinned.
        Assert.Contains("2000", block, StringComparison.Ordinal);
        // contains-ok: source-fact test, no JS runner - scroll.js's own text is what's pinned.
        Assert.Contains("return false", block, StringComparison.Ordinal);
    }

    /// <summary><c>scroll.isAtBottom</c> (in <c>scroll.js</c>) is true within 4 px of the bottom, in the element's own scroll coordinates.</summary>
    [Fact]
    public void ScrollJs_IsAtBottom_UsesFourPixelsInTheElementsOwnCoordinates()
    {
        string block = AppJsSourceTests.ScrollBlock();

        // contains-ok: source-fact test, no JS runner - scroll.js's own text is what's pinned.
        Assert.Contains("isAtBottom(el)", block, StringComparison.Ordinal);
        // contains-ok: source-fact test, no JS runner - scroll.js's own text is what's pinned.
        Assert.Contains("el.scrollHeight - el.scrollTop - el.clientHeight <= 4", block, StringComparison.Ordinal);
    }

    /// <summary>The flash rules (from the <c>/* The Room jump */</c> comment inside the <c>/* Global search */</c> block to the end of app.css) fade from a MudBlazor palette variable, have a reduced-motion outline, and use neither <c>zoom</c> nor viewport units; the dropdown rules above them legitimately use <c>vh</c> (plan §3.6).</summary>
    [Fact]
    public void AppCss_SearchHitFlash_IsInTheGlobalSearchBlockAndHonoursReducedMotion()
    {
        string css = CssText.AppCss(CssNewlines.AsIs);
        int blockStart = css.IndexOf("/* ---- Global search ", StringComparison.Ordinal);
        Assert.True(blockStart >= 0, "Expected a /* Global search */ block in app.css.");
        int flashStart = css.IndexOf("/* The Room jump", blockStart, StringComparison.Ordinal);
        Assert.True(flashStart >= 0, "Expected the /* The Room jump */ flash rules inside the Global search block.");
        // The flash rules end where the /* References */ block begins (its picker legitimately uses vh; AT-T13 pins it).
        int referencesStart = css.IndexOf("/* ---- References ", flashStart, StringComparison.Ordinal);
        string block = referencesStart >= 0 ? css[flashStart..referencesStart] : css[flashStart..];

        // contains-ok: source-fact test, no stylesheet runner - app.css's own text is what's pinned.
        Assert.Contains(".search-hit-flash", block, StringComparison.Ordinal);
        // contains-ok: source-fact test, no stylesheet runner - app.css's own text is what's pinned.
        Assert.Contains("var(--mud-palette-", block, StringComparison.Ordinal);
        // contains-ok: source-fact test, no stylesheet runner - app.css's own text is what's pinned.
        Assert.Contains("@media (prefers-reduced-motion: reduce)", block, StringComparison.Ordinal);
        Assert.DoesNotContain("zoom", block, StringComparison.Ordinal);
        Assert.DoesNotContain("vh", block, StringComparison.Ordinal);
    }

    /// <summary>The text of the <c>scroll</c> object in <c>scroll.js</c>, up to the <c>};</c> that closes it.</summary>
    private static string ScrollBlock()
    {
        string text = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "wwwroot", "scroll.js"));
        int start = text.IndexOf("const scroll = {", StringComparison.Ordinal);
        int end = start < 0 ? -1 : text.IndexOf("\n};", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, "Expected scroll.js to define const scroll as an object literal closed by a line holding only };.");
        return text[start..(end + 3)];
    }

    /// <summary>The <c>search</c> object in <c>search.js</c> adds its one <c>keydown</c> listener on <c>window</c> in the capture phase, so the composer and CodeMirror cannot swallow the key, and removes it in <c>detach</c> (Global Search spec §5.2).</summary>
    [Fact]
    public void HuddleSearch_ListensInCapturePhase()
    {
        string block = AppJsSourceTests.HuddleSearchBlock();

        AppJsSourceTests.AssertInOrder(
            block,
            "attach(dotnetRef, host)",
            "window.addEventListener(\"keydown\", search._handler, true)",
            "detach()",
            "window.removeEventListener(\"keydown\", search._handler, true)");
        Assert.Equal(1, CssText.CountOccurrences(block, "addEventListener("));
    }

    /// <summary><c>Ctrl+K</c> (no Shift, either letter case) calls <c>preventDefault</c>, remembers the focused element, focuses the box and tells C# to open (Global Search spec §5.2, D-3).</summary>
    [Fact]
    public void HuddleSearch_CtrlK_PreventsDefault()
    {
        string block = AppJsSourceTests.HuddleSearchBlock();

        AppJsSourceTests.AssertInOrder(
            block,
            "e.ctrlKey",
            "e.shiftKey === search.shortcutNeedsShift",
            "\"k\"",
            "e.preventDefault()",
            "document.activeElement",
            ".focus()",
            "invokeMethodAsync(\"OpenAsync\")");
        AppJsSourceTests.AssertInOrder(block, "e.key.toLowerCase()");
    }

    /// <summary><c>/</c> opens the box only when the target is not an <c>input</c>, a <c>textarea</c>, a <c>[contenteditable]</c> element or inside <c>.cm-editor</c>, so a slash typed in the composer is never stolen (Global Search spec §5.2).</summary>
    [Fact]
    public void HuddleSearch_Slash_SkipsTextFieldsAndCodeMirror()
    {
        string block = AppJsSourceTests.HuddleSearchBlock();

        AppJsSourceTests.AssertInOrder(
            block,
            "e.key === \"/\"",
            "INPUT",
            "TEXTAREA",
            "[contenteditable]",
            ".cm-editor",
            "e.preventDefault()");
    }

    /// <summary><c>Alt+1</c> to <c>Alt+6</c> and <c>Ctrl+Left/Right</c> are handled only after a check of the open flag that <c>setOpen</c> sets, so they never collide with the Tab shortcuts (Global Search spec §6.5).</summary>
    [Fact]
    public void HuddleSearch_AltDigits_OnlyWhileOpen()
    {
        string block = AppJsSourceTests.HuddleSearchBlock();

        AppJsSourceTests.AssertInOrder(
            block,
            "if (!search._open)",
            "e.altKey",
            "invokeMethodAsync(\"SelectChipAsync\"",
            "\"ArrowLeft\"",
            "invokeMethodAsync(\"MoveChipAsync\"");
        AppJsSourceTests.AssertInOrder(block, "setOpen(open)", "search._open = open");
        AppJsSourceTests.AssertInOrder(block, "restoreFocus()", "search._previous", ".focus()");
    }

    /// <summary>The text of the <c>search</c> object in <c>search.js</c>, up to the closing <c>};</c> at the start of a line.</summary>
    private static string HuddleSearchBlock()
    {
        string text = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "wwwroot", "search.js"));
        int start = text.IndexOf("const search = {", StringComparison.Ordinal);
        Assert.True(start >= 0, "Expected const search in search.js.");
        int end = text.IndexOf("\n};", start, StringComparison.Ordinal);
        Assert.True(end > start, "Expected the huddleSearch object literal to end with a line holding only };.");
        return text[start..(end + 3)];
    }

    /// <summary>Asserts that each needle occurs in <paramref name="text"/> after the one before it.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="needles">The fragments, in the order they must appear.</param>
    private static void AssertInOrder(string text, params string[] needles)
    {
        int from = 0;
        foreach (string needle in needles)
        {
            int found = text.IndexOf(needle, from, StringComparison.Ordinal);
            Assert.True(found >= 0, $"Expected '{needle}' after offset {from} in the huddleSearch block.");
            from = found + needle.Length;
        }
    }

    /// <summary>The <c>#</c> picker (Spec §13.13.4, corrections-B6 "D15.3" item 3) lives in <c>composer.js</c>'s single <c>keydown</c> handler, tracking its state per element on <c>el._picker</c> rather than in a module-level variable.</summary>
    [Fact]
    public void ComposerJs_KeydownHandler_IsSingleAndTracksPerElementPickerState()
    {
        string composerBlock = AppJsSourceTests.ComposerBlock();

        Assert.Equal(1, CssText.CountOccurrences(composerBlock, "addEventListener(\"keydown\""));
        // contains-ok: source-fact test, no JS runner
        Assert.Contains("el._picker", composerBlock, StringComparison.Ordinal);
    }

    /// <summary><c>preventDefault()</c> is called before any <c>await</c> in the keydown handler, so the event object is still valid when it's read (corrections-B6 "D15.3" item 3).</summary>
    [Fact]
    public void ComposerJs_KeydownHandler_PreventDefaultPrecedesAnyAwait()
    {
        string composerBlock = AppJsSourceTests.ComposerBlock();

        int preventDefaultIndex = composerBlock.IndexOf("preventDefault()", StringComparison.Ordinal);
        int firstAwaitIndex = composerBlock.IndexOf("await ", StringComparison.Ordinal);

        Assert.True(preventDefaultIndex >= 0, "Expected a preventDefault() call in composer's keydown handler.");
        Assert.True(firstAwaitIndex >= 0, "Expected an await in composer's keydown handler.");
        Assert.True(
            preventDefaultIndex < firstAwaitIndex,
            $"Expected preventDefault() (at {preventDefaultIndex}) before the first await (at {firstAwaitIndex}).");
    }

    /// <summary>On send, the picker state is reset shortly after <c>el.value</c> is cleared (corrections-B6 "D15.3" item 3: "reset after <c>el.value = ""</c> on send").</summary>
    [Fact]
    public void ComposerJs_KeydownHandler_ResetsPickerStateAfterClearingValueOnSend()
    {
        string composerBlock = AppJsSourceTests.ComposerBlock();

        int valueClearIndex = composerBlock.IndexOf("el.value = \"\";", StringComparison.Ordinal);
        Assert.True(valueClearIndex >= 0, "Expected the send path to clear el.value.");

        int pickerResetIndex = composerBlock.IndexOf("_picker", valueClearIndex, StringComparison.Ordinal);
        Assert.True(pickerResetIndex > valueClearIndex, "Expected a picker-state reset after clearing el.value on send.");
        Assert.InRange(pickerResetIndex - valueClearIndex, 1, 200);
    }

    /// <summary>Enter is skipped (never sends, never picks) while the IME is still composing (corrections-B6 "D15.3" item 3: "skip Enter when <c>e.isComposing</c>").</summary>
    [Fact]
    public void ComposerJs_KeydownHandler_SkipsEnterWhenComposing()
    {
        string composerBlock = AppJsSourceTests.ComposerBlock();

        // contains-ok: source-fact test, no JS runner
        Assert.Contains("isComposing", composerBlock, StringComparison.Ordinal);
    }

    /// <summary>Escape closes the picker by calling <c>ReferenceQueryAsync</c> with a <see langword="null"/> token (spec §5.4: "Escape: Close the picker").</summary>
    [Fact]
    public void ComposerJs_KeydownHandler_EscapeClosesThePickerWithANullQuery()
    {
        string composerBlock = AppJsSourceTests.ComposerBlock();

        int escapeIndex = composerBlock.IndexOf("\"Escape\"", StringComparison.Ordinal);
        Assert.True(escapeIndex >= 0, "Expected an Escape case in composer's keydown handler.");

        int nullQueryCallIndex = composerBlock.IndexOf("ReferenceQueryAsync\", null", escapeIndex, StringComparison.Ordinal);
        Assert.True(nullQueryCallIndex > escapeIndex, "Expected Escape's branch to call ReferenceQueryAsync with null.");
    }

    /// <summary>The 150 ms blur-close callback is wrapped in try/catch, so a disposed component or <c>DotNetObjectReference</c> can't throw out of a timer callback (corrections-B6 "D15.3" item 3).</summary>
    [Fact]
    public void ComposerJs_BlurHandler_Wraps150MsCallbackInTryCatch()
    {
        string composerBlock = AppJsSourceTests.ComposerBlock();

        int blurIndex = composerBlock.IndexOf("\"blur\"", StringComparison.Ordinal);
        int tryIndex = composerBlock.IndexOf("try", blurIndex, StringComparison.Ordinal);
        int catchIndex = composerBlock.IndexOf("catch", blurIndex, StringComparison.Ordinal);
        int delayIndex = composerBlock.IndexOf("150", blurIndex, StringComparison.Ordinal);

        Assert.True(blurIndex >= 0, "Expected a blur listener in composer.");
        Assert.True(tryIndex > blurIndex, "Expected a try block after the blur listener.");
        Assert.True(catchIndex > tryIndex, "Expected a catch block after the try block.");
        Assert.True(delayIndex > catchIndex, "Expected the 150 ms delay to close the setTimeout call after the try/catch.");
    }

    /// <summary>The <c>@</c> token rule is <c>MentionParser</c>'s: at the start or after a character that is not a letter, digit, <c>_</c> or <c>@</c>, then up to 64 characters with no newline and no <c>@</c> (spec §5.1).</summary>
    [Fact]
    public void AtToken_UsesMentionParsersBlockedCharacters()
    {
        string composerBlock = AppJsSourceTests.ComposerBlock();

        // contains-ok: source-fact test, no JS runner
        Assert.Contains("/(?:^|[^A-Za-z0-9_@])@([^\\n@]{0,64})$/", composerBlock, StringComparison.Ordinal);
    }

    /// <summary>The <c>#</c> token regex is today's, unchanged (spec §5.1).</summary>
    [Fact]
    public void HashToken_RegexUnchanged()
    {
        string composerBlock = AppJsSourceTests.ComposerBlock();

        // contains-ok: source-fact test, no JS runner
        Assert.Contains("/(?:^|\\s)#([A-Za-z0-9-]{0,40})$/", composerBlock, StringComparison.Ordinal);
    }

    /// <summary>An insert is one undoable edit: <c>execCommand("insertText")</c>, with <c>setRangeText</c> and an <c>input</c> event when it returns false (spec §6 undo, D-24).</summary>
    [Fact]
    public void Insert_UsesExecCommandWithSetRangeTextFallback()
    {
        string composerBlock = AppJsSourceTests.ComposerBlock();

        int insertIndex = composerBlock.IndexOf("insertReference(el, text)", StringComparison.Ordinal);
        Assert.True(insertIndex >= 0, "Expected composer.insertReference.");
        int execIndex = composerBlock.IndexOf("document.execCommand(\"insertText\", false, text + \" \")", insertIndex, StringComparison.Ordinal);
        int fallbackIndex = composerBlock.IndexOf("el.setRangeText(text + \" \", tokenStart, caret, \"end\")", insertIndex, StringComparison.Ordinal);
        int inputIndex = composerBlock.IndexOf("new Event(\"input\"", fallbackIndex, StringComparison.Ordinal);
        Assert.True(execIndex > insertIndex, "Expected execCommand insertText.");
        Assert.True(fallbackIndex > execIndex, "Expected the setRangeText fallback after execCommand.");
        Assert.True(inputIndex > fallbackIndex, "Expected an input event after the fallback.");
    }

    /// <summary>A pick suppresses the picker for the inserted text until the caret leaves it, and the suppression is set before the insert fires <c>input</c> (spec §5.1).</summary>
    [Fact]
    public void Insert_SuppressesUntilCaretLeaves()
    {
        string composerBlock = AppJsSourceTests.ComposerBlock();

        int fromIndex = composerBlock.IndexOf("suppressFrom = tokenStart", StringComparison.Ordinal);
        int execIndex = composerBlock.IndexOf("document.execCommand(\"insertText\"", StringComparison.Ordinal);
        int refreshIndex = composerBlock.IndexOf("async refresh(el)", StringComparison.Ordinal);
        int checkIndex = composerBlock.IndexOf("suppressFrom", refreshIndex, StringComparison.Ordinal);
        Assert.True(fromIndex >= 0, "Expected suppressFrom to be recorded.");
        Assert.DoesNotContain("suppressTo", composerBlock, StringComparison.Ordinal);
        Assert.True(fromIndex < execIndex, "Expected the suppression to be set before execCommand fires input.");
        Assert.True(checkIndex > refreshIndex, "Expected refresh (run by the input handler) to read the suppression.");
    }

    /// <summary>The suppression holds only while the SAME token continues: a caret range alone must not keep it, or a new token typed over replaced text never reopens the picker (QA defect D1).</summary>
    [Fact]
    public void InputHandler_SuppressionEndsWhenTheTokenChanges()
    {
        string composerBlock = AppJsSourceTests.ComposerBlock();

        int inputHandlerIndex = composerBlock.IndexOf("async refresh(el)", StringComparison.Ordinal);
        int sameTokenIndex = composerBlock.IndexOf("if (sameToken)", inputHandlerIndex, StringComparison.Ordinal);
        int clearIndex = composerBlock.IndexOf("picker.suppressFrom = null", inputHandlerIndex, StringComparison.Ordinal);
        Assert.DoesNotContain("inRange", composerBlock, StringComparison.Ordinal);
        Assert.True(sameTokenIndex > inputHandlerIndex, "Expected the suppression to be kept only when the token is the same one.");
        Assert.True(clearIndex > sameTokenIndex, "Expected any other token to clear the suppression.");
    }

    /// <summary>Sending empties the box without an <c>input</c> event, so the send path clears the suppression itself (QA defect D1).</summary>
    [Fact]
    public void Send_ClearsTheSuppression()
    {
        string composerBlock = AppJsSourceTests.ComposerBlock();

        int sendIndex = composerBlock.IndexOf("async send(el)", StringComparison.Ordinal);
        int endIndex = composerBlock.IndexOf("findToken(el) {", sendIndex, StringComparison.Ordinal);
        int clearIndex = composerBlock.IndexOf("el._picker.suppressFrom = null", sendIndex, StringComparison.Ordinal);
        Assert.True(sendIndex >= 0 && endIndex > sendIndex, "Expected send before findToken.");
        Assert.True(clearIndex > sendIndex && clearIndex < endIndex, "Expected send to clear the suppression.");
    }

    /// <summary>Esc suppresses by the token it closed (so only that token's continuation stays shut), not by an empty prefix that matches any token at the same start (QA defect D1, AT-M8).</summary>
    [Fact]
    public void Escape_SuppressesTheClosedTokenOnly()
    {
        string composerBlock = AppJsSourceTests.ComposerBlock();

        int escapeIndex = composerBlock.IndexOf("e.key === \"Escape\"", StringComparison.Ordinal);
        int suppressIndex = composerBlock.IndexOf("suppress(el, null)", escapeIndex, StringComparison.Ordinal);
        int returnIndex = composerBlock.IndexOf("return;", escapeIndex, StringComparison.Ordinal);
        Assert.True(escapeIndex >= 0, "Expected an Escape branch.");
        Assert.True(suppressIndex > escapeIndex && suppressIndex < returnIndex, "Expected Esc to suppress by the closed token.");
    }

    /// <summary><c>composer.insertTask</c> is gone: every kind goes through <c>insertReference</c> (plan T7.4).</summary>
    [Fact]
    public void InsertTask_IsGone()
    {
        string composerBlock = AppJsSourceTests.ComposerBlock();

        // contains-ok: source-fact test, no JS runner
        Assert.DoesNotContain("insertTask", composerBlock, StringComparison.Ordinal);
    }

    /// <summary>Extracts the <c>const composer = { ... };</c> object literal's source text from <c>composer.js</c>, so the picker's source-fact tests above don't match the module's header comment or its export line.</summary>
    private static string ComposerBlock()
    {
        string text = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "wwwroot", "composer.js"));
        int start = text.IndexOf("const composer = {", StringComparison.Ordinal);
        if (start < 0)
        {
            throw new InvalidOperationException("const composer not found in composer.js.");
        }

        int end = text.IndexOf("\n};", start, StringComparison.Ordinal);
        if (end < 0)
        {
            throw new InvalidOperationException("Could not find the end of the composer object literal in composer.js.");
        }

        return text[start..(end + 3)];
    }

}
