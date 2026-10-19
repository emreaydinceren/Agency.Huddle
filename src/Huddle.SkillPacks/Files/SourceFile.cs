using Agency.Huddle.SkillPacks.Checking;
using Agency.Huddle.SkillPacks.Reading;
using System.Globalization;
using System.Text.Json;

namespace Agency.Huddle.SkillPacks.Files;

/// <summary>A <c>sources/&lt;source&gt;/source.json</c> file (§7.2). Written by the AI tool; Huddle reads it.</summary>
/// <param name="SchemaVersion">Always 1 once parsed.</param>
/// <param name="Kind">Where the files came from.</param>
/// <param name="Url">The Source's URL; required for github and git.</param>
/// <param name="Ref">The branch or tag followed.</param>
/// <param name="Commit">The full commit fetched.</param>
/// <param name="LocalPath">Where the clone is, for the AI tool's next visit.</param>
/// <param name="FetchedAt">When it was fetched.</param>
/// <param name="FetchedBy">The AI tool's name.</param>
/// <param name="SignIn">Whether a sign-in is needed.</param>
/// <param name="Status">The Source's status.</param>
/// <param name="StatusReason">One sentence for the Human when not active.</param>
/// <param name="MovedTo">Where a moved Source went.</param>
public sealed record SourceFile(
    int SchemaVersion,
    SourceKind Kind,
    string? Url,
    string? Ref,
    string? Commit,
    PathEntries? LocalPath,
    DateTimeOffset FetchedAt,
    string FetchedBy,
    SignInMode SignIn,
    SourceStatus Status,
    string? StatusReason,
    string? MovedTo)
{
    /// <summary>The file's name.</summary>
    public const string FileName = "source.json";

    /// <summary>Reads a <c>source.json</c>; <paramref name="filePath"/> is its full path.</summary>
    public static ParseResult<SourceFile> Parse(string json, string filePath)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(filePath);
        using JsonDocument? document = JsonFields.TryParse(json);
        if (document is null || document.RootElement.ValueKind != JsonValueKind.Object)
        {
            return Fail("O001", filePath, "source.json is not a JSON object.");
        }

        JsonElement root = document.RootElement;
        int? schema = JsonFields.Int(root, "schemaVersion");
        if (schema != PackFile.KnownSchemaVersion)
        {
            return Fail("O001", filePath, schema is null ? "schemaVersion is missing." : $"schemaVersion {schema} is not one this Huddle knows (1).");
        }

        List<string> missing = [];
        SourceKind? kind = ParseEnum<SourceKind>(JsonFields.String(root, "kind"));
        SignInMode? signIn = ParseEnum<SignInMode>(JsonFields.String(root, "signIn"));
        SourceStatus? status = ParseEnum<SourceStatus>(JsonFields.String(root, "status"));
        string? fetchedBy = JsonFields.String(root, "fetchedBy");
        string? url = JsonFields.String(root, "url");
        DateTimeOffset? fetchedAt = ParseTime(JsonFields.String(root, "fetchedAt"));
        AddIf(missing, kind is null, "kind");
        AddIf(missing, fetchedAt is null, "fetchedAt");
        AddIf(missing, string.IsNullOrWhiteSpace(fetchedBy), "fetchedBy");
        AddIf(missing, signIn is null, "signIn");
        AddIf(missing, status is null, "status");
        AddIf(missing, kind is SourceKind.Github or SourceKind.Git && string.IsNullOrWhiteSpace(url), "url");
        if (missing.Count > 0 || kind is not { } kindValue || signIn is not { } signInValue || status is not { } statusValue || fetchedAt is not { } fetchedAtValue || fetchedBy is null)
        {
            return Fail("O001", filePath, $"Missing or unknown: {string.Join(", ", missing)}.");
        }

        PathEntries? localPath = JsonFields.Get(root, "localPath") is { } localValue ? PathEntries.FromJson(localValue) : null;
        string? reason = JsonFields.String(root, "statusReason");
        string? movedTo = JsonFields.String(root, "movedTo");
        List<Finding> findings = StatusFindings(statusValue, reason, movedTo, filePath);
        SourceFile file = new(
            PackFile.KnownSchemaVersion,
            kindValue,
            url,
            JsonFields.String(root, "ref"),
            JsonFields.String(root, "commit"),
            localPath,
            fetchedAtValue,
            fetchedBy,
            signInValue,
            statusValue,
            reason,
            movedTo);
        return new ParseResult<SourceFile>(file, findings);
    }

    /// <summary>The O003 findings for a status that needs a reason or a destination it does not have.</summary>
    private static List<Finding> StatusFindings(SourceStatus status, string? reason, string? movedTo, string filePath)
    {
        List<Finding> findings = [];
        if (status != SourceStatus.Active && string.IsNullOrWhiteSpace(reason))
        {
            findings.Add(Finding.Error("O003", filePath, $"status is {Lower(status)} but statusReason is empty."));
        }

        if (status == SourceStatus.Moved && string.IsNullOrWhiteSpace(movedTo))
        {
            findings.Add(Finding.Error("O003", filePath, "status is moved but movedTo is empty."));
        }

        return findings;
    }

    /// <summary>The lowercase name an enum value is written with.</summary>
    public static string Lower<TEnum>(TEnum value) where TEnum : struct, Enum
    {
#pragma warning disable HAA0102 // Enum.ToString returns the runtime's cached name, so a lookup table would save nothing.
        return new string([.. value.ToString().Select(char.ToLowerInvariant)]);
#pragma warning restore HAA0102
    }

    /// <summary>An enum value from its lowercase name, or null.</summary>
    private static TEnum? ParseEnum<TEnum>(string? text) where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(text) || !text.All(char.IsAsciiLetterLower))
        {
            return null;
        }

        return Enum.TryParse(text, ignoreCase: true, out TEnum value) ? value : null;
    }

    /// <summary>An ISO-8601 time with an offset, or null.</summary>
    private static DateTimeOffset? ParseTime(string? text)
    {
        return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset value) ? value : null;
    }

    /// <summary>Adds <paramref name="field"/> when <paramref name="condition"/> holds.</summary>
    private static void AddIf(List<string> missing, bool condition, string field)
    {
        if (condition)
        {
            missing.Add(field);
        }
    }

    /// <summary>A failed parse with one error.</summary>
    private static ParseResult<SourceFile> Fail(string code, string filePath, string message)
    {
        return new ParseResult<SourceFile>(null, [Finding.Error(code, filePath, message)]);
    }
}
