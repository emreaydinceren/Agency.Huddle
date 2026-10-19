namespace Agency.Huddle.App.Tabs;

/// <summary>The page a Tab's URL names: its component type and the parameters the URL supplies.</summary>
/// <param name="Component">The routable component.</param>
/// <param name="Parameters">Route values and query values, converted to the types of the component's matching <c>[Parameter]</c> properties.</param>
internal sealed record TabPage(Type Component, IDictionary<string, object> Parameters);
