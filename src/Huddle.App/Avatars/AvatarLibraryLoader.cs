using System.Text.Json;

namespace Agency.Huddle.App.Avatars;

/// <summary>
/// Reads the Avatar Library's <c>master.json</c> and single images from disk. It is stateless by design
/// (requirement R-1): it holds only its two dependencies, never the catalogue, and every call reads the file afresh.
/// </summary>
internal sealed class AvatarLibraryLoader(AvatarLibraryLocator locator, ILogger<AvatarLibraryLoader> logger) : IAvatarLibraryLoader
{
    private const string MasterFileName = "master.json";

    private const long MaxMasterBytes = 4 * 1024 * 1024;

    private const int MaxExamples = 5;

    private const int HeadBytes = 16;

    /// <inheritdoc />
    public async Task<AvatarLibraryLoadResult> LoadAsync(CancellationToken cancellationToken)
    {
        string? dir = locator.Dir;
        if (dir is null || !locator.IsConfigured)
        {
            logger.LogInformation("The Avatar Library is not configured or has no master.json.");
            return new AvatarLibraryLoadResult(null, 0, true);
        }

        string masterPath = Path.Combine(dir, MasterFileName);
        try
        {
            if (new FileInfo(masterPath).Length > MaxMasterBytes)
            {
                logger.LogWarning("The Avatar Library index {Path} is larger than 4 MB and was refused.", masterPath);
                return new AvatarLibraryLoadResult(null, 0, true);
            }

            FileStream stream = new(masterPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, useAsync: true);
            await using var streamScope = stream.ConfigureAwait(false);
            using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("avatars", out JsonElement avatars)
                || avatars.ValueKind != JsonValueKind.Array)
            {
                logger.LogWarning("The Avatar Library index {Path} has no avatars array.", masterPath);
                return new AvatarLibraryLoadResult(null, 0, true);
            }

            return this.Build(dir, avatars, cancellationToken);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "The Avatar Library index {Path} is not valid JSON.", masterPath);
            return new AvatarLibraryLoadResult(null, 0, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "The Avatar Library index {Path} could not be read.", masterPath);
            return new AvatarLibraryLoadResult(null, 0, true);
        }
    }

    /// <inheritdoc />
    public async Task<AvatarLibraryImage?> ReadImageAsync(string fileName, CancellationToken cancellationToken)
    {
        string? dir = locator.Dir;
        if (dir is null || !IsSafeImageName(fileName))
        {
            return null;
        }

        try
        {
            string path = Path.Combine(dir, fileName);
            FileInfo info = new(path);
            if (!info.Exists || info.Length > AvatarImage.MaxBytes)
            {
                return null;
            }

            byte[] bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            string? extension = bytes.Length > AvatarImage.MaxBytes ? null : AvatarImage.SniffExtension(bytes);
            return extension is null ? null : new AvatarLibraryImage(bytes, extension);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "The Avatar Library image {FileName} could not be read.", fileName);
            return null;
        }
    }

    private static bool IsSafeImageName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)
            || fileName.AsSpan().IndexOfAny('/', '\\') >= 0
            || !string.Equals(fileName, Path.GetFileName(fileName), StringComparison.Ordinal))
        {
            return false;
        }

        string extension = Path.GetExtension(fileName);
        return string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".webp", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ReadString(JsonElement record, string name)
    {
        return record.ValueKind == JsonValueKind.Object
            && record.TryGetProperty(name, out JsonElement value)
            && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
    }

    private static List<string> ReadTags(JsonElement record)
    {
        List<string> tags = [];
        if (record.ValueKind == JsonValueKind.Object
            && record.TryGetProperty("tags", out JsonElement array)
            && array.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement tag in array.EnumerateArray())
            {
                if (tag.ValueKind == JsonValueKind.String && tag.GetString() is string text)
                {
                    tags.Add(text);
                }
            }
        }

        return tags;
    }

    private static bool HasImageContent(string path)
    {
        FileInfo info = new(path);
        if (!info.Exists || info.Length > AvatarImage.MaxBytes)
        {
            return false;
        }

        byte[] head = new byte[HeadBytes];
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        int read = stream.ReadAtLeast(head, head.Length, throwOnEndOfStream: false);
        return AvatarImage.SniffExtension(head.AsSpan(0, read)) is not null;
    }

    private AvatarLibraryLoadResult Build(string dir, JsonElement avatars, CancellationToken cancellationToken)
    {
        List<LibraryAvatar> valid = [];
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        List<string> examples = [];
        int skipped = 0;

        foreach (JsonElement record in avatars.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();

            string? file = ReadString(record, "file");
            string? profession = ReadString(record, "profession");
            bool ok = !string.IsNullOrWhiteSpace(profession)
                && IsSafeImageName(file)
                && file is not null
                && !seen.Contains(file)
                && HasImageContent(Path.Combine(dir, file));

            if (!ok || file is null || profession is null)
            {
                skipped++;
                if (examples.Count < MaxExamples)
                {
                    examples.Add(file ?? "(no file)");
                }

                continue;
            }

            seen.Add(file);
            valid.Add(new LibraryAvatar(
                file,
                profession,
                ReadString(record, "skin_tone") ?? string.Empty,
                ReadString(record, "gender_presentation") ?? string.Empty,
                ReadTags(record)));
        }

        if (skipped > 0)
        {
            logger.LogWarning(
                "The Avatar Library skipped {Skipped} records, for example: {Examples}.",
                skipped,
                string.Join(", ", examples));
        }

        return new AvatarLibraryLoadResult(new AvatarCatalog(valid), skipped, false);
    }
}
