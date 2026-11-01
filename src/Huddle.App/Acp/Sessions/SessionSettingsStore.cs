using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Watching;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Acp.Sessions;

/// <summary>
/// Holds the overrides-only, hand-editable, watched <c>{DataDir}/session-settings.json</c> (Spec 5.2):
/// an absent file is normal and is never created by a read; it appears on the first save and is
/// deleted when the last override is reset. A corrupt file falls back to the configured defaults
/// with <see cref="LoadError"/> set; an out-of-range hand-edited value is ignored for that key alone.
/// </summary>
internal sealed class SessionSettingsStore : ISessionSettings, IDisposable
{
    private const string MessagesKey = "transcriptCatchUpMessages";
    private const string TokensKey = "transcriptCatchUpTokens";
    private const string FileName = "session-settings.json";

    // Derives from ProtocolJson.Options as AppearanceStore does, but with a relaxed encoder: the file is hand-edited.
    private static readonly JsonSerializerOptions IndentedJsonOptions = new(ProtocolJson.Options)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly string path;
    private readonly int defaultMessages;
    private readonly int defaultTokens;
    private readonly ILogger<SessionSettingsStore> logger;
    private readonly WatchedFileStore<State> watched;

    /// <summary>Loads (or defaults) the file at <c>{DataDir}/session-settings.json</c> and starts watching it.</summary>
    /// <param name="options">Supplies <see cref="TeamOptions.DataDir"/> and the configured defaults in <see cref="TeamOptions.Acp"/>.</param>
    /// <param name="logger">Used to warn about an unreadable file.</param>
    public SessionSettingsStore(IOptions<TeamOptions> options, ILogger<SessionSettingsStore> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        this.logger = logger;
        this.path = Path.Combine(options.Value.DataDir, FileName);
        this.defaultMessages = options.Value.Acp.TranscriptCatchUpMessages;
        this.defaultTokens = options.Value.Acp.TranscriptCatchUpTokens;

        string? directory = Path.GetDirectoryName(this.path);
        if (string.IsNullOrEmpty(directory))
        {
            throw new InvalidOperationException($"'{this.path}' has no parent directory to watch.");
        }

        Directory.CreateDirectory(directory);

        this.watched = new WatchedFileStore<State>(
            directory,
            FileName,
            this.LoadAtConstruction(),
            _ => this.ReloadForWatcher(),
            (_, _) => this.Changed,
            this.logger,
            nameof(SessionSettingsStore));
    }

    /// <summary>Raised after a save and after a hand edit is picked up; outside the write lock, so a subscriber cannot deadlock against a write.</summary>
    public event Action? Changed;

    /// <summary>The absolute path of the settings file, whether or not it exists.</summary>
    public string FilePath => this.path;

    /// <inheritdoc />
    public SessionSettings Current => this.watched.Current.Settings;

    /// <summary>Why the file could not be used in full (unreadable, or a key ignored), or <see langword="null"/> when it was read cleanly.</summary>
    public string? LoadError => this.watched.Current.Error;

    /// <summary>The Message cap the user has saved, or <see langword="null"/> when none is in force.</summary>
    public int? MessagesOverride => this.watched.Current.Messages;

    /// <summary>The token budget the user has saved, or <see langword="null"/> when none is in force.</summary>
    public int? TokensOverride => this.watched.Current.Tokens;

    /// <summary>Saves the Message cap, or resets it when <paramref name="value"/> is <see langword="null"/>.</summary>
    /// <param name="value">The new cap, or <see langword="null"/> to remove the override.</param>
    /// <returns>The refusal text when the value is out of range (nothing is written); otherwise <see langword="null"/>.</returns>
    public string? SaveMessages(int? value) =>
        this.Save(MessagesKey, value, SessionSettingsRules.CheckMessages);

    /// <summary>Saves the token budget, or resets it when <paramref name="value"/> is <see langword="null"/>.</summary>
    /// <param name="value">The new budget, or <see langword="null"/> to remove the override.</param>
    /// <returns>The refusal text when the value is out of range (nothing is written); otherwise <see langword="null"/>.</returns>
    public string? SaveTokens(int? value) =>
        this.Save(TokensKey, value, SessionSettingsRules.CheckTokens);

