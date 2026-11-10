using System.Globalization;

namespace Agency.Huddle.Tests.Themes.Skins;

/// <summary>
/// <see cref="DeltaE.Ciede2000(LabColour, LabColour)"/> against the 34 test pairs Sharma, Wu and Dalal publish with the
/// CIEDE2000 formula (2005), which exist to catch the hue-angle, mean-hue and rotation-term slips every implementation
/// makes once, plus the sRGB to Lab conversion that turns it into a colour-difference measure.
/// </summary>
public sealed class DeltaETests
{
    /// <summary>L1, a1, b1, L2, a2, b2 and the published difference, in the order of the paper's Table 1.</summary>
    private static readonly double[][] Sharma =
    [
        [50.0000, 2.6772, -79.7751, 50.0000, 0.0000, -82.7485, 2.0425],
        [50.0000, 3.1571, -77.2803, 50.0000, 0.0000, -82.7485, 2.8615],
        [50.0000, 2.8361, -74.0200, 50.0000, 0.0000, -82.7485, 3.4412],
        [50.0000, -1.3802, -84.2814, 50.0000, 0.0000, -82.7485, 1.0000],
        [50.0000, -1.1848, -84.8006, 50.0000, 0.0000, -82.7485, 1.0000],
        [50.0000, -0.9009, -85.5211, 50.0000, 0.0000, -82.7485, 1.0000],
        [50.0000, 0.0000, 0.0000, 50.0000, -1.0000, 2.0000, 2.3669],
        [50.0000, -1.0000, 2.0000, 50.0000, 0.0000, 0.0000, 2.3669],
        [50.0000, 2.4900, -0.0010, 50.0000, -2.4900, 0.0009, 7.1792],
        [50.0000, 2.4900, -0.0010, 50.0000, -2.4900, 0.0010, 7.1792],
        [50.0000, 2.4900, -0.0010, 50.0000, -2.4900, 0.0011, 7.2195],
        [50.0000, 2.4900, -0.0010, 50.0000, -2.4900, 0.0012, 7.2195],
        [50.0000, -0.0010, 2.4900, 50.0000, 0.0009, -2.4900, 4.8045],
        [50.0000, -0.0010, 2.4900, 50.0000, 0.0010, -2.4900, 4.8045],
        [50.0000, -0.0010, 2.4900, 50.0000, 0.0011, -2.4900, 4.7461],
        [50.0000, 2.5000, 0.0000, 50.0000, 0.0000, -2.5000, 4.3065],
        [50.0000, 2.5000, 0.0000, 73.0000, 25.0000, -18.0000, 27.1492],
        [50.0000, 2.5000, 0.0000, 61.0000, -5.0000, 29.0000, 22.8977],
        [50.0000, 2.5000, 0.0000, 56.0000, -27.0000, -3.0000, 31.9030],
        [50.0000, 2.5000, 0.0000, 58.0000, 24.0000, 15.0000, 19.4535],
        [50.0000, 2.5000, 0.0000, 50.0000, 3.1736, 0.5854, 1.0000],
        [50.0000, 2.5000, 0.0000, 50.0000, 3.2972, 0.0000, 1.0000],
        [50.0000, 2.5000, 0.0000, 50.0000, 1.8634, 0.5757, 1.0000],
        [50.0000, 2.5000, 0.0000, 50.0000, 3.2592, 0.3350, 1.0000],
        [60.2574, -34.0099, 36.2677, 60.4626, -34.1751, 39.4387, 1.2644],
        [63.0109, -31.0961, -5.8663, 62.8187, -29.7946, -4.0864, 1.2630],
        [61.2901, 3.7196, -5.3901, 61.4292, 2.2480, -4.9620, 1.8731],
        [35.0831, -44.1164, 3.7933, 35.0232, -40.0716, 1.5901, 1.8645],
        [22.7233, 20.0904, -46.6940, 23.0331, 14.9730, -42.5619, 2.0373],
        [36.4612, 47.8580, 18.3852, 36.2715, 50.5065, 21.2231, 1.4146],
        [90.8027, -2.0831, 1.4410, 91.1528, -1.6435, 0.0447, 1.4441],
        [90.9257, -0.5406, -0.9208, 88.6381, -0.8985, -0.7239, 1.5381],
        [6.7747, -0.2908, -2.4247, 5.8714, -0.0985, -2.2286, 0.6377],
        [2.0776, 0.0795, -1.1350, 0.9033, -0.0636, -0.5514, 0.9082],
    ];

    /// <summary>All 34 published pairs come out to four decimals, in both argument orders.</summary>
    [Fact]
    public void Ciede2000_MatchesTheSharmaTestPairs()
    {
        List<string> failures = [];
        for (int index = 0; index < Sharma.Length; index++)
        {
            double[] row = Sharma[index];
            LabColour sample1 = new(row[0], row[1], row[2]);
            LabColour sample2 = new(row[3], row[4], row[5]);
            double forward = DeltaE.Ciede2000(sample1, sample2);
            double backward = DeltaE.Ciede2000(sample2, sample1);
            if (Math.Abs(forward - row[6]) >= 5e-5 || Math.Abs(backward - row[6]) >= 5e-5)
            {
                failures.Add(string.Create(CultureInfo.InvariantCulture, $"pair {index + 1}: expected {row[6]:0.0000}, got {forward:0.0000} and {backward:0.0000}"));
            }
        }

        Assert.Equal(34, Sharma.Length);
        Assert.Empty(failures);
    }

    /// <summary>Identical colours differ by exactly nothing.</summary>
    [Fact]
    public void Ciede2000_IdenticalColours_AreZero()
    {
        SrgbColour colour = new(115, 104, 211);

        Assert.Equal(0.0, DeltaE.Ciede2000(colour, colour));
    }

    /// <summary>White is L 100 and black L 0 with no chroma, and pure red lands on its published Lab value.</summary>
    [Fact]
    public void ToLab_KnownColours()
    {
        LabColour white = new SrgbColour(255, 255, 255).ToLab();
        LabColour black = new SrgbColour(0, 0, 0).ToLab();
        LabColour red = new SrgbColour(255, 0, 0).ToLab();

        Assert.InRange(white.L, 99.99, 100.01);
        Assert.InRange(Math.Abs(white.A) + Math.Abs(white.B), 0, 0.1);
        Assert.InRange(black.L, -0.01, 0.01);
        Assert.InRange(red.L, 53.1, 53.4);
        Assert.InRange(red.A, 79.9, 80.3);
        Assert.InRange(red.B, 67.0, 67.4);
    }

    /// <summary>The hex form rounds half away from zero, and clamps nothing it should not: 229.5 prints as e6.</summary>
    [Fact]
    public void Hex_RoundsHalfAwayFromZero()
    {
        Assert.Equal("#e6e6e6", new SrgbColour(229.5, 229.5, 229.5).Hex);
        Assert.Equal("#0001ff", new SrgbColour(0.4, 0.5, 255).Hex);
    }
}
