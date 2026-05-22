using System;
using System.Collections.ObjectModel;
using MarkdownStudio.Models;
using MarkdownStudio.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MarkdownStudio.Views;

public sealed partial class WelcomeView : UserControl
{
    public ObservableCollection<MruEntry> RecentEntries { get; } = new();

    public event Action? OpenFolderRequested;
    public event Action? OpenFileRequested;
    public event Action? NewFileRequested;
    public event Action<MruEntry>? MruActivated;

    private MruService? _mru;

    public WelcomeView()
    {
        InitializeComponent();
    }

    public void Attach(MruService mru)
    {
        _mru = mru;
        Refresh();
        mru.Changed += () => DispatcherQueue.TryEnqueue(Refresh);
    }

    private void Refresh()
    {
        if (_mru == null) return;

        RecentEntries.Clear();
        foreach (var e in _mru.Entries) RecentEntries.Add(e);

        EmptyText.Visibility       = RecentEntries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ClearAllButton.Visibility  = RecentEntries.Count >  0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnOpenFolder(object sender, RoutedEventArgs e) => OpenFolderRequested?.Invoke();
    private void OnOpenFile(object sender, RoutedEventArgs e)   => OpenFileRequested?.Invoke();
    private void OnNewFile(object sender, RoutedEventArgs e)    => NewFileRequested?.Invoke();

    private void OnMruClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: MruEntry entry })
            MruActivated?.Invoke(entry);
    }

    private void OnClearAll(object sender, RoutedEventArgs e) => _mru?.Clear();
}
