using System;
using System.Collections.ObjectModel;
using MarkdownStudio.Models;
using MarkdownStudio.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MarkdownStudio.Views;

public sealed partial class WelcomeView : UserControl
{
    public ObservableCollection<MruEntry> PinnedEntries { get; } = new();
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

        PinnedEntries.Clear();
        foreach (var e in _mru.Pinned) PinnedEntries.Add(e);

        RecentEntries.Clear();
        foreach (var e in _mru.Recent) RecentEntries.Add(e);

        PinnedSection.Visibility   = PinnedEntries.Count >  0 ? Visibility.Visible : Visibility.Collapsed;
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

    private void OnPinClicked(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem { Tag: MruEntry entry })
            _mru?.SetPinned(entry.Path, !entry.IsPinned);
    }

    private void OnRemoveClicked(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem { Tag: MruEntry entry })
            _mru?.Remove(entry.Path);
    }

    private void OnRevealClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { Tag: MruEntry entry }) return;
        if (string.IsNullOrEmpty(entry.Path)) return;

        try
        {
            // Folders open as-is; files open their parent folder with the file
            // pre-selected via the /select switch.
            var args = entry.Kind == MruKind.Folder
                ? $"\"{entry.Path}\""
                : $"/select,\"{entry.Path}\"";
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = args,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Reveal failed: {ex}");
        }
    }

    private void OnClearAll(object sender, RoutedEventArgs e) => _mru?.Clear();
}
