using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Acp;

/// <summary>
/// Provides paths for agent definitions and work directories, under the ADR-0031 layout where
/// each agent has its own folder ({DataDir}/{AgentsDir}/{name}) holding its definition file
/// ({name}.md) and its Work Dir as a sub-folder ({Acp:WorkDir}).
/// </summary>
internal sealed class AgentPaths(IOptions<TeamOptions> options)
{
    /// <summary>
    /// The root directory containing agent folders, derived from {DataDir}/{Acp:AgentsDir}.
    /// </summary>
    public string DefinitionsRoot => Path.Combine(this.options.Value.DataDir, this.options.Value.Acp.AgentsDir);

    /// <summary>
    /// The configured data directory an agent's folder is nested under.
    /// </summary>
    public string DataDir => this.options.Value.DataDir;

    /// <summary>
    /// The folder holding every Agent's browser profile, <c>{DataDir}/browser-profiles</c>. Outside the Agent folders on purpose, so a scan of
    /// a Persona's folder never walks a Chromium profile.
    /// </summary>
    public string BrowserProfilesRoot => Path.Combine(this.options.Value.DataDir, "browser-profiles");

    private readonly IOptions<TeamOptions> options = options;

    /// <summary>Returns the path to an agent's browser profile folder.</summary>
    /// <param name="name">The agent name (not null or whitespace).</param>
    /// <returns>The full path to {BrowserProfilesRoot}/{name}.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="name"/> is null, empty or whitespace.</exception>
    public string BrowserProfile(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return Path.Combine(this.BrowserProfilesRoot, name);
    }

    /// <summary>
    /// Returns the path to an agent's own folder.
    /// </summary>
    /// <param name="name">The agent name (not null or whitespace).</param>
    /// <returns>The full path to {DefinitionsRoot}/{name}.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="name"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="name"/> is empty or whitespace.</exception>
    public string AgentFolder(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return Path.Combine(this.DefinitionsRoot, name);
    }

    /// <summary>
    /// Returns the path to an agent's definition file.
    /// </summary>
    /// <param name="name">The agent name (not null or whitespace).</param>
    /// <returns>The full path to {AgentFolder(name)}/{name}.md.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="name"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="name"/> is empty or whitespace.</exception>
    public string DefinitionFile(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return Path.Combine(this.AgentFolder(name), $"{name}.md");
    }

    /// <summary>
    /// Returns the path to an agent's work directory.
    /// </summary>
    /// <param name="name">The agent name (not null or whitespace).</param>
    /// <returns>The full path to {AgentFolder(name)}/{Acp:WorkDir}.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="name"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="name"/> is empty or whitespace.</exception>
    public string WorkDir(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return Path.Combine(this.AgentFolder(name), this.options.Value.Acp.WorkDir);
    }
}
