using System.Text;
using Agency.Huddle.App.Library;

namespace Agency.Huddle.Tests.Library;

/// <summary>
/// Tests for <see cref="WikiLinkRewriter"/>: Spec §6.5 <i>Rename or move rewrite</i> step 2 (only
/// the target inside <c>[[…]]</c> changes, alias/heading/<c>!</c> are kept) and ADR-0028
/// (a save is byte-exact, so a rewrite must change exactly the link characters).
/// </summary>
public sealed class WikiLinkRewriterTests
{
    /// <summary>Finds the single wikilink whose target equals <paramref name="target"/> in <paramref name="markdown"/>, parsed fresh so its <c>Start</c>/<c>Length</c> are valid.</summary>
    private static WikiLink FindLink(string markdown, string target)
    {
        IReadOnlyList<WikiLink> links = WikiLinkParser.Parse(markdown);
        foreach (WikiLink link in links)
        {
            if (string.Equals(link.Target, target, StringComparison.Ordinal))
            {
                return link;
            }
        }

        throw new InvalidOperationException($"No link with target '{target}' found.");
    }

    /// <summary>Spec §6.5 step 2: only the target changes, alias/heading/<c>!</c> are kept, for every wikilink form.</summary>
    [Theory]
    [InlineData("[[auth]]", "login", "[[login]]")]
    [InlineData("[[auth|the auth spec]]", "login", "[[login|the auth spec]]")]
    [InlineData("[[auth#Flow]]", "login", "[[login#Flow]]")]
    [InlineData("[[auth#Flow|x]]", "login", "[[login#Flow|x]]")]
    [InlineData("![[auth]]", "login", "![[login]]")]
    [InlineData("[[specs/auth]]", "specs/login", "[[specs/login]]")]
    public void Rewrite_Table_ChangesOnlyTheTarget(string token, string newTarget, string expectedToken)
    {
        string markdown = $"Before {token} after.";
        WikiLink link = WikiLinkRewriterTests.FindLink(markdown, WikiLinkParser.Parse(markdown)[0].Target);

        string result = WikiLinkRewriter.Rewrite(markdown, [(link, newTarget)]);

        Assert.Equal($"Before {expectedToken} after.", result);
    }

    /// <summary>Two links in one note; only the one named in the edit list changes.</summary>
    [Fact]
    public void Rewrite_OnlyGivenLinks_Change()
    {
        string markdown = "See [[auth]] and also [[billing]] for details.";
        WikiLink authLink = WikiLinkRewriterTests.FindLink(markdown, "auth");

        string result = WikiLinkRewriter.Rewrite(markdown, [(authLink, "login")]);

        Assert.Equal("See [[login]] and also [[billing]] for details.", result);
    }

    /// <summary>Two links on the same line: applying edits from the highest <c>Start</c> down keeps the lower offset valid.</summary>
    [Fact]
    public void Rewrite_MultipleOnOneLine_OffsetsStayValid()
    {
        string markdown = "[[auth]] then [[auth]] again.";
        IReadOnlyList<WikiLink> links = WikiLinkParser.Parse(markdown);
        Assert.Equal(2, links.Count);

        string result = WikiLinkRewriter.Rewrite(markdown, [(links[0], "login"), (links[1], "signin")]);

        Assert.Equal("[[login]] then [[signin]] again.", result);
    }

    /// <summary>B5 item 2: a target written with <c>.md</c> keeps the extension; one written bare stays bare.</summary>
    [Fact]
    public void Rewrite_KeepsWriterExtensionStyle()
    {
        string withExtension = "[[auth.md]]";
        WikiLink linkWithExtension = WikiLinkRewriterTests.FindLink(withExtension, "auth.md");
        string resultWithExtension = WikiLinkRewriter.Rewrite(withExtension, [(linkWithExtension, "login")]);
        Assert.Equal("[[login.md]]", resultWithExtension);

        string bare = "[[auth]]";
        WikiLink linkBare = WikiLinkRewriterTests.FindLink(bare, "auth");
        string resultBare = WikiLinkRewriter.Rewrite(bare, [(linkBare, "login")]);
        Assert.Equal("[[login]]", resultBare);
    }

    /// <summary>A table cell with an escaped pipe keeps the escape bytes; only the target changes.</summary>
    [Fact]
    public void Rewrite_TableCellEscapedPipe_KeepsEscape()
    {
        string markdown = @"| a | [[auth\|x]] |";
        WikiLink link = WikiLinkRewriterTests.FindLink(markdown, "auth");

        string result = WikiLinkRewriter.Rewrite(markdown, [(link, "login")]);

        Assert.Equal(@"| a | [[login\|x]] |", result);
    }

