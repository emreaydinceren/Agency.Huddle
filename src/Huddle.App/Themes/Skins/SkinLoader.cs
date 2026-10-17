using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using MudBlazor;

namespace Agency.Huddle.App.Themes.Skins;

/// <summary>
/// Reads the skin files embedded under <c>Themes/Skins/</c>, merges each file's <c>include</c> chain, validates the result
/// and builds the <see cref="MudTheme"/> for each skin (architecture section 5). No DI: the skins are fixed data, so
/// <see cref="LoadAll()"/> reads them once and hands out the same list.
/// </summary>
internal static partial class SkinLoader
{
    /// <summary>The first MudBlazor elevation index a skin may set.</summary>
    public const int FirstElevation = 1;

    /// <summary>The last MudBlazor elevation index a skin may set.</summary>
    public const int LastElevation = 25;

    private const string ResourcePrefix = "Themes/Skins/";
    private const string SchemaFileName = "skin.schema.json";

    private static readonly string[] FaceNames = ["ui", "read", "mono"];

    private static readonly (string Name, Type EnumType)[] TraitTypes =
    [
        ("message", typeof(MessageLayout)),
        ("send", typeof(SendStyle)),
        ("status", typeof(StatusStyle)),
        ("labels", typeof(LabelCase)),
        ("composer", typeof(ComposerStyle)),
        ("nav", typeof(NavStyle)),
        ("surface", typeof(SurfaceStyle)),
        ("marks", typeof(MarkStyle)),
        ("reading", typeof(ReadingStyle)),
    ];

    private static readonly IReadOnlyDictionary<string, PropertyInfo> PaletteProperties = typeof(Palette)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(static property => property.SetMethod is { IsPublic: true }
            && (property.PropertyType == typeof(MudBlazor.Utilities.MudColor) || property.PropertyType == typeof(string)))
        .ToDictionary(static property => char.ToLowerInvariant(property.Name[0]) + property.Name[1..], StringComparer.Ordinal);

    private static readonly Lazy<IReadOnlyList<Skin>> Loaded = new(static () => LoadAll(ReadEmbedded()));

    /// <summary>Every palette key a skin may set: camelCase of a settable colour property of <see cref="Palette"/>, sorted.</summary>
    public static IReadOnlyList<string> PaletteKeys { get; } =
        [.. PaletteProperties.Keys.OrderBy(static key => key, StringComparer.Ordinal)];

    /// <summary>Every trait a skin may set, with the kebab-case values it accepts, in the order of <see cref="SkinTraits"/>.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> TraitValues { get; } =
        TraitTypes.ToDictionary(
            static trait => trait.Name,
            static trait => (IReadOnlyList<string>)[.. Enum.GetNames(trait.EnumType).Select(Kebab)],
            StringComparer.Ordinal);

    /// <summary>Loads the skins embedded in this assembly, ordered by <see cref="Skin.Order"/>. Read once, then cached.</summary>
    /// <returns>The same list on every call.</returns>
    public static IReadOnlyList<Skin> LoadAll() => Loaded.Value;

    /// <summary>Loads skins from in-memory files, for tests.</summary>
    /// <param name="files">File name (such as <c>lumen.light.json</c>) to its JSON text.</param>
    /// <returns>The skins, ordered by <see cref="Skin.Order"/>.</returns>
    /// <exception cref="SkinLoadException">A file is invalid; the message names the file and key.</exception>
    public static IReadOnlyList<Skin> LoadAll(IReadOnlyDictionary<string, string> files)
    {
        ArgumentNullException.ThrowIfNull(files);

        Dictionary<string, Draft> parsed = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> file in files)
        {
            parsed[file.Key] = Parse(file.Key, file.Value);
        }

        List<Skin> skins = [];
        foreach (string name in parsed.Keys.Order(StringComparer.Ordinal))
        {
            skins.Add(Build(Resolve(name, parsed, [])));
        }

        IGrouping<string, Skin>? duplicate = skins.GroupBy(static skin => skin.Id, StringComparer.Ordinal).FirstOrDefault(static group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new SkinLoadException($"Skin id '{duplicate.Key}' is used by more than one file.");
        }

