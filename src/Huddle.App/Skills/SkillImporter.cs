using System.Text;
using System.Text.Json;

namespace Agency.Huddle.App.Skills;

/// <summary>What the user reviews before a skill is installed: where it comes from, what differs, what to look at (SK-2 to SK-6).</summary>
/// <param name="CatalogEntry">The catalog entry the skill matches, or <see langword="null"/> for a skill the catalog does not list.</param>
/// <param name="RepoUrl">The repository URL.</param>
/// <param name="Path">The skill's folder inside the repository.</param>
/// <param name="Commit">The commit the download is pinned to.</param>
/// <param name="Publisher">Who publishes the skill.</param>
/// <param name="License">The license identifier, or <c>none</c>.</param>
/// <param name="Name">The skill name from its frontmatter, or the folder name when it has none.</param>
/// <param name="Description">The description from its frontmatter, or empty.</param>
/// <param name="FrontmatterDifferences">Each way the downloaded name or description differs from the catalog entry, in words.</param>
/// <param name="Flags">The review flags for scripts, commands, network calls, outside paths and a missing license.</param>
/// <param name="ValidationProblems">Why the skill cannot be installed; empty when it can.</param>
/// <param name="CanInstall">Whether <see cref="SkillImporter.Install"/> accepts this preview.</param>
/// <param name="Content">The downloaded files; empty when the download failed.</param>
internal sealed record ImportPreview(
    SkillsCatalogEntry? CatalogEntry,
    string RepoUrl,
    string Path,
    string Commit,
    string Publisher,
    string License,
    string Name,
    string Description,
    IReadOnlyList<string> FrontmatterDifferences,
    IReadOnlyList<ReviewFlag> Flags,
    IReadOnlyList<string> ValidationProblems,
    bool CanInstall,
    DownloadedSkill Content);

/// <summary>The result of an install.</summary>
/// <param name="Installed">Whether the skill is installed (also <see langword="true"/> when the same import was already there).</param>
/// <param name="Error">What went wrong, or <see langword="null"/> on success.</param>
/// <param name="SkillFolder">The installed folder, or <see langword="null"/> on failure.</param>
internal sealed record ImportOutcome(bool Installed, string? Error, string? SkillFolder);

/// <summary>What the import screen and the hiring installer need from <see cref="SkillImporter"/>, so either can be tested without the network or the disk.</summary>
internal interface ISkillImporter
{
    /// <summary>Previews the catalog entry <paramref name="catalogId"/>.</summary>
    /// <param name="catalogId">The catalog entry id.</param>
    /// <param name="ct">Cancels the download.</param>
    /// <returns>The preview, or <see langword="null"/> when the catalog has no such entry.</returns>
    Task<ImportPreview?> PreviewFromCatalogAsync(string catalogId, CancellationToken ct);

    /// <summary>Previews the skill at a GitHub tree URL.</summary>
    /// <param name="githubTreeUrl">The tree URL.</param>
    /// <param name="ct">Cancels the requests.</param>
    /// <returns>The preview, or <see langword="null"/> when the text is not a GitHub tree URL.</returns>
    Task<ImportPreview?> PreviewFromUrlAsync(string githubTreeUrl, CancellationToken ct);

    /// <summary>Writes the previewed skill into the Skills folder.</summary>
    /// <param name="preview">The preview the user confirmed.</param>
    /// <returns>The outcome.</returns>
    ImportOutcome Install(ImportPreview preview);
}

/// <summary>
/// Orchestrates a skill import: preview the provenance, download, validate against the catalog and scan, and
/// write only on <see cref="Install"/> (SK-2 to SK-7). A preview never touches the disk.
/// </summary>
/// <param name="catalog">The skills catalog.</param>
/// <param name="downloader">Downloads a skill folder at a commit.</param>
/// <param name="skills">The skill store whose folder receives the install.</param>
/// <param name="http">Resolves a branch name to a commit through the GitHub API.</param>
/// <param name="clock">The clock for the import time; the system clock when omitted.</param>
internal sealed class SkillImporter(SkillsCatalogIndex catalog, SkillDownloader downloader, SkillStore skills, HttpClient http, TimeProvider? clock = null) : ISkillImporter
{
    private const int CommitLength = 40;

