namespace Agency.Huddle.Tests.Hooks;

using System.Text.RegularExpressions;
using Agency.Huddle.App.Hooks;

/// <summary>
/// Self-consistency checks on <see cref="HookCatalog"/>: that its 24 entries are well-formed on their
/// own terms, independent of any config file or renderer that will later consume them.
/// </summary>
public sealed partial class HookCatalogTests
{
    /// <summary>The catalog carries exactly the 24 hooks the task specifies, no more and no fewer.</summary>
    [Fact]
    public void All_HasExactlyTwentyFourHooks()
    {
        Assert.Equal(24, HookCatalog.All.Count);
    }

    /// <summary>No two hooks share a <see cref="HookDefinition.Key"/>.</summary>
    [Fact]
    public void All_KeysAreUnique()
    {
        var keys = HookCatalog.All.Select(hook => hook.Key).ToList();

        Assert.Equal(keys.Count, keys.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>Every required placeholder is also a declared placeholder.</summary>
    [Fact]
    public void All_RequiredPlaceholdersAreDeclaredPlaceholders()
    {
        foreach (var hook in HookCatalog.All)
        {
            foreach (var required in hook.RequiredPlaceholders)
            {
                Assert.Contains(required, hook.Placeholders);
            }
        }
    }

    /// <summary>No hook's default text is null, empty, or whitespace-only.</summary>
    [Fact]
    public void All_DefaultsAreNeverBlank()
    {
        foreach (var hook in HookCatalog.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(hook.Default), $"Hook '{hook.Key}' has a blank Default.");
        }
    }

    /// <summary><see cref="HookCatalog.Get"/> returns the hook whose key was asked for.</summary>
    [Fact]
    public void Get_ReturnsTheMatchingHook()
    {
        var hook = HookCatalog.Get("turn.roomLabel");

        Assert.Equal("turn.roomLabel", hook.Key);
    }

    /// <summary><see cref="HookCatalog.Get"/> throws <see cref="KeyNotFoundException"/>, naming the key, when nothing matches.</summary>
    [Fact]
    public void Get_UnknownKey_ThrowsKeyNotFoundExceptionNamingTheKey()
    {
        var exception = Assert.Throws<KeyNotFoundException>(() => HookCatalog.Get("nope"));

        Assert.Contains("nope", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Every <c>{{...}}</c> token that appears in a hook's Default is declared in that same hook's
    /// Placeholders — catching a typo'd placeholder at build/test time rather than at render time.
    /// </summary>
    [Fact]
    public void All_EveryTokenInDefaultIsDeclaredAsAPlaceholder()
    {
        foreach (var hook in HookCatalog.All)
        {
            var tokensFound = PlaceholderToken().Matches(hook.Default).Select(m => m.Value).Distinct();

            foreach (var token in tokensFound)
            {
                Assert.Contains(token, hook.Placeholders);
            }
        }
    }

    /// <summary>
    /// No hook's Default still carries the literal <c>mcp__team__</c> tool prefix: every mention of a
    /// tool name — the help tool in the orientation, the five-name list in the tools paragraph, and
    /// the per-tool name in the help catalog entry — was replaced by a placeholder when the text was
    /// lifted out of C#, so the prefix itself now belongs only to whatever substitutes the
    /// placeholder's value, never to the template text.
    /// </summary>
    [Fact]
    public void All_NoDefaultContainsTheLiteralToolPrefix()
    {
        foreach (var hook in HookCatalog.All)
        {
            Assert.False(
                hook.Default.Contains("mcp__team__", StringComparison.Ordinal),
                $"Hook '{hook.Key}' still contains the literal 'mcp__team__' prefix in its Default.");
        }
    }

    [GeneratedRegex(@"\{\{[a-zA-Z]+\}\}")]
    private static partial Regex PlaceholderToken();
}
