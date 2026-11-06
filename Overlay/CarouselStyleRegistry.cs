using System.Drawing;
using System.Reflection;

public enum CarouselCategory
{
    Full,
    Compact,
    Minimal
}

public interface ICarouselStyle
{
    string DisplayName { get; }
    CarouselCategory Category { get; }
    int SortOrder => 100;
    bool IsInstantCut => false;
    bool DrawsOwnNeighborPreviews => false;
    void Render(CrossfadeOverlayForm host, Graphics g, RectangleF heroRect, int baseH, float scale);
}

public static class CarouselStyleRegistry
{
    private static readonly List<ICarouselStyle> _allStyles = DiscoverStyles();

    private static List<ICarouselStyle> DiscoverStyles()
    {
        var list = new List<ICarouselStyle>();
        try
        {
            var iface = typeof(ICarouselStyle);
            foreach (var type in Assembly.GetExecutingAssembly().GetTypes())
            {
                if (!type.IsAbstract && !type.IsInterface && iface.IsAssignableFrom(type))
                {
                    if (Activator.CreateInstance(type) is ICarouselStyle instance)
                    {
                        list.Add(instance);
                    }
                }
            }
        }
        catch { }

        return list
            .OrderBy(s => s.Category)
            .ThenBy(s => s.SortOrder)
            .ThenBy(s => s.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static CarouselCategory ParseCategory(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode)) return CarouselCategory.Full;
        if (mode.StartsWith("Compact", StringComparison.OrdinalIgnoreCase)) return CarouselCategory.Compact;
        if (mode.StartsWith("Minimal", StringComparison.OrdinalIgnoreCase)) return CarouselCategory.Minimal;
        return CarouselCategory.Full;
    }

    public static IReadOnlyList<ICarouselStyle> GetStyles(CarouselCategory category) =>
        _allStyles.Where(s => s.Category == category).ToList();

    public static object[] GetDisplayNames(CarouselCategory category) =>
        _allStyles.Where(s => s.Category == category).Select(s => (object)s.DisplayName).ToArray();

    public static ICarouselStyle? Resolve(string? styleName, string? modeHint)
    {
        CarouselCategory cat = ParseCategory(modeHint);
        var categoryStyles = GetStyles(cat);
        if (categoryStyles.Count == 0) return _allStyles.FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(styleName))
        {
            string s = styleName.Trim();
            var exact = categoryStyles.FirstOrDefault(x => x.DisplayName.Equals(s, StringComparison.OrdinalIgnoreCase));
            if (exact != null) return exact;

            var partial = categoryStyles.FirstOrDefault(x =>
                x.DisplayName.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                s.Contains(x.DisplayName, StringComparison.OrdinalIgnoreCase));
            if (partial != null) return partial;
        }

        return categoryStyles[0];
    }
}