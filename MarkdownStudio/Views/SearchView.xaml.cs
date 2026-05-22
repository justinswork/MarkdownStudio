using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MarkdownStudio.Models;
using MarkdownStudio.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MarkdownStudio.Views;

public sealed partial class SearchView : UserControl
{
    public ObservableCollection<SearchGroup> Groups { get; } = new();

    public event Action<SearchHit>? HitActivated;

    private string? _folderPath;
    private CancellationTokenSource? _cts;
    private DispatcherTimer? _debounce;

    public SearchView()
    {
        InitializeComponent();
    }

    public string? FolderPath
    {
        get => _folderPath;
        set
        {
            _folderPath = value;
            ScopeText.Text = string.IsNullOrEmpty(value)
                ? "Open a folder to enable search."
                : $"Searching in: {value}";
            QueryBox.IsEnabled = !string.IsNullOrEmpty(value);
            if (string.IsNullOrEmpty(value))
            {
                Groups.Clear();
                StatusText.Text = string.Empty;
                EmptyState.Visibility = Visibility.Visible;
            }
        }
    }

    private void OnQueryTextChanged(object sender, TextChangedEventArgs e)
    {
        _debounce?.Stop();
        _debounce ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(220) };
        _debounce.Tick -= OnDebounceTick;
        _debounce.Tick += OnDebounceTick;
        _debounce.Start();
    }

    private void OnDebounceTick(object? sender, object e)
    {
        _debounce?.Stop();
        _ = RunSearchAsync();
    }

    private async Task RunSearchAsync()
    {
        var query = QueryBox.Text?.Trim() ?? string.Empty;

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        if (string.IsNullOrEmpty(query))
        {
            Groups.Clear();
            StatusText.Text = string.Empty;
            Spinner.IsActive = false;
            EmptyState.Visibility = Visibility.Visible;
            EmptyHint.Text = string.IsNullOrEmpty(_folderPath)
                ? "Open a folder to enable search."
                : "Type to search file names and content.";
            return;
        }

        if (string.IsNullOrEmpty(_folderPath))
        {
            StatusText.Text = "Open a folder first.";
            return;
        }

        Spinner.IsActive = true;
        StatusText.Text  = "Searching…";
        EmptyState.Visibility = Visibility.Collapsed;

        try
        {
            var results = await SearchService.SearchAsync(_folderPath, query, ct);
            if (ct.IsCancellationRequested) return;

            Groups.Clear();
            int totalHits = 0;
            foreach (var g in results)
            {
                Groups.Add(g);
                totalHits += g.HitCount;
            }

            StatusText.Text = Groups.Count switch
            {
                0 => $"No matches for \"{query}\".",
                1 => $"{totalHits} match in 1 file.",
                _ => $"{totalHits} matches in {Groups.Count} files.",
            };
            EmptyState.Visibility = Groups.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            if (Groups.Count == 0) EmptyHint.Text = $"No matches for \"{query}\".";
        }
        catch (OperationCanceledException) { /* superseded by newer query */ }
        catch (Exception ex)
        {
            StatusText.Text = $"Error: {ex.Message}";
        }
        finally
        {
            Spinner.IsActive = false;
        }
    }

    private void OnHitClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: SearchHit hit })
            HitActivated?.Invoke(hit);
    }
}
