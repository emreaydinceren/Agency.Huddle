using Agency.Huddle.App.Components.Tasks;
using Agency.Huddle.App.Helpers;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Helpers;
using Bunit;
using Microsoft.Extensions.DependencyInjection;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// The Task panel tells the Human, under the Human gate switch, when a Helper turned the gate on and that turning it off is
/// final (Helpers Spec §6.13, plan HL-18).
/// </summary>
public sealed class HelperGateNoteTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 20, 8, 0, 0, TimeSpan.Zero);
    private static readonly ScriptedHelper NeedsHuman = new("NeedsHuman");

    /// <summary>Builds a Change log entry.</summary>
    /// <param name="actor">Who made the change.</param>
    /// <param name="summary">What changed.</param>
    private static ChangeLogEntry Entry(string actor, string summary) => new(At, actor, summary);

    /// <summary>A Helper's gate-on is explained with its reason and the warning that turning it off is final.</summary>
    [Fact]
    public void HelperTurnedItOn_ShowsTheReasonAndTheFinality()
    {
        IReadOnlyList<ChangeLogEntry> log = [Entry("Leo", "created"), Entry("Scripted (Helper)", "human gate on (reason: Sends a reply to a customer)")];

        string? note = HelperGateNote.For(log, gateOn: true, [NeedsHuman]);

        Assert.Equal("Turned on by the Scripted Helper: Sends a reply to a customer. If you turn it off, the Helper won't ask about this Task again.", note);
    }

    /// <summary>A gate-on with no reason still names the Helper.</summary>
    [Fact]
    public void HelperTurnedItOn_WithoutAReason_StillNamesTheHelper()
    {
        IReadOnlyList<ChangeLogEntry> log = [Entry("Scripted (Helper)", "human gate on")];

        string? note = HelperGateNote.For(log, gateOn: true, [NeedsHuman]);

        Assert.Equal("Turned on by the Scripted Helper. If you turn it off, the Helper won't ask about this Task again.", note);
    }

    /// <summary>A Teammate's or the Human's gate-on shows no Helper line.</summary>
    /// <param name="actor">Who turned the gate on.</param>
    [Theory]
    [InlineData("Leo")]
    [InlineData("You")]
    [InlineData("Other (Helper)")]
    public void SomeoneElseTurnedItOn_ShowsNothing(string actor)
    {
        IReadOnlyList<ChangeLogEntry> log = [Entry(actor, "human gate on")];

        Assert.Null(HelperGateNote.For(log, gateOn: true, [NeedsHuman]));
    }

    /// <summary>When the Human turned the gate on after the Helper did, the latest gate-on is theirs and no line shows.</summary>
    [Fact]
    public void HumanTurnedItOnAfterTheHelper_ShowsNothing()
    {
        IReadOnlyList<ChangeLogEntry> log =
        [
            Entry("Scripted (Helper)", "human gate on (reason: outward)"),
            Entry("You", "human gate off"),
            Entry("You", "human gate on"),
        ];

        Assert.Null(HelperGateNote.For(log, gateOn: true, [NeedsHuman]));
    }

    /// <summary>A gate-on that shares a summary with another change, after a Helper's and a Human's gate-off, is the Human's: no line.</summary>
    [Fact]
    public void CombinedSummaryGateOnAfterHumanOff_ShowsNothing()
    {
        IReadOnlyList<ChangeLogEntry> log =
        [
            Entry("Scripted (Helper)", "human gate on (reason: outward)"),
            Entry("You", "human gate off"),
            Entry("You", "status Review; human gate on"),
        ];

        Assert.Null(HelperGateNote.For(log, gateOn: true, [NeedsHuman]));
    }

    /// <summary>With the gate off, as the panel now shows it, there is no line, whatever the log says.</summary>
    [Fact]
    public void GateOff_ShowsNothing()
    {
        IReadOnlyList<ChangeLogEntry> log = [Entry("Scripted (Helper)", "human gate on (reason: outward)")];

        Assert.Null(HelperGateNote.For(log, gateOn: false, [NeedsHuman]));
    }

    /// <summary>The panel renders the line under the explanation when a Helper turned the gate on, and not otherwise.</summary>
    [Fact]
    public async Task Panel_RendersTheLineOnlyForAHelpersGateOn()
    {
        await using MudBunitContext ctx = new();
        ctx.Services.AddSingleton(new HelperRegistry([NeedsHuman]));
        IReadOnlyList<ChangeLogEntry> byHelper = [Entry("Scripted (Helper)", "human gate on (reason: outward)")];
        IReadOnlyList<ChangeLogEntry> byLeo = [Entry("Leo", "human gate on")];

        IRenderedComponent<TaskHumanGate> helperCut = ctx.Render<TaskHumanGate>(parameters => parameters
            .Add(p => p.HumanGate, true)
            .Add(p => p.ChangeLog, byHelper));
        IRenderedComponent<TaskHumanGate> leoCut = ctx.Render<TaskHumanGate>(parameters => parameters
            .Add(p => p.HumanGate, true)
            .Add(p => p.ChangeLog, byLeo));

        Assert.Equal(
            "Turned on by the Scripted Helper: outward. If you turn it off, the Helper won't ask about this Task again.",
            helperCut.Find(".task-human-gate-helper-note").TextContent.Trim());
        Assert.Empty(leoCut.FindAll(".task-human-gate-helper-note"));
    }

    /// <summary>A host with no Helpers registered renders the panel as it always did.</summary>
    [Fact]
    public async Task Panel_WithoutHelpers_RendersNoLine()
    {
        await using MudBunitContext ctx = new();

        IRenderedComponent<TaskHumanGate> cut = ctx.Render<TaskHumanGate>(parameters => parameters
            .Add(p => p.HumanGate, true)
            .Add(p => p.ChangeLog, [Entry("Scripted (Helper)", "human gate on")]));

        Assert.Empty(cut.FindAll(".task-human-gate-helper-note"));
    }
}
