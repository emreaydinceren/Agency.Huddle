namespace Agency.Huddle.Tests.Themes.Skins;

/// <summary>A colour in CIE L*a*b* (D65 white), the space <see cref="DeltaE.Ciede2000(LabColour, LabColour)"/> measures in.</summary>
/// <param name="L">Lightness, 0 to 100.</param>
/// <param name="A">The green to red axis.</param>
/// <param name="B">The blue to yellow axis.</param>
internal readonly record struct LabColour(double L, double A, double B);
