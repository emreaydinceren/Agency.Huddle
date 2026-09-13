using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Components;
using Agency.Huddle.App.Components.Shared;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Renders <see cref="TeammateCard"/> on its own. The /teammates page is an interactive component,
/// so an HTTP GET only ever returns the prerender with no card open — these are the tests that can
/// actually see the card's three modes. <c>HtmlRenderer</c> ships with the framework, so this costs
/// no new package, in keeping with the suite's no-mocking-framework rule.
/// </summary>
public sealed class TeammateCardTests
{
    [Fact]
    public async Task ViewMode_ShowsDetailsAndActions()
    {
        var html = await RenderAsync(new Dictionary<string, object?>
        {
            ["Mode"] = TeammateCardMode.View,
            ["Name"] = "Chief of Staff",
            ["Text"] = "You keep the team honest.",
            ["Status"] = new PersonaStatus(PersonaState.Online, null, DateTimeOffset.UtcNow),
            ["FilePath"] = @"C:\App_Data\personas\Chief of Staff.md",
        });

        Assert.Contains("Chief of Staff", html, StringComparison.Ordinal);
        Assert.Contains("You keep the team honest.", html, StringComparison.Ordinal);
        Assert.Contains("Online", html, StringComparison.Ordinal);
        Assert.Contains(">Edit<", html, StringComparison.Ordinal);
        Assert.Contains(">Open<", html, StringComparison.Ordinal);
        Assert.Contains(">Remove<", html, StringComparison.Ordinal);

        // Viewing is not editing: the Persona text is shown, not offered for typing into.
        Assert.DoesNotContain("<textarea", html, StringComparison.Ordinal);
    }

    /// <summary>View mode is the one place Title, Alias and Teams are shown alongside the Name - Edit's identity area stays exactly as before (see <see cref="EditMode_DoesNotShowTitleAliasOrTeams"/>).</summary>
    [Fact]
    public async Task ViewMode_ShowsTitleAliasAndTeams()
    {
        var html = await RenderAsync(new Dictionary<string, object?>
        {
            ["Mode"] = TeammateCardMode.View,
            ["Name"] = "Jarvis",
            ["Title"] = "Chief of Staff",
            ["Alias"] = "jar",
            ["Teams"] = "Business, Household",
            ["Text"] = "x",
        });

        Assert.Contains("Chief of Staff", html, StringComparison.Ordinal);
        Assert.Contains("jar", html, StringComparison.Ordinal);
        Assert.Contains("Business, Household", html, StringComparison.Ordinal);
    }

