using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Agents;
using Agency.Huddle.App.Data;
using Agency.Huddle.Tests.Factories;

namespace Agency.Huddle.Tests.Agents;

/// <summary>
/// Only <c>PersonaStore.SetConnectors</c> writes the account-connectors row (docs/Huddle.AgentConnectors-Specifications.md
/// section 10): the seeder that writes the Chief of Staff can leave a row absent, and the reset that restores a built-in
/// Agent removes a present one.
/// </summary>
public sealed class ConnectorsCannotTurnOnTests
{
    /// <summary>The seeder writes a Chief of Staff with no connectors row, so the new Agent is off.</summary>
    [Fact]
    public async Task Seeder_WritesChiefOfStaff_WithConnectorsOff()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        using PersonaStore personas = dataDir.CreatePersonaStore();
        BuiltinAgentSeeder seeder = new(personas, NullLogger<BuiltinAgentSeeder>.Instance);

        await seeder.StartAsync(ct);

        PersonaEntry entry = Assert.Single(personas.Entries);
        Assert.False(new PersonaConnectorsStore(dataDir.Options()).IsOn(entry.Name));
        Assert.False(personas.Get(entry.Name)?.Connectors);
    }

    /// <summary>Resetting a built-in Agent whose row is present removes the row.</summary>
    [Fact]
    public async Task Reset_RemovesAPresentRow()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        using PersonaStore personas = dataDir.CreatePersonaStore();
        await new BuiltinAgentSeeder(personas, NullLogger<BuiltinAgentSeeder>.Instance).StartAsync(ct);
        string name = Assert.Single(personas.Entries).Name;
        personas.SetConnectors(name, true);
        PersonaConnectorsStore rows = new(dataDir.Options());
        Assert.True(rows.IsOn(name));

        _ = new BuiltinAgentReset(personas).ResetToDefault(name);

        Assert.False(rows.IsOn(name));
    }
}
