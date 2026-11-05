namespace Agency.Huddle.Tests.Acp.Sessions;

using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Acp.Sessions.Fakes;
using Agency.Huddle.Tests.Waiting;

/// <summary>
/// Pins the silent reply token (#362, #354): a reply that is only <see cref="SilentReply.Token"/> posts nothing,
/// a reply that ends with it posts the rest, the token anywhere else stays, and it never streams to the page.
/// A Message that is not posted spends no Room Budget, because only a posted Message reaches the Transcript.
/// </summary>
public sealed class SilentReplyTests
{
    private static readonly WorkItem Item = new("room-a", "Room A", "Bob", "hello", []);

    /// <summary>A reply that is only the token posts nothing, so the Room's Budget is not spent on it.</summary>
    [Fact]
    public async Task OnlyTheToken_PostsNothing()
    {
        FakeRoomSessionOwner owner = await RunAsync(SilentReply.Token);

        Assert.Empty(owner.Written.OfType<PostMessage>());
    }

    /// <summary>The token alone is held back from the stream, so it never flashes on the page.</summary>
    [Fact]
    public async Task OnlyTheToken_NeverStreams()
    {
        FakeRoomSessionOwner owner = await RunAsync(SilentReply.Token);

        Assert.DoesNotContain(SilentReply.Token, Streamed(owner), StringComparison.Ordinal);
    }

    /// <summary>The token arriving in two chunks is still recognised as the whole reply.</summary>
    [Fact]
    public async Task TokenSplitAcrossChunks_PostsNothingAndNeverStreams()
    {
        FakeRoomSessionOwner owner = await RunAsync("NO_", "REPLY");

        Assert.Empty(owner.Written.OfType<PostMessage>());
        Assert.DoesNotContain("NO_", Streamed(owner), StringComparison.Ordinal);
    }

    /// <summary>The token arriving one character at a time never leaves a fragment on the page.</summary>
    [Fact]
    public async Task TokenOneCharacterAtATime_NeverStreamsAFragment()
    {
        FakeRoomSessionOwner owner = await RunAsync([.. SilentReply.Token.Select(static c => c.ToString())]);

        Assert.Empty(owner.Written.OfType<PostMessage>());
        Assert.Equal(string.Empty, Streamed(owner));
    }

    /// <summary>A real answer followed by the token posts the answer without the token or the gap before it.</summary>
    [Fact]
    public async Task ReplyThenTrailingToken_PostsTheReply()
    {
        FakeRoomSessionOwner owner = await RunAsync("The date is the 3rd.\n\n" + SilentReply.Token);

        PostMessage posted = Assert.Single(owner.Written.OfType<PostMessage>());
        Assert.Equal("The date is the 3rd.", posted.Text);
        Assert.DoesNotContain(SilentReply.Token, Streamed(owner), StringComparison.Ordinal);
    }

    /// <summary>The Stop hook's reply can follow the answer with no space; the token is still removed from the end.</summary>
    [Fact]
    public async Task ReplyThenTokenWithNoSpace_PostsTheReply()
    {
        FakeRoomSessionOwner owner = await RunAsync("It is the 3rd." + SilentReply.Token);

        PostMessage posted = Assert.Single(owner.Written.OfType<PostMessage>());
        Assert.Equal("It is the 3rd.", posted.Text);
    }

    /// <summary>A trailing token split across chunks leaves neither the token nor a fragment of it on the page, and the answer still streams.</summary>
    [Fact]
    public async Task ReplyThenTokenSplitAcrossChunks_StreamsOnlyTheReply()
    {
        FakeRoomSessionOwner owner = await RunAsync("The date ", "is the 3rd. NO", "_REP", "LY");

        PostMessage posted = Assert.Single(owner.Written.OfType<PostMessage>());
        Assert.Equal("The date is the 3rd.", posted.Text.TrimEnd());
        string streamed = Streamed(owner);
        Assert.Equal("The date is the 3rd. ", streamed);
    }

    /// <summary>Whitespace after the token does not stop it being the end of the reply.</summary>
    [Fact]
    public async Task TokenFollowedByWhitespace_IsStillTheEnd()
    {
        FakeRoomSessionOwner owner = await RunAsync("Done.\n" + SilentReply.Token + "\n");

        PostMessage posted = Assert.Single(owner.Written.OfType<PostMessage>());
        Assert.Equal("Done.", posted.Text);
    }

    /// <summary>The token inside ordinary prose is not silence: the whole reply posts as written, and streams in full.</summary>
    [Fact]
    public async Task TokenMidText_StaysAsWritten()
    {
        string text = "End a reply with " + SilentReply.Token + " when you have nothing to add.";

        FakeRoomSessionOwner owner = await RunAsync(text);

        PostMessage posted = Assert.Single(owner.Written.OfType<PostMessage>());
        Assert.Equal(text, posted.Text);
        Assert.Equal(text, Streamed(owner));
    }

    /// <summary>Text that only begins like the token is held back while it might be one, then released in full once it is not.</summary>
    [Fact]
    public async Task TextThatOnlyStartsLikeTheToken_IsPostedAndStreamedInFull()
    {
        FakeRoomSessionOwner owner = await RunAsync("NO_", "THANKS");

        PostMessage posted = Assert.Single(owner.Written.OfType<PostMessage>());
        Assert.Equal("NO_THANKS", posted.Text);
        Assert.Equal("NO_THANKS", Streamed(owner));
    }

