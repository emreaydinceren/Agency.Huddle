using Agency.Huddle.App.Prompts;

namespace Agency.Huddle.Tests.Prompts;

/// <summary>Pins the catalog default of the Raw guard's refusal text (Spec §8.6).</summary>
public sealed class RawRefusalPromptTests
{
    /// <summary>The default of <c>systemPrompt.rawRefusal</c> is the settled text, character for character.</summary>
    [Fact]
    public void RawRefusal_Default_IsTheSettledText()
    {
        Assert.Equal(
            "Files in raw/ are kept as they arrived. Add a new file beside the original instead, for example \"<name> - extract.md\".",
            PromptCatalog.Get("systemPrompt.rawRefusal").Default);
    }
}
