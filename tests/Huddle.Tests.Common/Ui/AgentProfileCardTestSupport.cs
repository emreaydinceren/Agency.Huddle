using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.Tests.Factories;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Shared rendering, seeding and interaction helpers for <c>AgentProfileCardTests</c>,
/// <c>AgentProfileCardModelEffortTests</c> and <c>AgentProfileCardAvatarTests</c>, which were one class
/// until it became the suite's slowest.
/// </summary>
internal static class AgentProfileCardTestSupport
{
    /// <summary>
    /// The isolated <see cref="TeamOptions.DataDir"/> <paramref name="factory"/> composed its app with -
    /// resolved from the running host, the same way <c>AvatarEndpointTests.DataDirOf</c> does, since
    /// <see cref="TeamWebApplicationFactory"/> exposes its temp directory only indirectly.
    /// </summary>
    /// <param name="factory">The factory whose composed <see cref="TeamOptions"/> to read.</param>
    internal static string DataDirOf(TeamWebApplicationFactory factory)
    {
        return factory.Services.GetRequiredService<IOptions<TeamOptions>>().Value.DataDir;
    }

    /// <summary>
    /// Clicks the radio input of the avatar editor's choice card whose text contains <paramref name="choiceLabel"/>.
    /// The click bubbles to the card, as a browser's does, so it exercises the same idempotent choose handler.
    /// </summary>
    /// <param name="cut">The rendered page holding the card.</param>
    /// <param name="choiceLabel">The card's visible name - "Initials of the name", "A short label", "Image from the library" or "Upload an image".</param>
    internal static Task SelectAvatarChoiceAsync(IRenderedComponent<ContainerFragment> cut, string choiceLabel) =>
        ClickAsync(cut, () =>
        {
            var card = cut.FindAll(".avatar-choice-card").First(element => element.TextContent.Contains(choiceLabel, StringComparison.Ordinal));
            return card.QuerySelector("input[type=radio]") ?? throw new InvalidOperationException($"No radio input under '{choiceLabel}'.");
        });

    /// <summary>The CSS selector of the card's avatar, a button that opens the avatar editor dialog.</summary>
    internal const string AvatarButton = "button.agent-profile-card-avatar";

    /// <summary>The CSS selector of the avatar editor dialog's root, which scopes every lookup that must not reach the card behind it (both dialogs have a Cancel button).</summary>
    internal const string EditorDialog = ".avatar-editor-dialog";

    /// <summary>
    /// Clicks the card's avatar and waits for the avatar editor dialog to paint.
    /// </summary>
    /// <param name="cut">The rendered page holding the card.</param>
    internal static async Task OpenAvatarEditorAsync(IRenderedComponent<ContainerFragment> cut)
    {
        await ClickAsync(cut, () => cut.Find(AvatarButton));
        cut.WaitForElement(EditorDialog);
    }

    /// <summary>The CSS selector of the avatar editor's Background palette while it is offered (not hidden under an image card): the wrapper around the swatches and the label above them.</summary>
    internal const string BackgroundPicker = EditorDialog + " .avatar-editor-background:not(.avatar-editor-background-hidden)";

    /// <summary>The CSS selector of the Background palette while an image card is chosen: still rendered, so the dialog keeps its height, but hidden and inert.</summary>
    internal const string HiddenBackgroundPicker = EditorDialog + " .avatar-editor-background.avatar-editor-background-hidden[inert]";

    /// <summary>The CSS selector of one swatch in the Background palette: the radio input behind each circle.</summary>
    internal const string Swatch = BackgroundPicker + " .avatar-swatch-input";

    /// <summary>The first button inside the avatar editor dialog whose trimmed text, or <c>aria-label</c>, equals <paramref name="text"/>.</summary>
    /// <param name="cut">The rendered page holding the card.</param>
    /// <param name="text">The button's trimmed text or <c>aria-label</c>.</param>
    internal static IElement FindEditorButton(IRenderedComponent<ContainerFragment> cut, string text) =>
        cut.Find(EditorDialog).QuerySelectorAll("button").First(button => MatchesButtonLabel(button, text));

