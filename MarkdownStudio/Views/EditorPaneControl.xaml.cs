using System;
using System.Collections.Generic;
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
    public string MonacoTheme { get; set; } = "ms-daylight";
    public string PreviewTheme { get; set; } = "theme-daylight";
    public string InitialFontFamily    { get; set; } = "";
    public int    InitialFontSize      { get; set; } = 14;
    public int    InitialTabSize       { get; set; } = 2;
    public bool   InitialShowWhitespace { get; set; }

    // Preview-side typography prefs (seeded into the preview page query string).
    public string InitialPreviewFontFamily { get; set; } = "";
    public int    InitialPreviewFontSize   { get; set; } = 16;
    public double InitialPreviewLineHeight { get; set; } = 1.7;
    public string InitialPreviewWidthCss   { get; set; } = "760px";
    public string InitialPreviewHeadingClass { get; set; } = "headings-standard";

    public event Action<string>? TextChanged;
    public event Action?         FocusToggleRequested;

    public EditorPaneControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_initialized) return;
        _initialized = true;
        await InitializeWebViewsAsync();
    }

    private async Task InitializeWebViewsAsync()
    {
        await EditorView.EnsureCoreWebView2Async();
        await PreviewView.EnsureCoreWebView2Async();

        var webRoot = Path.Combine(AppContext.BaseDirectory, "Web");

        EditorView.CoreWebView2.SetVirtualHostNameToFolderMapping(
            VirtualHost, webRoot, CoreWebView2HostResourceAccessKind.Allow);
        PreviewView.CoreWebView2.SetVirtualHostNameToFolderMapping(
            VirtualHost, webRoot, CoreWebView2HostResourceAccessKind.Allow);

#if DEBUG
        EditorView.CoreWebView2.Settings.AreDevToolsEnabled = true;
        PreviewView.CoreWebView2.Settings.AreDevToolsEnabled = true;
#else
        EditorView.CoreWebView2.Settings.AreDevToolsEnabled = false;
        PreviewView.CoreWebView2.Settings.AreDevToolsEnabled = false;
#endif
        EditorView.CoreWebView2.Settings.IsStatusBarEnabled = false;
        PreviewView.CoreWebView2.Settings.IsStatusBarEnabled = false;
        // Filter Chromium's default right-click menu down to just Copy and
        // Select All. Strips noise like "Copy link to highlight", "Search the
        // web", and Inspect that don't make sense in a markdown editor.
        // Monaco renders its own context menu in the editor area, so this
        // filter only kicks in for the scrollbar / empty regions there.
        EditorView.CoreWebView2.ContextMenuRequested  += OnContextMenuRequested;
        PreviewView.CoreWebView2.ContextMenuRequested += OnContextMenuRequested;

        EditorView.WebMessageReceived += OnEditorWebMessage;
        PreviewView.WebMessageReceived += OnPreviewWebMessage;

        var editorQueryParts = new System.Collections.Generic.List<string>
        {
            $"theme={Uri.EscapeDataString(MonacoTheme)}",
            $"size={InitialFontSize}",
            $"tab={InitialTabSize}",
            $"ws={(InitialShowWhitespace ? 1 : 0)}",
        };
        if (!string.IsNullOrEmpty(InitialFontFamily))
            editorQueryParts.Add($"family={Uri.EscapeDataString(InitialFontFamily)}");
        var editorQuery  = "?" + string.Join("&", editorQueryParts);

        var previewQueryParts = new System.Collections.Generic.List<string>
        {
            $"theme={Uri.EscapeDataString(PreviewTheme)}",
            $"pfSize={InitialPreviewFontSize}",
            $"pfLh={InitialPreviewLineHeight.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
            $"pfWidth={Uri.EscapeDataString(InitialPreviewWidthCss)}",
            $"pfHead={Uri.EscapeDataString(InitialPreviewHeadingClass)}",
        };
        if (!string.IsNullOrEmpty(InitialPreviewFontFamily))
            previewQueryParts.Add($"pfFamily={Uri.EscapeDataString(InitialPreviewFontFamily)}");
        var previewQuery = "?" + string.Join("&", previewQueryParts);

        EditorView.CoreWebView2.Navigate($"https://{VirtualHost}/editor/index.html{editorQuery}");
        PreviewView.CoreWebView2.Navigate($"https://{VirtualHost}/preview/index.html{previewQuery}");

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
                    TextChanged?.Invoke(text);
                    _ = PushToPreviewAsync(text);
                    break;
                case "scrolled":
                    var editorLine = root.GetProperty("line").GetInt32();
                    _ = SyncPreviewScrollAsync(editorLine);
                    break;
                case "toggleFocus":
                    FocusToggleRequested?.Invoke();
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
                case "scrolled":
                    var previewLine = root.GetProperty("line").GetInt32();
                    _ = SyncEditorScrollAsync(previewLine);
                    break;
                case "toggleFocus":
                    FocusToggleRequested?.Invoke();
                    break;
                case "xrayApply":
                    var xrayStart = root.GetProperty("startLine").GetInt32();
                    var xrayEnd   = root.GetProperty("endLine").GetInt32();
                    var xrayText  = root.GetProperty("text").GetString() ?? string.Empty;
                    _ = ApplyXrayEditAsync(xrayStart, xrayEnd, xrayText);
                    break;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Preview message error: {ex}");
        }
    }

    private async Task ApplyXrayEditAsync(int startLine, int endLine, string newText)
    {
        if (EditorView.CoreWebView2 == null) return;
        await _editorReady.Task;
        var encoded = JsonSerializer.Serialize(newText);
        await EditorView.CoreWebView2.ExecuteScriptAsync(
            $"window.host.replaceLines({startLine}, {endLine}, {encoded});");
    }

    private static readonly HashSet<string> _allowedContextMenuItems =
        new(StringComparer.OrdinalIgnoreCase) { "copy", "selectAll" };

    private static void OnContextMenuRequested(
        CoreWebView2 sender, CoreWebView2ContextMenuRequestedEventArgs args)
    {
        var items = args.MenuItems;
        for (int i = items.Count - 1; i >= 0; i--)
        {
            if (!_allowedContextMenuItems.Contains(items[i].Name))
                items.RemoveAt(i);
        }
        // Collapse runs of adjacent separators and trim leading/trailing ones.
        for (int i = items.Count - 1; i > 0; i--)
        {
            if (items[i].Kind == CoreWebView2ContextMenuItemKind.Separator &&
                items[i - 1].Kind == CoreWebView2ContextMenuItemKind.Separator)
                items.RemoveAt(i);
        }
        while (items.Count > 0 && items[0].Kind == CoreWebView2ContextMenuItemKind.Separator)
            items.RemoveAt(0);
        while (items.Count > 0 && items[^1].Kind == CoreWebView2ContextMenuItemKind.Separator)
            items.RemoveAt(items.Count - 1);
    }

    private async Task SyncPreviewScrollAsync(int line)
    {
        if (PreviewView.CoreWebView2 == null || !_previewReady.Task.IsCompleted) return;
        await PreviewView.CoreWebView2.ExecuteScriptAsync($"window.host.scrollToLine({line});");
    }

    private async Task SyncEditorScrollAsync(int line)
    {
        if (EditorView.CoreWebView2 == null || !_editorReady.Task.IsCompleted) return;
        await EditorView.CoreWebView2.ExecuteScriptAsync($"window.host.scrollToLine({line});");
    }

    private async Task OnEditorReadyAsync()
    {
        _editorReady.TrySetResult(true);
        if (!string.IsNullOrEmpty(_initialContent))
        {
            var encoded = JsonSerializer.Serialize(_initialContent);
            await EditorView.CoreWebView2.ExecuteScriptAsync($"window.host.setText({encoded});");
            TextChanged?.Invoke(_initialContent);
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
        await _editorReady.Task;
        if (EditorView.CoreWebView2 != null)
        {
            var encoded = JsonSerializer.Serialize(content);
            await EditorView.CoreWebView2.ExecuteScriptAsync($"window.host.setText({encoded});");
        }
        if (PreviewView.CoreWebView2 != null)
        {
            await PushToPreviewAsync(content);
        }
        if (Document != null)
        {
            Document.Content = content;
            Document.IsDirty = false;
        }
        TextChanged?.Invoke(content);
    }

    public async Task<string> GetContentAsync()
    {
        if (EditorView.CoreWebView2 == null) return Document?.Content ?? string.Empty;
        await _editorReady.Task;
        var raw = await EditorView.CoreWebView2.ExecuteScriptAsync("window.host.getText();");
        return JsonSerializer.Deserialize<string>(raw) ?? string.Empty;
    }

    public async Task RevealLineAsync(int lineNumber, string? query = null)
    {
        if (EditorView.CoreWebView2 == null) return;
        await _editorReady.Task;
        var queryArg = string.IsNullOrEmpty(query) ? "null" : JsonSerializer.Serialize(query);
        await EditorView.CoreWebView2.ExecuteScriptAsync(
            $"window.host.revealLine({lineNumber}, {queryArg});");
        // Drive the preview to the same source line so outline / search-hit
        // navigation moves both panes. The editor's own scroll event is
        // suppressed for SYNC_LOCK_MS by window.host.revealLine, so this
        // explicit command isn't overwritten by the natural scroll sync.
        if (PreviewView.CoreWebView2 != null && _previewReady.Task.IsCompleted)
        {
            await PreviewView.CoreWebView2.ExecuteScriptAsync(
                $"window.host.scrollToLine({lineNumber});");
        }
    }

    public async Task OpenFindAsync()
    {
        if (EditorView.CoreWebView2 == null) return;
        await _editorReady.Task;
        await EditorView.CoreWebView2.ExecuteScriptAsync("window.host.openFind();");
        EditorView.Focus(FocusState.Programmatic);
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

    public async Task SetEditorFontAsync(string fontFamily, int fontSize, int tabSize)
    {
        InitialFontFamily = fontFamily;
        InitialFontSize   = fontSize;
        InitialTabSize    = tabSize;
        if (EditorView.CoreWebView2 == null || !_editorReady.Task.IsCompleted) return;
        var family = JsonSerializer.Serialize(fontFamily);
        await EditorView.CoreWebView2.ExecuteScriptAsync(
            $"window.host.setFontOptions({family}, {fontSize}, {tabSize});");
    }

    public async Task SetPreviewOptionsAsync(
        string fontFamily, int fontSize, double lineHeight, string widthCss, string headingClass)
    {
        InitialPreviewFontFamily   = fontFamily;
        InitialPreviewFontSize     = fontSize;
        InitialPreviewLineHeight   = lineHeight;
        InitialPreviewWidthCss     = widthCss;
        InitialPreviewHeadingClass = headingClass;
        if (PreviewView.CoreWebView2 == null || !_previewReady.Task.IsCompleted) return;

        var payload = JsonSerializer.Serialize(new
        {
            fontFamily   = fontFamily,
            fontSize     = fontSize,
            lineHeight   = lineHeight,
            width        = widthCss,
            headingClass = headingClass,
        });
        await PreviewView.CoreWebView2.ExecuteScriptAsync(
            $"window.host.setPreviewOptions({payload});");
    }

    public async Task SetRenderWhitespaceAsync(bool show)
    {
        if (EditorView.CoreWebView2 == null || !_editorReady.Task.IsCompleted) return;
        await EditorView.CoreWebView2.ExecuteScriptAsync(
            $"window.host.setRenderWhitespace({(show ? "true" : "false")});");
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
