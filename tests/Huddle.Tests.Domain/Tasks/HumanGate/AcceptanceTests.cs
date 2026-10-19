using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Agency.Huddle.App.Tasks;
using Acceptance = Agency.Huddle.App.Tasks.HumanGate.Acceptance;
using AcceptanceInfo = Agency.Huddle.App.Tasks.HumanGate.AcceptanceInfo;
using AcceptanceState = Agency.Huddle.App.Tasks.HumanGate.AcceptanceState;
using ContentFingerprint = Agency.Huddle.App.Tasks.HumanGate.ContentFingerprint;

namespace Agency.Huddle.Tests.Tasks.HumanGate;

/// <summary>Tests for <c>Acceptance.Of</c> and <c>ContentFingerprint.Of</c> (Human gate Spec §6.5, D-5).</summary>
public sealed class AcceptanceTests
{
    private const string EnteredNeedsYou = "status: Review → Needs You (Proof 4/4)";

    /// <summary>Builds a Needs You Task whose Change log is the given entries, in order.</summary>
    /// <param name="log">The Change log entries.</param>
    private static TaskItem NeedsYouTask(params ChangeLogEntry[] log) =>
        TestTasks.Make(title: "Write the summary", status: TaskState.NeedsYou, description: "Body text", changeLog: log) with { Action = "Read the summary" };

    /// <summary>The Change log entry that accepts the Task as it is now.</summary>
    /// <param name="task">The Task whose current content is accepted.</param>
    /// <param name="at">When the acceptance was logged.</param>
    private static ChangeLogEntry AcceptedFor(TaskItem task, string at = "2026-10-02T09:00:00Z") =>
        TestTasks.Entry(at, "Emre", "accepted " + ContentFingerprint.Of(task.Title, task.Action, task.Description));

    /// <summary>A Needs You Task accepted at its current content, entered once.</summary>
    private static TaskItem AcceptedTask()
    {
        TaskItem draft = NeedsYouTask();
        return draft with { ChangeLog = [TestTasks.Entry("2026-10-01T09:00:00Z", "Ada", EnteredNeedsYou), AcceptedFor(draft)] };
    }