    /// <summary>Whether the avatar editor dialog holds a button named <paramref name="text"/>; false when the dialog is closed.</summary>
    /// <param name="cut">The rendered page holding the card.</param>
    /// <param name="text">The button's trimmed text or <c>aria-label</c>.</param>
    internal static bool HasEditorButton(IRenderedComponent<ContainerFragment> cut, string text) =>
        cut.FindAll(EditorDialog).Any(dialog => dialog.QuerySelectorAll("button").Any(button => MatchesButtonLabel(button, text)));

    /// <summary>
    /// The trimmed text of the editor dialog's image status line: the stored image's file name, or the pending-image notice.
    /// It is the only <c>body2</c> text in the editor, and exists only under the Image choice once an image is set.
    /// </summary>
    /// <param name="cut">The rendered page holding the card.</param>
    internal static string EditorImageStatus(IRenderedComponent<ContainerFragment> cut) =>
        cut.Find(EditorDialog + " .mud-typography-body2").TextContent.Trim();

    /// <summary>Clicks the editor dialog's button <see cref="FindEditorButton"/> names, through <see cref="ClickAsync"/>.</summary>
    /// <param name="cut">The rendered page holding the card.</param>
    /// <param name="text">The button's trimmed text or <c>aria-label</c>.</param>
    internal static Task ClickEditorButtonAsync(IRenderedComponent<ContainerFragment> cut, string text) =>
        ClickAsync(cut, () => FindEditorButton(cut, text));

    /// <summary>
    /// The <c>Immediate="true"</c> counterpart of <see cref="SetTextValueAsync"/>: raises <c>@oninput</c>
    /// rather than <c>@onchange</c>, matching the Label box's own binding (see the markup comment on
    /// why it is <c>Immediate</c>, unlike every blur-only box in this file).
    /// </summary>
    /// <param name="cut">The rendered page holding the card.</param>
    /// <param name="label">The control's label text.</param>
    /// <param name="value">The text to type.</param>
    internal static Task SetImmediateTextValueAsync(IRenderedComponent<ContainerFragment> cut, string label, string value) =>
        DispatchWhenPresentAsync(
            cut,
            () => FindInputControl(cut, label).QuerySelector("input") ?? throw new InvalidOperationException($"No input under '{label}'."),
            input => input.Input(value));

    /// <summary>The CSS selector of the avatar editor's label text input: the field has no visible label, so it is found by its <c>aria-label</c>.</summary>
    internal const string AvatarLabelInput = ".avatar-label-field input[aria-label='Avatar label']";

    /// <summary>
    /// Types <paramref name="value"/> into the avatar editor's label field by raising its <c>oninput</c>, through
    /// <see cref="DispatchWhenPresentAsync"/>. The field is found by <see cref="AvatarLabelInput"/>, not by a visible label.
    /// </summary>
    /// <param name="cut">The rendered page holding the editor.</param>
    /// <param name="value">The text to type.</param>
    internal static Task SetAvatarLabelAsync(IRenderedComponent<ContainerFragment> cut, string value) =>
        DispatchWhenPresentAsync(
            cut,
            () => cut.Find(AvatarLabelInput),
            input => input.Input(value));

