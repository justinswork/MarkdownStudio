using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MarkdownStudio.Models;
using MarkdownStudio.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;

namespace MarkdownStudio.Views;

public sealed partial class SettingsView : UserControl
{
    public IReadOnlyList<FontPreset> FontPresetsList { get; } = FontPresets.All;

    private EditorPreferencesService? _service;
    private bool _suppressEvents;
    private string _sampleText = "";

    public SettingsView()
    {
        InitializeComponent();
    }

    public void Attach(EditorPreferencesService service)
    {
        _service = service;
        ApplyPreferencesToUi(service.Preferences);
        service.Changed += p => DispatcherQueue.TryEnqueue(() => ApplyPreferencesToUi(p));
    }

    // Pre-loads Sample.md content for the right-side preview. Called by
    // SettingsDialog before ShowAsync, so the textblock has content the
    // moment the dialog opens.
    public async Task LoadSampleAsync()
    {
        _sampleText = await MainWindow.GetBundledSampleAsync();
        if (string.IsNullOrEmpty(_sampleText))
            _sampleText = "(Sample.md not found in the install location.)";
        UpdateSampleText();
    }

    private void ApplyPreferencesToUi(EditorPreferences prefs)
    {
        _suppressEvents = true;
        try
        {
            for (int i = 0; i < FontPresetsList.Count; i++)
            {
                if (FontPresetsList[i].Id == prefs.FontPresetId)
                {
                    FontCombo.SelectedIndex = i;
                    break;
                }
            }

            FontSizeSlider.Value = prefs.FontSize;
            FontSizeText.Text    = $"{prefs.FontSize} pt";
            TabSizeSlider.Value  = prefs.TabSize;
            TabSizeText.Text     = $"{prefs.TabSize} space" + (prefs.TabSize == 1 ? "" : "s");
            WhitespaceToggle.IsOn = prefs.ShowWhitespace;

            UpdatePreviewFont(prefs);
            UpdateSampleText();
        }
        finally { _suppressEvents = false; }
    }

    private void UpdatePreviewFont(EditorPreferences prefs)
    {
        // CssFamily uses CSS syntax (single quotes, "monospace" keyword) which
        // XAML's FontFamily parser doesn't understand — strip those bits so
        // each preset actually applies in the dialog preview.
        SamplePreview.FontFamily = new FontFamily(CssFontFamilyToXaml(prefs.Font.CssFamily));
        SamplePreview.FontSize   = prefs.FontSize;
    }

    internal static string CssFontFamilyToXaml(string css)
    {
        if (string.IsNullOrWhiteSpace(css)) return "Consolas";
        var parts = css.Split(',');
        var keep  = new List<string>(parts.Length);
        foreach (var p in parts)
        {
            var name = p.Trim().Trim('\'', '"').Trim();
            if (string.IsNullOrEmpty(name)) continue;
            // Drop CSS generic keywords that XAML doesn't recognise.
            if (string.Equals(name, "monospace",  StringComparison.OrdinalIgnoreCase)) continue;
            if (string.Equals(name, "serif",      StringComparison.OrdinalIgnoreCase)) continue;
            if (string.Equals(name, "sans-serif", StringComparison.OrdinalIgnoreCase)) continue;
            if (string.Equals(name, "system-ui",  StringComparison.OrdinalIgnoreCase)) continue;
            keep.Add(name);
        }
        return keep.Count == 0 ? "Consolas" : string.Join(", ", keep);
    }

    // TextBlock has no native "show whitespace" mode, but to make the toggle
    // do something visible in this preview we substitute middle-dots for
    // spaces and arrows for tabs when the setting is on. The actual Monaco
    // editor uses renderWhitespace which is a separate code path.
    private void UpdateSampleText()
    {
        if (string.IsNullOrEmpty(_sampleText)) return;
        var showWs = _service?.Preferences.ShowWhitespace ?? false;
        SamplePreview.Text = showWs
            ? _sampleText.Replace("\t", "→   ").Replace(" ", "·")
            : _sampleText;
    }

    private void OnFontChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressEvents || _service == null) return;
        if (FontCombo.SelectedItem is FontPreset preset)
            _service.SetFontPreset(preset.Id);
    }

    private void OnFontSizeChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_suppressEvents || _service == null) return;
        var size = (int)Math.Round(e.NewValue);
        FontSizeText.Text = $"{size} pt";
        _service.SetFontSize(size);
    }

    private void OnTabSizeChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_suppressEvents || _service == null) return;
        var tab = (int)Math.Round(e.NewValue);
        TabSizeText.Text = $"{tab} space" + (tab == 1 ? "" : "s");
        _service.SetTabSize(tab);
    }

    private void OnWhitespaceToggled(object sender, RoutedEventArgs e)
    {
        if (_suppressEvents || _service == null) return;
        if (sender is ToggleSwitch t) _service.SetShowWhitespace(t.IsOn);
    }
}
