using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Agency.Huddle.Tests.Ui.References;

/// <summary>
/// Behaviour tests for the Reference picker's script (<c>window.teamComposer</c> in <c>app.js</c>): token
/// detection, suppression, IME and the post-pick rule. They run <c>composer-picker.harness.js</c> under Node
/// against a stand-in textarea, so they pin what the script does, not what its source says. A machine without
/// <c>node</c> on PATH fails them, as it fails the other Node-backed tests.
/// </summary>
public sealed class ComposerScriptTests
{
    /// <summary>One harness run shared by every test in the class: the scenarios are independent.</summary>
    private static readonly Lazy<Task<JsonElement>> Results = new(() => ComposerScriptTests.RunHarnessAsync(CancellationToken.None));

    /// <summary>A filter prefix followed by a space is still one token, with the text after the prefix as the phrase (spec §5.1, §5.5).</summary>
    [Fact]
    public async Task FindToken_FilterPrefixThenSpace_StaysOneToken()
    {
        JsonElement results = await ComposerScriptTests.Results.Value;

        Assert.Equal("lib: ", results.GetProperty("tokenLibSpace").GetProperty("token").GetString());
        Assert.Equal("lib: my plan", results.GetProperty("tokenLibPhrase").GetProperty("token").GetString());
    }

    /// <summary>The <c>@</c> token follows MentionParser: a Name may contain spaces, mid-word <c>@</c> is not a trigger, and a second <c>@</c> starts a new token.</summary>
    [Fact]
    public async Task FindToken_FollowsMentionParserRule()
    {
        JsonElement results = await ComposerScriptTests.Results.Value;

        Assert.Equal("Nova Pri", results.GetProperty("tokenNameWithSpace").GetProperty("token").GetString());
        Assert.Equal(JsonValueKind.Null, results.GetProperty("tokenMidWord").ValueKind);
        Assert.Equal("b", results.GetProperty("tokenAtSpanBreak").GetProperty("token").GetString());
        Assert.Equal("#", results.GetProperty("tokenHash").GetProperty("trigger").GetString());
    }

    /// <summary>ArrowUp, ArrowDown and Escape during an IME composition (isComposing or keyCode 229) belong to the IME: no picker call, no default prevented, picker stays open.</summary>
    [Fact]
    public async Task Keydown_DuringImeComposition_IsLeftToTheIme()
    {
        JsonElement results = await ComposerScriptTests.Results.Value;

        Assert.Equal(0, results.GetProperty("imeCallsDuringComposingKeys").GetInt32());
        Assert.False(results.GetProperty("imeDefaultPrevented").GetBoolean());
        Assert.True(results.GetProperty("imePickerStillOpen").GetBoolean());
        Assert.True(results.GetProperty("plainArrowMoves").GetBoolean());
    }

    /// <summary>The picker does not query while a composition is active and queries once, with the committed text, after compositionend.</summary>
    [Fact]
    public async Task Input_DuringComposition_QueriesOnlyAfterCompositionEnd()
    {
        JsonElement results = await ComposerScriptTests.Results.Value;

        Assert.Equal(0, results.GetProperty("imeQueriesWhileComposing").GetInt32());
        Assert.Equal(["たな"], ComposerScriptTests.Strings(results.GetProperty("imeQueriesAfterEnd")));
    }

    /// <summary>After a pick no earlier <c>@</c> token is active: the picker stays shut, whichever way the insert is made, and the next Enter sends.</summary>
    /// <param name="execCommandWorks">Whether the harness's <c>execCommand</c> succeeds (<c>true</c>) or is refused so <c>setRangeText</c> runs (<c>false</c>).</param>
    [Theory]
    [InlineData("false")]
    [InlineData("true")]
    public async Task Pick_DoesNotReopenOnAnEarlierToken(string execCommandWorks)
    {
        JsonElement results = await ComposerScriptTests.Results.Value;

        JsonElement pick = results.GetProperty("postPick" + execCommandWorks);
        Assert.True(pick.GetProperty("openBefore").GetBoolean());
        Assert.Equal("@Nova check GROW-0001 ", pick.GetProperty("value").GetString());
        Assert.False(pick.GetProperty("pickerOpenAfterPick").GetBoolean());
        Assert.Equal(["gro"], ComposerScriptTests.Strings(pick.GetProperty("queries")));
        Assert.True(results.GetProperty("postPickEnterSends" + execCommandWorks).GetBoolean());
        Assert.True(results.GetProperty("postPickTypingStaysShut" + execCommandWorks).GetBoolean());
    }

    /// <summary>A new <c>@</c> typed after a pick, or after Escape, opens the picker again; more of the closed token does not.</summary>
    [Fact]
    public async Task Suppression_EndsWhenANewTokenStarts()
    {
        JsonElement results = await ComposerScriptTests.Results.Value;

        Assert.Equal(["ta"], ComposerScriptTests.Strings(results.GetProperty("newTokenAfterPickQueries")));
        Assert.Equal(0, results.GetProperty("escape").GetProperty("sameTokenQueries").GetInt32());
        Assert.Equal(["ta"], ComposerScriptTests.Strings(results.GetProperty("escape").GetProperty("newTokenQueries")));
    }

    /// <summary>Reads a JSON array of strings.</summary>
    private static string[] Strings(JsonElement array) => [.. array.EnumerateArray().Select(static item => item.GetString() ?? string.Empty)];

    /// <summary>Runs the harness once and parses its JSON.</summary>
    private static async Task<JsonElement> RunHarnessAsync(CancellationToken ct)
    {
        ProcessStartInfo info = new("node")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
        };
        info.ArgumentList.Add(RepoPaths.Data("composer-picker.harness.js"));
        info.ArgumentList.Add(CssSource.RepoPath("src", "Huddle.App", "wwwroot", "app.js"));
        using Process process = Process.Start(info) ?? throw new InvalidOperationException("node did not start.");
        Task<string> error = process.StandardError.ReadToEndAsync(ct);
        Task<string> output = process.StandardOutput.ReadToEndAsync(ct);
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        await process.WaitForExitAsync(timeout.Token);
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException("The picker harness failed: " + await error);
        }

        using JsonDocument document = JsonDocument.Parse(await output);
        return document.RootElement.Clone();
    }
}
