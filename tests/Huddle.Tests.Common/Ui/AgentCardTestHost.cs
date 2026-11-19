using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.App.Pipes;
using Agency.Huddle.App.Data;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using MudBlazor;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// A bare page that does only what the retired Agents page did for the Agent card tests: a "New agent" button that opens the
/// card in Create mode and one button per Agent, named by its Name, that opens the card in View mode through
/// <see cref="AgentProfileCardOpener"/>. The card is the thing under test, and it needs something to open it.
/// </summary>
internal sealed class AgentCardTestHost : ComponentBase, IDisposable
{
    [Inject]
    public required PersonaStore Personas { get; set; }

    [Inject]
    public required ITeamDirectory TeamDirectory { get; set; }

    [Inject]
    public required IAgentGateway Gateway { get; set; }

    [Inject]
    public required PersonaHealth Health { get; set; }

    [Inject]
    public required IDialogService DialogService { get; set; }

    /// <summary>Repaints when the Agent directory changes, so an Agent written after the first render gets its button.</summary>
    protected override void OnInitialized() => this.Personas.PersonasChanged += this.OnPersonasChanged;

    /// <inheritdoc/>
    public void Dispose() => this.Personas.PersonasChanged -= this.OnPersonasChanged;

    /// <inheritdoc/>
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "button");
        builder.AddAttribute(1, "type", "button");
        builder.AddAttribute(2, "class", "page-header-create");
        builder.AddAttribute(3, "onclick", EventCallback.Factory.Create(this, this.BeginCreateAsync));
        builder.AddContent(4, "New agent");
        builder.CloseElement();

        builder.OpenElement(5, "ul");
        foreach (PersonaEntry entry in this.Personas.Entries)
        {
            string name = entry.Name;
            builder.OpenElement(6, "li");
            builder.SetKey(name);
            builder.OpenElement(7, "button");
            builder.AddAttribute(8, "type", "button");
            builder.AddAttribute(9, "class", "agent-tile");
            builder.AddAttribute(10, "onclick", EventCallback.Factory.Create(this, () => this.OpenCardAsync(name)));
            builder.OpenElement(11, "span");
            builder.AddAttribute(12, "class", "agents-item-name");
            builder.AddContent(13, name);
            builder.CloseElement();
            builder.CloseElement();
            builder.CloseElement();
        }

        builder.CloseElement();
    }

    private Task BeginCreateAsync() =>
        this.DialogService.ShowAsync<AgentProfileCard>("New agent", AgentProfileCardOpener.BuildCreateParameters(), AgentProfileCard.Options);

    private async Task OpenCardAsync(string name)
    {
        AgentProfileCardOpener opener = new(this.Personas, this.TeamDirectory, this.Gateway, this.Health, this.DialogService);
        _ = await opener.OpenAsync(name, CancellationToken.None);
    }

    private void OnPersonasChanged() =>
        _ = RendererDispatch.RunAsync(this.InvokeAsync, () =>
        {
            this.StateHasChanged();
            return Task.CompletedTask;
        });
}
