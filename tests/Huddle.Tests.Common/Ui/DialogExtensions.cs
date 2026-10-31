using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace Agency.Huddle.Tests.Ui;

/// <summary>Opening a dialog and editing a select in a bUnit test, the two steps several suites had copied.</summary>
internal static class DialogExtensions
{
    /// <summary>Shows <typeparamref name="TDialog"/> through the context's <see cref="IDialogService"/> and waits until it is on screen.</summary>
    /// <typeparam name="TDialog">The dialog component.</typeparam>
    /// <param name="ctx">The context that owns the dialog service.</param>
    /// <param name="host">The rendered fragment holding the dialog provider; the wait and the show run on its dispatcher.</param>
    /// <param name="title">The dialog title.</param>
    /// <param name="parameters">The dialog's parameters.</param>
    /// <param name="waitSelector">The selector that proves the dialog has painted.</param>
    /// <returns>The reference to the shown dialog.</returns>
    internal static async Task<IDialogReference> ShowDialogAsync<TDialog>(this MudBunitContext ctx, IRenderedComponent<IComponent> host, string title, DialogParameters<TDialog> parameters, string waitSelector = ".mud-dialog-title")
        where TDialog : ComponentBase
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(parameters);

        IDialogService dialogService = ctx.Services.GetRequiredService<IDialogService>();
        IDialogReference reference = await host.InvokeAsync(() => dialogService.ShowAsync<TDialog>(title, parameters));
        host.WaitForAssertion(() => Assert.NotEmpty(host.FindAll(waitSelector)));
        return reference;
    }

    /// <summary>Sets the one <see cref="MudSelect{T}"/> of <typeparamref name="T"/> to <paramref name="value"/> by raising its <c>ValueChanged</c>, which is how a select commits an edit.</summary>
    /// <typeparam name="T">The select's value type.</typeparam>
    /// <param name="cut">The rendered fragment that holds the select.</param>
    /// <param name="value">The value to commit.</param>
    /// <param name="cssClass">Narrows to the select whose <c>Class</c> contains this when the fragment has several of the same type.</param>
    /// <returns>A task that completes when the change has been handled.</returns>
    /// <exception cref="InvalidOperationException">No select, or more than one, matches.</exception>
    internal static async Task EditSelectAsync<T>(this IRenderedComponent<IComponent> cut, T value, string? cssClass = null)
    {
        ArgumentNullException.ThrowIfNull(cut);

        IRenderedComponent<MudSelect<T>> select = cut.FindComponents<MudSelect<T>>()
            .Single(candidate => cssClass is null || (candidate.Instance.Class?.Contains(cssClass, StringComparison.Ordinal) ?? false));
        await cut.InvokeAsync(() => select.Instance.ValueChanged.InvokeAsync(value));
    }
}
