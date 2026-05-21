using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MarkdownStudio.Models;
using MarkdownStudio.Services;
using MarkdownStudio.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace MarkdownStudio;

public sealed partial class MainWindow : Window
{
    private readonly ThemeService _theme = new();
    private readonly Dictionary<TabViewItem, EditorPaneControl> _panes = new();

    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        _theme.ThemeChanged += t => DispatcherQueue.TryEnqueue(() => ApplyTheme(t));
        ApplyTheme(_theme.CurrentTheme);

        AddNewTab();
    }

    private void ApplyTheme(ElementTheme theme)
    {
        if (RootGrid is FrameworkElement fe)
        {
            fe.RequestedTheme = theme;
        }
        foreach (var pane in _panes.Values)
        {
            _ = pane.ApplyThemeAsync(_theme.MonacoThemeName, _theme.PreviewThemeName);
        }
    }

    private TabViewItem AddNewTab(DocumentTab? doc = null)
    {
        doc ??= new DocumentTab();
        var pane = new EditorPaneControl
        {
            Document = doc,
            MonacoTheme = _theme.MonacoThemeName,
            PreviewTheme = _theme.PreviewThemeName,
        };

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
            {
                DispatcherQueue.TryEnqueue(() => tab.Header = doc.Header);
            }
        };

        Tabs.TabItems.Add(tab);
        Tabs.SelectedItem = tab;
        ActiveContent.Content = pane;
        return tab;
    }

    private EditorPaneControl? CurrentPane =>
        Tabs.SelectedItem is TabViewItem item && _panes.TryGetValue(item, out var p) ? p : null;

    private DocumentTab? CurrentDocument => CurrentPane?.Document;

    private async void OnNew(object sender, RoutedEventArgs e) =>
        await Task.FromResult(AddNewTab());

    private async void OnOpen(object sender, RoutedEventArgs e)
    {
        var (path, content) = await FileService.OpenAsync(this);
        if (path == null || content == null) return;

        var doc = new DocumentTab { FilePath = path, Content = content };
        var tab = AddNewTab(doc);
        if (_panes.TryGetValue(tab, out var pane))
        {
            await pane.SetContentAsync(content);
        }
    }

    private async void OnSave(object sender, RoutedEventArgs e) => await SaveCurrentAsync(saveAs: false);
    private async void OnSaveAs(object sender, RoutedEventArgs e) => await SaveCurrentAsync(saveAs: true);

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
        StatusText.Text = $"Saved {System.IO.Path.GetFileName(path)}";
    }

    private void OnCloseTab(object sender, RoutedEventArgs e)
    {
        if (Tabs.SelectedItem is TabViewItem item)
            CloseTab(item);
    }

    private void OnExit(object sender, RoutedEventArgs e) => Close();

    private async void OnSplitView(object sender, RoutedEventArgs e) =>
        await (CurrentPane?.SetLayoutAsync(EditorLayout.Split) ?? Task.CompletedTask);

    private async void OnEditorOnly(object sender, RoutedEventArgs e) =>
        await (CurrentPane?.SetLayoutAsync(EditorLayout.EditorOnly) ?? Task.CompletedTask);

    private async void OnPreviewOnly(object sender, RoutedEventArgs e) =>
        await (CurrentPane?.SetLayoutAsync(EditorLayout.PreviewOnly) ?? Task.CompletedTask);

    private async void OnToggleWordWrap(object sender, RoutedEventArgs e) =>
        await (CurrentPane?.ToggleWordWrapAsync() ?? Task.CompletedTask);

    private async void OnAbout(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Markdown Studio",
            Content = "A native Windows markdown editor.\nBuilt with WinUI 3 and .NET 10.",
            CloseButtonText = "OK",
            XamlRoot = RootGrid.XamlRoot,
        };
        await dialog.ShowAsync();
    }

    private void Tabs_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
    {
        CloseTab(args.Tab);
    }

    private void CloseTab(TabViewItem item)
    {
        _panes.Remove(item);
        Tabs.TabItems.Remove(item);
        if (Tabs.TabItems.Count == 0)
        {
            AddNewTab();
        }
        else
        {
            UpdateActiveContent();
        }
    }

    private void Tabs_AddTabButtonClick(TabView sender, object args) => AddNewTab();

    private void Tabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateActiveContent();
        var doc = CurrentDocument;
        FileInfoText.Text = doc?.FilePath != null
            ? $"Markdown · UTF-8 · {doc.FilePath}"
            : "Markdown · UTF-8";
    }

    private void UpdateActiveContent()
    {
        ActiveContent.Content = CurrentPane;
    }
}
