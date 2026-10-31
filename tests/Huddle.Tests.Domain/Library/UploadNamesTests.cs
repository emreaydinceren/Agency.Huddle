using Agency.Huddle.App.Library;
using Microsoft.Extensions.Time.Testing;

namespace Agency.Huddle.Tests.Library;

/// <summary>Tests for <see cref="UploadNames"/>: the clean-up of a browser's file name for an upload (spec 6.3 Names).</summary>
public sealed class UploadNamesTests
{
    /// <summary>The clock every test reads: 9:05:03 on 12 October 2026, in UTC.</summary>
    private static readonly DateTimeOffset FixedNow = new(2026, 10, 12, 9, 5, 3, TimeSpan.Zero);

    /// <summary>A name the browser sends already valid is kept as it is, spaces included.</summary>
    [Theory]
    [InlineData("plan.md", "plan.md")]
    [InlineData("Launch Q4 plan.docx", "Launch Q4 plan.docx")]
    [InlineData("image (2).png", "image (2).png")]
    public void Clean_ValidName_IsKept(string browserName, string expected)
    {
        string name = UploadNames.Clean(browserName, CreateClock());

        Assert.Equal(expected, name);
    }

    /// <summary>The directory part is stripped, whichever separator the browser's path used.</summary>
    [Theory]
    [InlineData("C:\\Users\\me\\Pictures\\photo.jpg", "photo.jpg")]
    [InlineData("/tmp/a/notes.txt", "notes.txt")]
    [InlineData("folder\\sub/deep.md", "deep.md")]
    public void Clean_PathWithDirectories_KeepsOnlyTheFileName(string browserName, string expected)
    {
        string name = UploadNames.Clean(browserName, CreateClock());

        Assert.Equal(expected, name);
    }

    /// <summary>Each character LibraryNames refuses becomes a dash, and control characters too.</summary>
    [Theory]
    [InlineData("a:b*c?.md", "a-b-c-.md")]
    [InlineData("a\"b<c>d|e.txt", "a-b-c-d-e.txt")]
    [InlineData("run\u0001me.txt", "run-me.txt")]
    public void Clean_RefusedCharacters_BecomeDashes(string browserName, string expected)
    {
        string name = UploadNames.Clean(browserName, CreateClock());

        Assert.Equal(expected, name);
    }

    /// <summary>A backtick becomes a dash, because it would end the code span the path is sent in (spec 6.3).</summary>
    [Theory]
    [InlineData("a`b.md", "a-b.md")]
    [InlineData("``x``.txt", "-x-.txt")]
    public void Clean_Backtick_BecomesADash(string browserName, string expected)
    {
        string name = UploadNames.Clean(browserName, CreateClock());

        Assert.Equal(expected, name);
    }

    /// <summary>Runs of dashes collapse to one, whether they were made by replacement or were in the name.</summary>
    [Theory]
    [InlineData("a::b.txt", "a-b.txt")]
    [InlineData("a---b.txt", "a-b.txt")]
    public void Clean_DashRuns_CollapseToOne(string browserName, string expected)
    {
        string name = UploadNames.Clean(browserName, CreateClock());

        Assert.Equal(expected, name);
    }

    /// <summary>Trailing dots and spaces are trimmed, because Windows does not keep them.</summary>
    [Theory]
    [InlineData("notes.", "notes")]
    [InlineData("report.pdf  ", "report.pdf")]
    public void Clean_TrailingDotsAndSpaces_AreTrimmed(string browserName, string expected)
    {
        string name = UploadNames.Clean(browserName, CreateClock());

        Assert.Equal(expected, name);
    }

    /// <summary>A stem longer than 100 characters is cut to 100, and the extension is kept.</summary>
    [Fact]
    public void Clean_LongStem_CutTo100KeepingExtension()
    {
        string longStem = new('a', 150);

        string name = UploadNames.Clean(longStem + ".txt", CreateClock());

        Assert.Equal(new string('a', 100) + ".txt", name);
    }

    /// <summary>A stem that is a Windows device name becomes file, and the extension stays.</summary>
    [Theory]
    [InlineData("CON", "file")]
    [InlineData("con.md", "file.md")]
    [InlineData("aux", "file")]
    [InlineData("LPT9.txt", "file.txt")]
    [InlineData("CON.tar.gz", "file.tar.gz")]
    public void Clean_DeviceNameStem_BecomesFile(string browserName, string expected)
    {
        string name = UploadNames.Clean(browserName, CreateClock());

        Assert.Equal(expected, name);
    }

    /// <summary>A name that is only dots becomes file once its trailing dots are trimmed away.</summary>
    [Fact]
    public void Clean_OnlyDots_BecomesFile()
    {
        string name = UploadNames.Clean("...", CreateClock());

        Assert.Equal("file", name);
    }

    /// <summary>A clipboard image with no name becomes the timestamped name, in the Human's local time.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("image.png")]
    [InlineData("IMAGE.PNG")]
    public void Clean_NoNameOrGenericPasteName_BecomesTimestampedPng(string? browserName)
    {
        string name = UploadNames.Clean(browserName, CreateClock());

        Assert.Equal("pasted-image-20261012-090503.png", name);
    }

    /// <summary>A cleaned name is always one LibraryNames accepts, so the byte writer does not refuse it.</summary>
    [Theory]
    [InlineData("a:b*c?.md")]
    [InlineData("CON.txt")]
    [InlineData("...")]
    [InlineData("notes.")]
    public void Clean_Output_PassesLibraryNamesValidate(string browserName)
    {
        string name = UploadNames.Clean(browserName, CreateClock());

        Assert.Null(LibraryNames.Validate(name));
    }

    /// <summary>FreeName puts the number before the extension, so the extension still names the kind.</summary>
    [Theory]
    [InlineData("plan.md", 2, "plan (2).md")]
    [InlineData("plan.md", 3, "plan (3).md")]
    [InlineData("Launch Q4", 2, "Launch Q4 (2)")]
    [InlineData("archive.tar.gz", 2, "archive.tar (2).gz")]
    public void FreeName_Number_InsertsBeforeExtension(string name, int n, string expected)
    {
        string free = UploadNames.FreeName(name, n);

        Assert.Equal(expected, free);
    }

    /// <summary>FreeName numbers from 2, because the first copy keeps the plain name.</summary>
    [Fact]
    public void FreeName_BelowTwo_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => UploadNames.FreeName("plan.md", 1));
    }

    /// <summary>Builds the fake clock every test reads.</summary>
    /// <returns>A clock fixed at <see cref="FixedNow"/> in UTC.</returns>
    private static FakeTimeProvider CreateClock()
    {
        FakeTimeProvider clock = new(FixedNow);
        clock.SetLocalTimeZone(TimeZoneInfo.Utc);
        return clock;
    }
}
