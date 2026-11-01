using Microsoft.CodeAnalysis;

namespace Agency.Huddle.Analyzers;

/// <summary>
/// Decides where a rule applies: only in the assembly that declares the helper its fix uses. A rule whose fix is out of
/// reach (the ACP effort's projects, test projects that only see the helper through <c>InternalsVisibleTo</c>) stays silent.
/// </summary>
internal static class OwnHelper
{
    /// <summary>Whether <paramref name="compilation"/> declares the type named <paramref name="metadataName"/> in its own source.</summary>
    /// <param name="compilation">The compilation being analysed.</param>
    /// <param name="metadataName">The helper's full metadata name.</param>
    /// <returns><see langword="true"/> when the helper is declared in this assembly.</returns>
    internal static bool IsDeclared(Compilation compilation, string metadataName) =>
        compilation.GetTypeByMetadataName(metadataName) is { } helper
        && SymbolEqualityComparer.Default.Equals(helper.ContainingAssembly, compilation.Assembly);
}
