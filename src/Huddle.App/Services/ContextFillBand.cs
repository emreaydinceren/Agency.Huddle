namespace Agency.Huddle.App.Services;

/// <summary>Maps a context fill percentage to a <see cref="ContextFillBandKind"/>. Sessions auto-compact around 97%, so the bands warn before that.</summary>
internal static class ContextFillBand
{
    /// <summary>The percentage from which the fill is a <see cref="ContextFillBandKind.Warning"/>.</summary>
    public const int WarningFrom = 75;

    /// <summary>The percentage from which the fill is a <see cref="ContextFillBandKind.Error"/>.</summary>
    public const int ErrorFrom = 90;

    /// <summary>The band for <paramref name="percent"/>.</summary>
    /// <param name="percent">The fill as a whole percent.</param>
    public static ContextFillBandKind Of(int percent) => percent switch
    {
        >= ErrorFrom => ContextFillBandKind.Error,
        >= WarningFrom => ContextFillBandKind.Warning,
        _ => ContextFillBandKind.Normal,
    };
}