    /// <summary>A reply that ends on the start of the token and then stops was held back while it might be one, and is released in full when the Turn ends.</summary>
    [Fact]
    public async Task ReplyEndingOnAPartOfTheToken_IsReleasedAtTheEnd()
    {
        FakeRoomSessionOwner owner = await RunAsync("Almost ", "NO_REP");

        PostMessage posted = Assert.Single(owner.Written.OfType<PostMessage>());
        Assert.Equal("Almost NO_REP", posted.Text);
        Assert.Equal("Almost NO_REP", Streamed(owner));
    }

    /// <summary>The token is exact: another case or a longer word is ordinary text.</summary>
    /// <param name="text">A reply that resembles the token without being it.</param>
    [Theory]
    [InlineData("no_reply")]
    [InlineData("NO_REPLYING")]
    [InlineData("THE_NO_REPLY")]
    [InlineData("ANO_REPLY")]
    public async Task NearMisses_ArePostedAsWritten(string text)
    {
        FakeRoomSessionOwner owner = await RunAsync(text);

        PostMessage posted = Assert.Single(owner.Written.OfType<PostMessage>());
        Assert.Equal(text, posted.Text);
    }

    /// <summary>The token is removed from the end however it was joined to the answer or repeated, and stays where it is part of a name or of prose.</summary>
    /// <param name="text">A reply as the Room has it after the chunks are joined.</param>
    /// <param name="expected">What is posted; empty means the Turn is silent.</param>
    [Theory]
    [InlineData("see report.mdNO_REPLY", "see report.md")]
    [InlineData("see report.md NO_REPLY", "see report.md")]
    [InlineData("done 3NO_REPLY", "done 3")]
    [InlineData("done!NO_REPLY", "done!")]
    [InlineData("NO_REPLYNO_REPLY", "")]
    [InlineData("NO_REPLY\nNO_REPLY", "")]
    [InlineData("answer NO_REPLY NO_REPLY", "answer")]
    [InlineData("answerNO_REPLYNO_REPLY", "answer")]
    [InlineData("NO_REPLY.", "")]
    [InlineData("NO_REPLY.\n", "")]
    [InlineData("answer NO_REPLY.", "answer")]
    [InlineData("THE_NO_REPLY", "THE_NO_REPLY")]
    [InlineData("ANO_REPLY", "ANO_REPLY")]
    [InlineData("NO_REPLY is the token", "NO_REPLY is the token")]
    [InlineData("no_reply", "no_reply")]
    public void Strip_RemovesTrailingTokensOnly(string text, string expected)
    {
        Assert.Equal(expected, SilentReply.Strip(text));
    }

    /// <summary>Anything that could still turn out to be a run of tokens, with or without a full stop, is held back from the stream; ordinary text is not.</summary>
    /// <param name="text">The reply so far.</param>
    /// <param name="expected">How many leading characters may stream.</param>
    [Theory]
    [InlineData("hello", 5)]
    [InlineData("hello NO_", 6)]
    [InlineData("helloNO_REPLY", 5)]
    [InlineData("hello NO_REPLY.", 6)]
    [InlineData("hello NO_REPLY.\n", 6)]
    [InlineData("hello NO_REPLYNO_", 6)]
    [InlineData("hello NO_REPLY NO_REPLY", 6)]
    [InlineData("NO_REPLY\nNO_REPLY", 0)]
    [InlineData("NO_REPLYNO_REPLY", 0)]
    [InlineData("hello NO_REPLYING", 17)]
    public void SafeLength_HoldsBackAnythingThatCouldBeTheTokens(string text, int expected)
    {
        Assert.Equal(expected, SilentReply.SafeLength(text));
    }

    /// <summary>A silent Turn writes only streamed-text frames and never a <see cref="PostMessage"/>. The pipe's <c>AgentConnection</c> turns a <c>PostMessage</c> into <c>ChatService.PostAsync</c>, the only place Room Budget is spent, and turns a delta into a Draft; so a silent Turn spends nothing. The hop from the pipe to <c>ChatService</c> is not run here: it is read in code, with no test of its own.</summary>
    [Fact]
    public async Task SilentTurn_WritesNoFrameThatSpendsBudget()
    {
        FakeRoomSessionOwner owner = await RunAsync(SilentReply.Token, SilentReply.Token);

        Assert.All(owner.Written, static message => Assert.IsType<MessageDelta>(message));
    }

    /// <summary>A silent Turn still ends cleanly: the page gets its final delta, so the pending bubble closes.</summary>
    [Fact]
    public async Task SilentTurn_StillSendsTheFinalDelta()
    {
        FakeRoomSessionOwner owner = await RunAsync(SilentReply.Token);

        Assert.Contains(owner.Written, message => message is MessageDelta { IsFinal: true });
    }

    /// <summary>Runs one Turn whose reply arrives as <paramref name="chunks"/> and waits for its final delta.</summary>
    /// <param name="chunks">The reply's streamed pieces, in order.</param>
    private static async Task<FakeRoomSessionOwner> RunAsync(params string[] chunks)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        session.EnqueueReply(chunks);
        FakeRoomSessionOwner owner = new();
        await using RoomSessionHarness harness = RoomSessionHarness.Create(owner, RoomSessionHarness.FixedOpen(session), new() { RoomId = null });
        harness.Room.Enqueue(new QueuedWork(1, Item));

        await Eventually.UntilAsync(() => owner.Written.OfType<MessageDelta>().Any(delta => delta.IsFinal), ct, timeout: TestBudgets.Standard);

        return owner;
    }

    /// <summary>Everything the page was sent as streamed text, in order.</summary>
    /// <param name="owner">The owner the Turn wrote to.</param>
    private static string Streamed(FakeRoomSessionOwner owner) =>
        string.Concat(owner.Written.OfType<MessageDelta>().Where(static delta => !delta.IsFinal).Select(static delta => delta.Text));
}
