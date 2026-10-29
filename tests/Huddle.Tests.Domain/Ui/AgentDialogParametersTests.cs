using System.Reflection;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Components;
using Agency.Huddle.App.Components.Pages;
using Agency.Huddle.App.Components.Shared;
using AgentsPage = Agency.Huddle.App.Components.Pages.Agents;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Replaces the retired <c>AgentsRazorSourceTests</c>, which guarded a bug that no longer has a
/// subject: <c>&lt;AgentProfileCard ... /&gt;</c> stopped being a Razor element Stage 4 of the MudBlazor
/// migration made <c>AgentProfileCard</c> a <c>MudDialog</c> shown through <c>IDialogService</c>, so there
/// is no component attribute left to bind without a leading <c>@</c>.
/// </summary>
/// <remarks>
/// <para>
/// The bug's shape survives in a new form, though. <see cref="AgentsPage.BuildViewParameters"/> and
/// <see cref="AgentsPage.BuildCreateParameters"/> build a <see cref="DialogParameters"/> keyed by plain
/// strings; <c>Agents.razor</c> writes every key as <c>nameof(AgentProfileCard.Something)</c> rather
/// than a literal, which makes a typo a compile error today, but nothing stops a future edit from
/// reaching for a raw string instead, or from renaming a <c>[Parameter]</c> on
/// <see cref="AgentProfileCard"/> without updating every call site that names it. Either mistake is
/// exactly as silent as the leading-<c>@</c> bug this test replaces: <see cref="DialogParameters"/> is
/// a dictionary, so a key that does not match a real parameter is not an error, it is a value
/// <see cref="AgentProfileCard"/> quietly never receives, and the card renders with whatever default that
/// parameter has instead of what the caller meant to show.
/// </para>
/// <para>
/// Both builder methods are pure functions of their inputs, with no dependency on a live
/// <see cref="PersonaStore"/> or a rendered page - the same reason
/// <see cref="AgentGrouping.Group"/> was pulled out of <c>Agents.razor</c> the same way - so
/// this test can call them directly and reflect over the real <see cref="AgentProfileCard"/> type, with no
/// rendering harness at all.
/// </para>
/// </remarks>
public sealed class AgentDialogParametersTests
{
    /// <summary>Every <see cref="DialogParameters"/> key <see cref="AgentsPage.BuildViewParameters"/> produces names a real public <c>[Parameter]</c> on <see cref="AgentProfileCard"/>.</summary>
    [Fact]
    public void ViewParameters_EveryKeyNamesARealAgentProfileCardParameter()
    {
        var entry = new PersonaEntry("Jarvis", "Chief of Staff", "jar", ["Business"], "Jarvis.md", "You are Jarvis.");
        var persona = new Persona("Jarvis", "You are Jarvis.", "claude-opus-4", "high");
        var status = new PersonaStatus(PersonaState.Online, null, DateTimeOffset.UtcNow);

        var parameters = AgentsPage.BuildViewParameters("Jarvis", entry, persona, status, roomId: "room-1", filePath: @"C:\Teams\Jarvis.md");

        AssertEveryKeyIsARealParameter(parameters);
    }

    /// <summary>The Work Mode is handed to the card under the name of its real parameter, so it is not silently dropped between the page and the dialog.</summary>
    [Fact]
    public void ViewParameters_CarryTheWorkMode()
    {
        var entry = new PersonaEntry("Jarvis", "Chief of Staff", "jar", ["Business"], "Jarvis.md", "You are Jarvis.");
        var persona = new Persona("Jarvis", "You are Jarvis.", "claude-opus-4", "high", WorkMode: "plan");
        var status = new PersonaStatus(PersonaState.Online, null, DateTimeOffset.UtcNow);

        var parameters = AgentsPage.BuildViewParameters("Jarvis", entry, persona, status, roomId: "room-1", filePath: @"C:\Teams\Jarvis.md");

        Assert.Equal("plan", parameters[nameof(AgentProfileCard.WorkMode)]);
        AssertEveryKeyIsARealParameter(parameters);
    }

    /// <summary>The same guard against <see cref="AgentsPage.BuildViewParameters"/>'s null-entry, null-persona path (a Persona somehow not yet in <see cref="PersonaStore.Entries"/>), which still must name only real parameters.</summary>
    [Fact]
    public void ViewParameters_WithNoEntryOrPersona_EveryKeyNamesARealAgentProfileCardParameter()
    {
        var status = new PersonaStatus(PersonaState.Offline, null, DateTimeOffset.MinValue);

        var parameters = AgentsPage.BuildViewParameters("ghost", entry: null, persona: null, status, roomId: null, filePath: null);

        AssertEveryKeyIsARealParameter(parameters);
    }

    /// <summary>Every <see cref="DialogParameters"/> key <see cref="AgentsPage.BuildCreateParameters"/> produces names a real public <c>[Parameter]</c> on <see cref="AgentProfileCard"/>.</summary>
    [Fact]
    public void CreateParameters_EveryKeyNamesARealAgentProfileCardParameter()
    {
        var parameters = AgentsPage.BuildCreateParameters();

        AssertEveryKeyIsARealParameter(parameters);
    }

    /// <summary><see cref="AgentsPage.BuildCreateParameters"/> sets <see cref="AgentProfileCard.Mode"/> to <see cref="AgentProfileCardMode.Create"/> - the one value a blank Create card cannot be opened without.</summary>
    [Fact]
    public void CreateParameters_SetsModeToCreate()
    {
        var parameters = AgentsPage.BuildCreateParameters();

        Assert.Equal(AgentProfileCardMode.Create, parameters.Get<AgentProfileCardMode>(nameof(AgentProfileCard.Mode)));
    }

    /// <summary>Asserts every key in <paramref name="parameters"/> names a real, public <c>[Parameter]</c> property on <see cref="AgentProfileCard"/> - the successor's whole guard.</summary>
    /// <param name="parameters">The <see cref="DialogParameters"/> to check.</param>
    private static void AssertEveryKeyIsARealParameter(DialogParameters parameters)
    {
        var realParameterNames = typeof(AgentProfileCard).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.GetCustomAttribute<ParameterAttribute>() is not null)
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var pair in parameters)
        {
            Assert.Contains(pair.Key, realParameterNames);
        }
    }
}
