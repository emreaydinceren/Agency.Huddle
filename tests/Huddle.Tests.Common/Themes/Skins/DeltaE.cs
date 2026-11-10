namespace Agency.Huddle.Tests.Themes.Skins;

/// <summary>
/// Colour difference. <see cref="Ciede2000(LabColour, LabColour)"/> is the CIEDE2000 formula as Sharma, Wu and Dalal
/// publish it ("The CIEDE2000 Color-Difference Formula: Implementation Notes, Supplementary Test Data, and Mathematical
/// Observations", 2005): about 1 is the smallest difference an eye can tell apart side by side, and under about 2 two
/// flat fills read as one.
/// </summary>
internal static class DeltaE
{
    private const double Pow25To7 = 6103515625.0;

    /// <summary>The CIEDE2000 difference between two sRGB colours, measured in Lab.</summary>
    /// <param name="first">One colour.</param>
    /// <param name="second">The other colour.</param>
    /// <returns>The difference; 0 for identical colours, symmetric in its arguments.</returns>
    public static double Ciede2000(SrgbColour first, SrgbColour second) => Ciede2000(first.ToLab(), second.ToLab());

    /// <summary>The CIEDE2000 difference between two Lab colours, with the weighting factors kL = kC = kH = 1.</summary>
    /// <param name="first">One colour.</param>
    /// <param name="second">The other colour.</param>
    /// <returns>The difference; 0 for identical colours, symmetric in its arguments.</returns>
    public static double Ciede2000(LabColour first, LabColour second)
    {
        double c1 = Math.Sqrt((first.A * first.A) + (first.B * first.B));
        double c2 = Math.Sqrt((second.A * second.A) + (second.B * second.B));
        double meanC = (c1 + c2) / 2;
        double meanC7 = Math.Pow(meanC, 7);
        double g = 0.5 * (1 - Math.Sqrt(meanC7 / (meanC7 + Pow25To7)));
        double a1 = (1 + g) * first.A;
        double a2 = (1 + g) * second.A;
        double chroma1 = Math.Sqrt((a1 * a1) + (first.B * first.B));
        double chroma2 = Math.Sqrt((a2 * a2) + (second.B * second.B));
        double hue1 = HueDegrees(first.B, a1);
        double hue2 = HueDegrees(second.B, a2);

        double deltaL = second.L - first.L;
        double deltaC = chroma2 - chroma1;
        double deltaHue = HueDifference(chroma1 * chroma2, hue1, hue2);
        double deltaH = 2 * Math.Sqrt(chroma1 * chroma2) * Math.Sin(Radians(deltaHue / 2));

        double meanL = (first.L + second.L) / 2;
        double meanChroma = (chroma1 + chroma2) / 2;
        double meanHue = MeanHue(chroma1 * chroma2, hue1, hue2);
        double t = 1
            - (0.17 * Math.Cos(Radians(meanHue - 30)))
            + (0.24 * Math.Cos(Radians(2 * meanHue)))
            + (0.32 * Math.Cos(Radians((3 * meanHue) + 6)))
            - (0.20 * Math.Cos(Radians((4 * meanHue) - 63)));
        double deltaTheta = 30 * Math.Exp(-Math.Pow((meanHue - 275) / 25, 2));
        double meanChroma7 = Math.Pow(meanChroma, 7);
        double rc = 2 * Math.Sqrt(meanChroma7 / (meanChroma7 + Pow25To7));
        double sl = 1 + ((0.015 * Math.Pow(meanL - 50, 2)) / Math.Sqrt(20 + Math.Pow(meanL - 50, 2)));
        double sc = 1 + (0.045 * meanChroma);
        double sh = 1 + (0.015 * meanChroma * t);
        double rt = -Math.Sin(Radians(2 * deltaTheta)) * rc;

        double l = deltaL / sl;
        double c = deltaC / sc;
        double h = deltaH / sh;
        return Math.Sqrt((l * l) + (c * c) + (h * h) + (rt * c * h));
    }

    private static double Radians(double degrees) => degrees * Math.PI / 180;

    private static double HueDegrees(double b, double aPrime)
    {
        if (b == 0 && aPrime == 0)
        {
            return 0;
        }

        double degrees = Math.Atan2(b, aPrime) * 180 / Math.PI;
        return degrees < 0 ? degrees + 360 : degrees;
    }

    private static double HueDifference(double chromaProduct, double hue1, double hue2)
    {
        if (chromaProduct == 0)
        {
            return 0;
        }

        double difference = hue2 - hue1;
        if (difference > 180)
        {
            return difference - 360;
        }

        return difference < -180 ? difference + 360 : difference;
    }

    private static double MeanHue(double chromaProduct, double hue1, double hue2)
    {
        if (chromaProduct == 0)
        {
            return hue1 + hue2;
        }

        return Math.Abs(hue1 - hue2) > 180 ? (hue1 + hue2 + 360) / 2 : (hue1 + hue2) / 2;
    }
}
