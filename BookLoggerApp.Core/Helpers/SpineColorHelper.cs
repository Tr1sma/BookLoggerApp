using System.Globalization;

namespace BookLoggerApp.Core.Helpers;

public static class SpineColorHelper
{
    private static readonly Dictionary<string, (string Dark, string Light)> _presetPalette = new(StringComparer.OrdinalIgnoreCase)
    {
        // Reds/Pinks
        { "red", ("8B0000", "CD5C5C") },       // DarkRed -> IndianRed
        { "crimson", ("DC143C", "FF6B6B") },
        { "maroon", ("800000", "A52A2A") },
        { "burgundy", ("6B3240", "9D5B6B") },
        { "pink", ("FF1493", "FF69B4") },       // DeepPink -> HotPink
        { "rose", ("C71585", "DB7093") },       // MediumVioletRed -> PaleVioletRed

        // Oranges/Browns
        { "orange", ("FF4500", "FF8C00") },     // OrangeRed -> DarkOrange
        { "amber", ("FFBF00", "FFD700") },
        { "brown", ("4A3426", "8B4513") },      // Old Brown -> SaddleBrown
        { "rust", ("A0522D", "CD853F") },       // Sienna -> Peru
        { "chocolate", ("D2691E", "F4A460") },  // Chocolate -> SandyBrown
        { "clay", ("8B4513", "BC8F8F") },       // SaddleBrown -> RosyBrown

        // Yellows/Golds
        { "gold", ("B8860B", "DAA520") },       // DarkGoldenrod -> Goldenrod
        { "sand", ("F4A460", "F0E68C") },       // SandyBrown -> Khaki

        // Greens
        { "green", ("006400", "228B22") },      // DarkGreen -> ForestGreen
        { "emerald", ("008000", "32CD32") },    // Green -> LimeGreen
        { "olive", ("556B2F", "6B8E23") },      // DarkOliveGreen -> OliveDrab
        { "sage", ("2F4F4F", "8FBC8F") },       // DarkSlateGray -> DarkSeaGreen
        { "forest", ("013220", "228B22") },
        { "lime", ("32CD32", "98FB98") },       // LimeGreen -> PaleGreen

        // Blues
        { "blue", ("00008B", "4169E1") },       // DarkBlue -> RoyalBlue
        { "navy", ("000080", "191970") },       // Navy -> MidnightBlue
        { "teal", ("008080", "20B2AA") },       // Teal -> LightSeaGreen
        { "cyan", ("008B8B", "48D1CC") },       // DarkCyan -> MediumTurquoise
        { "sky", ("00BFFF", "87CEEB") },        // DeepSkyBlue -> SkyBlue
        { "indigo", ("4B0082", "6A5ACD") },     // Indigo -> SlateBlue

        // Purples/Violets
        { "purple", ("800080", "9370DB") },     // Purple -> MediumPurple
        { "violet", ("8A2BE2", "BA55D3") },     // BlueViolet -> MediumOrchid
        { "plum", ("DDA0DD", "EE82EE") },       // Plum -> Violet
        { "lavender", ("9370DB", "E6E6FA") },   // MediumPurple -> Lavender

        // Grays/Blacks
        { "black", ("000000", "2F4F4F") },      // Black -> DarkSlateGray
        { "gray", ("696969", "A9A9A9") },       // DimGray -> DarkGray
        { "silver", ("808080", "C0C0C0") },     // Gray -> Silver
        { "slate", ("708090", "778899") },      // SlateGray -> LightSlateGray
        { "white", ("DCDCDC", "F5F5F5") },      // Gainsboro -> WhiteSmoke
    };

    /// <summary>
    /// Gets the palette of preset colors.
    /// Key: Color Name, Value: (DarkHex, LightHex) without # prefix.
    /// </summary>
    public static IReadOnlyDictionary<string, (string Dark, string Light)> Palette => _presetPalette;

    /// <summary>
    /// Gets the dark and light hex colors (without #) for a given spine color identifier.
    /// Handles presets, custom hex codes, and falls back to a hash-based color if invalid.
    /// </summary>
    public static (string Dark, string Light) GetColors(string? spineColor, Guid bookId)
    {
        // 1. Try Presets
        if (!string.IsNullOrEmpty(spineColor) && _presetPalette.TryGetValue(spineColor, out var preset))
        {
            return preset;
        }

        // 2. Try Custom Hex Code
        if (!string.IsNullOrEmpty(spineColor) && spineColor.StartsWith("#"))
        {
            var baseHex = spineColor.TrimStart('#');
            if (baseHex.Length == 6)
            {
                // Generate light variant simply by blending with white or brightening
                // For a spine, "Dark" is the base, "Light" is the highlight
                // Let's assume the user picked the "Dark" base color.
                return (baseHex, Lighten(baseHex, 0.3f));
            }
        }

        // 3. Fallback: Hash-based color
        var colorList = _presetPalette.Values.ToList();
        var hash = bookId.GetHashCode();
        var colorIndex = (hash & 0x7FFFFFFF) % colorList.Count;
        return colorList[colorIndex];
    }

    private static string Lighten(string hexColor, float amount)
    {
        if (int.TryParse(hexColor.AsSpan(0, 2), NumberStyles.HexNumber, null, out int r) &&
            int.TryParse(hexColor.AsSpan(2, 2), NumberStyles.HexNumber, null, out int g) &&
            int.TryParse(hexColor.AsSpan(4, 2), NumberStyles.HexNumber, null, out int b))
        {
            r = (int)(r + (255 - r) * amount);
            g = (int)(g + (255 - g) * amount);
            b = (int)(b + (255 - b) * amount);
            return $"{Math.Clamp(r, 0, 255):X2}{Math.Clamp(g, 0, 255):X2}{Math.Clamp(b, 0, 255):X2}";
        }
        return hexColor; // Return original if parse fails
    }
}