    /// <summary>Whitespace inside the brackets is kept; only the target text between the spaces changes.</summary>
    [Fact]
    public void Rewrite_WhitespaceInsideBrackets_KeepsSpacing()
    {
        string markdown = "[[ auth | x ]]";
        WikiLink link = WikiLinkRewriterTests.FindLink(markdown, "auth");

        string result = WikiLinkRewriter.Rewrite(markdown, [(link, "login")]);

        Assert.Equal("[[ login | x ]]", result);
    }

    /// <summary>
    /// The real path: bytes (UTF-8 BOM, CRLF, frontmatter, trailing spaces) decoded, parsed,
    /// rewritten and re-encoded. The byte diff against the original is exactly the link
    /// characters - the prefix and suffix bytes around the changed span are identical.
    /// </summary>
    [Fact]
    public void Rewrite_PreservesEverythingElse()
    {
        string editorText = "---\ntitle: Note\n---\n\nSee [[auth]] for details.   \n";
        byte[] originalBytes = new UTF8Encoding(true).GetBytes(editorText.Replace("\n", "\r\n", StringComparison.Ordinal));

        Assert.True(TextFileCodec.TryDecode(originalBytes, LineEnding.Lf, out string? decoded, out TextFileFormat? format));
        WikiLink link = WikiLinkRewriterTests.FindLink(decoded, "auth");

        string rewritten = WikiLinkRewriter.Rewrite(decoded, [(link, "login")]);
        byte[] newBytes = TextFileCodec.Encode(rewritten, format);

        int prefixLength = 0;
        while (prefixLength < originalBytes.Length && prefixLength < newBytes.Length && originalBytes[prefixLength] == newBytes[prefixLength])
        {
            prefixLength++;
        }

        int suffixLength = 0;
        while (suffixLength < originalBytes.Length - prefixLength
            && suffixLength < newBytes.Length - prefixLength
            && originalBytes[^(suffixLength + 1)] == newBytes[^(suffixLength + 1)])
        {
            suffixLength++;
        }

        byte[] oldMiddle = originalBytes[prefixLength..^suffixLength];
        byte[] newMiddle = newBytes[prefixLength..^suffixLength];
        Assert.Equal("auth", new UTF8Encoding(false).GetString(oldMiddle));
        Assert.Equal("login", new UTF8Encoding(false).GetString(newMiddle));

        byte[] oldPrefix = originalBytes[..prefixLength];
        byte[] newPrefix = newBytes[..prefixLength];
        Assert.Equal(oldPrefix, newPrefix);

        byte[] oldSuffix = originalBytes[^suffixLength..];
        byte[] newSuffix = newBytes[^suffixLength..];
        Assert.Equal(oldSuffix, newSuffix);
    }

    /// <summary>A <see cref="WikiLink"/> whose recorded position no longer matches the text (stale, from before another edit) is rejected rather than silently corrupting the text.</summary>
    [Fact]
    public void Rewrite_StalePosition_Throws()
    {
        string markdown = "[[auth]]";
        WikiLink staleLink = WikiLinkRewriterTests.FindLink(markdown, "auth") with { Start = 2 };

        Assert.Throws<ArgumentException>(() => WikiLinkRewriter.Rewrite(markdown, [(staleLink, "login")]));
    }

    /// <summary>Two edits whose spans overlap are rejected: applying one would invalidate the other's recorded position.</summary>
    [Fact]
    public void Rewrite_OverlappingEdits_Throws()
    {
        string markdown = "[[auth]]";
        WikiLink link = WikiLinkRewriterTests.FindLink(markdown, "auth");
        WikiLink overlapping = link with { Length = link.Length - 1 };

        Assert.Throws<ArgumentException>(() => WikiLinkRewriter.Rewrite(markdown, [(link, "login"), (overlapping, "signin")]));
    }

    /// <summary>Duplicate edits for the same link position are rejected.</summary>
    [Fact]
    public void Rewrite_DuplicateEdits_Throws()
    {
        string markdown = "[[auth]]";
        WikiLink link = WikiLinkRewriterTests.FindLink(markdown, "auth");

        Assert.Throws<ArgumentException>(() => WikiLinkRewriter.Rewrite(markdown, [(link, "login"), (link, "signin")]));
    }

    /// <summary>No edits: the exact same string instance is returned, never a copy.</summary>
    [Fact]
    public void Rewrite_EmptyEdits_ReturnsSameInstance()
    {
        string markdown = "[[auth]]";

        string result = WikiLinkRewriter.Rewrite(markdown, []);

        Assert.Same(markdown, result);
    }
}
