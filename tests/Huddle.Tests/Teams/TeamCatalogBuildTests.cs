using Agency.Huddle.App.Teams;
using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.Tests.Teams;

/// <summary>Tests for TeamCatalog.Build pure projection.</summary>
public sealed class TeamCatalogBuildTests
{
    private static readonly string[] Business_Empty_Empty_False = ["Business|projects=|members=|folder=False"];
    private static readonly string[] Business_Empty_Empty_True = ["Business|projects=|members=|folder=True"];
    private static readonly string[] B_Empty_Empty_False = ["B|projects=|members=|folder=False"];
    private static readonly string[] BusinessHousehold_Members =
    [
        "Business|projects=|members=Ada,Nova|folder=False",
        "Household|projects=|members=Ada|folder=False"
    ];
    private static readonly string[] Business_Nova = ["Business|projects=|members=Nova|folder=False"];
    private static readonly string[] Business_Alpha_Zeta = ["Business|projects=alpha,Zeta|members=|folder=True"];
    private static readonly string[] Alpha_Business_Household =
    [
        "alpha|projects=|members=|folder=False",
        "Business|projects=|members=|folder=False",
        "household|projects=|members=|folder=False"
    ];
    private static readonly string[] Ops_Nova = ["Ops|projects=|members=Nova|folder=False"];
    private static readonly string[] Business_A_B_True = ["Business|projects=A,B|members=|folder=True"];

    /// <summary>All inputs empty yields an empty list.</summary>
    [Fact]
    public void Build_AllInputsEmpty_ReturnsEmptyList()
    {
        IReadOnlyList<string> labels = [];
        IReadOnlyList<(string Persona, IReadOnlyList<string> Teams)> members = [];
        IReadOnlyList<TeamFolder> folders = [];

        IReadOnlyList<TeamSummary> result = TeamCatalog.Build(labels, members, folders);

        Assert.Equal([], result);
    }

    /// <summary>Label with no folder creates summary with no projects or members.</summary>
    [Fact]
    public void Build_LabelWithNoFolder_ReturnsSummaryWithEmptyProjectsAndMembers()
    {
        IReadOnlyList<string> labels = ["Business"];
        IReadOnlyList<(string Persona, IReadOnlyList<string> Teams)> members = [];
        IReadOnlyList<TeamFolder> folders = [];

        IReadOnlyList<TeamSummary> result = TeamCatalog.Build(labels, members, folders);

        Assert.Equal(Business_Empty_Empty_False, Describe(result));
    }

    /// <summary>Folder with matching label uses folder's spelling for Name.</summary>
    [Fact]
    public void Build_FolderWithMatchingLabel_UsesFolderSpellingForName()
    {
        IReadOnlyList<string> labels = ["business"];
        IReadOnlyList<(string Persona, IReadOnlyList<string> Teams)> members = [];
        IReadOnlyList<TeamFolder> folders = [new("Business", [], false)];

        IReadOnlyList<TeamSummary> result = TeamCatalog.Build(labels, members, folders);

        Assert.Equal(Business_Empty_Empty_True, Describe(result));
    }

    /// <summary>Multiple labels with no folder uses first in ordinal order.</summary>
    [Fact]
    public void Build_MultipleLabelsDifferentCase_UsesFirstInOrdinalOrder()
    {
        IReadOnlyList<string> labels = ["b", "B"];
        IReadOnlyList<(string Persona, IReadOnlyList<string> Teams)> members = [];
        IReadOnlyList<TeamFolder> folders = [];

        IReadOnlyList<TeamSummary> result = TeamCatalog.Build(labels, members, folders);

        Assert.Equal(B_Empty_Empty_False, Describe(result));
    }

    /// <summary>Members in teams are deduplicated by case and sorted.</summary>
    [Fact]
    public void Build_MultipleMembersInTeams_DeduplicatesByCaseAndSorts()
    {
        IReadOnlyList<string> labels = ["Business", "Household"];
        IReadOnlyList<(string Persona, IReadOnlyList<string> Teams)> members =
        [
            ("Nova", (IReadOnlyList<string>)["Business"]),
            ("Ada", (IReadOnlyList<string>)["business", "Household"]),
            ("Kim", (IReadOnlyList<string>)[])
        ];
        IReadOnlyList<TeamFolder> folders = [];

        IReadOnlyList<TeamSummary> result = TeamCatalog.Build(labels, members, folders);

        Assert.Equal(BusinessHousehold_Members, Describe(result));
    }

