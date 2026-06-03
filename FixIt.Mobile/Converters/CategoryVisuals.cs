namespace FixIt.Mobile.Converters;

// Single source of truth mapping an issue category to its feed colour (hue) and glyph.
// Recognises the real server taxonomy (IssueCategory: Infrastructure, PublicSafety,
// EnvironmentalHealth, Parks, Transportation, Utilities, Sanitation, PublicHealth, Other —
// arriving as spaced display names) plus the design prototype's keyword aliases. Unknown
// values hash to a stable hue so each category still gets a consistent colour rather than
// collapsing to one flat fill. Used by CategoryToBrushConverter + CategoryToIconConverter.
internal static class CategoryVisuals
{
    public static double HueFor(string? category)
    {
        var c = Normalize(category);
        if (c.Length == 0) return 210;

        if (c.Contains("infrastructure") || c.Contains("pothole") || c.Contains("road") || c.Contains("bridge")) return 28;
        if (c.Contains("transport") || c.Contains("transit") || c.Contains("traffic") || c.Contains("sign") || c.Contains("signal") || c.Contains("parking")) return 220;
        if (c.Contains("environment")) return 174;
        if (c.Contains("park") || c.Contains("tree") || c.Contains("green") || c.Contains("garden") || c.Contains("recreation")) return 135;
        if (c.Contains("utilit") || c.Contains("electric") || c.Contains("light") || c.Contains("gas") || c.Contains("power")) return 45;
        if (c.Contains("sanit") || c.Contains("trash") || c.Contains("waste") || c.Contains("dump") || c.Contains("litter") || c.Contains("clean")) return 150;
        if (c.Contains("safety") || c.Contains("hazard") || c.Contains("crime") || c.Contains("accident")) return 2;
        if (c.Contains("health") || c.Contains("disease")) return 320;
        if (c.Contains("water") || c.Contains("leak") || c.Contains("flood") || c.Contains("drain")) return 200;
        if (c.Contains("graffiti") || c.Contains("vandal")) return 286;
        if (c.Contains("sidewalk") || c.Contains("pavement")) return 240;
        if (c.Contains("other")) return 250;

        return StableHash(c) % 360;
    }

    public static string IconFor(string? category)
    {
        var c = Normalize(category);
        if (c.Length == 0) return "feed_hazard.png";

        if (c.Contains("infrastructure") || c.Contains("pothole") || c.Contains("road") || c.Contains("bridge")) return "cat_road.png";
        if (c.Contains("transport") || c.Contains("transit") || c.Contains("traffic") || c.Contains("sign") || c.Contains("signal") || c.Contains("parking")) return "cat_signage.png";
        if (c.Contains("environment")) return "cat_water.png";
        if (c.Contains("park") || c.Contains("tree") || c.Contains("green") || c.Contains("garden") || c.Contains("recreation")) return "cat_park.png";
        if (c.Contains("utilit") || c.Contains("electric") || c.Contains("light") || c.Contains("gas") || c.Contains("power")) return "cat_light.png";
        if (c.Contains("sanit") || c.Contains("trash") || c.Contains("waste") || c.Contains("dump") || c.Contains("litter") || c.Contains("clean")) return "cat_waste.png";
        if (c.Contains("water") || c.Contains("leak") || c.Contains("flood") || c.Contains("drain")) return "cat_water.png";
        if (c.Contains("graffiti") || c.Contains("vandal")) return "cat_graffiti.png";
        if (c.Contains("sidewalk") || c.Contains("pavement")) return "cat_sidewalk.png";

        // Public Safety / Public Health / Other → the generic civic hazard glyph.
        return "feed_hazard.png";
    }

    private static string Normalize(string? category) => category?.Trim().ToLowerInvariant() ?? string.Empty;

    private static int StableHash(string c)
    {
        var hash = 0;
        foreach (var ch in c)
        {
            hash = (hash * 31 + ch) & 0x7fffffff;
        }
        return hash;
    }
}
