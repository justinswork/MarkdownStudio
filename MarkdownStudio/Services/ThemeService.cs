using System;
using Microsoft.UI.Xaml;
using Windows.UI.ViewManagement;

namespace MarkdownStudio.Services;

public sealed class ThemeService
{
    private readonly UISettings _uiSettings = new();

    public event Action<ElementTheme>? ThemeChanged;

    public ThemeService()
    {
        _uiSettings.ColorValuesChanged += (_, _) =>
        {
            ThemeChanged?.Invoke(CurrentTheme);
        };
    }

    public ElementTheme CurrentTheme
    {
        get
        {
            var background = _uiSettings.GetColorValue(UIColorType.Background);
            var isDark = background.R == 0 && background.G == 0 && background.B == 0;
            return isDark ? ElementTheme.Dark : ElementTheme.Light;
        }
    }

    public string MonacoThemeName => CurrentTheme == ElementTheme.Dark ? "vs-dark" : "vs";
    public string PreviewThemeName => CurrentTheme == ElementTheme.Dark ? "dark" : "light";
}