    private const string TempFolderPrefix = ".skill-import-";

    private const string SkillFileName = "SKILL.md";

    private readonly Lock installGate = new();

    /// <summary>Previews the catalog entry <paramref name="catalogId"/>.</summary>
    /// <param name="catalogId">The catalog entry id.</param>
    /// <param name="ct">Cancels the download.</param>
    /// <returns>The preview, or <see langword="null"/> when the catalog has no such entry.</returns>
    public async Task<ImportPreview?> PreviewFromCatalogAsync(string catalogId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(catalogId);

        SkillsCatalogEntry? entry = catalog.FindById(catalogId);
        if (entry is null)
        {
            return null;
        }

        return await this.BuildPreviewAsync(entry, entry.Repo, entry.Path, entry.Commit, entry.Publisher, entry.License, entry.Name, ct);
    }

    /// <summary>Previews the skill at a GitHub tree URL, <c>https://github.com/o/r/tree/&lt;commit-or-branch&gt;/&lt;path&gt;</c>.</summary>
    /// <param name="githubTreeUrl">The tree URL.</param>
    /// <param name="ct">Cancels the requests.</param>
    /// <returns>The preview, or <see langword="null"/> when the text is not a GitHub tree URL.</returns>
    public async Task<ImportPreview?> PreviewFromUrlAsync(string githubTreeUrl, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(githubTreeUrl);

        if (!SkillImporter.TryParseTreeUrl(githubTreeUrl, out string owner, out string repo, out string reference, out string path))
        {
            return null;
        }

        string repoUrl = $"https://github.com/{owner}/{repo}";
        SkillsCatalogEntry? entry = catalog.FindByRepoPath(repoUrl, path);
        string folderName = entry?.Name ?? SkillImporter.LastSegment(path, repo);
        string publisher = entry?.Publisher ?? owner;
        string license = entry?.License ?? "none";

        string? commit = SkillImporter.IsCommit(reference) ? reference : await this.ResolveCommitAsync(owner, repo, reference, ct);
        if (commit is null)
        {
            return SkillImporter.Failed(entry, repoUrl, path, reference, publisher, license, folderName, "The branch or commit could not be resolved.");
        }

        return await this.BuildPreviewAsync(entry, repoUrl, path, commit, publisher, license, folderName, ct);
    }

    /// <summary>
    /// Writes the previewed skill to <c>Skills/&lt;name&gt;/</c>: the files and <c>.source.json</c> go to a temporary folder that is then moved
    /// into place, so a failure never leaves a partial skill. An existing folder from the same import is a no-op success; any other
    /// existing folder of that name is refused.
    /// </summary>
    /// <param name="preview">The preview the user confirmed.</param>
    /// <returns>The outcome.</returns>
    public ImportOutcome Install(ImportPreview preview)
    {
        ArgumentNullException.ThrowIfNull(preview);

        if (!preview.CanInstall || preview.Content.Files.Count == 0 || !SkillImporter.IsFolderName(preview.Name))
        {
            return new ImportOutcome(false, "This skill cannot be installed.", null);
        }

        string target = Path.Combine(skills.SkillsDirectory, preview.Name);
        lock (this.installGate)
        {
            if (Directory.Exists(target) || File.Exists(target))
            {
                return SkillImporter.IsSameImport(target, preview)
                    ? new ImportOutcome(true, null, target)
                    : new ImportOutcome(false, $"A skill named '{preview.Name}' already exists and was not imported from this source.", null);
            }

            string temp = Path.Combine(Path.GetDirectoryName(skills.SkillsDirectory) ?? skills.SkillsDirectory, SkillImporter.TempFolderPrefix + Guid.NewGuid().ToString("N"));
            try
            {
                SkillImporter.WriteFiles(temp, preview.Content.Files);
                SkillProvenance provenance = new(
                    preview.RepoUrl,
                    preview.Path,
                    preview.Commit,
                    SkillFolderHash.Compute(preview.Content.Files),
                    preview.Publisher,
                    preview.License,
                    (clock ?? TimeProvider.System).GetUtcNow());
                provenance.Write(temp);
                Directory.Move(temp, target);
                return new ImportOutcome(true, null, target);
            }
            catch (IOException)
            {
                SkillImporter.DeleteQuietly(temp);
                return new ImportOutcome(false, "The skill could not be written.", null);
            }
            catch (UnauthorizedAccessException)
            {
                SkillImporter.DeleteQuietly(temp);
                return new ImportOutcome(false, "The skill could not be written.", null);
            }
            catch (InvalidOperationException)
            {
                SkillImporter.DeleteQuietly(temp);
                return new ImportOutcome(false, "The skill could not be written.", null);
            }
        }
    }

