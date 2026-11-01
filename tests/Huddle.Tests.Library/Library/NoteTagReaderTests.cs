using Agency.Huddle.App.Library;

namespace Agency.Huddle.Tests.Library;

/// <summary>Pins the frontmatter <c>tags</c> forms and the frontmatter boundary of Spec §6.17.</summary>
public sealed class NoteTagReaderTests
{
    private static string Read(string block) => string.Join("|", NoteTagReader.Read(block, NoteFrontmatterForm.Yaml));

    /// <summary>Every accepted way of writing the list.</summary>
    [Theory]
    [InlineData("tags:\n- a\n- b", "a|b")]
    [InlineData("tags:\n  - a\n  - b", "a|b")]
    [InlineData("tags: [a, \"b\"]", "a|b")]
    [InlineData("tags: a, b", "a|b")]
    [InlineData("tags: a", "a")]
    [InlineData("tags: [\"#a\"]", "a")]
    [InlineData("tags:\n- \"#a\"", "a")]
    [InlineData("tags: a\ntags: b", "b")]
    [InlineData("tags: [a,\n  b]", "a|b")]
    [InlineData("tags: [\"null\", true]", "null")]
    public void Read_Forms(string block, string expected) => Assert.Equal(expected, Read(block));

    /// <summary>A value that would end an inline tag, or is numeric, is skipped; its siblings still count.</summary>
    [Theory]
    [InlineData("tags: [a.b, c++, d]", "d")]
    [InlineData("tags: [\"a, b\"]", "")]
    [InlineData("tags: [1984]", "")]
    [InlineData("tags: [PLAT-0042, x]", "x")]
    [InlineData("tags:\n- #a\n- b", "b")]
    public void Read_SkipsOnlyTheBadValue(string block, string expected) => Assert.Equal(expected, Read(block));

    /// <summary>An unquoted <c>#</c> is a YAML comment, so these give no tags.</summary>
    [Theory]
    [InlineData("tags: #a")]
    [InlineData("tags:\n- #a")]
    [InlineData("tags: [#a]")]
    public void Read_UnquotedHash_GivesNothing(string block) => Assert.Equal(string.Empty, Read(block));

    /// <summary>A double-quoted tag decodes <c>\uXXXX</c> as the property parser does, in the scalar, flow and block forms alike.</summary>
    [Theory]
    [InlineData("tags: \"caf\\u00e9\"", "café")]
    [InlineData("tags: [\"caf\\u00e9\", b]", "café|b")]
    [InlineData("tags:\n- \"caf\\u00e9\"", "café")]
    public void Read_DoubleQuotedUnicodeEscape_IsDecoded(string block, string expected) => Assert.Equal(expected, Read(block));

    /// <summary>A double-quoted <c>\t</c> is a tab, which is not a tag character, so the value is skipped rather than read as <c>atb</c>.</summary>
    [Theory]
    [InlineData("tags: \"a\\tb\"")]
    [InlineData("tags: [\"a\\tb\"]")]
    [InlineData("tags:\n- \"a\\tb\"")]
    public void Read_DoubleQuotedTabEscape_IsNotATag(string block) => Assert.Equal(string.Empty, Read(block));

    /// <summary>A key that is not exactly <c>tags</c>, a malformed entry or a map gives no tags.</summary>
    [Theory]
    [InlineData("tag: a")]
    [InlineData("Tags: a")]
    [InlineData("tags: [a")]
    [InlineData("tags: \"a")]
    [InlineData("tags: a\n\tx: y")]
    [InlineData("tags: null")]
    [InlineData("tags: true")]
    [InlineData("tags:\n- false")]
    [InlineData("tags: 1e5")]
    [InlineData("tags: [\"a\"x, b]")]
    [InlineData("tags:\n  a: b")]
    [InlineData("tags: 5")]
    [InlineData("tags: {a: b}")]
    public void Read_NotTags_GivesNothing(string block) => Assert.Equal(string.Empty, Read(block));