    /// <summary>Stops watching the file.</summary>
    public void Dispose()
    {
        this.watched.Dispose();
    }

    private static JsonObject ParseDocumentFile(string path)
    {
        string json = File.ReadAllText(path);
        JsonNode? node = JsonNode.Parse(json);
        return node as JsonObject ?? throw new JsonException($"Expected a JSON object at the top level of '{path}'.");
    }

    private static int? ReadValue(JsonObject document, string key, Func<int, string?> check, List<string> problems)
    {
        if (!document.TryGetPropertyValue(key, out JsonNode? node) || node is null)
        {
            return null;
        }

        if (node is JsonValue jsonValue && jsonValue.TryGetValue(out int value))
        {
            string? refusal = check(value);
            if (refusal is null)
            {
                return value;
            }

            problems.Add($"Ignored '{key}': {refusal}");
            return null;
        }

        problems.Add($"Ignored '{key}': the value is not a whole number.");
        return null;
    }

    private string? Save(string key, int? value, Func<int, string?> check)
    {
        if (value is int candidate && check(candidate) is { } refusal)
        {
            return refusal;
        }

        Action? changed;
        lock (this.watched.Gate)
        {
            ObjectDisposedException.ThrowIf(this.watched.IsDisposed, this);

            JsonObject document = this.ReadDocumentTolerantly();
            if (value is int accepted)
            {
                document[key] = accepted;
            }
            else
            {
                document.Remove(key);
            }

            this.WriteOrDelete(document);
            this.watched.Current = this.BuildState(document, null);
            changed = this.Changed;
        }

        changed?.Invoke();
        return null;
    }

    private JsonObject ReadDocumentTolerantly()
    {
        if (!File.Exists(this.path))
        {
            return new JsonObject();
        }

        try
        {
            return ParseDocumentFile(this.path);
        }
        catch (JsonException)
        {
            // A corrupt file is replaced wholesale by the first successful save; LoadError already told the user.
            return new JsonObject();
        }
    }

    private void WriteOrDelete(JsonObject document)
    {
        if (document.Count == 0)
        {
            File.Delete(this.path);
            return;
        }

        AtomicFile.WriteText(this.path, document.ToJsonString(IndentedJsonOptions));
    }

    private State BuildState(JsonObject document, string? baseError)
    {
        List<string> problems = [];
        int? messages = ReadValue(document, MessagesKey, SessionSettingsRules.CheckMessages, problems);
        int? tokens = ReadValue(document, TokensKey, SessionSettingsRules.CheckTokens, problems);
        string? error = baseError ?? (problems.Count == 0 ? null : string.Join(' ', problems));

        return new State(new SessionSettings(messages ?? this.defaultMessages, tokens ?? this.defaultTokens), messages, tokens, error);
    }

    private State CorruptState(Exception ex)
    {
        string error = ex.Message;
        this.logger.LogWarning(ex, "Could not parse session settings file '{Path}'; falling back to the configured defaults.", this.path);
        return new State(new SessionSettings(this.defaultMessages, this.defaultTokens), null, null, error);
    }

    private State LoadAtConstruction()
    {
        if (!File.Exists(this.path))
        {
            return this.BuildState(new JsonObject(), null);
        }

        try
        {
            return this.BuildState(ParseDocumentFile(this.path), null);
        }
        catch (JsonException ex)
        {
            return this.CorruptState(ex);
        }
    }

    private State? ReloadForWatcher()
    {
        if (!File.Exists(this.path))
        {
            return this.BuildState(new JsonObject(), null);
        }

        return RetryingFileRead.Run(
            () => this.BuildState(ParseDocumentFile(this.path), null),
            ex =>
            {
                if (ex is JsonException)
                {
                    return this.CorruptState(ex);
                }

                this.logger.LogWarning(ex, "Could not read session settings file '{Path}'; keeping the previously resolved settings.", this.path);
                return null;
            });
    }

    private sealed record State(SessionSettings Settings, int? Messages, int? Tokens, string? Error);
}
