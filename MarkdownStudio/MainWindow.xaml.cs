using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using MarkdownStudio.Models;
using MarkdownStudio.Services;
using MarkdownStudio.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace MarkdownStudio;

public sealed partial class MainWindow : Window
{
    private const string WelcomeTabId = "welcome";

    private readonly AppThemeService _appTheme = new();
    private readonly MruService _mru = new();
    private readonly Dictionary<TabViewItem, EditorPaneControl> _panes = new();

    private readonly WelcomeView _welcomeView = new();
    private readonly FileTreeView _fileTreeView = new();
    private readonly OutlineView _outlineView = new();
    private readonly ThemePickerView _themePickerView = new();

    private TabViewItem? _welcomeTab;
    private bool _focusMode;

    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        WireUpViews();

        _appTheme.Changed += t => DispatcherQueue.TryEnqueue(() => ApplyTheme(t));
        ApplyTheme(_appTheme.Selected);

        SetSidebarPane(ActivityPane.None);
        ShowWelcomeTab();
    }

    private void WireUpViews()
    {
        _welcomeView.Attach(_mru);
        _welcomeView.OpenFolderRequested += async () => await OpenFolderInteractiveAsync();
        _welcomeView.OpenFileRequested   += async () => await OpenFileInteractiveAsync();
        _welcomeView.NewFileRequested    += () => AddEditorTab();
        _welcomeView.MruActivated        += OnMruActivated;

        _fileTreeView.OpenFolderRequested += async () => await OpenFolderInteractiveAsync();
        _fileTreeView.FileOpenRequested   += async path => await OpenFileFromPathAsync(path);

        _outlineView.HeadingActivated += node =>
        {
            if (CurrentPane is { } pane) _ = pane.RevealLineAsync(node.LineNumber);
        };

        _themePickerView.ThemeSelected += t => _appTheme.Select(t);
        _themePickerView.SetSelected(_appTheme.Selected);

        ActivityRail.HomeRequested += ShowWelcomeTab;
        ActivityRail.PaneSelected  += SetSidebarPane;

        ModeControl.ModeChanged += OnModeChanged;
    }

    // ---- Theme ----
    private void ApplyTheme(AppTheme theme)
    {
        var effective = theme.FollowsSystem ? _appTheme.EffectiveTheme : theme;

        RootGrid.RequestedTheme = _appTheme.EffectiveElementTheme;

        SetBrushColor("MdsWindowBrush",        effective.WindowFill);
        SetBrushColor("MdsRailBrush",          effective.RailFill);
        SetBrushColor("MdsSidebarBrush",       effective.SidebarFill);
        SetBrushColor("MdsSurfaceBrush",       effective.SurfaceFill);
        SetBrushColor("MdsBorderBrush",        effective.BorderColor);
        SetBrushColor("MdsTextPrimaryBrush",   effective.TextPrimary);
        SetBrushColor("MdsTextSecondaryBrush", effective.TextSecondary);
        SetBrushColor("MdsAccentBrush",        effective.AccentColor);
        var a = effective.AccentColor;
        SetBrushColor("MdsAccentSoftBrush",  Color.FromArgb(31, a.R, a.G, a.B));
        SetBrushColor("MdsAccentHoverBrush", Color.FromArgb(51, a.R, a.G, a.B));

        try
        {
            SystemBackdrop = effective.UseMica ? new MicaBackdrop() : null;
        }
        catch { /* unsupported on some hardware */ }

        ThemeStatusText.Text = theme.DisplayName;
        _themePickerView.SetSelected(theme);

        var monaco  = _appTheme.EffectiveMonacoTheme;
        var preview = _appTheme.EffectivePreviewClass;
        foreach (var pane in _panes.Values)
            _ = pane.ApplyThemeAsync(monaco, preview);
    }

    private static void SetBrushColor(string key, Color color)
    {
        if (Application.Current.Resources[key] is SolidColorBrush b)
            b.Color = color;
    }

    // ---- Sidebar ----
    private void SetSidebarPane(ActivityPane pane)
    {
        UIElement? content = pane switch
        {
            ActivityPane.Files   => _fileTreeView,
            ActivityPane.Outline => _outlineView,
            ActivityPane.Themes  => _themePickerView,
            _                    => null,
        };

        if (content == null)
        {
            SidebarHost.Visibility = Visibility.Collapsed;
            SidebarPresenter.Content = null;
        }
        else
        {
            SidebarPresenter.Content = content;
            SidebarHost.Visibility = Visibility.Visible;
        }
    }

    // ---- Tabs ----
    private TabViewItem GetOrCreateWelcomeTab()
    {
        if (_welcomeTab != null) return _welcomeTab;

        var tab = new TabViewItem
        {
            IconSource = new SymbolIconSource { Symbol = Symbol.Home },
            Header = "Welcome",
            Tag = WelcomeTabId,
        };
        _welcomeTab = tab;
        Tabs.TabItems.Insert(0, tab);
        return tab;
    }

    private TabViewItem AddEditorTab(DocumentTab? doc = null)
    {
        doc ??= new DocumentTab();
        var pane = new EditorPaneControl
        {
            Document = doc,
            MonacoTheme = _appTheme.EffectiveMonacoTheme,
            PreviewTheme = _appTheme.EffectivePreviewClass,
        };
        pane.TextChanged += text => DispatcherQueue.TryEnqueue(() =>
        {
            if (CurrentPane == pane)
                _outlineView.SetNodes(OutlineService.Parse(text));
        });

        var tab = new TabViewItem
        {
            IconSource = new SymbolIconSource { Symbol = Symbol.Document },
            Header = doc.Header,
            Tag = doc,
        };
        _panes[tab] = pane;

        doc.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(DocumentTab.Header))
                DispatcherQueue.TryEnqueue(() => tab.Header = doc.Header);
        };

        Tabs.TabItems.Add(tab);
        Tabs.SelectedItem = tab;
        return tab;
    }

    private void ShowWelcomeTab()
    {
        var t = GetOrCreateWelcomeTab();
        Tabs.SelectedItem = t;
    }

    private EditorPaneControl? CurrentPane =>
        Tabs.SelectedItem is TabViewItem item && _panes.TryGetValue(item, out var p) ? p : null;

    // ---- Mode ----
    private void OnModeChanged(EditorMode mode)
    {
        if (CurrentPane is { } pane)
        {
            _ = pane.SetLayoutAsync(mode switch
            {
                EditorMode.Editor  => EditorLayout.EditorOnly,
                EditorMode.Preview => EditorLayout.PreviewOnly,
                _                  => EditorLayout.Split,
            });
        }
    }

    // ---- File operations ----
    private async Task OpenFileInteractiveAsync()
    {
        var (path, content) = await FileService.OpenAsync(this);
        if (path == null || content == null) return;
        await OpenFileAsync(path, content);
    }

    private async Task OpenFileFromPathAsync(string path)
    {
        try
        {
            var content = await FileService.ReadAllTextAsync(path);
            await OpenFileAsync(path, content);
        }
        catch (Exception ex)
        {
            await ShowMessageAsync("Couldn't open file", ex.Message);
        }
    }

    private async Task OpenFileAsync(string path, string content)
    {
        foreach (var (tab, pane) in _panes)
        {
            if (string.Equals(pane.Document?.FilePath, path, StringComparison.OrdinalIgnoreCase))
            {
                Tabs.SelectedItem = tab;
                _mru.Touch(path, MruKind.File);
                return;
            }
        }

        var doc = new DocumentTab { FilePath = path, Content = content };
        AddEditorTab(doc);
        if (CurrentPane is { } newPane) await newPane.SetContentAsync(content);
        _mru.Touch(path, MruKind.File);
    }

    private async Task OpenFolderInteractiveAsync()
    {
        var path = await FileService.PickFolderAsync(this);
        if (string.IsNullOrEmpty(path)) return;
        OpenFolder(path);
    }

    private void OpenFolder(string path)
    {
        _fileTreeView.FolderPath = path;
        _mru.Touch(path, MruKind.Folder);
        ActivityRail.CurrentPane = ActivityPane.Files;
        SetSidebarPane(ActivityPane.Files);
        StatusText.Text = $"Folder: {path}";
    }

    private void OnMruActivated(MruEntry entry)
    {
        if (entry.Kind == MruKind.Folder)
        {
            if (Directory.Exists(entry.Path)) OpenFolder(entry.Path);
            else _mru.Remove(entry.Path);
        }
        else
        {
            if (File.Exists(entry.Path)) _ = OpenFileFromPathAsync(entry.Path);
            else _mru.Remove(entry.Path);
        }
    }

    // ---- Menu / button handlers ----
    private void OnNew(object sender, RoutedEventArgs e) => AddEditorTab();
    private async void OnOpenFile(object sender, RoutedEventArgs e)   => await OpenFileInteractiveAsync();
    private async void OnOpenFolder(object sender, RoutedEventArgs e) => await OpenFolderInteractiveAsync();
    private async void OnSave(object sender, RoutedEventArgs e)       => await SaveCurrentAsync(saveAs: false);
    private async void OnSaveAs(object sender, RoutedEventArgs e)     => await SaveCurrentAsync(saveAs: true);
    private void OnShowWelcome(object sender, RoutedEventArgs e)      => ShowWelcomeTab();
    private void OnCloseTab(object sender, RoutedEventArgs e)
    {
        if (Tabs.SelectedItem is TabViewItem item) CloseTab(item);
    }
    private void OnExit(object sender, RoutedEventArgs e) => Close();

    private void OnSplitMode(object sender, RoutedEventArgs e)
    { ModeControl.Mode = EditorMode.Split;   OnModeChanged(EditorMode.Split); }
    private void OnEditorMode(object sender, RoutedEventArgs e)
    { ModeControl.Mode = EditorMode.Editor;  OnModeChanged(EditorMode.Editor); }
    private void OnPreviewMode(object sender, RoutedEventArgs e)
    { ModeControl.Mode = EditorMode.Preview; OnModeChanged(EditorMode.Preview); }

    private async void OnToggleWordWrap(object sender, RoutedEventArgs e) =>
        await (CurrentPane?.ToggleWordWrapAsync() ?? Task.CompletedTask);

    private void OnToggleFocus(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    { ToggleFocusMode(); args.Handled = true; }

    private void OnToggleFocus(object sender, RoutedEventArgs e)      => ToggleFocusMode();
    private void OnToggleFocusMenu(object sender, RoutedEventArgs e)  => ToggleFocusMode();

    private void ToggleFocusMode()
    {
        _focusMode = !_focusMode;
        var hidden = _focusMode ? Visibility.Collapsed : Visibility.Visible;
        AppTitleBar.Visibility = hidden;
        RailColumn.Width    = _focusMode ? new GridLength(0) : GridLength.Auto;
        SidebarColumn.Width = _focusMode ? new GridLength(0) : GridLength.Auto;
        SidebarHost.Visibility = _focusMode ? Visibility.Collapsed : SidebarHost.Visibility;
        TopToolbar.Visibility = hidden;
        Tabs.Visibility = hidden;
        StatusBar.Visibility = hidden;
    }

    // Accelerators
    private void OnNewAccel(KeyboardAccelerator s, KeyboardAcceleratorInvokedEventArgs a)
    { AddEditorTab(); a.Handled = true; }

    private async void OnOpenAccel(KeyboardAccelerator s, KeyboardAcceleratorInvokedEventArgs a)
    { a.Handled = true; await OpenFileInteractiveAsync(); }

    private async void OnOpenFolderAccel(KeyboardAccelerator s, KeyboardAcceleratorInvokedEventArgs a)
    { a.Handled = true; await OpenFolderInteractiveAsync(); }

    private async void OnSaveAccel(KeyboardAccelerator s, KeyboardAcceleratorInvokedEventArgs a)
    { a.Handled = true; await SaveCurrentAsync(false); }

    private async void OnSaveAsAccel(KeyboardAccelerator s, KeyboardAcceleratorInvokedEventArgs a)
    { a.Handled = true; await SaveCurrentAsync(true); }

    private void OnCloseTabAccel(KeyboardAccelerator s, KeyboardAcceleratorInvokedEventArgs a)
    { a.Handled = true; if (Tabs.SelectedItem is TabViewItem item) CloseTab(item); }

    private async Task SaveCurrentAsync(bool saveAs)
    {
        var pane = CurrentPane;
        var doc = pane?.Document;
        if (pane == null || doc == null) return;

        var text = await pane.GetContentAsync();
        doc.Content = text;

        var path = doc.FilePath;
        if (string.IsNullOrEmpty(path) || saveAs)
        {
            path = await FileService.PickSavePathAsync(this, doc.DisplayName);
            if (string.IsNullOrEmpty(path)) return;
            doc.FilePath = path;
        }

        await FileService.SaveAsync(path, text);
        doc.IsDirty = false;
        _mru.Touch(path, MruKind.File);
        StatusText.Text = $"Saved {Path.GetFileName(path)}";
    }

    private async void OnAbout(object sender, RoutedEventArgs e) =>
        await ShowMessageAsync("Markdown Studio",
            "A premium native markdown editor for Windows.\nBuilt with WinUI 3 and .NET 10.");

    private async Task ShowMessageAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = RootGrid.XamlRoot,
        };
        await dialog.ShowAsync();
    }

    // ---- Tab events ----
    private void Tabs_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args) =>
        CloseTab(args.Tab);

    private void CloseTab(TabViewItem item)
    {
        if (item == _welcomeTab) _welcomeTab = null;
        else _panes.Remove(item);

        Tabs.TabItems.Remove(item);
        if (Tabs.TabItems.Count == 0) ShowWelcomeTab();
    }

    private void Tabs_AddTabButtonClick(TabView sender, object args) => AddEditorTab();

    private void Tabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Tabs.SelectedItem is not TabViewItem item)
        {
            ActiveContent.Content = null;
            DocInfoText.Text = string.Empty;
            return;
        }

        if (item == _welcomeTab)
        {
            ActiveContent.Content = _welcomeView;
            DocInfoText.Text = "Welcome";
            TopToolbar.Visibility = Visibility.Collapsed;
            return;
        }

        if (!_focusMode) TopToolbar.Visibility = Visibility.Visible;

        if (_panes.TryGetValue(item, out var pane))
        {
            ActiveContent.Content = pane;
            var doc = pane.Document;
            DocInfoText.Text = doc?.FilePath != null
                ? $"Markdown · UTF-8 · {Path.GetFileName(doc.FilePath)}"
                : "Markdown · UTF-8";
            _outlineView.SetNodes(OutlineService.Parse(pane.Document?.Content ?? string.Empty));
        }
    }
}
