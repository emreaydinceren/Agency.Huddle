namespace Agency.Huddle.App.Gateway;

/// <summary>Which routes the gate lets through without a session (Spec 6.8, C-1).</summary>
internal static class GatewayPaths
{
    private static readonly string[] PublicPrefixes = ["/auth", "/lib/noble"];
    private static readonly string[] PublicFiles = ["/auth.js", "/auth-core.js", "/auth.css"];

    /// <summary>How the gate treats a route.</summary>
    internal enum Kind
    {
        /// <summary>A route that needs a session.</summary>
        Protected,

        /// <summary>The connect page, its endpoints and its scripts: no session, counted by the pre-auth limiter.</summary>
        Public,

        /// <summary><c>/health</c> and <c>/_desktop/*</c>: no session, loopback only.</summary>
        LoopbackOnly,
    }

    /// <summary>Classifies a request path.</summary>
    /// <param name="path">The request path.</param>
    public static Kind Of(PathString path)
    {
        if (path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase) || path.StartsWithSegments("/_desktop", StringComparison.OrdinalIgnoreCase))
        {
            return Kind.LoopbackOnly;
        }

        bool isPublic = PublicPrefixes.Any(prefix => path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase))
            || PublicFiles.Any(file => string.Equals(path.Value, file, StringComparison.OrdinalIgnoreCase));
        return isPublic ? Kind.Public : Kind.Protected;
    }
}
