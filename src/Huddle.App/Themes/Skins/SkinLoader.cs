using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using MudBlazor;
using MudBlazor.Utilities;

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
        ("voice", typeof(VoiceStyle)),
        ("ids", typeof(IdStyle)),
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
    /// <exception cref="SkinException">A file is invalid; the message names the file and key.</exception>
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
            throw new SkinException($"Skin id '{duplicate.Key}' is used by more than one file.");
        }

        return [.. skins.OrderBy(static skin => skin.Order)];
    }

    /// <summary>
    /// Keeps the entries of <paramref name="requested"/> that a skin file may hold and drops the rest, reporting each dropped
    /// entry. The rules are the skin file's own: a palette key must be a settable colour of <see cref="Palette"/> and its value a
    /// hex or <c>rgb()</c>/<c>rgba()</c> colour; a token key must be in the registry and its expression must parse, reference
    /// only registry tokens, and not loop back on itself once merged over the skin's own tokens.
    /// </summary>
    /// <param name="skin">The skin the customization applies to.</param>
    /// <param name="requested">The Human's overrides, as read.</param>
    /// <param name="rejected">Called once for each dropped entry with a message naming it, or <see langword="null"/> to drop silently.</param>
    /// <returns>The valid subset; never <see langword="null"/>.</returns>
    public static SkinCustomization Sanitize(Skin skin, SkinCustomization requested, Action<string>? rejected = null)
    {
        ArgumentNullException.ThrowIfNull(skin);
        ArgumentNullException.ThrowIfNull(requested);

        Dictionary<string, string> palette = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> entry in requested.Palette)
        {
            string? problem = PaletteProblem(entry.Key, entry.Value);
            if (problem is null)
            {
                palette[entry.Key] = entry.Value;
            }
            else
            {
                rejected?.Invoke($"palette.{entry.Key}: {problem}");
            }
        }

        Dictionary<string, string> merged = [];
        foreach (Token token in SkinTokens.All)
        {
            merged[token.Key] = skin.Tokens.GetValueOrDefault(token.Key) ?? (skin.Mode == ThemeMode.Dark ? token.DarkDefault : token.LightDefault);
        }

        Dictionary<string, string> tokens = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> entry in requested.Tokens)
        {
            string? problem = TokenProblem(skin, merged, entry.Key, entry.Value);
            if (problem is null)
            {
                tokens[entry.Key] = entry.Value;
                merged[entry.Key] = entry.Value;
            }
            else
            {
                rejected?.Invoke($"tokens.{entry.Key}: {problem}");
            }
        }

        return new SkinCustomization(palette, tokens);
    }

    /// <summary>
    /// The skin with a Human's overrides merged over it: a new <see cref="Skin"/> whose palette, tokens and
    /// <see cref="MudTheme"/> carry them. Invalid entries are dropped (see <see cref="Sanitize"/>), never thrown on, because the
    /// caller is a render. A customized <c>primary</c> is clamped for contrast through <see cref="AccentPalette.ClampForContrast"/>,
    /// and its <c>primaryDarken</c>, <c>primaryContrastText</c> and <c>tertiary</c> follow it unless the customization sets them.
    /// The shared <paramref name="skin"/> is never mutated.
    /// </summary>
    /// <param name="skin">The skin to customize.</param>
    /// <param name="customization">The Human's overrides.</param>
    /// <returns><paramref name="skin"/> itself when nothing valid is overridden; otherwise a new skin.</returns>
    public static Skin Customize(Skin skin, SkinCustomization customization)
    {
        ArgumentNullException.ThrowIfNull(skin);
        ArgumentNullException.ThrowIfNull(customization);

        SkinCustomization clean = Sanitize(skin, customization);
        if (clean.IsEmpty)
        {
            return skin;
        }

        Dictionary<string, string> palette = new(skin.Palette, StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> entry in clean.Palette)
        {
            palette[entry.Key] = entry.Value;
        }

        Dictionary<string, string> tokens = new(skin.Tokens, StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> entry in clean.Tokens)
        {
            tokens[entry.Key] = entry.Value;
        }

        Draft draft = new()
        {
            File = skin.Id,
            Id = skin.Id,
            Label = skin.Label,
            Mode = skin.Mode,
            Order = skin.Order,
            Radius = skin.Radius,
            LetterSpacing = skin.Typography.LetterSpacing,
            LineHeight = skin.Typography.LineHeight,
        };
        AddFace(draft, "ui", skin.Faces.Ui);
        AddFace(draft, "read", skin.Faces.Read);
        AddFace(draft, "mono", skin.Faces.Mono);
        foreach (KeyValuePair<int, string> shadow in skin.Shadows)
        {
            draft.Shadows[shadow.Key] = shadow.Value;
        }

        foreach (KeyValuePair<string, string> entry in palette)
        {
            draft.Palette[entry.Key] = entry.Value;
        }

        MudTheme theme = BuildTheme(draft);
        string? clamped = ApplyAccent(theme, skin.Mode, clean.Palette);
        if (clamped is not null)
        {
            palette["primary"] = clamped;
        }

        return skin with { Palette = palette, Tokens = tokens, Theme = theme };
    }

    private static void AddFace(Draft draft, string name, IReadOnlyList<string> families)
    {
        if (families.Count > 0)
        {
            draft.Faces[name] = [.. families];
        }
    }

    /// <summary>Why a palette entry is refused, or <see langword="null"/> when it is valid.</summary>
    private static string? PaletteProblem(string key, string value)
    {
        if (!PaletteProperties.TryGetValue(key, out PropertyInfo? property))
        {
            return "unknown palette key.";
        }

        if (!ColourPattern().IsMatch(value))
        {
            return $"'{value}' is not a hex or rgb()/rgba() colour.";
        }

        if (property.PropertyType == typeof(MudColor))
        {
            try
            {
                _ = new MudColor(value);
            }
            catch (Exception)
            {
                // MudColor's parser throws a different exception type for each malformed shape; every one means the same thing here.
                return $"'{value}' is not a colour MudBlazor can read.";
            }
        }

        return null;
    }

    /// <summary>Why a token entry is refused against the tokens merged so far, or <see langword="null"/> when it is valid.</summary>
    private static string? TokenProblem(Skin skin, Dictionary<string, string> merged, string key, string value)
    {
        if (!SkinTokens.Contains(key))
        {
            return "unknown token key.";
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            return "the expression is empty.";
        }

        Dictionary<string, string> candidate = new(merged, StringComparer.Ordinal) { [key] = value };
        try
        {
            SkinExpression.Validate(candidate, skin.Id);
        }
        catch (SkinException exception)
        {
            return exception.Message;
        }

        return null;
    }

    /// <summary>
    /// Replaces the Primary family of the theme's palette with the customized <c>primary</c>, clamped against that palette's
    /// Background and Surface, and returns the clamped colour as hex; <see langword="null"/> when <c>primary</c> is not customized.
    /// </summary>
    private static string? ApplyAccent(MudTheme theme, ThemeMode mode, IReadOnlyDictionary<string, string> customized)
    {
        if (!customized.TryGetValue("primary", out string? primary))
        {
            return null;
        }

        bool dark = mode == ThemeMode.Dark;
        Palette palette = dark ? theme.PaletteDark : theme.PaletteLight;
        MudColor clamped = AccentPalette.ClampForContrast(new MudColor(primary), palette.Background, palette.Surface, towardBlack: !dark);
        palette.Primary = clamped;
        if (!customized.ContainsKey("primaryDarken"))
        {
            palette.PrimaryDarken = clamped.ColorRgbDarken().Value;
        }

        if (!customized.ContainsKey("primaryContrastText"))
        {
            palette.PrimaryContrastText = ContrastColour.ReadableForeground(clamped);
        }

        if (!customized.ContainsKey("tertiary"))
        {
            // Review's signal moves with the accent so it stays at least 30 degrees from it, from Info and from the other signals.
            palette.Tertiary = AccentPalette.ChooseTertiary(clamped, palette, palette.Tertiary, towardBlack: !dark);
        }

        return clamped.ToString(MudColorOutputFormats.Hex);
    }

    private static Dictionary<string, string> ReadEmbedded()
    {
        return EmbeddedResources.ReadFolder(
            typeof(SkinLoader).Assembly,
            ResourcePrefix,
            static name => name.EndsWith(".json", StringComparison.Ordinal) && !string.Equals(name, SchemaFileName, StringComparison.Ordinal));
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
            HashSet<string> seen = new(StringComparer.Ordinal);
            foreach (JsonProperty property in root.EnumerateObject())
            {
                ApplyProperty(file, property, draft);
                _ = seen.Add(property.Name);
            }

            if (!(seen.Contains("id") && seen.Contains("label") && seen.Contains("mode") && seen.Contains("order")))
            {
                throw Fail(file, "'id', 'label', 'mode' and 'order' are required.");
            }

            return draft;
        }
    }

    /// <summary>Applies one top-level property of a skin file to the draft.</summary>
    /// <param name="file">The file name, for error messages.</param>
    /// <param name="property">The property to apply.</param>
    /// <param name="draft">The draft being filled in.</param>
    private static void ApplyProperty(string file, JsonProperty property, Draft draft)
    {
        switch (property.Name)
        {
            case "$schema":
                break;
            case "id":
                draft.Id = ReadString(file, "id", property.Value);
                break;
            case "label":
                draft.Label = ReadString(file, "label", property.Value);
                break;
            case "mode":
                draft.Mode = ReadMode(file, property.Value);
                break;
            case "order":
                if (property.Value.ValueKind != JsonValueKind.Number || !property.Value.TryGetInt32(out int order))
                {
                    throw Fail(file, "'order' must be an integer.");
                }

                draft.Order = order;
                break;
            case "include":
                draft.Include = ReadString(file, "include", property.Value);
                break;
            case "radius":
                draft.Radius = ReadString(file, "radius", property.Value);
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
        ValidateTokens(draft);
        SkinFaces faces = new(
            draft.Faces.GetValueOrDefault("ui") ?? [],
            draft.Faces.GetValueOrDefault("read") ?? [],
            draft.Faces.GetValueOrDefault("mono") ?? []);
        SkinTraits traits = new(
            Trait(draft, "message", MessageLayout.Bubble),
            Trait(draft, "send", SendStyle.Icon),
            Trait(draft, "status", StatusStyle.Glyph),
            Trait(draft, "labels", LabelCase.Sentence),
            Trait(draft, "composer", ComposerStyle.Box),
            Trait(draft, "nav", NavStyle.Tint),
            Trait(draft, "surface", SurfaceStyle.Flat),
            Trait(draft, "voice", VoiceStyle.Plain),
            Trait(draft, "ids", IdStyle.Plain));

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
            BuildTheme(draft));
    }

    /// <summary>
    /// Checks the skin's merged token set - the registry defaults for its mode with its overrides applied - against the
    /// expression grammar, so a bad value fails at load rather than at first render.
    /// </summary>
    /// <param name="draft">The merged skin.</param>
    /// <exception cref="SkinException">A value is invalid; the message names the file, the key and the value.</exception>
    private static void ValidateTokens(Draft draft)
    {
        Dictionary<string, string> merged = new(StringComparer.Ordinal);
        foreach (Token token in SkinTokens.All)
        {
            string fallback = draft.Mode == ThemeMode.Dark ? token.DarkDefault : token.LightDefault;
            merged[token.Key] = draft.Tokens.GetValueOrDefault(token.Key) ?? fallback;
        }

        try
        {
            SkinExpression.Validate(merged, draft.Id);
        }
        catch (SkinException exception)
        {
            throw Fail(draft.File, exception.Message);
        }
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
            ApplyReadableContrastText(palette, draft.Palette);
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

    /// <summary>
    /// Gives the warning, info and secondary hues the glyph colour that reads on them. A dark Theme's hues are bright, so
    /// MudBlazor's white default fails the 3:1 floor for graphics on the Feed's kind badges; a skin that sets the key
    /// itself keeps its own value.
    /// </summary>
    /// <param name="palette">The dark palette being built.</param>
    /// <param name="values">The palette keys the skin set.</param>
    private static void ApplyReadableContrastText(PaletteDark palette, Dictionary<string, string> values)
    {
        if (!values.ContainsKey("warningContrastText"))
        {
            palette.WarningContrastText = ContrastColour.ReadableForeground(palette.Warning);
        }

        if (!values.ContainsKey("infoContrastText"))
        {
            palette.InfoContrastText = ContrastColour.ReadableForeground(palette.Info);
        }

        if (!values.ContainsKey("secondaryContrastText"))
        {
            palette.SecondaryContrastText = ContrastColour.ReadableForeground(palette.Secondary);
        }
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

    internal static string Kebab(string name) =>
        string.Concat(name.Select(static (character, index) =>
            char.IsUpper(character) && index > 0 ? "-" + char.ToLowerInvariant(character) : char.ToLowerInvariant(character).ToString()));

    private static SkinException Fail(string file, string message) => new($"{file}: {message}");

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

        public string? LetterSpacing { get; set; }

        public string? LineHeight { get; set; }

        public Dictionary<string, string[]> Faces { get; } = new(StringComparer.Ordinal);

        public Dictionary<int, string> Shadows { get; } = [];

        public Dictionary<string, string> Palette { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, string> Tokens { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, string> Traits { get; } = new(StringComparer.Ordinal);
    }
}