    /// <summary>An empty Teams string (a Persona with no Teams field) shows no "Teams:" line at all rather than an empty one.</summary>
    [Fact]
    public async Task ViewMode_WithNoTeams_ShowsNoTeamsLine()
    {
        var html = await RenderAsync(new Dictionary<string, object?>
        {
            ["Mode"] = TeammateCardMode.View,
            ["Name"] = "echo",
            ["Title"] = "Echo",
            ["Alias"] = "echo",
            ["Text"] = "x",
        });

        Assert.DoesNotContain("Teams:", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Emily Lee", ">EL<")]
    [InlineData("Chief of Staff", ">CS<")]
    [InlineData("echo", ">E<")]
    public async Task ViewMode_ShowsMonogramFromFirstAndLastWord(string name, string expected)
    {
        var html = await RenderAsync(new Dictionary<string, object?>
        {
            ["Mode"] = TeammateCardMode.View,
            ["Name"] = name,
        });

        Assert.Contains(expected, html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ViewMode_OffersMessageOnlyWhenARoomExists()
    {
        var withRoom = await RenderAsync(new Dictionary<string, object?>
        {
            ["Mode"] = TeammateCardMode.View,
            ["Name"] = "echo",
            ["RoomId"] = "room-1",
        });

        Assert.Contains("/rooms/room-1", withRoom, StringComparison.Ordinal);
        Assert.Contains(">Message<", withRoom, StringComparison.Ordinal);

        var withoutRoom = await RenderAsync(new Dictionary<string, object?>
        {
            ["Mode"] = TeammateCardMode.View,
            ["Name"] = "echo",
        });

        Assert.DoesNotContain(">Message<", withoutRoom, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EditMode_OffersTheTextButNotTheName()
    {
        var html = await RenderAsync(new Dictionary<string, object?>
        {
            ["Mode"] = TeammateCardMode.Edit,
            ["Name"] = "Chief of Staff",
            ["Text"] = "You keep the team honest.",
        });

        Assert.Contains("<textarea", html, StringComparison.Ordinal);
        Assert.Contains("You keep the team honest.", html, StringComparison.Ordinal);
        Assert.Contains(">Save<", html, StringComparison.Ordinal);

        // Renaming a Persona would mean renaming its file and re-registering its Agent under a new
        // identity, so the Name is deliberately not editable here.
        Assert.DoesNotContain("<input type=\"text\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EditMode_WarnsThatSavingClearsMemory()
    {
        var html = await RenderAsync(new Dictionary<string, object?>
        {
            ["Mode"] = TeammateCardMode.Edit,
            ["Name"] = "coo",
            ["Text"] = "x",
        });

        Assert.Contains("clears what it remembers", html, StringComparison.Ordinal);
    }

    /// <summary>Edit's identity area is unchanged by this phase: only the Name and online status show, never Title/Alias/Teams - the textarea is still a raw-file editor over the whole file, front matter included, so those fields would be redundant with it.</summary>
    [Fact]
    public async Task EditMode_DoesNotShowTitleAliasOrTeams()
    {
        var html = await RenderAsync(new Dictionary<string, object?>
        {
            ["Mode"] = TeammateCardMode.Edit,
            ["Name"] = "coo",
            ["Title"] = "Legendary Assistant",
            ["Alias"] = "coo",
            ["Teams"] = "Business",
            ["Text"] = "---\nName: coo\n---\nx",
        });

        // "Chief of Staff" is deliberately avoided as the Title here: it is also the textarea's own
        // hardcoded placeholder sample text, which would make this assertion pass for the wrong
        // reason.
        Assert.DoesNotContain("Legendary Assistant", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Alias:", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Teams:", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateMode_OffersBothNameAndText()
    {
        var html = await RenderAsync(new Dictionary<string, object?>
        {
            ["Mode"] = TeammateCardMode.Create,
        });

        Assert.Contains("<input type=\"text\"", html, StringComparison.Ordinal);
        Assert.Contains("<textarea", html, StringComparison.Ordinal);
        Assert.Contains("New teammate", html, StringComparison.Ordinal);

        // The card is where a user learns that a Name may hold spaces at all.
        Assert.Contains("Spaces are fine.", html, StringComparison.Ordinal);
    }

    /// <summary>Create mode's identity area offers all four structural fields, not just the Name - the whole point of Phase 5's Add signature is that they arrive at PersonaStore structured rather than typed into the body.</summary>
    [Fact]
    public async Task CreateMode_OffersTitleAliasAndTeamsInputs()
    {
        var html = await RenderAsync(new Dictionary<string, object?>
        {
            ["Mode"] = TeammateCardMode.Create,
            ["Title"] = "Chief of Staff",
            ["Alias"] = "coo",
            ["Teams"] = "Business, Household",
        });

        Assert.Contains("Title", html, StringComparison.Ordinal);
        Assert.Contains("Alias", html, StringComparison.Ordinal);
        Assert.Contains("Teams", html, StringComparison.Ordinal);
        Assert.Contains("value=\"Chief of Staff\"", html, StringComparison.Ordinal);
        Assert.Contains("value=\"coo\"", html, StringComparison.Ordinal);
        Assert.Contains("value=\"Business, Household\"", html, StringComparison.Ordinal);

        // The hint text this phase asks for, verbatim.
        Assert.Contains("Business, Household", html, StringComparison.Ordinal);
    }

    /// <summary>The Create textarea is the Persona's BODY only, not the whole file - its label and hint must say so, distinct from Edit's "whole file" wording.</summary>
    [Fact]
    public async Task CreateMode_TextareaIsExplicitlyTheBodyOnly()
    {
        var html = await RenderAsync(new Dictionary<string, object?>
        {
            ["Mode"] = TeammateCardMode.Create,
        });

        Assert.Contains("Persona body", html, StringComparison.Ordinal);
        Assert.Contains("no front matter needed here", html, StringComparison.Ordinal);

        // "Persona text" (Edit's label) and "front matter included" (Edit's hint) must not leak
        // into Create's copy - the two modes describe two different things in the same textarea.
        Assert.DoesNotContain("Persona text", html, StringComparison.Ordinal);
        Assert.DoesNotContain("front matter included", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Error_IsShownOnTheCardRatherThanLosingTheDraft()
    {
        var html = await RenderAsync(new Dictionary<string, object?>
        {
            ["Mode"] = TeammateCardMode.Create,
            ["Name"] = "bad/name",
            ["Text"] = "a draft worth keeping",
            ["Error"] = "'bad/name' is not a valid Persona name.",
        });

        Assert.Contains("is not a valid Persona name.", html, StringComparison.Ordinal);
        Assert.Contains("a draft worth keeping", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConfirmingRemove_ReplacesRemoveWithAConfirmStep()
    {
        var html = await RenderAsync(new Dictionary<string, object?>
        {
            ["Mode"] = TeammateCardMode.View,
            ["Name"] = "coo",
            ["ConfirmingRemove"] = true,
        });

        Assert.Contains(">Confirm<", html, StringComparison.Ordinal);
        Assert.DoesNotContain(">Remove<", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateMode_OffersAModelChoice()
    {
        var html = await RenderAsync(new Dictionary<string, object?>
        {
            ["Mode"] = TeammateCardMode.Create,
            ["AvailableModels"] = new List<AgentModelOption>
            {
                new("claude-opus-4", "Opus", null),
                new("claude-sonnet-4", "Sonnet", null),
            },
        });

        Assert.Contains("<select", html, StringComparison.Ordinal);
        Assert.Contains("Opus", html, StringComparison.Ordinal);
        Assert.Contains("Sonnet", html, StringComparison.Ordinal);

        // Razor's HtmlEncoder escapes the apostrophe, so match around it rather than through it.
        Assert.Contains("Use the agent", html, StringComparison.Ordinal);
        Assert.Contains("default</option>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateMode_DefaultsToTheAgentsOwnModel()
    {
        var html = await RenderAsync(new Dictionary<string, object?>
        {
            ["Mode"] = TeammateCardMode.Create,
            ["AvailableModels"] = new List<AgentModelOption>
            {
                new("claude-opus-4", "Opus", null),
            },
        });

        // No option carries a literal "selected" — the empty option is first and is what an
        // unselected <select> shows by default, precisely because Model is null here.
        Assert.DoesNotContain("selected", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EditMode_PreselectsTheStoredModel()
    {
        var html = await RenderAsync(new Dictionary<string, object?>
        {
            ["Mode"] = TeammateCardMode.Edit,
            ["Name"] = "coo",
            ["Text"] = "x",
            ["Model"] = "claude-opus-4",
            ["AvailableModels"] = new List<AgentModelOption>
            {
                new("claude-opus-4", "Opus", null),
                new("claude-sonnet-4", "Sonnet", null),
            },
        });

        Assert.Contains("value=\"claude-opus-4\" selected", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EditMode_StoredModelNotInTheCatalog_IsStillOffered()
    {
        // The ModelChoices guard: the probe failed (or the adapter's catalog changed) and no
        // longer lists the model this Persona was saved with. Opening Edit must still show and
        // keep that stored choice rather than silently resetting it to default on save.
        var html = await RenderAsync(new Dictionary<string, object?>
        {
            ["Mode"] = TeammateCardMode.Edit,
            ["Name"] = "coo",
            ["Text"] = "x",
            ["Model"] = "claude-vintage-1",
            ["AvailableModels"] = new List<AgentModelOption>
            {
                new("claude-opus-4", "Opus", null),
            },
        });

        Assert.Contains("value=\"claude-vintage-1\" selected", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateMode_NoModelsAdvertised_StillRenders()
    {
        var html = await RenderAsync(new Dictionary<string, object?>
        {
            ["Mode"] = TeammateCardMode.Create,
            ["AvailableModels"] = Array.Empty<AgentModelOption>(),
        });

        Assert.Contains("<select", html, StringComparison.Ordinal);
        Assert.Contains("advertises no models", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateMode_WhileTheCatalogLoads_SaysSo()
    {
        var html = await RenderAsync(new Dictionary<string, object?>
        {
            ["Mode"] = TeammateCardMode.Create,
            ["ModelsLoading"] = true,
        });

        Assert.Contains("Reading the models this agent offers", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ViewMode_ShowsTheChosenModel()
    {
        var html = await RenderAsync(new Dictionary<string, object?>
        {
            ["Mode"] = TeammateCardMode.View,
            ["Name"] = "coo",
            ["Text"] = "x",
            ["Model"] = "claude-opus-4",
            ["AvailableModels"] = new List<AgentModelOption>
            {
                new("claude-opus-4", "Opus", null),
            },
        });

        // The display name, not the raw wire id.
        Assert.Contains("Opus", html, StringComparison.Ordinal);
        Assert.DoesNotContain("claude-opus-4", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ViewMode_WithNoModel_SaysAgentDefault()
    {
        var html = await RenderAsync(new Dictionary<string, object?>
        {
            ["Mode"] = TeammateCardMode.View,
            ["Name"] = "coo",
            ["Text"] = "x",
        });

        // View mode must render correctly with no catalog at all: AvailableModels is left at its
        // default empty list here.
        Assert.Contains("Agent default", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateMode_OffersAnEffortChoice()
    {
        var html = await RenderAsync(new Dictionary<string, object?>
        {
            ["Mode"] = TeammateCardMode.Create,
            ["AvailableEfforts"] = new List<AgentEffortOption>
            {
                new("high", "High", null),
                new("low", "Low", null),
            },
        });

        Assert.Contains("<select", html, StringComparison.Ordinal);
        Assert.Contains("High", html, StringComparison.Ordinal);
        Assert.Contains("Low", html, StringComparison.Ordinal);

        // Razor's HtmlEncoder escapes the apostrophe, so match around it rather than through it.
        Assert.Contains("Use the agent", html, StringComparison.Ordinal);
        Assert.Contains("default</option>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EditMode_PreselectsTheStoredEffort()
    {
        var html = await RenderAsync(new Dictionary<string, object?>
        {
            ["Mode"] = TeammateCardMode.Edit,
            ["Name"] = "coo",
            ["Text"] = "x",
            ["Effort"] = "high",
            ["AvailableEfforts"] = new List<AgentEffortOption>
            {
                new("high", "High", null),
                new("low", "Low", null),
            },
        });

        Assert.Contains("value=\"high\" selected", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EditMode_StoredEffortNotInTheCatalog_IsStillOffered()
    {
        // The EffortChoices guard: the probe failed (or the adapter's catalog changed) and no
        // longer lists the effort this Persona was saved with. Opening Edit must still show and
        // keep that stored choice rather than silently resetting it to default on save.
        var html = await RenderAsync(new Dictionary<string, object?>
        {
            ["Mode"] = TeammateCardMode.Edit,
            ["Name"] = "coo",
            ["Text"] = "x",
            ["Effort"] = "vintage",
            ["AvailableEfforts"] = new List<AgentEffortOption>
            {
                new("high", "High", null),
            },
        });

        Assert.Contains("value=\"vintage\" selected", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateMode_NoEffortLevelsAdvertised_StillRenders()
    {
        var html = await RenderAsync(new Dictionary<string, object?>
        {
            ["Mode"] = TeammateCardMode.Create,
            ["AvailableEfforts"] = Array.Empty<AgentEffortOption>(),
        });

        Assert.Contains("<select", html, StringComparison.Ordinal);
        Assert.Contains("offers no effort choice", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateMode_WhileTheEffortCatalogLoads_SaysSo()
    {
        var html = await RenderAsync(new Dictionary<string, object?>
        {
            ["Mode"] = TeammateCardMode.Create,
            ["EffortsLoading"] = true,
        });

        Assert.Contains("Reading the effort levels this model offers", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ViewMode_ShowsTheChosenEffort()
    {
        var html = await RenderAsync(new Dictionary<string, object?>
        {
            ["Mode"] = TeammateCardMode.View,
            ["Name"] = "coo",
            ["Text"] = "x",
            ["Effort"] = "effort-high",
            ["AvailableEfforts"] = new List<AgentEffortOption>
            {
                new("effort-high", "High", null),
            },
        });

        // The display name, not the raw wire id.
        Assert.Contains("High", html, StringComparison.Ordinal);
        Assert.DoesNotContain("effort-high", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ViewMode_WithNoEffort_SaysModelDefault()
    {
        var html = await RenderAsync(new Dictionary<string, object?>
        {
            ["Mode"] = TeammateCardMode.View,
            ["Name"] = "coo",
            ["Text"] = "x",
        });

        // View mode must render correctly with no catalog at all: AvailableEfforts is left at its
        // default empty list here. "Model default", not "Agent default": effort is resolved by the
        // model, not the agent.
        Assert.Contains("Model default", html, StringComparison.Ordinal);
    }

    /// <summary>An Offline status's reason renders as its own line under the status, not just the badge.</summary>
    [Fact]
    public async Task TeammateCard_ShowsTheReasonWhenOffline()
    {
        var html = await RenderAsync(new Dictionary<string, object?>
        {
            ["Mode"] = TeammateCardMode.View,
            ["Name"] = "coo",
            ["Status"] = new PersonaStatus(PersonaState.Offline, "The Adapter needs authentication.", DateTimeOffset.UtcNow),
        });

        Assert.Contains("Offline", html, StringComparison.Ordinal);
        Assert.Contains("The Adapter needs authentication.", html, StringComparison.Ordinal);
    }

    /// <summary>A Degraded status renders its own label and reason, distinct from Offline - the same reason is connected but not doing what was asked of it.</summary>
    [Fact]
    public async Task TeammateCard_ShowsDegraded()
    {
        var html = await RenderAsync(new Dictionary<string, object?>
        {
            ["Mode"] = TeammateCardMode.View,
            ["Name"] = "coo",
            ["Status"] = new PersonaStatus(PersonaState.Degraded, "The Reply Gate has not answered in a while.", DateTimeOffset.UtcNow),
        });

        Assert.Contains("Degraded", html, StringComparison.Ordinal);
        Assert.Contains("The Reply Gate has not answered in a while.", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Offline", html, StringComparison.Ordinal);
    }

    /// <summary>A Starting status renders "Starting", never "Offline" - a Persona launching for the first time must not read as a fault.</summary>
    [Fact]
    public async Task TeammateCard_ShowsStartingRatherThanOffline()
    {
        var html = await RenderAsync(new Dictionary<string, object?>
        {
            ["Mode"] = TeammateCardMode.View,
            ["Name"] = "coo",
            ["Status"] = new PersonaStatus(PersonaState.Starting, null, DateTimeOffset.UtcNow),
        });

        Assert.Contains("Starting", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Offline", html, StringComparison.Ordinal);
    }

    /// <summary>Restart is offered only while the resolved state is Offline or Degraded - never for a healthy or still-starting teammate, which has nothing for it to fix.</summary>
    [Theory]
    [InlineData(PersonaState.Offline, true)]
    [InlineData(PersonaState.Degraded, true)]
    [InlineData(PersonaState.Online, false)]
    [InlineData(PersonaState.Starting, false)]
    public async Task TeammateCard_ShowsRestartOnlyWhenOfflineOrDegraded(PersonaState state, bool expectRestart)
    {
        var html = await RenderAsync(new Dictionary<string, object?>
        {
            ["Mode"] = TeammateCardMode.View,
            ["Name"] = "coo",
            ["Status"] = new PersonaStatus(state, null, DateTimeOffset.UtcNow),
        });

        if (expectRestart)
        {
            Assert.Contains(">Restart<", html, StringComparison.Ordinal);
        }
        else
        {
            Assert.DoesNotContain(">Restart<", html, StringComparison.Ordinal);
        }
    }

    private static async Task<string> RenderAsync(Dictionary<string, object?> parameters)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        await using var provider = services.BuildServiceProvider();

        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());

        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<TeammateCard>(
                ParameterView.FromDictionary(parameters));

            return output.ToHtmlString();
        });
    }
}