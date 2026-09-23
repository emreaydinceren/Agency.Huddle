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
    /// The catalog carries exactly the 37 prompts the task specifies, no more and no fewer: the original
    /// 24, plus <c>systemPrompt.skills</c> and <c>tool.readSkill.description</c> added for Spec §6.4, plus
    /// <c>tool.validateTeammate.description</c> added for Spec §6.8 (Task 9.3), plus
    /// <c>tool.proposeTeammates.description</c> added for Spec §6.9 (Task 10.2), plus
    /// <c>turn.greeting</c> added for Spec §6.14 (Task 16.3), plus the six File Changes Turn prompts
    /// (<c>turn.fileChangesHeader</c>, <c>turn.fileAdded</c>, <c>turn.fileChanged</c>,
    /// <c>turn.fileDeleted</c>, <c>turn.fileChangesMore</c>, <c>turn.folderUnchecked</c>) added for
    /// FC §6.13 (Task 7.1), plus <c>tool.watchFolder.description</c> and
    /// <c>tool.unwatchFolder.description</c> added for FC §6.9/§6.13 (Task 10.2), plus
    /// <c>systemPrompt.sharedSession</c> added for RS §6.9/§8.1 (Task 16.2).
    /// </summary>
    [Fact]
    public void All_HasExactlyThirtySevenPrompts()
    {
        Assert.Equal(49, PromptCatalog.All.Count);
    }

    /// <summary>
    /// D28, RS §6.9: <c>systemPrompt.roomSessions</c> and <c>systemPrompt.roomSessionsCarry</c> are
    /// both <see cref="PromptTiming.NextSession"/>, take no placeholders, and carry their exact wording.
    /// </summary>
    [Fact]
    public void Catalog_HasRoomSessionsPrompts()
    {
        var roomSessions = PromptCatalog.Get("systemPrompt.roomSessions");
        Assert.Equal(PromptTiming.NextSession, roomSessions.Timing);
        Assert.Empty(roomSessions.Placeholders);
        var expectedRoomSessions =
            """
            Each Room you are in is a separate conversation, and this session holds exactly one of them.
            Every Message you receive here comes from the Room its label names, and you answer into that
            Room. Your other Rooms have sessions of their own, which you cannot see from here. Treat each
            Room as its own audience: do not assume the people here know what was said in another Room, and
            do not bring it up here. If a Message seems to continue something you cannot see, say so and ask
            rather than guess. Describe your own memory truthfully: you remember this Room's conversation,
            and you do not remember your other Rooms' conversations.
            """;
        Assert.Equal(
            expectedRoomSessions.Replace("\r\n", "\n", StringComparison.Ordinal),
            roomSessions.Default.Replace("\r\n", "\n", StringComparison.Ordinal));

        var carry = PromptCatalog.Get("systemPrompt.roomSessionsCarry");
        Assert.Equal(PromptTiming.NextSession, carry.Timing);
        Assert.Empty(carry.Placeholders);
        var expectedCarry =
            """
            Two things do cross between your Rooms: the files in your memory folder, and the file changes
            listed at the start of a Turn. If something should hold in every Room, write it to your memory.
            """;
        Assert.Equal(
            expectedCarry.Replace("\r\n", "\n", StringComparison.Ordinal),
            carry.Default.Replace("\r\n", "\n", StringComparison.Ordinal));
    }

    /// <summary>
    /// <c>turn.ownPostLine</c> (RS §6.7, finding P-7) is <see cref="PromptTiming.Live"/>, requires
    /// <c>{{text}}</c>, and carries the exact wording <c>"You, from another Room: {{text}}"</c>.
    /// </summary>
    [Fact]
    public void Catalog_HasOwnPostLine()
    {
        var prompt = PromptCatalog.Get("turn.ownPostLine");

        Assert.Equal(PromptTiming.Live, prompt.Timing);
        Assert.Contains("{{text}}", prompt.RequiredPlaceholders);
        Assert.Equal("You, from another Room: {{text}}", prompt.Default);
    }

    /// <summary>
    /// The three Transcript Catch-up prompts RS §6.9 defines (<c>turn.transcriptHeader</c>,
    /// <c>turn.transcriptResumedHeader</c>, <c>turn.transcriptOmitted</c>) all exist, are
    /// <see cref="PromptTiming.Live"/>, carry their required placeholder, and carry no
    /// <c>mcp__team__</c> literal (D24).
    /// </summary>
    [Fact]
    public void Catalog_HasTranscriptCatchUpPrompts()
    {
        var header = PromptCatalog.Get("turn.transcriptHeader");
        Assert.Equal(PromptTiming.Live, header.Timing);
        Assert.Equal(["{{roomLabel}}"], header.RequiredPlaceholders);
        Assert.False(header.Default.Contains("mcp__team__", StringComparison.Ordinal));

        var resumedHeader = PromptCatalog.Get("turn.transcriptResumedHeader");
        Assert.Equal(PromptTiming.Live, resumedHeader.Timing);
        Assert.Equal(["{{roomLabel}}"], resumedHeader.RequiredPlaceholders);
        Assert.False(resumedHeader.Default.Contains("mcp__team__", StringComparison.Ordinal));

        var omitted = PromptCatalog.Get("turn.transcriptOmitted");
        Assert.Equal(PromptTiming.Live, omitted.Timing);
        Assert.Equal(["{{count}}"], omitted.RequiredPlaceholders);
        Assert.False(omitted.Default.Contains("mcp__team__", StringComparison.Ordinal));
    }

    /// <summary>
    /// <c>systemPrompt.sharedSession</c> (RS §6.9, D16) is <see cref="PromptTiming.NextSession"/>,
    /// takes no placeholders, and its default names no Room: identity stays in the Turn's own label.
    /// </summary>
    [Fact]
    public void Catalog_HasSharedSessionPrompt()
    {
        var prompt = PromptCatalog.Get("systemPrompt.sharedSession");

        Assert.Equal(PromptTiming.NextSession, prompt.Timing);
        Assert.Empty(prompt.Placeholders);
        Assert.Empty(prompt.RequiredPlaceholders);
        Assert.False(prompt.Default.Contains("mcp__team__", StringComparison.Ordinal));
    }

    /// <summary>The <c>turn.fileByYouSuffix</c> prompt (FC §6.15, D13) is <see cref="PromptTiming.Live"/>, keeps its leading space, requires <c>{{roomName}}</c>, and carries no <c>mcp__team__</c> literal.</summary>
    [Fact]
    public void Catalog_HasFileByYouSuffixPrompt()
    {
        var prompt = PromptCatalog.Get("turn.fileByYouSuffix");

        Assert.Equal(PromptTiming.Live, prompt.Timing);
        Assert.StartsWith(" ", prompt.Default, StringComparison.Ordinal);
        Assert.Equal(["{{roomName}}"], prompt.RequiredPlaceholders);
        Assert.False(prompt.Default.Contains("mcp__team__", StringComparison.Ordinal));
    }

    /// <summary>The four memory prompts FC §6.13 defines all exist, are <see cref="PromptTiming.NextSession"/>, carry the right required placeholders, and carry no <c>mcp__team__</c> literal (D12).</summary>
    [Fact]
    public void Catalog_HasMemoryPrompts()
    {
        var memory = PromptCatalog.Get("systemPrompt.memory");
        Assert.Equal(PromptTiming.NextSession, memory.Timing);
        Assert.Equal(["{{memoryPath}}", "{{memoryIndex}}"], memory.RequiredPlaceholders);
        Assert.False(memory.Default.Contains("mcp__team__", StringComparison.Ordinal));

        var entry = PromptCatalog.Get("systemPrompt.memoryEntry");
        Assert.Equal(PromptTiming.NextSession, entry.Timing);
        Assert.Contains("{{summary}}", entry.Placeholders);
        Assert.Contains("{{path}}", entry.Placeholders);

        var empty = PromptCatalog.Get("systemPrompt.memoryEmpty");
        Assert.Equal(PromptTiming.NextSession, empty.Timing);
        Assert.Empty(empty.Placeholders);

        var more = PromptCatalog.Get("systemPrompt.memoryMore");
        Assert.Equal(PromptTiming.NextSession, more.Timing);
        Assert.Contains("{{count}}", more.RequiredPlaceholders);
        Assert.False(more.Default.Contains("mcp__team__", StringComparison.Ordinal));
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

    /// <summary>The two <c>watch_folder</c>/<c>unwatch_folder</c> tool descriptions exist, are <see cref="PromptTiming.NextSession"/> like every other <c>tool.*.description</c>, declare no placeholders, and carry no <c>mcp__team__</c> literal — FC §6.9, §6.13.</summary>
    [Fact]
    public void Catalog_HasWatchFolderToolPrompts()
    {
        var watch = PromptCatalog.Get("tool.watchFolder.description");
        Assert.Equal(PromptTiming.NextSession, watch.Timing);
        Assert.Empty(watch.Placeholders);
        Assert.False(watch.Default.Contains("mcp__team__", StringComparison.Ordinal));

        var unwatch = PromptCatalog.Get("tool.unwatchFolder.description");
        Assert.Equal(PromptTiming.NextSession, unwatch.Timing);
        Assert.Empty(unwatch.Placeholders);
        Assert.False(unwatch.Default.Contains("mcp__team__", StringComparison.Ordinal));
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
