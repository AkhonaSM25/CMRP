namespace CMRP.Models;

/// <summary>Maps an incident type to the icon and colour class used on report cards.</summary>
public static class CategoryStyles
{
    public record Style(string Icon, string Css);

    public static Style For(string categoryName)
    {
        var n = categoryName.ToLowerInvariant();
        if (n.Contains("electr") || n.Contains("light")) return new Style("zap", "cat-orange");
        if (n.Contains("water") || n.Contains("plumb")) return new Style("droplet", "cat-blue");
        if (n.Contains("vandal")) return new Style("shield", "cat-green");
        if (n.Contains("window")) return new Style("image", "cat-red");
        if (n.Contains("sanit")) return new Style("trash", "cat-teal");
        if (n.Contains("wi-fi") || n.Contains("network") || n.Contains("projector") || n.Contains("computer"))
            return new Style("monitor", "cat-purple");
        return new Style("tool", "cat-grey");
    }
}
