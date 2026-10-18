using Agency.Huddle.App.Skills;
using System.Text.Json;

namespace Agency.Huddle.Tests.Skills;

/// <summary>
/// Pins Spec §Provenance (<c>Skills/&lt;name&gt;/.source.json</c>): the record round-trips through
/// <see cref="SkillProvenance.Write"/> and <see cref="SkillProvenance.TryRead"/>, its JSON keys and
/// UTC ISO-8601 <c>importedAt</c> match the spec example, and a missing or malformed file reads as absent.
/// </summary>
public sealed class SkillProvenanceTests
{
    /// <summary>The spec example, with a sha256 of a fixed string so the file contents are predictable.</summary>
    private static SkillProvenance Sample()
    {
        return new SkillProvenance(
            "https://github.com/addyosmani/agent-skills",
            "skills/code-review-and-quality",
            "0123456789abcdef0123456789abcdef01234567",
            "9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08",
            "Addy Osmani",
            "MIT",
            new DateTimeOffset(2026, 10, 8, 10, 0, 0, TimeSpan.Zero));
    }

    /// <summary>Creates the skill folder inside a fresh data directory and returns its path.</summary>
    private static string CreateSkillFolder(TempDataDir dir)
    {
        string folder = Path.Combine(dir.Path, "code-review-and-quality");
        Directory.CreateDirectory(folder);
        return folder;
    }

    /// <summary>A record written to a folder is read back equal, field for field.</summary>
    [Fact]
    public void Write_ThenTryRead_RoundTrips()
    {
        using TempDataDir dir = new();
        string folder = CreateSkillFolder(dir);
        SkillProvenance original = Sample();

        original.Write(folder);
        bool found = SkillProvenance.TryRead(folder, out SkillProvenance? read);

        Assert.True(found);
        Assert.Equal(original, read);
    }

    /// <summary>The written JSON has exactly the spec keys, in the spec order.</summary>
    [Fact]
    public void Write_JsonKeys_MatchSpecExample()
    {
        using TempDataDir dir = new();
        string folder = CreateSkillFolder(dir);

        Sample().Write(folder);
        string text = File.ReadAllText(Path.Combine(folder, SkillProvenance.FileName));
        using JsonDocument document = JsonDocument.Parse(text);
        string[] names = document.RootElement.EnumerateObject().Select(property => property.Name).ToArray();

        string[] expected = ["repo", "path", "commit", "sha256", "publisher", "license", "importedAt"];
        Assert.Equal(expected, names);
    }

    /// <summary>An <c>importedAt</c> with a non-zero offset is written as the same instant in UTC with a <c>Z</c> suffix.</summary>
    [Fact]
    public void Write_ImportedAt_IsIsoEightDigitUtcWithZSuffix()
    {
        using TempDataDir dir = new();
        string folder = CreateSkillFolder(dir);
        SkillProvenance source = Sample() with { ImportedAt = new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.FromHours(2)) };

        source.Write(folder);
        string text = File.ReadAllText(Path.Combine(folder, SkillProvenance.FileName));
        using JsonDocument document = JsonDocument.Parse(text);
        string importedAt = document.RootElement.GetProperty("importedAt").GetString() ?? string.Empty;

        Assert.Equal("2026-10-08T10:00:00Z", importedAt);
    }

    /// <summary>The file is indented with LF line endings, never CRLF.</summary>
    [Fact]
    public void Write_Layout_IsIndentedWithLfOnly()
    {
        using TempDataDir dir = new();
        string folder = CreateSkillFolder(dir);

        Sample().Write(folder);
        string text = File.ReadAllText(Path.Combine(folder, SkillProvenance.FileName));

        Assert.Contains("\n  \"repo\": ", text);
        Assert.DoesNotContain("\r", text);
    }

    /// <summary>A second Write replaces the first: the folder keeps one file and reads back the newer record.</summary>
    [Fact]
    public void Write_Twice_ReplacesTheFileAndLeavesNoTempFile()
    {
        using TempDataDir dir = new();
        string folder = CreateSkillFolder(dir);
        SkillProvenance newer = Sample() with { Commit = "fedcba9876543210fedcba9876543210fedcba98" };

        Sample().Write(folder);
        newer.Write(folder);
        bool found = SkillProvenance.TryRead(folder, out SkillProvenance? read);

        Assert.True(found);
        Assert.Equal(newer, read);
        Assert.Equal(SkillProvenance.FileName, Path.GetFileName(Assert.Single(Directory.GetFiles(folder))));
    }

    /// <summary>A folder with no <c>.source.json</c> reads as absent, not as an error.</summary>
    [Fact]
    public void TryRead_MissingFile_ReturnsFalseAndNull()
    {
        using TempDataDir dir = new();
        string folder = CreateSkillFolder(dir);

        bool found = SkillProvenance.TryRead(folder, out SkillProvenance? read);

        Assert.False(found);
        Assert.Null(read);
    }

    /// <summary>A file that is not JSON reads as absent.</summary>
    [Fact]
    public void TryRead_MalformedJson_ReturnsFalseAndNull()
    {
        using TempDataDir dir = new();
        string folder = CreateSkillFolder(dir);
        File.WriteAllText(Path.Combine(folder, SkillProvenance.FileName), "{ \"repo\": ");

        bool found = SkillProvenance.TryRead(folder, out SkillProvenance? read);

        Assert.False(found);
        Assert.Null(read);
    }

    /// <summary>A file missing a required key reads as absent rather than yielding a record with nulls.</summary>
    [Fact]
    public void TryRead_MissingRequiredKey_ReturnsFalseAndNull()
    {
        using TempDataDir dir = new();
        string folder = CreateSkillFolder(dir);
        File.WriteAllText(
            Path.Combine(folder, SkillProvenance.FileName),
            "{ \"repo\": \"https://github.com/x/y\", \"path\": \"skills/a\", \"commit\": \"abc\", \"publisher\": \"P\", \"license\": \"MIT\", \"importedAt\": \"2026-10-08T10:00:00Z\" }");

        bool found = SkillProvenance.TryRead(folder, out SkillProvenance? read);

        Assert.False(found);
        Assert.Null(read);
    }

    /// <summary>An <c>importedAt</c> that is not a date reads as absent.</summary>
    [Fact]
    public void TryRead_ImportedAtNotADate_ReturnsFalseAndNull()
    {
        using TempDataDir dir = new();
        string folder = CreateSkillFolder(dir);
        File.WriteAllText(
            Path.Combine(folder, SkillProvenance.FileName),
            "{ \"repo\": \"r\", \"path\": \"p\", \"commit\": \"c\", \"sha256\": \"s\", \"publisher\": \"P\", \"license\": \"MIT\", \"importedAt\": \"yesterday\" }");

        bool found = SkillProvenance.TryRead(folder, out SkillProvenance? read);

        Assert.False(found);
        Assert.Null(read);
    }
}
