using System.Globalization;

namespace Agency.Huddle.Tests.Themes.Skins;

/// <summary>
/// An opaque sRGB colour with channels kept as unrounded doubles from 0 to 255, so a chain of <c>color-mix()</c> steps
/// carries no rounding error until the one the browser applies when it paints (<see cref="Hex"/>).
/// </summary>
/// <param name="R">Red, 0 to 255.</param>
/// <param name="G">Green, 0 to 255.</param>
/// <param name="B">Blue, 0 to 255.</param>
internal readonly record struct SrgbColour(double R, double G, double B)
{
    /// <summary>The colour as <c>#rrggbb</c>, each channel rounded half away from zero as a browser does on paint.</summary>
    public string Hex => string.Create(CultureInfo.InvariantCulture, $"#{Round(this.R):x2}{Round(this.G):x2}{Round(this.B):x2}");

    /// <summary>The colour in CIE L*a*b* (D65), by the sRGB transfer curve and the usual XYZ matrix.</summary>
    /// <returns>The Lab colour.</returns>
    public LabColour ToLab()
    {
        double r = Linear(this.R);
        double g = Linear(this.G);
        double b = Linear(this.B);
        double x = ((0.4124 * r) + (0.3576 * g) + (0.1805 * b)) / 0.95047;
        double y = (0.2126 * r) + (0.7152 * g) + (0.0722 * b);
        double z = ((0.0193 * r) + (0.1192 * g) + (0.9505 * b)) / 1.08883;
        double fx = Pivot(x);
        double fy = Pivot(y);
        double fz = Pivot(z);
        return new LabColour((116 * fy) - 16, 500 * (fx - fy), 200 * (fy - fz));
    }

    private static int Round(double channel) => (int)Math.Clamp(Math.Round(channel, MidpointRounding.AwayFromZero), 0, 255);

    private static double Linear(double channel)
    {
        double value = channel / 255;
        return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
    }

    private static double Pivot(double t) => t > 0.008856 ? Math.Cbrt(t) : (7.787 * t) + (16.0 / 116);
}
