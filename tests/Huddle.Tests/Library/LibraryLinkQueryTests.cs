namespace Agency.Huddle.App.Library;

/// <summary>Tests for <see cref="LibraryLinkQuery"/>.</summary>
public sealed class LibraryLinkQueryTests
{
    /// <summary>The raw value is returned still percent-encoded, whatever else is in the query.</summary>
    [Theory]
    [InlineData("http://h/rooms/x?library=notes%2Fa.md", "notes%2Fa.md")]
    [InlineData("http://h/rooms/x?a=1&library=notes%2Fa.md&b=2", "notes%2Fa.md")]
    [InlineData("http://h/rooms/x?library=notes%2Fa.md#top", "notes%2Fa.md")]
    [InlineData("http://h/rooms/x?other=1", null)]
    [InlineData("http://h/rooms/x", null)]
    public void ExtractRawValue_FindsTheLibraryParameter(string location, string? expected)
    {
        Assert.Equal(expected, LibraryLinkQuery.ExtractRawValue(location));
    }

    /// <summary>Stripping removes only the library parameter and keeps the other parameters and the fragment.</summary>
    [Theory]
    [InlineData("http://h/rooms/x?library=a", "http://h/rooms/x")]
    [InlineData("http://h/rooms/x?a=1&library=a&b=2", "http://h/rooms/x?a=1&b=2")]
    [InlineData("http://h/rooms/x?library=a#top", "http://h/rooms/x#top")]
    [InlineData("http://h/rooms/x", "http://h/rooms/x")]
    public void Strip_RemovesOnlyTheLibraryParameter(string uri, string expected)
    {
        Assert.Equal(expected, LibraryLinkQuery.Strip(uri));
    }
}
