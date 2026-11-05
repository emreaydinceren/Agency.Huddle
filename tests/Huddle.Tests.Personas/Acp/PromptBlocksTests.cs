using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.Tests.Acp.Fakes;

namespace Agency.Huddle.Tests.Acp;

/// <summary>
/// Task 13.1 (#362, #345): every Agent's composed prompt carries the Promised Work and Silent replies blocks, and the
/// Second Brain summary carries one search-order line whose tool names are filled from the offered list.
/// </summary>
public sealed class PromptBlocksTests
{
    /// <summary>What the summary says in place of a tool the session does not offer.</summary>
    private const string Unavailable = "(not available here; tell the Human)";

    /// <summary>The tools of a session that offers the two lookup tools.</summary>
    private static readonly IReadOnlyList<string> LookupTools =
    [
        "mcp__team__get_help",
        "mcp__team__list_tasks",
        "mcp__team__search",
    ];

    /// <summary>Composes the parts for a plain Persona offering <paramref name="toolNames"/>.</summary>
    /// <param name="toolNames">The offered tools' names.</param>
    private static IReadOnlyList<PromptPart> Compose(IReadOnlyList<string> toolNames) =>
        SystemPromptComposer.ComposeParts(
            new Persona("Nova", "You are Nova."), new FakePromptSource(), "mcp__team__get_help", toolNames, [], string.Empty, memory: null, SessionScope.Shared);

    /// <summary>A session that offers no tool but get_help still carries the Promised Work block, as its own part.</summary>
    [Fact]
    public void ComposeParts_AnyAgent_CarriesPromisedWorkBlock()
    {
        string text = Assert.Single(Compose(["mcp__team__get_help"]), p => p.Source is PromptPartSource.Prompt { Key: "systemPrompt.promisedWork" }).Text;

        Assert.Contains("is a progress line, not an ending", text, StringComparison.Ordinal); // contains-ok: one clause of a short paragraph; the whole text is pinned by the goldens.
        Assert.Contains("a Task, a Job or a Mention", text, StringComparison.Ordinal); // contains-ok: names the ways back; the whole text is pinned by the goldens.
        Assert.DoesNotContain("{{", text, StringComparison.Ordinal);
    }

    /// <summary>The Silent replies block renders <c>{{silentToken}}</c> as <see cref="SilentReply.Token"/>, so the wording cannot drift from what the Room recognises.</summary>
    [Fact]
    public void ComposeParts_AnyAgent_SilentRepliesBlockRendersTheConstant()
    {
        string text = Assert.Single(Compose(["mcp__team__get_help"]), p => p.Source is PromptPartSource.Prompt { Key: "systemPrompt.silentReply" }).Text;

        Assert.Contains($"reply with exactly {SilentReply.Token} and nothing else", text, StringComparison.Ordinal); // contains-ok: the token's placement; the whole text is pinned by the goldens.
        Assert.Contains("question addressed to you", text, StringComparison.Ordinal); // contains-ok: the exception clause; the whole text is pinned by the goldens.
        Assert.DoesNotContain("{{", text, StringComparison.Ordinal);
    }

    /// <summary>The token is typed in no catalog default: the shipped text holds the placeholder, and a changed constant would reach the model.</summary>
    [Fact]
    public void SilentReplyDefault_NamesTheTokenOnlyByPlaceholder()
    {
        var prompt = Agency.Huddle.App.Prompts.PromptCatalog.Get("systemPrompt.silentReply");

        Assert.Contains("{{silentToken}}", prompt.Default, StringComparison.Ordinal); // contains-ok: pins the placeholder, not the wording.
        Assert.DoesNotContain(SilentReply.Token, prompt.Default, StringComparison.Ordinal);
        Assert.Equal(["{{silentToken}}"], prompt.RequiredPlaceholders);
    }

    /// <summary>With both lookup tools offered, the summary names them in the search order, Memory then outputs then Tasks then raw.</summary>
    [Fact]
    public void ComposeParts_SecondBrain_SearchOrderNamesBothLookupTools()
    {
        string text = Assert.Single(Compose(LookupTools), p => p.Label == "Second Brain").Text;

        Assert.Contains(
            "To find something, check Memory, then outputs/, then Tasks with mcp__team__list_tasks or mcp__team__search, then raw/. Dates usually live in Tasks. If a lookup comes back weak or empty, try the next source before you say you do not know.",
            text,
            StringComparison.Ordinal);
        Assert.DoesNotContain("{{", text, StringComparison.Ordinal);
    }

    /// <summary>A session without the lookup tools reads the unavailable phrase, never a bare tool name.</summary>
    [Fact]
    public void ComposeParts_SecondBrainLookupToolsNotOffered_RendersUnavailablePhrase()
    {
        string text = Assert.Single(Compose(["mcp__team__get_help"]), p => p.Label == "Second Brain").Text;

        Assert.Contains($"then Tasks with {Unavailable} or {Unavailable}, then raw/.", text, StringComparison.Ordinal); // contains-ok: one sentence of a long prompt; the whole text is pinned by systemPrompt.secondBrain.txt.
        Assert.DoesNotContain("list_tasks", text, StringComparison.Ordinal);
        Assert.DoesNotContain("{{", text, StringComparison.Ordinal);
    }

    /// <summary>One lookup tool offered and the other not: each placeholder resolves on its own.</summary>
    [Fact]
    public void ComposeParts_SecondBrainOnlySearchOffered_ResolvesEachPlaceholderOnItsOwn()
    {
        string text = Assert.Single(Compose(["mcp__team__get_help", "mcp__team__search"]), p => p.Label == "Second Brain").Text;

        Assert.Contains($"then Tasks with {Unavailable} or mcp__team__search, then raw/.", text, StringComparison.Ordinal); // contains-ok: one sentence of a long prompt; the whole text is pinned by systemPrompt.secondBrain.txt.
    }
}
