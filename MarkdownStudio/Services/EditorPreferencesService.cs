using System;
using MarkdownStudio.Models;
using Windows.Storage;

namespace MarkdownStudio.Services;

public sealed class EditorPreferencesService
{
    // Editor keys
    private const string FontPresetKey     = "editor.fontPreset.v1";
    private const string FontSizeKey       = "editor.fontSize.v1";
    private const string TabSizeKey        = "editor.tabSize.v1";
    private const string ShowWhitespaceKey = "editor.showWhitespace.v1";

    // Preview keys
    private const string PreviewFontKey       = "preview.fontPreset.v1";
    private const string PreviewSizeKey       = "preview.fontSize.v1";
    private const string PreviewLineHeightKey = "preview.lineHeight.v1";
    private const string PreviewWidthKey      = "preview.width.v1";
    private const string PreviewHeadingKey    = "preview.headingStyle.v1";

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

        if (values[FontPresetKey]     is string id   && !string.IsNullOrEmpty(id)) prefs.FontPresetId   = id;
        if (values[FontSizeKey]       is int    size && size > 0)                  prefs.FontSize       = Clamp(size, 10, 28);
        if (values[TabSizeKey]        is int    tab  && tab  > 0)                  prefs.TabSize        = Clamp(tab,  1, 8);
        if (values[ShowWhitespaceKey] is bool   ws)                                prefs.ShowWhitespace = ws;

        if (values[PreviewFontKey]       is string pfId && !string.IsNullOrEmpty(pfId)) prefs.PreviewFontPresetId = pfId;
        if (values[PreviewSizeKey]       is int    ps   && ps > 0)                      prefs.PreviewFontSize     = Clamp(ps,  13, 22);
        if (values[PreviewLineHeightKey] is double plh  && plh > 0)                     prefs.PreviewLineHeight   = ClampD(plh, 1.3, 2.2);
        if (values[PreviewWidthKey]      is string pwId && !string.IsNullOrEmpty(pwId)) prefs.PreviewWidthId      = pwId;
        if (values[PreviewHeadingKey]    is string phId && !string.IsNullOrEmpty(phId)) prefs.PreviewHeadingId    = phId;

        return prefs;
    }

    // -------- Editor setters --------

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

    public void SetShowWhitespace(bool show)
    {
        if (_prefs.ShowWhitespace == show) return;
        _prefs.ShowWhitespace = show;
        ApplicationData.Current.LocalSettings.Values[ShowWhitespaceKey] = show;
        Changed?.Invoke(_prefs);
    }

    // -------- Preview setters --------

    public void SetPreviewFontPreset(string id)
    {
        if (_prefs.PreviewFontPresetId == id) return;
        _prefs.PreviewFontPresetId = id;
        ApplicationData.Current.LocalSettings.Values[PreviewFontKey] = id;
        Changed?.Invoke(_prefs);
    }

    public void SetPreviewFontSize(int size)
    {
        size = Clamp(size, 13, 22);
        if (_prefs.PreviewFontSize == size) return;
        _prefs.PreviewFontSize = size;
        ApplicationData.Current.LocalSettings.Values[PreviewSizeKey] = size;
        Changed?.Invoke(_prefs);
    }

    public void SetPreviewLineHeight(double lh)
    {
        lh = ClampD(lh, 1.3, 2.2);
        if (Math.Abs(_prefs.PreviewLineHeight - lh) < 0.001) return;
        _prefs.PreviewLineHeight = lh;
        ApplicationData.Current.LocalSettings.Values[PreviewLineHeightKey] = lh;
        Changed?.Invoke(_prefs);
    }

    public void SetPreviewWidth(string id)
    {
        if (_prefs.PreviewWidthId == id) return;
        _prefs.PreviewWidthId = id;
        ApplicationData.Current.LocalSettings.Values[PreviewWidthKey] = id;
        Changed?.Invoke(_prefs);
    }

    public void SetPreviewHeadingStyle(string id)
    {
        if (_prefs.PreviewHeadingId == id) return;
        _prefs.PreviewHeadingId = id;
        ApplicationData.Current.LocalSettings.Values[PreviewHeadingKey] = id;
        Changed?.Invoke(_prefs);
    }

    private static int    Clamp (int    v, int    lo, int    hi) => v < lo ? lo : (v > hi ? hi : v);
    private static double ClampD(double v, double lo, double hi) => v < lo ? lo : (v > hi ? hi : v);
}
