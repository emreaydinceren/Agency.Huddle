using Agency.Huddle.Contracts;

namespace Agency.Huddle.Tests.Chat;

/// <summary>The fresh-start note wording (Spec LR-R21; texts T12 to T14), pinned as a pure function of the note.</summary>
public sealed class FreshStartNoteTextTests
{
    /// <summary>Several Messages reread: the plural line states the count and the estimated tokens with a thousands separator (T12).</summary>
    [Fact]
    public void Format_SeveralMessagesReread_NamesCountAndTokens()
    {
        FreshStartNote note = new("room-1", "asked", 38, 31000, false);

        string text = Agency.Huddle.App.Components.Chat.FreshStartNoteText.Format("Nova", note);

        Assert.Equal("Nova started afresh, as you asked. It reread the last 38 Messages (about 31,000 tokens).", text);
    }

    /// <summary>A single Message reread uses the singular noun (T12, singular form).</summary>
    [Fact]
    public void Format_OneMessageReread_UsesSingularNoun()
    {
        FreshStartNote note = new("room-1", "asked", 1, 120, false);

        string text = Agency.Huddle.App.Components.Chat.FreshStartNoteText.Format("Nova", note);

        Assert.Equal("Nova started afresh, as you asked. It reread the last Message (about 120 tokens).", text);
    }

    /// <summary>No earlier Messages and no read failure: the note says there was nothing to reread (T13).</summary>
    [Fact]
    public void Format_NoMessagesAndNotFailed_SaysThereWereNoEarlierMessages()
    {
        FreshStartNote note = new("room-1", "asked", 0, 0, false);

        string text = Agency.Huddle.App.Components.Chat.FreshStartNoteText.Format("Nova", note);

        Assert.Equal("Nova started afresh, as you asked. There were no earlier Messages to reread.", text);
    }

    /// <summary>A failed read gives the could-not-reread line whatever the counts are (T14).</summary>
    [Fact]
    public void Format_ReadFailedWithNonzeroCounts_SaysItCouldNotRereadTheRoom()
    {
        FreshStartNote note = new("room-1", "asked", 38, 31000, true);

        string text = Agency.Huddle.App.Components.Chat.FreshStartNoteText.Format("Nova", note);

        Assert.Equal("Nova started afresh, as you asked. It could not reread the Room.", text);
    }
}