    /// <summary>The fingerprint is the first twelve hex characters of SHA-256 over title, newline, Action, newline, description.</summary>
    [Fact]
    public void Fingerprint_IsTwelveHexOfLfNormalisedText()
    {
        string expected = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("Title\nDo it\nBody")))[..12];

        string actual = ContentFingerprint.Of("Title", "Do it", "Body");

        Assert.Equal(expected, actual);
        Assert.Equal(12, actual.Length);
    }

    /// <summary>Carriage returns do not change the fingerprint.</summary>
    [Fact]
    public void Fingerprint_CrlfEqualsLf()
    {
        Assert.Equal(ContentFingerprint.Of("Title", "Do it", "one\ntwo\n"), ContentFingerprint.Of("Title", "Do it", "one\r\ntwo\r\n"));
    }

    /// <summary>A missing Action hashes as an empty Action.</summary>
    [Fact]
    public void Fingerprint_NullAction_EqualsEmptyAction()
    {
        Assert.Equal(ContentFingerprint.Of("Title", "", "Body"), ContentFingerprint.Of("Title", null, "Body"));
    }

    /// <summary>The Evidence section is not part of the fingerprint, so logging Evidence does not make an acceptance Stale (corrections-P4 item 2).</summary>
    [Fact]
    public void Fingerprint_IgnoresEvidenceSection()
    {
        string plain = ContentFingerprint.Of("Title", "Do it", "body");

        Assert.Equal(plain, ContentFingerprint.Of("Title", "Do it", "body\n## Evidence\n- 2026-10-02 Ada ran it"));
        Assert.Equal(
            ContentFingerprint.Of("Title", "Do it", "body\n## Notes\nkept"),
            ContentFingerprint.Of("Title", "Do it", "body\n## Evidence\n- 2026-10-02 Ada ran it\n## Notes\nkept"));
    }

    /// <summary>An Evidence heading inside a code fence is ordinary text and still counts.</summary>
    [Fact]
    public void Fingerprint_EvidenceHeadingInsideCodeFence_IsKept()
    {
        Assert.NotEqual(
            ContentFingerprint.Of("Title", "Do it", "```\n```"),
            ContentFingerprint.Of("Title", "Do it", "```\n## Evidence\nx\n```"));
    }

    /// <summary>A Task with no accepted entry has no acceptance.</summary>
    [Fact]
    public void Of_NoAcceptedEntry_IsNone()
    {
        TaskItem task = NeedsYouTask(TestTasks.Entry("2026-10-01T09:00:00Z", "Ada", EnteredNeedsYou));

        Assert.Equal(AcceptanceState.None, Acceptance.Of(task).State);
    }

    /// <summary>An accepted entry after the move into Needs You, carrying the current hash, is Valid with its time and author.</summary>
    [Fact]
    public void Of_MatchingEntryAfterEntry_IsValid()
    {
        TaskItem task = AcceptedTask();

        AcceptanceInfo info = Acceptance.Of(task);

        Assert.Equal(AcceptanceState.Valid, info.State);
        Assert.Equal(DateTimeOffset.Parse("2026-10-02T09:00:00Z", CultureInfo.InvariantCulture), info.At);
        Assert.Equal("Emre", info.By);
        Assert.Equal(ContentFingerprint.Of(task.Title, task.Action, task.Description), info.Hash);
    }

    /// <summary>Changing the title after the acceptance makes it Stale.</summary>
    [Fact]
    public void Of_TitleChanged_IsStale()
    {
        Assert.Equal(AcceptanceState.Stale, Acceptance.Of(AcceptedTask() with { Title = "Another title" }).State);
    }

    /// <summary>Changing the Action after the acceptance makes it Stale.</summary>
    [Fact]
    public void Of_ActionChanged_IsStale()
    {
        Assert.Equal(AcceptanceState.Stale, Acceptance.Of(AcceptedTask() with { Action = "Read it twice" }).State);
    }

    /// <summary>Changing the description after the acceptance makes it Stale.</summary>
    [Fact]
    public void Of_DescriptionChanged_IsStale()
    {
        Assert.Equal(AcceptanceState.Stale, Acceptance.Of(AcceptedTask() with { Description = "Different body" }).State);
    }

    /// <summary>Logging Evidence after the acceptance leaves it Valid.</summary>
    [Fact]
    public void Of_EvidenceAppendedAfterAcceptance_StaysValid()
    {
        Assert.Equal(
            AcceptanceState.Valid,
            Acceptance.Of(AcceptedTask() with { Description = "Body text\n## Evidence\n- 2026-10-02 Ada read it" }).State);
    }

    /// <summary>An acceptance from before the latest move into Needs You no longer counts.</summary>
    [Fact]
    public void Of_AcceptedBeforeLastEntryIntoNeedsYou_IsNone()
    {
        TaskItem draft = NeedsYouTask();
        TaskItem task = draft with
        {
            ChangeLog =
            [
                TestTasks.Entry("2026-10-01T09:00:00Z", "Ada", EnteredNeedsYou),
                AcceptedFor(draft),
                TestTasks.Entry("2026-10-03T09:00:00Z", "Ada", "status: Needs You → Review"),
                TestTasks.Entry("2026-10-04T09:00:00Z", "Ada", EnteredNeedsYou),
            ],
        };

        Assert.Equal(AcceptanceState.None, Acceptance.Of(task).State);
    }

    /// <summary>A Task that is not in Needs You has no acceptance, even with a matching entry.</summary>
    [Fact]
    public void Of_NotInNeedsYou_IsNone()
    {
        Assert.Equal(AcceptanceState.None, Acceptance.Of(AcceptedTask() with { Status = TaskState.Review }).State);
    }

    /// <summary>A move written as an outside edit, with its suffix, still counts as the entry into Needs You.</summary>
    [Fact]
    public void Of_OutsideEditEntryIntoNeedsYou_IsRecognised()
    {
        TaskItem draft = NeedsYouTask();
        TaskItem task = draft with
        {
            ChangeLog =
            [
                TestTasks.Entry("2026-10-01T09:00:00Z", "Emre", "edited outside Huddle: title changed; status: Review → Needs You (Proof 2/2)"),
                AcceptedFor(draft),
            ],
        };

        Assert.Equal(AcceptanceState.Valid, Acceptance.Of(task).State);
    }

    /// <summary>Entries with the same second are ordered by their place in the log, not by time.</summary>
    [Fact]
    public void Of_SameTimestamp_OrdersByLogPosition()
    {
        TaskItem draft = NeedsYouTask();
        TaskItem task = draft with
        {
            ChangeLog =
            [
                TestTasks.Entry("2026-10-01T09:00:00Z", "Ada", EnteredNeedsYou),
                AcceptedFor(draft, "2026-10-01T09:00:00Z"),
            ],
        };

        Assert.Equal(AcceptanceState.Valid, Acceptance.Of(task).State);
    }

    /// <summary>The latest accepted entry decides: an older matching one does not rescue a newer stale one.</summary>
    [Fact]
    public void Of_LatestAcceptedEntryDecides()
    {
        TaskItem draft = NeedsYouTask();
        TaskItem task = draft with
        {
            ChangeLog =
            [
                TestTasks.Entry("2026-10-01T09:00:00Z", "Ada", EnteredNeedsYou),
                AcceptedFor(draft),
                TestTasks.Entry("2026-10-02T10:00:00Z", "Emre", "accepted 000000000000"),
            ],
        };

        Assert.Equal(AcceptanceState.Stale, Acceptance.Of(task).State);
    }
}