    /// <summary>The frontmatter boundary: BOM, CRLF and a <c>...</c> closer all work.</summary>
    [Theory]
    [InlineData("﻿---\ntags: a\n---\nbody")]
    [InlineData("---\r\ntags: a\r\n---\r\nbody")]
    [InlineData("---\ntags: a\n...\nbody")]
    public void Split_FindsBlock(string text)
    {
        (string? block, int bodyStart) = NoteFrontmatter.Split(text);

        Assert.NotNull(block);
        Assert.Equal("a", Read(block));
        Assert.Equal("body", text[bodyStart..]);
    }

    /// <summary>A delimiter not on line 1, or a block that never closes, is not frontmatter.</summary>
    [Theory]
    [InlineData("x\n---\ntags: a\n---\n")]
    [InlineData("---\ntags: a\n")]
    [InlineData("")]
    public void Split_NoBlock(string text)
    {
        (string? block, int bodyStart) = NoteFrontmatter.Split(text);

        Assert.Null(block);
        Assert.Equal(0, bodyStart);
    }

    /// <summary>Frontmatter and body tags merge, a tag written two ways counts once, and a fence inside the block cannot hide a body tag.</summary>
    [Theory]
    [InlineData("---\ntags: [Inbox]\n---\n#inbox #Work", "Inbox|Work")]
    [InlineData("---\n```\n---\n#b", "b")]
    [InlineData("---\n#a\n", "a")]
    [InlineData("---\ntags: a\n\tx: y\n---\n#b", "b")]
    [InlineData("---\n    ```\n---\n#b", "b")]
    public void Of_MergesBothSources(string text, string expected) => Assert.Equal(expected, string.Join("|", NoteTags.Of(text)));

    /// <summary>The lenient fence rule: a BOM, spaces around a fence line and a <c>...</c> closer all still give the block, and the body starts after the closing line.</summary>
    /// <param name="opening">The first line.</param>
    /// <param name="closing">The closing line.</param>
    [Theory]
    [InlineData("---", "---")]
    [InlineData("\uFEFF---", "---")]
    [InlineData("---", "...")]
    [InlineData("  ---  ", "---")]
    [InlineData("---", "  ...  ")]
    [InlineData("\uFEFF  ---  ", "  ...  ")]
    public void Split_LenientFences_FindTheBlock(string opening, string closing)
    {
        string text = opening + "\ntags: a\n" + closing + "\nbody\n";

        (string? block, int bodyStart) = NoteFrontmatter.Split(text);

        Assert.Equal("tags: a\n", block);
        Assert.Equal("body\n", text[bodyStart..]);
    }

    /// <summary>A block that never closes is not frontmatter, whatever the opening line looks like; a near miss is not a fence.</summary>
    /// <param name="text">The note.</param>
    [Theory]
    [InlineData("\uFEFF---\ntags: a\n")]
    [InlineData("  ---  \ntags: a\n")]
    [InlineData("---\ntags: a\n--- x\n")]
    [InlineData("----\ntags: a\n---\n")]
    [InlineData("---\ntags: a\n....\n")]
    public void Split_UnclosedOrNearMiss_IsNotABlock(string text)
    {
        (string? block, int bodyStart) = NoteFrontmatter.Split(text);

        Assert.Null(block);
        Assert.Equal(0, bodyStart);
    }

    /// <summary>A fence line in the body, after the closing line, does not extend the block or start another.</summary>
    /// <param name="opening">The first line.</param>
    /// <param name="closing">The closing line.</param>
    [Theory]
    [InlineData("---", "---")]
    [InlineData("\uFEFF---", "...")]
    [InlineData("  ---  ", "  ---  ")]
    public void Split_FenceInBodyAfterTheClose_IsBody(string opening, string closing)
    {
        string text = opening + "\ntags: a\n" + closing + "\nbody\n---\nlater: x\n...\n";

        (string? block, int bodyStart) = NoteFrontmatter.Split(text);

        Assert.Equal("tags: a\n", block);
        Assert.Equal("body\n---\nlater: x\n...\n", text[bodyStart..]);
    }
}
