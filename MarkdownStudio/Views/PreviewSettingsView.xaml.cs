using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using MarkdownStudio.Models;
using MarkdownStudio.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.Web.WebView2.Core;

namespace MarkdownStudio.Views;

public sealed partial class PreviewSettingsView : UserControl
{
    public IReadOnlyList<FontPreset>         FontList    { get; } = PreviewFontPresets.All;
    public IReadOnlyList<WidthPreset>        WidthList   { get; } = WidthPresets.All;
    public IReadOnlyList<HeadingStylePreset> HeadingList { get; } = HeadingStylePresets.All;

    private const string VirtualHost = "markdownstudio.app";

    private EditorPreferencesService? _service;
    private bool _suppressEvents;
    private bool _webViewInitialized;
    private readonly TaskCompletionSource<bool> _previewReady = new();
    private string _sampleText = "";
    private string _initialPreviewTheme = "theme-daylight";

    // Set by the host before showing so the embedded preview picks up the
    // user's actual app theme rather than the system theme.
    public string InitialPreviewTheme
    {
        get => _initialPreviewTheme;
        set => _initialPreviewTheme = string.IsNullOrEmpty(value) ? "theme-daylight" : value;
    }

    public PreviewSettingsView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    public void Attach(EditorPreferencesService service)
    {
        _service = service;
        ApplyPreferencesToUi(service.Preferences);
        service.Changed += p => DispatcherQueue.TryEnqueue(() => ApplyPreferencesToUi(p));
    }

    public async Task LoadSampleAsync()
    {
        _sampleText = await MainWindow.GetBundledSampleAsync();
        if (string.IsNullOrEmpty(_sampleText))
            _sampleText = "*(Sample.md not found in the install location.)*";
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_webViewInitialized) return;
        _webViewInitialized = true;
        try { await InitializeWebViewAsync(); }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"PreviewSettingsView WebView init failed: {ex}");
        }
    }

    private async Task InitializeWebViewAsync()
    {
        await PreviewWebView.EnsureCoreWebView2Async();

        var webRoot = Path.Combine(AppContext.BaseDirectory, "Web");
        PreviewWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
            VirtualHost, webRoot, CoreWebView2HostResourceAccessKind.Allow);
        PreviewWebView.CoreWebView2.Settings.AreDevToolsEnabled = false;
        PreviewWebView.CoreWebView2.Settings.IsStatusBarEnabled = false;
        PreviewWebView.WebMessageReceived += OnWebMessage;

        var prefs = _service?.Preferences ?? new EditorPreferences();
        var qs = new List<string>
        {
            $"theme={Uri.EscapeDataString(_initialPreviewTheme)}",
            $"pfSize={prefs.PreviewFontSize}",
            $"pfLh={prefs.PreviewLineHeight.ToString(CultureInfo.InvariantCulture)}",
            $"pfWidth={Uri.EscapeDataString(prefs.PreviewWidth.CssMaxWidth)}",
            $"pfHead={Uri.EscapeDataString(prefs.PreviewHeadingStyle.CssClass)}",
            $"pfFamily={Uri.EscapeDataString(prefs.PreviewFont.CssFamily)}",
        };
        PreviewWebView.CoreWebView2.Navigate(
            $"https://{VirtualHost}/preview/index.html?{string.Join("&", qs)}");
    }

    private void OnWebMessage(WebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        try
        {
            using var doc = JsonDocument.Parse(args.WebMessageAsJson);
            if (doc.RootElement.TryGetProperty("type", out var type) && type.GetString() == "ready")
            {
                _previewReady.TrySetResult(true);
                _ = OnPreviewReadyAsync();
            }
        }
        catch { /* ignore */ }
    }

    private async Task OnPreviewReadyAsync()
    {
        if (string.IsNullOrEmpty(_sampleText)) _sampleText = await MainWindow.GetBundledSampleAsync();
        var encoded = JsonSerializer.Serialize(_sampleText ?? string.Empty);
        await PreviewWebView.CoreWebView2.ExecuteScriptAsync($"window.host.render({encoded});");
        LoadingRing.IsActive = false;
        LoadingRing.Visibility = Visibility.Collapsed;
    }

    private void ApplyPreferencesToUi(EditorPreferences prefs)
    {
        _suppressEvents = true;
        try
        {
            SelectByIdInto(FontCombo,    FontList,    prefs.PreviewFontPresetId, p => p.Id);
            SelectByIdInto(WidthCombo,   WidthList,   prefs.PreviewWidthId,       p => p.Id);
            SelectByIdInto(HeadingCombo, HeadingList, prefs.PreviewHeadingId,     p => p.Id);

            FontSizeSlider.Value   = prefs.PreviewFontSize;
            FontSizeText.Text      = $"{prefs.PreviewFontSize} pt";
            LineHeightSlider.Value = prefs.PreviewLineHeight;
            LineHeightText.Text    = prefs.PreviewLineHeight.ToString("0.0", CultureInfo.InvariantCulture);
        }
        finally { _suppressEvents = false; }

        _ = PushPrefsToPreviewAsync(prefs);
    }

    private async Task PushPrefsToPreviewAsync(EditorPreferences prefs)
    {
        if (PreviewWebView.CoreWebView2 == null || !_previewReady.Task.IsCompleted) return;
        var payload = JsonSerializer.Serialize(new
        {
            fontFamily   = prefs.PreviewFont.CssFamily,
            fontSize     = prefs.PreviewFontSize,
            lineHeight   = prefs.PreviewLineHeight,
            width        = prefs.PreviewWidth.CssMaxWidth,
            headingClass = prefs.PreviewHeadingStyle.CssClass,
        });
        await PreviewWebView.CoreWebView2.ExecuteScriptAsync(
            $"window.host.setPreviewOptions({payload});");
    }

    // Called by SettingsDialog when the user picks a different app theme so
    // the embedded preview re-skins live.
    public async Task SetThemeAsync(string previewClassName)
    {
        _initialPreviewTheme = previewClassName;
        if (PreviewWebView.CoreWebView2 == null || !_previewReady.Task.IsCompleted) return;
        var encoded = JsonSerializer.Serialize(previewClassName);
        await PreviewWebView.CoreWebView2.ExecuteScriptAsync($"window.host.setTheme({encoded});");
    }

    private static void SelectByIdInto<T>(
        ComboBox combo, IReadOnlyList<T> list, string id, Func<T, string> idOf)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (idOf(list[i]) == id) { combo.SelectedIndex = i; return; }
        }
    }

    private void OnFontChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressEvents || _service == null) return;
        if (FontCombo.SelectedItem is FontPreset p) _service.SetPreviewFontPreset(p.Id);
    }

    private void OnFontSizeChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_suppressEvents || _service == null) return;
        var size = (int)Math.Round(e.NewValue);
        FontSizeText.Text = $"{size} pt";
        _service.SetPreviewFontSize(size);
    }

    private void OnLineHeightChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_suppressEvents || _service == null) return;
        var lh = Math.Round(e.NewValue * 100) / 100.0;
        LineHeightText.Text = lh.ToString("0.00", CultureInfo.InvariantCulture);
        _service.SetPreviewLineHeight(lh);
    }

    private void OnWidthChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressEvents || _service == null) return;
        if (WidthCombo.SelectedItem is WidthPreset p) _service.SetPreviewWidth(p.Id);
    }

    private void OnHeadingChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressEvents || _service == null) return;
        if (HeadingCombo.SelectedItem is HeadingStylePreset p) _service.SetPreviewHeadingStyle(p.Id);
    }
}
