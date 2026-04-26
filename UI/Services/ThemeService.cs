using System.Windows;
using System.Windows.Media;

namespace EvUdsAnalyzer.UI.Services;

public static class ThemeService
{
    public static void Apply(string? theme)
    {
        theme = string.IsNullOrWhiteSpace(theme) ? "Dark" : theme;
        ApplyWpfThemeMode(theme);
        bool dark = theme.Equals("Dark", StringComparison.OrdinalIgnoreCase);
        ApplyAppBrushes(dark);
    }

    private static void ApplyWpfThemeMode(string theme)
    {
        var app = System.Windows.Application.Current;
        if (app == null)
        {
            return;
        }

        var property = typeof(System.Windows.Application).GetProperty("ThemeMode");
        if (property?.PropertyType.IsEnum != true)
        {
            return;
        }

        object value = Enum.Parse(property.PropertyType, theme.Equals("System", StringComparison.OrdinalIgnoreCase) ? "System" : theme, ignoreCase: true);
        property.SetValue(app, value);
    }

    private static void ApplyAppBrushes(bool dark)
    {
        var resources = System.Windows.Application.Current.Resources;

        if (dark)
        {
            SetBrush(resources, "AppBackgroundBrush", "#0B1220");
            SetBrush(resources, "AppSurfaceBrush", "#111827");
            SetBrush(resources, "AppSurfaceAltBrush", "#1E293B");
            SetBrush(resources, "AppBorderBrush", "#334155");
            SetBrush(resources, "AppTextBrush", "#E5E7EB");
            SetBrush(resources, "AppMutedTextBrush", "#94A3B8");
            SetBrush(resources, "AppAccentBrush", "#60A55A");
            SetBrush(resources, "AppDangerBrush", "#F87171");
            SetBrush(resources, "AppWarningBrush", "#FBBF24");
            SetBrush(resources, "AppSuccessBrush", "#34D399");
        }
        else
        {
            SetBrush(resources, "AppBackgroundBrush", "#F7F9FC");
            SetBrush(resources, "AppSurfaceBrush", "#FFFFFF");
            SetBrush(resources, "AppSurfaceAltBrush", "#F1F5F9");
            SetBrush(resources, "AppBorderBrush", "#D0D7E2");
            SetBrush(resources, "AppTextBrush", "#0F172A");
            SetBrush(resources, "AppMutedTextBrush", "#475569");
            SetBrush(resources, "AppAccentBrush", "#2563EB");
            SetBrush(resources, "AppSuccessBrush", "#059669");
            SetBrush(resources, "AppWarningBrush", "#D97706");
            SetBrush(resources, "AppDangerBrush", "#DC2626");
        }
    }

    private static void SetBrush(ResourceDictionary resources, string key, string hex)
    {
        var color = (Color)ColorConverter.ConvertFromString(hex);

        var brush = new SolidColorBrush(color);
        brush.Freeze();

        resources[key] = brush;
    }
}
