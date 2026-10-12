namespace Agency.Huddle.Tests.Ui.Tasks;

/// <summary>
/// Pins Spec §13.9's <c>window.huddleStorage</c> helper and §13.13.1's <c>window.huddleClipboard</c>
/// helper as source facts, because the repo has no JavaScript test runner - the same source-text
/// approach <see cref="AppStylesheetTests"/> and <c>ThemeSourceTests</c> use for <c>app.css</c>.
/// </summary>
public sealed class AppJsSourceTests
{
    /// <summary><c>window.huddleStorage</c> reads through <c>localStorage.getItem</c> inside a try/catch (Spec §13.9).</summary>
    [Fact]
    public void AppJs_DefinesHuddleStorage_WithTryCatch()
    {
        string text = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "wwwroot", "app.js"));

        // contains-ok: source-fact test, no JS runner - app.js's own text is what's pinned.
        Assert.Contains("window.huddleStorage", text, StringComparison.Ordinal);
        // contains-ok: source-fact test, no JS runner - app.js's own text is what's pinned.
        Assert.Contains("localStorage.getItem", text, StringComparison.Ordinal);
        // contains-ok: source-fact test, no JS runner - app.js's own text is what's pinned.
        Assert.Contains("catch", text, StringComparison.Ordinal);
    }

    /// <summary><c>window.huddleClipboard</c> checks <c>isSecureContext</c> and falls back to <c>execCommand("copy")</c> (Spec §13.13.1).</summary>
    [Fact]
    public void AppJs_DefinesHuddleClipboard_WithSecureContextCheckAndFallback()
    {
        string text = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "wwwroot", "app.js"));

        // contains-ok: source-fact test, no JS runner - app.js's own text is what's pinned.
        Assert.Contains("window.huddleClipboard", text, StringComparison.Ordinal);
        // contains-ok: source-fact test, no JS runner - app.js's own text is what's pinned.
        Assert.Contains("isSecureContext", text, StringComparison.Ordinal);
        // contains-ok: source-fact test, no JS runner - app.js's own text is what's pinned.
        Assert.Contains("execCommand(\"copy\")", text, StringComparison.Ordinal);
    }

    /// <summary>The pre-existing <c>teamComposer</c> and <c>teamScroll</c> helpers stay unchanged (Task 11.2.i).</summary>
    [Fact]
    public void AppJs_KeepsTeamComposerAndTeamScroll()
    {
        string text = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "wwwroot", "app.js"));

        // contains-ok: source-fact test, no JS runner - app.js's own text is what's pinned.
        Assert.Contains("window.teamComposer", text, StringComparison.Ordinal);
        // contains-ok: source-fact test, no JS runner - app.js's own text is what's pinned.
        Assert.Contains("window.teamScroll", text, StringComparison.Ordinal);
    }

    /// <summary><c>teamScroll.toMessage</c> finds the row by <c>data-message-id</c> with <c>CSS.escape</c>, centres it, focuses it without scrolling, flashes it and clears the flash on <c>animationend</c> or after two seconds (Global Search spec §9.1 step 3).</summary>
    [Fact]
    public void AppJs_TeamScroll_ToMessage_CentresFocusesAndFlashesTheRow()
    {
        string block = AppJsSourceTests.TeamScrollBlock();

        // contains-ok: source-fact test, no JS runner - app.js's own text is what's pinned.
        Assert.Contains("toMessage(el, id)", block, StringComparison.Ordinal);
        // contains-ok: source-fact test, no JS runner - app.js's own text is what's pinned.
        Assert.Contains("[data-message-id=\"", block, StringComparison.Ordinal);
        // contains-ok: source-fact test, no JS runner - app.js's own text is what's pinned.
        Assert.Contains("CSS.escape(id)", block, StringComparison.Ordinal);
        // contains-ok: source-fact test, no JS runner - app.js's own text is what's pinned.
        Assert.Contains("scrollIntoView({ block: \"center\" })", block, StringComparison.Ordinal);
        // contains-ok: source-fact test, no JS runner - app.js's own text is what's pinned.
        Assert.Contains("focus({ preventScroll: true })", block, StringComparison.Ordinal);
        // contains-ok: source-fact test, no JS runner - app.js's own text is what's pinned.
        Assert.Contains("classList.add(\"search-hit-flash\")", block, StringComparison.Ordinal);
        // contains-ok: source-fact test, no JS runner - app.js's own text is what's pinned.
        Assert.Contains("animationend", block, StringComparison.Ordinal);
        // contains-ok: source-fact test, no JS runner - app.js's own text is what's pinned.
        Assert.Contains("2000", block, StringComparison.Ordinal);
        // contains-ok: source-fact test, no JS runner - app.js's own text is what's pinned.
        Assert.Contains("return false", block, StringComparison.Ordinal);
    }

    /// <summary><c>teamScroll.isAtBottom</c> is true within 4 px of the bottom, in the element's own (zoomed) scroll coordinates.</summary>
    [Fact]
    public void AppJs_TeamScroll_IsAtBottom_UsesFourPixelsInTheElementsOwnCoordinates()
    {
        string block = AppJsSourceTests.TeamScrollBlock();

        // contains-ok: source-fact test, no JS runner - app.js's own text is what's pinned.
        Assert.Contains("isAtBottom(el)", block, StringComparison.Ordinal);
        // contains-ok: source-fact test, no JS runner - app.js's own text is what's pinned.
        Assert.Contains("el.scrollHeight - el.scrollTop - el.clientHeight <= 4", block, StringComparison.Ordinal);
    }

    /// <summary>The flash rules (from the <c>/* The Room jump */</c> comment inside the <c>/* Global search */</c> block to the end of app.css) fade from a MudBlazor palette variable, have a reduced-motion outline, and use neither <c>zoom</c> nor viewport units; the dropdown rules above them legitimately use <c>vh</c> (plan §3.6).</summary>
    [Fact]
    public void AppCss_SearchHitFlash_IsInTheGlobalSearchBlockAndHonoursReducedMotion()
    {
        string css = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "wwwroot", "app.css"));
        int blockStart = css.IndexOf("/* Global search */", StringComparison.Ordinal);
        Assert.True(blockStart >= 0, "Expected a /* Global search */ block in app.css.");
        int flashStart = css.IndexOf("/* The Room jump", blockStart, StringComparison.Ordinal);
        Assert.True(flashStart >= 0, "Expected the /* The Room jump */ flash rules inside the Global search block.");
        string block = css[flashStart..];

        // contains-ok: source-fact test, no stylesheet runner - app.css's own text is what's pinned.
        Assert.Contains(".search-hit-flash", block, StringComparison.Ordinal);
        // contains-ok: source-fact test, no stylesheet runner - app.css's own text is what's pinned.
        Assert.Contains("var(--mud-palette-", block, StringComparison.Ordinal);
        // contains-ok: source-fact test, no stylesheet runner - app.css's own text is what's pinned.
        Assert.Contains("@media (prefers-reduced-motion: reduce)", block, StringComparison.Ordinal);
        Assert.DoesNotContain("zoom", block, StringComparison.Ordinal);
        Assert.DoesNotContain("vh", block, StringComparison.Ordinal);
    }

    /// <summary>The text of the <c>window.teamScroll</c> object, up to the next top-level comment.</summary>
    private static string TeamScrollBlock()
    {
        string text = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "wwwroot", "app.js"));
        int start = text.IndexOf("window.teamScroll", StringComparison.Ordinal);
        int end = text.IndexOf("// Tab shortcuts", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, "Expected window.teamScroll to be followed by the Tab shortcuts comment.");
        return text[start..end];
    }

    /// <summary>The <c>#</c> picker (Spec §13.13.4, corrections-B6 "D15.3" item 3) lives in <c>teamComposer</c>'s single <c>keydown</c> handler, tracking its state per element on <c>el._picker</c> rather than in a module-level variable.</summary>
    [Fact]
    public void AppJs_TeamComposerKeydownHandler_IsSingleAndTracksPerElementPickerState()
    {
        string composerBlock = AppJsSourceTests.TeamComposerBlock();

        Assert.Equal(1, AppJsSourceTests.CountOccurrences(composerBlock, "addEventListener(\"keydown\""));
        // contains-ok: source-fact test, no JS runner
        Assert.Contains("el._picker", composerBlock, StringComparison.Ordinal);
    }

    /// <summary><c>preventDefault()</c> is called before any <c>await</c> in the keydown handler, so the event object is still valid when it's read (corrections-B6 "D15.3" item 3).</summary>
    [Fact]
    public void AppJs_TeamComposerKeydownHandler_PreventDefaultPrecedesAnyAwait()
    {
        string composerBlock = AppJsSourceTests.TeamComposerBlock();

        int preventDefaultIndex = composerBlock.IndexOf("preventDefault()", StringComparison.Ordinal);
        int firstAwaitIndex = composerBlock.IndexOf("await ", StringComparison.Ordinal);

        Assert.True(preventDefaultIndex >= 0, "Expected a preventDefault() call in teamComposer's keydown handler.");
        Assert.True(firstAwaitIndex >= 0, "Expected an await in teamComposer's keydown handler.");
        Assert.True(
            preventDefaultIndex < firstAwaitIndex,
            $"Expected preventDefault() (at {preventDefaultIndex}) before the first await (at {firstAwaitIndex}).");
    }

    /// <summary>On send, the picker state is reset shortly after <c>el.value</c> is cleared (corrections-B6 "D15.3" item 3: "reset after <c>el.value = ""</c> on send").</summary>
    [Fact]
    public void AppJs_TeamComposerKeydownHandler_ResetsPickerStateAfterClearingValueOnSend()
    {
        string composerBlock = AppJsSourceTests.TeamComposerBlock();

        int valueClearIndex = composerBlock.IndexOf("el.value = \"\";", StringComparison.Ordinal);
        Assert.True(valueClearIndex >= 0, "Expected the send path to clear el.value.");

        int pickerResetIndex = composerBlock.IndexOf("_picker", valueClearIndex, StringComparison.Ordinal);
        Assert.True(pickerResetIndex > valueClearIndex, "Expected a picker-state reset after clearing el.value on send.");
        Assert.InRange(pickerResetIndex - valueClearIndex, 1, 200);
    }

    /// <summary>Enter is skipped (never sends, never picks) while the IME is still composing (corrections-B6 "D15.3" item 3: "skip Enter when <c>e.isComposing</c>").</summary>
    [Fact]
    public void AppJs_TeamComposerKeydownHandler_SkipsEnterWhenComposing()
    {
        string composerBlock = AppJsSourceTests.TeamComposerBlock();

        // contains-ok: source-fact test, no JS runner
        Assert.Contains("isComposing", composerBlock, StringComparison.Ordinal);
    }

    /// <summary>Escape closes the picker by calling <c>TaskQueryAsync</c> with <see langword="null"/> (Spec §13.13.4: "Escape: Close the picker").</summary>
    [Fact]
    public void AppJs_TeamComposerKeydownHandler_EscapeClosesThePickerWithANullQuery()
    {
        string composerBlock = AppJsSourceTests.TeamComposerBlock();

        int escapeIndex = composerBlock.IndexOf("\"Escape\"", StringComparison.Ordinal);
        Assert.True(escapeIndex >= 0, "Expected an Escape case in teamComposer's keydown handler.");

        int nullQueryCallIndex = composerBlock.IndexOf("TaskQueryAsync\", null", escapeIndex, StringComparison.Ordinal);
        Assert.True(nullQueryCallIndex > escapeIndex, "Expected Escape's branch to call TaskQueryAsync with null.");
    }

    /// <summary>The 150 ms blur-close callback is wrapped in try/catch, so a disposed component or <c>DotNetObjectReference</c> can't throw out of a timer callback (corrections-B6 "D15.3" item 3).</summary>
    [Fact]
    public void AppJs_TeamComposerBlurHandler_Wraps150MsCallbackInTryCatch()
    {
        string composerBlock = AppJsSourceTests.TeamComposerBlock();

        int blurIndex = composerBlock.IndexOf("\"blur\"", StringComparison.Ordinal);
        int tryIndex = composerBlock.IndexOf("try", blurIndex, StringComparison.Ordinal);
        int catchIndex = composerBlock.IndexOf("catch", blurIndex, StringComparison.Ordinal);
        int delayIndex = composerBlock.IndexOf("150", blurIndex, StringComparison.Ordinal);

        Assert.True(blurIndex >= 0, "Expected a blur listener in teamComposer.");
        Assert.True(tryIndex > blurIndex, "Expected a try block after the blur listener.");
        Assert.True(catchIndex > tryIndex, "Expected a catch block after the try block.");
        Assert.True(delayIndex > catchIndex, "Expected the 150 ms delay to close the setTimeout call after the try/catch.");
    }

    /// <summary><c>teamComposer.insertTask</c> exists, for a picked or clicked Task id to be inserted into the textarea (Spec §13.13.4).</summary>
    [Fact]
    public void AppJs_DefinesInsertTask()
    {
        string composerBlock = AppJsSourceTests.TeamComposerBlock();

        // contains-ok: source-fact test, no JS runner
        Assert.Contains("insertTask", composerBlock, StringComparison.Ordinal);
    }

    /// <summary>Extracts the <c>window.teamComposer = { ... };</c> object literal's source text, so the picker's source-fact tests above don't match unrelated code elsewhere in <c>app.js</c> (e.g. <c>huddleClipboard</c>'s own <c>await</c>s and <c>catch</c>es).</summary>
    private static string TeamComposerBlock()
    {
        string text = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "wwwroot", "app.js"));
        int start = text.IndexOf("window.teamComposer", StringComparison.Ordinal);
        if (start < 0)
        {
            throw new InvalidOperationException("window.teamComposer not found in app.js.");
        }

        int end = text.IndexOf("\n};", start, StringComparison.Ordinal);
        if (end < 0)
        {
            throw new InvalidOperationException("Could not find the end of the teamComposer object literal in app.js.");
        }

        return text[start..(end + 3)];
    }

    /// <summary>Counts non-overlapping occurrences of <paramref name="needle"/> in <paramref name="haystack"/>, ordinally.</summary>
    private static int CountOccurrences(string haystack, string needle)
    {
        int count = 0;
        int index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }
}
