using System.Windows;
using System.Windows.Media;

namespace EvUdsAnalyzer.UI.Services;

public static class ThemeService
{
    public static void Apply(string? theme)
    {
        theme = string.IsNullOrWhiteSpace(theme) ? "System" : theme;
        ApplyWpfThemeMode(theme);
        ApplyAppBrushes(theme.Equals("Dark", StringComparison.OrdinalIgnoreCase));
    }

    private static void ApplyWpfThemeMode(string theme)
    {
        var property = typeof(System.Windows.Application).GetProperty("ThemeMode");
        if (property?.PropertyType.IsEnum != true)
        {
            return;
        }

        var normalizedTheme = theme.Equals("System", StringComparison.OrdinalIgnoreCase) ? "System" : theme;
        var value = Enum.Parse(property.PropertyType, normalizedTheme, ignoreCase: true);
        property.SetValue(System.Windows.Application.Current, value);
    }

    private static void ApplyAppBrushes(bool dark)
    {
        var resources = System.Windows.Application.Current.Resources;
        SetBrush(resources, "AppBackgroundBrush", dark ? "#111827" : "#F6F8FB");
        SetBrush(resources, "AppSurfaceBrush", dark ? "#1F2937" : "#FFFFFF");
        SetBrush(resources, "AppSurfaceAltBrush", dark ? "#263244" : "#EEF3F8");
        SetBrush(resources, "AppBorderBrush", dark ? "#374151" : "#D8DEE9");
        SetBrush(resources, "AppTextBrush", dark ? "#F8FAFC" : "#172033");
        SetBrush(resources, "AppMutedTextBrush", dark ? "#CBD5E1" : "#64748B");
        SetBrush(resources, "AppAccentBrush", dark ? "#60A5FA" : "#2563EB");
        SetBrush(resources, "AppDangerBrush", dark ? "#F87171" : "#B91C1C");
        SetBrush(resources, "AppWarningBrush", dark ? "#FBBF24" : "#B45309");
        SetBrush(resources, "AppSuccessBrush", dark ? "#34D399" : "#047857");
    }

    private static void SetBrush(ResourceDictionary resources, string key, string color) =>
        resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
}
