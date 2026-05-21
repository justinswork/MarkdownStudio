using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using MarkdownStudio.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;

namespace MarkdownStudio.Views;

public enum EditorLayout
{
    Split,
    EditorOnly,
    PreviewOnly,
}

public sealed partial class EditorPaneControl : UserControl
{
    private const string VirtualHost = "markdownstudio.app";

    private TaskCompletionSource<bool> _editorReady = new();
    private TaskCompletionSource<bool> _previewReady = new();
    private bool _initialized;
    private bool _wordWrap = true;
    private string _initialContent = string.Empty;

    public DocumentTab? Document { get; set; }
    public string MonacoTheme { get; set; } = "vs";
    public string PreviewTheme { get; set; } = "light";

    public EditorPaneControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private static void LogSize(string tag, EditorPaneControl pane)
    {
        try
        {
            var line = $"{DateTime.Now:HH:mm:ss.fff} [{tag}] UC {pane.ActualWidth:F0}x{pane.ActualHeight:F0}" +
                       $"  Grid {pane.LayoutGrid.ActualWidth:F0}x{pane.LayoutGrid.ActualHeight:F0}" +
                       $"  Editor {pane.EditorView.ActualWidth:F0}x{pane.EditorView.ActualHeight:F0}" +
                       $"  Preview {pane.PreviewView.ActualWidth:F0}x{pane.PreviewView.ActualHeight:F0}{Environment.NewLine}";
            System.IO.File.AppendAllText(@"C:\Users\perso\source\MarkdownStudio\layout.log", line);
        }
        catch { }
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        LogSize("Loaded", this);
        SizeChanged += (_, _) => LogSize("SizeChanged", this);
        if (_initialized) return;
        _initialized = true;
        await InitializeWebViewsAsync();
        LogSize("Post-Init", this);
    }

    private async Task InitializeWebViewsAsync()
    {
        EditorView.DefaultBackgroundColor  = Windows.UI.Color.FromArgb(255, 200, 0, 0);
        PreviewView.DefaultBackgroundColor = Windows.UI.Color.FromArgb(255, 0, 0, 200);

        await EditorView.EnsureCoreWebView2Async();
        await PreviewView.EnsureCoreWebView2Async();

        var webRoot = Path.Combine(AppContext.BaseDirectory, "Web");

        EditorView.CoreWebView2.SetVirtualHostNameToFolderMapping(
            VirtualHost, webRoot, CoreWebView2HostResourceAccessKind.Allow);
        PreviewView.CoreWebView2.SetVirtualHostNameToFolderMapping(
            VirtualHost, webRoot, CoreWebView2HostResourceAccessKind.Allow);

        EditorView.CoreWebView2.Settings.AreDevToolsEnabled = true;
        PreviewView.CoreWebView2.Settings.AreDevToolsEnabled = true;
        EditorView.CoreWebView2.Settings.IsStatusBarEnabled = false;
        PreviewView.CoreWebView2.Settings.IsStatusBarEnabled = false;

        EditorView.WebMessageReceived += OnEditorWebMessage;
        PreviewView.WebMessageReceived += OnPreviewWebMessage;

        var initialQuery = $"?theme={Uri.EscapeDataString(MonacoTheme)}";
        EditorView.CoreWebView2.Navigate($"https://{VirtualHost}/editor/index.html{initialQuery}");
        PreviewView.CoreWebView2.Navigate(
            $"https://{VirtualHost}/preview/index.html?theme={Uri.EscapeDataString(PreviewTheme)}");

        if (Document != null && !string.IsNullOrEmpty(Document.Content))
            _initialContent = Document.Content;
    }

