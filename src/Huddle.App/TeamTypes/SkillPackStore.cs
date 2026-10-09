using System.Reflection;
using System.Text.Json;
using Agency.Huddle.App.Teams;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.TeamTypes;

/// <summary>A suggested bundle of skills for some roles (Spec §Pack).</summary>
/// <param name="Id">The pack id, also its file name.</param>
/// <param name="Version">The pack version.</param>
/// <param name="Title">The title shown to the user.</param>
/// <param name="AppliesTo">The role ids the pack suits.</param>
/// <param name="ConsultWhenAdds">When a manager should consult the pack.</param>
/// <param name="Skills">The skills the pack bundles.</param>
internal sealed record SkillPack(string Id, string Version, string Title, IReadOnlyList<string> AppliesTo, string ConsultWhenAdds, IReadOnlyList<SkillRef> Skills);

/// <summary>A skill pack file that could not be loaded.</summary>
/// <param name="Source">Where the file came from.</param>
/// <param name="Message">Why it was rejected.</param>
internal sealed record SkillPackProblem(string Source, string Message);

/// <summary>Loads skill packs from built-ins and the user's SkillPacks folder (Spec §Pack, MS-4).</summary>
/// <remarks>
/// A user file <c>{DataDir}/SkillPacks/{id}.json</c> whose pack id equals a built-in's replaces it. A file that
/// cannot be read or fails validation becomes a <see cref="SkillPackProblem"/>; loading never throws for bad content.
/// </remarks>
internal sealed class SkillPackStore
{
    private const string ResourcePrefix = "TeamTypes/Builtin/SkillPacks/";

    private readonly string userDir;
    private readonly ILogger<SkillPackStore> logger;
    private readonly IReadOnlyDictionary<string, string> builtins;
    private volatile Snapshot snapshot = new([], []);

    /// <summary>Creates the store over the assembly's embedded built-in packs.</summary>
    /// <param name="options">Supplies <see cref="TeamOptions.DataDir"/>.</param>
    /// <param name="logger">Receives a warning per rejected file.</param>
    public SkillPackStore(IOptions<TeamOptions> options, ILogger<SkillPackStore> logger)
        : this(options, logger, ReadEmbedded())
    {
    }

