using Agency.Huddle.App.Library;
using Agency.Huddle.App.TeamTypes;
using Microsoft.Extensions.Options;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Agency.Huddle.App.HiringPool;

/// <summary>
/// The Human's own roles, stored in <c>custom-roles.json</c> under the data folder. Adding, editing and deleting
/// go through here; a file that cannot be read is reported in <see cref="LoadError"/> and every write is refused,
/// so a broken file is never replaced by a smaller one. Writes are atomic (temporary file, then move).
/// </summary>
internal sealed class CustomRoleStore
{
    private const string FileName = "custom-roles.json";
    private const string IdPrefix = "custom-";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly string filePath;
    private readonly Lock gate = new();
    private IReadOnlyList<CustomRole> roles = [];
    private string? loadError;

    /// <summary>Reads the file, if there is one.</summary>
    /// <param name="options">Where the data folder is.</param>
    public CustomRoleStore(IOptions<TeamOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.filePath = Path.Combine(options.Value.DataDir, FileName);
        this.Load();
    }

    /// <summary>Raised after a role is added, changed or deleted.</summary>
    public event Action? Changed;

    /// <summary>The roles, in the order they were added.</summary>
    public IReadOnlyList<CustomRole> Roles => this.roles;

    /// <summary>Why the file could not be read, or <see langword="null"/>; while set, every write is refused.</summary>
    public string? LoadError => this.loadError;

    /// <summary>Finds a role by id.</summary>
    /// <param name="id">The role id.</param>
    /// <returns>The role, or <see langword="null"/>.</returns>
    public CustomRole? Find(string id) => this.roles.FirstOrDefault(role => string.Equals(role.Id, id, StringComparison.Ordinal));

    /// <summary>Adds a role, giving it an id that no other role has.</summary>
    /// <param name="title">The title.</param>
    /// <param name="description">The description.</param>
    /// <param name="instructions">The instructions.</param>
    /// <param name="tier">The model strength.</param>
    /// <param name="skills">The names of the Skills an Agent in the role carries, or <see langword="null"/> for none.</param>
    /// <returns>The role as stored.</returns>
    /// <exception cref="InvalidOperationException">The file is unreadable, or the title is blank.</exception>
    public CustomRole Add(string title, string description, string instructions, ModelTier tier, IReadOnlyList<string>? skills = null)
    {
        ArgumentNullException.ThrowIfNull(title);
        CustomRole role;
        lock (this.gate)
        {
            this.ThrowIfBroken();
            string id = IdPrefix + LibraryRootStore.Slug(title, [.. this.roles.Select(static existing => existing.Id[IdPrefix.Length..])]);
            role = Clean(new CustomRole(id, title, description, instructions, tier, skills));
            this.Write([.. this.roles, role]);
        }

        this.Changed?.Invoke();
        return role;
    }

    /// <summary>Replaces the role with the same id; the id never changes.</summary>
    /// <param name="role">The edited role.</param>
    /// <returns><see langword="false"/> when no role has that id.</returns>
    /// <exception cref="InvalidOperationException">The file is unreadable, or the title is blank.</exception>
    public bool Update(CustomRole role)
    {
        ArgumentNullException.ThrowIfNull(role);
        lock (this.gate)
        {
            this.ThrowIfBroken();
            if (this.Find(role.Id) is null)
            {
                return false;
            }

            CustomRole clean = Clean(role);
            this.Write([.. this.roles.Select(existing => string.Equals(existing.Id, clean.Id, StringComparison.Ordinal) ? clean : existing)]);
        }

        this.Changed?.Invoke();
        return true;
    }

    /// <summary>Deletes a role.</summary>
    /// <param name="id">The role id.</param>
    /// <returns><see langword="false"/> when no role has that id.</returns>
    /// <exception cref="InvalidOperationException">The file is unreadable.</exception>
    public bool Delete(string id)
    {
        lock (this.gate)
        {
            this.ThrowIfBroken();
            if (this.Find(id) is null)
            {
                return false;
            }

            this.Write([.. this.roles.Where(existing => !string.Equals(existing.Id, id, StringComparison.Ordinal))]);
        }

        this.Changed?.Invoke();
        return true;
    }

    private static CustomRole Clean(CustomRole role)
    {
        string title = role.Title.Trim();
        if (title.Length == 0)
        {
            throw new InvalidOperationException("A role needs a title.");
        }

        return role with { Title = title, Description = role.Description.Trim(), Instructions = role.Instructions.Trim(), Skills = [.. role.Skills.Distinct(StringComparer.Ordinal)] };
    }

    private void ThrowIfBroken()
    {
        if (this.loadError is not null)
        {
            throw new InvalidOperationException($"'{this.filePath}' could not be read; fix it by hand before changing roles.");
        }
    }

    private void Write(IReadOnlyList<CustomRole> next)
    {
        _ = Directory.CreateDirectory(Path.GetDirectoryName(this.filePath) ?? ".");
        AtomicFile.WriteText(this.filePath, JsonSerializer.Serialize(new RolesFile(next), JsonOptions));
        this.roles = next;
    }

    private void Load()
    {
        if (!File.Exists(this.filePath))
        {
            return;
        }

        try
        {
            RolesFile? file = JsonSerializer.Deserialize<RolesFile>(File.ReadAllText(this.filePath), JsonOptions);
            this.roles = [.. (file?.Roles ?? []).Where(static role => !string.IsNullOrWhiteSpace(role.Id) && !string.IsNullOrWhiteSpace(role.Title))];
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            this.loadError = ex.Message;
        }
    }

    /// <summary>The shape of <c>custom-roles.json</c>.</summary>
    /// <param name="Roles">The roles.</param>
    private sealed record RolesFile(IReadOnlyList<CustomRole> Roles);
}
