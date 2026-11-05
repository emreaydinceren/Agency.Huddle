using Agency.Huddle.App.AgentsMdChanges;

namespace Agency.Huddle.Tests.AgentsMdChanges;

/// <summary>
/// Pins <see cref="AgentsMdBody.Parse"/> (Spec §6.1): the hash covers the text an Agent receives, so a frontmatter
/// edit, a byte-order mark, a line-ending change or trailing whitespace is not a change, while an inner edit is.
/// </summary>
public sealed class AgentsMdBodyTests
{
    /// <summary>A frontmatter block is not part of the hash: the same body with other settings hashes the same.</summary>
    [Fact]
    public void Parse_FrontmatterIsNotPartOfTheHash()
    {
        AgentsMdBody withFrontmatter = AgentsMdBody.Parse("---\nmodel: a\n---\nRule one.\n");
        AgentsMdBody plain = AgentsMdBody.Parse("Rule one.\n");

        Assert.Equal("Rule one.", withFrontmatter.Text);
        Assert.Equal(plain.Hash, withFrontmatter.Hash);
    }

    /// <summary>A leading byte-order mark is removed before hashing, so it does not change the hash.</summary>
    [Fact]
    public void Parse_ByteOrderMark_SameHash()
    {
        AgentsMdBody withMark = AgentsMdBody.Parse("﻿Rule one.");
        AgentsMdBody plain = AgentsMdBody.Parse("Rule one.");

        Assert.Equal("Rule one.", withMark.Text);
        Assert.Equal(plain.Hash, withMark.Hash);
    }

    /// <summary>CR LF and LF are normalised to LF before hashing, so a line-ending change alone is not a change.</summary>
    [Fact]
    public void Parse_CrLfAndLf_SameHash()
    {
        AgentsMdBody crLf = AgentsMdBody.Parse("Rule one.\r\nRule two.\r\n");
        AgentsMdBody lf = AgentsMdBody.Parse("Rule one.\nRule two.\n");

        Assert.Equal("Rule one.\nRule two.", crLf.Text);
        Assert.Equal(lf.Hash, crLf.Hash);
    }

    /// <summary>Whitespace at the end of the text is trimmed before hashing, so it does not change the hash.</summary>
    [Fact]
    public void Parse_TrailingWhitespace_SameHash()
    {
        AgentsMdBody padded = AgentsMdBody.Parse("Rule one.  \n\t\n\n");
        AgentsMdBody plain = AgentsMdBody.Parse("Rule one.");

        Assert.Equal("Rule one.", padded.Text);
        Assert.Equal(plain.Hash, padded.Hash);
    }

    /// <summary>Text that is only whitespace is absent: it has the empty text and the empty hash.</summary>
    [Fact]
    public void Parse_WhitespaceOnly_IsAbsent()
    {
        AgentsMdBody body = AgentsMdBody.Parse(" \r\n\t \n");

        Assert.Equal(AgentsMdBody.Absent, body);
        Assert.Equal(string.Empty, body.Hash);
    }

    /// <summary>A file that holds only a frontmatter block is absent, because its body is empty.</summary>
    [Fact]
    public void Parse_FrontmatterOnly_IsAbsent()
    {
        AgentsMdBody body = AgentsMdBody.Parse("---\nmodel: a\n---\n");

        Assert.Equal(AgentsMdBody.Absent, body);
    }

    /// <summary>A change inside the text, even one space, is a different hash.</summary>
    [Fact]
    public void Parse_InnerWhitespaceChange_DifferentHash()
    {
        AgentsMdBody original = AgentsMdBody.Parse("Rule one.");
        AgentsMdBody changed = AgentsMdBody.Parse("Rule  one.");

        Assert.NotEqual(original.Hash, changed.Hash);
    }
}
