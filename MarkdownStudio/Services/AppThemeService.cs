using System;
using MarkdownStudio.Models;
using Microsoft.UI.Xaml;
using Windows.Storage;
using Windows.UI.ViewManagement;

namespace MarkdownStudio.Services;

public sealed class AppThemeService
{
    private const string ThemeKey = "selectedTheme.v1";

    private readonly UISettings _ui = new();
    private AppTheme _selected;

    public event Action<AppTheme>? Changed;

    public AppThemeService()
    {
        var stored = ApplicationData.Current.LocalSettings.Values[ThemeKey] as string;
        _selected = string.IsNullOrEmpty(stored) ? AppThemes.System : AppThemes.ById(stored);

        _ui.ColorValuesChanged += (_, _) =>
        {
            if (_selected.FollowsSystem) Changed?.Invoke(_selected);
        };
    }

    public AppTheme Selected => _selected;

    public ElementTheme EffectiveElementTheme =>
        _selected.FollowsSystem ? GetSystemTheme() : _selected.BaseTheme;

    public string EffectiveMonacoTheme
    {
        get
        {
            if (_selected.FollowsSystem)
                return GetSystemTheme() == ElementTheme.Dark ? "ms-midnight" : "ms-daylight";
            return string.IsNullOrEmpty(_selected.MonacoTheme) ? "ms-daylight" : _selected.MonacoTheme;
        }
    }

    public string EffectivePreviewClass
    {
        get
        {
            if (_selected.FollowsSystem)
                return GetSystemTheme() == ElementTheme.Dark ? "theme-midnight" : "theme-daylight";
            return string.IsNullOrEmpty(_selected.PreviewClassName) ? "theme-daylight" : _selected.PreviewClassName;
        }
    }

    public AppTheme EffectiveTheme =>
        _selected.FollowsSystem
            ? (GetSystemTheme() == ElementTheme.Dark ? AppThemes.Midnight : AppThemes.Daylight)
            : _selected;

    private ElementTheme GetSystemTheme()
    {
        var bg = _ui.GetColorValue(UIColorType.Background);
        return (bg.R == 0 && bg.G == 0 && bg.B == 0) ? ElementTheme.Dark : ElementTheme.Light;
    }

    public void Select(AppTheme theme)
    {
        if (_selected.Id == theme.Id) return;
        _selected = theme;
        ApplicationData.Current.LocalSettings.Values[ThemeKey] = theme.Id;
        Changed?.Invoke(theme);
    }
}
