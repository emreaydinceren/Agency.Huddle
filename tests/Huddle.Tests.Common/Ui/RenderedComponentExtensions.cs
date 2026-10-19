using System.Reflection;
using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Queries and clicks on a rendered bUnit fragment that more than one suite had written for itself. Every
/// click goes through <c>InvokeAsync</c> and awaits <c>ClickAsync</c>, so it cannot land on a stale handler
/// and the next assert sees the finished state (BlazorTesting.md, "Clicking without racing a re-render").
/// </summary>
internal static class RenderedComponentExtensions
{
    /// <summary>The selector of an open <c>MudMenu</c> item, which is not <c>mud-list-item</c> (that class belongs to <c>MudSelect</c>'s popover).</summary>
    private const string MenuItemSelector = "div.mud-menu-item";

    /// <summary>Renders <typeparamref name="T"/> beside the popover and dialog providers, in one root, so a menu, select or dialog it opens is reachable from the returned fragment.</summary>
    /// <typeparam name="T">The component under test.</typeparam>
    /// <param name="ctx">The context to render in.</param>
    /// <param name="parameters">Sets the component's parameters; <see langword="null"/> sets none.</param>
    /// <returns>The rendered root holding both providers and the component.</returns>
    /// <remarks>bUnit 2.11.3 keeps <c>ComponentParameterCollectionBuilder.Build</c> and <c>ToRenderFragment</c> internal, so this reaches them by reflection; both are looked up by name and fail loudly if bUnit renames them.</remarks>
    internal static IRenderedComponent<ContainerFragment> RenderWithPopovers<T>(this MudBunitContext ctx, Action<ComponentParameterCollectionBuilder<T>>? parameters = null)
        where T : IComponent
    {
        ArgumentNullException.ThrowIfNull(ctx);

        ComponentParameterCollectionBuilder<T> builder = new(parameters);
        MethodInfo build = typeof(ComponentParameterCollectionBuilder<T>).GetMethod("Build", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("bUnit no longer has ComponentParameterCollectionBuilder.Build; RenderWithPopovers<T> needs a new way to turn the builder into parameters.");
        object collection = build.Invoke(builder, null)
            ?? throw new InvalidOperationException("ComponentParameterCollectionBuilder.Build returned null.");
        MethodInfo toFragment = collection.GetType().GetMethod("ToRenderFragment", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("bUnit no longer has ComponentParameterCollection.ToRenderFragment.");
        RenderFragment content = toFragment.MakeGenericMethod(typeof(T)).Invoke(collection, null) as RenderFragment
            ?? throw new InvalidOperationException("ComponentParameterCollection.ToRenderFragment did not return a RenderFragment.");
        return ctx.RenderWithPopovers(content);
    }

    /// <summary>Finds the one button whose text, or with <paramref name="orAriaLabel"/> whose aria-label, is <paramref name="text"/>.</summary>
    /// <param name="cut">The rendered fragment to search.</param>
    /// <param name="text">The label, compared trimmed and ordinally.</param>
    /// <param name="orAriaLabel">Also match a button whose <c>aria-label</c> is <paramref name="text"/>, for an icon-only button.</param>
    /// <returns>The button.</returns>
    /// <exception cref="InvalidOperationException">No button, or more than one, matches.</exception>
    internal static IElement FindButton(this IRenderedComponent<IComponent> cut, string text, bool orAriaLabel = false)
    {
        ArgumentNullException.ThrowIfNull(cut);

        return cut.FindAll("button").Single(button => Matches(button, text, orAriaLabel));
    }

    /// <summary>Finds the first button, in document order, whose text, or with <paramref name="orAriaLabel"/> whose aria-label, is <paramref name="text"/>.</summary>
    /// <param name="cut">The rendered fragment to search.</param>
    /// <param name="text">The label, compared trimmed and ordinally.</param>
    /// <param name="orAriaLabel">Also match a button whose <c>aria-label</c> is <paramref name="text"/>.</param>
    /// <returns>The button.</returns>
    /// <exception cref="InvalidOperationException">No button matches.</exception>
    internal static IElement FindFirstButton(this IRenderedComponent<IComponent> cut, string text, bool orAriaLabel = false)
    {
        ArgumentNullException.ThrowIfNull(cut);

        return cut.FindAll("button").First(button => Matches(button, text, orAriaLabel));
    }

    /// <summary>The trimmed text of the first element that matches <paramref name="selector"/>.</summary>
    /// <param name="cut">The rendered fragment to search.</param>
    /// <param name="selector">A CSS selector.</param>
    /// <returns>The element's trimmed text content.</returns>
    internal static string TextOf(this IRenderedComponent<IComponent> cut, string selector)
    {
        ArgumentNullException.ThrowIfNull(cut);

        return cut.Find(selector).TextContent.Trim();
    }

    /// <summary>Every open <c>MudMenu</c> item, rendered with <c>role="menuitem"</c> in the popover provider.</summary>
    /// <param name="cut">The rendered fragment that holds the popover provider.</param>
    /// <returns>The items in document order; empty while no menu is open.</returns>
    internal static IReadOnlyList<IElement> MenuItems(this IRenderedComponent<IComponent> cut)
    {
        ArgumentNullException.ThrowIfNull(cut);

        return cut.FindAll(MenuItemSelector);
    }

    /// <summary>Clicks the open menu's item labelled <paramref name="text"/> and waits for its handler to finish.</summary>
    /// <param name="cut">The rendered fragment that holds the open menu.</param>
    /// <param name="text">The item's label, compared trimmed and ordinally.</param>
    /// <returns>A task that completes when the handler does.</returns>
    /// <exception cref="InvalidOperationException">No open menu item has that label.</exception>
    internal static Task ClickMenuItemAsync(this IRenderedComponent<IComponent> cut, string text)
    {
        ArgumentNullException.ThrowIfNull(cut);

        return cut.InvokeAsync(() => cut.MenuItems().First(item => string.Equals(item.TextContent.Trim(), text, StringComparison.Ordinal)).ClickAsync());
    }

    /// <summary>Whether <paramref name="button"/> carries the label <paramref name="text"/> as its text or, when asked, its aria-label.</summary>
    private static bool Matches(IElement button, string text, bool orAriaLabel) =>
        string.Equals(button.TextContent.Trim(), text, StringComparison.Ordinal)
        || (orAriaLabel && string.Equals(button.GetAttribute("aria-label"), text, StringComparison.Ordinal));
}
