using System.Diagnostics.CodeAnalysis;

namespace Agency.Huddle.App.Teams;

/// <summary>An id pinned to a version, written <c>id@version</c> (for example <c>msbuild-authoring@1.0.0</c>).</summary>
/// <param name="Id">The referenced id.</param>
/// <param name="Version">The pinned version.</param>
public sealed record VersionedRef(string Id, string Version)
{
    /// <summary>Parses <c>id@version</c>; both parts must be non-blank and there must be exactly one <c>@</c>.</summary>
    /// <param name="text">The text to parse.</param>
    /// <param name="value">The parsed reference, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when <paramref name="text"/> was well formed.</returns>
    public static bool TryParse(string? text, [NotNullWhen(true)] out VersionedRef? value)
    {
        value = null;
        string[] parts = (text ?? string.Empty).Trim().Split('@');
        if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
        {
            return false;
        }

        value = new VersionedRef(parts[0].Trim(), parts[1].Trim());
        return true;
    }

    /// <inheritdoc />
    public override string ToString() => $"{this.Id}@{this.Version}";
}

/// <summary>A role of a team type pinned to a version, written <c>type/role@version</c>.</summary>
/// <param name="TypeId">The team type id.</param>
/// <param name="RoleId">The role id within the type.</param>
/// <param name="Version">The pinned team-type version.</param>
public sealed record RoleRef(string TypeId, string RoleId, string Version)
{
    /// <summary>Parses <c>type/role@version</c>; all parts must be non-blank.</summary>
    /// <param name="text">The text to parse.</param>
    /// <param name="value">The parsed reference, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when <paramref name="text"/> was well formed.</returns>
    public static bool TryParse(string? text, [NotNullWhen(true)] out RoleRef? value)
    {
        value = null;
        if (!VersionedRef.TryParse(text, out VersionedRef? versioned))
        {
            return false;
        }

        string[] parts = versioned.Id.Split('/');
        if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
        {
            return false;
        }

        value = new RoleRef(parts[0].Trim(), parts[1].Trim(), versioned.Version);
        return true;
    }

    /// <inheritdoc />
    public override string ToString() => $"{this.TypeId}/{this.RoleId}@{this.Version}";
}
