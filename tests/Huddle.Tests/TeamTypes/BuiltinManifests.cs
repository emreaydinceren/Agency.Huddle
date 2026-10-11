using Agency.Huddle.App.TeamTypes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.TeamTypes;

/// <summary>Loads a shipped team type the way the app does, through <see cref="TeamTypeCatalog"/> over the embedded types and hiring pool, so a test sees the roles composed from their pool jobs.</summary>
internal static class BuiltinManifests
{
    /// <summary>Loads a built-in type with the roles it lists itself; the pool jobs the catalog adds as optional roles are left out.</summary>
    /// <param name="typeId">The type id, such as <c>development</c>.</param>
    /// <returns>The type, with <see cref="RoleSpec.Pooled"/> roles removed.</returns>
    public static TeamTypeManifest Load(string typeId)
    {
        using TempDataDir dir = new();
        TeamTypeCatalog catalog = new(dir.Options(), NullLogger<TeamTypeCatalog>.Instance);
        TeamTypeManifest? manifest = catalog.Find(typeId);
        Assert.NotNull(manifest);
        return manifest with { Roles = [.. manifest.Roles.Where(static role => !role.Pooled)] };
    }

    /// <summary>Loads the embedded catalog and returns the problems it reported, so a test can assert every shipped type is valid.</summary>
    /// <returns>The rejected types, empty when every shipped type loaded.</returns>
    public static IReadOnlyList<TeamTypeFileProblem> Problems()
    {
        using TempDataDir dir = new();
        return new TeamTypeCatalog(dir.Options(), NullLogger<TeamTypeCatalog>.Instance).Problems;
    }
}
