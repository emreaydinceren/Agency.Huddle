using System.Reflection;
using System.Text.RegularExpressions;
using Agency.Huddle.SkillPacks.Checking;

namespace Agency.Huddle.Tests.SkillPacks.Checks;

/// <summary>Pins the check catalog of §6.3: every code in use, the retired list, and that each has a title and a fix hint.</summary>
public sealed partial class CheckCatalogTests
{
    private static readonly string[] PinnedCodes =
    [
        "R001", "R002", "R003", "R004", "R005", "R006", "R007", "R008", "R009", "R010", "R011", "R012", "R013",
        "P001", "P002", "P003", "P004", "P005", "P006", "P007", "P008", "P009",
        "D001", "D002", "D003", "D004", "D005",
        "S001", "S002", "S003", "S004", "S005", "S006", "S007", "S008", "S009", "S010", "S011", "S012",
        "O001", "O002", "O003", "O004", "O005",
        "X001", "X002",
    ];

    private static readonly string[] PinnedWarnings =
    [
        "R006", "R007", "R008", "R009", "R012", "P006", "P007", "D004", "S001", "S007", "S008", "S009", "S010", "S012",
        "O002", "O004", "O005", "X002",
    ];

    /// <summary>The codes in use are exactly the pinned list, in table order.</summary>
    [Fact]
    public void All_Codes_MatchPinnedList()
    {
        Assert.Equal(PinnedCodes, CheckCatalog.All.Select(static info => info.Code));
        Assert.Equal(PinnedWarnings.Order(StringComparer.Ordinal), CheckCatalog.All.Where(static info => info.Severity == Severity.Warning).Select(static info => info.Code).Order(StringComparer.Ordinal));
    }

    /// <summary>No retired code is in use.</summary>
    [Fact]
    public void Retired_Codes_AreNeverInAll()
    {
        Assert.DoesNotContain(CheckCatalog.All, info => CheckCatalog.Retired.Contains(info.Code, StringComparer.Ordinal));
    }

    /// <summary>Every entry has a title and a fix hint, and Find returns it.</summary>
    [Fact]
    public void All_Entries_HaveHintAndTitle()
    {
        Assert.All(CheckCatalog.All, static info =>
        {
            Assert.False(string.IsNullOrWhiteSpace(info.Title));
            Assert.False(string.IsNullOrWhiteSpace(info.FixHint));
            Assert.Same(info, CheckCatalog.Find(info.Code));
        });
        Assert.Null(CheckCatalog.Find("Z999"));
    }

    /// <summary>Codes are unique and are one letter of R, P, D, S, O or X followed by three digits.</summary>
    [Fact]
    public void Codes_AreUniqueAndWellFormed()
    {
        Assert.Equal(CheckCatalog.All.Count, CheckCatalog.All.Select(static info => info.Code).Distinct(StringComparer.Ordinal).Count());
        Assert.All(CheckCatalog.All, static info => Assert.Matches(CodeShape(), info.Code));
    }

    /// <summary>The library references Markdig and the framework only: never the app, the contracts or ASP.NET.</summary>
    [Fact]
    public void Library_ReferencesOnlyMarkdig()
    {
        Assembly library = typeof(CheckCatalog).Assembly;

        string[] references = [.. library.GetReferencedAssemblies().Select(static name => name.Name ?? string.Empty)];

        Assert.Contains("Markdig", references);
        Assert.DoesNotContain(references, static name => name.StartsWith("Agency.Huddle.", StringComparison.Ordinal) || name.StartsWith("Huddle.", StringComparison.Ordinal));
        Assert.DoesNotContain(references, static name => name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
    }

    [GeneratedRegex(@"\A[RPDSOX]\d{3}\z", RegexOptions.CultureInvariant)]
    private static partial Regex CodeShape();
}
