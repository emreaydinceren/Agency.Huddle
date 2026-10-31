using Agency.Huddle.App.Teams;

namespace Agency.Huddle.Tests.Teams;

/// <summary>Tests the risk sentences <see cref="ProcessGateService.GateRisk"/> gives the two Human gate refusal ids.</summary>
public sealed class ProcessGateServiceHumanGateTests
{
    /// <summary>The Human gate id warns that a Task could reach Done without the Human's acceptance.</summary>
    [Fact]
    public void GateRisk_HumanGate_NamesMissingAcceptance()
    {
        Assert.Equal("A Task can then reach Done without the Human's acceptance.", ProcessGateService.GateRisk(GateId.HumanGate));
    }

    /// <summary>The Proof incomplete id warns that a Task could reach Needs You with open Proof items.</summary>
    [Fact]
    public void GateRisk_ProofIncomplete_NamesIncompleteProof()
    {
        Assert.Equal("A Task can then reach Needs You with its Proof incomplete.", ProcessGateService.GateRisk(GateId.ProofIncomplete));
    }
}
