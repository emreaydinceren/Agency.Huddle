using Agency.Huddle.App.Library;

namespace Agency.Huddle.Tests.Library;

/// <summary>Pins the property text rules of D-11: bidi controls are removed and a long value is cut without splitting a surrogate pair.</summary>
public sealed class NotePropertyTextTests
{
    /// <summary>Fix 11: an emoji whose two halves straddle the 2,000-character boundary is dropped whole, never cut in half.</summary>
    [Fact]
    public void Prepare_CutBetweenSurrogates_CutsOneEarlier()
    {
        string text = new string('x', 1999) + "\U0001F600" + "tail";

        string prepared = NotePropertyText.Prepare(text);

        Assert.Equal(new string('x', 1999) + "…", prepared);
    }

    /// <summary>The cut keeps a whole emoji that ends exactly at the boundary.</summary>
    [Fact]
    public void Prepare_EmojiEndingAtTheBoundary_IsKept()
    {
        string text = new string('x', 1998) + "\U0001F600" + "tail";

        Assert.Equal(new string('x', 1998) + "\U0001F600…", NotePropertyText.Prepare(text));
    }

    /// <summary>A value at the limit is returned whole, and bidi controls are removed.</summary>
    [Fact]
    public void Prepare_ShortValue_OnlyLosesBidiControls()
    {
        Assert.Equal("ab", NotePropertyText.Prepare("a\u202Eb"));
        Assert.Equal(new string('y', 2000), NotePropertyText.Prepare(new string('y', 2000)));
    }
}