    /// <summary>Persona with duplicate team memberships appears once.</summary>
    [Fact]
    public void Build_PersonaWithDuplicateTeams_AppearsOnce()
    {
        IReadOnlyList<string> labels = ["Business"];
        IReadOnlyList<(string Persona, IReadOnlyList<string> Teams)> members =
        [
            ("Nova", (IReadOnlyList<string>)["Business", "business"])
        ];
        IReadOnlyList<TeamFolder> folders = [];

        IReadOnlyList<TeamSummary> result = TeamCatalog.Build(labels, members, folders);

        Assert.Equal(Business_Nova, Describe(result));
    }

    /// <summary>Folder projects are deduplicated and sorted, excluding reserved names.</summary>
    [Fact]
    public void Build_FolderProjectsWithReservedNames_FiltersAndSorts()
    {
        IReadOnlyList<string> labels = ["Business"];
        IReadOnlyList<(string Persona, IReadOnlyList<string> Teams)> members = [];
        IReadOnlyList<TeamFolder> folders =
        [
            new("Business", (IReadOnlyList<string>)["Zeta", "alpha", "memory", "Memory", "_x", ".y"], false)
        ];

        IReadOnlyList<TeamSummary> result = TeamCatalog.Build(labels, members, folders);

        Assert.Equal(Business_Alpha_Zeta, Describe(result));
    }

    /// <summary>Multiple labels are returned in ordinal order.</summary>
    [Fact]
    public void Build_MultipleLabelsDifferentCasing_ReturnsInOrdinalOrder()
    {
        IReadOnlyList<string> labels = ["household", "Business", "alpha"];
        IReadOnlyList<(string Persona, IReadOnlyList<string> Teams)> members = [];
        IReadOnlyList<TeamFolder> folders = [];

        IReadOnlyList<TeamSummary> result = TeamCatalog.Build(labels, members, folders);

        Assert.Equal(Alpha_Business_Household, Describe(result));
    }

    /// <summary>Team in member list but not in labels is still listed.</summary>
    [Fact]
    public void Build_TeamInMembersButNotLabels_IsStillListed()
    {
        IReadOnlyList<string> labels = [];
        IReadOnlyList<(string Persona, IReadOnlyList<string> Teams)> members =
        [
            ("Nova", (IReadOnlyList<string>)["Ops"])
        ];
        IReadOnlyList<TeamFolder> folders = [];

        IReadOnlyList<TeamSummary> result = TeamCatalog.Build(labels, members, folders);

        Assert.Equal(Ops_Nova, Describe(result));
    }

    /// <summary>Orphan folder without any label or member reference is still listed.</summary>
    [Fact]
    public void Build_OrphanFolder_IsListed()
    {
        IReadOnlyList<string> labels = [];
        IReadOnlyList<(string Persona, IReadOnlyList<string> Teams)> members = [];
        IReadOnlyList<TeamFolder> folders = [new("Business", [], true)];

        IReadOnlyList<TeamSummary> result = TeamCatalog.Build(labels, members, folders);

        Assert.Equal(Business_Empty_Empty_True, Describe(result));
    }

    /// <summary>Two folders differing only by case merge into one summary with combined projects.</summary>
    [Fact]
    public void Build_TwoFoldersDifferentCase_MergeWithCombinedProjects()
    {
        IReadOnlyList<string> labels = [];
        IReadOnlyList<(string Persona, IReadOnlyList<string> Teams)> members = [];
        IReadOnlyList<TeamFolder> folders =
        [
            new("Business", (IReadOnlyList<string>)["A"], false),
            new("business", (IReadOnlyList<string>)["B"], false)
        ];

        IReadOnlyList<TeamSummary> result = TeamCatalog.Build(labels, members, folders);

        Assert.Equal(Business_A_B_True, Describe(result));
    }

    private static string[] Describe(IReadOnlyList<TeamSummary> teams)
    {
        return teams.Select(
            t => $"{t.Name}|projects={string.Join(",", t.Projects)}|members={string.Join(",", t.Members)}|folder={t.HasFolder}")
            .ToArray();
    }
}
