using System;
using MarkdownStudio.Models;
using Windows.Storage;

namespace MarkdownStudio.Services;

public sealed class EditorPreferencesService
{
    private const string FontPresetKey = "editor.fontPreset.v1";
    private const string FontSizeKey   = "editor.fontSize.v1";
    private const string TabSizeKey    = "editor.tabSize.v1";

    private readonly EditorPreferences _prefs;

    public event Action<EditorPreferences>? Changed;

    public EditorPreferencesService()
    {
        _prefs = Load();
    }

    public EditorPreferences Preferences => _prefs;

    private static EditorPreferences Load()
    {
        var values = ApplicationData.Current.LocalSettings.Values;
        var prefs = new EditorPreferences();
        if (values[FontPresetKey] is string id   && !string.IsNullOrEmpty(id)) prefs.FontPresetId = id;
        if (values[FontSizeKey]   is int    size && size > 0)                  prefs.FontSize     = Clamp(size, 10, 28);
        if (values[TabSizeKey]    is int    tab  && tab  > 0)                  prefs.TabSize      = Clamp(tab,  1, 8);
        return prefs;
    }

    public void SetFontPreset(string id)
    {
        if (_prefs.FontPresetId == id) return;
        _prefs.FontPresetId = id;
        ApplicationData.Current.LocalSettings.Values[FontPresetKey] = id;
        Changed?.Invoke(_prefs);
    }

    public void SetFontSize(int size)
    {
        size = Clamp(size, 10, 28);
        if (_prefs.FontSize == size) return;
        _prefs.FontSize = size;
        ApplicationData.Current.LocalSettings.Values[FontSizeKey] = size;
        Changed?.Invoke(_prefs);
    }

    public void SetTabSize(int tab)
    {
        tab = Clamp(tab, 1, 8);
        if (_prefs.TabSize == tab) return;
        _prefs.TabSize = tab;
        ApplicationData.Current.LocalSettings.Values[TabSizeKey] = tab;
        Changed?.Invoke(_prefs);
    }

    private static int Clamp(int v, int lo, int hi) => v < lo ? lo : (v > hi ? hi : v);
}
