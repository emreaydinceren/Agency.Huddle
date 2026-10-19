using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;

namespace Agency.Huddle.App.Tabs;

/// <summary>
/// Resolves a Tab's URL to a page without the <c>Router</c>. Two groups show two pages at once and the
/// router can mount only the one the browser URL names, so each group's page host asks this instead:
/// it reads the same <c>@page</c> templates the router would, picks the template with the most literal
/// segments (so <c>/tasks/new</c> beats <c>/tasks/{ViewId}</c>), and supplies route and query values to
/// the component's parameters as <c>RouteView</c> does.
/// </summary>
internal sealed class TabPageRoutes
{
    private static readonly ConcurrentDictionary<(Type Component, string Name), PropertyInfo?> ParameterProperties = new();

    private readonly IReadOnlyList<Route> routes;

    /// <summary>Reads the routable components of <paramref name="assembly"/>. The app registers its own assembly; a test registers one holding stub pages.</summary>
    /// <param name="assembly">The assembly whose <c>@page</c> components are the routes.</param>
    public TabPageRoutes(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        this.routes = Discover(assembly);
    }

    /// <summary>Finds the page for <paramref name="url"/>, or <see langword="null"/> when no <c>@page</c> template matches it.</summary>
    /// <param name="url">A site-relative address, with or without a query.</param>
    public TabPage? Resolve(string url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        string relative = TabKeys.ToRelative(url);
        int queryStart = relative.IndexOf('?', StringComparison.Ordinal);
        string path = queryStart >= 0 ? relative[..queryStart] : relative;
        string[] segments = [.. path
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(Uri.UnescapeDataString)];

        Route? best = null;
        Dictionary<string, string>? bestValues = null;
        foreach (Route route in this.routes)
        {
            if (route.TryMatch(segments, out Dictionary<string, string>? values) && (best is null || route.Literals > best.Literals))
            {
                best = route;
                bestValues = values;
            }
        }

        if (best is null || bestValues is null)
        {
            return null;
        }

        Dictionary<string, object> parameters = new(StringComparer.OrdinalIgnoreCase);
        foreach ((string name, string value) in bestValues)
        {
            Assign(best.Component, parameters, name, value);
        }

        if (queryStart >= 0)
        {
            foreach ((string name, Microsoft.Extensions.Primitives.StringValues values) in QueryHelpers.ParseQuery(relative[queryStart..]))
            {
                if (values.Count > 0 && values[0] is { } first && !parameters.ContainsKey(name))
                {
                    Assign(best.Component, parameters, name, first);
                }
            }
        }

        return new TabPage(best.Component, parameters);
    }

    private static void Assign(Type component, Dictionary<string, object> parameters, string name, string value)
    {
        PropertyInfo? property = ParameterProperties.GetOrAdd((component, name), static key =>
            key.Component
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(candidate =>
                    candidate.GetCustomAttribute<ParameterAttribute>() is not null
                    && string.Equals(candidate.Name, key.Name, StringComparison.OrdinalIgnoreCase)));

        if (property is not null && TryConvert(value, property.PropertyType, out object? converted))
        {
            parameters[property.Name] = converted;
        }
    }

    private static bool TryConvert(string value, Type target, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out object? converted)
    {
        Type type = Nullable.GetUnderlyingType(target) ?? target;
        if (type == typeof(string))
        {
            converted = value;
            return true;
        }

        if (type == typeof(int) && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number))
        {
            converted = number;
            return true;
        }

        if (type == typeof(bool) && bool.TryParse(value, out bool flag))
        {
            converted = flag;
            return true;
        }

        if (type == typeof(Guid) && Guid.TryParse(value, out Guid guid))
        {
            converted = guid;
            return true;
        }

        converted = null;
        return false;
    }

    private static List<Route> Discover(Assembly assembly)
    {
        List<Route> routes = [];
        foreach (Type type in assembly.GetTypes())
        {
            if (!typeof(IComponent).IsAssignableFrom(type) || type.IsAbstract)
            {
                continue;
            }

            foreach (RouteAttribute attribute in type.GetCustomAttributes<RouteAttribute>(inherit: false))
            {
                routes.Add(new Route(type, attribute.Template));
            }
        }

        return routes;
    }

    /// <summary>One <c>@page</c> template: literal segments and <c>{Name}</c> parameters. Constraints and catch-alls are not used by any page and are not supported.</summary>
    private sealed class Route
    {
        private readonly string[] template;

        public Route(Type component, string template)
        {
            this.Component = component;
            this.template = template.Split('/', StringSplitOptions.RemoveEmptyEntries);
            this.Literals = this.template.Count(static segment => !IsParameter(segment));
        }

        public Type Component { get; }

        public int Literals { get; }

        public bool TryMatch(string[] segments, out Dictionary<string, string>? values)
        {
            values = null;
            if (segments.Length != this.template.Length)
            {
                return false;
            }

            Dictionary<string, string> found = new(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < segments.Length; index++)
            {
                if (IsParameter(this.template[index]))
                {
                    found[this.template[index][1..^1]] = segments[index];
                }
                else if (!string.Equals(this.template[index], segments[index], StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            values = found;
            return true;
        }

        private static bool IsParameter(string segment) => segment.Length > 2 && segment[0] == '{' && segment[^1] == '}';
    }
}
