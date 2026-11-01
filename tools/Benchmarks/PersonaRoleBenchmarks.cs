using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Teams;
using BenchmarkDotNet.Attributes;

namespace Agency.Huddle.Tools.Benchmarks;

/// <summary>
/// perf-audit P1: how <c>ProofContextBuilder.CoverageFor</c> finds a named Agent's role, which it does for each name on
/// every Task render. <see cref="Old"/> re-parses the Persona's frontmatter as the code did; <see cref="New"/> reads the
/// role the index parsed once when it was built. Both run the app's own code.
/// </summary>
[MemoryDiagnoser]
public class PersonaRoleBenchmarks
{
    private const string Name = "Quinn";

    private const string Text = """
        ---
        name: 'Quinn'
        title: 'QA Engineer'
        alias: 'quinn'
        teams: ['Platform', 'Support']
        specialty: 'test plans, release verification, flaky-test triage'
        _role: 'web/qa@1.2.0'
        skills: ['docx', 'xlsx']
        ---
        You are Quinn, the QA engineer. You verify every release against its test plan,
        record Evidence on the Task, and you never sign off on work you have not run yourself.
        """;

    private PersonaIndex index = PersonaIndex.Build([]);

    /// <summary>Builds an index holding the one Persona.</summary>
    [GlobalSetup]
    public void Setup() => this.index = PersonaIndex.Build([("C:/agents/Quinn/Quinn.md", Text)]);

    /// <summary>The code before the fix: parse the frontmatter to read the role.</summary>
    [Benchmark(Baseline = true)]
    public RoleRef? Old() =>
        PersonaFrontmatter.TryReadIdentity(Text, out PersonaIdentity? identity, out _) ? identity.Role : null;

    /// <summary>The shipped code: the role the index already holds.</summary>
    [Benchmark]
    public RoleRef? New() => this.index.ByName(Name)?.Role;
}