    /// <summary>Downloads, validates and scans a skill and assembles its preview.</summary>
    private async Task<ImportPreview> BuildPreviewAsync(
        SkillsCatalogEntry? entry,
        string repoUrl,
        string path,
        string commit,
        string publisher,
        string license,
        string folderName,
        CancellationToken ct)
    {
        DownloadResult download = await downloader.DownloadAsync(repoUrl, path, commit, ct);
        if (download.Skill is null)
        {
            return SkillImporter.Failed(entry, repoUrl, path, commit, publisher, license, folderName, download.Error ?? "The download failed.");
        }

        IReadOnlyDictionary<string, byte[]> files = download.Skill.Files;
        Dictionary<string, string> text = new(StringComparer.Ordinal);
        foreach ((string name, byte[] bytes) in files)
        {
            if (!name.Contains('/') && name.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            {
                text[name] = Encoding.UTF8.GetString(bytes);
            }
        }

        SkillValidation validation = SkillValidator.Validate(folderName, text);
        List<string> problems = [.. validation.Issues.Where(static issue => issue.Severity == SkillIssueSeverity.Error).Select(static issue => issue.Message)];
        return new ImportPreview(
            entry,
            repoUrl,
            path,
            commit,
            publisher,
            license,
            validation.Name ?? folderName,
            validation.Description ?? string.Empty,
            SkillImporter.Differences(entry, validation),
            SkillReviewScanner.Scan(files, license),
            problems,
            problems.Count == 0 && files.ContainsKey(SkillImporter.SkillFileName),
            download.Skill);
    }

    /// <summary>Resolves a branch, tag or commit name to a commit hash through the GitHub API.</summary>
    private async Task<string?> ResolveCommitAsync(string owner, string repo, string reference, CancellationToken ct)
    {
        Uri uri = new($"https://api.github.com/repos/{owner}/{repo}/commits/{Uri.EscapeDataString(reference)}");
        try
        {
            using HttpRequestMessage request = new(HttpMethod.Get, uri);
            request.Headers.UserAgent.ParseAdd("Agency.Huddle");
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            using HttpResponseMessage response = await http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            await using Stream body = await response.Content.ReadAsStreamAsync(ct);
            using JsonDocument document = await JsonDocument.ParseAsync(body, cancellationToken: ct);
            string? sha = document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("sha", out JsonElement value)
                && value.ValueKind == JsonValueKind.String
                    ? value.GetString()
                    : null;
            return sha is not null && SkillImporter.IsCommit(sha) ? sha : null;
        }
        catch (HttpRequestException)
        {
            // An unreachable API means the branch is unresolved; the preview reports that in words.
            return null;
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            // A request timeout is an unresolved branch, not a cancellation by the caller.
            return null;
        }
        catch (JsonException)
        {
            // A body that is not JSON cannot name a commit.
            return null;
        }
    }

    /// <summary>Lists each way the downloaded name or description differs from the catalog entry.</summary>
    private static List<string> Differences(SkillsCatalogEntry? entry, SkillValidation validation)
    {
        List<string> differences = [];
        if (entry is null)
        {
            return differences;
        }

        if (validation.Name is not null && !string.Equals(validation.Name, entry.Name, StringComparison.Ordinal))
        {
            differences.Add($"name: the catalog says '{entry.Name}' but the file says '{validation.Name}'");
        }

        if (validation.Description is not null && !string.Equals(validation.Description, entry.Description, StringComparison.Ordinal))
        {
            differences.Add($"description: the catalog says '{entry.Description}' but the file says '{validation.Description}'");
        }

        return differences;
    }

    /// <summary>Builds a preview for a skill that could not be fetched.</summary>
    private static ImportPreview Failed(
        SkillsCatalogEntry? entry,
        string repoUrl,
        string path,
        string commit,
        string publisher,
        string license,
        string folderName,
        string problem)
    {
        return new ImportPreview(
            entry,
            repoUrl,
            path,
            commit,
            publisher,
            license,
            folderName,
            string.Empty,
            [],
            [],
            [problem],
            false,
            new DownloadedSkill(new Dictionary<string, byte[]>(StringComparer.Ordinal)));
    }

    /// <summary>Whether the folder already holds this very import (same repository, path and commit).</summary>
    private static bool IsSameImport(string folder, ImportPreview preview)
    {
        return SkillProvenance.TryRead(folder, out SkillProvenance? existing)
            && existing is not null
            && string.Equals(existing.Repo, preview.RepoUrl, StringComparison.Ordinal)
            && string.Equals(existing.Path, preview.Path, StringComparison.Ordinal)
            && string.Equals(existing.Commit, preview.Commit, StringComparison.Ordinal);
    }

    /// <summary>Writes the files into a new folder, refusing any name that would land outside it.</summary>
    private static void WriteFiles(string folder, IReadOnlyDictionary<string, byte[]> files)
    {
        string root = Path.GetFullPath(folder) + Path.DirectorySeparatorChar;
        Directory.CreateDirectory(folder);
        foreach (string name in files.Keys.Order(StringComparer.Ordinal))
        {
            string full = Path.GetFullPath(Path.Combine(folder, name));
            if (!full.StartsWith(root, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("A file would be written outside the skill folder.");
            }

            _ = Directory.CreateDirectory(Path.GetDirectoryName(full) ?? folder);
            File.WriteAllBytes(full, files[name]);
        }
    }

    /// <summary>Deletes a temporary folder, ignoring a failure to do so.</summary>
    private static void DeleteQuietly(string folder)
    {
        try
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
        catch (IOException)
        {
            // A leftover temporary folder is harmless and is named so it is never read as a skill.
        }
        catch (UnauthorizedAccessException)
        {
            // Same as above: the leftover is inert.
        }
    }

    /// <summary>Whether the name is a single folder name that cannot climb out of the Skills folder.</summary>
    private static bool IsFolderName(string name)
    {
        return name.Length > 0
            && !string.Equals(name, ".", StringComparison.Ordinal)
            && !string.Equals(name, "..", StringComparison.Ordinal)
            && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
    }

    /// <summary>Splits a GitHub tree URL into owner, repository, ref and folder path.</summary>
    private static bool TryParseTreeUrl(string url, out string owner, out string repo, out string reference, out string path)
    {
        owner = string.Empty;
        repo = string.Empty;
        reference = string.Empty;
        path = string.Empty;
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)
            || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
            || !string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string[] parts = uri.AbsolutePath.Trim('/').Split('/').Select(Uri.UnescapeDataString).ToArray();
        if (parts.Length < 4 || !string.Equals(parts[2], "tree", StringComparison.Ordinal) || parts[..4].Any(static part => part.Length == 0))
        {
            return false;
        }

        owner = parts[0];
        repo = parts[1];
        reference = parts[3];
        path = string.Join('/', parts[4..]);
        return true;
    }

    /// <summary>The last segment of a path, or the fallback when the path is empty.</summary>
    private static string LastSegment(string path, string fallback)
    {
        int slash = path.LastIndexOf('/');
        string last = slash < 0 ? path : path[(slash + 1)..];
        return last.Length == 0 ? fallback : last;
    }

    /// <summary>Whether the text is exactly 40 hexadecimal characters.</summary>
    private static bool IsCommit(string text)
    {
        return text.Length == SkillImporter.CommitLength && text.All(static c => char.IsAsciiHexDigit(c));
    }
}
