using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Skills;
using Agency.Huddle.Tests.Waiting;

namespace Agency.Huddle.Tests.Acp;

/// <summary>Builders and doubles shared by <c>PersonaSupervisorLifecycleTests</c>, <c>PersonaSupervisorHealthTests</c> and <c>PersonaSupervisorRestartTests</c>.</summary>
internal static class PersonaSupervisorTestSupport
{
    /// <summary>Builds a fresh <see cref="PersonaHealth"/> against the real clock - nothing in this file asserts against <see cref="PersonaStatus.Since"/> precisely enough to need a controllable one.</summary>
    internal static PersonaHealth NewHealth() => new(TimeProvider.System, NullLogger<PersonaHealth>.Instance);

    /// <summary>
    /// Builds a real <see cref="SkillStore"/> over <paramref name="options"/>'s own <c>DataDir</c>, for
    /// a test that needs <see cref="PersonaSupervisor"/>'s new constructor parameter but does not care
    /// about Skills itself - <c>SkillStore</c> is not nullable, and CSharpPrinciples.md's "make illegal
    /// states unrepresentable" is exactly why this file does not give <see cref="PersonaSupervisor"/> a
    /// test-only nullable one instead. The caller disposes the result with <see langword="using"/>: a
    /// real <see cref="SkillStore"/> owns a <see cref="System.IO.FileSystemWatcher"/>.
    /// </summary>
    /// <param name="options">Supplies the <c>DataDir</c> the returned store watches and resolves Skills against.</param>
    internal static SkillStore NewSkillStore(IOptions<TeamOptions> options) => new(options, NullLogger<SkillStore>.Instance);

    internal static void WritePersonaFile(IOptions<TeamOptions> options, string name, string body = "You are a persona.")
    {
        TestPersonaFiles.Write(new AgentPaths(options), name, PersonaText(name, body));
    }

    /// <summary>Minimal valid Persona frontmatter (Name, Title and Alias all <paramref name="name"/>) wrapped around <paramref name="body"/> - identity is front-matter driven from this phase on, so every seeded Persona needs one to be discoverable at all.</summary>
    internal static string PersonaText(string name, string body) => $"---\nName: {name}\nTitle: {name}\nAlias: {name}\n---\n{body}";

    /// <summary><see cref="PersonaText"/>, plus a <c>skills: [team-building]</c> field - for <c>PersonaSupervisorLifecycleTests.OnPersonasChanged_UnrelatedFileEvent_DoesNotRestartPersonaWithSkills</c> alone.</summary>
    internal static string SkillsPersonaText(string name) => $"---\nName: {name}\nTitle: {name}\nAlias: {name}\nskills: [team-building]\n---\nYou are a persona.";

    /// <summary>A valid <see cref="PersonaIdentity"/> for <paramref name="name"/>, with Title and Alias both <paramref name="name"/> too and no Teams - the structured input <see cref="PersonaStore.Add"/> now takes.</summary>
    internal static PersonaIdentity Identity(string name) => new(name, name, name, []);

    /// <summary>
    /// Polls every 5 ms until <paramref name="agentName"/> is registered and online, and fails naming it after
    /// <see cref="TestBudgets.Slowest"/> (the wait was unbounded before).
    /// </summary>
    /// <param name="directory">The Team Directory to look the Agent up in.</param>
    /// <param name="gateway">The Agent gateway that reports whether it is online.</param>
    /// <param name="agentName">The Agent's Name.</param>
    /// <param name="ct">The test's own deadline.</param>
    /// <returns>The Agent's user id.</returns>
    internal static async Task<string> WaitForAgentOnlineAsync(
        Agency.Huddle.App.Data.ITeamDirectory directory, Agency.Huddle.App.Pipes.IAgentGateway gateway, string agentName, CancellationToken ct)
    {
        string? agentId = await Eventually.ValueAsync<string?>(
            async probeCt =>
            {
                var user = await directory.FindUserByNameAsync(agentName, probeCt);
                return user is not null && gateway.IsOnline(user.Id) ? user.Id : null;
            },
            static id => id is not null,
            ct,
            TestBudgets.Slowest,
            TimeSpan.FromMilliseconds(5),
            what: $"agent {agentName} registered and online");

        return agentId ?? throw new InvalidOperationException("The wait accepted a null agent id.");
    }

    /// <summary>
    /// Polls <paramref name="condition"/> every 5 ms until it holds, and fails naming it when
    /// <paramref name="ct"/> fires first. A bare <see cref="TaskCanceledException"/> from inside this
    /// helper hid which wait never arrived, and read as "the run was slow" - when the actual cause was a
    /// file-system event lost for good, which no budget outlasts.
    /// </summary>
    /// <param name="condition">The state the test needs before it can go on.</param>
    /// <param name="ct">The test's own deadline.</param>
    /// <param name="conditionText">The source text of <paramref name="condition"/>, supplied by the compiler.</param>
    internal static Task WaitUntilAsync(
        Func<bool> condition, CancellationToken ct, [CallerArgumentExpression(nameof(condition))] string conditionText = "")
    {
        return Eventually.UntilAsync(condition, ct, interval: TimeSpan.FromMilliseconds(5), conditionText: conditionText);
    }
}