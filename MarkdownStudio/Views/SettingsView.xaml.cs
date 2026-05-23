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
        SamplePreview.Text = _sampleText;
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
        }
        finally { _suppressEvents = false; }
    }

    private void UpdatePreviewFont(EditorPreferences prefs)
    {
        SamplePreview.FontFamily = new FontFamily(prefs.Font.CssFamily);
        SamplePreview.FontSize   = prefs.FontSize;
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