        return [.. skins.OrderBy(static skin => skin.Order)];
    }

    private static Dictionary<string, string> ReadEmbedded()
    {
        Assembly assembly = typeof(SkinLoader).Assembly;
        Dictionary<string, string> files = new(StringComparer.Ordinal);
        foreach (string resource in assembly.GetManifestResourceNames())
        {
            if (!resource.StartsWith(ResourcePrefix, StringComparison.Ordinal) || !resource.EndsWith(".json", StringComparison.Ordinal))
            {
                continue;
            }

            string name = resource[ResourcePrefix.Length..];
            if (string.Equals(name, SchemaFileName, StringComparison.Ordinal))
            {
                continue;
            }

            using Stream stream = assembly.GetManifestResourceStream(resource)
                ?? throw new SkinLoadException($"{name}: the embedded resource could not be opened.");
            using StreamReader reader = new(stream);
            files[name] = reader.ReadToEnd();
        }

        return files;
    }

    private static Draft Resolve(string file, Dictionary<string, Draft> parsed, List<string> chain)
    {
        if (chain.Contains(file, StringComparer.Ordinal))
        {
            throw Fail(chain[0], $"include cycle {string.Join(" -> ", chain.Append(file))}.");
        }

        Draft self = parsed[file];
        if (self.Include is null)
        {
            return self;
        }

        if (!parsed.ContainsKey(self.Include))
        {
            throw Fail(file, $"include '{self.Include}' does not exist.");
        }

        chain.Add(file);
        Draft inherited = Resolve(self.Include, parsed, chain);
        chain.RemoveAt(chain.Count - 1);
        return Merge(inherited, self);
    }

    private static Draft Merge(Draft inherited, Draft self)
    {
        Draft merged = new()
        {
            File = self.File,
            Id = self.Id,
            Label = self.Label,
            Mode = self.Mode,
            Order = self.Order,
            Include = self.Include,
            Radius = self.Radius ?? inherited.Radius,
            Pack = self.Pack ?? inherited.Pack,
            LetterSpacing = self.LetterSpacing ?? inherited.LetterSpacing,
            LineHeight = self.LineHeight ?? inherited.LineHeight,
        };

        foreach (string face in FaceNames)
        {
            if (self.Faces.TryGetValue(face, out string[]? own))
            {
                merged.Faces[face] = own;
            }
            else if (inherited.Faces.TryGetValue(face, out string[]? fallback))
            {
                merged.Faces[face] = fallback;
            }
        }

        Overlay(merged.Shadows, inherited.Shadows, self.Shadows);
        Overlay(merged.Tokens, inherited.Tokens, self.Tokens);
        Overlay(merged.Traits, inherited.Traits, self.Traits);

        // A dark file must not silently pick up light colours, so a palette is inherited only within one mode.
        Overlay(merged.Palette, inherited.Mode == self.Mode ? inherited.Palette : [], self.Palette);
        return merged;
    }

    private static void Overlay<TKey, TValue>(Dictionary<TKey, TValue> target, Dictionary<TKey, TValue> inherited, Dictionary<TKey, TValue> own)
        where TKey : notnull
    {
        foreach (KeyValuePair<TKey, TValue> pair in inherited)
        {
            target[pair.Key] = pair.Value;
        }

        foreach (KeyValuePair<TKey, TValue> pair in own)
        {
            target[pair.Key] = pair.Value;
        }
    }

    private static Draft Parse(string file, string json)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException exception)
        {
            throw Fail(file, $"not valid JSON: {exception.Message}");
        }

        using (document)
        {
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw Fail(file, "the top level must be an object.");
            }

            Draft draft = new() { File = file, Id = string.Empty, Label = string.Empty, Mode = ThemeMode.Light };
            bool hasId = false, hasLabel = false, hasMode = false, hasOrder = false;
            foreach (JsonProperty property in root.EnumerateObject())
            {
                switch (property.Name)
                {
                    case "$schema":
                        break;
                    case "id":
                        draft.Id = ReadString(file, "id", property.Value);
                        hasId = true;
                        break;
                    case "label":
                        draft.Label = ReadString(file, "label", property.Value);
                        hasLabel = true;
                        break;
                    case "mode":
                        draft.Mode = ReadMode(file, property.Value);
                        hasMode = true;
                        break;
                    case "order":
                        if (property.Value.ValueKind != JsonValueKind.Number || !property.Value.TryGetInt32(out int order))
                        {
                            throw Fail(file, "'order' must be an integer.");
                        }

                        draft.Order = order;
                        hasOrder = true;
                        break;
                    case "include":
                        draft.Include = ReadString(file, "include", property.Value);
                        break;
                    case "radius":
                        draft.Radius = ReadString(file, "radius", property.Value);
                        break;
                    case "pack":
                        draft.Pack = ReadString(file, "pack", property.Value);
                        break;
                    case "faces":
                        ParseFaces(file, property.Value, draft);
                        break;
                    case "typography":
                        ParseTypography(file, property.Value, draft);
                        break;
                    case "shadows":
                        ParseShadows(file, property.Value, draft);
                        break;
                    case "palette":
                        ParsePalette(file, property.Value, draft);
                        break;
                    case "tokens":
                        ParseTokens(file, property.Value, draft);
                        break;
                    case "traits":
                        ParseTraits(file, property.Value, draft);
                        break;
                    default:
                        throw Fail(file, $"unknown key '{property.Name}'.");
                }
            }

            if (!(hasId && hasLabel && hasMode && hasOrder))
            {
                throw Fail(file, "'id', 'label', 'mode' and 'order' are required.");
            }

            return draft;
        }
    }

    private static void ParseFaces(string file, JsonElement element, Draft draft)
    {
        foreach (JsonProperty face in ReadObject(file, "faces", element))
        {
            if (!FaceNames.Contains(face.Name, StringComparer.Ordinal))
            {
                throw Fail(file, $"faces.{face.Name}: unknown face; expected ui, read or mono.");
            }

            if (face.Value.ValueKind != JsonValueKind.Array)
            {
                throw Fail(file, $"faces.{face.Name} must be an array of family names.");
            }

            List<string> families = [];
            foreach (JsonElement family in face.Value.EnumerateArray())
            {
                string name = ReadString(file, $"faces.{face.Name}", family);
                if (!FaceNamePattern().IsMatch(name))
                {
                    throw Fail(file, $"faces.{face.Name}: '{name}' must match ^[A-Za-z0-9 -]+$.");
                }

                families.Add(name);
            }

            if (families.Count > 0)
            {
                draft.Faces[face.Name] = [.. families];
            }
        }
    }

    private static void ParseTypography(string file, JsonElement element, Draft draft)
    {
        foreach (JsonProperty entry in ReadObject(file, "typography", element))
        {
            string value = ReadString(file, $"typography.{entry.Name}", entry.Value);
            switch (entry.Name)
            {
                case "letterSpacing":
                    draft.LetterSpacing = value;
                    break;
                case "lineHeight":
                    draft.LineHeight = value;
                    break;
                default:
                    throw Fail(file, $"typography.{entry.Name}: unknown key; expected letterSpacing or lineHeight.");
            }
        }
    }

    private static void ParseShadows(string file, JsonElement element, Draft draft)
    {
        foreach (JsonProperty entry in ReadObject(file, "shadows", element))
        {
            Match range = ShadowKeyPattern().Match(entry.Name);
            if (!range.Success)
            {
                throw Fail(file, $"shadows.{entry.Name}: the key must be \"N\" or \"N-M\".");
            }

            int first = int.Parse(range.Groups["first"].Value, CultureInfo.InvariantCulture);
            int last = range.Groups["last"].Success ? int.Parse(range.Groups["last"].Value, CultureInfo.InvariantCulture) : first;
            if (first < FirstElevation || first > last || last > LastElevation)
            {
                throw Fail(file, $"shadows.{entry.Name}: indexes must satisfy {FirstElevation} <= N <= M <= {LastElevation}.");
            }

            string value = ReadString(file, $"shadows.{entry.Name}", entry.Value);
            if (value.Length == 0 || value.Contains("palette.", StringComparison.Ordinal))
            {
                throw Fail(file, $"shadows.{entry.Name}: the value must be a literal shadow with no palette terms.");
            }

            for (int index = first; index <= last; index++)
            {
                if (!draft.Shadows.TryAdd(index, value))
                {
                    throw Fail(file, $"shadows.{entry.Name}: elevation {index} is already set by another key.");
                }
            }
        }
    }

    private static void ParsePalette(string file, JsonElement element, Draft draft)
    {
        foreach (JsonProperty entry in ReadObject(file, "palette", element))
        {
            if (!PaletteProperties.ContainsKey(entry.Name))
            {
                throw Fail(file, $"palette.{entry.Name}: unknown palette key.");
            }

            string value = ReadString(file, $"palette.{entry.Name}", entry.Value);
            if (!ColourPattern().IsMatch(value))
            {
                throw Fail(file, $"palette.{entry.Name}: '{value}' is not a hex or rgb()/rgba() colour.");
            }

            draft.Palette[entry.Name] = value;
        }
    }

    private static void ParseTokens(string file, JsonElement element, Draft draft)
    {
        foreach (JsonProperty entry in ReadObject(file, "tokens", element))
        {
            if (!SkinTokens.Contains(entry.Name))
            {
                throw Fail(file, $"tokens.{entry.Name}: unknown token key.");
            }

            string value = ReadString(file, $"tokens.{entry.Name}", entry.Value);
            if (value.Length == 0)
            {
                throw Fail(file, $"tokens.{entry.Name}: the expression is empty.");
            }

            draft.Tokens[entry.Name] = value;
        }
    }

    private static void ParseTraits(string file, JsonElement element, Draft draft)
    {
        foreach (JsonProperty entry in ReadObject(file, "traits", element))
        {
            (string Name, Type EnumType) trait = TraitTypes.FirstOrDefault(candidate => string.Equals(candidate.Name, entry.Name, StringComparison.Ordinal));
            if (trait.EnumType is null)
            {
                throw Fail(file, $"traits.{entry.Name}: unknown trait.");
            }

            string value = ReadString(file, $"traits.{entry.Name}", entry.Value);
            string? match = Enum.GetNames(trait.EnumType).FirstOrDefault(name =>
                string.Equals(name, value.Replace("-", string.Empty, StringComparison.Ordinal), StringComparison.OrdinalIgnoreCase));
            if (match is null)
            {
                throw Fail(file, $"traits.{entry.Name}: '{value}' is not a value of this trait; expected one of {string.Join(", ", TraitValues[entry.Name])}.");
            }

            draft.Traits[entry.Name] = match;
        }
    }

    private static JsonElement.ObjectEnumerator ReadObject(string file, string key, JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw Fail(file, $"'{key}' must be an object.");
        }

        return element.EnumerateObject();
    }

    private static string ReadString(string file, string key, JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.String)
        {
            throw Fail(file, $"'{key}' must be a string.");
        }

        return element.GetString() ?? string.Empty;
    }

    private static ThemeMode ReadMode(string file, JsonElement element) =>
        ReadString(file, "mode", element) switch
        {
            "light" => ThemeMode.Light,
            "dark" => ThemeMode.Dark,
            _ => throw Fail(file, "'mode' must be \"light\" or \"dark\"."),
        };

    private static Skin Build(Draft draft)
    {
        SkinFaces faces = new(
            draft.Faces.GetValueOrDefault("ui") ?? [],
            draft.Faces.GetValueOrDefault("read") ?? [],
            draft.Faces.GetValueOrDefault("mono") ?? []);
        SkinTraits traits = new(
            Trait(draft, "message", MessageLayout.Bubble),
            Trait(draft, "send", SendStyle.Icon),
            Trait(draft, "status", StatusStyle.Chip),
            Trait(draft, "labels", LabelCase.Sentence),
            Trait(draft, "composer", ComposerStyle.Box),
            Trait(draft, "nav", NavStyle.Tint),
            Trait(draft, "surface", SurfaceStyle.Flat),
            Trait(draft, "marks", MarkStyle.Plain),
            Trait(draft, "reading", ReadingStyle.Plain));

        return new Skin(
            draft.Id,
            draft.Label,
            draft.Mode,
            draft.Order,
            draft.Radius,
            faces,
            new SkinTypography(draft.LetterSpacing, draft.LineHeight),
            new Dictionary<int, string>(draft.Shadows),
            new Dictionary<string, string>(draft.Palette, StringComparer.Ordinal),
            new Dictionary<string, string>(draft.Tokens, StringComparer.Ordinal),
            traits,
            draft.Pack,
            BuildTheme(draft));
    }

    private static T Trait<T>(Draft draft, string name, T fallback)
        where T : struct, Enum =>
        draft.Traits.TryGetValue(name, out string? value) ? Enum.Parse<T>(value) : fallback;

    private static MudTheme BuildTheme(Draft draft)
    {
        MudTheme theme;
        if (draft.Mode == ThemeMode.Light)
        {
            PaletteLight palette = new();
            ApplyPalette(palette, draft.Palette);
            theme = ThemeDefaults.CreateLight(palette);
        }
        else
        {
            PaletteDark palette = new();
            ApplyPalette(palette, draft.Palette);
            theme = ThemeDefaults.CreateDark(palette);
        }

        if (draft.Radius is not null)
        {
            theme.LayoutProperties.DefaultBorderRadius = draft.Radius;
        }

        if (draft.Faces.TryGetValue("ui", out string[]? ui))
        {
            theme.Typography.Default.FontFamily = ui;
        }

        if (draft.LetterSpacing is not null)
        {
            // Never Overline: its 0.04em tracking is pinned by TypeScaleTests.
            BaseTypography[] roles =
            [
                theme.Typography.Default, theme.Typography.Body1, theme.Typography.Body2, theme.Typography.Caption,
                theme.Typography.Subtitle1, theme.Typography.Subtitle2, theme.Typography.Button,
            ];
            foreach (BaseTypography role in roles)
            {
                role.LetterSpacing = draft.LetterSpacing;
            }
        }

        if (draft.LineHeight is not null)
        {
            theme.Typography.Default.LineHeight = draft.LineHeight;
        }

        foreach (KeyValuePair<int, string> shadow in draft.Shadows)
        {
            theme.Shadows.Elevation[shadow.Key] = shadow.Value;
        }

        return theme;
    }

    private static void ApplyPalette(Palette palette, Dictionary<string, string> values)
    {
        foreach (KeyValuePair<string, string> value in values)
        {
            PropertyInfo property = PaletteProperties[value.Key];
            if (property.PropertyType == typeof(string))
            {
                property.SetValue(palette, value.Value);
            }
            else
            {
                property.SetValue(palette, new MudBlazor.Utilities.MudColor(value.Value));
            }
        }
    }

    private static string Kebab(string name) =>
        string.Concat(name.Select(static (character, index) =>
            char.IsUpper(character) && index > 0 ? "-" + char.ToLowerInvariant(character) : char.ToLowerInvariant(character).ToString()));

    private static SkinLoadException Fail(string file, string message) => new($"{file}: {message}");

    [GeneratedRegex("^[A-Za-z0-9 -]+$", RegexOptions.CultureInvariant)]
    private static partial Regex FaceNamePattern();

    [GeneratedRegex(@"^(?<first>\d+)(?:-(?<last>\d+))?$", RegexOptions.CultureInvariant)]
    private static partial Regex ShadowKeyPattern();

    [GeneratedRegex(@"^(#[0-9a-fA-F]{3,8}|rgba?\([0-9 ,.%]+\))$", RegexOptions.CultureInvariant)]
    private static partial Regex ColourPattern();

    /// <summary>One file's own content, then (after <see cref="Merge"/>) its include chain merged in.</summary>
    private sealed class Draft
    {
        public required string File { get; init; }

        public required string Id { get; set; }

        public required string Label { get; set; }

        public required ThemeMode Mode { get; set; }

        public int Order { get; set; }

        public string? Include { get; set; }

        public string? Radius { get; set; }

        public string? Pack { get; set; }

        public string? LetterSpacing { get; set; }

        public string? LineHeight { get; set; }

        public Dictionary<string, string[]> Faces { get; } = new(StringComparer.Ordinal);

        public Dictionary<int, string> Shadows { get; } = [];

        public Dictionary<string, string> Palette { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, string> Tokens { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, string> Traits { get; } = new(StringComparer.Ordinal);
    }
}