    /// <summary>Registers this factory's real services (<see cref="PersonaStore"/> and friends) into a fresh <see cref="MudBunitContext"/>, the same pattern the page tests use.</summary>
    internal static MudBunitContext NewContext(TeamWebApplicationFactory factory)
    {
        MudBunitContext ctx = UiTestHost.Create(services => services.CopyFrom(factory.Services, ServicePresets.Agents));

        // The card's avatar dialog hosts AvatarEditor, which injects the library locator and loader (U26, UX-R18).
        AvatarLibraryPickSupport.RegisterUnconfigured(ctx.Services);
        return ctx;
    }
    /// <summary>Renders <see cref="AgentCardTestHost"/>, the page that opens the card, with the popover and dialog providers <see cref="MudBunitContext.RenderWithPopovers"/> supplies so an opened card actually renders (see the type-level remarks) and a <c>MudSelect</c> inside it can too.</summary>
    internal static IRenderedComponent<ContainerFragment> RenderPage(MudBunitContext ctx, TeamWebApplicationFactory factory)
    {
        _ = factory;
        return ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<AgentCardTestHost>(0);
            builder.CloseComponent();
        });
    }

    /// <summary>
    /// Renders the page and clicks the tile matching <paramref name="personaName"/> exactly,
    /// opening its View card.
    /// </summary>
    /// <remarks>
    /// Matches the tile's own <c>.agents-item-name</c> span - the exact Name text and nothing
    /// else - rather than a substring <c>Contains</c> against the whole tile. Since D15's
    /// <see cref="Agency.Huddle.App.Agents.BuiltinAgentSeeder"/> writes a real "Chief of
    /// Staff" whenever no other Persona already carries its marker, a test seeding its own Persona
    /// literally named "Chief of Staff" now shares the page with a free-name "Chief of Staff 2" -
    /// whose tile text also CONTAINS "Chief of Staff" - and a substring match would click that one
    /// instead.
    ///
    /// The page's <c>OnInitializedAsync</c> finishes on a real SQLite lookup per tile, so its
    /// completion render can land between finding a tile and clicking it - which is why the click
    /// goes through <see cref="ClickAsync"/>, and why this waits for the dialog rather than assuming
    /// the click painted it synchronously.
    /// </remarks>
    internal static async Task<IRenderedComponent<ContainerFragment>> OpenViewCardAsync(MudBunitContext ctx, TeamWebApplicationFactory factory, string personaName)
    {
        var cut = RenderPage(ctx, factory);
        await ClickAsync(cut, () =>
        {
            var nameSpan = cut.FindAll(".agents-item-name")
                .First(span => string.Equals(span.TextContent.Trim(), personaName, StringComparison.Ordinal));
            return nameSpan.Closest("button.agent-tile")
                ?? throw new InvalidOperationException($"No agent-tile ancestor found for Persona '{personaName}'.");
        });
        cut.WaitForElement(".mud-dialog-container");
        return cut;
    }

    /// <summary>Writes a minimally-valid Persona file (Name, Title and Alias all <paramref name="name"/>), creating the factory's Agents directory first.</summary>
    internal static async Task SeedPersonaAsync(TeamWebApplicationFactory factory, string name, string body) =>
        await factory.WriteDefinitionAsync(SanitizeFileName(name), $"---\nName: {name}\nTitle: {name}\nAlias: {name}\n---\n{body}", CancellationToken.None);

    /// <summary>ADR-0031: a definition lives inside its own agent folder, so this creates <c>Agents/coo/</c> and returns <c>coo.md</c> inside it.</summary>
    internal static string EnsureTeamsDir(TeamWebApplicationFactory factory)
    {
        var agentFolder = Path.Combine(factory.AgentsDirPath, "coo");
        Directory.CreateDirectory(agentFolder);
        return Path.Combine(agentFolder, "coo.md");
    }

    internal static string SanitizeFileName(string name) => name.Replace(' ', '_');

    /// <summary>
    /// Adds a Persona directly through the factory's real <see cref="PersonaStore"/>, with a stored
    /// Model and/or Effort - the only way to seed those, since they live in the SQLite-backed
    /// <c>persona_models</c>/<c>persona_efforts</c> tables, not the Persona file itself.
    /// </summary>
    /// <param name="factory">The factory whose <see cref="PersonaStore"/> to add the Persona through.</param>
    /// <param name="name">The Persona's Name, Title and Alias alike.</param>
    /// <param name="body">The Persona's system-prompt body.</param>
    /// <param name="model">The Model to store, or <see langword="null"/> for the agent's default.</param>
    /// <param name="effort">The Effort to store, or <see langword="null"/> for the model's default.</param>
    internal static void SeedPersonaWithModelAndEffort(TeamWebApplicationFactory factory, string name, string body, string? model, string? effort)
    {
        factory.Services.GetRequiredService<PersonaStore>().Add(new PersonaIdentity(name, name, name, []), body, model, effort);
    }

    /// <summary>
    /// The <see cref="SeedPersonaWithModelAndEffort"/> sibling that also seeds an Adapter through
    /// <see cref="PersonaIdentity.Adapter"/> — the only way to seed one, since it travels with the
    /// frontmatter rather than through a separate store (Spec §7.1, §7.2).
    /// </summary>
    /// <param name="factory">The factory whose <see cref="PersonaStore"/> to add the Persona through.</param>
    /// <param name="name">The Persona's Name, Title and Alias alike.</param>
    /// <param name="body">The Persona's system-prompt body.</param>
    /// <param name="model">The Model to store, or <see langword="null"/> for the agent's default.</param>
    /// <param name="effort">The Effort to store, or <see langword="null"/> for the model's default.</param>
    /// <param name="adapter">The Adapter id to store, or <see langword="null"/> for the installation's default.</param>
    internal static void SeedPersonaWithModelEffortAndAdapter(TeamWebApplicationFactory factory, string name, string body, string? model, string? effort, string? adapter)
    {
        factory.Services.GetRequiredService<PersonaStore>().Add(new PersonaIdentity(name, name, name, [], adapter), body, model, effort);
    }

    /// <summary>
    /// The <see cref="SeedPersonaWithModelEffortAndAdapter"/> sibling that also seeds a Work Mode
    /// (ADR-0033), which lives in the SQLite-backed <c>persona_work_modes</c> table, so a store call is
    /// the only way to seed one.
    /// </summary>
    /// <param name="factory">The factory whose <see cref="PersonaStore"/> to add the Persona through.</param>
    /// <param name="name">The Persona's Name, Title and Alias alike.</param>
    /// <param name="body">The Persona's system-prompt body.</param>
    /// <param name="workMode">The Work Mode to store, or <see langword="null"/> for the Adapter's own.</param>
    /// <param name="adapter">The Adapter id to store, or <see langword="null"/> for the installation's default.</param>
    internal static void SeedPersonaWithWorkMode(TeamWebApplicationFactory factory, string name, string body, string? workMode, string? adapter = null)
    {
        factory.Services.GetRequiredService<PersonaStore>().Add(new PersonaIdentity(name, name, name, [], adapter), body, model: null, effort: null, workMode: workMode);
    }

    /// <summary>Registers <paramref name="name"/>'s Agent with the real <see cref="ITeamDirectory"/> and marks it connected in <see cref="TeamWebApplicationFactory.FakeAgentGateway"/>, so <see cref="PersonaStatusResolver"/> resolves it Online.</summary>
    internal static async Task MakeOnlineAsync(TeamWebApplicationFactory factory, string name)
    {
        var directory = factory.Services.GetRequiredService<ITeamDirectory>();
        await directory.InitializeAsync("You");
        var agent = await directory.UpsertAgentUserAsync(name, null);
        Assert.NotNull(agent);
        factory.FakeAgentGateway.SetOnline(agent.Id);
    }

    /// <summary>
    /// Whether any rendered button's trimmed text, OR its <c>aria-label</c>, equals <paramref name="text"/>.
    /// The Remove button became an icon-only <c>MudIconButton</c> (see <c>AgentProfileCardTests.ViewMode_ShowsDetailsAndActions</c>
    /// and its neighbours) and carries no text content at all - <c>aria-label</c> is the reliable, accessible
    /// handle for it, and this helper is deliberately not scoped to just that one button so every other
    /// text-button caller keeps working unchanged.
    /// </summary>
    internal static bool HasButton(IRenderedComponent<ContainerFragment> cut, string text) =>
        cut.FindAll("button").Any(button => MatchesButtonLabel(button, text));

    /// <summary>
    /// Opens the card's more-actions menu for <paramref name="name"/> and chooses "Delete agent", which swaps the menu for the
    /// inline confirm. The menu's activator is named "Actions for {name}" and has no tooltip (design-system "Menus").
    /// </summary>
    /// <param name="cut">The rendered page holding the card.</param>
    /// <param name="name">The Agent whose card is open.</param>
    internal static async Task OpenDeleteAsync(IRenderedComponent<ContainerFragment> cut, string name)
    {
        await ClickButtonAsync(cut, "Actions for " + name);
        await ClickAsync(cut, () => cut.Find(".menu-item-destructive"));
    }

    /// <summary>The first rendered button whose trimmed text, OR its <c>aria-label</c>, equals <paramref name="text"/> - see <see cref="HasButton"/>.</summary>
    internal static IElement FindButton(IRenderedComponent<ContainerFragment> cut, string text) =>
        cut.FindAll("button").First(button => MatchesButtonLabel(button, text));

    /// <summary>Clicks the button <see cref="FindButton"/> names, through <see cref="ClickAsync"/> - see its remarks.</summary>
    /// <param name="cut">The rendered page holding the card.</param>
    /// <param name="text">The button's trimmed text or <c>aria-label</c>.</param>
    internal static Task ClickButtonAsync(IRenderedComponent<ContainerFragment> cut, string text) =>
        ClickAsync(cut, () => FindButton(cut, text));

    /// <summary>Clicks the element <paramref name="find"/> resolves, through <see cref="DispatchWhenPresentAsync"/> - see its remarks.</summary>
    /// <param name="cut">The rendered page holding the card.</param>
    /// <param name="find">Resolves the element against the current DOM, throwing when it is absent.</param>
    internal static Task ClickAsync(IRenderedComponent<ContainerFragment> cut, Func<IElement> find) =>
        DispatchWhenPresentAsync(cut, find, static element => { _ = element.ClickAsync(); });

    /// <summary>
    /// Waits until <paramref name="find"/> resolves an element, then resolves it AGAIN and raises
    /// <paramref name="raise"/> on it inside one turn of the renderer's dispatcher.
    /// </summary>
    /// <remarks>
    /// Neither the page nor the card is synchronous end to end: the opener's lookups
    /// and a View/Edit card's own <c>OnInitializedAsync</c> each finish on a real SQLite lookup, and
    /// their completion renders land on the dispatcher whenever that lookup answers - under full-suite
    /// load, often mid-test. A bare synchronous click from the test thread races them twice
    /// over: the element may not be painted yet (<c>Sequence contains no matching element</c>), or a
    /// render can land between the find and the event and retire the element's handler id
    /// (<see cref="UnknownEventHandlerIdException"/>). Waiting fixes the first; finding and raising
    /// inside <c>InvokeAsync</c>, where no render can interleave, fixes the second. The event is
    /// raised, not awaited, exactly as the plain <c>Click()</c> it replaces - a handler parked on a
    /// never-completing catalog probe must not hang the test.
    /// </remarks>
    /// <param name="cut">The rendered page holding the card.</param>
    /// <param name="find">Resolves the element against the current DOM, throwing when it is absent.</param>
    /// <param name="raise">Raises the event on the freshly resolved element.</param>
    internal static async Task DispatchWhenPresentAsync(IRenderedComponent<ContainerFragment> cut, Func<IElement> find, Action<IElement> raise)
    {
        cut.WaitForAssertion(() => _ = find());
        await cut.InvokeAsync(() => raise(find()));
    }

    internal static bool MatchesButtonLabel(IElement button, string text) =>
        string.Equals(button.TextContent.Trim(), text, StringComparison.Ordinal)
        || string.Equals(button.GetAttribute("aria-label"), text, StringComparison.Ordinal);

    internal static IElement FindInputControl(IRenderedComponent<ContainerFragment> cut, string label) =>
        cut.FindAll("div.mud-input-control")
            .First(control => control.QuerySelectorAll("label").Any(l => l.TextContent.Contains(label, StringComparison.Ordinal)));

    /// <summary>
    /// The number of rendered <c>.mud-input-control</c>s whose own label contains <paramref name="label"/> -
    /// used to prove the Adapter select is absent (0) or renders exactly once (1), since a hidden
    /// <c>@if</c> block leaves no element at all for <see cref="FindInputControl"/>'s single-match
    /// lookup to find, and <c>Assert.DoesNotContain</c> on markup text would be fooled by "Adapter"
    /// appearing in this file's own doc comments once compiled into nothing of the sort - counting
    /// actual rendered controls is the only honest oracle for absence.
    /// </summary>
    internal static int CountControlsLabelled(IRenderedComponent<ContainerFragment> cut, string label) =>
        cut.FindAll("div.mud-input-control").Count(control => control.QuerySelectorAll("label").Any(l => l.TextContent.Contains(label, StringComparison.Ordinal)));

    /// <summary>
    /// Builds a real <see cref="AdapterCatalog"/> over in-memory <see cref="TeamOptions"/> - a pure
    /// configuration object, not a fake (Spec §6.6) - holding one <see cref="AdapterProfileOptions"/>
    /// entry per <paramref name="profiles"/> pair, in order.
    /// </summary>
    /// <param name="profiles">Each configured Adapter's (Id, DisplayName) pair.</param>
    internal static AdapterCatalog BuildAdapterCatalog(params (string Id, string DisplayName)[] profiles)
    {
        var acp = new AcpOptions
        {
            Adapters = profiles
                .Select(profile => new AdapterProfileOptions { Id = profile.Id, DisplayName = profile.DisplayName, Command = "node" })
                .ToList(),
        };

        return new AdapterCatalog(TestOptions.Create(opts => opts.Acp = acp));
    }

    /// <summary>The <c>&lt;input&gt;</c> under a <c>MudSelect</c> labelled <paramref name="label"/> - its closed-state value is the currently selected item's own display text, which is how a "preselected" assertion can be made without opening the popover at all. Since MudBlazor 9.12 it is <c>type="hidden"</c> and carries no ARIA attributes: read those off <see cref="FindSelectCombobox"/>.</summary>
    internal static IElement FindSelectInput(IRenderedComponent<ContainerFragment> cut, string label) =>
        FindInputControl(cut, label).QuerySelector("input") ?? throw new InvalidOperationException($"No input under the '{label}' select.");

    /// <summary>The focusable <c>role="combobox"</c> element under a <c>MudSelect</c> labelled <paramref name="label"/> - since MudBlazor 9.12 the element a screen reader lands on, so the one that carries <c>aria-label</c>, <c>aria-busy</c> and the other attributes passed to the select.</summary>
    internal static IElement FindSelectCombobox(IRenderedComponent<ContainerFragment> cut, string label) =>
        FindInputControl(cut, label).QuerySelector("[role=combobox]") ?? throw new InvalidOperationException($"No combobox under the '{label}' select.");

    /// <summary>
    /// Mouses down on the <c>MudSelect</c> labelled <paramref name="label"/> and awaits its handler -
    /// found and raised inside one dispatcher turn for the same reason as
    /// <see cref="DispatchWhenPresentAsync"/>, but awaited, unlike it, because
    /// <see cref="OpenSelectAndListOptionsAsync"/> reads the popover straight afterwards.
    /// </summary>
    /// <param name="cut">The rendered page holding the card.</param>
    /// <param name="label">The select's label text.</param>
    internal static async Task OpenSelectAsync(IRenderedComponent<ContainerFragment> cut, string label)
    {
        cut.WaitForAssertion(() => _ = FindInputControl(cut, label));
        await cut.InvokeAsync(() => FindInputControl(cut, label).MouseDownAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs()));
    }

    /// <summary>
    /// Whether the <c>MudSelect</c> labelled <paramref name="label"/> is refusing interaction, which is
    /// how the card says "this list is still being probed" (issue #39). While the list is read the select is disabled
    /// (so it refuses to open) but styled as busy, not off (audit E5): it carries <c>aria-busy</c> beside a progress bar. Asserted off the
    /// rendered attributes, not the component's parameter: what matters is what the control does, not what it was passed.
    /// A <c>readonly</c> attribute would be useless here - MudSelect puts it on the input unconditionally, because a
    /// select's text box is never typeable.
    /// </summary>
    /// <param name="cut">The rendered page holding the card.</param>
    /// <param name="label">The select's label text.</param>
    /// <returns><see langword="true"/> when the select's input is disabled or its combobox is marked busy.</returns>
    internal static bool IsSelectInert(IRenderedComponent<ContainerFragment> cut, string label) =>
        FindSelectInput(cut, label).HasAttribute("disabled") || FindSelectCombobox(cut, label).HasAttribute("aria-busy");

    /// <summary>
    /// Clicks the <c>MudSelect</c> labelled <paramref name="label"/> and returns the options that
    /// actually appeared. Empty means the popover refused to open - the behavioural half of
    /// <see cref="IsSelectInert"/>, and the one a user would notice. <c>MudSelect</c> renders no
    /// <c>&lt;option&gt;</c> elements, so <c>.mud-list-item</c> is the only honest oracle here.
    /// </summary>
    /// <param name="cut">The rendered page holding the card.</param>
    /// <param name="label">The select's label text.</param>
    /// <returns>The rendered option elements, empty when the select did not open.</returns>
    internal static async Task<IReadOnlyList<IElement>> OpenSelectAndListOptionsAsync(IRenderedComponent<ContainerFragment> cut, string label)
    {
        await OpenSelectAsync(cut, label);
        await Task.Yield();
        return cut.FindAll("div.mud-list-item");
    }

    /// <summary>
    /// Clicks the open select option whose trimmed text is exactly <paramref name="text"/>, waiting for
    /// the popover to paint it rather than yielding once and hoping - see <see cref="DispatchWhenPresentAsync"/>.
    /// </summary>
    /// <param name="cut">The rendered page holding the card.</param>
    /// <param name="text">The option's display text.</param>
    internal static Task ClickSelectItemAsync(IRenderedComponent<ContainerFragment> cut, string text) =>
        ClickAsync(cut, () => cut.FindAll("div.mud-list-item").First(item => string.Equals(item.TextContent.Trim(), text, StringComparison.Ordinal)));

    /// <summary>Ticks or unticks the Skills list's checkbox for <paramref name="skillName"/>, found by its row's <c>data-skill</c>.</summary>
    /// <param name="cut">The rendered page holding the card.</param>
    /// <param name="skillName">The Skill's name.</param>
    /// <param name="ticked">Whether to tick it.</param>
    internal static Task SetSkillTickedAsync(IRenderedComponent<ContainerFragment> cut, string skillName, bool ticked) =>
        cut.InvokeAsync(() => cut.Find($".agent-profile-card-skill-row[data-skill='{skillName}'] input[type=checkbox]").ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = ticked }));

    /// <summary>Raises <c>@onchange</c> with <paramref name="value"/> on the input or textarea labelled <paramref name="label"/>, through <see cref="DispatchWhenPresentAsync"/>.</summary>
    /// <param name="cut">The rendered page holding the card.</param>
    /// <param name="label">The control's label text.</param>
    /// <param name="value">The text to commit.</param>
    internal static Task SetTextValueAsync(IRenderedComponent<ContainerFragment> cut, string label, string value) =>
        DispatchWhenPresentAsync(
            cut,
            () =>
            {
                var control = FindInputControl(cut, label);
                return control.QuerySelector("input") ?? control.QuerySelector("textarea") ?? throw new InvalidOperationException($"No input/textarea under '{label}'.");
            },
            input => input.Change(value));
}