    /// <summary>Creates the store over a given set of built-in pack files, for tests.</summary>
    /// <param name="options">Supplies <see cref="TeamOptions.DataDir"/>.</param>
    /// <param name="logger">Receives a warning per rejected file.</param>
    /// <param name="builtins">Built-in file name to JSON text.</param>
    internal SkillPackStore(IOptions<TeamOptions> options, ILogger<SkillPackStore> logger, IReadOnlyDictionary<string, string> builtins)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(builtins);
        this.userDir = Path.Combine(options.Value.DataDir, TeamPaths.SkillPacksDir);
        this.logger = logger;
        this.builtins = builtins;
        this.Reload();
    }

    /// <summary>Every loaded pack, ordered by id.</summary>
    public IReadOnlyList<SkillPack> All => this.snapshot.Packs;

    /// <summary>Every file that was rejected, with the reason.</summary>
    public IReadOnlyList<SkillPackProblem> Problems => this.snapshot.Problems;

    /// <summary>Finds a pack by id.</summary>
    /// <param name="id">The pack id.</param>
    /// <returns>The pack, or null when none has that id.</returns>
    public SkillPack? Get(string id)
    {
        return this.snapshot.Packs.FirstOrDefault(pack => string.Equals(pack.Id, id, StringComparison.Ordinal));
    }

    /// <summary>Lists the packs whose <see cref="SkillPack.AppliesTo"/> contains the role.</summary>
    /// <param name="roleId">The role id.</param>
    /// <returns>The matching packs, ordered by id.</returns>
    public IReadOnlyList<SkillPack> ForRole(string roleId)
    {
        return this.snapshot.Packs.Where(pack => pack.AppliesTo.Contains(roleId, StringComparer.Ordinal)).ToList();
    }

    /// <summary>Rereads the built-ins and the user folder, replacing the current packs and problems.</summary>
    public void Reload()
    {
        List<SkillPackProblem> problems = [];
        Dictionary<string, SkillPack> packs = new(StringComparer.Ordinal);

        foreach (KeyValuePair<string, string> builtin in this.builtins.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            SkillPack? pack = Parse(builtin.Value, "builtin:" + builtin.Key, problems);
            if (pack is not null)
            {
                packs[pack.Id] = pack;
            }
        }

        HashSet<string> userIds = new(StringComparer.Ordinal);
        foreach (string file in this.ListUserFiles())
        {
            string source = Path.GetFileName(file);
            string text;
            try
            {
                text = File.ReadAllText(file);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                problems.Add(new SkillPackProblem(source, "The file could not be read."));
                continue;
            }

            SkillPack? pack = Parse(text, source, problems);
            if (pack is null)
            {
                continue;
            }

            if (!userIds.Add(pack.Id))
            {
                problems.Add(new SkillPackProblem(source, $"Another user file already defines pack '{pack.Id}'."));
                continue;
            }

            packs[pack.Id] = pack;
        }

        foreach (SkillPackProblem problem in problems)
        {
            this.logger.LogWarning("Skill pack {Source} was not loaded: {Message}", problem.Source, problem.Message);
        }

        this.snapshot = new Snapshot(packs.Values.OrderBy(pack => pack.Id, StringComparer.Ordinal).ToList(), problems);
    }

    /// <summary>Reads every embedded built-in pack file, keyed by file name.</summary>
    /// <returns>File name to JSON text; empty when the assembly embeds none.</returns>
    private static Dictionary<string, string> ReadEmbedded()
    {
        Assembly assembly = typeof(SkillPackStore).Assembly;
        Dictionary<string, string> files = new(StringComparer.Ordinal);
        foreach (string resourceName in assembly.GetManifestResourceNames())
        {
            if (!resourceName.StartsWith(ResourcePrefix, StringComparison.Ordinal))
            {
                continue;
            }

            using Stream? stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is null)
            {
                continue;
            }

            using StreamReader reader = new(stream);
            files[resourceName[ResourcePrefix.Length..]] = reader.ReadToEnd();
        }

        return files;
    }

    /// <summary>Parses and validates one pack file, appending a problem when it is rejected.</summary>
    /// <param name="text">The file's JSON text.</param>
    /// <param name="source">The source label used in a problem.</param>
    /// <param name="problems">Receives the problem when the file is rejected.</param>
    /// <returns>The pack, or null when the file was rejected.</returns>
    private static SkillPack? Parse(string text, string source, List<SkillPackProblem> problems)
    {
        PackDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<PackDto>(text, TeamTypeJson.Options);
        }
        catch (JsonException)
        {
            problems.Add(new SkillPackProblem(source, "The file is not valid pack JSON."));
            return null;
        }

        if (dto is null)
        {
            problems.Add(new SkillPackProblem(source, "The file is empty."));
            return null;
        }

        if (string.IsNullOrWhiteSpace(dto.Id))
        {
            problems.Add(new SkillPackProblem(source, "The 'id' field is required."));
            return null;
        }

        if (string.IsNullOrWhiteSpace(dto.Version))
        {
            problems.Add(new SkillPackProblem(source, "The 'version' field is required."));
            return null;
        }

        if (string.IsNullOrWhiteSpace(dto.Title))
        {
            problems.Add(new SkillPackProblem(source, "The 'title' field is required."));
            return null;
        }

        List<SkillRef> skills = [];
        foreach (SkillDto skill in dto.Skills ?? [])
        {
            if (string.IsNullOrWhiteSpace(skill.Name) || string.IsNullOrWhiteSpace(skill.Source))
            {
                problems.Add(new SkillPackProblem(source, "Every skill needs a 'name' and a 'source'."));
                return null;
            }

            skills.Add(new SkillRef(skill.Name, skill.Source, skill.Path));
        }

        return new SkillPack(dto.Id, dto.Version, dto.Title, dto.AppliesTo ?? [], dto.ConsultWhenAdds ?? string.Empty, skills);
    }

    /// <summary>Lists the user's pack files in a stable order.</summary>
    /// <returns>The file paths; empty when the folder does not exist.</returns>
    private List<string> ListUserFiles()
    {
        if (!Directory.Exists(this.userDir))
        {
            return [];
        }

        return Directory.EnumerateFiles(this.userDir, "*.json", SearchOption.TopDirectoryOnly)
            .OrderBy(file => file, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>One consistent view of the loaded packs and problems.</summary>
    /// <param name="Packs">The packs.</param>
    /// <param name="Problems">The problems.</param>
    private sealed record Snapshot(IReadOnlyList<SkillPack> Packs, IReadOnlyList<SkillPackProblem> Problems);

    /// <summary>The JSON shape of a pack file, every field nullable so a gap is a problem and not an exception.</summary>
    /// <param name="Id">The id.</param>
    /// <param name="Version">The version.</param>
    /// <param name="Title">The title.</param>
    /// <param name="AppliesTo">The role ids.</param>
    /// <param name="ConsultWhenAdds">The consult hint.</param>
    /// <param name="Skills">The skills.</param>
    private sealed record PackDto(string? Id, string? Version, string? Title, List<string>? AppliesTo, string? ConsultWhenAdds, List<SkillDto>? Skills);

    /// <summary>The JSON shape of a skill reference.</summary>
    /// <param name="Name">The name.</param>
    /// <param name="Source">The source.</param>
    /// <param name="Path">The path.</param>
    private sealed record SkillDto(string? Name, string? Source, string? Path);
}
