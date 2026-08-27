using System.Windows;
using System.Windows.Media;

namespace QuickLookNext;

/// <summary>
/// v3.11.0: single source of truth for the custom-drawn surfaces (tray menu
/// and plugin manager panel). Text / fill colors come from one palette so the
/// two surfaces cannot drift apart; the accent prefers the live WPF-UI theme
/// accent so all surfaces follow the system accent color.
/// </summary>
internal static class ThemePalette
{
    internal static Brush Text(bool isDark) => FromHex(isDark ? "#F5F5F5" : "#1A1A1A");

    internal static Brush SecondaryText(bool isDark) => FromHex(isDark ? "#9E9E9E" : "#7A7A7A");

    internal static Brush Hover(bool isDark) => FromHex(isDark ? "#14FFFFFF" : "#10000000");

    internal static Brush Separator(bool isDark) => FromHex(isDark ? "#14FFFFFF" : "#10000000");

    internal static Brush Border(bool isDark) => FromHex(isDark ? "#26FFFFFF" : "#26000000");

    internal static Brush Tint(bool isDark) => FromHex(isDark ? "#8C20242A" : "#B8F8F6F4");

    internal static Brush ButtonBg(bool isDark) => FromHex(isDark ? "#14FFFFFF" : "#14000000");

    internal static Brush ButtonHover(bool isDark) => FromHex(isDark ? "#24FFFFFF" : "#24000000");

    internal static Brush Danger(bool isDark) => FromHex(isDark ? "#FFFF7B72" : "#FFC42B1C");

    /// <summary>
    /// v3.20.0: scrollbar thumb - dark on light surfaces, light on dark ones.
    /// </summary>
    internal static Brush ScrollbarThumb(bool isDark) => FromHex(isDark ? "#66FFFFFF" : "#59000000");

    internal static Brush Accent(bool isDark)
    {
        return LiveAccent() ?? FromHex(isDark ? "#60CDFF" : "#005FB8");
    }

    /// <summary>
    /// The accent color at low opacity, used for badges / selected states.
    /// </summary>
    internal static Brush AccentTint(bool isDark)
    {
        if (LiveAccent() is SolidColorBrush solid)
        {
            var c = solid.Color;
            return new SolidColorBrush(Color.FromArgb(0x18, c.R, c.G, c.B));
        }

        return FromHex(isDark ? "#1860CDFF" : "#18005FB8");
    }

    private static Brush LiveAccent()
    {
        try
        {
            return Application.Current?.TryFindResource("AccentFillColorDefaultBrush") as Brush;
        }
        catch
        {
            // Theme resources unavailable; fall back to the fixed palette.
            return null;
        }
    }

    private static Brush FromHex(string hex) =>
        new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
}
