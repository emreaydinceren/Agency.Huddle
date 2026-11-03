using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Jobs;
using Agency.Huddle.Tests.Acp;
using Agency.Huddle.Tests.Factories;
using Agency.Huddle.Tests.Waiting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.Tests.Jobs;

/// <summary>Pins how <see cref="JobStore"/> keeps each owner's <c>schedule.md</c>.</summary>
public sealed class JobStoreTests : IDisposable
{
    private readonly TempDataDir dir = new();
    private readonly IOptions<TeamOptions> options;
    private readonly PersonaStore personas;
    private readonly JobStore store;

    /// <summary>Seeds two Agents, Nova and Orion, and opens the store over them.</summary>
    public JobStoreTests()
    {
        this.options = this.dir.Options();
        TestPersonaFiles.Write(new AgentPaths(this.options), "Nova", PersonaSupervisorTestSupport.PersonaText("Nova", "You are Nova."));
        TestPersonaFiles.Write(new AgentPaths(this.options), "Orion", PersonaSupervisorTestSupport.PersonaText("Orion", "You are Orion."));
        this.personas = this.options.CreatePersonaStore();
        this.store = new JobStore(this.personas, this.options, NullLogger<JobStore>.Instance);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        this.store.Dispose();
        this.personas.Dispose();
        this.dir.Dispose();
    }

    /// <summary>A Job added for an Agent is written beside its definition, given an id, and listed.</summary>
    [Fact]
    public void Add_ForAnAgent_WritesBesideItsDefinition()
    {
        Job added = this.store.Add(Draft(JobOwner.Agent("Nova")));

        Assert.Matches("^job-[0-9a-f]{6}$", added.Id);
        Assert.Equal(added, Assert.Single(this.store.Jobs));
        string file = Path.Combine(this.dir.Path, "Agents", "Nova", JobFiles.FileName);
        Assert.Equal(JobDocument.Write([new JobSection(added, string.Empty, added.Title, null)]), File.ReadAllText(file));
    }

    /// <summary>The Human's Jobs go in <c>Human/schedule.md</c> under the data folder.</summary>
    [Fact]
    public void Add_ForTheHuman_WritesTheHumanFile()
    {
        Job added = this.store.Add(Draft(JobOwner.Human));

        Assert.True(File.Exists(Path.Combine(this.dir.Path, "Human", JobFiles.FileName)));
        Assert.True(Assert.Single(this.store.Jobs).Owner.IsHuman);
        Assert.Equal(added.Id, this.store.Jobs[0].Id);
    }

    /// <summary>A Job file beside a definition is neither a Persona nor a rejected file.</summary>
    [Fact]
    public void AgentsJobFile_IsNotRejectedByThePersonaStore()
    {
        _ = this.store.Add(Draft(JobOwner.Agent("Nova")));

        using PersonaStore fresh = this.options.CreatePersonaStore();

        Assert.Empty(fresh.RejectedFiles);
        Assert.Equal(["Nova", "Orion"], fresh.ListNames().Order(StringComparer.Ordinal));
    }

    /// <summary>Removing an owner's last Job deletes the file rather than leaving an empty schedule.</summary>
    [Fact]
    public void Remove_LastJob_DeletesTheFile()
    {
        Job added = this.store.Add(Draft(JobOwner.Agent("Nova")));

        Assert.True(this.store.Remove(added.Id));

        Assert.Empty(this.store.Jobs);
        Assert.False(File.Exists(Path.Combine(this.dir.Path, "Agents", "Nova", JobFiles.FileName)));
        Assert.False(this.store.Remove(added.Id));
    }

    /// <summary>A section the Human wrote that cannot be read is listed as a problem and survives a write by Huddle.</summary>
    [Fact]
    public void BadHandWrittenSection_IsListedAndKeptAcrossAWrite()
    {
        string file = Path.Combine(this.dir.Path, "Agents", "Nova", JobFiles.FileName);
        File.WriteAllText(file, "# Schedule\n\n## Broken\n- id: job-1\n- when: soon\n\nKeep me.\n");
        TestPersonaFiles.Write(new AgentPaths(this.options), "Nova", PersonaSupervisorTestSupport.PersonaText("Nova", "You are Nova, still."));
        using JobStore reopened = new(this.personas, this.options, NullLogger<JobStore>.Instance);

        _ = reopened.Add(Draft(JobOwner.Agent("Nova")));

        JobProblem problem = Assert.Single(reopened.Problems);
        Assert.Equal(("Broken", true), (problem.Title, problem.Reason.StartsWith("'soon' is not a time", StringComparison.Ordinal)));
        Assert.Contains("## Broken\n- id: job-1\n- when: soon\n\nKeep me.\n", File.ReadAllText(file), StringComparison.Ordinal); // contains-ok: the rewritten file also holds the added Job.
    }

    /// <summary>Changing a Job's owner moves its section, keeping its id.</summary>
    [Fact]
    public void Update_NewOwner_MovesTheSection()
    {
        Job added = this.store.Add(Draft(JobOwner.Agent("Nova")));

        Assert.True(this.store.Update(added with { Owner = JobOwner.Agent("Orion") }));

        Job moved = Assert.Single(this.store.Jobs);
        Assert.Equal((added.Id, "Orion"), (moved.Id, moved.Owner.AgentName));
        Assert.False(File.Exists(Path.Combine(this.dir.Path, "Agents", "Nova", JobFiles.FileName)));
        Assert.True(File.Exists(Path.Combine(this.dir.Path, "Agents", "Orion", JobFiles.FileName)));
    }

    /// <summary>A Job for an Agent that does not exist is refused.</summary>
    [Fact]
    public void Add_UnknownAgent_Throws()
    {
        KeyNotFoundException refused = Assert.Throws<KeyNotFoundException>(() => this.store.Add(Draft(JobOwner.Agent("Nobody"))));

        Assert.Equal("No Agent is called 'Nobody'.", refused.Message);
    }

    /// <summary>The same id in two files keeps the first and reports the second.</summary>
    [Fact]
    public void DuplicateIdAcrossFiles_KeepsTheFirstAndReportsTheSecond()
    {
        string section = "## Digest\n- id: job-1\n- when: daily at 08:00\n\nDo it.\n";
        File.WriteAllText(Path.Combine(this.dir.Path, "Agents", "Nova", JobFiles.FileName), section);
        File.WriteAllText(Path.Combine(this.dir.Path, "Agents", "Orion", JobFiles.FileName), section);

        using JobStore reopened = new(this.personas, this.options, NullLogger<JobStore>.Instance);

        Assert.Equal("Nova", Assert.Single(reopened.Jobs).Owner.AgentName);
        Assert.Equal("Another Job already has the id 'job-1'.", Assert.Single(reopened.Problems).Reason);
    }

    /// <summary>A Job typed into a file by hand is picked up by the watcher and announced.</summary>
    [Fact]
    public async Task HandEdit_IsPickedUpAndAnnounced()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        int changes = 0;
        this.store.Changed += () => Interlocked.Increment(ref changes);

        await File.WriteAllTextAsync(Path.Combine(this.dir.Path, "Human", JobFiles.FileName), "## Stretch\n- id: job-9\n- when: every 2 hours\n\nStand up.\n", ct);

        await Eventually.UntilAsync(() => this.store.Find("job-9") is not null, ct, TestBudgets.Slow);
        Assert.True(Volatile.Read(ref changes) > 0);
    }

    private static Job Draft(JobOwner owner) =>
        new(string.Empty, owner, "Morning digest", "Summarise yesterday.", JobRecurrence.Daily(new TimeOnly(9, 0)), "UTC", "You", false);
}
