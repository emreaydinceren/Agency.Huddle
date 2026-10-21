using System.Globalization;
using Agency.Huddle.App;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Library;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Pins how a Transcript reads: day separators, grouped sender headers, and the slim system row a
/// Task wake-up becomes instead of a Human-looking bubble.
/// </summary>
public sealed class MessageDisplayTests
{
    private static readonly string[] HumanIds = ["human-1"];

    private const string WakeText = "@Grace Task PLAT-0004 \"Fix login\" (Done, Platform) was changed by You:\n- status: Review -> Done\n- priority: High\nCall get_task with taskId PLAT-0004 for the full task.";

    /// <summary>The default wake-up wording is read back into id, title, actor and one line per change.</summary>
    [Fact]
    public void TryParseTaskWake_DefaultWording_ReadsItsParts()
    {
        bool parsed = MessageDisplay.TryParseTaskWake(WakeText, out TaskWakeInfo info);

        Assert.True(parsed);
        Assert.Equal("PLAT-0004", info.TaskId);
        Assert.Equal("Fix login", info.Title);
        Assert.Equal("You", info.Actor);
        Assert.Equal(["status: Review -> Done", "priority: High"], info.Changes);
    }

    /// <summary>Ordinary chat text, even one that starts with a Mention, is never taken for a wake-up.</summary>
    [Fact]
    public void TryParseTaskWake_OrdinaryMessage_IsNotAWakeUp()
    {
        Assert.False(MessageDisplay.TryParseTaskWake("@Grace Task PLAT-0004 looks fine to me", out _));
    }

    /// <summary>Today and yesterday get words; anything older gets a short date.</summary>
    [Fact]
    public void DayLabel_TodayYesterdayAndOlder()
    {
        DateTimeOffset now = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

        Assert.Equal("Today", MessageDisplay.DayLabel(now, now));
        Assert.Equal("Yesterday", MessageDisplay.DayLabel(now.AddDays(-1), now));
        Assert.Equal(now.AddDays(-4).ToLocalTime().ToString("ddd, MMM d", CultureInfo.InvariantCulture), MessageDisplay.DayLabel(now.AddDays(-4), now.AddMinutes(0)));
    }

    /// <summary>Only a change of local day starts a new separator, and the first Message always does.</summary>
    [Fact]
    public void StartsNewDay_FirstMessageAndDayChangeOnly()
    {
        DateTimeOffset first = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

        Assert.True(MessageDisplay.StartsNewDay(null, first));
        Assert.False(MessageDisplay.StartsNewDay(first, first.AddMinutes(1)));
        Assert.True(MessageDisplay.StartsNewDay(first, first.AddDays(1)));
    }

    /// <summary>The same sender within the window shares a header; another sender or a long gap does not.</summary>
    [Fact]
    public void ContinuesGroup_SameSenderWithinWindowOnly()
    {
        DateTimeOffset first = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

        Assert.True(MessageDisplay.ContinuesGroup("a", first, "a", first.AddMinutes(2)));
        Assert.False(MessageDisplay.ContinuesGroup("a", first, "b", first.AddMinutes(2)));
        Assert.False(MessageDisplay.ContinuesGroup("a", first, "a", first.AddMinutes(30)));
        Assert.False(MessageDisplay.ContinuesGroup(null, null, "a", first));
    }

    /// <summary>A wake-up posted as the Human renders as a slim system row, with no bubble and no agent-facing "Call get_task" text.</summary>
    [Fact]
    public async Task MessageList_WakeUpFromTheHuman_RendersASlimSystemRow()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = new();
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<AvatarStore>());
        ctx.Services.AddSingleton<ITaskReferenceResolver>(new FakeTaskReferenceResolver());
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<IOptions<TeamOptions>>());
        ctx.Services.AddSingleton<ILibraryReferenceResolver>(new FakeLibraryNoteResolver());
        ChatMessage wake = new("m-1", DateTimeOffset.UtcNow, "human-1", "You", WakeText);

        var cut = ctx.Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, [wake])
            .Add(p => p.HumanIds, HumanIds));

        Assert.Single(cut.FindAll("details.system-row"));
        Assert.Empty(cut.FindAll(".message-bubble"));
        Assert.DoesNotContain("get_task", cut.Markup, StringComparison.Ordinal);
        Assert.Single(cut.FindAll(".day-separator"));
    }

    /// <summary>A wake-up Huddle posted itself renders as the same slim system row, not as a Teammate's bubble with the agent-facing line.</summary>
    [Fact]
    public async Task MessageList_WakeUpFromHuddle_RendersASlimSystemRow()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = new();
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<AvatarStore>());
        ctx.Services.AddSingleton<ITaskReferenceResolver>(new FakeTaskReferenceResolver());
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<IOptions<TeamOptions>>());
        ctx.Services.AddSingleton<ILibraryReferenceResolver>(new FakeLibraryNoteResolver());
        ChatMessage wake = new("m-1", DateTimeOffset.UtcNow, KnownIds.Huddle, "Huddle", WakeText);

        var cut = ctx.Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, [wake])
            .Add(p => p.HumanIds, HumanIds));

        Assert.Single(cut.FindAll("details.system-row"));
        Assert.Empty(cut.FindAll(".message-bubble"));
        Assert.Empty(cut.FindAll(".huddle-mark"));
        Assert.DoesNotContain("get_task", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>Any other Message Huddle wrote is an ordinary row named "Huddle" with Huddle's mark, and not the Human's right-hand bubble.</summary>
    [Fact]
    public async Task MessageList_OtherHuddleMessage_WearsTheHuddleMark()
    {
        await using TeamWebApplicationFactory factory = new();
        await using MudBunitContext ctx = new();
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<AvatarStore>());
        ctx.Services.AddSingleton<ITaskReferenceResolver>(new FakeTaskReferenceResolver());
        ctx.Services.AddSingleton(factory.Services.GetRequiredService<IOptions<TeamOptions>>());
        ctx.Services.AddSingleton<ILibraryReferenceResolver>(new FakeLibraryNoteResolver());
        ChatMessage note = new("m-1", DateTimeOffset.UtcNow, KnownIds.Huddle, "Huddle", "Something Huddle noticed.");

        var cut = ctx.Render<MessageList>(parameters => parameters
            .Add(p => p.Messages, [note])
            .Add(p => p.HumanIds, HumanIds));

        Assert.Equal("Huddle", cut.Find(".message-sender").TextContent.Trim());
        Assert.NotNull(cut.Find(".huddle-mark .mud-icon-root"));
        Assert.Empty(cut.FindAll(".mud-avatar"));
        Assert.Empty(cut.FindAll(".message-row.human"));
        Assert.Contains("Something Huddle noticed.", cut.Find(".message-body").TextContent, StringComparison.Ordinal);
    }
}
