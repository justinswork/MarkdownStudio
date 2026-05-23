using System;
using System.Collections.Generic;
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

public sealed partial class SettingsView : UserControl
{
    public IReadOnlyList<FontPreset> FontPresetsList { get; } = FontPresets.All;

    private const string VirtualHost = "markdownstudio.app";

    private EditorPreferencesService? _service;
    private bool _suppressEvents;
    private bool _webViewInitialized;
    private readonly TaskCompletionSource<bool> _editorReady = new();
    private string _sampleText = "";

    public SettingsView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    public void Attach(EditorPreferencesService service)
    {
        _service = service;
        ApplyPreferencesToUi(service.Preferences);
        service.Changed += OnPrefsChanged;
    }

    private void OnPrefsChanged(EditorPreferences prefs) =>
        DispatcherQueue.TryEnqueue(() => ApplyPreferencesToUi(prefs));

    public async Task LoadSampleAsync()
    {
        _sampleText = await MainWindow.GetBundledSampleAsync();
        if (string.IsNullOrEmpty(_sampleText))
            _sampleText = "(Sample.md not found in the install location.)";
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_webViewInitialized) return;
        _webViewInitialized = true;
        try { await InitializeWebViewAsync(); }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SettingsView WebView init failed: {ex}");
        }
    }

    private async Task InitializeWebViewAsync()
    {
        await EditorWebView.EnsureCoreWebView2Async();

        var webRoot = Path.Combine(AppContext.BaseDirectory, "Web");
        EditorWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
            VirtualHost, webRoot, CoreWebView2HostResourceAccessKind.Allow);
        EditorWebView.CoreWebView2.Settings.AreDevToolsEnabled = false;
        EditorWebView.CoreWebView2.Settings.IsStatusBarEnabled = false;
        EditorWebView.WebMessageReceived += OnWebMessage;

        var prefs = _service?.Preferences ?? new EditorPreferences();
        var theme = (Application.Current?.RequestedTheme ?? ApplicationTheme.Light) == ApplicationTheme.Dark
            ? "ms-midnight" : "ms-daylight";

        var qs = new List<string>
        {
            $"theme={Uri.EscapeDataString(theme)}",
            $"size={prefs.FontSize}",
            $"tab={prefs.TabSize}",
            $"ws={(prefs.ShowWhitespace ? 1 : 0)}",
            $"family={Uri.EscapeDataString(prefs.Font.CssFamily)}",
            "readOnly=1",
        };
        EditorWebView.CoreWebView2.Navigate(
            $"https://{VirtualHost}/editor/index.html?{string.Join("&", qs)}");
    }

    private void OnWebMessage(WebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        try
        {
            using var doc = JsonDocument.Parse(args.WebMessageAsJson);
            if (doc.RootElement.TryGetProperty("type", out var type) && type.GetString() == "ready")
            {
                _editorReady.TrySetResult(true);
                _ = OnEditorReadyAsync();
            }
        }
        catch { /* ignore */ }
    }

    private async Task OnEditorReadyAsync()
    {
        if (string.IsNullOrEmpty(_sampleText)) _sampleText = await MainWindow.GetBundledSampleAsync();
        var encoded = JsonSerializer.Serialize(_sampleText ?? string.Empty);
        await EditorWebView.CoreWebView2.ExecuteScriptAsync($"window.host.setText({encoded});");
        LoadingRing.IsActive = false;
        LoadingRing.Visibility = Visibility.Collapsed;
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

            FontSizeSlider.Value  = prefs.FontSize;
            FontSizeText.Text     = $"{prefs.FontSize} pt";
            TabSizeSlider.Value   = prefs.TabSize;
            TabSizeText.Text      = $"{prefs.TabSize} space" + (prefs.TabSize == 1 ? "" : "s");
            WhitespaceToggle.IsOn = prefs.ShowWhitespace;
        }
        finally { _suppressEvents = false; }

        _ = PushPrefsToEditorAsync(prefs);
    }

    private async Task PushPrefsToEditorAsync(EditorPreferences prefs)
    {
        if (EditorWebView.CoreWebView2 == null || !_editorReady.Task.IsCompleted) return;
        var family = JsonSerializer.Serialize(prefs.Font.CssFamily);
        await EditorWebView.CoreWebView2.ExecuteScriptAsync(
            $"window.host.setFontOptions({family}, {prefs.FontSize}, {prefs.TabSize});");
        await EditorWebView.CoreWebView2.ExecuteScriptAsync(
            $"window.host.setRenderWhitespace({(prefs.ShowWhitespace ? "true" : "false")});");
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
