using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MarkdownStudio.Models;
using MarkdownStudio.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Text;

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

    private void OnHitTextLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextBlock tb && tb.Tag is SearchHit hit)
            BuildHitInlines(tb, hit);
    }

    private static void BuildHitInlines(TextBlock tb, SearchHit hit)
    {
        tb.Inlines.Clear();

        var secondaryBrush = Application.Current.Resources["MdsTextSecondaryBrush"] as Brush;
        var accentBrush    = Application.Current.Resources["MdsAccentBrush"]        as Brush;
        var accentSoftBrush = Application.Current.Resources["MdsAccentSoftBrush"]   as Brush;

        if (hit.IsFileNameMatch)
        {
            var fn = new Run { Text = "filename match" };
            if (secondaryBrush != null) fn.Foreground = secondaryBrush;
            tb.Inlines.Add(fn);
            return;
        }

        var prefix = new Run { Text = hit.LinePrefix };
        if (secondaryBrush != null) prefix.Foreground = secondaryBrush;
        tb.Inlines.Add(prefix);

        if (hit.MatchStart < 0 || string.IsNullOrEmpty(hit.Query))
        {
            tb.Inlines.Add(new Run { Text = hit.Snippet });
            return;
        }

        if (!string.IsNullOrEmpty(hit.SnippetBeforeMatch))
            tb.Inlines.Add(new Run { Text = hit.SnippetBeforeMatch });

        var match = new Run
        {
            Text = hit.MatchText,
            FontWeight = FontWeights.SemiBold,
        };
        if (accentBrush != null) match.Foreground = accentBrush;
        tb.Inlines.Add(match);

        if (!string.IsNullOrEmpty(hit.SnippetAfterMatch))
            tb.Inlines.Add(new Run { Text = hit.SnippetAfterMatch });
    }
}
