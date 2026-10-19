using Agency.Huddle.App.DocHistory;
using static Agency.Huddle.Tests.DocHistory.DocHistoryTestPaths;

namespace Agency.Huddle.Tests.DocHistory;

/// <summary>Pins <see cref="DocHistoryView"/> (DocHistory spec §6.6, §6.10): the byline's rules, the panel's rows and the History's days.</summary>
public sealed class DocHistoryViewTests
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(5);

    /// <summary>An empty history has no byline.</summary>
    [Fact]
    public void Byline_Empty_IsNull()
    {
        Assert.Null(DocHistoryView.Byline([]));
    }

    /// <summary>The spec's example: writer, editor, reviewer and objector, and the newest time.</summary>
    [Fact]
    public void Byline_EachKind_LandsInItsPart()
    {
        DocByline? byline = DocHistoryView.Byline(
        [
            At(9, 12, "Miriam", DocChangeKind.Created, "Draft"),
            At(10, 30, "Mira", DocChangeKind.Edited, "Figures"),
            At(10, 48, "Ruth", DocChangeKind.Reviewed, "Citations"),
            At(11, 40, "Quinn", DocChangeKind.Objected, "F2"),
        ]);

        Assert.NotNull(byline);
        Assert.Equal("Miriam", byline.Writer);
        Assert.Equal(["Mira"], byline.EditedBy);
        Assert.Equal(["Ruth"], byline.ReviewedBy);
        Assert.Equal(["Quinn"], byline.ObjectedBy);
        Assert.Equal(At(11, 40, "x", DocChangeKind.Edited).At, byline.When);
    }

    /// <summary>With no <c>Created</c> entry, the first entry's actor is the writer.</summary>
    [Fact]
    public void Byline_NoCreated_FirstActorWrites()
    {
        DocByline? byline = DocHistoryView.Byline([At(9, 0, "Mira", DocChangeKind.Edited, "a"), At(10, 0, "Ruth", DocChangeKind.Edited, "b")]);

        Assert.NotNull(byline);
        Assert.Equal("Mira", byline.Writer);
        Assert.Equal(["Ruth"], byline.EditedBy);
    }

    /// <summary>Editors are distinct, case-insensitively, most recent first, and the writer is not also an editor.</summary>
    [Fact]
    public void Byline_EditedBy_DistinctRecentFirstWithoutWriter()
    {
        DocByline? byline = DocHistoryView.Byline(
        [
            At(9, 0, "Miriam", DocChangeKind.Created, "a"),
            At(9, 10, "Mira", DocChangeKind.Edited, "b"),
            At(9, 20, "Ruth", DocChangeKind.Edited, "c"),
            At(9, 30, "mira", DocChangeKind.Edited, "d"),
            At(9, 40, "Miriam", DocChangeKind.Edited, "e"),
        ]);

        Assert.NotNull(byline);
        Assert.Equal(["mira", "Ruth"], byline.EditedBy);
    }

    /// <summary>Three Names show in full; a fourth and more become "+N".</summary>
    [Fact]
    public void JoinNames_CutsAfterThree()
    {
        Assert.Equal("Mira, Ruth, Quinn", DocHistoryView.JoinNames(["Mira", "Ruth", "Quinn"]));
        Assert.Equal("Mira, Ruth, Quinn +2", DocHistoryView.JoinNames(["Mira", "Ruth", "Quinn", "Nova", "Kai"]));
    }

    /// <summary><c>Recent</c> returns the newest first, at most <c>max</c>.</summary>
    [Fact]
    public void Recent_NewestFirstCapped()
    {
        DocHistoryEntry a = At(9, 0, "A", DocChangeKind.Created, "a");
        DocHistoryEntry b = At(10, 0, "B", DocChangeKind.Edited, "b");
        DocHistoryEntry c = At(11, 0, "C", DocChangeKind.Reviewed, "c");

        Assert.Equal([c, b], DocHistoryView.Recent([a, b, c], 2, Window));
    }

    /// <summary>Bare Human saves within the window share one row showing the newest; one outside the window keeps its own.</summary>
    [Fact]
    public void Recent_GroupsCloseHumanSaves()
    {
        DocHistoryEntry early = At(9, 0, "Emre", DocChangeKind.Edited, actorKind: DocActorKind.Human);
        DocHistoryEntry middle = At(9, 30, "Emre", DocChangeKind.Edited, actorKind: DocActorKind.Human);
        DocHistoryEntry late = At(9, 33, "Emre", DocChangeKind.Edited, actorKind: DocActorKind.Human);

        Assert.Equal([late, early], DocHistoryView.Recent([early, middle, late], 10, Window));
    }

    /// <summary>An Agent's edit between two Human saves keeps them apart, and Agent entries never group.</summary>
    [Fact]
    public void Recent_DoesNotGroupAcrossOtherEntriesOrAgents()
    {
        DocHistoryEntry save1 = At(9, 0, "Emre", DocChangeKind.Edited, actorKind: DocActorKind.Human);
        DocHistoryEntry agent1 = At(9, 1, "Mira", DocChangeKind.Edited, "x");
        DocHistoryEntry agent2 = At(9, 2, "Mira", DocChangeKind.Edited, "x");
        DocHistoryEntry save2 = At(9, 3, "Emre", DocChangeKind.Edited, actorKind: DocActorKind.Human);

        Assert.Equal([save2, agent2, agent1, save1], DocHistoryView.Recent([save1, agent1, agent2, save2], 10, Window));
    }

    /// <summary>Days are labelled Today, Yesterday, then the full date, newest first, in the viewer's zone.</summary>
    [Fact]
    public void ByDay_LabelsAndOrdersDays()
    {
        DocHistoryEntry today = At(11, 2, "Mira", DocChangeKind.Edited, "t");
        DocHistoryEntry yesterday = new(new DateTimeOffset(2026, 10, 17, 16, 20, 0, TimeSpan.Zero), "Quinn", DocActorKind.Agent, DocChangeKind.Objected, "y");
        DocHistoryEntry older = new(new DateTimeOffset(2026, 10, 11, 9, 12, 0, TimeSpan.Zero), "Miriam", DocActorKind.Agent, DocChangeKind.Created, "o");

        IReadOnlyList<DocHistoryDay> days = DocHistoryView.ByDay([older, yesterday, today], new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc, Window);

        Assert.Equal(["Today", "Yesterday", "11 October 2026"], days.Select(static day => day.Label));
        Assert.Equal([[today], [yesterday], [older]], days.Select(static day => day.Entries));
    }

    /// <summary>The day boundary is the viewer's midnight, not UTC's.</summary>
    [Fact]
    public void ByDay_UsesTheViewersZone()
    {
        TimeZoneInfo plusTwo = TimeZoneInfo.CreateCustomTimeZone("plus-two", TimeSpan.FromHours(2), "plus-two", "plus-two");
        DocHistoryEntry lateUtc = new(new DateTimeOffset(2026, 10, 17, 23, 0, 0, TimeSpan.Zero), "Mira", DocActorKind.Agent, DocChangeKind.Edited, "x");

        IReadOnlyList<DocHistoryDay> days = DocHistoryView.ByDay([lateUtc], new DateTimeOffset(2026, 10, 18, 9, 0, 0, TimeSpan.Zero), plusTwo, Window);

        Assert.Equal("Today", Assert.Single(days).Label);
    }

    /// <summary>An empty history has no days.</summary>
    [Fact]
    public void ByDay_Empty_HasNoDays()
    {
        Assert.Empty(DocHistoryView.ByDay([], DateTimeOffset.UnixEpoch, TimeZoneInfo.Utc, Window));
    }

    /// <summary>A time reads as "today 11:02", "yesterday 16:20" or "11 Oct" in the viewer's zone.</summary>
    [Fact]
    public void RelativeTime_ReadsTodayYesterdayAndDate()
    {
        DateTimeOffset now = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

        string[] texts =
        [
            DocHistoryView.RelativeTime(new DateTimeOffset(2026, 10, 18, 11, 2, 0, TimeSpan.Zero), now, TimeZoneInfo.Utc),
            DocHistoryView.RelativeTime(new DateTimeOffset(2026, 10, 17, 16, 20, 0, TimeSpan.Zero), now, TimeZoneInfo.Utc),
            DocHistoryView.RelativeTime(new DateTimeOffset(2026, 10, 11, 9, 12, 0, TimeSpan.Zero), now, TimeZoneInfo.Utc),
        ];

        Assert.Equal(["today 11:02", "yesterday 16:20", "11 Oct"], texts);
    }

    /// <summary>A bare Human save is labelled Saved; every other entry carries its kind.</summary>
    [Fact]
    public void KindLabel_NamesTheKind_AndSavedForABareHumanSave()
    {
        string[] labels =
        [
            DocHistoryView.KindLabel(At(9, 0, "Emre", DocChangeKind.Edited, actorKind: DocActorKind.Human)),
            DocHistoryView.KindLabel(At(9, 0, "Emre", DocChangeKind.Edited, "Reworded the intro", DocActorKind.Human)),
            DocHistoryView.KindLabel(At(9, 0, "Mira", DocChangeKind.Created, "x")),
            DocHistoryView.KindLabel(At(9, 0, "Ruth", DocChangeKind.Reviewed, "x")),
            DocHistoryView.KindLabel(At(9, 0, "Quinn", DocChangeKind.Objected, "x")),
        ];

        Assert.Equal(["Saved", "Edited", "Created", "Reviewed", "Objected"], labels);
    }
}
