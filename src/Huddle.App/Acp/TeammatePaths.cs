using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Acp;

/// <summary>
/// Provides paths for teammate definitions and work directories, under the ADR-0031 layout where
/// each teammate has its own folder ({DataDir}/{TeammatesDir}/{name}) holding its definition file
/// ({name}.md) and its Work Dir as a sub-folder ({Acp:WorkDir}).
/// </summary>
internal sealed class TeammatePaths(IOptions<TeamOptions> options)
{
    /// <summary>
    /// The root directory containing teammate folders, derived from {DataDir}/{Acp:TeammatesDir}.
    /// </summary>
    public string DefinitionsRoot => Path.Combine(this.options.Value.DataDir, this.options.Value.Acp.TeammatesDir);

    /// <summary>
    /// The configured data directory a teammate's folder is nested under.
    /// </summary>
    public string DataDir => this.options.Value.DataDir;

    private readonly IOptions<TeamOptions> options = options;

    /// <summary>
    /// Returns the path to a teammate's own folder.
    /// </summary>
    /// <param name="name">The teammate name (not null or whitespace).</param>
    /// <returns>The full path to {DefinitionsRoot}/{name}.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="name"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="name"/> is empty or whitespace.</exception>
    public string TeammateFolder(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return Path.Combine(this.DefinitionsRoot, name);
    }

    /// <summary>
    /// Returns the path to a teammate's definition file.
    /// </summary>
    /// <param name="name">The teammate name (not null or whitespace).</param>
    /// <returns>The full path to {TeammateFolder(name)}/{name}.md.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="name"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="name"/> is empty or whitespace.</exception>
    public string DefinitionFile(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return Path.Combine(this.TeammateFolder(name), $"{name}.md");
    }

    /// <summary>
    /// Returns the path to a teammate's work directory.
    /// </summary>
    /// <param name="name">The teammate name (not null or whitespace).</param>
    /// <returns>The full path to {TeammateFolder(name)}/{Acp:WorkDir}.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="name"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="name"/> is empty or whitespace.</exception>
    public string WorkDir(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return Path.Combine(this.TeammateFolder(name), this.options.Value.Acp.WorkDir);
    }
}
