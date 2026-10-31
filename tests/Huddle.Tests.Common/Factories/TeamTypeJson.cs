using System.Text.Json;

namespace Agency.Huddle.Tests.Factories;

/// <summary>
/// Builds <c>team-type.json</c> manifest text for tests, so no test copies the manifest literal. The defaults are the
/// most common literal: workspace kind "none", one default process <c>flow</c>, and one required manager role <c>lead</c>.
/// A deliberately malformed manifest is written raw, not through this class.
/// </summary>
internal static class TeamTypeJson
{
    /// <summary>The default <c>roles</c> array: one required manager role <c>lead</c> whose candidate is <c>candidates/lead.md</c>.</summary>
    public const string DefaultRolesJson = """[{ "id": "lead", "title": "Lead", "candidate": "candidates/lead.md", "manager": true, "required": true }]""";

    /// <summary>Builds the manifest text of a team type.</summary>
    /// <param name="id">The team type id.</param>
    /// <param name="name">The display name; <see langword="null"/> uses <paramref name="id"/>.</param>
    /// <param name="order">The sort order.</param>
    /// <param name="version">The manifest version.</param>
    /// <param name="workspaceKind">The workspace kind; the label is the kind with an upper-case first letter.</param>
    /// <param name="rolesJson">The complete <c>roles</c> array text, brackets included; <see langword="null"/> gives <see cref="DefaultRolesJson"/>.</param>
    /// <param name="processFile">The default process's file under the processes folder.</param>
    /// <param name="extraJson">Further top-level members, written as <c>"key": value</c> text without a leading comma; <see langword="null"/> adds none.</param>
    public static string Manifest(
        string id,
        string? name = null,
        int order = 1,
        string version = "1.0.0",
        string workspaceKind = "none",
        string? rolesJson = null,
        string processFile = "processes/flow.md",
        string? extraJson = null)
    {
        string label = workspaceKind.Length == 0 ? workspaceKind : string.Concat(char.ToUpperInvariant(workspaceKind[0]).ToString(), workspaceKind.AsSpan(1));
        string extra = extraJson is null ? string.Empty : $", {extraJson}";
        return $$"""
            { "schemaVersion": 1, "id": {{Quote(id)}}, "version": {{Quote(version)}}, "displayName": {{Quote(name ?? id)}}, "order": {{order}},
              "workspace": { "kind": {{Quote(workspaceKind)}}, "label": {{Quote(label)}}, "required": false },
              "processes": [{ "id": "flow", "file": {{Quote(processFile)}}, "default": true }],
              "roles": {{rolesJson ?? DefaultRolesJson}}{{extra}} }
            """;
    }

    /// <summary>Writes <c>team-type.json</c> into <paramref name="folder"/>, creating the folder; the parameters are those of <see cref="Manifest"/>.</summary>
    /// <param name="folder">The team type's folder.</param>
    /// <param name="id">The team type id.</param>
    /// <param name="name">The display name; <see langword="null"/> uses <paramref name="id"/>.</param>
    /// <param name="order">The sort order.</param>
    /// <param name="version">The manifest version.</param>
    /// <param name="workspaceKind">The workspace kind.</param>
    /// <param name="rolesJson">The complete <c>roles</c> array text; <see langword="null"/> gives <see cref="DefaultRolesJson"/>.</param>
    /// <param name="processFile">The default process's file under the processes folder.</param>
    /// <param name="extraJson">Further top-level members; <see langword="null"/> adds none.</param>
    public static void WriteManifest(
        string folder,
        string id,
        string? name = null,
        int order = 1,
        string version = "1.0.0",
        string workspaceKind = "none",
        string? rolesJson = null,
        string processFile = "processes/flow.md",
        string? extraJson = null)
    {
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "team-type.json"), Manifest(id, name, order, version, workspaceKind, rolesJson, processFile, extraJson));
    }

    /// <summary>Quotes <paramref name="value"/> as a JSON string, escaping what JSON requires (a Windows path's backslashes).</summary>
    /// <param name="value">The text to quote.</param>
    private static string Quote(string value) => JsonSerializer.Serialize(value);
}
