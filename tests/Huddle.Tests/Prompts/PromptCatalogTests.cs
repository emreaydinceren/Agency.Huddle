namespace Agency.Huddle.Tests.Prompts;

using System.Text.RegularExpressions;
using Agency.Huddle.App.Prompts;

/// <summary>
/// Self-consistency checks on <see cref="PromptCatalog"/>: that its 29 entries are well-formed on their
/// own terms, independent of any config file or renderer that will later consume them.
/// </summary>
public sealed partial class PromptCatalogTests
{
    /// <summary>
    /// The catalog carries exactly the 35 prompts the task specifies, no more and no fewer: the original
    /// 24, plus <c>systemPrompt.skills</c> and <c>tool.readSkill.description</c> added for Spec §6.4, plus
    /// <c>tool.validateTeammate.description</c> added for Spec §6.8 (Task 9.3), plus
    /// <c>tool.proposeTeammates.description</c> added for Spec §6.9 (Task 10.2), plus
    /// <c>turn.greeting</c> added for Spec §6.14 (Task 16.3), plus the six File Changes Turn prompts
    /// (<c>turn.fileChangesHeader</c>, <c>turn.fileAdded</c>, <c>turn.fileChanged</c>,
    /// <c>turn.fileDeleted</c>, <c>turn.fileChangesMore</c>, <c>turn.folderUnchecked</c>) added for
    /// FC §6.13 (Task 7.1).
    /// </summary>
    [Fact]
    public void All_HasExactlyThirtyFivePrompts()
    {
        Assert.Equal(35, PromptCatalog.All.Count);
    }

    /// <summary>The six File Changes Turn prompts FC §6.13 defines all exist, are Live, and carry no <c>mcp__team__</c> literal.</summary>
    [Fact]
    public void Catalog_HasFileChangesTurnPrompts()
    {
        AssertFileChangesPrompt("turn.fileChangesHeader", [], []);
        AssertFileChangesPrompt("turn.fileAdded", ["{{path}}"], ["{{path}}"]);
        AssertFileChangesPrompt("turn.fileChanged", ["{{path}}"], ["{{path}}"]);
        AssertFileChangesPrompt("turn.fileDeleted", ["{{path}}"], ["{{path}}"]);
        AssertFileChangesPrompt("turn.fileChangesMore", ["{{count}}"], ["{{count}}"]);
        AssertFileChangesPrompt("turn.folderUnchecked", ["{{path}}", "{{max}}"], ["{{path}}"]);
    }

    /// <summary>Asserts one File Changes Turn prompt exists, is <see cref="PromptTiming.Live"/>, declares exactly <paramref name="placeholders"/> and requires exactly <paramref name="requiredPlaceholders"/>, and carries no <c>mcp__team__</c> literal.</summary>
    private static void AssertFileChangesPrompt(string key, IReadOnlyList<string> placeholders, IReadOnlyList<string> requiredPlaceholders)
    {
        var prompt = PromptCatalog.Get(key);

        Assert.Equal(PromptTiming.Live, prompt.Timing);
        Assert.Equal(placeholders, prompt.Placeholders);
        Assert.Equal(requiredPlaceholders, prompt.RequiredPlaceholders);
        Assert.False(prompt.Default.Contains("mcp__team__", StringComparison.Ordinal));
    }

    /// <summary>No two prompts share a <see cref="PromptDefinition.Key"/>.</summary>
    [Fact]
    public void All_KeysAreUnique()
    {
        var keys = PromptCatalog.All.Select(prompt => prompt.Key).ToList();

        Assert.Equal(keys.Count, keys.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>Every required placeholder is also a declared placeholder.</summary>
    [Fact]
    public void All_RequiredPlaceholdersAreDeclaredPlaceholders()
    {
        foreach (var prompt in PromptCatalog.All)
        {
            foreach (var required in prompt.RequiredPlaceholders)
            {
                Assert.Contains(required, prompt.Placeholders);
            }
        }
    }

    /// <summary>No prompt's default text is null, empty, or whitespace-only.</summary>
    [Fact]
    public void All_DefaultsAreNeverBlank()
    {
        foreach (var prompt in PromptCatalog.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(prompt.Default), $"Prompt '{prompt.Key}' has a blank Default.");
        }
    }

    /// <summary><see cref="PromptCatalog.Get"/> returns the prompt whose key was asked for.</summary>
    [Fact]
    public void Get_ReturnsTheMatchingPrompt()
    {
        var prompt = PromptCatalog.Get("turn.roomLabel");

        Assert.Equal("turn.roomLabel", prompt.Key);
    }

    /// <summary><see cref="PromptCatalog.Get"/> throws <see cref="KeyNotFoundException"/>, naming the key, when nothing matches.</summary>
    [Fact]
    public void Get_UnknownKey_ThrowsKeyNotFoundExceptionNamingTheKey()
    {
        var exception = Assert.Throws<KeyNotFoundException>(() => PromptCatalog.Get("nope"));

        Assert.Contains("nope", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Every <c>{{...}}</c> token that appears in a prompt's Default is declared in that same prompt's
    /// Placeholders — catching a typo'd placeholder at build/test time rather than at render time.
    /// </summary>
    [Fact]
    public void All_EveryTokenInDefaultIsDeclaredAsAPlaceholder()
    {
        foreach (var prompt in PromptCatalog.All)
        {
            var tokensFound = PlaceholderToken().Matches(prompt.Default).Select(m => m.Value).Distinct();

            foreach (var token in tokensFound)
            {
                Assert.Contains(token, prompt.Placeholders);
            }
        }
    }

    /// <summary>
    /// No prompt's Default still carries the literal <c>mcp__team__</c> tool prefix: every mention of a
    /// tool name — the help tool in the orientation, the five-name list in the tools paragraph, and
    /// the per-tool name in the help catalog entry — was replaced by a placeholder when the text was
    /// lifted out of C#, so the prefix itself now belongs only to whatever substitutes the
    /// placeholder's value, never to the template text.
    /// </summary>
    [Fact]
    public void All_NoDefaultContainsTheLiteralToolPrefix()
    {
        foreach (var prompt in PromptCatalog.All)
        {
            Assert.False(
                prompt.Default.Contains("mcp__team__", StringComparison.Ordinal),
                $"Prompt '{prompt.Key}' still contains the literal 'mcp__team__' prefix in its Default.");
        }
    }

    [GeneratedRegex(@"\{\{[a-zA-Z]+\}\}")]
    private static partial Regex PlaceholderToken();
}