    private void OnEditorWebMessage(WebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        try
        {
            using var doc = JsonDocument.Parse(args.WebMessageAsJson);
            var root = doc.RootElement;
            var type = root.GetProperty("type").GetString();
            switch (type)
            {
                case "ready":
                    _ = OnEditorReadyAsync();
                    break;
                case "changed":
                    var text = root.GetProperty("text").GetString() ?? string.Empty;
                    if (Document != null)
                    {
                        Document.Content = text;
                        Document.IsDirty = true;
                    }
                    _ = PushToPreviewAsync(text);
                    break;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Editor message error: {ex}");
        }
    }

    private void OnPreviewWebMessage(WebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        try
        {
            using var doc = JsonDocument.Parse(args.WebMessageAsJson);
            var root = doc.RootElement;
            var type = root.GetProperty("type").GetString();
            switch (type)
            {
                case "ready":
                    _previewReady.TrySetResult(true);
                    if (!string.IsNullOrEmpty(_initialContent))
                        _ = PushToPreviewAsync(_initialContent);
                    break;
                case "linkClicked":
                    var url = root.GetProperty("url").GetString();
                    if (!string.IsNullOrWhiteSpace(url))
                        _ = Windows.System.Launcher.LaunchUriAsync(new Uri(url));
                    break;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Preview message error: {ex}");
        }
    }

    private async Task OnEditorReadyAsync()
    {
        _editorReady.TrySetResult(true);
        if (!string.IsNullOrEmpty(_initialContent))
        {
            var encoded = JsonSerializer.Serialize(_initialContent);
            await EditorView.CoreWebView2.ExecuteScriptAsync($"window.host.setText({encoded});");
        }
    }

    private async Task PushToPreviewAsync(string markdown)
    {
        await _previewReady.Task;
        var encoded = JsonSerializer.Serialize(markdown);
        await PreviewView.CoreWebView2.ExecuteScriptAsync($"window.host.render({encoded});");
    }

    public async Task SetContentAsync(string content)
    {
        _initialContent = content;
        if (_editorReady.Task.IsCompleted && EditorView.CoreWebView2 != null)
        {
            var encoded = JsonSerializer.Serialize(content);
            await EditorView.CoreWebView2.ExecuteScriptAsync($"window.host.setText({encoded});");
        }
        if (_previewReady.Task.IsCompleted && PreviewView.CoreWebView2 != null)
        {
            await PushToPreviewAsync(content);
        }
        if (Document != null)
        {
            Document.Content = content;
            Document.IsDirty = false;
        }
    }

    public async Task<string> GetContentAsync()
    {
        if (EditorView.CoreWebView2 == null) return Document?.Content ?? string.Empty;
        await _editorReady.Task;
        var raw = await EditorView.CoreWebView2.ExecuteScriptAsync("window.host.getText();");
        return JsonSerializer.Deserialize<string>(raw) ?? string.Empty;
    }

    public async Task ApplyThemeAsync(string monacoTheme, string previewTheme)
    {
        MonacoTheme = monacoTheme;
        PreviewTheme = previewTheme;
        if (EditorView.CoreWebView2 != null && _editorReady.Task.IsCompleted)
        {
            var encoded = JsonSerializer.Serialize(monacoTheme);
            await EditorView.CoreWebView2.ExecuteScriptAsync($"window.host.setTheme({encoded});");
        }
        if (PreviewView.CoreWebView2 != null && _previewReady.Task.IsCompleted)
        {
            var encoded = JsonSerializer.Serialize(previewTheme);
            await PreviewView.CoreWebView2.ExecuteScriptAsync($"window.host.setTheme({encoded});");
        }
    }

    public Task ToggleWordWrapAsync()
    {
        _wordWrap = !_wordWrap;
        if (EditorView.CoreWebView2 == null) return Task.CompletedTask;
        return EditorView.CoreWebView2.ExecuteScriptAsync(
            $"window.host.setWordWrap({(_wordWrap ? "true" : "false")});").AsTask();
    }

    public Task SetLayoutAsync(EditorLayout layout)
    {
        switch (layout)
        {
            case EditorLayout.Split:
                EditorColumn.Width = new GridLength(1, GridUnitType.Star);
                PreviewColumn.Width = new GridLength(1, GridUnitType.Star);
                break;
            case EditorLayout.EditorOnly:
                EditorColumn.Width = new GridLength(1, GridUnitType.Star);
                PreviewColumn.Width = new GridLength(0);
                break;
            case EditorLayout.PreviewOnly:
                EditorColumn.Width = new GridLength(0);
                PreviewColumn.Width = new GridLength(1, GridUnitType.Star);
                break;
        }
        return Task.CompletedTask;
    }
}
